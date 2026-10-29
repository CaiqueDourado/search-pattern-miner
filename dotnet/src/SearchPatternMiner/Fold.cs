using System.Text;

namespace SearchPatternMiner;

/// <summary>Accent folding by an explicit table, so .NET and Python give the same result without Unicode normalization.</summary>
public static class Fold
{
    /// <summary>The folding table (same as <c>FOLD</c> in <c>fold.py</c>).</summary>
    public static readonly IReadOnlyDictionary<char, char> Table = new Dictionary<char, char>
    {
        ['á'] = 'a', ['à'] = 'a', ['â'] = 'a', ['ã'] = 'a', ['ä'] = 'a', ['é'] = 'e', ['è'] = 'e', ['ê'] = 'e', ['ë'] = 'e',
        ['í'] = 'i', ['ì'] = 'i', ['î'] = 'i', ['ï'] = 'i', ['ó'] = 'o', ['ò'] = 'o', ['ô'] = 'o', ['õ'] = 'o', ['ö'] = 'o',
        ['ú'] = 'u', ['ù'] = 'u', ['û'] = 'u', ['ü'] = 'u', ['ç'] = 'c', ['ñ'] = 'n', ['ý'] = 'y', ['ÿ'] = 'y',
    };

    /// <summary>Lower case and remove the accents of Portuguese and Spanish letters.</summary>
    public static string Apply(string text) => Translate(text.ToLowerInvariant(), Table);

    internal static string Translate(string text, IReadOnlyDictionary<char, char> table)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            sb.Append(table.TryGetValue(c, out var r) ? r : c);
        }
        return sb.ToString();
    }
}
