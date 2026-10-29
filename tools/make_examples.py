"""Write examples/keywords.csv: SYNTHETIC searches with invented metrics, for the README, the tests and the CLI.

    python tools/make_examples.py

The searches follow common Brazilian query shapes; volume, competition and CPC are random (fixed seed) and mean
nothing. No metric comes from Ubersuggest, DataForSEO, SEMrush or any other tool.
"""

from __future__ import annotations

import csv
import random
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

TEMPLATES = {
    "como fazer {}": ["bolo de cenoura", "pudim", "brigadeiro", "panqueca", "lasanha", "pão de queijo", "tapioca",
                      "feijoada", "coxinha", "mousse de maracujá"],
    "receita de {}": ["bolo de chocolate", "canjica", "escondidinho", "torta de limão", "strogonoff", "cuscuz"],
    "previsão do tempo {}": ["salvador", "curitiba", "recife", "belo horizonte", "porto alegre", "manaus", "natal"],
    "apartamento para alugar em {}": ["salvador", "curitiba", "goiânia", "fortaleza", "campinas"],
    "hotel barato em {}": ["salvador", "recife", "florianópolis", "maceió", "são paulo"],
    "resultado da {}": ["mega sena", "quina", "lotofácil", "lotomania", "timemania"],
    "consulta {}": ["cpf", "cnpj", "ipva", "fgts", "pis"],
    "{} entrar": ["facebook", "gmail", "hotmail", "instagram", "outlook"],
    "salário de {}": ["enfermeiro", "advogado", "professor", "dentista", "eletricista", "motorista"],
    "detran {}": ["sp", "rj", "mg", "ba", "pe", "pr"],
}


def main() -> None:
    rnd = random.Random(2019)
    rows = []
    for tpl, fills in TEMPLATES.items():
        for f in fills:
            rows.append({"keyword": tpl.format(f), "volume": rnd.choice([170, 320, 590, 880, 1300, 2400, 4400, 8100,
                                                                        12100, 22200, 49500]),
                         "competition": round(rnd.random(), 2), "cpc": round(rnd.uniform(0.05, 3.5), 2)})
    out = ROOT / "examples" / "keywords.csv"
    with open(out, "w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["keyword", "volume", "competition", "cpc"])
        w.writeheader()
        w.writerows(rows)
    print(f"{len(rows)} synthetic searches -> {out}")


if __name__ == "__main__":
    main()
