# Contributing

Issues and pull requests are welcome in English or Portuguese.

```bash
python -m pip install ./python[test] && python -m pytest python/tests
cd dotnet && dotnet test
```

## Rules that keep both languages identical

- Change the rule in [spec/ALGORITHMS.md](spec/ALGORITHMS.md) first, then in **both** implementations.
- Ordinal string comparison, stable sorts, no culture-dependent case mapping and no Unicode normalization at run
  time. Accents go through the table in `fold.py` / `Fold.cs`.
- Regenerate the test vectors with `python tools/make_testdata.py` and commit `testdata/`. Both test suites must pass
  with the new `golden.json`.
- `python evaluation/reproduce.py` must still reproduce every published pattern in legacy mode.

## Useful contributions

- Gazetteers for other domains (with their license), searches whose patterns look wrong, stemmers for other
  languages.
