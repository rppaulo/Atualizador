using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Atulizador.Config;
using Atulizador.Models;

namespace Atulizador.Services;

/// <summary>
/// Auto-atualização do próprio IMS Toolkit via GitHub Releases — sem depender do FTP da
/// RP Info para isso. Consulta a versão mais recente publicada, compara com AppVersion, e
/// baixa o asset certo (.exe/.zip/.rar). Se publicado, confere o SHA-256 antes de trocar
/// o executável — a troca só acontece se o hash bater.
///
/// Como o app roda sempre compilado (ao contrário do original em Python, que também podia
/// rodar como script .py direto pelo interpretador), aqui só existe o caminho equivalente
/// a "rodando congelado": o Windows tranca o .exe em uso, então a troca é feita por um
/// .bat que espera o processo atual fechar, copia o .exe novo por cima, reabre e se
/// autodeleta. Pressupõe publicação em modo single-file (um .exe só).
///
/// Equivalente a verificar_atualizacao_toolkit_github / aplicar_atualizacao_toolkit_github /
/// calcular_sha256 no script Python.
/// </summary>
public static class SelfUpdateService
{
    private static readonly HttpClient Http = CriarHttpClient();

    private static HttpClient CriarHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("User-Agent", "IMS-Toolkit-AutoUpdate");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return client;
    }

    public static async Task<GithubUpdateInfo?> VerificarAtualizacaoAsync(CancellationToken ct = default)
    {
        GithubReleaseDto? dados;
        try
        {
            using var resposta = await Http.GetAsync(AppConstants.GitHubApiReleasesLatest, ct);
            resposta.EnsureSuccessStatusCode();
            var json = await resposta.Content.ReadAsStringAsync(ct);
            dados = JsonSerializer.Deserialize<GithubReleaseDto>(json);
        }
        catch
        {
            // sem internet / repositório não configurado — nunca deve travar o app
            return null;
        }

        if (dados is null) return null;

        var versaoRemota = (dados.TagName ?? "").TrimStart('v', 'V');
        if (string.IsNullOrEmpty(versaoRemota) || !VersionHelper.CompareVersions(versaoRemota, AppConstants.AppVersion))
            return null;

        string[] extensoesPreferidas = { ".exe", ".zip", ".rar" };
        GithubAssetDto? escolhido = null;
        foreach (var ext in extensoesPreferidas)
        {
            escolhido = dados.Assets.FirstOrDefault(a => a.Name.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
            if (escolhido is not null) break;
        }

        if (escolhido is null) return null;

        string? hashEsperado = null;
        var nomeHashEsperado = escolhido.Name + ".sha256";
        var assetHash = dados.Assets.FirstOrDefault(a => a.Name == nomeHashEsperado);
        if (assetHash is not null)
        {
            try
            {
                var conteudo = (await Http.GetStringAsync(assetHash.BrowserDownloadUrl, ct)).Trim();
                // aceita tanto "HASH" puro quanto o formato "HASH  nome_arquivo" do sha256sum
                hashEsperado = conteudo.Length > 0 ? conteudo.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant() : null;
            }
            catch
            {
                hashEsperado = null;
            }
        }

        return new GithubUpdateInfo
        {
            Versao = versaoRemota,
            UrlDownload = escolhido.BrowserDownloadUrl,
            NomeArquivo = escolhido.Name,
            HashEsperado = hashEsperado,
            Notas = (dados.Body ?? "").Trim(),
        };
    }

    public static string CalcularSha256(string caminho)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(caminho);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? AcharArquivoExtraido(string pasta, string extensao) =>
        Directory.EnumerateFiles(pasta).FirstOrDefault(f => f.EndsWith(extensao, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Baixa e aplica a atualização. Levanta exceção em caso de falha — e nesse caso NADA
    /// é trocado, o app continua na versão atual normalmente. Se tudo correr bem, encerra
    /// o processo atual (os._exit equivalente) para liberar o .exe e deixar o .bat copiar
    /// por cima e reabrir.
    /// </summary>
    public static async Task AplicarAtualizacaoAsync(GithubUpdateInfo info, Action<string, string> log, CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ims_toolkit_autoupdate");
        Directory.CreateDirectory(tempDir);
        var localPath = Path.Combine(tempDir, info.NomeArquivo);

        log($"Baixando {info.NomeArquivo} do GitHub...", "info");
        long? tamanhoEsperado;
        using (var resposta = await Http.GetAsync(info.UrlDownload, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resposta.EnsureSuccessStatusCode();
            tamanhoEsperado = resposta.Content.Headers.ContentLength;
            await using var origem = await resposta.Content.ReadAsStreamAsync(ct);
            await using var destino = File.Create(localPath);
            await origem.CopyToAsync(destino, ct);
        }

        var tamanhoBaixado = new FileInfo(localPath).Length;
        if (tamanhoEsperado.HasValue && tamanhoBaixado != tamanhoEsperado.Value)
        {
            File.Delete(localPath);
            throw new InvalidOperationException(
                $"Download incompleto: esperava {tamanhoEsperado} bytes, recebi {tamanhoBaixado}. " +
                "Provavelmente a conexão caiu no meio — tente novamente.");
        }

        // Verificação de integridade: como essa atualização troca o .exe sozinha, não
        // seguimos adiante se o hash publicado na release não bater (indica arquivo
        // corrompido OU adulterado no meio do caminho).
        if (!string.IsNullOrEmpty(info.HashEsperado))
        {
            log("Conferindo integridade do arquivo baixado...", "info");
            var hashReal = CalcularSha256(localPath);
            if (!string.Equals(hashReal, info.HashEsperado, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(localPath);
                throw new InvalidOperationException(
                    $"O arquivo baixado não bate com o hash publicado na release " +
                    $"(esperado {info.HashEsperado[..12]}..., calculado {hashReal[..12]}...). " +
                    "Atualização CANCELADA por segurança.");
            }
        }
        else
        {
            log("Release não publicou um \".sha256\" para conferência — seguindo sem essa " +
                "verificação extra (recomendo sempre publicar).", "warning");
        }

        string extraidoDir;
        if (info.NomeArquivo.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            extraidoDir = Path.Combine(tempDir, "extraido");
            Directory.CreateDirectory(extraidoDir);
            log("Extraindo...", "info");
            ZipFile.ExtractToDirectory(localPath, extraidoDir, overwriteFiles: true);
        }
        else if (info.NomeArquivo.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
        {
            var extractor = ExtractorService.LocalizarExtrator();
            if (extractor is null)
            {
                log("Nenhum extrator (WinRAR/7-Zip) encontrado — tentando instalar o WinRAR...", "warning");
                extractor = await ExtractorService.InstalarWinRarViaFtpAsync(log, ct);
                if (extractor is null)
                    throw new InvalidOperationException(
                        "Não foi possível localizar nem instalar um extrator (WinRAR/7-Zip) para abrir o pacote .rar da atualização.");
            }
            extraidoDir = Path.Combine(tempDir, "extraido");
            Directory.CreateDirectory(extraidoDir);
            log("Extraindo...", "info");
            var codigoSaida = ExtractorService.ExecutarExtracao(extractor, localPath, extraidoDir);
            if (codigoSaida != 0)
                throw new InvalidOperationException($"O extrator retornou erro (código {codigoSaida}) ao abrir {info.NomeArquivo}.");
        }
        else
        {
            extraidoDir = tempDir; // o próprio arquivo baixado já é o .exe final
        }

        var novoExe = localPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? localPath
            : AcharArquivoExtraido(extraidoDir, ".exe");
        if (novoExe is null)
            throw new InvalidOperationException("Não encontrei um .exe no pacote baixado do GitHub.");

        var exeAtual = Environment.ProcessPath ?? throw new InvalidOperationException("Não foi possível determinar o executável atual.");
        var batPath = Path.Combine(tempDir, "atualizar.bat");
        var conteudoBat =
            "@echo off\r\n" +
            "timeout /t 2 /nobreak > nul\r\n" +
            $"copy /Y \"{novoExe}\" \"{exeAtual}\"\r\n" +
            $"start \"\" \"{exeAtual}\"\r\n" +
            "del \"%~f0\"\r\n";
        await File.WriteAllTextAsync(batPath, conteudoBat, ct);

        log("Reiniciando com a nova versão...", "success");
        Logger.RegistrarAuditoria($"Auto-atualização do IMS Toolkit via GitHub iniciada ({info.NomeArquivo}).");

        var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c \"{batPath}\"")
        {
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
        };
        System.Diagnostics.Process.Start(psi);

        // Encerra JÁ, sem esperar cleanup de UI, para liberar o .exe pro .bat copiar por cima.
        Environment.Exit(0);
    }
}
