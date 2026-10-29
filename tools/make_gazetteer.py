"""Build data/gazetteer-pt.tsv, the small example gazetteer shipped with the library.

    python tools/make_gazetteer.py --municipios <path to municipios-e-estados-do-brasil/data>

Places come from the author's open dataset municipios-e-estados-do-brasil (IBGE, CC BY 4.0): the 27 states, their
two-letter codes and every municipality with 100,000+ people in the 2022 Census. The other types are short lists
written by hand. No brand that asked for removal is listed; no entity comes from the original 2019 dictionary.
"""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "python" / "src"))
from search_pattern_miner.stemmer import STOPWORDS  # noqa: E402

HAND = {
    "Comida": """bolo de cenoura|bolo de chocolate|bolo de fubá|bolo de milho|bolo de laranja|pão de queijo|pão caseiro
        |panqueca|pudim|brigadeiro|lasanha|strogonoff|feijoada|moqueca|tapioca|cuscuz|coxinha|esfiha|pizza|hambúrguer
        |arroz doce|canjica|mousse de maracujá|torta de limão|frango assado|carne de panela|escondidinho|farofa
        |macarrão|nhoque|risoto|omelete|vinagrete|salpicão|maionese|brownie|cookie|sorvete|açaí|quindim|beijinho
        |pé de moleque|paçoca|bolinho de chuva|pastel|empada|quiche|sopa de legumes|caldo verde|baião de dois""",
    "Loterias": "mega sena|mega-sena|quina|lotofácil|lotomania|timemania|dupla sena|dia de sorte|super sete|loteria federal",
    "Imóvel": """apartamento|apartamentos|casa|casas|kitnet|kitnets|terreno|terrenos|sobrado|sobrados|chácara|chácaras
        |sítio|sítios|galpão|galpões|sala comercial|salas comerciais|cobertura|coberturas|flat|flats""",
    "Turismo": "hotel|hotéis|hoteis|pousada|pousadas|resort|resorts|hostel|hostels|camping|passagem aérea|passagens aéreas",
    "Documento": """cpf|cnpj|rg|cnh|título de eleitor|certidão de nascimento|certidão de casamento|passaporte|carteira de trabalho
        |boleto|nota fiscal|ipva|iptu|fgts|pis""",
    "Site": "facebook|instagram|youtube|gmail|hotmail|outlook|whatsapp|netflix|google|twitter|tiktok|linkedin|wikipedia",
    "Órgão": "detran|inss|receita federal|polícia federal|procon|caixa econômica|banco do brasil|correios|enem|sisu",
    "Profissão": """médico|médica|enfermeiro|enfermeira|advogado|advogada|engenheiro|engenheira|professor|professora|dentista
        |psicólogo|psicóloga|nutricionista|eletricista|encanador|pedreiro|motorista|veterinário|contador""",
    "Animal": "cachorro|gato|cavalo|vaca|galinha|peixe|pássaro|papagaio|coelho|tartaruga|cobra|rato|aranha|abelha",
}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--municipios", type=Path, required=True, help="the data/ folder of municipios-e-estados-do-brasil")
    a = ap.parse_args()
    entries: dict[str, str] = {}
    states = list(csv.DictReader(open(a.municipios / "estados.csv", encoding="utf-8")))
    cities = [c for c in csv.DictReader(open(a.municipios / "municipios.csv", encoding="utf-8"))
              if int(c["population_2022"] or 0) >= 100_000]
    state_names = {s["name"].lower() for s in states}
    for c in sorted(cities, key=lambda c: -int(c["population_2022"])):
        name = c["name"].lower()
        entries.setdefault(name, "Local > Cidade > Estado" if name in state_names else "Local > Cidade")
    for s in states:
        entries.setdefault(s["name"].lower(), "Local > Estado")
        entries.setdefault(s["uf"].lower(), "Local > Estado")
    for stop in set(entries) & set(STOPWORDS):  # "se" (Sergipe) is also a stopword: never an entity
        del entries[stop]
    for typ, words in HAND.items():
        for w in words.split("|"):
            w = " ".join(w.split()).lower()
            if w:
                entries.setdefault(w, typ)
    out = ROOT / "data" / "gazetteer-pt.tsv"
    lines = ["# surface<TAB>types (\" > \" separates alternative types; one pattern is made per type)",
             "# places: municipios-e-estados-do-brasil (IBGE, CC BY 4.0); other types written by hand"]
    lines += [f"{k}\t{v}" for k, v in entries.items()]
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(entries)} entries -> {out}")


if __name__ == "__main__":
    main()
