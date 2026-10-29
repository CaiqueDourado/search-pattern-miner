using System.Text;
using System.Text.RegularExpressions;

namespace SearchPatternMiner;

/// <summary>
/// The seven pattern algorithms, in two modes. <c>"fixed"</c> (default) is the original method with its bugs fixed;
/// <c>"legacy2019"</c> reproduces what the author's C# code produced in 2019, bugs included. See spec/ALGORITHMS.md.
/// </summary>
public static class Miner
{
    /// <summary>Technical names, in output order.</summary>
    public static readonly IReadOnlyList<string> Algorithms = new[]
    {
        "prefix", "contiguous", "skipgram", "bag", "entity_prefix", "entity_contiguous", "entity_skipgram",
    };

    /// <summary>Surname alias of each algorithm (Viana, Vilela, Oliveira, Dourado, Barbosa, Figueredo, Rosa).</summary>
    public static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["prefix"] = "viana", ["contiguous"] = "vilela", ["skipgram"] = "oliveira", ["bag"] = "dourado",
        ["entity_prefix"] = "barbosa", ["entity_contiguous"] = "figueredo", ["entity_skipgram"] = "rosa",
    };

    /// <summary>A search longer than this produces no pattern.</summary>
    public const int MaxChars = 150;

    private static readonly Dictionary<string, string> ByAlias = Aliases.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.Ordinal);
    private static readonly HashSet<string> StopFolded = new(Stemmer.Stopwords.Select(Fold.Apply), StringComparer.Ordinal);
    private static readonly HashSet<string> StopExact = new(Stemmer.Stopwords, StringComparer.Ordinal);
    internal static readonly HashSet<string> FunctionWords = new(
        StopFolded.Concat(new[] { "para", "mais", "quando", "como", "muito", "qual", "pelo", "pelos" }), StringComparer.Ordinal);
    private static readonly Regex Bracket = new(@"\[.+?\]", RegexOptions.CultureInvariant);
    private const string Edge = "\"'.,;:!?()";
    private static readonly string[] EntityAlgorithms = { "entity_prefix", "entity_contiguous", "entity_skipgram" };

    /// <summary>Technical name for a technical name or a surname alias (viana, vilela, oliveira...).</summary>
    public static string AlgorithmName(string name)
    {
        var n = Py.Strip(name).ToLowerInvariant();
        if (Aliases.ContainsKey(n)) return n;
        if (ByAlias.TryGetValue(n, out var t)) return t;
        throw new ArgumentException($"unknown algorithm: '{name}'; use one of {string.Join(", ", Algorithms)} or their aliases", nameof(name));
    }

    // ------------------------------------------------------------------------------------------ fixed mode

    /// <summary>Fixed-mode tokens: lower case, no accents, edge punctuation removed, stopwords out, stemmed, 1-2 letter words out.</summary>
    public static IReadOnlyList<string> Tokenize(string text) => TokenizeList(text);

    private static List<string> TokenizeList(string text)
    {
        var out_ = new List<string>();
        foreach (var word in Py.Split(text))
        {
            var w = Py.Strip(Fold.Apply(word), Edge);
            if (w.Length == 0 || StopFolded.Contains(w)) continue;
            var s = Stemmer.Stem(w);
            if (!string.IsNullOrEmpty(s)) out_.Add(s);
        }
        return out_;
    }

    private static List<string> Unique(IEnumerable<string> items, ISet<string>? exclude = null)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var out_ = new List<string>();
        foreach (var p in items)
        {
            if (!seen.Contains(p) && (exclude is null || !exclude.Contains(p)))
            {
                seen.Add(p);
                out_.Add(p);
            }
        }
        return out_;
    }

    /// <summary><c>itertools.combinations(range(n), k)</c>, in the same order.</summary>
    private static IEnumerable<int[]> Combinations(int n, int k)
    {
        if (k > n) yield break;
        var idx = Enumerable.Range(0, k).ToArray();
        while (true)
        {
            yield return (int[])idx.Clone();
            int i = k - 1;
            while (i >= 0 && idx[i] == i + n - k) i--;
            if (i < 0) yield break;
            idx[i]++;
            for (int j = i + 1; j < k; j++) idx[j] = idx[j - 1] + 1;
        }
    }

    /// <summary><c>itertools.product(range(n), repeat=k)</c>, in the same order.</summary>
    private static IEnumerable<int[]> Product(int n, int k)
    {
        if (n == 0) yield break;
        var idx = new int[k];
        while (true)
        {
            yield return (int[])idx.Clone();
            int i = k - 1;
            while (i >= 0 && idx[i] == n - 1) idx[i--] = 0;
            if (i < 0) yield break;
            idx[i]++;
        }
    }

    private static string Join(IReadOnlyList<string> t, int start, int count)
    {
        var sb = new StringBuilder();
        for (int i = start; i < start + count; i++)
        {
            if (i > start) sb.Append(' ');
            sb.Append(t[i]);
        }
        return sb.ToString();
    }

    private static List<string> Prefixes(List<string> t)
    {
        var items = new List<string>();
        for (int k = 2; k <= Math.Min(t.Count, 5); k++) items.Add(Join(t, 0, k));
        return Unique(items);
    }

    private static List<string> Contiguous(List<string> t, ISet<string>? exclude = null)
    {
        int n = t.Count;
        var grams = new List<string>();
        for (int size = 2; size < n; size++)
        {
            for (int s = 0; s < n - size + 1; s++) grams.Add(Join(t, s, size));
        }
        if (n >= 2) grams.Add(string.Join(" ", t));
        return Unique(grams, exclude);
    }

    private static List<string> Skipgrams(List<string> t, ISet<string>? exclude = null)
    {
        var items = new List<string>();
        foreach (var k in new[] { 2, 3, 4 })
        {
            foreach (var c in Combinations(t.Count, k)) items.Add(string.Join(" ", c.Select(i => t[i])));
        }
        return Unique(items, exclude);
    }

    private static List<string> Bags(List<string> t, ISet<string>? exclude = null)
    {
        var items = new List<string>();
        foreach (var k in new[] { 2, 3, 4 })
        {
            foreach (var c in Combinations(t.Count, k))
            {
                var words = c.Select(i => t[i]).ToList();
                if (new HashSet<string>(words, StringComparer.Ordinal).Count == k)
                {
                    items.Add(string.Join(" ", words.OrderBy(w => w, StringComparer.Ordinal)));
                }
            }
        }
        return Unique(items, exclude);
    }

    private static HashSet<string> Set(params IEnumerable<string>[] lists)
    {
        var s = new HashSet<string>(StringComparer.Ordinal);
        foreach (var l in lists) s.UnionWith(l);
        return s;
    }

    private static Dictionary<string, List<string>> Fixed(string keyword, Gazetteer? gaz, HashSet<string> wanted, bool exclude)
    {
        var res = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (keyword.Length > MaxChars) return res;
        var t = TokenizeList(keyword);
        var pre = Prefixes(t);
        res["prefix"] = pre;
        var con = Contiguous(t, exclude ? Set(pre) : null);
        res["contiguous"] = con;
        var sk = Skipgrams(t, exclude ? Set(pre, con) : null);
        res["skipgram"] = sk;
        res["bag"] = Bags(t, exclude ? Set(pre, con, sk) : null);
        if (EntityAlgorithms.Any(wanted.Contains))
        {
            var seq = new List<string>();
            IEnumerable<(string Token, bool IsEntity)> segments = gaz is not null
                ? gaz.Replace(keyword)
                : Py.Split(keyword).Select(w => (w, false));
            foreach (var (token, isEntity) in segments)
            {
                if (isEntity) seq.Add(token);
                else seq.AddRange(TokenizeList(token));
            }
            var epRaw = Prefixes(seq);
            var ep = Entity(epRaw);
            var ec = Entity(Contiguous(seq, exclude ? Set(epRaw) : null));
            var es = Entity(Skipgrams(seq));
            if (exclude)
            {
                var epSet = Set(ep);
                ec = ec.Where(p => !epSet.Contains(p)).ToList();
                var both = Set(ep, ec);
                es = es.Where(p => !both.Contains(p)).ToList();
            }
            res["entity_prefix"] = ep;
            res["entity_contiguous"] = ec;
            res["entity_skipgram"] = es;
        }
        return res;
    }

    private static List<string> Entity(List<string> patterns)
    {
        var out_ = new List<string>();
        foreach (var p in patterns)
        {
            if (p.Contains(']')) out_.AddRange(Collapse(Expand(new List<string> { p })));
        }
        return Unique(out_);
    }

    // ------------------------------------------------------------------------------------------ shared

    private static List<string> Expand(List<string> patterns)
    {
        while (true)
        {
            var out_ = new List<string>();
            bool changed = false;
            foreach (var p in patterns)
            {
                bool split = false;
                foreach (Match m in Bracket.Matches(p))
                {
                    var e = m.Value;
                    var parts = e.Split('>');
                    if (parts.Length > 1)
                    {
                        foreach (var part in parts)
                        {
                            out_.Add(Py.Replace(p, e, "[" + Py.Strip(Py.Replace(Py.Replace(part, "[", ""), "]", "")) + "]"));
                        }
                        changed = true;
                        split = true;
                        break;
                    }
                }
                if (!split) out_.Add(p);
            }
            patterns = out_;
            if (!changed) return patterns;
        }
    }

    private static List<string> Collapse(List<string> patterns)
    {
        var out_ = new List<string>();
        foreach (var p in patterns)
        {
            var t = p;
            foreach (Match m in Bracket.Matches(p))
            {
                var e = m.Value;
                if (Py.Strip(Py.Replace(t, e, "")).Length > 0 && t.Contains(e + " " + e, StringComparison.Ordinal))
                {
                    t = Py.Replace(t, e + " " + e, e);
                }
            }
            out_.Add(t);
        }
        return out_;
    }

    // ------------------------------------------------------------------------------------------ legacy 2019

    /// <summary>
    /// The 2019 tokenizer: no hyphen split, accents kept for the stopword check, digits and punctuation kept, and a
    /// dropped word still leaves its separator (so "bolo de cenoura" gives "bol  cenour").
    /// </summary>
    public static string LegacyTokenize(string text)
    {
        var sb = new StringBuilder();
        foreach (var word in text.ToLowerInvariant().Split(' '))
        {
            if (word.Length > 0 && !StopExact.Contains(word))
            {
                var s = Stemmer.Stem(word);
                sb.Append(string.IsNullOrEmpty(s) ? " " : s + " ");
            }
            else sb.Append(' ');
        }
        return Py.Strip(sb.ToString());
    }

    private static string LTok(string text) => Py.Replace(Py.Strip(LegacyTokenize(text)), "  ", " ");

    private static List<string> NonEmptyWords(string s) => s.Split(' ').Where(w => w.Length > 0).ToList();

    /// <summary>A list with O(1) membership, preserving order and duplicates rules of the original code.</summary>
    private sealed class OrderedList
    {
        public readonly List<string> Items = new();
        private readonly HashSet<string> _set = new(StringComparer.Ordinal);
        public bool Contains(string s) => _set.Contains(s);
        public void Add(string s)
        {
            Items.Add(s);
            _set.Add(s);
        }
    }

    private static List<string> LViana(string text)
    {
        if (text.Length > MaxChars) return new List<string>();
        var words = NonEmptyWords(LTok(text));
        var out_ = new OrderedList();
        var p = "";
        for (int x = 0; x < Math.Min(words.Count, 5); x++)
        {
            if (x > 0) p += " ";
            p += Py.Strip(words[x]);
            if (x > 0 && !out_.Contains(p)) out_.Add(p);
        }
        return out_.Items;
    }

    private static List<string> LVilela(string text, List<string>? tv = null)
    {
        if (text.Length > MaxChars) return new List<string>();
        tv ??= LViana(text);
        var tvs = Set(tv);
        var tk = LTok(text);
        var words = NonEmptyWords(tk);
        var out_ = new OrderedList();
        for (int size = 2; size < words.Count; size++)
        {
            for (int start = 0; start < words.Count - size + 1; start++)
            {
                var p = Join(words, start, size);
                if (!out_.Contains(p) && !tvs.Contains(p)) out_.Add(p);
            }
        }
        if (tk.Contains(' ') && !out_.Contains(tk) && !tvs.Contains(tk)) out_.Add(tk);
        return out_.Items;
    }

    private static List<string> LOliveira(string text, List<string>? tvl = null, List<string>? tv = null)
    {
        if (text.Length > MaxChars) return new List<string>();
        tvl ??= LVilela(text);
        tv ??= LViana(text);
        var excl = Set(tvl, tv);
        var w = NonEmptyWords(LTok(text));
        var out_ = new OrderedList();
        foreach (var k in new[] { 2, 3, 4 })
        {
            foreach (var c in Combinations(w.Count, k))
            {
                var p = string.Join(" ", c.Select(i => w[i]));
                if (!out_.Contains(p) && !excl.Contains(p)) out_.Add(p);
            }
        }
        return out_.Items;
    }

    private static List<string> LDourado(string text, List<string>? tvl = null, List<string>? tol = null, List<string>? tv = null)
    {
        if (text.Length > MaxChars) return new List<string>();
        tvl ??= LVilela(text);
        tol ??= LOliveira(text);
        tv ??= LViana(text);
        var excl = Set(tvl, tol, tv);
        var parts = LTok(text).Split(' ');
        var out_ = new OrderedList();
        foreach (var k in new[] { 2, 3, 4 })
        {
            foreach (var tup in Product(parts.Length, k))
            {
                var items = tup.Select(i => parts[i]).ToArray();
                if (new HashSet<string>(items, StringComparer.Ordinal).Count != k) continue;
                var words = string.Join(" ", items).Split(' ').OrderBy(x => x, StringComparer.Ordinal);
                var p = Py.Strip(string.Join(" ", words));
                if (!out_.Contains(p) && !excl.Contains(p)) out_.Add(p);
            }
        }
        return out_.Items;
    }

    private static List<string> LNgrams(string text)
    {
        var words = NonEmptyWords(text);
        var out_ = new OrderedList();
        for (int size = 1; size < words.Count; size++)
        {
            for (int start = 0; start < words.Count - size + 1; start++)
            {
                var p = Join(words, start, size);
                if (!out_.Contains(p)) out_.Add(p);
            }
        }
        if (!out_.Contains(text)) out_.Add(text);
        return out_.Items;
    }

    private static string LNormalize(IReadOnlyDictionary<string, string> raw, string text)
    {
        for (int round = 0; round < 1000; round++)
        {
            var keys = LNgrams(text).OrderByDescending(k => k.Length); // stable, like Python's sorted
            bool hit = false;
            foreach (var key in keys)
            {
                if (key.Length > 0 && raw.TryGetValue(key, out var types))
                {
                    var flat = Py.Replace(Py.Replace(Py.Strip(types), " > ", ">"), " ", "-");
                    var ent = string.Join("¨", flat.Select(c => c.ToString()));
                    var t = text;
                    if (t.StartsWith(key + " ", StringComparison.Ordinal)) t = Py.Replace(t, key + " ", "[" + ent + "] ");
                    if (t.EndsWith(" " + key, StringComparison.Ordinal)) t = Py.Replace(t, " " + key, " [" + ent + "]");
                    if (t.Contains(" " + key + " ", StringComparison.Ordinal)) t = Py.Replace(t, " " + key + " ", " [" + ent + "] ");
                    else if (t.Contains(key, StringComparison.Ordinal)) t = Py.Replace(t, key, " [" + ent + "] ");
                    text = t;
                    hit = true;
                    break;
                }
            }
            if (!hit) return text;
        }
        throw new InvalidOperationException("entity replacement did not converge");
    }

    private static string LStripTrema(string text)
    {
        foreach (var o in Bracket.Matches(text).Select(m => m.Value).ToList())
        {
            text = Py.Replace(text, o, Py.Replace(o, "¨", ""));
        }
        return text;
    }

    private static (List<string> Patterns, string Text) LEntity(IReadOnlyDictionary<string, string> raw, string text, Func<string, List<string>> generator)
    {
        text = LStripTrema(LNormalize(raw, Py.CollapseSpaces(text).ToLowerInvariant()));
        var out_ = new List<string>();
        foreach (var p in generator(text))
        {
            if (p.Contains(']')) out_.AddRange(Collapse(Expand(new List<string> { p })));
        }
        return (out_, text);
    }

    private static Dictionary<string, List<string>> Legacy(string keyword, Gazetteer? gaz, HashSet<string> wanted, bool exclude)
    {
        IReadOnlyDictionary<string, string> raw = gaz?.Raw ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var res = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var tv = LViana(keyword);
        res["prefix"] = tv;
        var tvl = LVilela(keyword, exclude ? null : new List<string>());
        res["contiguous"] = tvl;
        if (wanted.Contains("skipgram") || wanted.Contains("bag"))
        {
            var tol = exclude ? LOliveira(keyword, tvl, tv) : LOliveira(keyword, new(), new());
            res["skipgram"] = tol;
            if (wanted.Contains("bag"))
            {
                res["bag"] = exclude ? LDourado(keyword, tvl, tol, tv) : LDourado(keyword, new(), new(), new());
            }
        }

        List<string> Barbosa(string text) => LEntity(raw, text, s => LViana(s)).Patterns;

        List<string> Figueredo(string text)
        {
            var (out_, t) = LEntity(raw, text, s => LVilela(s, exclude ? null : new List<string>()));
            if (!exclude) return out_;
            var b = Set(Barbosa(t)); // the original runs Barbosa again on the already-normalized text
            return out_.Where(x => !b.Contains(x)).ToList();
        }

        if (EntityAlgorithms.Any(wanted.Contains))
        {
            res["entity_prefix"] = Barbosa(keyword);
            res["entity_contiguous"] = Figueredo(keyword);
            var t0 = LStripTrema(Py.CollapseSpaces(keyword).ToLowerInvariant());
            var rosa = LEntity(raw, t0, s => LOliveira(s, new(), new())).Patterns;
            if (exclude)
            {
                var bf = Set(Barbosa(t0), Figueredo(t0));
                rosa = rosa.Where(x => !bf.Contains(x)).ToList();
            }
            res["entity_skipgram"] = rosa;
        }
        return res;
    }

    // ------------------------------------------------------------------------------------------ public API

    /// <summary>Patterns of one search, per algorithm, keyed in <see cref="Algorithms"/> order.</summary>
    /// <param name="keyword">The search.</param>
    /// <param name="gazetteer">Entities for the three entity algorithms (none: no entity patterns).</param>
    /// <param name="algorithms">Technical names or aliases (default: all seven).</param>
    /// <param name="mode"><c>"fixed"</c> or <c>"legacy2019"</c>.</param>
    /// <param name="exclude">Apply the exclusion chain (each algorithm drops what an earlier one already produced).</param>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Mine(string keyword, Gazetteer? gazetteer = null,
        IEnumerable<string>? algorithms = null, string mode = "fixed", bool exclude = true)
    {
        var list = algorithms?.ToList();
        var wanted = new HashSet<string>((list is { Count: > 0 } ? list : Algorithms).Select(AlgorithmName), StringComparer.Ordinal);
        var res = mode switch
        {
            "fixed" => Fixed(keyword, gazetteer, wanted, exclude),
            "legacy2019" => Legacy(keyword, gazetteer, wanted, exclude),
            _ => throw new ArgumentException("mode must be 'fixed' or 'legacy2019'", nameof(mode)),
        };
        var ordered = new OrderedDictionary<IReadOnlyList<string>>();
        foreach (var a in Algorithms)
        {
            if (wanted.Contains(a)) ordered.Add(a, res.TryGetValue(a, out var v) ? v : new List<string>());
        }
        return ordered;
    }

    /// <summary>Read-only dictionary that enumerates in insertion order.</summary>
    private sealed class OrderedDictionary<TValue> : IReadOnlyDictionary<string, TValue>
    {
        private readonly List<KeyValuePair<string, TValue>> _items = new();
        private readonly Dictionary<string, TValue> _map = new(StringComparer.Ordinal);

        public void Add(string key, TValue value)
        {
            _map.Add(key, value);
            _items.Add(new(key, value));
        }

        public TValue this[string key] => _map[key];
        public IEnumerable<string> Keys => _items.Select(kv => kv.Key);
        public IEnumerable<TValue> Values => _items.Select(kv => kv.Value);
        public int Count => _items.Count;
        public bool ContainsKey(string key) => _map.ContainsKey(key);
        public bool TryGetValue(string key, out TValue value) => _map.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<string, TValue>> GetEnumerator() => _items.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
