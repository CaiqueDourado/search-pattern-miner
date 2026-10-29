"""The seven pattern algorithms, in two modes.

``mode="fixed"`` (default) is the original method with its bugs fixed: entities match whole words only, the longest
match at each position wins, entity placeholders are atomic tokens, stopwords match without accents and every
algorithm returns each pattern once.

``mode="legacy2019"`` reproduces what the author's C# code produced in 2019, bugs included (string-level
replacement, the "¨" trick, duplicated patterns, the 2019 tokenizer). It is what the published google-keywords-dataset
was built with. See spec/ALGORITHMS.md for every rule.
"""

from __future__ import annotations

import re
from itertools import combinations, product
from pathlib import Path
from typing import Iterable

from .fold import fold
from .stemmer import STOPWORDS, stem

ALGORITHMS = ("prefix", "contiguous", "skipgram", "bag", "entity_prefix", "entity_contiguous", "entity_skipgram")
ALIASES = {"prefix": "viana", "contiguous": "vilela", "skipgram": "oliveira", "bag": "dourado",
           "entity_prefix": "barbosa", "entity_contiguous": "figueredo", "entity_skipgram": "rosa"}
_BY_ALIAS = {v: k for k, v in ALIASES.items()}
MAX_CHARS = 150
_STOP_FOLDED = frozenset(fold(s) for s in STOPWORDS)
_STOP_EXACT = frozenset(STOPWORDS)
# stopwords plus the 8 function words the original kept in patterns
_FUNCTION = _STOP_FOLDED | {"para", "mais", "quando", "como", "muito", "qual", "pelo", "pelos"}
_BRACKET = re.compile(r"\[.+?\]")
_EDGE = "\"'.,;:!?()"


def algorithm_name(name: str) -> str:
    """Technical name for a technical name or a surname alias (viana, vilela, oliveira…)."""
    n = name.strip().lower()
    if n in ALIASES:
        return n
    if n in _BY_ALIAS:
        return _BY_ALIAS[n]
    raise ValueError(f"unknown algorithm: {name!r}; use one of {', '.join(ALGORITHMS)} or their aliases")


# --------------------------------------------------------------------------------------------- gazetteer

class Gazetteer:
    """Entities and their types: surface form -> "Type" or "Type > Other type" (one pattern per type)."""

    def __init__(self, entries: Iterable[tuple[str, str]] = ()):
        self.raw: dict[str, str] = {}
        for surface, types in entries:
            if surface and surface not in self.raw:  # the first entry wins, as in the original loader
                self.raw[surface] = types
        self._keys: dict[tuple[str, ...], str] = {}
        self._exact: dict[str, str] = {}
        self._max = 0
        for surface, types in self.raw.items():
            key = tuple(fold(surface).split())
            if not key:
                continue
            if len(key) == 1 and (len(key[0]) <= 3 or key[0] in _FUNCTION):
                # short or function-like words match with their accents only: "pará" is a state, "para" is not;
                # "pe" is Pernambuco, "pé" is a foot
                self._exact.setdefault(surface.lower().strip(), placeholder(types))
                continue
            if key not in self._keys:
                self._keys[key] = placeholder(types)
                self._max = max(self._max, len(key))

    @classmethod
    def load(cls, path: str | Path) -> "Gazetteer":
        """Read a UTF-8 file with one ``surface<TAB>types`` per line; lines starting with # are comments."""
        entries = []
        for line in Path(path).read_text(encoding="utf-8").splitlines():
            if not line.strip() or line.startswith("#"):
                continue
            surface, _, types = line.partition("\t")
            entries.append((surface.strip().lower(), types.strip()))
        return cls(entries)

    def __len__(self) -> int:
        return len(self.raw)

    def replace(self, keyword: str) -> list[tuple[str, bool]]:
        """Split a keyword into (word, False) and (placeholder, True), longest entity first at each position."""
        words = keyword.split()
        folded = [fold(w).strip(_EDGE) for w in words]
        out: list[tuple[str, bool]] = []
        i = 0
        while i < len(words):
            for size in range(min(self._max, len(words) - i), 0, -1):
                hit = self._keys.get(tuple(folded[i:i + size]))
                if hit is not None:
                    out.append((hit, True))
                    i += size
                    break
            else:
                hit = self._exact.get(words[i].lower().strip(_EDGE))
                if hit is not None:
                    out.append((hit, True))
                    i += 1
                    continue
                out.append((words[i], False))
                i += 1
        return out


