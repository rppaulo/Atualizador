using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Atulizador.Config;
using Atulizador.Models;
using Atulizador.Services;
using Atulizador.Views.Windows;
using MessageBox = System.Windows.MessageBox;

namespace Atulizador.Views.Pages;

/// <summary>
/// Página "Atualizador de Aplicações" — equivalente a AtualizadorPage no script Python.
/// A lógica de rede/arquivos fica em <see cref="AtualizadorService"/>; esta classe só
/// cuida de UI.
/// </summary>
public sealed class AtualizadorPage : UserControl
{
    private readonly Window _ownerWindow;
    private readonly Dictionary<string, bool> _checkVars = new();
    private readonly AtualizadorService _service;

    private bool _ftpValidado;
    private string _modoAtual = AppConstants.ModeRpInfo;

    private TextBlock _ftpStatus = null!;
    private Button _btnRpInfo = null!;
    private Button _btnIms = null!;
    private RichTextBox _logBox = null!;
    private ProgressBar _prog = null!;
    private TextBlock _statusLabel = null!;
    private TextBlock _lblPercent = null!;
    private Button _btnStart = null!;

    public AtualizadorPage(Window ownerWindow)
    {
        _ownerWindow = ownerWindow;
        foreach (var app in UpdaterApps.Apps) _checkVars[app.Name] = false;
        _service = new AtualizadorService(Log, AtualizarProgresso);

        Content = ConstruirUi();

        Loaded += (_, _) =>
        {
            LogDiagnosticosSistema();
            SafeThread.Run(TestarLoginFtpAsync, msg => Log(msg));
        };
    }

