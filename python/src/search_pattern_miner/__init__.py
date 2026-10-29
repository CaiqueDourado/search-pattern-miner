"""Turn search queries into reusable patterns ("como fazer [comida]"), with the seven algorithms the author used
from 2017 to 2019 to choose niches: prefixes, n-grams, skip-grams, bags of words and the same after replacing
entities by their type. Same results as the .NET version."""

from __future__ import annotations

from .aggregate import Pattern, Search, aggregate, mine_pairs, ntile
from .miner import ALGORITHMS, ALIASES, Gazetteer, algorithm_name, legacy_tokenize, mine, placeholder, tokenize
from .stemmer import STOPWORDS, stem

__version__ = "0.1.0"
__all__ = ["ALGORITHMS", "ALIASES", "Gazetteer", "Pattern", "STOPWORDS", "Search", "aggregate", "algorithm_name",
           "legacy_tokenize", "mine", "mine_pairs", "ntile", "placeholder", "stem", "tokenize"]
