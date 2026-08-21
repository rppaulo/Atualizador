using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Atulizador.Config;
using Atulizador.Models;
using Atulizador.Services;
using MessageBox = System.Windows.MessageBox;

namespace Atulizador.Views.Controls;

/// <summary>
/// Formulário genérico de configuração pós-instalação, construído dinamicamente a partir
/// de um <see cref="InstallProfile"/>. Para adicionar uma aplicação nova, não é preciso
/// mexer nesta classe — só criar o perfil em <see cref="InstallProfiles"/>.
/// Equivalente a FormularioConfiguracaoIni no script Python.
/// </summary>
public sealed class IniConfigFormControl : UserControl
{
    private readonly InstallProfile _perfil;
    private readonly string _iniPath;
    private readonly Action _onConcluir;
    private readonly Dictionary<string, Dictionary<string, string>> _contextoValores;
    private readonly string? _chaveContexto;

    private readonly Dictionary<string, TextBox> _entries = new();
    private readonly Dictionary<string, CheckBox> _varMesmo = new();
    private readonly Dictionary<string, string> _simNaoValores = new();
    private readonly Dictionary<string, (Button sim, Button nao)> _simNaoBotoes = new();
    private readonly Dictionary<string, (TextBox inicio, TextBox qtd)> _pdvsEntries = new();
    private readonly Dictionary<string, TextBox> _entryQtdLojas = new();
    private readonly Dictionary<string, StackPanel> _frameLojas = new();
    private readonly Dictionary<string, List<LinhaLoja>> _linhasLoja = new();
    private readonly Dictionary<string, string?> _valorConhecidoMesmoOutroApp = new();

    private TextBlock _lblErro = null!;

    public IniConfigFormControl(InstallProfile perfil, string iniPath, Action onConcluir,
        Dictionary<string, Dictionary<string, string>> contextoValores, string? chaveContexto,
        string textoBotaoVoltar, int? passoAtual, int? passoTotal)
    {
        _perfil = perfil;
        _iniPath = iniPath;
        _onConcluir = onConcluir;
        _contextoValores = contextoValores;
        _chaveContexto = chaveContexto;

        Content = ConstruirUi(textoBotaoVoltar, passoAtual, passoTotal);
    }

    private UIElement ConstruirUi(string textoBotaoVoltar, int? passoAtual, int? passoTotal)
    {
        var raiz = new DockPanel();

        var titulo = $"CONFIGURAÇÃO PÓS-INSTALAÇÃO — {System.IO.Path.GetFileName(_iniPath)}";
        if (passoAtual.HasValue && passoTotal is > 1)
            titulo += $"  (etapa {passoAtual} de {passoTotal})";

        var cabecalho = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        cabecalho.Children.Add(UiFactory.Title(titulo, 13));
        cabecalho.Children.Add(UiFactory.Muted("Preencha os campos abaixo. Somente os parâmetros marcados para " +
                                                "definição na instalação aparecem aqui."));
        DockPanel.SetDock(cabecalho, Dock.Top);
        raiz.Children.Add(cabecalho);

        _lblErro = new TextBlock
        {
            FontFamily = new FontFamily(Theme.FontUi), FontSize = 11, Foreground = Theme.Danger,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        };
        DockPanel.SetDock(_lblErro, Dock.Bottom);
        raiz.Children.Add(_lblErro);

        var botoes = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        botoes.ColumnDefinitions.Add(new ColumnDefinition());
        botoes.ColumnDefinitions.Add(new ColumnDefinition());
        var btnVoltar = UiFactory.GhostButton(textoBotaoVoltar);
        btnVoltar.Margin = new Thickness(0, 0, 5, 0);
        btnVoltar.Click += (_, _) => _onConcluir();
        var btnSalvar = UiFactory.PrimaryButton("SALVAR CONFIGURAÇÕES", 38);
        btnSalvar.Margin = new Thickness(5, 0, 0, 0);
        btnSalvar.Click += (_, _) => Salvar();
        Grid.SetColumn(btnVoltar, 0);
        Grid.SetColumn(btnSalvar, 1);
        botoes.Children.Add(btnVoltar);
        botoes.Children.Add(btnSalvar);
        DockPanel.SetDock(botoes, Dock.Bottom);
        raiz.Children.Add(botoes);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var camposPanel = new StackPanel { Margin = new Thickness(12) };
        scroll.Content = camposPanel;
        raiz.Children.Add(UiFactory.Card(scroll, new Thickness(0)));

        foreach (var campo in _perfil.Fields)
            camposPanel.Children.Add(ConstruirCampo(campo));

        return raiz;
    }