def placeholder(types: str) -> str:
    """"Local > Cidade" -> "[local>cidade]": each type folded, inner spaces as hyphens."""
    parts = ["-".join(fold(p).split()) for p in types.split(">")]
    return "[" + ">".join(p for p in parts if p) + "]"


# --------------------------------------------------------------------------------------------- fixed mode

def tokenize(text: str) -> list[str]:
    """Fixed-mode tokens: lower case, no accents, edge punctuation removed, stopwords out, stemmed, 1-2 letter words out."""
    out = []
    for word in text.split():
        w = fold(word).strip(_EDGE)
        if not w or w in _STOP_FOLDED:
            continue
        s = stem(w)
        if s:
            out.append(s)
    return out


def _unique(items: Iterable[str], exclude: set[str] | None = None) -> list[str]:
    seen: set[str] = set()
    out = []
    for p in items:
        if p not in seen and (exclude is None or p not in exclude):
            seen.add(p)
            out.append(p)
    return out


def prefixes(t: list[str]) -> list[str]:
    return _unique(" ".join(t[:k]) for k in range(2, min(len(t), 5) + 1))


def contiguous(t: list[str], exclude: set[str] | None = None) -> list[str]:
    n = len(t)
    grams = [" ".join(t[s:s + size]) for size in range(2, n) for s in range(n - size + 1)]
    if n >= 2:
        grams.append(" ".join(t))
    return _unique(grams, exclude)


def skipgrams(t: list[str], exclude: set[str] | None = None) -> list[str]:
    return _unique((" ".join(t[i] for i in c) for k in (2, 3, 4) for c in combinations(range(len(t)), k)), exclude)


def bags(t: list[str], exclude: set[str] | None = None) -> list[str]:
    out = []
    for k in (2, 3, 4):
        for c in combinations(range(len(t)), k):
            words = [t[i] for i in c]
            if len(set(words)) == k:
                out.append(" ".join(sorted(words)))
    return _unique(out, exclude)


def _fixed(keyword: str, gaz: Gazetteer | None, wanted: set[str], exclude: bool) -> dict[str, list[str]]:
    res: dict[str, list[str]] = {}
    if len(keyword) > MAX_CHARS:
        return {a: [] for a in ALGORITHMS if a in wanted}
    t = tokenize(keyword)
    pre = prefixes(t)
    res["prefix"] = pre
    ex = set(pre) if exclude else None
    con = contiguous(t, ex)
    res["contiguous"] = con
    ex2 = set(pre) | set(con) if exclude else None
    sk = skipgrams(t, ex2)
    res["skipgram"] = sk
    res["bag"] = bags(t, set(pre) | set(con) | set(sk) if exclude else None)
    if wanted & {"entity_prefix", "entity_contiguous", "entity_skipgram"}:
        seq: list[str] = []
        for token, is_entity in (gaz.replace(keyword) if gaz else [(w, False) for w in keyword.split()]):
            seq.extend([token] if is_entity else tokenize(token))
        ep_raw = prefixes(seq)
        ep = _entity(ep_raw)
        ec = _entity(contiguous(seq, set(ep_raw) if exclude else None))
        es = _entity(skipgrams(seq))
        if exclude:
            ec = [p for p in ec if p not in set(ep)]
            es = [p for p in es if p not in set(ep) | set(ec)]
        res["entity_prefix"], res["entity_contiguous"], res["entity_skipgram"] = ep, ec, es
    return {a: res.get(a, []) for a in ALGORITHMS if a in wanted}


def _entity(patterns: list[str]) -> list[str]:
    out: list[str] = []
    for p in patterns:
        if "]" in p:
            out.extend(_collapse(_expand([p])))
    return _unique(out)


# --------------------------------------------------------------------------------------------- shared

