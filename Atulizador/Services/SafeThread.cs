using Atulizador.Config;

namespace Atulizador.Services;

/// <summary>
/// Substitui "new Thread(...).Start()"/Task.Run cru em todo o app.
///
/// Uma exceção que escapa de dentro de uma tarefa de background normalmente só
/// derruba a tarefa silenciosamente — numa aplicação WPF compilada como janela (sem
/// console), isso significa que a tela trava numa etapa (ex.: "INSTALANDO...") e não
/// sobra nenhuma pista do que aconteceu.
///
/// Este helper garante que QUALQUER exceção não tratada dentro da tarefa seja: (1)
/// gravada com stack trace completo em erros.txt (ou %TEMP%, se não conseguir escrever
/// lá), e (2) opcionalmente repassada para o log em tela da própria página via
/// <paramref name="logDestino"/>.
///
/// Equivalente a executar_em_thread_segura no script Python.
/// </summary>
public static class SafeThread
{
    public static void Run(Action acao, Action<string>? logDestino = null)
    {
        Task.Run(() =>
        {
            try
            {
                acao();
            }
            catch (Exception ex)
            {
                Logger.RegistrarErroCritico(ex.ToString());
                logDestino?.Invoke($"Erro inesperado — detalhes gravados em {AppConstants.ErrosLogPath}");
            }
        });
    }

    public static void Run(Func<Task> acaoAsync, Action<string>? logDestino = null)
    {
        Task.Run(async () =>
        {
            try
            {
                await acaoAsync();
            }
            catch (Exception ex)
            {
                Logger.RegistrarErroCritico(ex.ToString());
                logDestino?.Invoke($"Erro inesperado — detalhes gravados em {AppConstants.ErrosLogPath}");
            }
        });
    }
}
