using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Atulizador.Services;

/// <summary>
/// Engine de leitura/gravação de .ini legados, preservando formatação original.
///
/// A gravação NUNCA reescreve o arquivo inteiro: localiza a linha exata da chave dentro
/// da seção certa (por regex, tolerante a espaços) e troca só o valor, preservando
/// indentação, chave original e todo o restante do arquivo. Isso evita quebrar arquivos
/// legados com formatação irregular. Sempre é feito um backup (.bak_AAAAMMDDHHmmss) do
/// .ini antes de qualquer gravação.
///
/// Equivalente ao bloco "ENGINE DE CONFIGURAÇÃO DE .INI PÓS-INSTALAÇÃO" no script Python
/// (ini_ler_valor / ini_gravar_valores / _limpar_backups_antigos).
/// </summary>
public static partial class IniFileService
{
    // arquivos .ini legados desses sistemas costumam ser ANSI/latin-1
    private static readonly Encoding IniEncoding = Encoding.Latin1;

    private const int MaxBackupsIni = 5;

    [GeneratedRegex(@"^\s*\[(.+?)\]\s*$")]
    private static partial Regex SecaoRegex();

    [GeneratedRegex(@"^(\s*)([^=;][^=]*?)\s*=(.*)$")]
    private static partial Regex ChaveRegex();

    /// <summary>Divide o texto em linhas preservando os terminadores originais (\r\n, \n ou nenhum na última).</summary>
    private static List<string> DividirPreservandoQuebras(string texto)
    {
        var linhas = new List<string>();
        var inicio = 0;
        for (var i = 0; i < texto.Length; i++)
        {
            if (texto[i] != '\n') continue;
            linhas.Add(texto[inicio..(i + 1)]);
            inicio = i + 1;
        }
        if (inicio < texto.Length)
            linhas.Add(texto[inicio..]);
        return linhas;
    }

    private static (string conteudo, string quebra) SepararQuebra(string linha)
    {
        if (linha.EndsWith("\r\n")) return (linha[..^2], "\r\n");
        if (linha.EndsWith("\n")) return (linha[..^1], "\n");
        return (linha, "");
    }

    /// <summary>Lê o valor atual de uma chave dentro de uma seção específica do .ini.</summary>
    public static string? LerValor(string caminho, string secao, string chave)
    {
        List<string> linhas;
        try
        {
            linhas = DividirPreservandoQuebras(File.ReadAllText(caminho, IniEncoding));
        }
        catch
        {
            return null;
        }

        string? secaoAtual = null;
        foreach (var linhaBruta in linhas)
        {
            var (conteudo, _) = SepararQuebra(linhaBruta);
            var mSecao = SecaoRegex().Match(conteudo);
            if (mSecao.Success)
            {
                secaoAtual = mSecao.Groups[1].Value.Trim();
                continue;
            }
            if (secaoAtual != null && string.Equals(secaoAtual, secao, StringComparison.OrdinalIgnoreCase))
            {
                var mChave = ChaveRegex().Match(conteudo);
                if (mChave.Success && string.Equals(mChave.Groups[2].Value.Trim(), chave, StringComparison.OrdinalIgnoreCase))
                    return mChave.Groups[3].Value.Trim();
            }
        }
        return null;
    }

    private static void LimparBackupsAntigos(string caminho, int manter = MaxBackupsIni)
    {
        try
        {
            var pasta = Path.GetDirectoryName(caminho) ?? ".";
            var baseNome = Path.GetFileName(caminho);
            var padrao = new Regex("^" + Regex.Escape(baseNome) + @"\.bak_\d{14}$");
            var candidatos = Directory.GetFiles(pasta)
                .Select(Path.GetFileName)
                .Where(f => f != null && padrao.IsMatch(f))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();
            if (candidatos.Count > manter)
            {
                foreach (var antigo in candidatos.Take(candidatos.Count - manter))
                    File.Delete(Path.Combine(pasta, antigo!));
            }
        }
        catch
        {
            // limpeza de backup nunca deve interromper a gravação
        }
    }

