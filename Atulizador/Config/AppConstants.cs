using System.IO;

namespace Atulizador.Config;

/// <summary>
/// Configurações globais do aplicativo — equivalente às constantes no topo do script Python.
/// </summary>
public static class AppConstants
{
    // --- FTP / caminhos remotos ---
    public const string FtpServer = "ftp.rpinfo.com.br";
    public const string FtpInstallBasePath = "/install/frente";
    public const string FtpRetaguardaPath = "/Paulo/Retaguarda";
    public const string FtpInstalacaoPath = "/Paulo/Instalacao";
    public const string DefaultLocalBaseDir = "C:\\wrpdv";
    public const string DllRpclientSubdir = "DLL Rpclient";

    public const string AppVersion = "3.00.00";
    public const string AppNome = "IMS - Toolkit";

    /// <summary>
    /// Enquanto true, exige login com usuário/senha do FTP antes de liberar o app.
    /// Deixe false só para testar telas sem depender do FTP.
    /// </summary>
    public const bool ExigirLoginFtp = true;

    public const string ModeRpInfo = "Versão RP Info";
    public const string ModeIms = "Versão IMS";

    public static readonly string ProgramDataDir = Path.Combine(
        Environment.GetEnvironmentVariable("PROGRAMDATA") ?? "C:\\ProgramData",
        "RPInfo", "Atualizador");

    public static readonly string InstallPathsFile = Path.Combine(ProgramDataDir, "install_paths.json");
    public static readonly string LogAuditoriaPath = Path.Combine(ProgramDataDir, "log_instalacoes.txt");
    public static readonly string ErrosLogPath = Path.Combine(ProgramDataDir, "erros.txt");
    public static readonly string PastaTempFallback = Path.GetTempPath();

    // --- Auto-atualização via GitHub Releases ---
    public const string GitHubOwnerRepo = "rppaulo/Atualizador";
    public static readonly string GitHubApiReleasesLatest = $"https://api.github.com/repos/{GitHubOwnerRepo}/releases/latest";
}
