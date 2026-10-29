using System.Text;

namespace SearchPatternMiner;

/// <summary>Python string semantics the port depends on (whitespace, split, strip, splitlines).</summary>
internal static class Py
{
    /// <summary>Python's <c>str.isspace</c> for one character (also what <c>str.split()</c>, <c>strip()</c> and <c>\s</c> use).</summary>
    public static bool IsSpace(char c) =>
        c is ' ' or '\t' or '\n' or '\v' or '\f' or '\r' or '\x1c' or '\x1d' or '\x1e' or '\x1f' or '\x85'
        || (c > '\x7f' && char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.SpaceSeparator
            or System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator);

    /// <summary><c>str.split()</c>: runs of whitespace, no empty items.</summary>
    public static List<string> Split(string s)
    {
        var out_ = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            while (i < s.Length && IsSpace(s[i])) i++;
            if (i >= s.Length) break;
            int start = i;
            while (i < s.Length && !IsSpace(s[i])) i++;
            out_.Add(s.Substring(start, i - start));
        }
        return out_;
    }

    /// <summary><c>str.strip()</c>: whitespace from both ends.</summary>
    public static string Strip(string s)
    {
        int a = 0, b = s.Length;
        while (a < b && IsSpace(s[a])) a++;
        while (b > a && IsSpace(s[b - 1])) b--;
        return s.Substring(a, b - a);
    }

    /// <summary><c>str.strip(chars)</c>: any of <paramref name="chars"/> from both ends.</summary>
    public static string Strip(string s, string chars)
    {
        int a = 0, b = s.Length;
        while (a < b && chars.IndexOf(s[a]) >= 0) a++;
        while (b > a && chars.IndexOf(s[b - 1]) >= 0) b--;
        return s.Substring(a, b - a);
    }

    /// <summary><c>re.sub(r"\s+", " ", s)</c>.</summary>
    public static string CollapseSpaces(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool inSpace = false;
        foreach (var c in s)
        {
            if (IsSpace(c))
            {
                if (!inSpace) sb.Append(' ');
                inSpace = true;
            }
            else
            {
                sb.Append(c);
                inSpace = false;
            }
        }
        return sb.ToString();
    }

    /// <summary><c>str.splitlines()</c>.</summary>
    public static List<string> SplitLines(string s)
    {
        var lines = new List<string>();
        int start = 0, i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c is '\n' or '\r' or '\v' or '\f' or '\x1c' or '\x1d' or '\x1e' or '\x85' or '\u2028' or '\u2029')
            {
                lines.Add(s.Substring(start, i - start));
                if (c == '\r' && i + 1 < s.Length && s[i + 1] == '\n') i++;
                i++;
                start = i;
            }
            else i++;
        }
        if (start < s.Length) lines.Add(s.Substring(start));
        return lines;
    }

    /// <summary>Python's <c>str.replace</c> (all non-overlapping occurrences, left to right).</summary>
    public static string Replace(string s, string a, string b) => s.Replace(a, b, StringComparison.Ordinal);
}
