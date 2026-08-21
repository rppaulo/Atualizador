using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Atulizador.Config;

namespace Atulizador.Views;

/// <summary>
/// Fábrica de controles com o estilo visual do app (equivalente às chamadas repetidas de
/// customtkinter com os mesmos parâmetros de cor/fonte no script Python). A UI é
/// majoritariamente montada em código-behind, do mesmo jeito imperativo do original.
/// </summary>
public static class UiFactory
{
    public static Border Card(UIElement? child = null, Thickness? padding = null)
    {
        var border = new Border
        {
            Background = Theme.Card,
            BorderBrush = Theme.CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Padding = padding ?? new Thickness(15),
        };
        if (child is not null) border.Child = child;
        return border;
    }

    public static TextBlock Title(string text, double size = 16, FontWeight? weight = null, Brush? color = null) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily(Theme.FontUi),
            FontSize = size,
            FontWeight = weight ?? FontWeights.Bold,
            Foreground = color ?? Theme.Text,
        };

    public static TextBlock Muted(string text, double size = 12) =>
        new()
        {
            Text = text,
            FontFamily = new FontFamily(Theme.FontUi),
            FontSize = size,
            Foreground = Theme.TextMuted,
            TextWrapping = TextWrapping.Wrap,
        };

    public static Button PrimaryButton(string text, double height = 40)
    {
        var btn = new Button
        {
            Content = text,
            Height = height,
            Background = Theme.Accent,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontFamily = new FontFamily(Theme.FontUi),
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand,
        };
        StyleHoverBackground(btn, Theme.Accent, Theme.AccentHover);
        return btn;
    }

    public static Button OutlineButton(string text, double height = 30)
    {
        var btn = new Button
        {
            Content = text,
            Height = height,
            Background = Brushes.Transparent,
            Foreground = Theme.Text,
            BorderBrush = Theme.Accent,
            BorderThickness = new Thickness(1),
            FontFamily = new FontFamily(Theme.FontUi),
            FontWeight = FontWeights.Bold,
            Cursor = Cursors.Hand,
        };
        return btn;
    }

    public static Button GhostButton(string text, double height = 38)
    {
        var btn = new Button
        {
            Content = text,
            Height = height,
            Background = Brushes.Transparent,
            Foreground = Theme.TextMuted,
            BorderBrush = Theme.CardBorder,
            BorderThickness = new Thickness(1),
            FontFamily = new FontFamily(Theme.FontUi),
            Cursor = Cursors.Hand,
        };
        return btn;
    }

    private static void StyleHoverBackground(Button btn, Brush normal, Brush hover)
    {
        btn.MouseEnter += (_, _) => btn.Background = hover;
        btn.MouseLeave += (_, _) => btn.Background = normal;
    }

    public static TextBox Entry(double height = 32, string placeholder = "")
    {
        var tb = new TextBox
        {
            Height = height,
            BorderBrush = Theme.CardBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily(Theme.FontUi),
        };
        if (!string.IsNullOrEmpty(placeholder)) Placeholder.SetText(tb, placeholder);
        return tb;
    }

    public static PasswordBox PasswordEntry(double height = 40)
    {
        return new PasswordBox
        {
            Height = height,
            BorderBrush = Theme.CardBorder,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily(Theme.FontUi),
        };
    }

    public static ProgressBar Progress(double height = 8) =>
        new()
        {
            Height = height,
            Background = Theme.CardBorder,
            Foreground = Theme.Accent,
            Minimum = 0,
            Maximum = 1,
            Value = 0,
        };
}

/// <summary>Watermark/placeholder simples para TextBox — WPF não tem isso nativamente.</summary>
public static class Placeholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(Placeholder), new PropertyMetadata(string.Empty, OnChanged));

    public static string GetText(DependencyObject obj) => (string)obj.GetValue(TextProperty);
    public static void SetText(DependencyObject obj, string value) => obj.SetValue(TextProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        tb.Loaded += (_, _) => Atualizar(tb);
        tb.TextChanged += (_, _) => Atualizar(tb);
    }

    private static void Atualizar(TextBox tb)
    {
        var placeholder = GetText(tb);
        if (string.IsNullOrEmpty(tb.Text) && !string.IsNullOrEmpty(placeholder))
        {
            tb.Background = new VisualBrush
            {
                Visual = new TextBlock
                {
                    Text = placeholder,
                    Foreground = Brushes.Gray,
                    FontFamily = new FontFamily(Theme.FontUi),
                    FontSize = 12,
                    Margin = new Thickness(4, 0, 0, 0),
                },
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Center,
                Stretch = Stretch.None,
            };
        }
        else
        {
            tb.Background = Brushes.White;
        }
    }
}