    private UIElement ConstruirUi()
    {
        var raiz = new DockPanel();

        // --- CARD DE STATUS ---
        var statusInner = new StackPanel();
        statusInner.Children.Add(new TextBlock
        {
            Text = "CONEXÃO E DIAGNÓSTICO", FontFamily = new FontFamily(Theme.FontUi), FontSize = 10,
            FontWeight = FontWeights.Bold, Foreground = Theme.TextMuted, Margin = new Thickness(0, 0, 0, 4),
        });
        _ftpStatus = new TextBlock
        {
            Text = "[ ] FTP: Aguardando conexão...", FontFamily = new FontFamily(Theme.FontMono), FontSize = 12,
            Foreground = Theme.TextMuted,
        };
        statusInner.Children.Add(_ftpStatus);
        var statusCard = UiFactory.Card(statusInner, new Thickness(15, 8, 15, 8));
        statusCard.Margin = new Thickness(0, 0, 0, 5);
        DockPanel.SetDock(statusCard, Dock.Top);
        raiz.Children.Add(statusCard);

        // --- SELETOR DE MODO ---
        var modeSelector = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        modeSelector.ColumnDefinitions.Add(new ColumnDefinition());
        modeSelector.ColumnDefinitions.Add(new ColumnDefinition());
        _btnRpInfo = CriarBotaoModo(AppConstants.ModeRpInfo);
        _btnIms = CriarBotaoModo(AppConstants.ModeIms);
        Grid.SetColumn(_btnRpInfo, 0);
        Grid.SetColumn(_btnIms, 1);
        modeSelector.Children.Add(_btnRpInfo);
        modeSelector.Children.Add(_btnIms);
        AtualizarSelecaoModo();
        DockPanel.SetDock(modeSelector, Dock.Top);
        raiz.Children.Add(modeSelector);

        // --- AÇÕES ---
        var acoes = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        acoes.ColumnDefinitions.Add(new ColumnDefinition());
        acoes.ColumnDefinitions.Add(new ColumnDefinition());
        var btnModules = UiFactory.OutlineButton("SELECIONAR MÓDULOS");
        btnModules.Margin = new Thickness(0, 0, 5, 0);
        btnModules.Click += (_, _) => new ModulesWindow(_ownerWindow, UpdaterApps.Apps, _checkVars) { Owner = _ownerWindow }.ShowDialog();
        var btnOpenApps = UiFactory.GhostButton("ABRIR APLICAÇÕES", 30);
        btnOpenApps.Margin = new Thickness(5, 0, 0, 0);
        btnOpenApps.Click += (_, _) => AbrirAplicacoes();
        Grid.SetColumn(btnModules, 0);
        Grid.SetColumn(btnOpenApps, 1);
        acoes.Children.Add(btnModules);
        acoes.Children.Add(btnOpenApps);
        DockPanel.SetDock(acoes, Dock.Top);
        raiz.Children.Add(acoes);

        // --- PROGRESSO (rodapé) ---
        var progFrame = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
        _prog = UiFactory.Progress();
        _prog.Margin = new Thickness(0, 5, 0, 5);
        progFrame.Children.Add(_prog);

        var progTextos = new Grid();
        progTextos.ColumnDefinitions.Add(new ColumnDefinition());
        progTextos.ColumnDefinitions.Add(new ColumnDefinition());
        _statusLabel = new TextBlock
        {
            Text = "Pronto para iniciar.", FontFamily = new FontFamily(Theme.FontUi), FontSize = 11,
            Foreground = Theme.TextMuted, HorizontalAlignment = HorizontalAlignment.Left,
        };
        _lblPercent = new TextBlock
        {
            Text = "0%", FontFamily = new FontFamily(Theme.FontMono), FontSize = 11, FontWeight = FontWeights.Bold,
            Foreground = Theme.Accent, HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(_statusLabel, 0);
        Grid.SetColumn(_lblPercent, 1);
        progTextos.Children.Add(_statusLabel);
        progTextos.Children.Add(_lblPercent);
        progFrame.Children.Add(progTextos);
        DockPanel.SetDock(progFrame, Dock.Bottom);
        raiz.Children.Add(progFrame);

        _btnStart = UiFactory.PrimaryButton("INICIALIZAR ROTINA DE ATUALIZAÇÃO", 40);
        _btnStart.Margin = new Thickness(0, 5, 0, 0);
        _btnStart.Click += (_, _) => IniciarAtualizacao();
        DockPanel.SetDock(_btnStart, Dock.Bottom);
        raiz.Children.Add(_btnStart);

        var separador = new Border { Height = 1, Background = Theme.CardBorder, Margin = new Thickness(0, 5, 0, 5) };
        DockPanel.SetDock(separador, Dock.Bottom);
        raiz.Children.Add(separador);

        // --- CONSOLE DE LOG ---
        _logBox = new RichTextBox
        {
            Background = Theme.ConsoleBg, BorderBrush = Theme.CardBorder, BorderThickness = new Thickness(1),
            FontFamily = new FontFamily(Theme.FontMono), FontSize = 12, Foreground = Theme.ConsoleText,
            IsReadOnly = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _logBox.Document.PageWidth = 2000; // evita quebra de linha forçada estranha
        raiz.Children.Add(_logBox);

        return raiz;
    }

    private Button CriarBotaoModo(string modo)
    {
        var btn = new Button
        {
            Content = modo, Height = 32, FontFamily = new FontFamily(Theme.FontUi), FontSize = 12,
            FontWeight = FontWeights.Bold, Cursor = System.Windows.Input.Cursors.Hand, BorderThickness = new Thickness(1),
            BorderBrush = Theme.CardBorder, Margin = modo == AppConstants.ModeRpInfo ? new Thickness(0, 0, 2, 0) : new Thickness(2, 0, 0, 0),
        };
        btn.Click += (_, _) =>
        {
            _modoAtual = modo;
            AtualizarSelecaoModo();
            Log($"Modo alterado para: {modo}");
        };
        return btn;
    }

    private void AtualizarSelecaoModo()
    {
        foreach (var (btn, modo) in new[] { (_btnRpInfo, AppConstants.ModeRpInfo), (_btnIms, AppConstants.ModeIms) })
        {
            var selecionado = modo == _modoAtual;
            btn.Background = selecionado ? Theme.Accent : Theme.Card;
            btn.Foreground = selecionado ? Brushes.White : Theme.Text;
        }
    }

    // --- Diagnósticos de sistema ---
    private void LogDiagnosticosSistema()
    {
        var arch = ValidationHelper.GetArch();
        var osVer = $"{Environment.OSVersion.VersionString}";
        var ramTotalGb = GetTotalPhysicalMemoryGb();

        LogSys("--- INFORMAÇÕES DO SISTEMA ---");
        LogSys($"OS: {osVer} | Arquitetura: {arch}");
        LogSys($"Memória RAM total: {ramTotalGb:F2} GB");
        LogSys($"Versão do .NET: {Environment.Version}");
        LogSys("------------------------------");
    }

    private static double GetTotalPhysicalMemoryGb()
    {
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            return gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
        }
        catch
        {
            return 0;
        }
    }

    // --- FTP ---
    private async Task TestarLoginFtpAsync()
    {
        try
        {
            using var ftp = await FtpService.ConectarSessaoAsync();
            await ftp.Disconnect();
            _ftpValidado = true;
            Dispatcher.Invoke(() => _ftpStatus.Text = "[+] FTP: Conectado com sucesso");
            Dispatcher.Invoke(() => _ftpStatus.Foreground = Theme.Success);
            Log("Conectado ao servidor FTP com sucesso.");
        }
        catch (Exception e)
        {
            Dispatcher.Invoke(() => _ftpStatus.Text = "[-] FTP: Falha ao conectar");
            Dispatcher.Invoke(() => _ftpStatus.Foreground = Theme.Danger);
            Log($"Erro ao conectar no FTP: {e.Message}");
        }
    }

    // --- Log ---
    private void Log(string msg)
    {
        var lower = msg.ToLowerInvariant();
        Brush cor = Theme.ConsoleText;
        if (lower.Contains("erro") || lower.Contains("falha") || lower.Contains("ops"))
            cor = Theme.ConsoleDanger;
        else if (new[] { "sucesso", "conectado", "atualizado", "tudo certo" }.Any(lower.Contains))
            cor = Theme.ConsoleSuccess;
        else if (lower.Contains("aviso"))
            cor = Theme.ConsoleWarning;

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Dispatcher.Invoke(() => AppendLogLine($"[{timestamp}] {msg}", cor));
    }

    private void LogSys(string msg) => Dispatcher.Invoke(() => AppendLogLine(msg, Theme.ConsoleSys));

    private void AppendLogLine(string texto, Brush cor)
    {
        var paragraph = new Paragraph(new Run(texto) { Foreground = cor }) { Margin = new Thickness(0) };
        _logBox.Document.Blocks.Add(paragraph);
        _logBox.ScrollToEnd();
    }

    private void AtualizarProgresso(double valor, string texto)
    {
        Dispatcher.Invoke(() =>
        {
            _prog.Value = valor;
            _statusLabel.Text = texto;
            _lblPercent.Text = $"{(int)(valor * 100)}%";
        });
    }

    // --- Processo de atualização ---
    private void IniciarAtualizacao()
    {
        if (!_ftpValidado)
        {
            MessageBox.Show("O FTP ainda não conectou. Aguarde um instante.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var selecionados = _checkVars.Where(kv => kv.Value).Select(kv => kv.Key).ToList();
        if (selecionados.Count == 0)
        {
            MessageBox.Show("Por favor, selecione pelo menos um módulo para atualizar.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _btnStart.IsEnabled = false;
        _btnStart.Content = "[ ATUALIZANDO... ]";

        if (_modoAtual == AppConstants.ModeRpInfo)
            SafeThread.Run(() => ExecutarModoRpInfoAsync(selecionados), Log);
        else
            SafeThread.Run(() => ExecutarModoImsAsync(selecionados), Log);
    }

    private async Task ExecutarModoRpInfoAsync(List<string> selecionados)
    {
        try
        {
            await _service.ExecutarModoRpInfoAsync(selecionados, PerguntarDiretorioAsync);
            Dispatcher.Invoke(() => FinalizarEPerguntarAbrir());
        }
        catch (Exception e)
        {
            Log($"Ops, ocorreu um erro durante a atualização: {e.Message}");
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                _btnStart.IsEnabled = true;
                _btnStart.Content = "INICIALIZAR ROTINA DE ATUALIZAÇÃO";
                AtualizarProgresso(0, "Pronto para iniciar.");
            });
        }
    }

    private async Task ExecutarModoImsAsync(List<string> selecionados)
    {
        try
        {
            await _service.ExecutarModoImsAsync(selecionados);
            Dispatcher.Invoke(() => FinalizarEPerguntarAbrir());
        }
        finally
        {
            Dispatcher.Invoke(() =>
            {
                _btnStart.IsEnabled = true;
                _btnStart.Content = "INICIALIZAR ROTINA DE ATUALIZAÇÃO";
                AtualizarProgresso(0, "Pronto para iniciar.");
            });
        }
    }

    private void FinalizarEPerguntarAbrir()
    {
        const string msg = "Atualização concluída com sucesso!";
        if (_service.AppsFechados.Count > 0)
        {
            var resposta = MessageBox.Show($"{msg}\nVocê gostaria de abrir os sistemas agora?", "Tudo Pronto",
                MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (resposta == MessageBoxResult.Yes) AbrirAplicacoes();
        }
        else
        {
            MessageBox.Show(msg, "Tudo Pronto", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void AbrirAplicacoes()
    {
        if (_service.AppsFechados.Count == 0)
        {
            MessageBox.Show("Nenhum sistema fechado foi registrado para reabrir.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _service.AbrirAplicacoesFechadas();
    }

    private Task<string?> PerguntarDiretorioAsync(string titulo)
    {
        var tcs = new TaskCompletionSource<string?>();
        Dispatcher.Invoke(() =>
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = titulo };
            var resultado = dialog.ShowDialog(_ownerWindow);
            tcs.SetResult(resultado == true ? dialog.FolderName : null);
        });
        return tcs.Task;
    }
}
