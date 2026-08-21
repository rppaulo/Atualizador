using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Atulizador.Services;

namespace Atulizador.Views;

/// <summary>
/// Tela de login — usuário e senha do FTP da RP Info. Nenhuma credencial fica salva em
/// lugar nenhum: o próprio login testa contra o servidor FTP de verdade. Se autenticar,
/// libera o app para essa sessão; se não, não passa. Equivalente a TelaLogin no script Python.
/// </summary>
public partial class LoginView : UserControl
{
    public event Action<string>? LoginSucedido;

    public LoginView()
    {
        InitializeComponent();
        Placeholder.SetText(EntryUsuario, "Usuário");
        Loaded += (_, _) => EntryUsuario.Focus();
    }

    private void EntryUsuario_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) EntrySenha.Focus();
    }

    private void EntrySenha_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Entrar();
    }

    private void BtnEntrar_Click(object sender, RoutedEventArgs e) => Entrar();

    private void Entrar()
    {
        var usuario = EntryUsuario.Text.Trim();
        var senha = EntrySenha.Password;
        if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(senha))
        {
            LblStatus.Text = "Informe usuário e senha.";
            return;
        }

        LblStatus.Text = "Conectando ao FTP...";
        BtnEntrar.IsEnabled = false;
        BtnEntrar.Content = "CONECTANDO...";

        SafeThread.Run(async () =>
        {
            var (ok, erro) = await FtpService.ValidarLoginAsync(usuario, senha);
            await Dispatcher.InvokeAsync(() =>
            {
                if (ok)
                {
                    SessionCredentials.Definir(usuario, senha);
                    Logger.RegistrarAuditoria($"Login bem-sucedido no FTP como \"{usuario}\".");
                    LoginSucedido?.Invoke(usuario);
                }
                else
                {
                    Logger.RegistrarAuditoria($"Falha de login no FTP para o usuário \"{usuario}\": {erro}");
                    LblStatus.Text = $"Não consegui entrar: {ErrorMessages.Amigavel(erro!)}";
                    BtnEntrar.IsEnabled = true;
                    BtnEntrar.Content = "ENTRAR";
                }
            });
        });
    }
}
