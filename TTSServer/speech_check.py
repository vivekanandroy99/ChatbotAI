"""
Listening check for recorded speech (a testing aid - Claude, which helps build this, can't
hear audio). For each sentence:
  - which words came out unclear or skipped: a phoneme recogniser
    (facebook/wav2vec2-xlsr-53-espeak-cv-ft) writes down the sounds actually spoken; they're
    aligned against the sounds the text should make (espeak, the dictionary the voices use) and
    each word gets a score. Sounds are compared in broad groups (a/ə/ʌ alike, r/ɾ/ɹ alike...) so
    an Indian accent isn't counted as a mistake - a word is flagged only when most of it is
    missing or different.
  - how natural it sounds: UTMOS22 (a MOS predictor, 1-5). Trained on English; use it to compare
    takes/settings, not as an absolute grade for Hindi.

Usage (venv python):
  python speech_check.py              latest SpeechReview session - flagged replies (F8) if any, else all
  python speech_check.py --all        every sentence of the latest session
  python speech_check.py <folder>     a SpeechReview session, or any folder with manifest.json {"file.wav": "text"}
Writes report.txt next to the recordings.
"""
import glob
import json
import os
import re
import sys
from pathlib import Path

os.environ.setdefault("HF_HUB_OFFLINE", "1")
import numpy as np
import soundfile as sf
import torch

PHONEME_MODEL = "facebook/wav2vec2-xlsr-53-espeak-cv-ft"
UTMOS_REPO = Path.home() / ".cache" / "torch" / "hub" / "tarepan_SpeechMOS_v1.2.0"
REVIEW_ROOT = Path(__file__).resolve().parent.parent / "SpeechReview"
DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
UNCLEAR_BELOW = 0.5  # share of a word's sounds that must come through
MIN_FLAG_SOUNDS = 3  # words shorter than this (के, है) aren't flagged: one missed sound is half the word

# Broad sound groups: accent differences stay inside a group.
_GROUPS = {
    "A": "aɑɐəʌæɒ", "E": "eɛ", "I": "iɪᵻɨy", "O": "oɔ", "U": "uʊɯ",
    "r": "rɾɹɽʁ", "t": "tʈθ", "d": "dɖð", "n": "nɳɲŋ", "l": "lɭʎ", "v": "vʋwβ",
    "s": "s", "S": "ʃʂɕ", "z": "zʒ", "k": "kq", "g": "ɡgɣ", "j": "j", "h": "hɦ",
    "p": "p", "b": "b", "m": "m", "f": "f", "x": "xχç",
}
_TO_GROUP = {ch: g for g, chars in _GROUPS.items() for ch in chars}
_MARKS = re.compile(r"[ˈˌːˑ̪̩̯̃ʰʲ.\d~-]")


def coarse(phone: str):
    """A phone -> its broad sound groups ('tʃ' -> ['t', 'S'])."""
    return [_TO_GROUP[ch] for ch in _MARKS.sub("", phone) if ch in _TO_GROUP]


