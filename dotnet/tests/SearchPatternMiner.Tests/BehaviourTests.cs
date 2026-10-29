namespace SearchPatternMiner.Tests;

/// <summary>What the algorithms are for, in a few readable cases (same as python/tests/test_behaviour.py).</summary>
public class BehaviourTests
{
    private static Gazetteer Gaz => GoldenTests.Gaz;

    [Fact]
    public void StemmerMatchesThePublishedPatterns()
    {
        Assert.Equal(new[] { "previsa", "com", "faz", "par", "receit" },
            new[] { "previsão", "como", "fazer", "para", "receita" }.Select(Stemmer.Stem));
        Assert.Null(Stemmer.Stem("sp")); // 1-2 letter words disappear, as in the original
    }

    [Fact]
    public void PrefixesAndExclusionChain()
    {
        var r = Miner.Mine("como fazer bolo de cenoura");
        Assert.Equal(new[] { "com faz", "com faz bol", "com faz bol cenour" }, r["prefix"]);
        Assert.DoesNotContain("com faz", r["contiguous"]); // what Viana produced is not repeated by Vilela
        Assert.Contains("faz bol", r["contiguous"]);
    }

    [Fact]
    public void EntitiesBecomeTypes()
    {
        Assert.Equal(new[] { "com faz [comida]" }, Miner.Mine("como fazer panqueca", Gaz, new[] { "barbosa" })["entity_prefix"]);
        Assert.Equal(new[] { "result [loterias]" }, Miner.Mine("resultado da quina", Gaz, new[] { "entity_prefix" })["entity_prefix"]);
    }

    [Fact]
    public void OnePatternPerType()
    {
        Assert.Equal(new[] { "fret par [local]", "fret par [estado]" },
            Miner.Mine("frete para pará", Gaz, new[] { "entity_prefix" })["entity_prefix"]);
    }

    [Fact]
    public void FixedModeMatchesWholeWordsOnly()
    {
        // in 2019 "pr" (Paraná) matched inside "previsão"; whole words only now
        var gaz = new Gazetteer(new[] { ("pr", "Local > Estado") });
        Assert.Equal("previsa temp curitib [local]", Miner.Mine("previsao do tempo curitiba pr", gaz, new[] { "barbosa" })["entity_prefix"][0]);
        var legacy = Miner.Mine("previsao do tempo curitiba pr", gaz, new[] { "barbosa" }, mode: "legacy2019")["entity_prefix"];
        Assert.Contains(legacy, p => p.StartsWith("[local]", StringComparison.Ordinal));
    }

    [Fact]
    public void FunctionWordsAreNotEntities()
    {
        Assert.Empty(Miner.Mine("bolo para festa", Gaz, new[] { "barbosa" })["entity_prefix"]);
        Assert.Equal(new[] { "bol", "par", "fest" }, Miner.Tokenize("bolo para festa"));
    }

    [Fact]
    public void Aliases()
    {
        Assert.Equal("entity_skipgram", Miner.AlgorithmName("Rosa"));
    }

    [Fact]
    public void AggregateRanksPatterns()
    {
        var searches = new[]
        {
            new Search("como fazer pudim", 1000, 0.2, 0.5), new Search("como fazer bolo de cenoura", 500, 0.4, 0.3),
            new Search("previsão do tempo salvador", 800, 0.1, 0.2),
        };
        var top = Aggregator.Aggregate(searches, Gaz, new[] { "barbosa" });
        Assert.Equal("com faz [comida]", top[0].Pattern);
        Assert.Equal(2, top[0].NumSearches);
        Assert.Equal(1500, top[0].VolumeTotal);
        Assert.Equal("como fazer pudim", top[0].TopSearch);
        Assert.Equal(1, top[0].BandVolume);
    }

    [Fact]
    public void ShowDetranPe()
    {
        // the Python CLI test: "detran pe" -> "[orgao] [estado]" among the entity patterns
        var r = Miner.Mine("detran pe", Gaz);
        Assert.Contains(r.Values.SelectMany(v => v), p => p.Contains("[orgao] [estado]", StringComparison.Ordinal));
    }
}
