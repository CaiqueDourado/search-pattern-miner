namespace SearchPatternMiner;

/// <summary>
/// Portuguese stemmer and stopwords used by the original pattern algorithms: a port of Lucene's BrazilianStemmer
/// (Apache License 2.0) as the author's C# code used it in 2017-2019, with Lucene's quirks kept on purpose
/// (spec/ALGORITHMS.md, section 1): words of 1-2 characters return null, tokens with non-letters come back unchanged,
/// the <c>logias</c> rule does not change the word and an <c>ira</c> ending removes <c>ava</c> (a no-op).
/// </summary>
public static class Stemmer
{
    /// <summary>NLTK / Snowball Portuguese stopwords (BSD), minus para, mais, quando, como, muito, qual, pelo, pelos.</summary>
    public static readonly IReadOnlyList<string> Stopwords = Py.Split("""
        de a o que e do da em um é com não uma os no se na por as dos mas foi ao ele das tem à seu sua ou ser há nos já está
        eu também só pela até isso ela entre era depois sem mesmo aos ter seus quem nas me esse eles estão você tinha foram
        essa num nem suas meu às minha têm numa elas havia seja será nós tenho lhe deles essas esses pelas este fosse dele tu
        te vocês vos lhes meus minhas teu tua teus tuas nosso nossa nossos nossas dela delas esta estes estas aquele aquela
        aqueles aquelas isto aquilo estou estamos estive esteve estivemos estiveram estava estávamos estavam estivera
        estivéramos esteja estejamos estejam estivesse estivéssemos estivessem estiver estivermos estiverem hei havemos hão
        houve houvemos houveram houvera houvéramos haja hajamos hajam houvesse houvéssemos houvessem houver houvermos
        houverem houverei houverá houveremos houverão houveria houveríamos houveriam sou somos são éramos eram fui fomos fora
        fôramos sejamos sejam fôssemos fossem for formos forem serei seremos serão seria seríamos seriam temos tém tínhamos
        tinham tive teve tivemos tiveram tivera tivéramos tenha tenhamos tenham tivesse tivéssemos tivessem tiver tivermos
        tiverem terei terá teremos terão teria teríamos teriam
        """).AsReadOnly();

    // Lucene's changeTerm: only these accents are folded (à è ì ò ù are not).
    private static readonly Dictionary<char, char> Change = new()
    {
        ['á'] = 'a', ['â'] = 'a', ['ã'] = 'a', ['é'] = 'e', ['ê'] = 'e', ['í'] = 'i', ['ó'] = 'o', ['ô'] = 'o', ['õ'] = 'o',
        ['ú'] = 'u', ['ü'] = 'u', ['ç'] = 'c', ['ñ'] = 'n',
    };

    private static readonly string[] S1_6 = { "imentos", "amentos", "adores", "adoras" };
    private static readonly (string Suffix, string? Replacement)[] S1_5 =
    {
        ("acoes", null), ("imento", null), ("amento", null), ("adora", null), ("ismos", null), ("istas", null),
        ("logia", "log"), ("ucion", "u"), ("encia", "ente"), ("mente", null), ("idade", null),
    };
    private static readonly string[] S1_4 = { "acao", "ezas", "icos", "icas", "ismo", "avel", "ivel", "ista", "osos", "osas", "ador", "ivas", "ivos" };
    private static readonly string[] S1_3 = { "eza", "ico", "ica", "oso", "osa", "iva", "ivo" };
    private static readonly (int Size, string[] Suffixes)[] S2 =
    {
        (7, new[] { "issemos", "essemos", "assemos", "ariamos", "eriamos", "iriamos" }),
        (6, new[] { "iremos", "eremos", "aremos", "avamos", "iramos", "eramos", "aramos", "asseis", "esseis", "isseis",
            "arieis", "erieis", "irieis" }),
        (5, new[] { "irmos", "iamos", "armos", "ermos", "areis", "ereis", "ireis", "asses", "esses", "isses", "astes", "assem",
            "essem", "issem", "ardes", "erdes", "irdes", "ariam", "eriam", "iriam", "arias", "erias", "irias", "estes",
            "istes", "areis", "aveis" }),
        (4, new[] { "aria", "eria", "iria", "asse", "esse", "isse", "aste", "este", "iste", "arei", "erei", "irei", "aram", "eram",
            "iram", "avam", "arem", "erem", "irem", "ando", "endo", "indo", "arao", "erao", "irao", "adas", "idas", "aras",
            "eras", "iras", "avas", "ares", "eres", "ires", "ados", "idos", "amos", "emos", "imos", "iras", "ieis" }),
        (3, new[] { "ada", "ida", "ara", "era", "ira*", "iam", "ado", "ido", "ias", "ais", "eis", "ira", "ear" }),
        (2, new[] { "ia", "ei", "am", "em", "ar", "er", "ir", "as", "es", "is", "eu", "iu", "iu", "ou" }),
    };
    private const string Vowels = "aeiou";

