using System.Windows.Controls;

namespace Atulizador.Views.Controls;

/// <summary>Uma linha Nome/CNPJ dentro do campo tipo "lista_lojas".</summary>
public sealed class LinhaLoja
{
    public required int Numero { get; init; }
    public required TextBox EntryNome { get; init; }
    public required TextBox EntryCnpj { get; init; }
}
