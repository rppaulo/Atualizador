namespace Atulizador.Models;

/// <summary>
/// Um módulo "bundled" dentro de um app do Atualizador (ex.: ImpArq dentro do ServerMatriz)
/// — equivalente aos dicionários da lista "bundled" em apps_config no Python.
/// </summary>
public sealed class BundledApp
{
    public required string Subdir { get; init; }
    public required List<string> Exes { get; init; }
}

/// <summary>
/// Um app gerenciado pela página Atualizador — equivalente a cada dicionário de
/// apps_config no AtualizadorPage do Python.
/// </summary>
public sealed class UpdaterAppConfig
{
    public required string Name { get; init; }
    public required string Subdir { get; init; }
    public required List<string> Exes { get; init; }
    public List<BundledApp> Bundled { get; init; } = new();
    public bool NeedsDll { get; init; }
}
