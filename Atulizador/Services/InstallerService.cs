using System.IO;
using Atulizador.Config;

namespace Atulizador.Services;

/// <summary>
/// Baixa o pacote de instalação completo (.rar) do FTP e extrai na pasta padrão
/// C:\wrpdv\&lt;appKey&gt;. Instala o WinRAR automaticamente se não achar nenhum extrator.
/// Equivalente a instalar_app_via_ftp no script Python.
/// </summary>
public static class InstallerService
{
    public static async Task<string> InstalarAppViaFtpAsync(string appKey, Action<string, string> log,
        Action<double, string>? progresso = null, CancellationToken ct = default)
    {
        var localizacao = InstallProfiles.AppLocalizacao.GetValueOrDefault(appKey);
        var palavrasChave = localizacao?.PalavrasChaveInstalacao ?? new List<string> { appKey.ToLowerInvariant() };
        var destino = Path.Combine(AppConstants.DefaultLocalBaseDir, appKey);
        Directory.CreateDirectory(destino);

        var extractor = ExtractorService.LocalizarExtrator();
        if (extractor is null)
        {
            log("Nenhum extrator (WinRAR/7-Zip) encontrado — tentando instalar o WinRAR...", "warning");
            extractor = await ExtractorService.InstalarWinRarViaFtpAsync(log, ct);
            if (extractor is null)
                throw new InvalidOperationException("Não foi possível localizar nem instalar um extrator (WinRAR/7-Zip).");
        }

        string arquivo;
        long? tamanho;
        var localRar = Path.Combine(destino, "_instalacao_temp.rar");

        using (var ftp = await FtpService.ConectarSessaoAsync(ct))
        {
            try
            {
                var arquivos = await FtpService.ListarNomesAsync(ftp, AppConstants.FtpInstalacaoPath, ct);
                var candidatos = arquivos
                    .Select(Path.GetFileName)
                    .Where(f => f is not null && f.ToLowerInvariant().EndsWith(".rar")
                                && palavrasChave.Any(p => f.ToLowerInvariant().Contains(p)))
                    .Select(f => f!)
                    .OrderBy(f => f, StringComparer.Ordinal)
                    .ToList();
                if (candidatos.Count == 0)
                    throw new InvalidOperationException(
                        $"Não encontrei nenhum .rar para \"{appKey}\" em {AppConstants.FtpInstalacaoPath}.");
                // se houver mais de um, pega o último por ordem alfabética/versão
                arquivo = candidatos[^1];
                log($"Baixando {arquivo}...", "info");

                if (File.Exists(localRar)) File.Delete(localRar);
                tamanho = await FtpService.TamanhoAsync(ftp, $"{AppConstants.FtpInstalacaoPath}/{arquivo}", ct);

                await FtpService.BaixarArquivoAsync(ftp, $"{AppConstants.FtpInstalacaoPath}/{arquivo}", localRar,
                    (baixado, total) =>
                    {
                        if (total.HasValue)
                            progresso?.Invoke((double)baixado / total.Value, $"Baixando {arquivo}...");
                    }, ct);
            }
            finally
            {
                await ftp.Disconnect(ct);
            }
        }

        var tamanhoBaixadoFinal = new FileInfo(localRar).Length;
        if (tamanho.HasValue && tamanhoBaixadoFinal != tamanho.Value)
        {
            File.Delete(localRar);
            throw new InvalidOperationException(
                $"Download incompleto: esperava {tamanho} bytes, recebi {tamanhoBaixadoFinal}. " +
                "Provavelmente a conexão caiu no meio — tente novamente.");
        }

        log($"Extraindo {arquivo}...", "info");
        progresso?.Invoke(1.0, $"Extraindo {arquivo}...");

        // Extrai numa pasta temporária em vez de direto no destino final: alguns .rar já
        // são empacotados contendo a própria pasta do app dentro deles, o que duplicaria
        // o caminho (C:\wrpdv\ServerMatriz\ServerMatriz). Extraindo à parte e "desembrulhando"
        // depois, isso nunca acontece.
        var stagingDir = Path.Combine(destino, $"_extract_tmp_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        Directory.CreateDirectory(stagingDir);
        var codigoSaida = ExtractorService.ExecutarExtracao(extractor, localRar, stagingDir);
        File.Delete(localRar);

        if (codigoSaida != 0)
            throw new InvalidOperationException(
                $"O extrator retornou erro (código {codigoSaida}) ao extrair {arquivo}. " +
                "Verifique se o arquivo baixado não está corrompido.");

        ExtractorService.NormalizarExtracao(stagingDir, destino, log);

        // Registra onde foi instalado — assim a etapa de configuração seguinte não precisa
        // perguntar de novo, e outros apps que espelham valores deste também já sabem achar o .ini aqui.
        InstallPathsStore.Lembrar(localizacao?.NomeApp ?? appKey, destino);
        Logger.RegistrarAuditoria($"Instalado \"{appKey}\" (pacote {arquivo}) em {destino}");

        log($"{appKey} extraído com sucesso em {destino}.", "success");
        return destino;
    }
}
