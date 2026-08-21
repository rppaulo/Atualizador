using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Atulizador.Config;

namespace Atulizador.Services;

/// <summary>
/// Persiste (install_paths.json em ProgramData) onde cada app foi instalado/encontrado,
/// e localiza esses diretórios pelo processo em execução quando o cache não tem a
/// resposta. Equivalente a load_install_paths/save_install_paths/lembrar_diretorio_instalado/
/// localizar_diretorio_instalado no script Python.
/// </summary>
public static class InstallPathsStore
{
    private static readonly object FileLock = new();

    public static Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(AppConstants.InstallPathsFile))
            {
                var json = File.ReadAllText(AppConstants.InstallPathsFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (dict != null) return dict;
            }
        }
        catch
        {
            // arquivo ausente/corrompido — segue com dicionário vazio
        }
        return new Dictionary<string, string>();
    }

    public static void Save(Dictionary<string, string> caminhos)
    {
        try
        {
            lock (FileLock)
            {
                Directory.CreateDirectory(AppConstants.ProgramDataDir);
                var json = JsonSerializer.Serialize(caminhos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(AppConstants.InstallPathsFile, json);
            }
        }
        catch
        {
            // não é crítico — próxima execução perguntará de novo
        }
    }

    /// <summary>Registra em install_paths.json onde um app foi instalado/encontrado.</summary>
    public static void Lembrar(string appKey, string? localDir)
    {
        if (string.IsNullOrEmpty(localDir)) return;
        var caminhos = Load();
        caminhos[appKey] = localDir;
        Save(caminhos);
    }

    /// <summary>
    /// Primeiro tenta o caminho já conhecido (install_paths.json), depois tenta achar
    /// pelo processo em execução (por nome de .exe).
    /// </summary>
    public static string? Localizar(string appName, IReadOnlyList<string> exes)
    {
        var salvo = Load().GetValueOrDefault(appName);
        if (!string.IsNullOrEmpty(salvo) && Directory.Exists(salvo))
            return salvo;

        var nomesExe = exes.Select(e => Path.GetFileNameWithoutExtension(e).ToLowerInvariant()).ToHashSet();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (!nomesExe.Contains(proc.ProcessName.ToLowerInvariant())) continue;
                var caminho = proc.MainModule?.FileName;
                if (!string.IsNullOrEmpty(caminho))
                    return Path.GetDirectoryName(caminho);
            }
            catch
            {
                // processos de sistema/sem permissão de leitura do módulo — ignora
            }
        }
        return null;
    }
}
