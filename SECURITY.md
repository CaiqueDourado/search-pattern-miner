# Security

Please report vulnerabilities privately through GitHub: **Security → Report a vulnerability** on this repository.
Do not open a public issue.

Supported versions: the latest release.

Notes for users:

- The library reads only the data you pass to it and makes no network calls.
- Skip-grams and bags of words grow with the number of words of a search (combinations of up to 4); searches over
  150 characters produce no pattern, which bounds the work per search.
- Regular expressions use bounded patterns without nested quantifiers.
