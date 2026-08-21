using Atulizador.Models;

namespace Atulizador.Config;

/// <summary>
/// Lista de apps gerenciados pela página Atualizador — equivalente a self.apps_config
/// dentro de AtualizadorPage.__init__ no script Python.
/// </summary>
public static class UpdaterApps
{
    public static readonly List<UpdaterAppConfig> Apps = new()
    {
        new UpdaterAppConfig { Name = "BuscaPreco", Subdir = "BuscaPreco", Exes = new() { "BuscaPreco.exe" } },
        new UpdaterAppConfig
        {
            Name = "ClientRP", Subdir = "ClientRP",
            Exes = new() { "ClientRP.exe", "Clienrrp.exe", "ClienrRP.exe" },
        },
        new UpdaterAppConfig
        {
            Name = "CredRP", Subdir = "CredRP",
            Exes = new() { "CredRP.exe", "ClienrRP.exe", "Clienrrp.exe", "ClientRP.exe" },
        },
        new UpdaterAppConfig { Name = "NFCe", Subdir = "NFC-e", Exes = new() { "NFCe.exe" } },
        new UpdaterAppConfig
        {
            Name = "ServerMatriz", Subdir = "ServerMatriz", Exes = new() { "ZServerMatriz.exe" },
            Bundled = new()
            {
                new BundledApp { Subdir = "ImpArq", Exes = new() { "ImpArq.exe" } },
                new BundledApp { Subdir = "ProcArq", Exes = new() { "ProcArq.exe" } },
                new BundledApp { Subdir = "ImpFlex", Exes = new() { "ImpFlex.exe" } },
            },
            NeedsDll = true,
        },
        new UpdaterAppConfig
        {
            Name = "ServerUn", Subdir = "ServerUn", Exes = new() { "ServerUN.exe" },
            Bundled = new() { new BundledApp { Subdir = "EnvLog", Exes = new() { "EnvLog.exe" } } },
            NeedsDll = true,
        },
        new UpdaterAppConfig { Name = "WRpdv", Subdir = "WRpdv", Exes = new() { "WRpdv.exe" }, NeedsDll = true },
    };
}