    private string ValorInicial(InstallField campo)
    {
        if (campo.Key is null || campo.Section is null) return "";
        var atual = IniFileService.LerValor(_iniPath, campo.Section, campo.Key) ?? "";
        var maiusculo = atual.Trim().ToUpperInvariant();
        string[] prefixosPlaceholder =
            { "SOLICITAR", "PERGUNTAR", "PREENCHER", "O MESMO", "MESMO CASO", "IP DO BANCO", "SOLCICITAR" };
        return prefixosPlaceholder.Any(maiusculo.StartsWith) ? "" : atual;
    }

    private UIElement ConstruirCampo(InstallField campo)
    {
        var wrap = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
        wrap.Children.Add(new TextBlock
        {
            Text = campo.Label, FontFamily = new FontFamily(Theme.FontUi), FontSize = 12,
            FontWeight = FontWeights.Bold, Foreground = Theme.Text,
        });
        if (!string.IsNullOrEmpty(campo.Ajuda))
            wrap.Children.Add(new TextBlock
            {
                Text = campo.Ajuda, FontFamily = new FontFamily(Theme.FontUi), FontSize = 10,
                Foreground = Theme.TextMuted, TextWrapping = TextWrapping.Wrap,
            });

        var valorAtual = ValorInicial(campo);

        switch (campo.Tipo)
        {
            case FieldType.Texto:
            case FieldType.Ip:
            case FieldType.Numero:
            {
                var entry = UiFactory.Entry();
                entry.Text = valorAtual;
                entry.Margin = new Thickness(0, 4, 0, 0);
                wrap.Children.Add(entry);
                _entries[campo.Id] = entry;
                break;
            }

            case FieldType.SimNao:
            {
                var valorInicial = string.Equals(valorAtual.Trim(), "SIM", StringComparison.OrdinalIgnoreCase) ? "SIM" : "NAO";
                _simNaoValores[campo.Id] = valorInicial;
                var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var btnSim = new Button { Content = "SIM", Height = 30, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 2, 0) };
                var btnNao = new Button { Content = "NAO", Height = 30, FontWeight = FontWeights.Bold, Cursor = Cursors.Hand, Margin = new Thickness(2, 0, 0, 0) };
                _simNaoBotoes[campo.Id] = (btnSim, btnNao);
                btnSim.Click += (_, _) => { _simNaoValores[campo.Id] = "SIM"; AtualizarSimNaoVisual(campo.Id); };
                btnNao.Click += (_, _) => { _simNaoValores[campo.Id] = "NAO"; AtualizarSimNaoVisual(campo.Id); };
                Grid.SetColumn(btnSim, 0);
                Grid.SetColumn(btnNao, 1);
                grid.Children.Add(btnSim);
                grid.Children.Add(btnNao);
                wrap.Children.Add(grid);
                AtualizarSimNaoVisual(campo.Id);
                break;
            }

            case FieldType.PdvsValidos:
            {
                var linha = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                linha.ColumnDefinitions.Add(new ColumnDefinition());
                linha.ColumnDefinitions.Add(new ColumnDefinition());
                var entryInicio = UiFactory.Entry(placeholder: "Nº inicial (ex: 101)");
                entryInicio.Margin = new Thickness(0, 0, 5, 0);
                var entryQtd = UiFactory.Entry(placeholder: "Quantidade de PDVs");
                entryQtd.Margin = new Thickness(5, 0, 0, 0);
                Grid.SetColumn(entryInicio, 0);
                Grid.SetColumn(entryQtd, 1);
                linha.Children.Add(entryInicio);
                linha.Children.Add(entryQtd);
                wrap.Children.Add(linha);
                wrap.Children.Add(UiFactory.Muted(
                    "A numeração segue a casa do número inicial (ex: 101 -> 101;102...; 201 -> 201;202...). " +
                    "O PDV 888 é sempre incluído automaticamente no final.", 10));
                _pdvsEntries[campo.Id] = (entryInicio, entryQtd);
                break;
            }

            case FieldType.MesmoQue:
            {
                var varMesmo = new CheckBox
                {
                    Content = campo.PerguntaMesmo ?? "Usar o mesmo valor?", IsChecked = true,
                    Foreground = Theme.Text, FontFamily = new FontFamily(Theme.FontUi), FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 4),
                };
                var entry = UiFactory.Entry();
                entry.IsEnabled = false;
                varMesmo.Checked += (_, _) => entry.IsEnabled = false;
                varMesmo.Unchecked += (_, _) => entry.IsEnabled = true;
                wrap.Children.Add(varMesmo);
                wrap.Children.Add(entry);
                _varMesmo[campo.Id] = varMesmo;
                _entries[campo.Id] = entry;
                break;
            }

