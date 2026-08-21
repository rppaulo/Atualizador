using System.IO;
using System.Net;
using System.Net.Sockets;
using FluentFTP.Exceptions;

namespace Atulizador.Services;

/// <summary>
/// Traduz os erros técnicos mais comuns (rede, arquivo, permissão) para uma frase curta
/// que um técnico em campo entende, sem precisar saber o que é "timeout" ou
/// "SocketException". O traceback técnico completo continua indo para o log
/// (erros.txt/log_instalacoes.txt) — isso aqui é só o que aparece na tela.
/// Equivalente a mensagem_amigavel_erro() no script Python.
/// </summary>
public static class ErrorMessages
{
    public static string Amigavel(Exception excecao)
    {
        switch (excecao)
        {
            case TimeoutException:
                return "A conexão demorou demais para responder. Confira a internet/rede e tente de novo.";

            case SocketException se when se.SocketErrorCode == SocketError.ConnectionRefused:
                return "A conexão foi recusada pelo servidor. Ele pode estar fora do ar ou bloqueado por firewall.";

            case SocketException se when se.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain:
                return "Não consegui resolver o endereço do servidor (problema de DNS/rede).";

            case SocketException:
                return "Não consegui acessar o servidor (sem internet ou endereço bloqueado).";

            case WebException:
                return "Não consegui acessar o servidor (sem internet ou endereço bloqueado).";

            case FtpException:
                return $"Erro de comunicação com o servidor FTP: {excecao.Message}";

            case UnauthorizedAccessException:
                return "Sem permissão para gravar nesse arquivo/pasta. Confira se o programa está rodando como Administrador.";

            case FileNotFoundException fnf:
                return $"Arquivo ou pasta não encontrado: {fnf.FileName ?? fnf.Message}";

            case DirectoryNotFoundException:
                return $"Arquivo ou pasta não encontrado: {excecao.Message}";

            default:
                return excecao.Message;
        }
    }
}
