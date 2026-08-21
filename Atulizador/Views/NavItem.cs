namespace Atulizador.Views;

/// <summary>Um item da árvore de navegação da sidebar — equivalente às entradas de self.nav_items no Python.</summary>
public sealed class NavItem
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public List<NavItem>? Children { get; init; }
}
