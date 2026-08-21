using System.Diagnostics;
using System.IO;
using Atulizador.Config;
using Atulizador.Models;
using FluentFTP;

namespace Atulizador.Services;

/// <summary>
/// Lógica de atualização de aplicações (fora da UI) — equivalente aos métodos
/// main_logic / _update_single_item / _process_nfce / _process_dlls /
/// main_logic_retaguarda / _kill_and_track / open_applications de AtualizadorPage no
/// script Python. A página (Views/Pages/AtualizadorPage) só cuida de UI e delega tudo
/// isto aqui.
/// </summary>
public sealed class AtualizadorService
{
    private readonly Action<string> _log;
    private readonly Action<double, string> _progresso;

    public List<AppFechado> AppsFechados { get; } = new();

    public AtualizadorService(Action<string> log, Action<double, string> progresso)
    {
        _log = log;
        _progresso = progresso;
    }

    // --- Modo "Versão RP Info" (pacotes .rar em FTP_INSTALL_BASE_PATH) -----------------
    public async Task ExecutarModoRpInfoAsync(IReadOnlyList<string> selecionados,
        Func<string, Task<string?>> perguntarDiretorio, CancellationToken ct = default)
    {
        var extractor = ExtractorService.LocalizarExtrator();
        if (extractor is null)
        {
            _log("Erro: Não encontrei o WinRAR ou 7-Zip instalado na máquina.");
            return;
        }

        using var ftp = await FtpService.ConectarSessaoAsync(ct);
        try
        {
            var isX64 = ValidationHelper.GetArch() == "x64";
            var dirsForDll = new Dictionary<string, string>();

            foreach (var app in UpdaterApps.Apps)
            {
                if (!selecionados.Contains(app.Name)) continue;
                _log($"Verificando o {app.Name}...");

                var localDir = InstallPathsStore.Localizar(app.Name, app.Exes);
                if (localDir is null)
                {
                    var escolhido = await perguntarDiretorio($"Onde o {app.Name} está instalado?");
                    if (string.IsNullOrEmpty(escolhido)) continue;
                    localDir = escolhido;
                }
                InstallPathsStore.Lembrar(app.Name, localDir);
                if (app.NeedsDll) dirsForDll[app.Name] = localDir;

                var remoteDir = $"{AppConstants.FtpInstallBasePath}/{app.Subdir}";
                if (app.Name == "NFCe")
                    await ProcessarNfceAsync(ftp, remoteDir, localDir, app, isX64, extractor, ct);
                else
                    await AtualizarItemAsync(ftp, remoteDir, localDir, app.Exes, app.Name, extractor, ct);

                foreach (var sub in app.Bundled)
                {
                    var subRemoteDir = $"{AppConstants.FtpInstallBasePath}/{sub.Subdir}";
                    await AtualizarItemAsync(ftp, subRemoteDir, localDir, sub.Exes, $"{sub.Subdir} ({app.Name})", extractor, ct);
                }
            }

            if (dirsForDll.Count > 0)
                await ProcessarDllsAsync(ftp, dirsForDll, extractor, ct);
        }
        finally
        {
            await ftp.Disconnect(ct);
        }
    }

    private async Task AtualizarItemAsync(AsyncFtpClient ftp, string remoteDir, string localDir,
        IReadOnlyList<string> exes, string displayName, ExtractorInfo extractor, CancellationToken ct)
    {
        try
        {
            var rawFiles = await FtpService.ListarNomesAsync(ftp, remoteDir, ct);
            var rars = NomesComExtensao(rawFiles, ".rar");
            if (rars.Count == 0) return;

            var latestRar = rars.OrderBy(f => f, VersionHelper.ByEmbeddedVersionComparer.Instance).Last();
            var remoteV = VersionHelper.ParseVersionFromFilename(latestRar);
            var localExe = CaminhoExeExistente(localDir, exes);
            var localV = VersionHelper.GetFileVersion(localExe);

            if (VersionHelper.CompareVersions(remoteV, localV))
            {
                _log($"Atualizando {displayName} (versão {localV} -> {remoteV})...");
                KillAndTrack(exes, displayName, localExe);
                await DownloadAndExtractAsync(ftp, remoteDir, latestRar, localDir, extractor, displayName, ct);
                _log($"{displayName} atualizado com sucesso!");
            }
            else
            {
                _log($"O {displayName} já está na versão mais recente (v{localV}). Tudo certo!");
            }
        }
        catch (Exception e)
        {
            _log($"Erro ao atualizar o {displayName}: {e.Message}");
        }
    }

