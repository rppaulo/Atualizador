using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Atulizador.Config;
using Atulizador.Models;
using Atulizador.Services;
using Atulizador.Views.Controls;
using MessageBox = System.Windows.MessageBox;

namespace Atulizador.Views.Pages;

/// <summary>
/// Fluxo em 2 etapas, sem abrir novas janelas — só troca o conteúdo do próprio módulo:
///   1) INSTALAÇÃO  -> log + botão "Iniciar instalação".
///   2) CONFIGURAÇÃO -> se o app tiver um perfil, mostra o formulário pós-instalação
///      (IniConfigFormControl) para preencher HOST, código da unidade, PDVs válidos, etc.
/// Equivalente a InstaladorServidorPage no script Python.
/// </summary>
public sealed class InstaladorServidorPage : UserControl
{
    private readonly Window _ownerWindow;
    private readonly string _titulo;
    private readonly List<string> _perfisPosInstalacaoFixos;
    private readonly Dictionary<string, OpcaoInstalacao>? _opcoesInstalacao;
    private string _tipoInstalacaoSelecionado;

    private readonly Queue<string> _filaPerfis = new();
    private int _filaTotal;
    private HashSet<string> _pularInstalacao = new();
    private readonly Dictionary<string, Dictionary<string, string>> _contextoValores = new();

    private readonly DockPanel _container = new();
    private RichTextBox _logBox = null!;
    private TextBlock _lblFila = null!;
    private ProgressBar _prog = null!;
    private Button _btnStart = null!;
    private TextBlock? _lblApps;
    private readonly Dictionary<string, Button> _botoesTipo = new();

    public InstaladorServidorPage(Window ownerWindow, string titulo, string descricao,
        List<string>? perfisPosInstalacao = null, Dictionary<string, OpcaoInstalacao>? opcoesInstalacao = null)
    {
        _ownerWindow = ownerWindow;
        _titulo = titulo;
        _perfisPosInstalacaoFixos = perfisPosInstalacao ?? new List<string>();
        _opcoesInstalacao = opcoesInstalacao;
        _tipoInstalacaoSelecionado = opcoesInstalacao is { Count: > 0 } ? opcoesInstalacao.Keys.First() : "";

        Content = ConstruirUi(titulo, descricao);
        MontarViewInstalacao();
    }

    private UIElement ConstruirUi(string titulo, string descricao)
    {
        var raiz = new DockPanel();

        var cardInner = new StackPanel();
        cardInner.Children.Add(UiFactory.Title(titulo.ToUpperInvariant(), 14));
        var descLabel = UiFactory.Muted(descricao);
        descLabel.Margin = new Thickness(0, 4, 0, 0);
        cardInner.Children.Add(descLabel);

        if (_opcoesInstalacao is { Count: > 0 })
        {
            var seletor = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            var chaves = _opcoesInstalacao.Keys.ToList();
            for (var i = 0; i < chaves.Count; i++) seletor.ColumnDefinitions.Add(new ColumnDefinition());

            for (var i = 0; i < chaves.Count; i++)
            {
                var chave = chaves[i];
                var btn = new Button
                {
                    Content = chave, Height = 32, FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily(Theme.FontUi), FontSize = 12, Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(i == 0 ? 0 : 2, 0, i == chaves.Count - 1 ? 0 : 2, 0),
                };
                btn.Click += (_, _) =>
                {
                    _tipoInstalacaoSelecionado = chave;
                    AtualizarSelecaoTipoVisual();
                    AtualizarListaApps();
                };
                Grid.SetColumn(btn, i);
                seletor.Children.Add(btn);
                _botoesTipo[chave] = btn;
            }
            cardInner.Children.Add(seletor);
            AtualizarSelecaoTipoVisual();

            _lblApps = UiFactory.Muted("");
            _lblApps.Margin = new Thickness(0, 8, 0, 0);
            cardInner.Children.Add(_lblApps);
            AtualizarListaApps();
        }

        var card = UiFactory.Card(cardInner);
        card.Margin = new Thickness(0, 0, 0, 10);
        DockPanel.SetDock(card, Dock.Top);
        raiz.Children.Add(card);

        raiz.Children.Add(_container);
        return raiz;
    }

    private void AtualizarSelecaoTipoVisual()
    {
        foreach (var (chave, btn) in _botoesTipo)
        {
            var selecionado = chave == _tipoInstalacaoSelecionado;
            btn.Background = selecionado ? Theme.Accent : Theme.Bg;
            btn.Foreground = selecionado ? Brushes.White : Theme.Text;
        }
    }

    private void AtualizarListaApps()
    {
        if (_lblApps is null || _opcoesInstalacao is null) return;
        var opcao = _opcoesInstalacao[_tipoInstalacaoSelecionado];
        var appsTxt = " • " + string.Join("\n • ", opcao.Apps);
        _lblApps.Text = $"{opcao.Descricao}\n\nO que é instalado:\n{appsTxt}";
    }

    private List<string> FilaSelecionada() =>
        _opcoesInstalacao is not null
            ? new List<string>(_opcoesInstalacao[_tipoInstalacaoSelecionado].PerfisPosInstalacao)
            : new List<string>(_perfisPosInstalacaoFixos);