    /// <summary>
    /// alteracoes: lista de tuplas (secao, chave, novoValor). Se a chave já existe na
    /// seção, só troca o valor da linha. Se a chave ainda não existe na seção (comum em
    /// listas dinâmicas, ex.: "LJ 003 Nome"), insere a linha nova logo após o cabeçalho
    /// da seção. Lança InvalidOperationException apenas se a SEÇÃO em si não existir.
    /// </summary>
    public static void GravarValores(string caminho, IReadOnlyList<(string Secao, string Chave, string Valor)> alteracoes)
    {
        var linhas = DividirPreservandoQuebras(File.ReadAllText(caminho, IniEncoding));

        var backupPath = $"{caminho}.bak_{DateTime.Now:yyyyMMddHHmmss}";
        File.Copy(caminho, backupPath, overwrite: true);
        LimparBackupsAntigos(caminho);

        var pendentes = new Dictionary<(string, string), (string Secao, string Chave, string Valor)>();
        foreach (var (s, c, v) in alteracoes)
            pendentes[(s.ToUpperInvariant(), c.ToUpperInvariant())] = (s, c, v);

        string? secaoAtual = null;
        var novasLinhas = new List<string>();

        foreach (var linhaBruta in linhas)
        {
            var mSecaoBruta = SecaoRegex().Match(SepararQuebra(linhaBruta).conteudo);
            if (mSecaoBruta.Success)
            {
                secaoAtual = mSecaoBruta.Groups[1].Value.Trim();
                novasLinhas.Add(linhaBruta);
                continue;
            }

            var (conteudo, quebra) = SepararQuebra(linhaBruta);
            var mChave = ChaveRegex().Match(conteudo);
            if (mChave.Success && secaoAtual != null)
            {
                var prefixo = mChave.Groups[1].Value;
                var chaveOriginal = mChave.Groups[2].Value.Trim();
                var lookup = (secaoAtual.ToUpperInvariant(), chaveOriginal.ToUpperInvariant());
                if (pendentes.TryGetValue(lookup, out var pendente))
                {
                    pendentes.Remove(lookup);
                    novasLinhas.Add($"{prefixo}{chaveOriginal}={pendente.Valor}{quebra}");
                    continue;
                }
            }

            novasLinhas.Add(linhaBruta);
        }

        // O que sobrou em "pendentes" são chaves que não existiam no arquivo — inserimos
        // logo após o cabeçalho da seção correspondente.
        var secoesNaoEncontradas = new HashSet<string>();
        if (pendentes.Count > 0)
        {
            var novasPorSecao = new Dictionary<string, List<string>>();
            foreach (var ((secaoUp, _), (secaoOrig, chaveOrig, valor)) in pendentes)
            {
                if (!novasPorSecao.TryGetValue(secaoUp, out var lista))
                    novasPorSecao[secaoUp] = lista = new List<string>();
                lista.Add($"{chaveOrig}={valor}\n");
            }

            var linhasComInsercoes = new List<string>();
            var secoesInseridas = new HashSet<string>();
            foreach (var linha in novasLinhas)
            {
                linhasComInsercoes.Add(linha);
                var mSecao = SecaoRegex().Match(SepararQuebra(linha).conteudo);
                if (!mSecao.Success) continue;
                var nomeSecao = mSecao.Groups[1].Value.Trim().ToUpperInvariant();
                if (novasPorSecao.TryGetValue(nomeSecao, out var novasDaSecao) && secoesInseridas.Add(nomeSecao))
                    linhasComInsercoes.AddRange(novasDaSecao);
            }
            novasLinhas = linhasComInsercoes;
            secoesNaoEncontradas = novasPorSecao.Keys.Except(secoesInseridas).ToHashSet();
        }

        File.WriteAllText(caminho, string.Concat(novasLinhas), IniEncoding);

        if (secoesNaoEncontradas.Count > 0)
        {
            var faltantes = string.Join(", ", secoesNaoEncontradas);
            throw new InvalidOperationException($"Não encontrei estas seções no arquivo para gravar os campos: {faltantes}");
        }
    }
}
