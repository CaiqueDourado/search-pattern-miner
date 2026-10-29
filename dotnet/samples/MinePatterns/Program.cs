// Mines the searches of a CSV (columns keyword,volume,competition,cpc; only keyword is required) with a gazetteer
// and prints the 10 entity_prefix (Barbosa) patterns with the most volume.
//   dotnet run --project samples/MinePatterns -- ../examples/keywords.csv ../data/gazetteer-pt.tsv
using System.Globalization;
using System.Text;
using SearchPatternMiner;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: MinePatterns <keywords.csv> <gazetteer.tsv>");
    return 1;
}

var rows = ReadCsv(args[0]);
var header = rows[0];
string Col(string[] row, string name)
{
    var i = Array.IndexOf(header, name);
    return i >= 0 && i < row.Length ? row[i] : "";
}
static double? Num(string v) =>
    string.IsNullOrWhiteSpace(v) ? null : double.Parse(v.Replace(',', '.'), CultureInfo.InvariantCulture);

var searches = rows.Skip(1)
    .Where(r => Col(r, "keyword").Trim().Length > 0)
    .Select(r => new Search(Col(r, "keyword"), Num(Col(r, "volume")) is { } v ? (long)v : null,
        Num(Col(r, "competition")), Num(Col(r, "cpc"))))
    .ToList();
var gazetteer = Gazetteer.Load(args[1]);

var top = Aggregator.Aggregate(searches, gazetteer, new[] { "entity_prefix" }).Take(10).ToList();
Console.WriteLine($"{"pattern",-32} {"searches",8} {"volume",10}  top search");
foreach (var p in top)
{
    Console.WriteLine($"{p.Pattern,-32} {p.NumSearches,8} {p.VolumeTotal,10}  {p.TopSearch}");
}
return 0;

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
