# Algorithms (the contract both implementations follow)

Rules go here first, then into Python (`python/src/search_pattern_miner`) and .NET (`dotnet/src/SearchPatternMiner`),
then `python tools/make_testdata.py` regenerates `testdata/golden.json`, which both test suites read.
Python is the reference when this text and the code disagree; fix the text.

Strings are compared ordinally. Lists keep insertion order. No Unicode normalization or culture-dependent call is
used at run time: lower case is `str.lower` / `ToLowerInvariant`, accents are folded by the table in `fold.py`.

## 1. Stemmer (`stem`)

A port of Lucene's `BrazilianStemmer` as the author's C# used it (Apache 2.0, see THIRD-PARTY-NOTICES.md).

1. **Prepare.** Lower case, then fold only `á â ã→a, é ê→e, í→i, ó ô õ→o, ú ü→u, ç→c, ñ→n` (Lucene's `changeTerm`; `à è ì ò ù`
   are kept). If the length is 2 or more, drop one leading `" ' - , ; . ? !`. If still 2 or more, drop one trailing
   `- ; ? ' "`. Then, if the term ends in `.`, `,` or `!` (checked in this order, first hit only), replace **every**
   occurrence of that character `c` by `" " + c`.
2. **Length.** Return null unless `2 < length < 300`. So every 1–2 character word disappears.
3. **Letters.** If any character is not a letter (`str.isalpha` / `char.IsLetter`), return the prepared term.
4. **Regions.** Vowels are `aeiou`. `R1 = r1(term)`, `R2 = r1(R1)`, `RV = rv(term)` (Snowball definitions; `r1` scans up to
   `len-1`, see `_r1`, `_rv`).
5. **Steps.** `altered = step1`; if not altered, `altered = step2`. If altered, step 3 (drop a final `i` of RV preceded by
   `c`), else step 4 (drop the first of `os, a, i, o` that ends RV). Always step 5 (`e` ending RV: drop `ue` after `g`, `ie`
   after `c`, else drop `e`).
6. **Suffix tables** are in `stemmer.py` (`_S1_*`, `_S2`), checked **in list order, first match wins**. Two Lucene quirks
   are kept on purpose: `logias` returns "altered" without changing the word; in step 2, an `ira` ending of RV removes
   `ava` (nothing) and returns "altered".

## 2. Stopwords

The 200-word NLTK/Snowball Portuguese list (`STOPWORDS`), minus `para, mais, quando, como, muito, qual, pelo, pelos`,
which the author kept because they carry meaning in searches (`como fazer` → `com faz`).

## 3. Tokenizers

**Fixed (`tokenize`)**, default. Split on whitespace. For each word: fold (lower case + `fold.py`), strip edge
characters `" ' . , ; : ! ? ( )`; drop it if empty or if it is a folded stopword; stem; drop it if the stem is null.

**Legacy 2019 (`legacy_tokenize`)**. Lower case, split on single spaces (empty strings kept). For each word: if it is
non-empty and not an exact (accented) stopword, emit `stem + " "`, or `" "` when the stem is null; otherwise emit `" "`.
Join and trim. Callers then replace `"  "` by `" "` **once** (so a run of three spaces leaves two). Hyphens, digits
and punctuation are kept.

## 4. Generators

Input: a token list (fixed) or the legacy token string split on spaces with empty items removed. Patterns are tokens
joined by one space. Each generator returns unique patterns in generation order.

| Name | Alias | Patterns |
|---|---|---|
| `prefix` | Viana | the first `k` tokens, `k = 2..min(n, 5)` |
| `contiguous` | Vilela | every run of `size` tokens, `size = 2..n-1`, by size then start; then the whole token list if `n ≥ 2` |
| `skipgram` | Oliveira | every ordered combination of positions of size 2, 3, 4 (by size, then lexicographic positions) |
| `bag` | Dourado | the same combinations, kept only when the words are pairwise different, words sorted ordinally |

**Exclusion chain** (per search, when `exclude` is true): contiguous drops prefix; skipgram drops prefix ∪ contiguous;
bag drops prefix ∪ contiguous ∪ skipgram.

A search longer than 150 characters produces no pattern.

**Legacy differences.** Contiguous adds the whole legacy token string as it is (it may hold a double space). Bag works
on every ordered tuple (`product`) of the legacy split **with empty items kept**, keeps tuples whose items are pairwise
different, sorts the words of `" ".join(tuple).split(" ")` ordinally, joins and trims. Order of output follows that
enumeration.

## 5. Entities

### 5.1 Gazetteer

