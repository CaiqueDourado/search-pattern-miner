"""Group searches by pattern and rank the patterns, as the author's curation screens did.

For each (group, algorithm, pattern): how many distinct searches it covers, their total monthly volume, the mean
competition and CPC, the search with the most volume and up to three examples. Then four bands from 1 to 20
(NTILE(20), "vigintiles"): band 1 is the best for SEO: most volume, most searches, least competition, lowest CPC.
Bands are computed within each (group, algorithm). Ties are broken by the pattern text, so results are stable.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Iterable

from .miner import ALIASES, Gazetteer, mine


@dataclass
class Search:
    keyword: str
    volume: int | None = None
    competition: float | None = None
    cpc: float | None = None
    group: str = ""


@dataclass
class Pattern:
    group: str
    algorithm: str
    pattern: str
    num_words: int
    num_searches: int = 0
    volume_total: int = 0
    competition_avg: float | None = None
    cpc_avg: float | None = None
    top_search: str = ""
    examples: list[str] = field(default_factory=list)
    band_volume: int = 0
    band_searches: int = 0
    band_competition: int | None = None
    band_cpc: int | None = None

    @property
    def alias(self) -> str:
        return ALIASES[self.algorithm]

    def as_dict(self) -> dict:
        d = dict(self.__dict__)
        d["alias"] = self.alias
        return d


def mine_pairs(searches: Iterable[Search], gazetteer: Gazetteer | None = None, algorithms: Iterable[str] | None = None,
               mode: str = "fixed", exclude: bool = True) -> Iterable[tuple[Search, str, str]]:
    """(search, algorithm, pattern) for every pattern of every search."""
    algos = list(algorithms) if algorithms else None
    for s in searches:
        for algorithm, patterns in mine(s.keyword, gazetteer, algos, mode, exclude).items():
            for p in patterns:
                yield s, algorithm, p


def ntile(n: int, k: int = 20) -> list[int]:
    """SQL NTILE(k) bands for n ordered rows: the first n % k bands get one extra row."""
    if n == 0:
        return []
    base, extra = divmod(n, k)
    bands = []
    for b in range(1, min(n, k) + 1):
        bands.extend([b] * (base + (1 if b <= extra else 0)))
    return bands


def aggregate(searches: Iterable[Search], gazetteer: Gazetteer | None = None, algorithms: Iterable[str] | None = None,
              mode: str = "fixed", exclude: bool = True) -> list[Pattern]:
    """Patterns with totals and bands, sorted by group, algorithm and volume (largest first)."""
    acc: dict[tuple[str, str, str], dict[str, Search]] = {}
    for s, algorithm, p in mine_pairs(searches, gazetteer, algorithms, mode, exclude):
        acc.setdefault((s.group, algorithm, p), {}).setdefault(s.keyword, s)
    out: list[Pattern] = []
    for (group, algorithm, p), by_kw in acc.items():
        rows = list(by_kw.values())
        comps = [r.competition for r in rows if r.competition is not None]
        cpcs = [r.cpc for r in rows if r.cpc is not None]
        ranked = sorted(rows, key=lambda r: (-(r.volume or 0), r.keyword))
        out.append(Pattern(
            group=group, algorithm=algorithm, pattern=p, num_words=len(p.split()), num_searches=len(rows),
            volume_total=sum(r.volume or 0 for r in rows),
            competition_avg=sum(comps) / len(comps) if comps else None,
            cpc_avg=sum(cpcs) / len(cpcs) if cpcs else None,
            top_search=ranked[0].keyword, examples=[r.keyword for r in ranked[:3]]))
    _bands(out)
    out.sort(key=lambda x: (x.group, list(ALIASES).index(x.algorithm), -x.volume_total, x.pattern))
    return out


def _bands(patterns: list[Pattern]) -> None:
    groups: dict[tuple[str, str], list[Pattern]] = {}
    for p in patterns:
        groups.setdefault((p.group, p.algorithm), []).append(p)
    for items in groups.values():
        for attr, key in (("band_volume", lambda x: (-x.volume_total, x.pattern)),
                          ("band_searches", lambda x: (-x.num_searches, x.pattern))):
            for p, b in zip(sorted(items, key=key), ntile(len(items))):
                setattr(p, attr, b)
        for attr, value in (("band_competition", "competition_avg"), ("band_cpc", "cpc_avg")):
            have = sorted((x for x in items if getattr(x, value) is not None), key=lambda x: (getattr(x, value), x.pattern))
            for p, b in zip(have, ntile(len(have))):
                setattr(p, attr, b)
