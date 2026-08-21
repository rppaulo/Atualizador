using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Atulizador.Config;
using Atulizador.Models;
using Atulizador.Services;
using Atulizador.Views.Pages;

namespace Atulizador.Views;

/// <summary>
/// Janela principal — sidebar fixa à esquerda com os módulos; a área da direita troca de
/// conteúdo sem abrir novas janelas. Equivalente a ToolkitApp no script Python.
/// </summary>
public partial class MainWindow : Window
{
    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, string> _navLabels = new();
    private readonly HashSet<string> _chavesComFilhos = new();
    private readonly Dictionary<string, FrameworkElement> _pages = new();
    private readonly Grid _contentArea = new();
    private TextBlock _lblTituloPagina = null!;
    private string? _activeKey;
    private string? _usuarioLogado;

    private readonly List<NavItem> _navItems = new()
    {
        new NavItem { Key = "atualizador", Label = "Atualizador de Aplicações" },
        new NavItem
        {
            Key = "instalador_servidor", Label = "Instalador de Servidor",
            Children = new()
            {
                new NavItem { Key = "inst_matriz", Label = "Server Matriz" },
                new NavItem { Key = "inst_un", Label = "Server Un" },
            },
        },
    };

    public MainWindow()
    {
        InitializeComponent();
        Width = 900;
        var screenH = SystemParameters.PrimaryScreenHeight;
        Height = Math.Max(620, Math.Min(820, screenH * 0.85));

        if (AppConstants.ExigirLoginFtp)
        {
            var login = new LoginView();
            login.LoginSucedido += usuario =>
            {
                _usuarioLogado = usuario;
                MontarInterfacePrincipal();
            };
            RootContent.Children.Add(login);
        }
        else
        {
            MontarInterfacePrincipal();
        }
    }