A file of `surface<TAB>types` lines; `#` starts a comment; the first line for a surface wins. Types are separated by
`>`: `Local > Cidade` means the entity is both a `[local]` and a `[cidade]`, and **one pattern is made per type**.

`placeholder(types)`: split on `>`, fold each part, join its words with `-`, drop empty parts, join with `>`, wrap
in brackets: `Local > Cidade` → `[local>cidade]`, `Órgão` → `[orgao]`.

### 5.2 Fixed replacement (`Gazetteer.replace`)

Keys are folded and split on whitespace. A one-word key of 3 characters or less, or one that folds to a stopword or
to one of the 8 kept function words, is an **exact key**: it matches the lower-cased word with its accents (after
stripping edge characters), so `pará` is a state and `para` is not, `pe` is Pernambuco and `pé` is not.

Walk the words of the search from the left. At each position try the longest key first (up to the longest key in
the gazetteer), on folded words with edge characters stripped; if none matches, try the exact keys on that word;
else emit the word. A match emits its placeholder as one token and jumps past the matched words.

### 5.3 Legacy replacement (2019 behaviour)

On the lower-cased search with whitespace runs collapsed to one space, repeat until nothing matches (at most 1,000
rounds): list every run of words (size 1..n-1 by size then start, then the whole text), sort by character length
descending (stable), take the first that is an exact key of the raw gazetteer and replace it at **string level**:
if the text starts with `key + " "`, replace every `key + " "` by `[T] `; if it ends with `" " + key`, replace every
`" " + key` by ` [T]`; then if it contains `" " + key + " "`, replace those by ` [T] `, else replace every raw
occurrence of `key` by ` [T] ` (this is how `pr` matched inside `previsão`). `T` is the type string trimmed, `" > "`
turned into `>`, spaces into `-`, with `¨` between every character so later keys cannot match inside it. At the
end, remove `¨` inside every `[...]`.

### 5.4 The three entity algorithms

Tokenize the replaced text: fixed mode keeps each placeholder as one token and tokenizes the other words; legacy mode
passes the whole replaced string to the legacy generators (placeholders survive the stemmer because they contain
non-letters, with Lucene's accent folding).

| Name | Alias | Generator on the replaced tokens | Excluded (when `exclude`) |
|---|---|---|---|
| `entity_prefix` | Barbosa | prefix | — |
| `entity_contiguous` | Figueredo | contiguous, minus the prefixes of the replaced tokens | entity_prefix |
| `entity_skipgram` | Rosa | skipgram with no internal exclusion | entity_prefix ∪ entity_contiguous |

Each raw pattern is kept only if it contains `]`, then **expanded** and **collapsed**:

- *Expand*: while a pattern has a bracket with `>`, take the first such bracket, and emit one pattern per part with
  every occurrence of that exact bracket replaced by `[part]`.
- *Collapse*: for each bracket `E` found in the pattern, if the pattern without `E` is not blank, replace
  `E + " " + E` by `E`. So `x [local] [local]` → `x [local]`, but `[local] [local]` stays.

Fixed mode returns each pattern once. **Legacy mode keeps duplicates** (as the original `List<string>` did), and its
entity_contiguous exclusion runs entity_prefix again on the already-replaced text; entity_skipgram excludes the
outputs of entity_prefix and entity_contiguous computed on the lower-cased, collapsed original search.

## 6. Aggregation (`aggregate`)

Pairs (search, algorithm, pattern) are grouped by (group, algorithm, pattern), with each keyword counted once:
`num_searches`, `volume_total` (missing = 0), `competition_avg` and `cpc_avg` over the non-missing values (none = null),
`top_search` and `examples` = the first 1 and 3 keywords by volume descending, then keyword. `num_words` = words of
the pattern.

Bands (`ntile`): within each (group, algorithm), sort and assign NTILE(20): with `n` rows the first `n mod 20` bands
get one extra row; with fewer than 20 rows each row gets its own band. `band_volume` by volume_total descending,
`band_searches` by num_searches descending, `band_competition` and `band_cpc` by the average ascending (only rows with a
value; others null). Ties: pattern text ascending. Output order: group, algorithm (in the order of the table in §4/§5.4),
volume_total descending, pattern.

## 7. Not implemented (the original rule was not found)

- `padrao_pai` / `padrao_pai_nivel` (the generalized parent pattern of the published table).
- The 2022 tokenizer (hyphen split, accents and digits removed) used for the DataForSEO batch.
- The English and Spanish stemmers of the non-Brazilian markets (Snowball): pass already-tokenized text or extend
  `tokenize`.
