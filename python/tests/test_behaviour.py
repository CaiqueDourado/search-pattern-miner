"""What the algorithms are for, in a few readable cases."""

from __future__ import annotations

from pathlib import Path

from search_pattern_miner import Gazetteer, aggregate, Search, algorithm_name, mine, stem, tokenize
from search_pattern_miner.cli import main

ROOT = Path(__file__).resolve().parents[2]
GAZ = Gazetteer.load(ROOT / "data" / "gazetteer-pt.tsv")


def test_stemmer_matches_the_published_patterns():
    assert [stem(w) for w in ("previsão", "como", "fazer", "para", "receita")] == ["previsa", "com", "faz", "par", "receit"]
    assert stem("sp") is None  # 1-2 letter words disappear, as in the original


def test_prefixes_and_exclusion_chain():
    r = mine("como fazer bolo de cenoura")
    assert r["prefix"] == ["com faz", "com faz bol", "com faz bol cenour"]
    assert "com faz" not in r["contiguous"]  # what Viana produced is not repeated by Vilela
    assert "faz bol" in r["contiguous"]


def test_entities_become_types():
    assert mine("como fazer panqueca", GAZ, ["barbosa"])["entity_prefix"] == ["com faz [comida]"]
    assert mine("resultado da quina", GAZ, ["entity_prefix"])["entity_prefix"] == ["result [loterias]"]


def test_one_pattern_per_type():
    assert mine("frete para pará", GAZ, ["entity_prefix"])["entity_prefix"] == ["fret par [local]", "fret par [estado]"]


def test_fixed_mode_matches_whole_words_only():
    # in 2019 "pr" (Paraná) matched inside "previsão"; whole words only now
    gaz = Gazetteer([("pr", "Local > Estado")])
    assert mine("previsao do tempo curitiba pr", gaz, ["barbosa"])["entity_prefix"][0] == "previsa temp curitib [local]"
    legacy = mine("previsao do tempo curitiba pr", gaz, ["barbosa"], mode="legacy2019")["entity_prefix"]
    assert any(p.startswith("[local]") for p in legacy)


def test_function_words_are_not_entities():
    assert mine("bolo para festa", GAZ, ["barbosa"])["entity_prefix"] == []
    assert tokenize("bolo para festa") == ["bol", "par", "fest"]


def test_aliases():
    assert algorithm_name("Rosa") == "entity_skipgram"


def test_aggregate_ranks_patterns():
    searches = [Search("como fazer pudim", 1000, 0.2, 0.5), Search("como fazer bolo de cenoura", 500, 0.4, 0.3),
                Search("previsão do tempo salvador", 800, 0.1, 0.2)]
    top = aggregate(searches, GAZ, ["barbosa"])
    assert top[0].pattern == "com faz [comida]" and top[0].num_searches == 2 and top[0].volume_total == 1500
    assert top[0].top_search == "como fazer pudim" and top[0].band_volume == 1


def test_cli(tmp_path, capsys):
    out = tmp_path / "p.csv"
    assert main(["mine", str(ROOT / "examples" / "keywords.csv"), "--gazetteer", str(ROOT / "data" / "gazetteer-pt.tsv"),
                 "--algorithms", "barbosa", "--out", str(out)]) == 0
    assert "com faz [comida]" in out.read_text(encoding="utf-8")
    assert main(["show", "detran pe", "--gazetteer", str(ROOT / "data" / "gazetteer-pt.tsv")]) == 0
    assert "[orgao] [estado]" in capsys.readouterr().out