            case FieldType.IpAuto:
            {
                var ipAtualValido = ValidationHelper.IpRegex().IsMatch(valorAtual);
                var ipInicial = ipAtualValido ? valorAtual : ValidationHelper.DetectarIpLocal();
                var linha = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                linha.ColumnDefinitions.Add(new ColumnDefinition());
                linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var entry = UiFactory.Entry();
                entry.Text = ipInicial;
                entry.Margin = new Thickness(0, 0, 5, 0);
                var btnDetectar = UiFactory.OutlineButton("Detectar novamente", 32);
                btnDetectar.Width = 150;
                btnDetectar.Click += (_, _) => entry.Text = ValidationHelper.DetectarIpLocal();
                Grid.SetColumn(entry, 0);
                Grid.SetColumn(btnDetectar, 1);
                linha.Children.Add(entry);
                linha.Children.Add(btnDetectar);
                wrap.Children.Add(linha);
                _entries[campo.Id] = entry;
                break;
            }

            case FieldType.Espelho:
            {
                string origemTxt;
                if (!string.IsNullOrEmpty(campo.OutroApp))
                {
                    var campoRef = InstallProfiles.Profiles[campo.OutroApp].Fields.First(c => c.Id == campo.Referencia);
                    origemTxt = $"{campo.OutroApp} — \"{campoRef.Label}\"";
                }
                else
                {
                    var campoRef = _perfil.Fields.First(c => c.Id == campo.Referencia);
                    origemTxt = $"\"{campoRef.Label}\"";
                }
                wrap.Children.Add(UiFactory.Muted(
                    $"Preenchido automaticamente com o mesmo valor de {origemTxt} — nenhuma ação necessária aqui.", 11));
                break;
            }

            case FieldType.MesmoQueOutroApp:
            {
                string? valorConhecido = null;
                if (campo.OutroApp is not null && _contextoValores.TryGetValue(campo.OutroApp, out var ctxOutro))
                    valorConhecido = ctxOutro.GetValueOrDefault(campo.Referencia!);
                _valorConhecidoMesmoOutroApp[campo.Id] = valorConhecido;

                var varMesmo = new CheckBox
                {
                    Content = campo.PerguntaMesmo ?? "Usar o mesmo valor de outro módulo?", IsChecked = true,
                    Foreground = Theme.Text, FontFamily = new FontFamily(Theme.FontUi), FontSize = 11,
                    Margin = new Thickness(0, 4, 0, 4),
                };
                var entry = UiFactory.Entry();
                entry.IsEnabled = false;
                if (valorConhecido is not null) entry.Text = valorConhecido;
                varMesmo.Checked += (_, _) => entry.IsEnabled = false;
                varMesmo.Unchecked += (_, _) => entry.IsEnabled = true;
                wrap.Children.Add(varMesmo);
                wrap.Children.Add(entry);
                if (valorConhecido is not null)
                    wrap.Children.Add(UiFactory.Muted($"Detectado automaticamente da etapa anterior ({campo.OutroApp}).", 10));
                _varMesmo[campo.Id] = varMesmo;
                _entries[campo.Id] = entry;
                break;
            }

