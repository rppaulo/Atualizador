using System.Net.Sockets;
using System.Security.Principal;
using System.Text.RegularExpressions;

namespace Atulizador.Services;

/// <summary>
/// Utilitários diversos de sistema/validação — equivalentes a validar_cnpj,
/// testar_conexao_tcp, detectar_ip_local, is_admin e get_arch no script Python.
/// </summary>
public static partial class ValidationHelper
{
    [GeneratedRegex(@"\D")]
    private static partial Regex NaoDigitoRegex();

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}$")]
    public static partial Regex IpRegex();

    /// <summary>Valida um CNPJ conferindo os dois dígitos verificadores.</summary>
    public static bool ValidarCnpj(string? cnpjEntrada)
    {
        var cnpj = NaoDigitoRegex().Replace(cnpjEntrada ?? "", "");
        if (cnpj.Length != 14 || cnpj.Distinct().Count() == 1)
            return false;

        static char Dv(string digitos, int[] pesos)
        {
            var soma = digitos.Select((c, i) => (c - '0') * pesos[i]).Sum();
            var resto = soma % 11;
            return resto < 2 ? '0' : (char)('0' + (11 - resto));
        }

        var pesos1 = new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var pesos2 = new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        var dv1 = Dv(cnpj[..12], pesos1);
        var dv2 = Dv(cnpj[..12] + dv1, pesos2);
        return cnpj[12..] == $"{dv1}{dv2}";
    }

    /// <summary>Testa se dá para abrir uma conexão TCP em ip:porta dentro do timeout.</summary>
    public static bool TestarConexaoTcp(string ip, int porta, int timeoutMs = 2000)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(ip, porta);
            return task.Wait(timeoutMs) && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Descobre o IP local desta máquina na rede, sem depender de nenhum serviço externo
    /// responder (socket UDP "conectado" — nenhum pacote chega a ser enviado).
    /// </summary>
    public static string DetectarIpLocal()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return ((System.Net.IPEndPoint?)socket.LocalEndPoint)?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                return host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString()
                       ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }
    }

    public static bool IsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static string GetArch() => Environment.Is64BitOperatingSystem ? "x64" : "x86";
}