class Checker:
    def __init__(self):
        from transformers import Wav2Vec2FeatureExtractor, Wav2Vec2ForCTC
        from huggingface_hub import snapshot_download
        from kokoro_onnx.tokenizer import Tokenizer

        folder = snapshot_download(PHONEME_MODEL)
        self.vocab = {i: t for t, i in json.load(open(os.path.join(folder, "vocab.json"), encoding="utf-8")).items()}
        self.phones = sorted((t for t in self.vocab.values() if not t.startswith("<")), key=len, reverse=True)
        self.features = Wav2Vec2FeatureExtractor.from_pretrained(folder)
        self.model = Wav2Vec2ForCTC.from_pretrained(folder).to(DEVICE).eval()
        self.espeak = Tokenizer()
        self.utmos = torch.hub.load(str(UTMOS_REPO), "utmos22_strong", source="local", trust_repo=True).to(DEVICE).eval()

    def heard_phones(self, audio16k):
        inputs = self.features(audio16k, sampling_rate=16000, return_tensors="pt").input_values.to(DEVICE)
        with torch.inference_mode():
            ids = self.model(inputs).logits[0].argmax(-1).tolist()
        phones, last = [], None
        for i in ids:
            if i != last and not self.vocab[i].startswith("<"):
                phones.append(self.vocab[i])
            last = i
        return phones

    def expected(self, text):
        """[(word, [sound groups])] for each word of the text, as the voice was asked to say it."""
        from speech_text import normalize
        text = normalize(text, "hi" if re.search(r"[\u0900-\u097F]", text) else "en-us")
        words = []
        for w in re.findall(r"[A-Za-z][A-Za-z'\-]*|[ऀ-ॣ०-ॿ]+|\d+", text):  # not the danda
            lang = "hi" if re.search(r"[ऀ-ॿ]", w) or (w.isdigit() and re.search(r"[ऀ-ॿ]", text)) else "en-us"
            ipa = re.sub(r"\([a-z\-]+\)", "", self.espeak.phonemize(w, lang))
            groups = [g for ch in ipa for g in coarse(ch)]
            if groups:
                words.append((w, groups))
        return words

    def mos(self, audio16k):
        with torch.inference_mode():
            return float(self.utmos(torch.from_numpy(audio16k).float().unsqueeze(0).to(DEVICE), 16000).item())

    def check(self, wav_path, text):
        audio, sr = sf.read(wav_path, dtype="float32", always_2d=True)
        audio = audio.mean(1)
        if sr != 16000:
            import torchaudio.functional as F
            audio = F.resample(torch.from_numpy(audio), sr, 16000).numpy()
        heard = [g for p in self.heard_phones(audio) for g in coarse(p)]
        words = self.expected(text)
        want = [(g, wi) for wi, (_, groups) in enumerate(words) for g in groups]
        matched = align(want, heard)
        report = []
        for wi, (word, groups) in enumerate(words):
            got = sum(1 for (g, w), ok in zip(want, matched) if w == wi and ok)
            share = got / len(groups)
            if share < UNCLEAR_BELOW and len(groups) >= MIN_FLAG_SOUNDS:
                report.append((word, "skipped" if got == 0 else "unclear", share))
        clarity = sum(matched) / max(len(want), 1)
        extra = max(0, len(heard) - sum(matched)) / max(len(want), 1)
        return clarity, extra, self.mos(audio), report


def align(want, heard):
    """Edit-distance alignment; for each wanted sound, whether it was heard (same group)."""
    n, m = len(want), len(heard)
    cost = np.zeros((n + 1, m + 1), np.int32)
    cost[:, 0] = np.arange(n + 1)
    cost[0, :] = np.arange(m + 1)
    for i in range(1, n + 1):
        for j in range(1, m + 1):
            same = want[i - 1][0] == heard[j - 1]
            cost[i, j] = min(cost[i - 1, j - 1] + (0 if same else 1), cost[i - 1, j] + 1, cost[i, j - 1] + 1)
    ok = [False] * n
    i, j = n, m
    while i > 0 and j > 0:
        same = want[i - 1][0] == heard[j - 1]
        if cost[i, j] == cost[i - 1, j - 1] + (0 if same else 1):
            ok[i - 1] = same
            i, j = i - 1, j - 1
        elif cost[i, j] == cost[i - 1, j] + 1:
            i -= 1
        else:
            j -= 1
    return ok


def load_items(folder: Path, everything: bool):
    manifest = folder / "manifest.json"
    if manifest.exists():
        return [(folder / f, t) for f, t in json.load(open(manifest, encoding="utf-8")).items()]
    entries = [json.loads(line) for line in open(folder / "index.jsonl", encoding="utf-8") if line.strip()]
    flagged = {e["flagged"] for e in entries if e.get("flagged")}
    return [(folder / e["file"], e["text"]) for e in entries
            if e.get("file") and (everything or not flagged or e["reply"] in flagged)]


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    folder = Path(args[0]) if args else max(REVIEW_ROOT.iterdir(), key=lambda p: p.name)
    items = load_items(folder, "--all" in sys.argv)
    checker = Checker()
    lines, clarities, moses = [], [], []
    for wav, text in items:
        clarity, extra, mos, problems = checker.check(wav, text)
        clarities.append(clarity)
        moses.append(mos)
        lines.append(f"{wav.name}  clarity {clarity:.0%}  extra sounds {extra:.0%}  naturalness {mos:.2f}/5\n  {text}")
        for word, kind, share in problems:
            lines.append(f"    {kind}: {word}  ({share:.0%} of its sounds heard)")
    lines.append(f"\n{len(items)} sentences: clarity {np.mean(clarities):.0%}, naturalness {np.mean(moses):.2f}/5 on average")
    report = "\n".join(lines)
    print(report)
    (folder / "report.txt").write_text(report, encoding="utf-8")


if __name__ == "__main__":
    main()
