"""Golden values shared with the .NET implementation (testdata/golden.json)."""

from __future__ import annotations

import csv
import json
from pathlib import Path

import pytest

from search_pattern_miner import Gazetteer, Search, aggregate, legacy_tokenize, mine, ntile, stem, tokenize

ROOT = Path(__file__).resolve().parents[2]
GOLDEN = json.loads((ROOT / "testdata" / "golden.json").read_text(encoding="utf-8"))
GAZ = Gazetteer.load(ROOT / "data" / "gazetteer-pt.tsv")


@pytest.mark.parametrize("case", GOLDEN["stem"], ids=lambda c: c["word"] or "<empty>")
def test_stem(case):
    assert stem(case["word"]) == case["stem"]


@pytest.mark.parametrize("case", GOLDEN["tokenize"], ids=lambda c: c["text"][:40])
def test_tokenize(case):
    assert tokenize(case["text"]) == case["fixed"]
    assert legacy_tokenize(case["text"]) == case["legacy2019"]


@pytest.mark.parametrize("case", GOLDEN["gazetteer_replace"], ids=lambda c: c["keyword"][:40])
def test_gazetteer(case):
    assert [[t, e] for t, e in GAZ.replace(case["keyword"])] == case["segments"]


@pytest.mark.parametrize("case", GOLDEN["mine"], ids=lambda c: f'{c["mode"]}-{c["exclude"]}-{c["keyword"][:30]}')
def test_mine(case):
    assert mine(case["keyword"], GAZ, None, case["mode"], case["exclude"]) == case["patterns"]


def test_ntile():
    for case in GOLDEN["ntile"]:
        assert ntile(case["n"]) == case["bands"]


def test_aggregate():
    with open(ROOT / "examples" / "keywords.csv", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    searches = [Search(r["keyword"], int(r["volume"]), float(r["competition"]), float(r["cpc"]), "A" if i % 3 else "B")
                for i, r in enumerate(rows)]
    got = [p.as_dict() for p in aggregate(searches, GAZ)]
    assert len(got) == len(GOLDEN["aggregate"])
    for g, e in zip(got, GOLDEN["aggregate"]):
        for k, v in e.items():
            if isinstance(v, float):
                assert g[k] == pytest.approx(v, abs=1e-9), k
            else:
                assert g[k] == v, k
