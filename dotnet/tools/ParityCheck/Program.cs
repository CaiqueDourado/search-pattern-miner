// Runs every case of testdata/golden.json against the .NET library without a test framework.
//   dotnet run --project tools/ParityCheck [-- path/to/repo]
using System.Globalization;
using System.Text;
using System.Text.Json;
using SearchPatternMiner;

var root = args.Length > 0 ? args[0] : FindRoot(AppContext.BaseDirectory) ?? FindRoot(Directory.GetCurrentDirectory());
if (root is null)
{
    Console.Error.WriteLine("testdata/golden.json not found; pass the repository root");
    return 2;
}

var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "testdata", "golden.json"))).RootElement;
var gaz = Gazetteer.Load(Path.Combine(root, "data", "gazetteer-pt.tsv"));
int cases = 0, failures = 0, inexact = 0;

void Check(bool ok, string what)
{
    cases++;
    if (!ok)
    {
        failures++;
        if (failures <= 30) Console.WriteLine("FAIL " + what);
    }
}

static string? Str(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetString();
static string[] Strs(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).ToArray();
static string Show(IEnumerable<string> xs) => "[" + string.Join(", ", xs.Select(x => "\"" + x + "\"")) + "]";

foreach (var c in golden.GetProperty("stem").EnumerateArray())
{
    var word = c.GetProperty("word").GetString()!;
    var expected = Str(c.GetProperty("stem"));
    var got = Stemmer.Stem(word);
    Check(got == expected, $"stem({word}) = {got ?? "null"}, expected {expected ?? "null"}");
}

foreach (var c in golden.GetProperty("tokenize").EnumerateArray())
{
    var text = c.GetProperty("text").GetString()!;
    var fixedTokens = Strs(c.GetProperty("fixed"));
    var got = Miner.Tokenize(text);
    Check(got.SequenceEqual(fixedTokens), $"tokenize({text}) = {Show(got)}, expected {Show(fixedTokens)}");
    var legacy = c.GetProperty("legacy2019").GetString();
    var gotLegacy = Miner.LegacyTokenize(text);
    Check(gotLegacy == legacy, $"legacy_tokenize({text}) = '{gotLegacy}', expected '{legacy}'");
}

foreach (var c in golden.GetProperty("gazetteer_replace").EnumerateArray())
{
    var keyword = c.GetProperty("keyword").GetString()!;
    var expected = c.GetProperty("segments").EnumerateArray().Select(s => s[0].GetString() + "|" + s[1].GetBoolean()).ToArray();
    var got = gaz.Replace(keyword).Select(s => s.Token + "|" + s.IsEntity).ToArray();
    Check(got.SequenceEqual(expected), $"replace({keyword}) = {Show(got)}, expected {Show(expected)}");
}

foreach (var c in golden.GetProperty("mine").EnumerateArray())
{
    var keyword = c.GetProperty("keyword").GetString()!;
    var mode = c.GetProperty("mode").GetString()!;
    var exclude = c.GetProperty("exclude").GetBoolean();
    var expected = c.GetProperty("patterns");
    var got = Miner.Mine(keyword, gaz, null, mode, exclude);
    var expectedKeys = expected.EnumerateObject().Select(p => p.Name).ToArray();
    Check(got.Keys.SequenceEqual(expectedKeys), $"mine({keyword}, {mode}, {exclude}) keys {Show(got.Keys)}, expected {Show(expectedKeys)}");
    foreach (var p in expected.EnumerateObject())
    {
        var want = Strs(p.Value);
        var have = got.TryGetValue(p.Name, out var v) ? v : Array.Empty<string>();
        Check(have.SequenceEqual(want), $"mine({keyword}, {mode}, {exclude})[{p.Name}] = {Show(have)}, expected {Show(want)}");
    }
}

foreach (var c in golden.GetProperty("ntile").EnumerateArray())
{
    var n = c.GetProperty("n").GetInt32();
    var want = c.GetProperty("bands").EnumerateArray().Select(x => x.GetInt32()).ToArray();
    var have = Aggregator.Ntile(n);
    Check(have.SequenceEqual(want), $"ntile({n})");
}

var rows = ReadCsv(Path.Combine(root, "examples", "keywords.csv"));
var header = rows[0];
int Col(string name) => Array.IndexOf(header, name);
var searches = rows.Skip(1).Select((r, i) => new Search(
    r[Col("keyword")],
    long.Parse(r[Col("volume")], CultureInfo.InvariantCulture),
    double.Parse(r[Col("competition")], CultureInfo.InvariantCulture),
    double.Parse(r[Col("cpc")], CultureInfo.InvariantCulture),
    i % 3 != 0 ? "A" : "B")).ToList();
var agg = Aggregator.Aggregate(searches, gaz);
var expectedAgg = golden.GetProperty("aggregate").EnumerateArray().ToList();
Check(agg.Count == expectedAgg.Count, $"aggregate count {agg.Count}, expected {expectedAgg.Count}");
for (int i = 0; i < Math.Min(agg.Count, expectedAgg.Count); i++)
{
    var g = agg[i];
    var e = expectedAgg[i];
    var id = $"aggregate[{i}] ({e.GetProperty("group").GetString()}, {e.GetProperty("algorithm").GetString()}, {e.GetProperty("pattern").GetString()})";
    Check(g.Group == e.GetProperty("group").GetString(), id + " group");
    Check(g.Algorithm == e.GetProperty("algorithm").GetString(), id + " algorithm");
    Check(g.Alias == e.GetProperty("alias").GetString(), id + " alias");
    Check(g.Pattern == e.GetProperty("pattern").GetString(), id + $" pattern {g.Pattern}");
    Check(g.NumWords == e.GetProperty("num_words").GetInt32(), id + " num_words");
    Check(g.NumSearches == e.GetProperty("num_searches").GetInt32(), id + " num_searches");
    Check(g.VolumeTotal == e.GetProperty("volume_total").GetInt64(), id + " volume_total");
    Check(Close(g.CompetitionAvg, e.GetProperty("competition_avg")), id + $" competition_avg {g.CompetitionAvg}");
    Check(Close(g.CpcAvg, e.GetProperty("cpc_avg")), id + $" cpc_avg {g.CpcAvg}");
    Check(g.TopSearch == e.GetProperty("top_search").GetString(), id + " top_search");
    Check(g.Examples.SequenceEqual(Strs(e.GetProperty("examples"))), id + " examples");
    Check(g.BandVolume == e.GetProperty("band_volume").GetInt32(), id + " band_volume");
    Check(g.BandSearches == e.GetProperty("band_searches").GetInt32(), id + " band_searches");
    Check(g.BandCompetition == NullableInt(e.GetProperty("band_competition")), id + " band_competition");
    Check(g.BandCpc == NullableInt(e.GetProperty("band_cpc")), id + " band_cpc");
}

Console.WriteLine($"{failures} failures out of {cases} checks ({inexact} averages within 1e-9 but not bit-identical)");
return failures == 0 ? 0 : 1;

bool Close(double? got, JsonElement expected)
{
    if (expected.ValueKind == JsonValueKind.Null) return got is null;
    if (got is null) return false;
    var want = expected.GetDouble();
    if (got.Value != want) inexact++;
    return Math.Abs(got.Value - want) <= 1e-9;
}

static int? NullableInt(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetInt32();

static string? FindRoot(string start)
{
    for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
    {
        if (File.Exists(Path.Combine(d.FullName, "testdata", "golden.json"))) return d.FullName;
    }
    return null;
}

static List<string[]> ReadCsv(string path)
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
