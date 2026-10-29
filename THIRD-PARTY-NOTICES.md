# Third-party notices

## Apache Lucene, BrazilianStemmer (Apache License 2.0)

`python/src/search_pattern_miner/stemmer.py` and `dotnet/src/SearchPatternMiner/Stemmer.cs` are ports of the
`BrazilianStemmer` class of Apache Lucene (`org.apache.lucene.analysis.br.BrazilianStemmer`), as the author's own C#
used it in 2017–2019. The suffix tables, the order in which they are checked and two quirks of the original are
kept on purpose (spec/ALGORITHMS.md, section 1).

> Apache Lucene. Copyright 2001-2024 The Apache Software Foundation.
> This product includes software developed at The Apache Software Foundation (https://www.apache.org/).
> Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
> the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0. Unless required by
> applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
> WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

Changes: rewritten in Python and C#; the length limit is `2 < length < 300` instead of Lucene's 30, as in the
author's version.

## Snowball / NLTK Portuguese stopwords (BSD 3-Clause)

The stopword list is the Portuguese list distributed with NLTK, which comes from the Snowball project
(https://snowballstem.org, BSD 3-Clause, Copyright (c) 2001 Dr Martin Porter, Copyright (c) 2004-2005 Richard Boulton),
minus eight words: para, mais, quando, como, muito, qual, pelo, pelos.

## Data

`data/gazetteer-pt.tsv`: the places come from municipios-e-estados-do-brasil (IBGE data, CC BY 4.0, by the same
author); the other entries were written by hand. `examples/keywords.csv` is synthetic. `evaluation/published_sample.csv`
is a sample of google-keywords-dataset (CC BY 4.0, by the same author).
