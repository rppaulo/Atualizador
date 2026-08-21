namespace Atulizador.Models;

/// <summary>Um app que foi fechado (kill) durante a atualização, para poder reabrir depois.</summary>
public sealed record AppFechado(string Nome, string CaminhoExe);
