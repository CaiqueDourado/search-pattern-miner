namespace SearchPatternMiner;

/// <summary>One search with its metrics.</summary>
/// <param name="Keyword">The search text.</param>
/// <param name="Volume">Monthly searches (missing counts as 0 in totals).</param>
/// <param name="Competition">Competition index (missing values are left out of the average).</param>
/// <param name="Cpc">Cost per click (missing values are left out of the average).</param>
/// <param name="Group">Patterns are ranked within each group (e.g. a market).</param>
public sealed record Search(string Keyword, long? Volume = null, double? Competition = null, double? Cpc = null, string Group = "");

/// <summary>A pattern with its totals and bands within its (group, algorithm).</summary>
public sealed class PatternStats
{
    /// <summary>Group of the searches.</summary>
    public string Group { get; init; } = "";
    /// <summary>Technical algorithm name.</summary>
    public string Algorithm { get; init; } = "";
    /// <summary>Surname alias of the algorithm.</summary>
    public string Alias => Miner.Aliases[Algorithm];
    /// <summary>The pattern text.</summary>
    public string Pattern { get; init; } = "";
    /// <summary>Words of the pattern.</summary>
    public int NumWords { get; init; }
    /// <summary>Distinct searches with this pattern.</summary>
    public int NumSearches { get; init; }
    /// <summary>Sum of their volumes (missing = 0).</summary>
    public long VolumeTotal { get; init; }
    /// <summary>Mean competition over the searches that have one, or null.</summary>
    public double? CompetitionAvg { get; init; }
    /// <summary>Mean CPC over the searches that have one, or null.</summary>
    public double? CpcAvg { get; init; }
    /// <summary>The search with the most volume (ties: keyword ascending).</summary>
    public string TopSearch { get; init; } = "";
    /// <summary>Up to three searches by volume descending, then keyword.</summary>
    public IReadOnlyList<string> Examples { get; init; } = Array.Empty<string>();
    /// <summary>NTILE(20) by volume_total descending (1 = most volume).</summary>
    public int BandVolume { get; internal set; }
    /// <summary>NTILE(20) by num_searches descending.</summary>
    public int BandSearches { get; internal set; }
    /// <summary>NTILE(20) by competition_avg ascending, or null without a value.</summary>
    public int? BandCompetition { get; internal set; }
    /// <summary>NTILE(20) by cpc_avg ascending, or null without a value.</summary>
    public int? BandCpc { get; internal set; }
}

/// <summary>Group searches by pattern and rank the patterns, as the author's curation screens did.</summary>
public static class Aggregator
{
    /// <summary>(search, algorithm, pattern) for every pattern of every search.</summary>
    public static IEnumerable<(Search Search, string Algorithm, string Pattern)> MinePairs(IEnumerable<Search> searches,
        Gazetteer? gazetteer = null, IEnumerable<string>? algorithms = null, string mode = "fixed", bool exclude = true)
    {
        var algos = algorithms?.ToList();
        foreach (var s in searches)
        {
            foreach (var (algorithm, patterns) in Miner.Mine(s.Keyword, gazetteer, algos, mode, exclude))
            {
                foreach (var p in patterns) yield return (s, algorithm, p);
            }
        }
    }

    /// <summary>SQL NTILE(k) bands for n ordered rows: the first n % k bands get one extra row.</summary>
    public static IReadOnlyList<int> Ntile(int n, int k = 20)
    {
        var bands = new List<int>();
        if (n == 0) return bands;
        int bse = n / k, extra = n % k;
        for (int b = 1; b <= Math.Min(n, k); b++)
        {
            for (int i = 0; i < bse + (b <= extra ? 1 : 0); i++) bands.Add(b);
        }
        return bands;
    }