    /// <summary>Stem one Portuguese word like the original code. Null means the word is dropped (1-2 characters).</summary>
    public static string? Stem(string term)
    {
        var ct = Prepare(term);
        if (!(2 < ct.Length && ct.Length < 300)) return null;
        foreach (var c in ct)
        {
            if (!char.IsLetter(c)) return ct;
        }
        var r1 = R1(ct);
        var r2 = R1(r1);
        var rv = Rv(ct);

        var (altered, s) = Step1(ct, r1, r2, rv);
        ct = s;
        if (!altered) (altered, ct) = Step2(ct, rv);
        if (altered)
        {
            if (rv is not null && EndsPreceded(rv, "i", "c")) ct = Remove(ct, "i");
        }
        else if (rv is not null)
        {
            foreach (var suf in new[] { "os", "a", "i", "o" })
            {
                if (Ends(rv, suf))
                {
                    ct = Remove(ct, suf);
                    break;
                }
            }
        }
        if (rv is not null && Ends(rv, "e"))
        {
            if (EndsPreceded(rv, "e", "gu")) ct = Remove(Remove(ct, "e"), "u");
            else if (EndsPreceded(rv, "e", "ci")) ct = Remove(Remove(ct, "e"), "i");
            else ct = Remove(ct, "e");
        }
        return ct;
    }

    private static bool Ends(string? v, string s) => v is not null && s.Length <= v.Length && v.EndsWith(s, StringComparison.Ordinal);

    private static string Remove(string v, string s) => Ends(v, s) ? v.Substring(0, v.Length - s.Length) : v;

    private static string Replace(string v, string s, string r)
    {
        var w = Remove(v, s);
        return w == v ? v : w + r;
    }

    private static bool EndsPreceded(string? v, string s, string p) => v is not null && Ends(v, s) && Ends(Remove(v, s), p);

    private static bool IsVowel(char c) => Vowels.IndexOf(c) >= 0;

    private static string? R1(string? v)
    {
        if (v is null) return null;
        int i = v.Length - 1, j = 0;
        while (j < i && !IsVowel(v[j])) j++;
        if (!(j < i)) return null;
        while (j < i && IsVowel(v[j])) j++;
        if (!(j < i)) return null;
        return v.Substring(j + 1);
    }

    private static string? Rv(string v)
    {
        int i = v.Length - 1;
        if (i > 0 && !IsVowel(v[1]))
        {
            int j = 2;
            while (j < i && !IsVowel(v[j])) j++;
            if (j < i) return v.Substring(j + 1);
        }
        if (i > 1 && IsVowel(v[0]) && IsVowel(v[1]))
        {
            int j = 2;
            while (j < i && IsVowel(v[j])) j++;
            if (j < i) return v.Substring(j + 1);
        }
        if (i > 2) return v.Substring(3);
        return null;
    }

    private static string Prepare(string term)
    {
        var ct = Fold.Translate(term.ToLowerInvariant(), Change);
        if (ct.Length < 2) return ct;
        if ("\"'-,;.?!".IndexOf(ct[0]) >= 0) ct = ct.Substring(1);
        if (ct.Length < 2) return ct;
        if ("-;?'\"".IndexOf(ct[^1]) >= 0) ct = ct.Substring(0, ct.Length - 1);
        foreach (var c in ".,!")
        {
            if (ct.EndsWith(c))
            {
                ct = ct.Replace(c.ToString(), " " + c, StringComparison.Ordinal);
                break;
            }
        }
        return ct;
    }

    private static (bool, string) Step1(string ct, string? r1, string? r2, string? rv)
    {
        if (Ends(ct, "uciones") && Ends(r2, "uciones")) return (true, Replace(ct, "uciones", "u"));
        if (ct.Length >= 6)
        {
            foreach (var s in S1_6)
            {
                if (Ends(ct, s) && Ends(r2, s)) return (true, Remove(ct, s));
            }
            if (Ends(ct, "logias") && Ends(r2, "logias")) return (true, ct); // Lucene quirk: replacement never assigned
            if (Ends(ct, "encias") && Ends(r2, "encias")) return (true, Replace(ct, "encias", "ente"));
            if (Ends(ct, "amente") && Ends(r1, "amente")) return (true, Remove(ct, "amente"));
            if (Ends(ct, "idades") && Ends(r2, "idades")) return (true, Remove(ct, "idades"));
        }
        if (ct.Length >= 5)
        {
            foreach (var (s, r) in S1_5)
            {
                if (Ends(ct, s) && Ends(r2, s)) return (true, r is null ? Remove(ct, s) : Replace(ct, s, r));
            }
        }
        if (ct.Length >= 4)
        {
            foreach (var s in S1_4)
            {
                if (Ends(ct, s) && Ends(r2, s)) return (true, Remove(ct, s));
            }
            if (Ends(ct, "iras") && Ends(rv, "iras") && EndsPreceded(ct, "iras", "e")) return (true, Replace(ct, "iras", "ir"));
        }
        if (ct.Length >= 3)
        {
            foreach (var s in S1_3)
            {
                if (Ends(ct, s) && Ends(r2, s)) return (true, Remove(ct, s));
            }
            if (Ends(ct, "ira") && Ends(rv, "ira") && EndsPreceded(ct, "ira", "e")) return (true, Replace(ct, "ira", "ir"));
        }
        return (false, ct);
    }

    private static (bool, string) Step2(string ct, string? rv)
    {
        if (rv is null) return (false, ct);
        foreach (var (size, suffixes) in S2)
        {
            if (rv.Length < size) continue;
            foreach (var s in suffixes)
            {
                if (s == "ira*") // Lucene quirk: an "ira" ending in RV removes "ava" (a no-op) and stops
                {
                    if (Ends(rv, "ira")) return (true, Remove(ct, "ava"));
                    continue;
                }
                if (Ends(rv, s)) return (true, Remove(ct, s));
            }
        }
        return (false, ct);
    }
}
