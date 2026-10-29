# search-pattern-miner

Turn search queries into reusable patterns (`como fazer [comida]`): prefixes, n-grams, skip-grams, bags of words
and the same after replacing entities by their type, for Brazilian Portuguese, with a Lucene-compatible stemmer.
No dependencies. Same results as the .NET package `SearchPatternMiner`.

```python
from search_pattern_miner import Gazetteer, mine

gaz = Gazetteer.load("data/gazetteer-pt.tsv")
mine("como fazer panqueca", gaz, ["barbosa"])   # {'entity_prefix': ['com faz [comida]']}
```

Command line: `search-pattern-miner mine keywords.csv --gazetteer gazetteer-pt.tsv > patterns.csv`.

Documentation and the rules: https://github.com/CaiqueDourado/search-pattern-miner
