using System.Diagnostics;
using System.IO;
using Atulizador.Config;

namespace Atulizador.Services;

public enum ExtractorType { WinRar, SevenZip }

public sealed record ExtractorInfo(string Path, ExtractorType Tipo);

/// <summary>
/// Localiza/instala o WinRAR ou 7-Zip, executa extrações e "desembrulha" pacotes que
/// vieram com uma subpasta extra. Equivalente a localizar_extrator, instalar_winrar_via_ftp
/// e _normalizar_extracao no script Python.
/// </summary>
public static class ExtractorService
{
    public static ExtractorInfo? LocalizarExtrator()
    {
        var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? "C:\\Program Files";
        var candidatos = new (string Path, ExtractorType Tipo)[]
        {
            (System.IO.Path.Combine(programFiles, "WinRAR", "WinRAR.exe"), ExtractorType.WinRar),
            (System.IO.Path.Combine(programFiles, "7-Zip", "7z.exe"), ExtractorType.SevenZip),
        };
        foreach (var candidato in candidatos)
        {
            if (File.Exists(candidato.Path))
                return new ExtractorInfo(candidato.Path, candidato.Tipo);
        }
        return null;
    }

    /// <summary>
    /// Procura um instalador do WinRAR dentro de FTP_INSTALACAO_PATH, baixa e instala
    /// silenciosamente (/S). Retorna o extrator localizado depois, ou null se não conseguir.
    /// </summary>
    public static async Task<ExtractorInfo?> InstalarWinRarViaFtpAsync(Action<string, string> log, CancellationToken ct = default)
    {
        using var ftp = await FtpService.ConectarSessaoAsync(ct);
        string arquivo;
        long? tamanhoEsperado;
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ims_toolkit_winrar");
        string localPath;
        try
        {
            var arquivos = await FtpService.ListarNomesAsync(ftp, AppConstants.FtpInstalacaoPath, ct);
            var candidatos = arquivos
                .Select(System.IO.Path.GetFileName)
                .Where(f => f is not null && f.ToLowerInvariant().Contains("winrar") && f.ToLowerInvariant().EndsWith(".exe"))
                .Select(f => f!)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            if (candidatos.Count == 0)
            {
                log($"Não encontrei um instalador do WinRAR (.exe) em {AppConstants.FtpInstalacaoPath}.", "error");
                return null;
            }
            arquivo = candidatos[^1];

            Directory.CreateDirectory(tempDir);
            localPath = System.IO.Path.Combine(tempDir, arquivo);
            log($"Baixando instalador do WinRAR ({arquivo})...", "info");
            tamanhoEsperado = await FtpService.TamanhoAsync(ftp, $"{AppConstants.FtpInstalacaoPath}/{arquivo}", ct);
            await FtpService.BaixarArquivoAsync(ftp, $"{AppConstants.FtpInstalacaoPath}/{arquivo}", localPath, ct: ct);
        }
        finally
        {
            await ftp.Disconnect(ct);
        }

        var tamanhoBaixado = new FileInfo(localPath).Length;
        if (tamanhoEsperado.HasValue && tamanhoBaixado != tamanhoEsperado.Value)
        {
            File.Delete(localPath);
            log($"Download do WinRAR incompleto (esperava {tamanhoEsperado} bytes, recebi {tamanhoBaixado}) — " +
                "provavelmente a conexão caiu. Tente novamente.", "error");
            return null;
        }

        log("Instalando o WinRAR silenciosamente...", "info");
        try
        {
            var psi = new ProcessStartInfo(localPath, "/S") { UseShellExecute = true };
            using var proc = Process.Start(psi);
            if (proc is null || !proc.WaitForExit(180_000))
            {
                log("Tempo esgotado ao instalar o WinRAR.", "error");
                return null;
            }
        }
        catch (Exception e)
        {
            log($"Falha ao instalar o WinRAR automaticamente: {e.Message}", "error");
            return null;
        }

        return LocalizarExtrator();
    }

    /// <summary>
    /// Move o conteúdo extraído (stagingDir) para o destino final. Se o pacote veio com
    /// tudo dentro de uma única subpasta, entende que essa subpasta é só o "invólucro" e
    /// move o CONTEÚDO dela — nunca a subpasta em si — evitando duplicar o caminho.
    /// </summary>
    public static void NormalizarExtracao(string stagingDir, string destino, Action<string, string> log)
    {
        var itens = Directory.GetFileSystemEntries(stagingDir);
        var origem = stagingDir;
        if (itens.Length == 1 && Directory.Exists(itens[0]))
        {
            var nomeSub = System.IO.Path.GetFileName(itens[0]);
            log($"O pacote trazia uma subpasta extra (\"{nomeSub}\") — ajustando para não duplicar a pasta.", "info");
            origem = itens[0];
            itens = Directory.GetFileSystemEntries(origem);
        }

        foreach (var itemOrigem in itens)
        {
            var nome = System.IO.Path.GetFileName(itemOrigem);
            var itemDestino = System.IO.Path.Combine(destino, nome);
            if (Directory.Exists(itemDestino)) Directory.Delete(itemDestino, true);
            else if (File.Exists(itemDestino)) File.Delete(itemDestino);

            if (Directory.Exists(itemOrigem)) Directory.Move(itemOrigem, itemDestino);
            else File.Move(itemOrigem, itemDestino);
        }

        Directory.Delete(stagingDir, true);
    }

    /// <summary>Executa a extração de um .rar/.zip local e retorna o código de saída do processo.</summary>
    public static int ExecutarExtracao(ExtractorInfo extractor, string arquivo, string destino, bool ignorarPastas = false)
    {
        var comandoExtracao = ignorarPastas ? "e" : "x";
        var argumentos = extractor.Tipo == ExtractorType.SevenZip
            ? $"{comandoExtracao} -y \"{arquivo}\" -o\"{destino}\""
            : $"{comandoExtracao} -o+ \"{arquivo}\" \"{destino}\"";

        var psi = new ProcessStartInfo(extractor.Path, argumentos)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Não foi possível iniciar o extrator.");
        proc.WaitForExit();
        return proc.ExitCode;
    }
}
