namespace Atulizador.Models;

/// <summary>
/// Perfil declarativo de configuração pós-instalação de um app — equivalente a cada
/// entrada de INSTALL_PROFILES no script Python.
/// </summary>
public sealed class InstallProfile
{
    public required string IniFilename { get; init; }
    public required List<InstallField> Fields { get; init; }
}

/// <summary>
/// Onde procurar um app já instalado e qual palavra-chave usar para achar o pacote de
/// instalação (.rar) certo — equivalente a APP_LOCALIZACAO no script Python.
/// </summary>
public sealed class AppLocation
{
    public required string NomeApp { get; init; }
    public required List<string> Exes { get; init; }
    public required List<string> PalavrasChaveInstalacao { get; init; }
}
