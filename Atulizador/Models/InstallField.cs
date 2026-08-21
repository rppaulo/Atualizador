namespace Atulizador.Models;

/// <summary>
/// Um campo dentro de um <see cref="InstallProfile"/> — equivalente a cada dicionário
/// dentro da lista "fields" em INSTALL_PROFILES no script Python.
/// </summary>
public sealed class InstallField
{
    public required string Id { get; init; }
    public string? Section { get; init; }
    public string? Key { get; init; }
    public required string Label { get; init; }
    public required FieldType Tipo { get; init; }
    public string? Ajuda { get; init; }

    /// <summary>Largura para zero-padding (tipo "numero").</summary>
    public int? Largura { get; init; }

    /// <summary>Porta TCP a testar antes de aceitar um IP (tipo "ip"/"mesmo_que_outro_app").</summary>
    public int? TestarPorta { get; init; }

    /// <summary>Id do campo referenciado (tipos "mesmo_que", "espelho", "mesmo_que_outro_app").</summary>
    public string? Referencia { get; init; }

    /// <summary>Chave do outro perfil referenciado (tipos "espelho", "mesmo_que_outro_app").</summary>
    public string? OutroApp { get; init; }

    /// <summary>Texto da pergunta "usar o mesmo valor?" (tipos "mesmo_que", "mesmo_que_outro_app").</summary>
    public string? PerguntaMesmo { get; init; }

    /// <summary>Prefixo de chave/campos para o tipo "lista_lojas" (ex.: "LJ").</summary>
    public string? PrefixoChave { get; init; }
    public string? CampoNome { get; init; }
    public string? CampoCnpj { get; init; }
}
