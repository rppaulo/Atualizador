using System.IO;
using Atulizador.Config;
using FluentFTP;

namespace Atulizador.Services;

/// <summary>
/// Conexão e operações FTP — equivalente a _conectar_ftp_com / conectar_ftp /
/// validar_login_ftp no script Python. Usa a biblioteca FluentFTP (pacote NuGet) no
/// lugar do ftplib da stdlib do Python.
///
/// Igual ao original: tenta primeiro FTPS explícito (equivalente a ftplib.FTP_TLS) e,
/// se falhar, cai para FTP simples sem criptografia.
/// </summary>
public static class FtpService
{
    private const int ConnectTimeoutMs = 15000;

    public static async Task<AsyncFtpClient> ConectarAsync(string usuario, string senha, CancellationToken ct = default)
    {
        var ftps = new AsyncFtpClient(AppConstants.FtpServer, usuario, senha);
        ftps.Config.EncryptionMode = FtpEncryptionMode.Explicit;
        ftps.Config.ValidateAnyCertificate = true;
        ftps.Config.ConnectTimeout = ConnectTimeoutMs;
        ftps.Config.ReadTimeout = ConnectTimeoutMs;
        try
        {
            await ftps.Connect(ct);
            return ftps;
        }
        catch
        {
            ftps.Dispose();
        }

        var ftp = new AsyncFtpClient(AppConstants.FtpServer, usuario, senha);
        ftp.Config.EncryptionMode = FtpEncryptionMode.None;
        ftp.Config.ConnectTimeout = ConnectTimeoutMs;
        ftp.Config.ReadTimeout = ConnectTimeoutMs;
        await ftp.Connect(ct);
        return ftp;
    }

    /// <summary>Conecta usando as credenciais que o técnico digitou na tela de login.</summary>
    public static Task<AsyncFtpClient> ConectarSessaoAsync(CancellationToken ct = default)
    {
        if (SessionCredentials.Usuario is null || SessionCredentials.Senha is null)
            throw new InvalidOperationException("Sessão não autenticada — faça login novamente.");
        return ConectarAsync(SessionCredentials.Usuario, SessionCredentials.Senha, ct);
    }

    /// <summary>Tenta autenticar no FTP com as credenciais informadas na tela de login.</summary>
    public static async Task<(bool Ok, Exception? Erro)> ValidarLoginAsync(string usuario, string senha)
    {
        try
        {
            using var ftp = await ConectarAsync(usuario, senha);
            await ftp.Disconnect();
            return (true, null);
        }
        catch (Exception e)
        {
            return (false, e);
        }
    }

    public static Task<string[]> ListarNomesAsync(AsyncFtpClient ftp, string caminhoRemoto, CancellationToken ct = default)
        => ftp.GetNameListing(caminhoRemoto, ct);

    public static async Task<long?> TamanhoAsync(AsyncFtpClient ftp, string caminhoRemoto, CancellationToken ct = default)
    {
        try
        {
            var tamanho = await ftp.GetFileSize(caminhoRemoto, -1, ct);
            return tamanho >= 0 ? tamanho : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Baixa um arquivo remoto para o disco, chamando <paramref name="progresso"/> a cada
    /// bloco lido com (bytesBaixados, tamanhoTotalOuNull) — equivalente ao callback do
    /// ftp.retrbinary() no Python.
    /// </summary>
    public static async Task BaixarArquivoAsync(AsyncFtpClient ftp, string caminhoRemoto, string caminhoLocal,
        Action<long, long?>? progresso = null, CancellationToken ct = default)
    {
        var tamanho = await TamanhoAsync(ftp, caminhoRemoto, ct);
        await using var origem = await ftp.OpenRead(caminhoRemoto, token: ct);
        await using var destino = File.Create(caminhoLocal);
        var buffer = new byte[81920];
        long total = 0;
        int lidos;
        while ((lidos = await origem.ReadAsync(buffer, ct)) > 0)
        {
            await destino.WriteAsync(buffer.AsMemory(0, lidos), ct);
            total += lidos;
            progresso?.Invoke(total, tamanho);
        }
    }
}
