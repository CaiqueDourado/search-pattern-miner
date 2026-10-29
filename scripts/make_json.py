"""Write docs/example-pattern.json: one aggregated pattern with the searches it groups.

    python scripts/make_json.py

Runs the library on examples/keywords.csv (SYNTHETIC searches and metrics) with data/gazetteer-pt.tsv and keeps the
entity_prefix (Barbosa) pattern "com faz [comida]".
"""

from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "python" / "src"))
from search_pattern_miner import Gazetteer, Search, aggregate, mine  # noqa: E402

PATTERN, ALGORITHM = "com faz [comida]", "entity_prefix"


def main() -> None:
    gaz = Gazetteer.load(ROOT / "data" / "gazetteer-pt.tsv")
    with open(ROOT / "examples" / "keywords.csv", encoding="utf-8") as f:
        searches = [Search(r["keyword"], int(r["volume"]), float(r["competition"]), float(r["cpc"]))
                    for r in csv.DictReader(f)]
    p = next(x for x in aggregate(searches, gaz, [ALGORITHM]) if x.pattern == PATTERN)
    members = sorted((s for s in searches if PATTERN in mine(s.keyword, gaz, [ALGORITHM])[ALGORITHM]),
                     key=lambda s: (-(s.volume or 0), s.keyword))
    d = p.as_dict()
    rec = {"pattern": d["pattern"], "algorithm": d["algorithm"], "alias": d["alias"], "num_words": d["num_words"],
           "totals": {k: (round(d[k], 4) if isinstance(d[k], float) else d[k])
                      for k in ("num_searches", "volume_total", "competition_avg", "cpc_avg")},
           "bands": {k.replace("band_", ""): d[k] for k in ("band_volume", "band_searches", "band_competition", "band_cpc")},
           "top_search": d["top_search"],
           "searches": [{"keyword": s.keyword, "replaced": " ".join(t for t, _ in gaz.replace(s.keyword)),
                         "volume": s.volume, "competition": s.competition, "cpc": s.cpc} for s in members],
           "note": "synthetic searches and metrics (examples/keywords.csv)"}
    out = ROOT / "docs" / "example-pattern.json"
    out.write_text(json.dumps(rec, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(members)} searches -> {out}")


if __name__ == "__main__":
    main()
