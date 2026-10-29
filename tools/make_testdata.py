"""Write testdata/golden.json: the values both implementations must reproduce exactly.

    python tools/make_testdata.py

Run it after any rule change (rules go first into spec/ALGORITHMS.md, then into both languages).
"""

from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "python" / "src"))
from search_pattern_miner import Gazetteer, Search, aggregate, legacy_tokenize, mine, ntile, stem, tokenize  # noqa: E402

EXTRA = [
    "previsão do tempo sp", "detran pe", "pé de moleque", "bolo para festa", "como se faz pudim",
    "smart tv 32 polegadas", "saint-germain", "mp3 2018 download", "google.com entrar", "(500) dias",
    "rio de janeiro hotel barato", "hotel em são paulo ou rio de janeiro", "apartamentos para alugar em curitiba",
    "melhor pizza de belém do pará", "frete para pará", "resultado da mega-sena acumulada", "papel de parede para celular",
    "a. b, c!", "", "   ", "x" * 151, "salvador salvador salvador", "Previsão do Tempo SALVADOR",
    "receita de bolo de cenoura com cobertura de chocolate", "loteria federal resultado de hoje", "natal em natal",
]
WORDS = ["previsão", "como", "fazer", "para", "receita", "resultado", "tempo", "bolo", "cenoura", "hotéis", "baratos",
         "vídeo", "imóveis", "brasileirão", "geração", "oração", "proprietário", "loterias", "polegadas", "lugares",
         "tabela", "consulta", "baixar", "celular", "saint-germain", "mp3", "2018", "tv", "sp", "a.", "google.com",
         "(500)", "´www", "arqueologias", "bibliotecária", "corrigiria", "averiguei", "conhecimento", "amável", "à",
         "crianças", "felizmente", "universidades", "nações", "x", "", "mãe", "pães", "água", "atenção", "!", "eu."]


def main() -> None:
    gaz = Gazetteer.load(ROOT / "data" / "gazetteer-pt.tsv")
    with open(ROOT / "examples" / "keywords.csv", encoding="utf-8") as f:
        rows = list(csv.DictReader(f))
    keywords = [r["keyword"] for r in rows] + EXTRA
    cases = []
    for k in keywords:
        for mode in ("fixed", "legacy2019"):
            for exclude in (True, False):
                cases.append({"keyword": k, "mode": mode, "exclude": exclude, "patterns": mine(k, gaz, None, mode, exclude)})
    searches = [Search(r["keyword"], int(r["volume"]), float(r["competition"]), float(r["cpc"]),
                       "A" if i % 3 else "B") for i, r in enumerate(rows)]
    agg = [p.as_dict() for p in aggregate(searches, gaz)]
    golden = {
        "stem": [{"word": w, "stem": stem(w)} for w in WORDS],
        "tokenize": [{"text": k, "fixed": tokenize(k), "legacy2019": legacy_tokenize(k)} for k in keywords],
        "gazetteer_replace": [{"keyword": k, "segments": [[t, e] for t, e in gaz.replace(k)]} for k in keywords],
        "mine": cases,
        "ntile": [{"n": n, "bands": ntile(n)} for n in (0, 1, 5, 19, 20, 21, 41, 99)],
        "aggregate": agg,
    }
    out = ROOT / "testdata" / "golden.json"
    out.write_text(json.dumps(golden, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    print(f"{len(cases)} mine cases, {len(agg)} aggregated patterns -> {out}")


if __name__ == "__main__":
    main()
