namespace Atulizador.Models;

/// <summary>
/// Resultado de <c>SelfUpdateService.VerificarAtualizacaoAsync</c> — equivalente ao dict
/// retornado por verificar_atualizacao_toolkit_github() no Python.
/// </summary>
public sealed class GithubUpdateInfo
{
    public required string Versao { get; init; }
    public required string UrlDownload { get; init; }
    public required string NomeArquivo { get; init; }
    public string? HashEsperado { get; init; }
    public string Notas { get; init; } = string.Empty;
}

/// <summary>
/// Opção de instalação "Completo"/"Parcial" de uma página de InstaladorServidor —
/// equivalente às entradas de opcoes_instalacao no Python.
/// </summary>
public sealed class OpcaoInstalacao
{
    public required string Descricao { get; init; }
    public required List<string> Apps { get; init; }
    public required List<string> PerfisPosInstalacao { get; init; }
}
