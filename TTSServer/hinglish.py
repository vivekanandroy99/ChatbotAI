"""
Undo protected English words that a translator wrote in Devanagari.

The Hindi reply is produced from an English answer. Any Devanagari word that
*sounds like* a protected word of that English answer (अल्टकोर ~ Altcore) is a
transliteration, and is replaced by the exact English spelling - so brand names
are always written, and therefore spoken, the same way. Which words are
protected is the caller's choice (brand names, team names, acronyms); everything
else stays as everyday Hindi. Real Hindi words don't sound like any word in
the English answer (they're translations of it), and the commonest ones are
protected outright.

Sound comparison uses consonant skeletons: vowels are too inconsistent across
the two spellings to compare, consonants are not.
"""
import re
import unicodedata

_DEVANAGARI_WORD = re.compile(r"[ऀ-ॣ०-ॿ]+")
# Phone numbers and prices should always read as plain digits.
_DEVANAGARI_DIGITS = str.maketrans("०१२३४५६७८९", "0123456789")
_LATIN_WORD = re.compile(r"[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'\-]*")  # café, naïve

# Consonants mapped to their plain Latin sound; aspirated forms collapse onto the
# unaspirated one because English spellings don't mark aspiration.
_CONSONANTS = {
    "क": "k", "ख": "k", "ग": "g", "घ": "g", "ङ": "n",
    "च": "c", "छ": "c", "ज": "j", "झ": "j", "ञ": "n",
    "ट": "t", "ठ": "t", "ड": "d", "ढ": "d", "ण": "n",
    "त": "t", "थ": "t", "द": "d", "ध": "d", "न": "n",
    "प": "p", "फ": "f", "ब": "b", "भ": "b", "म": "m",
    "य": "", "र": "r", "ल": "l", "व": "v",
    "श": "s", "ष": "s", "स": "s", "ह": "h",
    "ं": "n", "ँ": "n",
}
_INDEPENDENT_VOWELS = set("अआइईउऊऋएऐओऔऑॲ")

# Common Hindi words that must never be swapped, however they sound.
_HINDI_PROTECTED = set(
    "है हैं था थी थे हो को की के का में से पर और या भी तो ही यह वह ये वो हम तुम आप मैं जो कि "
    "कर करें करता करती करते करना किया लिए साथ बाद पहले नहीं न अब जब तब कब क्या कैसे कौन कहाँ "
    "यहाँ वहाँ उन इन उस इस अपने अपना अपनी हर बस सिर्फ लेकिन फिर मदद काम समय दिन साल लोग बात "
    "कम ज़्यादा ज्यादा सब कुछ एक दो तीन चार पाँच बहुत सकते सकता सकती रहा रही रहे गया गई गए होता होती "
    # Everyday verbs: short consonant skeletons that English words collide with (रख ~ reach).
    "रख रखें रखे रखते रखता रखती रखना देख देखें देखे देखते देखता देखती दिख दिखा दिखे बन बना बनी बने बनाता बनाती "
    "चल चला चली चले चलता चलती मिल मिला मिली मिले मिलता मिलती कह कहा कहें सुन सुना ले लें लिया दे दें दिया "
    "जा जाता जाती जाते जाना आ आता आती आते पा पाते पाता पाती बता बताइए बताएं पूछ पूछिए पूछें जान जानना जानें "
    "समझ समझें सोच लग लगता लगती लगते भेज भेजें खोल खुला खुली खुले बंद रुक रोक".split()
)

_ENGLISH_STOPWORDS = set(
    "the and for with that this from are was were has have had will can our your their its his her "
    "you they them who what when where which how not but all any also into than then there these those".split()
)


def _dev_skeleton(word: str) -> str:
    # Word-initial य counts as a vowel: यूनिट ~ unit, यूज़र ~ user.
    out = "V" if word[0] in _INDEPENDENT_VOWELS or word[0] == "य" else ""
    for i, ch in enumerate(word):
        if ch == "़" and out.endswith("d"):  # ड़ / ढ़ are flapped r sounds
            out = out[:-1] + "r"
        if ch == "ह" and i > 0:  # like English, h only counts at the start (सलाह is not "sls")
            continue
        out += _CONSONANTS.get(ch, "")
    # j, z (ज़) and s are one class: Hindi writers use ज and ज़ interchangeably
    # for an English z, and English writes that z sound as "s" (residential).
    return re.sub(r"(.)\1+", r"\1", out.replace("j", "s"))


