using System.IO;
using Atulizador.Config;

namespace Atulizador.Services;

/// <summary>
/// Grava logs em disco em ProgramData, com fallback para %TEMP% se não conseguir
/// escrever lá (permissão, antivírus, disco C: bloqueado etc.) — um log que "às vezes"
/// não é gravado é pior que não ter log nenhum: ninguém sabe que faltou.
/// Equivalente a registrar_log_auditoria / registrar_erro_critico no script Python.
/// </summary>
public static class Logger
{
    private static readonly object FileLock = new();

    public static void RegistrarAuditoria(string mensagem)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var maquina = Environment.MachineName;
        GravarComFallback(AppConstants.LogAuditoriaPath, "ims_toolkit_log.txt", $"[{timestamp}] [{maquina}] {mensagem}\n");
    }

    public static void RegistrarErroCritico(string texto)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var maquina = Environment.MachineName;
        var linha = $"\n[{timestamp}] [{maquina}]\n{texto}\n{new string('-', 70)}\n";
        GravarComFallback(AppConstants.ErrosLogPath, "ims_toolkit_erros.txt", linha);
    }

    private static bool GravarComFallback(string caminhoPrincipal, string nomeArquivoFallback, string linha)
    {
        var caminhoFallback = Path.Combine(AppConstants.PastaTempFallback, nomeArquivoFallback);
        foreach (var caminho in new[] { caminhoPrincipal, caminhoFallback })
        {
            try
            {
                lock (FileLock)
                {
                    var dir = Path.GetDirectoryName(caminho);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    File.AppendAllText(caminho, linha);
                }
                return true;
            }
            catch
            {
                // tenta o próximo caminho da lista
            }
        }
        return false;
    }
}
