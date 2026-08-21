namespace Atulizador.Models;

/// <summary>
/// Tipos de campo suportados num perfil de instalação (equivalente ao "tipo" string
/// usado no dicionário Python INSTALL_PROFILES).
/// </summary>
public enum FieldType
{
    Texto,
    Ip,
    Numero,
    SimNao,
    PdvsValidos,
    MesmoQue,
    IpAuto,
    Espelho,
    MesmoQueOutroApp,
    ListaLojas,
}
