"""
Turn a Hinglish reply into all-Devanagari text for voices that only read Indian
scripts (IndicF5). Only the spoken text changes; the reply on screen keeps its
English words.

- English words are written the way they're pronounced: espeak (via Kokoro's
  tokenizer) gives their English IPA, which is spelled out in Devanagari -
  "platform" becomes प्लैटफ़ॉर्म.
- Numbers become Hindi number words: 2024 -> दो हज़ार चौबीस.
"""
import re

_UNITS = (
    "शून्य एक दो तीन चार पाँच छह सात आठ नौ दस ग्यारह बारह तेरह चौदह पंद्रह सोलह सत्रह अठारह उन्नीस "
    "बीस इक्कीस बाईस तेईस चौबीस पच्चीस छब्बीस सत्ताईस अट्ठाईस उनतीस तीस इकतीस बत्तीस तैंतीस चौंतीस "
    "पैंतीस छत्तीस सैंतीस अड़तीस उनतालीस चालीस इकतालीस बयालीस तैंतालीस चवालीस पैंतालीस छियालीस "
    "सैंतालीस अड़तालीस उनचास पचास इक्यावन बावन तिरेपन चौवन पचपन छप्पन सत्तावन अट्ठावन उनसठ साठ "
    "इकसठ बासठ तिरेसठ चौंसठ पैंसठ छियासठ सड़सठ अड़सठ उनहत्तर सत्तर इकहत्तर बहत्तर तिहत्तर चौहत्तर "
    "पचहत्तर छिहत्तर सतहत्तर अठहत्तर उन्यासी अस्सी इक्यासी बयासी तिरासी चौरासी पचासी छियासी सत्तासी "
    "अट्ठासी नवासी नब्बे इक्यानवे बानवे तिरानवे चौरानवे पचानवे छियानवे सत्तानवे अट्ठानवे निन्यानवे"
).split()


def hindi_number(n: int) -> str:
    """Indian numbering: हज़ार, लाख, करोड़."""
    if n < 100:
        return _UNITS[n]
    parts = []
    for size, name in ((10_000_000, "करोड़"), (100_000, "लाख"), (1000, "हज़ार"), (100, "सौ")):
        if n >= size:
            parts.append(f"{hindi_number(n // size)} {name}")
            n %= size
    if n:
        parts.append(_UNITS[n])
    return " ".join(parts)


# English IPA (espeak en-us, as Kokoro's tokenizer gives it) -> Devanagari. Longest first.
_CONSONANTS = [
    ("tʃ", "च"), ("dʒ", "ज"), ("p", "प"), ("b", "ब"), ("t", "ट"), ("d", "ड"), ("ɾ", "ट"), ("k", "क"),
    ("ɡ", "ग"), ("g", "ग"), ("f", "फ़"), ("v", "व"), ("w", "व"), ("θ", "थ"), ("ð", "द"), ("s", "स"),
    ("z", "ज़"), ("ʃ", "श"), ("ʒ", "ज़"), ("h", "ह"), ("x", "ख़"), ("m", "म"), ("n", "न"), ("l", "ल"),
    ("ɹ", "र"), ("r", "र"), ("j", "य"),
]
# (independent letter, vowel sign after a consonant); "" sign = the inherent a.
_VOWELS = [
    ("aɪə", ("आयअ", "ायअ")), ("aɪ", ("आइ", "ाइ")), ("aʊ", ("आउ", "ाउ")), ("eɪ", ("ए", "े")), ("oʊ", ("ओ", "ो")),
    ("ɔɪ", ("ऑय", "ॉय")), ("ɛɹ", ("एयर", "ेयर")), ("ɪɹ", ("इयर", "ियर")), ("ʊɹ", ("उअर", "ुअर")),
    ("ɜːɹ", ("अर", "र")), ("ɜː", ("अर", "र")), ("ɝ", ("अर", "र")), ("ɚ", ("अर", "र")),
    ("iː", ("ई", "ी")), ("uː", ("ऊ", "ू")), ("ɑːɹ", ("आर", "ार")), ("ɑː", ("आ", "ा")), ("ɔːɹ", ("ऑर", "ॉर")),
    ("ɔː", ("ऑ", "ॉ")), ("æ", ("ऐ", "ै")), ("ɑ", ("आ", "ा")), ("ɒ", ("ऑ", "ॉ")), ("ɔ", ("ऑ", "ॉ")),
    ("ʌ", ("अ", "")), ("ə", ("अ", "")), ("ɐ", ("अ", "")), ("a", ("अ", "")), ("ɪ", ("इ", "ि")), ("ᵻ", ("इ", "ि")),
    ("i", ("इ", "ि")), ("ʊ", ("उ", "ु")), ("u", ("उ", "ु")), ("ɛ", ("ए", "े")), ("e", ("ए", "े")), ("o", ("ओ", "ो")),
]
_HALANT = "्"
_IGNORED = set("ˈˌːˑ.‿-ʔ̩")


def ipa_to_devanagari(ipa: str) -> str:
    out, i, prev_consonant = [], 0, False
    while i < len(ipa):
        ch = ipa[i]
        if ch == " ":
            out.append(" ")
            prev_consonant = False
            i += 1
            continue
        if ch == "ŋ":
            # "ng" is a nasal sign: थिंकिंग, बैंक. Before k/g it's just the nasal.
            nxt = ipa[i + 1:i + 2]
            out.append("ं" if nxt in ("k", "ɡ", "g") else "ंग")
            prev_consonant = nxt not in ("k", "ɡ", "g")
            i += 1
            continue
        if ch in _IGNORED:
            i += 1
            continue
        vowel = next(((sym, v) for sym, v in _VOWELS if ipa.startswith(sym, i)), None)
        if vowel:
            sym, (letter, sign) = vowel
            out.append(sign if prev_consonant else letter)
            # A vowel that ends in r (ɑːɹ -> ार) leaves a consonant behind.
            prev_consonant = letter.endswith("र")
            i += len(sym)
            continue
        cons = next(((sym, d) for sym, d in _CONSONANTS if ipa.startswith(sym, i)), None)
        if cons:
            sym, letter = cons
            if prev_consonant and not (out and out[-1] == "ं"):
                out.append(_HALANT)
            out.append(letter)
            prev_consonant = True
            i += len(sym)
            continue
        i += 1  # anything unknown is dropped
    # Word-final consonants carry no halant in Hindi spelling (पार्क, not पार्क्).
    return "".join(out)


_TOKEN = re.compile(r"\d[\d,]*|[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’&\-]*")


def to_devanagari_speech(text: str, phonemize) -> str:
    """phonemize(text, lang) -> IPA, e.g. kokoro_onnx's tokenizer.phonemize.
    Run the server's normalize() first so prices, phone numbers and emails are
    already spelled out."""
    def convert(match):
        token = match.group()
        if token[0].isdigit():
            digits = token.replace(",", "")
            return hindi_number(int(digits)) if len(digits) <= 9 else " ".join(_UNITS[int(d)] for d in digits)
        ipa = phonemize(token.replace("&", " and "), "en-us")
        return ipa_to_devanagari(re.sub(r"\([a-z\-]+\)", "", ipa).strip())

    return _TOKEN.sub(convert, text.replace("&", " and "))