def _expand(patterns: list[str]) -> list[str]:
    """[a>b] -> one pattern with [a] and one with [b]; every occurrence of that exact entity changes together."""
    while True:
        out, changed = [], False
        for p in patterns:
            for m in _BRACKET.finditer(p):
                e = m.group(0)
                parts = e.split(">")
                if len(parts) > 1:
                    out.extend(p.replace(e, "[" + part.replace("[", "").replace("]", "").strip() + "]") for part in parts)
                    changed = True
                    break
            else:
                out.append(p)
        patterns = out
        if not changed:
            return patterns


def _collapse(patterns: list[str]) -> list[str]:
    """"x [local] [local]" -> "x [local]", but a pattern made only of that entity keeps both."""
    out = []
    for p in patterns:
        t = p
        for m in _BRACKET.finditer(p):
            e = m.group(0)
            if t.replace(e, "").strip() and (e + " " + e) in t:
                t = t.replace(e + " " + e, e)
        out.append(t)
    return out


# --------------------------------------------------------------------------------------------- legacy 2019

def legacy_tokenize(text: str) -> str:
    """The 2019 tokenizer: no hyphen split, accents kept for the stopword check, digits and punctuation kept, and a
    dropped word still leaves its separator (so "bolo de cenoura" -> "bol  cenour")."""
    final = []
    for word in text.lower().split(" "):
        if word and word not in _STOP_EXACT:
            s = stem(word)
            final.append(s + " " if s else " ")
        else:
            final.append(" ")
    return "".join(final).strip()


def _ltok(text: str) -> str:
    return legacy_tokenize(text).strip().replace("  ", " ")


def _lviana(text: str) -> list[str]:
    if len(text) > MAX_CHARS:
        return []
    words = [w for w in _ltok(text).split(" ") if w]
    out, p = [], ""
    for x in range(min(len(words), 5)):
        if x > 0:
            p += " "
        p += words[x].strip()
        if x > 0 and p not in out:
            out.append(p)
    return out


def _lvilela(text: str, tv: list[str] | None = None) -> list[str]:
    if len(text) > MAX_CHARS:
        return []
    if tv is None:
        tv = _lviana(text)
    tk = _ltok(text)
    words = [w for w in tk.split(" ") if w]
    out: list[str] = []
    for size in range(2, len(words)):
        for start in range(len(words) - size + 1):
            p = " ".join(words[start:start + size])
            if p not in out and p not in tv:
                out.append(p)
    if " " in tk and tk not in out and tk not in tv:
        out.append(tk)
    return out


def _loliveira(text: str, tvl: list[str] | None = None, tv: list[str] | None = None) -> list[str]:
    if len(text) > MAX_CHARS:
        return []
    if tvl is None:
        tvl = _lvilela(text)
    if tv is None:
        tv = _lviana(text)
    w = [x for x in _ltok(text).split(" ") if x]
    out: list[str] = []
    for k in (2, 3, 4):
        for c in combinations(range(len(w)), k):
            p = " ".join(w[i] for i in c)
            if p not in out and p not in tvl and p not in tv:
                out.append(p)
    return out


def _ldourado(text: str, tvl: list[str] | None = None, tol: list[str] | None = None,
              tv: list[str] | None = None) -> list[str]:
    if len(text) > MAX_CHARS:
        return []
    if tvl is None:
        tvl = _lvilela(text)
    if tol is None:
        tol = _loliveira(text)
    if tv is None:
        tv = _lviana(text)
    parts = _ltok(text).split(" ")
    out: list[str] = []
    for k in (2, 3, 4):
        for tup in product(parts, repeat=k):
            if len(set(tup)) != k:
                continue
            p = " ".join(sorted(" ".join(tup).split(" "))).strip()
            if p not in out and p not in tvl and p not in tol and p not in tv:
                out.append(p)
    return out


def _lngrams(text: str) -> list[str]:
    words = [w for w in text.split(" ") if w]
    out: list[str] = []
    for size in range(1, len(words)):
        for start in range(len(words) - size + 1):
            p = " ".join(words[start:start + size])
            if p not in out:
                out.append(p)
    if text not in out:
        out.append(text)
    return out


