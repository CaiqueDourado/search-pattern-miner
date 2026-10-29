"""Portuguese stemmer and stopwords used by the original pattern algorithms.

The stemmer is a port of Lucene's BrazilianStemmer (Apache License 2.0, see THIRD-PARTY-NOTICES.md), as the
author's C# code used it in 2017-2019, with Lucene's quirks kept on purpose (spec/ALGORITHMS.md, section 1.3):
words of 1-2 characters return None, tokens with non-letters come back unchanged, the `logias` rule does not
change the word and an `ira` ending removes `ava` (a no-op). Changing them would change every published pattern.
"""

from __future__ import annotations

# NLTK / Snowball Portuguese stopwords (BSD), minus 8 words the author kept on purpose because they carry meaning
# in searches: para, mais, quando, como, muito, qual, pelo, pelos.
STOPWORDS: tuple[str, ...] = tuple("""
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
""".split())

# Lucene's changeTerm: only these accents are folded (à è ì ò ù are not).
_CHANGE = str.maketrans({"á": "a", "â": "a", "ã": "a", "é": "e", "ê": "e", "í": "i", "ó": "o", "ô": "o", "õ": "o",
                         "ú": "u", "ü": "u", "ç": "c", "ñ": "n"})

_S1_6 = (("imentos", None), ("amentos", None), ("adores", None), ("adoras", None))
_S1_5 = (("acoes", None), ("imento", None), ("amento", None), ("adora", None), ("ismos", None), ("istas", None),
         ("logia", "log"), ("ucion", "u"), ("encia", "ente"), ("mente", None), ("idade", None))
_S1_4 = ("acao", "ezas", "icos", "icas", "ismo", "avel", "ivel", "ista", "osos", "osas", "ador", "ivas", "ivos")
_S1_3 = ("eza", "ico", "ica", "oso", "osa", "iva", "ivo")
_S2 = (
    (7, ("issemos", "essemos", "assemos", "ariamos", "eriamos", "iriamos")),
    (6, ("iremos", "eremos", "aremos", "avamos", "iramos", "eramos", "aramos", "asseis", "esseis", "isseis",
         "arieis", "erieis", "irieis")),
    (5, ("irmos", "iamos", "armos", "ermos", "areis", "ereis", "ireis", "asses", "esses", "isses", "astes", "assem",
         "essem", "issem", "ardes", "erdes", "irdes", "ariam", "eriam", "iriam", "arias", "erias", "irias", "estes",
         "istes", "areis", "aveis")),
    (4, ("aria", "eria", "iria", "asse", "esse", "isse", "aste", "este", "iste", "arei", "erei", "irei", "aram", "eram",
         "iram", "avam", "arem", "erem", "irem", "ando", "endo", "indo", "arao", "erao", "irao", "adas", "idas", "aras",
         "eras", "iras", "avas", "ares", "eres", "ires", "ados", "idos", "amos", "emos", "imos", "iras", "ieis")),
    (3, ("ada", "ida", "ara", "era", "ira*", "iam", "ado", "ido", "ias", "ais", "eis", "ira", "ear")),
    (2, ("ia", "ei", "am", "em", "ar", "er", "ir", "as", "es", "is", "eu", "iu", "iu", "ou")),
)
_VOWELS = "aeiou"


def _ends(v: str | None, s: str) -> bool:
    return v is not None and len(s) <= len(v) and v.endswith(s)


def _remove(v: str, s: str) -> str:
    return v[: len(v) - len(s)] if _ends(v, s) else v


def _replace(v: str, s: str, r: str) -> str:
    w = _remove(v, s)
    return v if w == v else w + r


def _ends_preceded(v: str | None, s: str, p: str) -> bool:
    return v is not None and _ends(v, s) and _ends(_remove(v, s), p)


def _r1(v: str | None) -> str | None:
    if v is None:
        return None
    i, j = len(v) - 1, 0
    while j < i and v[j] not in _VOWELS:
        j += 1
    if not j < i:
        return None
    while j < i and v[j] in _VOWELS:
        j += 1
    if not j < i:
        return None
    return v[j + 1:]


