using System.Globalization;
using System.Text;
using System.Text.Json;

namespace SearchPatternMiner.Tests;

/// <summary>testdata/golden.json comes from the Python package; every value must match exactly.</summary>
public class GoldenTests
{
    internal static readonly string RepoRoot = FindRoot();
    private static readonly JsonElement Root = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(RepoRoot, "testdata", "golden.json"))).RootElement;
    internal static readonly Gazetteer Gaz = Gazetteer.Load(Path.Combine(RepoRoot, "data", "gazetteer-pt.tsv"));

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "testdata", "golden.json"))) return d.FullName;
        }
        throw new FileNotFoundException("testdata/golden.json not found above " + AppContext.BaseDirectory);
    }

    private static IEnumerable<object[]> Indexes(string section) =>
        Enumerable.Range(0, Root.GetProperty(section).GetArrayLength()).Select(i => new object[] { i });

    public static IEnumerable<object[]> StemCases() => Indexes("stem");
    public static IEnumerable<object[]> TokenizeCases() => Indexes("tokenize");
    public static IEnumerable<object[]> ReplaceCases() => Indexes("gazetteer_replace");
    public static IEnumerable<object[]> MineCases() => Indexes("mine");

    private static string? Str(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetString();
    private static string[] Strs(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).ToArray();

    [Theory]
    [MemberData(nameof(StemCases))]
    public void Stem(int index)
    {
        var c = Root.GetProperty("stem")[index];
        Assert.Equal(Str(c.GetProperty("stem")), Stemmer.Stem(c.GetProperty("word").GetString()!));
    }

    [Theory]
    [MemberData(nameof(TokenizeCases))]
    public void Tokenize(int index)
    {
        var c = Root.GetProperty("tokenize")[index];
        var text = c.GetProperty("text").GetString()!;
        Assert.Equal(Strs(c.GetProperty("fixed")), Miner.Tokenize(text));
        Assert.Equal(c.GetProperty("legacy2019").GetString(), Miner.LegacyTokenize(text));
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void GazetteerReplace(int index)
    {
        var c = Root.GetProperty("gazetteer_replace")[index];
        var expected = c.GetProperty("segments").EnumerateArray().Select(s => (s[0].GetString()!, s[1].GetBoolean())).ToArray();
        Assert.Equal(expected, Gaz.Replace(c.GetProperty("keyword").GetString()!).ToArray());
    }

    [Theory]
    [MemberData(nameof(MineCases))]
    public void Mine(int index)
    {
        var c = Root.GetProperty("mine")[index];
        var got = Miner.Mine(c.GetProperty("keyword").GetString()!, Gaz, null, c.GetProperty("mode").GetString()!,
            c.GetProperty("exclude").GetBoolean());
        var expected = c.GetProperty("patterns");
        Assert.Equal(expected.EnumerateObject().Select(p => p.Name).ToArray(), got.Keys.ToArray());
        foreach (var p in expected.EnumerateObject())
        {
            Assert.Equal(Strs(p.Value), got[p.Name]);
        }
    }

    [Fact]
    public void Ntile()
    {
        foreach (var c in Root.GetProperty("ntile").EnumerateArray())
        {
            var bands = c.GetProperty("bands").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            Assert.Equal(bands, Aggregator.Ntile(c.GetProperty("n").GetInt32()));
        }
    }

    [Fact]
    public void Aggregate()
    {
        var rows = ReadCsv(Path.Combine(RepoRoot, "examples", "keywords.csv"));
        var header = rows[0];
        int Col(string name) => Array.IndexOf(header, name);
        var searches = rows.Skip(1).Select((r, i) => new Search(
            r[Col("keyword")],
            long.Parse(r[Col("volume")], CultureInfo.InvariantCulture),
            double.Parse(r[Col("competition")], CultureInfo.InvariantCulture),
            double.Parse(r[Col("cpc")], CultureInfo.InvariantCulture),
            i % 3 != 0 ? "A" : "B")).ToList();

        var got = Aggregator.Aggregate(searches, Gaz);
        var expected = Root.GetProperty("aggregate").EnumerateArray().ToList();
        Assert.Equal(expected.Count, got.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            var g = got[i];
            var e = expected[i];
            Assert.Equal(e.GetProperty("group").GetString(), g.Group);
            Assert.Equal(e.GetProperty("algorithm").GetString(), g.Algorithm);
            Assert.Equal(e.GetProperty("alias").GetString(), g.Alias);
            Assert.Equal(e.GetProperty("pattern").GetString(), g.Pattern);
            Assert.Equal(e.GetProperty("num_words").GetInt32(), g.NumWords);
            Assert.Equal(e.GetProperty("num_searches").GetInt32(), g.NumSearches);
            Assert.Equal(e.GetProperty("volume_total").GetInt64(), g.VolumeTotal);
            AssertClose(e.GetProperty("competition_avg"), g.CompetitionAvg);
            AssertClose(e.GetProperty("cpc_avg"), g.CpcAvg);
            Assert.Equal(e.GetProperty("top_search").GetString(), g.TopSearch);
            Assert.Equal(Strs(e.GetProperty("examples")), g.Examples);
            Assert.Equal(e.GetProperty("band_volume").GetInt32(), g.BandVolume);
            Assert.Equal(e.GetProperty("band_searches").GetInt32(), g.BandSearches);
            Assert.Equal(NullableInt(e.GetProperty("band_competition")), g.BandCompetition);
            Assert.Equal(NullableInt(e.GetProperty("band_cpc")), g.BandCpc);
        }
    }

    private static void AssertClose(JsonElement expected, double? actual)
    {
        if (expected.ValueKind == JsonValueKind.Null)
        {
            Assert.Null(actual);
            return;
        }
        Assert.NotNull(actual);
        Assert.Equal(expected.GetDouble(), actual!.Value, 1e-9);
    }

    private static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

    internal static List<string[]> ReadCsv(string path)
    {
        // Minimal RFC 4180 reader: quoted fields, doubled quotes, commas and line breaks inside quotes.
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var text = File.ReadAllText(path, Encoding.UTF8);
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString()); field.Clear();
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row.ToArray());
                row.Clear();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
        return rows;
    }
}