            case FieldType.ListaLojas:
            {
                var linhaQtd = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                linhaQtd.Children.Add(new TextBlock
                {
                    Text = "Quantidade de lojas:", FontFamily = new FontFamily(Theme.FontUi), FontSize = 11,
                    Foreground = Theme.TextMuted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0),
                });
                var entryQtd = UiFactory.Entry();
                entryQtd.Width = 60;
                linhaQtd.Children.Add(entryQtd);
                var btnGerar = UiFactory.OutlineButton("Gerar campos", 32);
                btnGerar.Width = 140;
                btnGerar.Margin = new Thickness(8, 0, 0, 0);
                linhaQtd.Children.Add(btnGerar);
                wrap.Children.Add(linhaQtd);

                var frameLojas = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                wrap.Children.Add(frameLojas);

                _entryQtdLojas[campo.Id] = entryQtd;
                _frameLojas[campo.Id] = frameLojas;
                _linhasLoja[campo.Id] = new List<LinhaLoja>();

                btnGerar.Click += (_, _) => GerarLinhasLojas(campo);
                break;
            }
        }

        return wrap;
    }

    private void AtualizarSimNaoVisual(string campoId)
    {
        var (btnSim, btnNao) = _simNaoBotoes[campoId];
        var valor = _simNaoValores[campoId];
        btnSim.Background = valor == "SIM" ? Theme.Accent : Theme.Bg;
        btnSim.Foreground = valor == "SIM" ? Brushes.White : Theme.Text;
        btnNao.Background = valor == "NAO" ? Theme.Accent : Theme.Bg;
        btnNao.Foreground = valor == "NAO" ? Brushes.White : Theme.Text;
    }

    private void GerarLinhasLojas(InstallField campo)
    {
        var qtdTxt = _entryQtdLojas[campo.Id].Text.Trim();
        if (!int.TryParse(qtdTxt, out var qtd) || qtd <= 0)
        {
            _lblErro.Text = $"\"{campo.Label}\": informe uma quantidade válida (maior que zero).";
            return;
        }
        _lblErro.Text = "";

        var frame = _frameLojas[campo.Id];
        frame.Children.Clear();
        var linhas = new List<LinhaLoja>();

        for (var i = 1; i <= qtd; i++)
        {
            var linha = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            linha.Children.Add(new TextBlock
            {
                Text = $"Loja {i:D3}", Width = 65, FontFamily = new FontFamily(Theme.FontUi), FontSize = 11,
                FontWeight = FontWeights.Bold, Foreground = Theme.Text, VerticalAlignment = VerticalAlignment.Center,
            });
            var entryNome = UiFactory.Entry(30, "Nome da loja");
            entryNome.Width = 160;
            entryNome.Margin = new Thickness(6, 0, 6, 0);
            var entryCnpj = UiFactory.Entry(30, "CNPJ (só números)");
            entryCnpj.Width = 170;
            linha.Children.Add(entryNome);
            linha.Children.Add(entryCnpj);
            frame.Children.Add(linha);
            linhas.Add(new LinhaLoja { Numero = i, EntryNome = entryNome, EntryCnpj = entryCnpj });
        }

        _linhasLoja[campo.Id] = linhas;
    }

    // --- Salvar -------------------------------------------------------------------------
    private sealed class ErroValidacao : Exception
    {
        public ErroValidacao(string mensagem) : base(mensagem) { }
    }

    private void Salvar()
    {
        _lblErro.Text = "";
        var valoresPorId = new Dictionary<string, object>();

        try
        {
            // 1ª passada: campos "normais" (independentes)
            foreach (var campo in _perfil.Fields)
            {
                switch (campo.Tipo)
                {
                    case FieldType.Texto:
                    {
                        var valor = _entries[campo.Id].Text.Trim();
                        if (valor.Length == 0) throw new ErroValidacao($"Preencha o campo \"{campo.Label}\".");
                        valoresPorId[campo.Id] = valor;
                        break;
                    }
                    case FieldType.Ip:
                    {
                        var valor = _entries[campo.Id].Text.Trim();
                        ValidarIpComTeste(valor, campo);
                        valoresPorId[campo.Id] = valor;
                        break;
                    }
                    case FieldType.Numero:
                    {
                        var valor = _entries[campo.Id].Text.Trim();
                        if (!valor.All(char.IsDigit) || valor.Length == 0)
                            throw new ErroValidacao($"\"{campo.Label}\": informe apenas números.");
                        valoresPorId[campo.Id] = campo.Largura.HasValue ? valor.PadLeft(campo.Largura.Value, '0') : valor;
                        break;
                    }
                    case FieldType.SimNao:
                        valoresPorId[campo.Id] = _simNaoValores[campo.Id];
                        break;

                    case FieldType.PdvsValidos:
                    {
                        var (entryInicio, entryQtd) = _pdvsEntries[campo.Id];
                        var inicio = entryInicio.Text.Trim();
                        var qtdTxt = entryQtd.Text.Trim();
                        if (inicio.Length != 3 || !inicio.All(char.IsDigit))
                            throw new ErroValidacao($"\"{campo.Label}\": número inicial deve ter 3 dígitos (ex: 101).");
                        if (!int.TryParse(qtdTxt, out var qtd) || qtd <= 0)
                            throw new ErroValidacao($"\"{campo.Label}\": informe a quantidade de PDVs.");
                        var numeroInicial = int.Parse(inicio);
                        var largura = inicio.Length;
                        var numeros = Enumerable.Range(0, qtd).Select(i => (numeroInicial + i).ToString().PadLeft(largura, '0')).ToList();
                        if (!numeros.Contains("888")) numeros.Add("888");
                        valoresPorId[campo.Id] = string.Join(";", numeros);
                        break;
                    }

                    case FieldType.IpAuto:
                    {
                        var valor = _entries[campo.Id].Text.Trim();
                        if (!ValidationHelper.IpRegex().IsMatch(valor))
                            throw new ErroValidacao($"\"{campo.Label}\": informe um IP válido.");
                        valoresPorId[campo.Id] = valor;
                        break;
                    }

                    case FieldType.ListaLojas:
                    {
                        var linhas = _linhasLoja.GetValueOrDefault(campo.Id) ?? new List<LinhaLoja>();
                        if (linhas.Count == 0)
                            throw new ErroValidacao($"\"{campo.Label}\": clique em \"Gerar campos\" e preencha as lojas.");
                        var prefixoBase = campo.PrefixoChave ?? "LJ";
                        var rotuloNome = campo.CampoNome ?? "Nome";
                        var rotuloCnpj = campo.CampoCnpj ?? "CNPJ";
                        var pares = new List<(string Chave, string Valor)>();
                        foreach (var l in linhas)
                        {
                            var nome = l.EntryNome.Text.Trim();
                            var cnpj = new string(l.EntryCnpj.Text.Where(char.IsDigit).ToArray());
                            if (nome.Length == 0)
                                throw new ErroValidacao($"Informe o nome da Loja {l.Numero:D3}.");
                            if (cnpj.Length != 14 || !ValidationHelper.ValidarCnpj(cnpj))
                                throw new ErroValidacao($"CNPJ da Loja {l.Numero:D3} parece inválido (dígito verificador não confere). Confira a digitação.");
                            var prefixo = $"{prefixoBase} {l.Numero:D3}";
                            pares.Add(($"{prefixo} {rotuloNome}", nome));
                            pares.Add(($"{prefixo} {rotuloCnpj}", cnpj));
                        }
                        valoresPorId[campo.Id] = pares;
                        break;
                    }
                }
            }

            // 2ª passada: "mesmo_que", "espelho" e "mesmo_que_outro_app"
            foreach (var campo in _perfil.Fields)
            {
                switch (campo.Tipo)
                {
                    case FieldType.Espelho:
                    {
                        string? valor;
                        if (!string.IsNullOrEmpty(campo.OutroApp))
                        {
                            valor = _contextoValores.TryGetValue(campo.OutroApp, out var ctx) ? ctx.GetValueOrDefault(campo.Referencia!) : null;
                            valor ??= BuscarValorNoIniDeOutroApp(campo.OutroApp, campo.Referencia!);
                            if (valor is null)
                                throw new ErroValidacao($"\"{campo.Label}\": não consegui localizar automaticamente o valor no " +
                                                         $"{campo.OutroApp}. Configure o {campo.OutroApp} primeiro.");
                        }
                        else
                        {
                            valor = valoresPorId.GetValueOrDefault(campo.Referencia!) as string;
                            if (valor is null) throw new ErroValidacao($"\"{campo.Label}\": não localizei o valor de referência.");
                        }
                        valoresPorId[campo.Id] = valor;
                        break;
                    }

                    case FieldType.MesmoQueOutroApp:
                    {
                        string valor;
                        if (_varMesmo[campo.Id].IsChecked == true)
                        {
                            valor = _valorConhecidoMesmoOutroApp.GetValueOrDefault(campo.Id) ??
                                    BuscarValorNoIniDeOutroApp(campo.OutroApp!, campo.Referencia!) ?? "";
                            if (valor.Length == 0 || !ValidationHelper.IpRegex().IsMatch(valor))
                                throw new ErroValidacao($"\"{campo.Label}\": não consegui localizar automaticamente o IP do " +
                                                         $"{campo.OutroApp}. Desmarque a opção e informe manualmente.");
                        }
                        else
                        {
                            valor = _entries[campo.Id].Text.Trim();
                            ValidarIpComTeste(valor, campo);
                        }
                        valoresPorId[campo.Id] = valor;
                        break;
                    }

                    case FieldType.MesmoQue:
                    {
                        string valor;
                        if (_varMesmo[campo.Id].IsChecked == true)
                        {
                            valor = valoresPorId.GetValueOrDefault(campo.Referencia!) as string
                                    ?? throw new ErroValidacao($"\"{campo.Label}\": não localizei o valor de referência.");
                        }
                        else
                        {
                            valor = _entries[campo.Id].Text.Trim();
                            if (!ValidationHelper.IpRegex().IsMatch(valor))
                                throw new ErroValidacao($"\"{campo.Label}\": informe um IP válido.");
                        }
                        valoresPorId[campo.Id] = valor;
                        break;
                    }
                }
            }
        }
        catch (ErroValidacao ex)
        {
            _lblErro.Text = ex.Message;
            return;
        }

        var alteracoes = new List<(string Secao, string Chave, string Valor)>();
        foreach (var campo in _perfil.Fields)
        {
            if (campo.Tipo == FieldType.ListaLojas)
            {
                foreach (var (chave, valor) in (List<(string Chave, string Valor)>)valoresPorId[campo.Id])
                    alteracoes.Add((campo.Section!, chave, valor));
            }
            else
            {
                alteracoes.Add((campo.Section!, campo.Key!, (string)valoresPorId[campo.Id]));
            }
        }

        // Tela de conferência: mostra o que realmente vai mudar antes de gravar de vez.
        var resumoLinhas = new List<string>();
        foreach (var campo in _perfil.Fields)
        {
            if (campo.Tipo == FieldType.ListaLojas)
            {
                var qtdLojas = ((List<(string Chave, string Valor)>)valoresPorId[campo.Id]).Count / 2;
                resumoLinhas.Add($"{campo.Label}: {qtdLojas} loja(s) configurada(s)");
                continue;
            }
            var antigo = (IniFileService.LerValor(_iniPath, campo.Section!, campo.Key!) ?? "").Trim();
            var novo = ((string)valoresPorId[campo.Id]).Trim();
            if (antigo != novo)
                resumoLinhas.Add($"{campo.Label}: \"{(antigo.Length == 0 ? "(vazio)" : antigo)}\" → \"{novo}\"");
        }

        var textoResumo = resumoLinhas.Count > 0 ? string.Join("\n", resumoLinhas) : "Nenhuma alteração detectada.";
        var confirmar = MessageBox.Show(
            $"As seguintes alterações serão gravadas em {System.IO.Path.GetFileName(_iniPath)}:\n\n{textoResumo}\n\nConfirmar e gravar?",
            "Confirmar alterações", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirmar != MessageBoxResult.Yes) return;

        try
        {
            IniFileService.GravarValores(_iniPath, alteracoes);
        }
        catch (Exception e)
        {
            _lblErro.Text = $"Erro ao gravar o arquivo: {ErrorMessages.Amigavel(e)}";
            return;
        }

        if (_chaveContexto is not null)
        {
            var contextoDoApp = new Dictionary<string, string>();
            foreach (var (id, valor) in valoresPorId)
                if (valor is string s) contextoDoApp[id] = s;
            _contextoValores[_chaveContexto] = contextoDoApp;
        }

        Logger.RegistrarAuditoria(
            $"Configurado {System.IO.Path.GetFileName(_iniPath)} ({_chaveContexto ?? "?"}): " +
            (resumoLinhas.Count > 0 ? string.Join(" | ", resumoLinhas) : "sem alterações"));

        MessageBox.Show("As configurações foram gravadas no arquivo .ini com sucesso.", "Configuração salva",
            MessageBoxButton.OK, MessageBoxImage.Information);
        _onConcluir();
    }

    private static void ValidarIpComTeste(string valor, InstallField campo)
    {
        if (!ValidationHelper.IpRegex().IsMatch(valor))
            throw new ErroValidacao($"\"{campo.Label}\": informe um IP válido (ex: 192.168.0.10).");
        if (campo.TestarPorta is { } porta && !ValidationHelper.TestarConexaoTcp(valor, porta))
        {
            var resposta = MessageBox.Show(
                $"Não consegui alcançar {valor}:{porta} nesta rede.\n\nPode ser só um firewall bloqueando o teste, " +
                "mas confira se o IP está certo. Gravar mesmo assim?", "Não consegui conectar",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (resposta != MessageBoxResult.Yes)
                throw new ErroValidacao($"\"{campo.Label}\": confirme o IP e tente novamente.");
        }
    }

    /// <summary>
    /// Tenta ler, direto do disco, um valor já configurado no .ini de OUTRO app (usado
    /// quando não veio pelo contexto da mesma instalação em cadeia).
    /// </summary>
    private static string? BuscarValorNoIniDeOutroApp(string outroApp, string campoIdNoOutroPerfil)
    {
        if (!InstallProfiles.Profiles.TryGetValue(outroApp, out var outroPerfil)) return null;
        var outroCampo = outroPerfil.Fields.FirstOrDefault(c => c.Id == campoIdNoOutroPerfil);
        if (outroCampo is null) return null;
        var localizacao = InstallProfiles.AppLocalizacao.GetValueOrDefault(outroApp);
        var outroDir = InstallPathsStore.Localizar(localizacao?.NomeApp ?? outroApp, localizacao?.Exes ?? new List<string>());
        if (outroDir is null) return null;
        var outroIni = System.IO.Path.Combine(outroDir, outroPerfil.IniFilename);
        return System.IO.File.Exists(outroIni) ? IniFileService.LerValor(outroIni, outroCampo.Section!, outroCampo.Key!) : null;
    }
}
