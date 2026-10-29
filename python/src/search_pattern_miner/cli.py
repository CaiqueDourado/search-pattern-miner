"""Command line.

    search-pattern-miner show "como fazer bolo de cenoura" --gazetteer data/gazetteer-pt.tsv
    search-pattern-miner mine examples/keywords.csv --gazetteer data/gazetteer-pt.tsv --out patterns.csv
"""

from __future__ import annotations

import argparse
import csv
import json
import sys
from pathlib import Path

from .aggregate import Search, aggregate, mine_pairs
from .miner import ALGORITHMS, ALIASES, Gazetteer, mine

FIELDS = ["group", "algorithm", "alias", "pattern", "num_words", "num_searches", "volume_total", "competition_avg",
          "cpc_avg", "top_search", "examples", "band_volume", "band_searches", "band_competition", "band_cpc"]


def _num(v: str | None, kind=float):
    if v is None or v.strip() == "":
        return None
    return kind(float(v.replace(",", "."))) if kind is int else kind(v.replace(",", "."))


def _read(path: Path, a) -> list[Search]:
    with open(path, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.DictReader(f))
    return [Search(r[a.keyword], _num(r.get(a.volume), int) if a.volume else None,
                   _num(r.get(a.competition)) if a.competition else None, _num(r.get(a.cpc)) if a.cpc else None,
                   r.get(a.group, "") if a.group else "") for r in rows if r.get(a.keyword, "").strip()]


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(prog="search-pattern-miner", description=__doc__.splitlines()[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("show", "mine"):
        p = sub.add_parser(name)
        p.add_argument("--gazetteer", type=Path, help="TSV: surface<TAB>types")
        p.add_argument("--algorithms", default=",".join(ALGORITHMS), help="comma separated names or aliases")
        p.add_argument("--mode", choices=["fixed", "legacy2019"], default="fixed")
        p.add_argument("--no-exclude", action="store_true", help="do not apply the exclusion chain")
    show = sub.choices["show"]
    show.add_argument("keyword")
    m = sub.choices["mine"]
    m.add_argument("csv", type=Path)
    m.add_argument("--keyword", default="keyword")
    m.add_argument("--volume", default="volume")
    m.add_argument("--competition", default="competition")
    m.add_argument("--cpc", default="cpc")
    m.add_argument("--group", default="", help="column to rank patterns within, e.g. market")
    m.add_argument("--out", type=Path, help="patterns CSV (default: stdout)")
    m.add_argument("--pairs", type=Path, help="also write keyword,algorithm,pattern pairs")
    a = ap.parse_args(argv)
    gaz = Gazetteer.load(a.gazetteer) if a.gazetteer else None
    algos = [x for x in a.algorithms.split(",") if x.strip()]
    if a.cmd == "show":
        res = mine(a.keyword, gaz, algos, a.mode, not a.no_exclude)
        print(json.dumps({f"{k} ({ALIASES[k]})": v for k, v in res.items()}, ensure_ascii=False, indent=2))
        return 0
    searches = _read(a.csv, a)
    if a.pairs:
        with open(a.pairs, "w", encoding="utf-8", newline="") as f:
            w = csv.writer(f)
            w.writerow(["group", "keyword", "algorithm", "pattern"])
            for s, algorithm, p in mine_pairs(searches, gaz, algos, a.mode, not a.no_exclude):
                w.writerow([s.group, s.keyword, algorithm, p])
    patterns = aggregate(searches, gaz, algos, a.mode, not a.no_exclude)
    out = open(a.out, "w", encoding="utf-8", newline="") if a.out else sys.stdout
    w = csv.DictWriter(out, fieldnames=FIELDS)
    w.writeheader()
    for p in patterns:
        d = p.as_dict()
        d["examples"] = " | ".join(d["examples"])
        w.writerow({k: d[k] for k in FIELDS})
    if a.out:
        out.close()
        print(f"{len(patterns)} patterns from {len(searches)} searches -> {a.out}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