def _en_skeleton(word: str) -> str:
    w = unicodedata.normalize("NFKD", word.lower()).encode("ascii", "ignore").decode()  # café -> cafe
    w = re.sub(r"gn$", "n", w)  # design, sign
    w = re.sub(r"[tc]i(?=[aeiou])", "sh", w)  # residential, special, patient
    w = re.sub(r"(?<=[aeiou])w(?![aeiouy])", "", w)  # a w after a vowel is part of it: town, new
    for a, b in (("tion", "shn"), ("sion", "shn"), ("ph", "f"), ("ck", "k"), ("qu", "kv"), ("x", "ks"),
                 ("sh", "s"), ("ch", "c"), ("th", "t"), ("gh", "g"), ("wh", "v"), ("w", "v"), ("q", "k")):
        w = w.replace(a, b)
    w = re.sub(r"c(?=[eiy])", "s", w).replace("c", "k")
    w = re.sub(r"g(?=[eiy])", "j", w)
    out = "V" if w[0] in "aeiouy" else ""
    out += "".join(ch for ch in w if ch not in "aeiouy'-" and ch != "h")
    if w[0] == "h":
        out = "h" + out
    return re.sub(r"(.)\1+", r"\1", out.replace("j", "s").replace("z", "s"))


def _close(a: str, b: str) -> bool:
    if a == b:
        return True
    # One-sound-off matches only for long words; short skeletons collide with real Hindi.
    if min(len(a), len(b)) < 5 or abs(len(a) - len(b)) > 1:
        return False
    # One edit apart (substitution, insertion or deletion).
    if len(a) == len(b):
        return sum(x != y for x, y in zip(a, b)) == 1
    short, long_ = sorted((a, b), key=len)
    return any(long_[:i] + long_[i + 1:] == short for i in range(len(long_)))


# Bookish Hindi -> everyday spoken Hindi. Only swaps that keep the sentence
# grammatical whatever the gender/number around them; longest phrases first.
_COLLOQUIAL = [
    ("के माध्यम से", "के ज़रिए"), ("माध्यम से", "के ज़रिए"),
    ("प्रदान करता है", "देता है"), ("प्रदान करती है", "देती है"), ("प्रदान करते हैं", "देते हैं"),
    ("प्रदान करता", "देता"), ("प्रदान करती", "देती"), ("प्रदान करते", "देते"), ("प्रदान करना", "देना"),
    ("प्रदान करने", "देने"), ("प्रदान करके", "देकर"), ("प्रदान किया", "दिया"), ("प्रदान की", "दी"),
    ("संपर्क कर सकते", "बात कर सकते"), ("संपर्क करें", "बात करें"), ("संपर्क करने", "बात करने"),
    ("सहायता", "मदद"), ("उपयोग", "इस्तेमाल"), ("अत्यधिक", "बहुत ज़्यादा"), ("अधिक", "ज़्यादा"),
    ("केवल", "सिर्फ़"), ("सरल", "आसान"), ("यदि", "अगर"), ("आवश्यकता", "ज़रूरत"), ("आवश्यक", "ज़रूरी"),
    ("आयोजित किया जाता है", "होता है"), ("आयोजित की जाती है", "होती है"), ("आयोजित होता है", "होता है"),
    ("आयोजित होती है", "होती है"), ("लागत", "कीमत"),
    ("परंतु", "लेकिन"), ("किंतु", "लेकिन"), ("प्रश्न", "सवाल"), ("हेतु", "के लिए"), ("विभिन्न", "अलग-अलग"),
]
# Whole words/phrases only: अधिक must not touch अधिकारी. (The danda । is not a letter.)
_COLLOQUIAL_RE = [
    (re.compile(r"(?<![ऀ-ॣॲ-ॿ])" + re.escape(a) + r"(?![ऀ-ॣॲ-ॿ])"), a, b) for a, b in _COLLOQUIAL
]


def colloquial(text: str):
    """Returns (text, [(bookish, everyday), ...])."""
    swaps = []
    for pattern, bookish, everyday in _COLLOQUIAL_RE:
        text, n = pattern.subn(everyday, text)
        if n:
            swaps.append((bookish, everyday))
    return text.replace("के के ज़रिए", "के ज़रिए"), swaps


def restore_english(english: str, hinglish: str, protect=None):
    """Returns (fixed_text, [(devanagari_word, english_word), ...]).
    protect(word) -> bool limits which English words may be restored; by default
    any word of the English text can be."""
    hinglish = hinglish.translate(_DEVANAGARI_DIGITS)
    candidates = {}
    for word in _LATIN_WORD.findall(english):
        if len(word) < 3 or word.lower() in _ENGLISH_STOPWORDS:
            continue
        if protect is not None and not protect(word):
            continue
        # The base form too ("supports" -> "support"): Hindi grammar carries the
        # verb/plural ending, so the transliterated word is usually the base form.
        forms = [word]
        if word.lower().endswith("s") and not word.lower().endswith("ss"):
            forms.append(word[:-1])
        for form in forms:
            skeleton = _en_skeleton(form)
            if len(skeleton.replace("V", "")) >= 2:
                candidates.setdefault(skeleton, form)

    replaced = []

    def swap(match):
        word = match.group()
        if word in _HINDI_PROTECTED:
            return word
        skeleton = _dev_skeleton(word)
        if len(skeleton.replace("V", "")) < 2:
            return word
        en_word = candidates.get(skeleton) or next(
            (w for s, w in candidates.items() if _close(skeleton, s)), None
        )
        if en_word is None:
            return word
        replaced.append((word, en_word))
        return en_word

    return _DEVANAGARI_WORD.sub(swap, hinglish), replaced
