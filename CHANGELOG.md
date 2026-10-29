# Changelog

## 0.1.0 — 2026-10-30

- Seven pattern algorithms with technical names and their original nicknames: prefix (Viana), contiguous (Vilela),
  skipgram (Oliveira), bag (Dourado), entity_prefix (Barbosa), entity_contiguous (Figueredo), entity_skipgram (Rosa),
  with the per-search exclusion chain.
- Port of Lucene's BrazilianStemmer and the NLTK Portuguese stopwords minus eight search words.
- Gazetteer with one pattern per type, expansion and collapse of entities; example gazetteer of places and
  hand-made types.
- Two modes: `fixed` (default; whole-word entity matching, atomic placeholders, no duplicates) and `legacy2019`
  (reproduces the published google-keywords-dataset patterns: 921/921 prefixes, 400/400 Viana, 400/400 Vilela).
- Aggregation with totals, top search, examples and NTILE(20) bands; command line `show` and `mine`.
- Python and .NET with identical results (8,801 golden checks).
