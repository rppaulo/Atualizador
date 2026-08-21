using System.Windows.Media;

namespace Atulizador.Config;

/// <summary>
/// Paleta de cores e fontes — espelha as constantes COLOR_*/FONT_* do script Python.
/// Mantida em C# (além do App.xaml) para uso direto em código-behind, já que a UI é
/// majoritariamente construída via código, do mesmo jeito que o original em customtkinter.
/// </summary>
public static class Theme
{
    public static readonly Brush Bg = Brush("#F4F6FA");
    public static readonly Brush Card = Brush("#FFFFFF");
    public static readonly Brush CardBorder = Brush("#E2E5EC");
    public static readonly Brush Accent = Brush("#3457FF");
    public static readonly Brush AccentHover = Brush("#2842D4");
    public static readonly Brush Text = Brush("#161B2E");
    public static readonly Brush TextMuted = Brush("#6C7488");
    public static readonly Brush Success = Brush("#15915A");
    public static readonly Brush Danger = Brush("#D8353F");
    public static readonly Brush Yellow = Brush("#B4650A");

    public static readonly Brush Sidebar = Brush("#0B1229");
    public static readonly Brush SidebarActive = Brush("#1B2447");
    public static readonly Brush SidebarText = Brush("#E7E9F3");
    public static readonly Brush SidebarTextMuted = Brush("#8A91AC");

    // Console de log: sempre escuro, estilo terminal, independente do tema geral.
    public static readonly Brush ConsoleBg = Brush("#121212");
    public static readonly Brush ConsoleText = Brush("#D1D5DB");
    public static readonly Brush ConsoleSuccess = Brush("#4CAF50");
    public static readonly Brush ConsoleDanger = Brush("#F44336");
    public static readonly Brush ConsoleWarning = Brush("#FFEB3B");
    public static readonly Brush ConsoleSys = Brush("#569CD6");

    public const string FontUi = "Segoe UI";
    public const string FontMono = "Consolas";

    private static Brush Brush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