    private void MontarInterfacePrincipal()
    {
        RootContent.Children.Clear();

        var raiz = new Grid();
        raiz.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        raiz.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        raiz.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RootContent.Children.Add(raiz);

        // --- SIDEBAR ---
        var sidebar = new Border { Background = Theme.Sidebar, Width = 230 };
        Grid.SetColumn(sidebar, 0);
        Grid.SetRow(sidebar, 0);
        Grid.SetRowSpan(sidebar, 2);
        raiz.Children.Add(sidebar);

        var sidebarDock = new DockPanel();
        sidebar.Child = sidebarDock;

        // Marca no topo
        var topo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 22, 18, 24) };
        DockPanel.SetDock(topo, Dock.Top);
        var badge = new Border { Background = Theme.Accent, CornerRadius = new CornerRadius(8), Width = 34, Height = 34 };
        badge.Child = new TextBlock
        {
            Text = "IMS", Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11,
            FontFamily = new FontFamily(Theme.FontUi), HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        topo.Children.Add(badge);
        topo.Children.Add(new TextBlock
        {
            Text = "IMS Toolkit", Foreground = Theme.SidebarText, FontWeight = FontWeights.Bold, FontSize = 14,
            FontFamily = new FontFamily(Theme.FontUi), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0),
        });
        sidebarDock.Children.Add(topo);

        // Cartão do usuário logado, fixo no rodapé
        var rodape = ConstruirRodapeUsuario();
        DockPanel.SetDock(rodape, Dock.Bottom);
        sidebarDock.Children.Add(rodape);

        // Navegação (ocupa o meio)
        var navScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var navContainer = new StackPanel();
        navScroll.Content = navContainer;
        sidebarDock.Children.Add(navScroll);

        navContainer.Children.Add(new TextBlock
        {
            Text = "MÓDULOS", FontFamily = new FontFamily(Theme.FontUi), FontSize = 10, FontWeight = FontWeights.Bold,
            Foreground = Theme.SidebarTextMuted, Margin = new Thickness(18, 4, 18, 8),
        });
        ConstruirSidebar(navContainer, _navItems, nivel: 0);

        // --- CABEÇALHO ---
        var header = new Grid { Margin = new Thickness(20, 18, 20, 5) };
        Grid.SetColumn(header, 1);
        Grid.SetRow(header, 0);
        raiz.Children.Add(header);

        _lblTituloPagina = new TextBlock
        {
            FontFamily = new FontFamily(Theme.FontUi), FontSize = 16, FontWeight = FontWeights.Bold,
            Foreground = Theme.Text, HorizontalAlignment = HorizontalAlignment.Left,
        };
        header.Children.Add(_lblTituloPagina);

        header.Children.Add(new TextBlock
        {
            Text = $"Build {AppConstants.AppVersion}", FontFamily = new FontFamily(Theme.FontMono), FontSize = 11,
            FontWeight = FontWeights.Bold, Foreground = Theme.TextMuted, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
        });

        var separador = new Border { Height = 1, Background = Theme.CardBorder, Margin = new Thickness(20, 0, 20, 0), VerticalAlignment = VerticalAlignment.Bottom };
        Grid.SetColumn(separador, 1);
        Grid.SetRow(separador, 0);
        raiz.Children.Add(separador);

        // --- ÁREA DE CONTEÚDO ---
        Grid.SetColumn(_contentArea, 1);
        Grid.SetRow(_contentArea, 1);
        _contentArea.Margin = new Thickness(20, 15, 20, 15);
        raiz.Children.Add(_contentArea);

        // Cria todas as páginas já no início (mantém, ex., a conexão FTP do atualizador
        // sendo testada em segundo plano desde a abertura).
        var atualizador = new AtualizadorPage(this);
        _pages["atualizador"] = atualizador;

        var opcoesServerMatriz = new Dictionary<string, OpcaoInstalacao>
        {
            ["Completo"] = new OpcaoInstalacao
            {
                Descricao = "Loja única — tudo instalado na mesma máquina.",
                Apps = new() { "Server Matriz", "CredRP + ClientRP (mesma pasta)", "Controle de Atividade", "NFC-e", "Server Un" },
                PerfisPosInstalacao = new() { "ServerMatriz", "CredRP", "NFCe", "ServerUn" },
            },
            ["Parcial"] = new OpcaoInstalacao
            {
                Descricao = "Somente a central — as demais lojas recebem o Server Un separadamente.",
                Apps = new() { "Server Matriz", "CredRP + ClientRP (mesma pasta)", "Controle de Atividade" },
                PerfisPosInstalacao = new() { "ServerMatriz", "CredRP" },
            },
        };
        _pages["inst_matriz"] = new InstaladorServidorPage(this, "Server Matriz",
            "Servidor central. Escolha Completo (loja única) ou Parcial (quando as demais lojas terão o " +
            "Server Un instalado separadamente). Ao concluir, configura em sequência o .ini de cada app instalado.",
            opcoesInstalacao: opcoesServerMatriz);

        _pages["inst_un"] = new InstaladorServidorPage(this, "Server Un",
            "Instalação para lojas adicionais: Server Un + NFC-e + ClientRP (pastas separadas, mesma máquina " +
            "da loja). Ao concluir, configura em sequência o serverun.ini (HOST, código da unidade, PDVs " +
            "válidos, etc.) e o ConfigNFe.ini.",
            perfisPosInstalacao: new List<string> { "ServerUn", "NFCe" });

        foreach (var pagina in _pages.Values)
        {
            pagina.Visibility = Visibility.Collapsed;
            _contentArea.Children.Add(pagina);
        }

        MostrarPagina("atualizador");

        // Checa por uma versão nova do próprio Toolkit em segundo plano.
        _ = VerificarAtualizacaoAposDelayAsync();
    }

    private Border ConstruirRodapeUsuario()
    {
        var rodape = new Border { Background = Theme.SidebarActive, CornerRadius = new CornerRadius(10), Margin = new Thickness(14, 0, 14, 16) };
        var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
        rodape.Child = linha;

        var avatar = new Border { Background = Theme.Accent, CornerRadius = new CornerRadius(16), Width = 32, Height = 32 };
        var iniciais = (_usuarioLogado ?? "??");
        iniciais = iniciais.Length >= 2 ? iniciais[..2].ToUpperInvariant() : iniciais.ToUpperInvariant();
        avatar.Child = new TextBlock
        {
            Text = iniciais, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 11,
            FontFamily = new FontFamily(Theme.FontUi), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        linha.Children.Add(avatar);

        var caixaUser = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        caixaUser.Children.Add(new TextBlock
        {
            Text = _usuarioLogado ?? "—", FontFamily = new FontFamily(Theme.FontUi), FontSize = 12,
            FontWeight = FontWeights.Bold, Foreground = Theme.SidebarText,
        });
        caixaUser.Children.Add(new TextBlock
        {
            Text = "Conectado ao FTP", FontFamily = new FontFamily(Theme.FontUi), FontSize = 10, Foreground = Theme.SidebarTextMuted,
        });
        linha.Children.Add(caixaUser);

        return rodape;
    }

    private void ConstruirSidebar(Panel parent, List<NavItem> items, int nivel)
    {
        foreach (var item in items)
        {
            _navLabels[item.Key] = item.Label;
            var temFilhos = item.Children is { Count: > 0 };
            if (temFilhos) _chavesComFilhos.Add(item.Key);

            var btn = new Button
            {
                Content = item.Label,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Brushes.Transparent,
                Foreground = nivel == 0 ? Theme.SidebarText : Theme.SidebarTextMuted,
                BorderThickness = new Thickness(0),
                FontFamily = new FontFamily(Theme.FontUi),
                FontSize = nivel == 0 ? 13 : 12,
                FontWeight = nivel == 0 ? FontWeights.Bold : FontWeights.Normal,
                Height = nivel == 0 ? 38 : 32,
                Cursor = Cursors.Hand,
                Padding = new Thickness(nivel == 0 ? 14 : 28, 0, 14, 0),
                Margin = new Thickness(0, 0, 0, 2),
            };
            btn.MouseEnter += (_, _) => { if (item.Key != _activeKey) btn.Background = Theme.SidebarActive; };
            btn.MouseLeave += (_, _) => { if (item.Key != _activeKey) btn.Background = Brushes.Transparent; };
            parent.Children.Add(btn);
            _navButtons[item.Key] = btn;

            if (temFilhos)
            {
                var subPanel = new StackPanel { Visibility = Visibility.Collapsed };
                btn.Click += (_, _) => subPanel.Visibility = subPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed : Visibility.Visible;
                ConstruirSidebar(subPanel, item.Children!, nivel + 1);
                parent.Children.Add(subPanel);
            }
            else
            {
                btn.Click += (_, _) => MostrarPagina(item.Key);
            }
        }
    }

    private void MostrarPagina(string key)
    {
        if (!_pages.TryGetValue(key, out var pagina)) return;
        _activeKey = key;
        foreach (var p in _pages.Values) p.Visibility = Visibility.Collapsed;
        pagina.Visibility = Visibility.Visible;
        _lblTituloPagina.Text = _navLabels.GetValueOrDefault(key, "");

        foreach (var (k, btn) in _navButtons)
        {
            if (k == key) btn.Background = Theme.SidebarActive;
            else if (!_chavesComFilhos.Contains(k)) btn.Background = Brushes.Transparent;
        }
    }

    private async Task VerificarAtualizacaoAposDelayAsync()
    {
        await Task.Delay(2000);
        SafeThread.Run(async () =>
        {
            var info = await SelfUpdateService.VerificarAtualizacaoAsync();
            if (info is null) return;

            // MessageBox.Show precisa da thread de UI; Dispatcher.Invoke bloqueia esta
            // thread de background até o usuário responder, o que é exatamente o que
            // queremos aqui (mesmo efeito do askyesno síncrono no Python).
            var notas = info.Notas.Length > 500 ? info.Notas[..500] + "..." : info.Notas;
            var textoNotas = notas.Length > 0 ? $"\n\nNovidades:\n{notas}" : "";
            var resposta = Dispatcher.Invoke(() => MessageBox.Show(
                $"Uma nova versão do IMS Toolkit está disponível: {info.Versao} " +
                $"(você está usando a {AppConstants.AppVersion}).{textoNotas}\n\nAtualizar agora?",
                "Atualização disponível", MessageBoxButton.YesNo, MessageBoxImage.Information));

            if (resposta == MessageBoxResult.Yes)
                await AplicarAtualizacaoToolkitAsync(info);
        });
    }

    private async Task AplicarAtualizacaoToolkitAsync(GithubUpdateInfo info)
    {
        var mensagens = new List<string>();
        try
        {
            await SelfUpdateService.AplicarAtualizacaoAsync(info, (msg, _) => mensagens.Add(msg));
        }
        catch (Exception e)
        {
            Dispatcher.Invoke(() => MessageBox.Show(ErrorMessages.Amigavel(e), "Erro ao atualizar",
                MessageBoxButton.OK, MessageBoxImage.Error));
            return;
        }
        // Se chegou até aqui é porque AplicarAtualizacaoAsync não encerrou o processo — não
        // deveria acontecer no fluxo normal (ele sempre finaliza o app ao concluir).
        Dispatcher.Invoke(() => MessageBox.Show(string.Join("\n", mensagens) + "\n\nReinicie o aplicativo para usar a nova versão.",
            "Atualizado", MessageBoxButton.OK, MessageBoxImage.Information));
    }
}