    private async Task ProcessarNfceAsync(AsyncFtpClient ftp, string remoteDir, string localDir,
        UpdaterAppConfig app, bool isX64, ExtractorInfo extractor, CancellationToken ct)
    {
        try
        {
            var rawFiles = await FtpService.ListarNomesAsync(ftp, remoteDir, ct);
            var files = rawFiles.Select(Path.GetFileName).Where(f => f is not null).Select(f => f!).ToList();

            List<string> rars;
            List<string> dllRars;
            if (isX64)
            {
                rars = files.Where(f => f.ToLowerInvariant().Contains("x64") && f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
                                         && !f.ToLowerInvariant().Contains("dll")).ToList();
                dllRars = files.Where(f => f.ToLowerInvariant().Contains("dlls nfce_x64- padrao windows 2")
                                            && f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            else
            {
                rars = files.Where(f => !f.ToLowerInvariant().Contains("x64") && f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
                                         && !f.ToLowerInvariant().Contains("dll")).ToList();
                dllRars = new List<string>();
            }

            if (rars.Count == 0) return;

            var latestRar = rars.OrderBy(f => f, VersionHelper.ByEmbeddedVersionComparer.Instance).Last();
            var remoteV = VersionHelper.ParseVersionFromFilename(latestRar);
            var localExe = CaminhoExeExistente(localDir, app.Exes);
            var localV = VersionHelper.GetFileVersion(localExe);

            if (!VersionHelper.CompareVersions(remoteV, localV))
            {
                _log($"NFCe já está atualizado (v{localV}).");
                return;
            }

            _log($"Atualizando pacote NFCe (versão {localV} -> {remoteV})...");
            KillAndTrack(app.Exes, "NFCe", localExe);
            await DownloadAndExtractAsync(ftp, remoteDir, latestRar, localDir, extractor, "NFCe", ct);

            var candidatos = Directory.EnumerateFiles(localDir)
                .Select(Path.GetFileName)
                .Where(f => f is not null && f.ToLowerInvariant().StartsWith("nfce")
                            && f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(f, "NFCe.exe", StringComparison.OrdinalIgnoreCase))
                .Select(f => f!)
                .OrderByDescending(f => File.GetLastWriteTimeUtc(Path.Combine(localDir, f)))
                .ToList();

            if (candidatos.Count > 0)
            {
                var targetPath = Path.Combine(localDir, "NFCe.exe");
                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(Path.Combine(localDir, candidatos[0]), targetPath);
            }

            foreach (var dllRar in dllRars)
                await DownloadAndExtractAsync(ftp, remoteDir, dllRar, localDir, extractor, "DLLs NFCe", ct, ignorarPastas: true);

            _log("NFCe atualizado com sucesso!");
        }
        catch (Exception e)
        {
            _log($"Erro ao extrair o NFCe: {e.Message}");
        }
    }

    private async Task ProcessarDllsAsync(AsyncFtpClient ftp, Dictionary<string, string> dirsForDll,
        ExtractorInfo extractor, CancellationToken ct)
    {
        var remoteDllDir = $"{AppConstants.FtpInstallBasePath}/{AppConstants.DllRpclientSubdir}";
        try
        {
            var rawFiles = await FtpService.ListarNomesAsync(ftp, remoteDllDir, ct);
            var filesDll = rawFiles.Select(Path.GetFileName)
                .Where(f => f is not null && (f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)
                                               || f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
                .Select(f => f!)
                .ToList();
            if (filesDll.Count == 0) return;

            var latestDll = filesDll.OrderBy(f => f, VersionHelper.ByEmbeddedVersionComparer.Instance).Last();
            var tempDllDir = Path.Combine(Path.GetTempPath(), "rpclient_dll");
            Directory.CreateDirectory(tempDllDir);

            if (latestDll.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
            {
                await DownloadAndExtractAsync(ftp, remoteDllDir, latestDll, tempDllDir, extractor, "DLLs Compartilhadas", ct);
            }
            else
            {
                var localPath = Path.Combine(tempDllDir, latestDll);
                await FtpService.BaixarArquivoAsync(ftp, $"{remoteDllDir}/{latestDll}", localPath, ct: ct);
            }

            foreach (var destino in dirsForDll.Values)
            {
                foreach (var item in Directory.EnumerateFileSystemEntries(tempDllDir))
                {
                    var nome = Path.GetFileName(item);
                    if (!nome.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                        && !nome.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var cOrigem = item;
                    var cDestino = Path.Combine(destino, nome);

                    if (File.Exists(cDestino) &&
                        !VersionHelper.CompareVersions(VersionHelper.GetFileVersion(cOrigem), VersionHelper.GetFileVersion(cDestino)))
                        continue;

                    if (File.Exists(cDestino))
                    {
                        try
                        {
                            File.Move(cDestino, $"{cDestino}_{DateTime.Now:yyyyMMddHHmm}.old");
                        }
                        catch
                        {
                            // não crítico — segue tentando copiar por cima
                        }
                    }
                    File.Copy(cOrigem, cDestino, overwrite: true);
                }
            }

            _log("DLLs copiadas com sucesso!");
        }
        catch (Exception e)
        {
            _log($"Erro ao copiar as DLLs: {e.Message}");
        }
    }

    // --- Modo "Versão IMS" (executáveis soltos em FTP_RETAGUARDA_PATH) -----------------
    public async Task ExecutarModoImsAsync(IReadOnlyList<string> selecionados, CancellationToken ct = default)
    {
        using var ftp = await FtpService.ConectarSessaoAsync(ct);
        try
        {
            const string remoteDir = AppConstants.FtpRetaguardaPath;
            foreach (var app in UpdaterApps.Apps)
            {
                if (!selecionados.Contains(app.Name)) continue;

                var localDir = InstallPathsStore.Localizar(app.Name, app.Exes);
                if (localDir is null) continue;
                InstallPathsStore.Lembrar(app.Name, localDir);

                try
                {
                    var rawFiles = await FtpService.ListarNomesAsync(ftp, remoteDir, ct);
                    var files = rawFiles.Select(Path.GetFileName).Where(f => f is not null).Select(f => f!).ToList();

                    string? remoteFile = null;
                    foreach (var e in app.Exes)
                    {
                        remoteFile = files.FirstOrDefault(f => string.Equals(f, e, StringComparison.OrdinalIgnoreCase));
                        if (remoteFile is not null) break;
                    }
                    if (remoteFile is null) continue;

                    var localExe = CaminhoExeExistente(localDir, app.Exes, remoteFile);
                    var tempDir = Path.Combine(Path.GetTempPath(), "rpinfo_retaguarda");
                    Directory.CreateDirectory(tempDir);
                    var tempExe = Path.Combine(tempDir, remoteFile);
                    await FtpService.BaixarArquivoAsync(ftp, $"{remoteDir}/{remoteFile}", tempExe, ct: ct);

                    var remoteV = VersionHelper.GetFileVersion(tempExe);
                    var localV = VersionHelper.GetFileVersion(localExe);
                    if (remoteV != "0.0.0.0" && (VersionHelper.CompareVersions(remoteV, localV) || localV == "0.0.0.0"))
                    {
                        _log($"Baixando e atualizando o {app.Name} [v{remoteV}]...");
                        KillAndTrack(app.Exes, app.Name, localExe);
                        if (File.Exists(localExe)) File.Move(localExe, $"{localExe}_{localV}.old");
                        File.Move(tempExe, localExe);
                        _log($"{app.Name} atualizado (IMS) com sucesso!");
                    }
                    else
                    {
                        File.Delete(tempExe);
                    }
                }
                catch (Exception e)
                {
                    _log($"Erro na atualização IMS: {e.Message}");
                }
            }
        }
        finally
        {
            await ftp.Disconnect(ct);
        }
    }

    // --- Compartilhado ------------------------------------------------------------------
    private async Task DownloadAndExtractAsync(AsyncFtpClient ftp, string remoteDir, string filename, string localDir,
        ExtractorInfo extractor, string displayName, CancellationToken ct, bool ignorarPastas = false)
    {
        var localRar = Path.Combine(localDir, "temp_update.rar");
        if (File.Exists(localRar)) File.Delete(localRar);

        await FtpService.BaixarArquivoAsync(ftp, $"{remoteDir}/{filename}", localRar,
            (baixado, total) =>
            {
                if (total.HasValue)
                    _progresso((double)baixado / total.Value, $"Baixando {displayName}...");
            }, ct);

        ExtractorService.ExecutarExtracao(extractor, localRar, localDir, ignorarPastas);
        File.Delete(localRar);
    }

    private static List<string> NomesComExtensao(IEnumerable<string> caminhosRemotos, string extensao) =>
        caminhosRemotos.Select(Path.GetFileName)
            .Where(f => f is not null && f.EndsWith(extensao, StringComparison.OrdinalIgnoreCase))
            .Select(f => f!)
            .ToList();

    private static string CaminhoExeExistente(string localDir, IReadOnlyList<string> exes, string? padrao = null)
    {
        foreach (var exe in exes)
        {
            var caminho = Path.Combine(localDir, exe);
            if (File.Exists(caminho)) return caminho;
        }
        return Path.Combine(localDir, padrao ?? exes[0]);
    }

    private void KillAndTrack(IReadOnlyList<string> exes, string displayName, string exePath)
    {
        var nomes = exes.Select(e => Path.GetFileNameWithoutExtension(e).ToLowerInvariant()).ToHashSet();
        var matou = false;
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (!nomes.Contains(proc.ProcessName.ToLowerInvariant())) continue;
                proc.Kill();
                matou = true;
            }
            catch
            {
                // processo de sistema/sem permissão — ignora
            }
        }
        if (matou) AppsFechados.Add(new AppFechado(displayName, exePath));
    }

    private static readonly HashSet<string> IgnorarReabertura = new(StringComparer.OrdinalIgnoreCase)
        { "imparq.exe", "impflex.exe", "procarq.exe", "envlog.exe" };

    /// <summary>Reabre os apps fechados durante a atualização (exceto os que não devem reabrir sozinhos).</summary>
    public void AbrirAplicacoesFechadas()
    {
        foreach (var (nome, caminho) in AppsFechados)
        {
            var exeName = Path.GetFileName(caminho);
            if (IgnorarReabertura.Contains(exeName))
            {
                _log($"O {nome} foi atualizado, mas está configurado para não abrir automaticamente.");
                continue;
            }
            if (File.Exists(caminho))
            {
                Process.Start(new ProcessStartInfo(caminho) { WorkingDirectory = Path.GetDirectoryName(caminho), UseShellExecute = true });
                _log($"Abrindo o {nome}...");
            }
        }
        AppsFechados.Clear();
    }
}
