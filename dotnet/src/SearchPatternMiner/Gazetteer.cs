using System.Text;

namespace SearchPatternMiner;

/// <summary>Entities and their types: surface form to "Type" or "Type &gt; Other type" (one pattern per type).</summary>
public sealed class Gazetteer
{
    private const string Edge = "\"'.,;:!?()";
    private readonly Dictionary<string, string> _raw = new(StringComparer.Ordinal);
    private readonly List<string> _rawOrder = new();
    // multi-word keys are joined by a single space: key words never hold whitespace, so this is unambiguous
    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _exact = new(StringComparer.Ordinal);
    private readonly int _max;

    /// <summary>Build from (surface, types) pairs; the first entry for a surface wins, as in the original loader.</summary>
    public Gazetteer(IEnumerable<(string Surface, string Types)> entries)
    {
        foreach (var (surface, types) in entries)
        {
            if (!string.IsNullOrEmpty(surface) && !_raw.ContainsKey(surface))
            {
                _raw[surface] = types;
                _rawOrder.Add(surface);
            }
        }
        foreach (var surface in _rawOrder)
        {
            var types = _raw[surface];
            var key = Py.Split(Fold.Apply(surface));
            if (key.Count == 0) continue;
            if (key.Count == 1 && (key[0].Length <= 3 || Miner.FunctionWords.Contains(key[0])))
            {
                // short or function-like words match with their accents only: "pará" is a state, "para" is not
                _exact.TryAdd(Py.Strip(surface.ToLowerInvariant()), Placeholder(types));
                continue;
            }
            var joined = string.Join(" ", key);
            if (!_keys.ContainsKey(joined))
            {
                _keys[joined] = Placeholder(types);
                _max = Math.Max(_max, key.Count);
            }
        }
    }

    /// <summary>Read a UTF-8 file with one <c>surface&lt;TAB&gt;types</c> per line; lines starting with # are comments.</summary>
    public static Gazetteer Load(string path)
    {
        var entries = new List<(string, string)>();
        // decode the bytes as Python does (a BOM is kept as U+FEFF, not stripped)
        var text = new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));
        foreach (var line in Py.SplitLines(text))
        {
            if (Py.Strip(line).Length == 0 || line.StartsWith('#')) continue;
            var tab = line.IndexOf('\t');
            var surface = tab < 0 ? line : line.Substring(0, tab);
            var types = tab < 0 ? "" : line.Substring(tab + 1);
            entries.Add((Py.Strip(surface).ToLowerInvariant(), Py.Strip(types)));
        }
        return new Gazetteer(entries);
    }

    /// <summary>Number of distinct surfaces.</summary>
    public int Count => _rawOrder.Count;

    /// <summary>The raw surface to types map (insertion order is not exposed; lookups only).</summary>
    internal IReadOnlyDictionary<string, string> Raw => _raw;

    /// <summary>Split a keyword into (word, false) and (placeholder, true), longest entity first at each position.</summary>
    public IReadOnlyList<(string Token, bool IsEntity)> Replace(string keyword)
    {
        var words = Py.Split(keyword);
        var folded = words.Select(w => Py.Strip(Fold.Apply(w), Edge)).ToList();
        var out_ = new List<(string, bool)>();
        int i = 0;
        while (i < words.Count)
        {
            bool found = false;
            for (int size = Math.Min(_max, words.Count - i); size > 0; size--)
            {
                if (_keys.TryGetValue(string.Join(" ", folded.GetRange(i, size)), out var hit))
                {
                    out_.Add((hit, true));
                    i += size;
                    found = true;
                    break;
                }
            }
            if (found) continue;
            if (_exact.TryGetValue(Py.Strip(words[i].ToLowerInvariant(), Edge), out var exact))
            {
                out_.Add((exact, true));
                i++;
                continue;
            }
            out_.Add((words[i], false));
            i++;
        }
        return out_;
    }

    /// <summary>"Local &gt; Cidade" to "[local&gt;cidade]": each type folded, inner spaces as hyphens.</summary>
    public static string Placeholder(string types)
    {
        var parts = types.Split('>').Select(p => string.Join("-", Py.Split(Fold.Apply(p)))).Where(p => p.Length > 0);
        return "[" + string.Join(">", parts) + "]";
    }
}