def _rv(v: str) -> str | None:
    i = len(v) - 1
    if i > 0 and v[1] not in _VOWELS:
        j = 2
        while j < i and v[j] not in _VOWELS:
            j += 1
        if j < i:
            return v[j + 1:]
    if i > 1 and v[0] in _VOWELS and v[1] in _VOWELS:
        j = 2
        while j < i and v[j] in _VOWELS:
            j += 1
        if j < i:
            return v[j + 1:]
    if i > 2:
        return v[3:]
    return None


def _prepare(term: str) -> str:
    ct = term.lower().translate(_CHANGE)
    if len(ct) < 2:
        return ct
    if ct[0] in "\"'-,;.?!":
        ct = ct[1:]
    if len(ct) < 2:
        return ct
    if ct[-1] in "-;?'\"":
        ct = ct[:-1]
    for c in ".,!":
        if ct.endswith(c):
            ct = ct.replace(c, " " + c)
            break
    return ct


def stem(term: str) -> str | None:
    """Stem one Portuguese word like the original code. None means the word is dropped (1-2 characters)."""
    ct = _prepare(term)
    if not 2 < len(ct) < 300:
        return None
    if not all(c.isalpha() for c in ct):
        return ct
    r1 = _r1(ct)
    r2 = _r1(r1)
    rv = _rv(ct)

    altered, ct = _step1(ct, r1, r2, rv)
    if not altered:
        altered, ct = _step2(ct, rv)
    if altered:
        if rv is not None and _ends_preceded(rv, "i", "c"):
            ct = _remove(ct, "i")
    elif rv is not None:
        for s in ("os", "a", "i", "o"):
            if _ends(rv, s):
                ct = _remove(ct, s)
                break
    if rv is not None and _ends(rv, "e"):
        if _ends_preceded(rv, "e", "gu"):
            ct = _remove(_remove(ct, "e"), "u")
        elif _ends_preceded(rv, "e", "ci"):
            ct = _remove(_remove(ct, "e"), "i")
        else:
            ct = _remove(ct, "e")
    return ct


def _step1(ct: str, r1: str | None, r2: str | None, rv: str | None) -> tuple[bool, str]:
    if _ends(ct, "uciones") and _ends(r2, "uciones"):
        return True, _replace(ct, "uciones", "u")
    if len(ct) >= 6:
        for s, _ in _S1_6:
            if _ends(ct, s) and _ends(r2, s):
                return True, _remove(ct, s)
        if _ends(ct, "logias") and _ends(r2, "logias"):
            return True, ct  # Lucene quirk: the replacement is computed but never assigned
        if _ends(ct, "encias") and _ends(r2, "encias"):
            return True, _replace(ct, "encias", "ente")
        if _ends(ct, "amente") and _ends(r1, "amente"):
            return True, _remove(ct, "amente")
        if _ends(ct, "idades") and _ends(r2, "idades"):
            return True, _remove(ct, "idades")
    if len(ct) >= 5:
        for s, r in _S1_5:
            if _ends(ct, s) and _ends(r2, s):
                return True, (_remove(ct, s) if r is None else _replace(ct, s, r))
    if len(ct) >= 4:
        for s in _S1_4:
            if _ends(ct, s) and _ends(r2, s):
                return True, _remove(ct, s)
        if _ends(ct, "iras") and _ends(rv, "iras") and _ends_preceded(ct, "iras", "e"):
            return True, _replace(ct, "iras", "ir")
    if len(ct) >= 3:
        for s in _S1_3:
            if _ends(ct, s) and _ends(r2, s):
                return True, _remove(ct, s)
        if _ends(ct, "ira") and _ends(rv, "ira") and _ends_preceded(ct, "ira", "e"):
            return True, _replace(ct, "ira", "ir")
    return False, ct


def _step2(ct: str, rv: str | None) -> tuple[bool, str]:
    if rv is None:
        return False, ct
    for size, suffixes in _S2:
        if len(rv) >= size:
            for s in suffixes:
                if s == "ira*":  # Lucene quirk: an "ira" ending in RV removes "ava" (a no-op) and stops
                    if _ends(rv, "ira"):
                        return True, _remove(ct, "ava")
                    continue
                if _ends(rv, s):
                    return True, _remove(ct, s)
    return False, ct
