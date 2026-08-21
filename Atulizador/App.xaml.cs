using System.Windows;
using System.Windows.Threading;
using Atulizador.Config;
using Atulizador.Services;
using Atulizador.Views;

namespace Atulizador;

/// <summary>
/// Ponto de entrada — equivalente ao bloco "if __name__ == '__main__':" no script Python:
/// confere se está rodando como Administrador (sem pedir elevação automática — ver
/// app.manifest) e só então abre a janela principal.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Qualquer exceção não tratada na thread de UI é registrada em erros.txt antes de
        // decidir o que fazer — sem isso, um app WPF sem console simplesmente fecha sem
        // deixar pista nenhuma (mesmo problema que executar_em_thread_segura resolve para
        // as threads de background).
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        if (!ValidationHelper.IsAdmin())
        {
            MessageBox.Show("Por favor, execute o atualizador como Administrador!", "Falta de Permissão",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Checagem de consistência dos perfis de instalação — não trava o app (um técnico
        // em campo não pode ficar sem ferramenta por causa disso), só deixa registrado em
        // erros.txt para pegarmos antes que vire um chamado confuso.
        var errosPerfis = InstallProfiles.Validar();
        if (errosPerfis.Count > 0)
            Logger.RegistrarErroCritico("Inconsistência em INSTALL_PROFILES:\n" + string.Join("\n", errosPerfis));

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.RegistrarErroCritico(e.Exception.ToString());
        MessageBox.Show($"Erro inesperado — detalhes gravados em {AppConstants.ErrosLogPath}",
            "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
