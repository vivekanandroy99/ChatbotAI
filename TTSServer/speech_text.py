"""
Text -> the form the voices should read aloud. Shared by the voice server and the listening
check (speech_check.py), so the check expects exactly what the voice was asked to say.
"""
import re

_EMAIL = re.compile(r"\b([\w.+-]+)@([\w-]+(?:\.[\w-]+)+)\b")
# Web addresses: "altscape.in" was mumbled (53% of its sounds heard) - say "altscape dot in".
_WEBSITE = re.compile(r"\b(?:https?://)?((?:www\.)?[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.(?:com|co|in|org|net|io|ai|app)(?:\.[a-z]{2})?)\b(?:/\S*)?", re.I)
_PHONE = re.compile(r"\+?\d[\d \-]{7,}\d")


def _spoken_domain(domain: str) -> str:
    """altscape.in -> "Altscape dot in" (the ending stays lowercase: dot com, dot in)."""
    parts = domain.split(".")
    return " dot ".join([p.capitalize() if p.lower() != "www" else p for p in parts[:-1]] + [parts[-1].lower()])


def normalize(text: str, lang: str) -> str:
    """Rewrite things the voice would otherwise read wrongly. lang: "hi" or an English code."""
    hindi = lang == "hi"
    rupees = "रुपये" if hindi else "rupees"
    text = re.sub(r"(?:₹|\bRs\.?|\bINR)\s*(\d[\d,]*(?:\.\d+)?)", rf"\1 {rupees}", text)
    # Phone numbers digit by digit, not "ninety thousand nine hundred...".
    text = _PHONE.sub(lambda m: " ".join(c for c in m.group() if c.isdigit()), text)
    # Emails: "contact at altcore dot co"; websites: "altscape dot in".
    # Name parts are capitalised so the voice treats them as names (Veena respells unknown names).
    text = _EMAIL.sub(lambda m: f"{m.group(1)} at {_spoken_domain(m.group(2))}", text)
    text = _WEBSITE.sub(lambda m: _spoken_domain(m.group(1)), text)
    text = text.replace("&", " और " if hindi else " and ")
    # Dashes between phrases are pauses, not words.
    text = re.sub(r"\s[–—-]\s", ", ", text)
    text = re.sub(r"[*_#`™®©]", "", text)
    return re.sub(r"\s{2,}", " ", text).strip()
