namespace Atulizador.Services;

/// <summary>
/// Nenhum usuário/senha do FTP fica gravado no código, no disco, nem em lugar nenhum.
/// O técnico digita as próprias credenciais do FTP na tela de login; elas ficam só na
/// memória desta sessão e são descartadas quando o app fecha. O próprio login É o teste:
/// só entra quem autenticar de verdade no servidor FTP.
/// Equivalente a _CREDENCIAIS_SESSAO no script Python.
/// </summary>
public static class SessionCredentials
{
    public static string? Usuario { get; private set; }
    public static string? Senha { get; private set; }

    public static void Definir(string usuario, string senha)
    {
        Usuario = usuario;
        Senha = senha;
    }
}
