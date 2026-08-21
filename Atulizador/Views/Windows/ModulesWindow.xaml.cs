using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Atulizador.Config;
using Atulizador.Models;

namespace Atulizador.Views.Windows;

/// <summary>
/// Janela de seleção de módulos para o Atualizador — equivalente a ModulesWindow no
/// script Python. Os checkboxes escrevem direto no dicionário <paramref name="selecionados"/>
/// compartilhado com a página que abriu esta janela (equivalente às BooleanVar do customtkinter).
/// </summary>
public sealed class ModulesWindow : Window
{
    public ModulesWindow(Window owner, List<UpdaterAppConfig> apps, Dictionary<string, bool> selecionados)
    {
        Title = "Módulos IMS";
        Owner = owner;
        Width = 400;
        Height = 550;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Theme.Bg;
        ResizeMode = ResizeMode.NoResize;

        var raiz = new DockPanel { Margin = new Thickness(20) };
        Content = raiz;

        var titulo = new TextBlock
        {
            Text = "MÓDULOS DISPONÍVEIS", FontFamily = new FontFamily(Theme.FontUi), FontSize = 16,
            FontWeight = FontWeights.Bold, Foreground = Theme.Text, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 20),
        };
        DockPanel.SetDock(titulo, Dock.Top);
        raiz.Children.Add(titulo);

        var btnConcluido = UiFactory.PrimaryButton("CONCLUÍDO");
        btnConcluido.Margin = new Thickness(0, 20, 0, 0);
        btnConcluido.Click += (_, _) => Close();
        DockPanel.SetDock(btnConcluido, Dock.Bottom);
        raiz.Children.Add(btnConcluido);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var lista = new StackPanel { Margin = new Thickness(15) };
        scroll.Content = lista;
        var card = UiFactory.Card(scroll, new Thickness(0));
        raiz.Children.Add(card);

        foreach (var app in apps)
        {
            if (!selecionados.ContainsKey(app.Name)) selecionados[app.Name] = false;
            var cb = new CheckBox
            {
                Content = app.Name, IsChecked = selecionados[app.Name], Foreground = Theme.Text,
                FontFamily = new FontFamily(Theme.FontUi), FontSize = 13, Margin = new Thickness(0, 8, 0, 8),
                Cursor = System.Windows.Input.Cursors.Hand,
            };
            cb.Checked += (_, _) => selecionados[app.Name] = true;
            cb.Unchecked += (_, _) => selecionados[app.Name] = false;
            lista.Children.Add(cb);
        }
    }
}
