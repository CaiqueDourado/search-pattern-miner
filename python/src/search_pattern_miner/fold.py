"""Accent folding by an explicit table, so Python and .NET give the same result without Unicode normalization."""

from __future__ import annotations

FOLD = {
    "á": "a", "à": "a", "â": "a", "ã": "a", "ä": "a", "é": "e", "è": "e", "ê": "e", "ë": "e",
    "í": "i", "ì": "i", "î": "i", "ï": "i", "ó": "o", "ò": "o", "ô": "o", "õ": "o", "ö": "o",
    "ú": "u", "ù": "u", "û": "u", "ü": "u", "ç": "c", "ñ": "n", "ý": "y", "ÿ": "y",
}
_TABLE = str.maketrans(FOLD)


def fold(text: str) -> str:
    """Lower case and remove the accents of Portuguese and Spanish letters."""
    return text.lower().translate(_TABLE)