    // --- ETAPA 1: instalação -------------------------------------------------------
    private void MontarViewInstalacao()
    {
        _container.Children.Clear();

        // Ordem de inserção no DockPanel importa: o primeiro filho "Bottom" fica na borda
        // mais externa (mais embaixo); os próximos "Bottom" empilham por cima dele. O
        // último filho (sem Dock definido) preenche o espaço restante — por isso o log
        // é adicionado por último.
        _btnStart = UiFactory.PrimaryButton("INICIAR INSTALAÇÃO", 40);
        _btnStart.Margin = new Thickness(0, 5, 0, 0);
        _btnStart.Click += (_, _) => ConfirmarEInstalar();
        DockPanel.SetDock(_btnStart, Dock.Bottom);
        _container.Children.Add(_btnStart);

        _prog = UiFactory.Progress();
        _prog.Margin = new Thickness(0, 0, 0, 5);
        DockPanel.SetDock(_prog, Dock.Bottom);
        _container.Children.Add(_prog);

        _lblFila = new TextBlock { FontFamily = new FontFamily(Theme.FontUi), FontSize = 11, Foreground = Theme.TextMuted };
        DockPanel.SetDock(_lblFila, Dock.Bottom);
        _container.Children.Add(_lblFila);

        _logBox = new RichTextBox
        {
            Background = Theme.ConsoleBg, BorderBrush = Theme.CardBorder, BorderThickness = new Thickness(1),
            FontFamily = new FontFamily(Theme.FontMono), FontSize = 12, Foreground = Theme.ConsoleText,
            IsReadOnly = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 0, 0, 5),
        };
        _container.Children.Add(_logBox);
        Log($"Módulo \"{_titulo}\" carregado. Aguardando início da instalação.");
    }

    private void Log(string msg, string tag = "info")
    {
        var cor = tag switch
        {
            "error" => Theme.ConsoleDanger,
            "success" => Theme.ConsoleSuccess,
            "warning" => Theme.ConsoleWarning,
            _ => Theme.ConsoleText,
        };
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Dispatcher.Invoke(() =>
        {
            _logBox.Document.Blocks.Add(new Paragraph(new Run($"[{timestamp}] {msg}") { Foreground = cor }) { Margin = new Thickness(0) });
            _logBox.ScrollToEnd();
        });
    }

    private void AtualizarProgresso(double valor, string texto = "") => Dispatcher.Invoke(() => _prog.Value = valor);

    private void AtualizarStatusFila(string texto) => Dispatcher.Invoke(() => _lblFila.Text = texto);

    private List<string> AppsJaInstalados(List<string> apps) => apps.Where(appKey =>
    {
        var loc = InstallProfiles.AppLocalizacao.GetValueOrDefault(appKey);
        return InstallPathsStore.Localizar(loc?.NomeApp ?? appKey, loc?.Exes ?? new List<string>()) is not null;
    }).ToList();

    private void ConfirmarEInstalar()
    {
        var apps = FilaSelecionada();
        var jaInstalados = apps.Count > 0 ? AppsJaInstalados(apps) : new List<string>();

        _pularInstalacao = new HashSet<string>();

        if (jaInstalados.Count > 0 && jaInstalados.Count < apps.Count)
        {
            var lista = string.Join(", ", jaInstalados);
            var resposta = MessageBox.Show(
                $"{lista} já parece(m) instalado(s) nesta máquina — parece uma instalação iniciada antes.\n\n" +
                "SIM = continuar só com o que falta (pula os já instalados)\n" +
                "NÃO = reinstalar tudo do zero, inclusive os já instalados\n" +
                "CANCELAR = não fazer nada agora",
                "Retomar instalação?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (resposta == MessageBoxResult.Cancel) return;
            if (resposta == MessageBoxResult.Yes) _pularInstalacao = jaInstalados.ToHashSet();
        }
        else if (jaInstalados.Count > 0)
        {
            var lista = string.Join(", ", jaInstalados);
            var resposta = MessageBox.Show(
                $"Detectei que {lista} já parece(m) instalado(s) nesta máquina.\n\n" +
                "Continuar vai sobrescrever os arquivos atuais. Deseja continuar mesmo assim?",
                "Já instalado?", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (resposta != MessageBoxResult.Yes) return;
        }

        var rotulo = _opcoesInstalacao is null ? _titulo : $"{_titulo} — {_tipoInstalacaoSelecionado}";
        if (MessageBox.Show($"Deseja iniciar a instalação de \"{rotulo}\"?", "Confirmar instalação",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _btnStart.IsEnabled = false;
        _btnStart.Content = "[ INSTALANDO... ]";
        SafeThread.Run(ExecutarInstalacaoAsync, msg => Log(msg, "error"));
    }

    private async Task ExecutarInstalacaoAsync()
    {
        var apps = FilaSelecionada();

        if (apps.Count == 0)
        {
            Log($"Iniciando instalação: {_titulo}...");
            Log("Nenhum pacote de instalação associado a este módulo ainda.", "warning");
            Dispatcher.Invoke(() =>
            {
                _btnStart.IsEnabled = true;
                _btnStart.Content = "INICIAR INSTALAÇÃO";
            });
            return;
        }

        var rotulo = _opcoesInstalacao is null ? _titulo : $"{_titulo} ({_tipoInstalacaoSelecionado})";
        Log($"Iniciando instalação: {rotulo}...");
        Logger.RegistrarAuditoria($"Início da instalação \"{rotulo}\" — apps: {string.Join(", ", apps)}");

        var total = apps.Count;
        var appsComFalha = new List<string>();
        for (var idx = 0; idx < apps.Count; idx++)
        {
            var appKey = apps[idx];
            AtualizarStatusFila($"Instalando {idx + 1} de {total}: {appKey}");
            if (_pularInstalacao.Contains(appKey))
            {
                Log($"--- {appKey} ({idx + 1}/{total}) --- já instalado, pulando download/extração.");
                continue;
            }
            Log($"--- {appKey} ({idx + 1}/{total}) ---");
            try
            {
                await InstallerService.InstalarAppViaFtpAsync(appKey, Log, AtualizarProgresso);
            }
            catch (Exception e)
            {
                Log($"Erro ao instalar {appKey}: {ErrorMessages.Amigavel(e)}", "error");
                Logger.RegistrarAuditoria($"ERRO ao instalar \"{appKey}\" (etapa {idx + 1}/{total} de \"{rotulo}\"): {e}");
                appsComFalha.Add(appKey);
                continue;
            }
            AtualizarProgresso(0);
        }

        AtualizarStatusFila("");

        if (appsComFalha.Count > 0)
            Log($"Não instalei: {string.Join(", ", appsComFalha)} (veja os erros acima). " +
                "Vou seguir para configurar só o que instalou com sucesso.", "warning");
        else
            Log("Instalação concluída com sucesso!", "success");

        Dispatcher.Invoke(() => InstalacaoConcluida(appsComFalha));
    }

    private void InstalacaoConcluida(List<string> appsComFalha)
    {
        var falhas = appsComFalha.ToHashSet();
        var fila = FilaSelecionada().Where(a => !falhas.Contains(a)).ToList();
        if (fila.Count > 0)
        {
            Log("Prosseguindo para a configuração pós-instalação...", "success");
            _filaPerfis.Clear();
            foreach (var app in fila) _filaPerfis.Enqueue(app);
            _filaTotal = fila.Count;
            _contextoValores.Clear();
            AvancarFilaConfiguracao();
        }
        else
        {
            _btnStart.IsEnabled = true;
            _btnStart.Content = "INICIAR INSTALAÇÃO";
        }
    }

    // --- ETAPA 2: configuração pós-instalação -------------------------------------------
    private void AvancarFilaConfiguracao()
    {
        if (_filaPerfis.Count == 0)
        {
            MontarViewInstalacao();
            MessageBox.Show("Instalação e configuração de todos os módulos foram finalizadas.", "Concluído",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var appKey = _filaPerfis.Dequeue();
        SafeThread.Run(() => PrepararConfiguracaoAsync(appKey), msg => Log(msg, "error"));
    }

    private async Task PrepararConfiguracaoAsync(string appKey)
    {
        var perfil = InstallProfiles.Profiles[appKey];
        var localizacao = InstallProfiles.AppLocalizacao.GetValueOrDefault(appKey);
        var localDir = InstallPathsStore.Localizar(localizacao?.NomeApp ?? appKey, localizacao?.Exes ?? new List<string>());
        var iniPath = localDir is not null ? System.IO.Path.Combine(localDir, perfil.IniFilename) : null;

        if (iniPath is null || !System.IO.File.Exists(iniPath))
        {
            var escolhido = await PerguntarDiretorioAsync($"Onde o {appKey} está instalado? (contém o {perfil.IniFilename})");
            if (string.IsNullOrEmpty(escolhido))
            {
                Log($"Diretório do {appKey} não informado — essa etapa de configuração foi pulada.", "warning");
                Dispatcher.Invoke(AvancarFilaConfiguracao);
                return;
            }
            localDir = escolhido;
            iniPath = System.IO.Path.Combine(localDir, perfil.IniFilename);
        }

        if (!System.IO.File.Exists(iniPath))
        {
            Log($"Não encontrei o arquivo {iniPath}.", "error");
            Dispatcher.Invoke(AvancarFilaConfiguracao);
            return;
        }

        InstallPathsStore.Lembrar(localizacao?.NomeApp ?? appKey, localDir);
        var caminhoFinal = iniPath;
        Dispatcher.Invoke(() => ExibirFormulario(appKey, perfil, caminhoFinal));
    }

    private void ExibirFormulario(string appKey, InstallProfile perfil, string iniPath)
    {
        _container.Children.Clear();
        var passoAtual = _filaTotal - _filaPerfis.Count;
        var formulario = new IniConfigFormControl(perfil, iniPath, AvancarFilaConfiguracao, _contextoValores, appKey,
            _filaPerfis.Count > 0 ? "PULAR ESTA ETAPA" : "PULAR", passoAtual, _filaTotal);
        _container.Children.Add(formulario);
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