def _lnormalize(raw: dict[str, str], text: str) -> str:
    """Original replacement: longest n-gram first (ties in generation order), string-level, repeated until stable."""
    for _ in range(1000):
        keys = sorted(_lngrams(text), key=lambda k: -len(k))
        for key in keys:
            if key and key in raw:
                ent = "¨".join(raw[key].strip().replace(" > ", ">").replace(" ", "-"))
                t = text
                if t.startswith(key + " "):
                    t = t.replace(key + " ", "[" + ent + "] ")
                if t.endswith(" " + key):
                    t = t.replace(" " + key, " [" + ent + "]")
                if " " + key + " " in t:
                    t = t.replace(" " + key + " ", " [" + ent + "] ")
                elif key in t:
                    t = t.replace(key, " [" + ent + "] ")
                text = t
                break
        else:
            return text
    raise RecursionError("entity replacement did not converge")


def _lstrip_trema(text: str) -> str:
    for m in list(_BRACKET.finditer(text)):
        o = m.group(0)
        text = text.replace(o, o.replace("¨", ""))
    return text


def _lentity(raw: dict[str, str], text: str, generator) -> tuple[list[str], str]:
    text = _lstrip_trema(_lnormalize(raw, re.sub(r"\s+", " ", text).lower()))
    out: list[str] = []
    for p in generator(text):
        if len(p.split("]")) > 1:
            out.extend(_collapse(_expand([p])))
    return out, text


def _legacy(keyword: str, gaz: Gazetteer | None, wanted: set[str], exclude: bool) -> dict[str, list[str]]:
    raw = gaz.raw if gaz else {}
    res: dict[str, list[str]] = {}
    tv = _lviana(keyword)
    res["prefix"] = tv
    tvl = _lvilela(keyword, None if exclude else [])
    res["contiguous"] = tvl
    if "skipgram" in wanted or "bag" in wanted:
        tol = _loliveira(keyword, *((tvl, tv) if exclude else ([], [])))
        res["skipgram"] = tol
        if "bag" in wanted:
            res["bag"] = _ldourado(keyword, *((tvl, tol, tv) if exclude else ([], [], [])))

    def barbosa(text: str) -> list[str]:
        return _lentity(raw, text, _lviana)[0]

    def figueredo(text: str) -> list[str]:
        out, t = _lentity(raw, text, lambda s: _lvilela(s, None if exclude else []))
        if not exclude:
            return out
        b = barbosa(t)  # the original runs Barbosa again on the already-normalized text
        return [x for x in out if x not in b]

    if wanted & {"entity_prefix", "entity_contiguous", "entity_skipgram"}:
        res["entity_prefix"] = barbosa(keyword)
        res["entity_contiguous"] = figueredo(keyword)
        t0 = _lstrip_trema(re.sub(r"\s+", " ", keyword).lower())
        rosa, _ = _lentity(raw, t0, lambda s: _loliveira(s, [], []))
        if exclude:
            b, f = barbosa(t0), figueredo(t0)
            rosa = [x for x in rosa if x not in b and x not in f]
        res["entity_skipgram"] = rosa
    return {a: res.get(a, []) for a in ALGORITHMS if a in wanted}


# --------------------------------------------------------------------------------------------- public API

def mine(keyword: str, gazetteer: Gazetteer | None = None, algorithms: Iterable[str] | None = None,
         mode: str = "fixed", exclude: bool = True) -> dict[str, list[str]]:
    """Patterns of one search, per algorithm.

    algorithms: technical names or aliases (default: all seven). exclude: apply the exclusion chain (each algorithm
    drops what an earlier one already produced for the same search). mode: "fixed" or "legacy2019".
    """
    wanted = {algorithm_name(a) for a in (algorithms or ALGORITHMS)}
    if mode == "fixed":
        return _fixed(keyword, gazetteer, wanted, exclude)
    if mode == "legacy2019":
        return _legacy(keyword, gazetteer, wanted, exclude)
    raise ValueError("mode must be 'fixed' or 'legacy2019'")
