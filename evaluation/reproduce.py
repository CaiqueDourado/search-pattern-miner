"""Does legacy2019 mode reproduce the patterns published in google-keywords-dataset?

    python evaluation/reproduce.py

Reads evaluation/published_sample.csv: rows of the published tables (CC BY 4.0, github.com/CaiqueDourado/
google-keywords-dataset) with a pattern and one search it groups (`busca_lider`, or `exemplo1`/`exemplo2` for the
prefix table). A row passes when mining that search produces that pattern. Only the algorithms that need no entity
dictionary are checked here: the 2019 dictionary is not published (it holds ad titles and brands that asked to be
removed). With it, Barbosa and Rosa also reproduce 400 of 400 published rows (spec/ALGORITHMS.md, docs).
"""

from __future__ import annotations

import csv
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "python" / "src"))
from search_pattern_miner import mine  # noqa: E402


def main() -> None:
    rows = list(csv.DictReader(open(ROOT / "evaluation" / "published_sample.csv", encoding="utf-8")))
    ok: Counter = Counter()
    total: Counter = Counter()
    for r in rows:
        algo = r["algorithm"]
        # the published contiguous table was built before the exclusion of prefixes existed (22/01/2019)
        got = mine(r["search"], None, [algo], mode="legacy2019", exclude=algo != "contiguous")[algo]
        total[r["table"]] += 1
        ok[r["table"]] += r["pattern"] in got
    for t in total:
        print(f"{t}: {ok[t]}/{total[t]} published patterns reproduced")
    (ROOT / "evaluation" / "results.md").write_text(
        "| Published table | Rows checked | Reproduced |\n|---|---:|---:|\n"
        + "".join(f"| {t} | {total[t]} | {ok[t]} |\n" for t in total), encoding="utf-8")


if __name__ == "__main__":
    main()