    /// <summary>Patterns with totals and bands, sorted by group, algorithm and volume (largest first), then pattern.</summary>
    public static IReadOnlyList<PatternStats> Aggregate(IEnumerable<Search> searches, Gazetteer? gazetteer = null,
        IEnumerable<string>? algorithms = null, string mode = "fixed", bool exclude = true)
    {
        var acc = new Dictionary<(string, string, string), (List<Search> Rows, HashSet<string> Seen)>();
        var order = new List<(string, string, string)>();
        foreach (var (s, algorithm, p) in MinePairs(searches, gazetteer, algorithms, mode, exclude))
        {
            var key = (s.Group, algorithm, p);
            if (!acc.TryGetValue(key, out var entry))
            {
                entry = (new List<Search>(), new HashSet<string>(StringComparer.Ordinal));
                acc[key] = entry;
                order.Add(key);
            }
            if (entry.Seen.Add(s.Keyword)) entry.Rows.Add(s);
        }
        var out_ = new List<PatternStats>();
        foreach (var key in order)
        {
            var (group, algorithm, p) = key;
            var rows = acc[key].Rows;
            var comps = rows.Where(r => r.Competition is not null).Select(r => r.Competition!.Value).ToList();
            var cpcs = rows.Where(r => r.Cpc is not null).Select(r => r.Cpc!.Value).ToList();
            var ranked = rows.OrderBy(r => -(r.Volume ?? 0)).ThenBy(r => r.Keyword, StringComparer.Ordinal).ToList();
            out_.Add(new PatternStats
            {
                Group = group, Algorithm = algorithm, Pattern = p, NumWords = Py.Split(p).Count, NumSearches = rows.Count,
                VolumeTotal = rows.Sum(r => r.Volume ?? 0),
                CompetitionAvg = comps.Count > 0 ? PySum(comps) / comps.Count : null,
                CpcAvg = cpcs.Count > 0 ? PySum(cpcs) / cpcs.Count : null,
                TopSearch = ranked[0].Keyword, Examples = ranked.Take(3).Select(r => r.Keyword).ToList(),
            });
        }
        Bands(out_);
        return out_
            .OrderBy(x => x.Group, StringComparer.Ordinal)
            .ThenBy(x => IndexOf(x.Algorithm))
            .ThenBy(x => -x.VolumeTotal)
            .ThenBy(x => x.Pattern, StringComparer.Ordinal)
            .ToList();
    }

    private static int IndexOf(string algorithm)
    {
        for (int i = 0; i < Miner.Algorithms.Count; i++)
        {
            if (Miner.Algorithms[i] == algorithm) return i;
        }
        return -1;
    }

    /// <summary>
    /// Python's built-in <c>sum</c> of floats (3.12+): Neumaier compensated summation in list order, so the averages are
    /// bit-for-bit those of the reference implementation.
    /// </summary>
    private static double PySum(List<double> values)
    {
        double f = 0.0, c = 0.0;
        foreach (var x in values)
        {
            double t = f + x;
            if (Math.Abs(f) >= Math.Abs(x)) c += (f - t) + x;
            else c += (x - t) + f;
            f = t;
        }
        if (c != 0.0 && double.IsFinite(c)) f += c;
        return f;
    }

    private static void Bands(List<PatternStats> patterns)
    {
        var groups = new Dictionary<(string, string), List<PatternStats>>();
        var order = new List<(string, string)>();
        foreach (var p in patterns)
        {
            var key = (p.Group, p.Algorithm);
            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = new List<PatternStats>();
                order.Add(key);
            }
            list.Add(p);
        }
        foreach (var key in order)
        {
            var items = groups[key];
            Assign(items.OrderBy(x => -x.VolumeTotal).ThenBy(x => x.Pattern, StringComparer.Ordinal).ToList(), (p, b) => p.BandVolume = b);
            Assign(items.OrderBy(x => -x.NumSearches).ThenBy(x => x.Pattern, StringComparer.Ordinal).ToList(), (p, b) => p.BandSearches = b);
            Assign(items.Where(x => x.CompetitionAvg is not null).OrderBy(x => x.CompetitionAvg!.Value)
                .ThenBy(x => x.Pattern, StringComparer.Ordinal).ToList(), (p, b) => p.BandCompetition = b);
            Assign(items.Where(x => x.CpcAvg is not null).OrderBy(x => x.CpcAvg!.Value)
                .ThenBy(x => x.Pattern, StringComparer.Ordinal).ToList(), (p, b) => p.BandCpc = b);
        }
    }

    private static void Assign(List<PatternStats> sorted, Action<PatternStats, int> set)
    {
        var bands = Ntile(sorted.Count);
        for (int i = 0; i < sorted.Count; i++) set(sorted[i], bands[i]);
    }
}
