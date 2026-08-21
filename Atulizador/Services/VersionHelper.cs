using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Atulizador.Services;

/// <summary>
/// Utilitários de versão — equivalentes a get_file_version, parse_version_from_filename,
/// normalize_version, compare_versions e version_sort_key no script Python.
/// </summary>
public static partial class VersionHelper
{
    [GeneratedRegex(@"[_-](\d+\.\d+\.\d+(?:\.\d+)?)\.rar$", RegexOptions.IgnoreCase)]
    private static partial Regex FilenameVersionRegex();

    [GeneratedRegex(@"[^\d.]")]
    private static partial Regex NonNumericDotRegex();

    /// <summary>Lê a versão de arquivo (FileVersionInfo) — dispensa a dependência pywin32 do original.</summary>
    public static string GetFileVersion(string filepath)
    {
        try
        {
            if (!File.Exists(filepath))
                return "0.0.0.0";
            var info = FileVersionInfo.GetVersionInfo(filepath);
            return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}.{info.FilePrivatePart}";
        }
        catch
        {
            return "0.0.0.0";
        }
    }

    public static string ParseVersionFromFilename(string filename)
    {
        var match = FilenameVersionRegex().Match(filename);
        return match.Success ? match.Groups[1].Value : "0.0.0.0";
    }

    public static List<int> NormalizeVersion(string v)
    {
        var limpo = NonNumericDotRegex().Replace(v, "");
        return limpo.Split('.')
            .Where(x => x.Length > 0 && x.All(char.IsDigit))
            .Select(int.Parse)
            .ToList();
    }

    /// <summary>True se remoteV for maior que localV.</summary>
    public static bool CompareVersions(string remoteV, string localV)
    {
        var r = NormalizeVersion(remoteV);
        var l = NormalizeVersion(localV);
        var tamanho = Math.Max(r.Count, l.Count);
        for (var i = 0; i < tamanho; i++)
        {
            var rv = i < r.Count ? r[i] : 0;
            var lv = i < l.Count ? l[i] : 0;
            if (rv > lv) return true;
            if (rv < lv) return false;
        }
        return false;
    }

    /// <summary>Chave de ordenação por versão embutida no nome do arquivo (usada em .OrderBy).</summary>
    public static string VersionSortKey(string filename) => ParseVersionFromFilename(filename);

    /// <summary>Comparer para ordenar nomes de arquivo pela versão embutida no nome.</summary>
    public sealed class ByEmbeddedVersionComparer : IComparer<string>
    {
        public static readonly ByEmbeddedVersionComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var vx = NormalizeVersion(ParseVersionFromFilename(x ?? ""));
            var vy = NormalizeVersion(ParseVersionFromFilename(y ?? ""));
            var tamanho = Math.Max(vx.Count, vy.Count);
            for (var i = 0; i < tamanho; i++)
            {
                var a = i < vx.Count ? vx[i] : 0;
                var b = i < vy.Count ? vy[i] : 0;
                var cmp = a.CompareTo(b);
                if (cmp != 0) return cmp;
            }
            return 0;
        }
    }
}
