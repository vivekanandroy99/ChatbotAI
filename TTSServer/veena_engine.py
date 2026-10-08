"""
Optional Hindi voice engine: Veena by Maya Research (Apache 2.0,
https://huggingface.co/maya-research/Veena). Four studio voices with a natural
Indian accent that read Hinglish natively (English words in English letters).
A 3B Llama model writes SNAC audio codes; the GGUF build runs on llama.cpp
(llama-cpp-python, CUDA) and SNAC turns the codes into 24 kHz audio.

It generates at about real time, so audio is streamed: the first ~0.1 s is
ready ~0.4 s after the request and the rest follows while it plays.

Pronunciation: Veena says ordinary English words well in its own Indian accent,
but drops sounds from made-up names ("Altcore" came out "Allcore" 4/4). Capitalised
names its vocabulary doesn't know whole are therefore respelled - in Hindi replies in
Devanagari from espeak's English pronunciation (the dictionary Kokoro speaks from,
"Altcore" 4/4 then); in English replies split into the known parts ("Alt-core").
Lowercase English words are left alone: respelling those too (American espeak
vowels, "holograms" -> हालग्रैम्ज़) made ordinary words sound wrong. Numbers in
Hindi replies become Hindi number words.

Voice consistency: the speaker tag alone doesn't hold the voice - in testing,
"kavya" came out male-pitched in whole sentences or switched female -> male ->
female mid-sentence in 7-13 of 24 sentences. Every sentence is therefore spoken
as the continuation of a fixed, verified sample of that voice (an "anchor":
voices/veena/anchors.json, listenable as voices/veena/<voice>.wav): 0 of 24 then.
Anchors are picked by pitch (the whole clip in the voice's range) with
`python veena_engine.py anchors [voice ...]`; rerun it to pick a different take.

Downloaded on demand (download_voice.py veena); loaded the first time one of its
voices is used. The model file is picked in Unity (TTSProcessManager's voice
models) and passed in as VEENA_MODEL.
"""
import json
import logging
import os
import queue
import re
import threading
import time
from pathlib import Path

import numpy as np

from devanagari_speech import hindi_number, ipa_to_devanagari

log = logging.getLogger("tts_server")

MODELS_DIR = Path(__file__).resolve().parent / "models"
ANCHORS_DIR = Path(__file__).resolve().parent / "voices" / "veena"
ANCHOR_TEXT = "नमस्ते, मैं आपकी मदद के लिए यहाँ हूँ। आप मुझसे कुछ भी पूछ सकते हैं।"
SAMPLE_RATE = 24000
SNAC_REPO = "hubertsiuzdak/snac_24khz"
# voice id -> (Veena speaker, female). ("agastya" is left out: no take of it stayed
# in a male voice for a whole sentence - 0 of 60.)
VOICES = {
    "veena_kavya": ("kavya", True),
    "veena_maitri": ("maitri", True),
    "veena_vinaya": ("vinaya", False),
}

# Veena's special tokens (Llama 3 vocabulary + its own).
START_OF_SPEECH, END_OF_SPEECH = 128257, 128258
START_OF_HUMAN, END_OF_HUMAN, START_OF_AI, END_OF_AI = 128259, 128260, 128261, 128262
AUDIO_BASE = 128266
FRAME_TOKENS = 7       # one SNAC frame = 1 + 2 + 4 codes over its three levels
FRAME_SAMPLES = 2048   # ~85 ms of audio per frame
# Sampling: the model card uses 0.4; 0.25 articulated names more reliably in testing.
TEMPERATURE, TOP_P, REPEAT_PENALTY = 0.25, 0.9, 1.1
# Streaming decode: each piece is decoded with this many frames of context on each
# side, so pieces join without clicks. The first piece is short to start speaking sooner.
CONTEXT_FRAMES, FIRST_PIECE_FRAMES, PIECE_FRAMES = 3, 2, 4
N_CTX = 3072  # the anchor takes ~500 of these
MIN_FRAMES_PER_CHAR = 0.45  # ~0.04 s of speech per character of text at least
MAX_FRAMES_PER_CHAR = 1.3   # ~0.11 s per character at most

_model = None  # (llama, snac)
_anchors = None  # voice id -> prompt tokens of its anchor turn
_load_error = None
_lock = threading.Lock()       # one generation at a time
_load_lock = threading.Lock()


def model_path() -> Path | None:
    """The selected model file (VEENA_MODEL), else any Veena .gguf in models/."""
    chosen = os.environ.get("VEENA_MODEL")
    if chosen and (MODELS_DIR / chosen).is_file():
        return MODELS_DIR / chosen
    found = sorted(MODELS_DIR.glob("*[Vv]eena*.gguf"))
    return found[0] if found else None


def is_downloaded() -> bool:
    return model_path() is not None


def _load():
    global _model, _load_error
    with _load_lock:
        if _model is not None or _load_error is not None:
            return
        path = model_path()
        if path is None:
            _load_error = "not downloaded - use the Download button on the avatar's Hindi voice"
            return
        try:
            import torch
            # llama.cpp's CUDA build needs the CUDA runtime DLLs that ship with torch.
            if hasattr(os, "add_dll_directory"):  # Windows only
                os.add_dll_directory(os.path.join(os.path.dirname(torch.__file__), "lib"))
            from llama_cpp import Llama
            from snac import SNAC

            llama = Llama(str(path), n_gpu_layers=-1, n_ctx=N_CTX, flash_attn=True, verbose=False)
            snac = SNAC.from_pretrained(SNAC_REPO).eval().to("cuda" if torch.cuda.is_available() else "cpu")
            _model = (llama, snac)
            log.info("Veena voice engine loaded (%s).", path.name)
        except Exception as e:
            _load_error = f"{type(e).__name__}: {e}"
            log.exception("Veena failed to load - Hindi falls back to Kokoro.")


_WORD = re.compile(r"[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]*")
_NUMBER = re.compile(r"\d[\d,]*")


_DEVANAGARI = re.compile(r"[\u0900-\u097F]")


def prepare(text: str, phonemize) -> str:
    """Spoken form of a reply (see the module notes). phonemize(text, lang) -> IPA
    (Kokoro's espeak tokenizer). Call after _load()."""
    llama = _model[0]
    hindi = bool(_DEVANAGARI.search(text))

    def pieces(w):
        return [llama.detokenize([t]).decode("utf-8", "ignore").strip()
                for t in llama.tokenize((" " + w).encode(), add_bos=False)]

    def word(m):
        w = m.group()
        if not w[0].isupper() or w.isupper():
            return w  # an ordinary English word or an acronym (UPI) - Veena's own reading is good
        parts = pieces(w)
        if len(parts) == 1:
            return w  # a name the model knows whole
        if not hindi:
            # "Altcore" -> "Alt-core" when it splits into two real-looking parts; else as is.
            return "-".join(parts) if len(parts) == 2 and all(len(p) >= 3 for p in parts) else w
        ipa = re.sub(r"\([a-z\-]+\)", "", phonemize(w, "en-us")).strip()
        return ipa_to_devanagari(ipa) or w

    if not hindi:
        return _WORD.sub(word, text)

    def number(m):
        digits = m.group().replace(",", "")
        return hindi_number(int(digits)) if len(digits) <= 9 else m.group()

    return _NUMBER.sub(number, _WORD.sub(word, text))


def _decode(frames, snac):
    """frames: list of 7-code frames -> float32 audio (len(frames) * FRAME_SAMPLES)."""
    import torch
    levels = [[], [], []]
    for c in frames:
        levels[0].append(c[0])
        levels[1] += [c[1], c[4]]
        levels[2] += [c[2], c[3], c[5], c[6]]
    device = next(snac.parameters()).device
    codes = [torch.tensor(l, dtype=torch.int32, device=device).unsqueeze(0) for l in levels]
    with torch.inference_mode():
        return snac.decode(codes).squeeze().float().cpu().numpy()


def _turn(text, speaker):
    prompt = _model[0].tokenize(f"<spk_{speaker}> {text}".encode(), add_bos=False, special=True)
    return [START_OF_HUMAN, *prompt, END_OF_HUMAN, START_OF_AI, START_OF_SPEECH]


# How a sentence is tied to its voice's anchor:
#   "turn": the anchor is a finished previous turn (cached, so only the new text is processed);
#   "continue": one turn "<anchor text> <new text>" whose speech starts with the anchor's audio, and
#   the model carries on from there (the usual voice-cloning prompt for this kind of model).
# "continue" won clearly on 5 real Hinglish replies x2 (words Whisper heard back 75% vs 45%, English
# words 55% vs 21%, no voice switches either way): as a separate turn the model sometimes read the
# anchor's sentence out again, mumbled, or stopped early. Its prompt can't be cached (~500 tokens
# processed per piece, a fraction of a second).
ANCHOR_STYLE = "continue"


def _anchor(voice_id):
    """(text, audio codes) of the voice's anchor, or None if it has none."""
    global _anchors
    if _anchors is None:
        _anchors = {}
        path = ANCHORS_DIR / "anchors.json"
        if path.is_file():
            for vid, a in json.loads(path.read_text(encoding="utf-8")).items():
                if vid in VOICES:
                    _anchors[vid] = (a["text"], a["codes"])
    if voice_id not in _anchors:
        log.warning("Veena voice %s has no anchor - its voice may drift (run: python veena_engine.py anchors).", voice_id)
    return _anchors.get(voice_id)


def _prompt(text, voice_id):
    speaker = VOICES[voice_id][0]
    anchor = _anchor(voice_id)
    if anchor is None:
        return _turn(text, speaker)
    anchor_text, anchor_codes = anchor
    if ANCHOR_STYLE == "continue":
        return _turn(f"{anchor_text} {text}", speaker) + anchor_codes
    return _turn(anchor_text, speaker) + anchor_codes + [END_OF_SPEECH, END_OF_AI] + _turn(text, speaker)


def _generate(text, voice_id, stop):
    """Yields float32 audio pieces as the model writes them."""
    from llama_cpp import LogitsProcessorList
    llama, snac = _model
    tokens = _prompt(text, voice_id)
    # Speech runs ~0.85 frames per character; well past that it's babbling (or reading the anchor's text again).
    budget = min(N_CTX - len(tokens) - 8, int(len(text) * MAX_FRAMES_PER_CHAR + 15) * FRAME_TOKENS)

    frames, frame, emitted, count = [], [], 0, 0

    # Continuing from the anchor, the model sometimes ends a sentence after 0.3 s (it
    # copies the anchor's own ending): ~30% of sentences in testing. Ending isn't
    # allowed until there's been time to say the text (half the fastest pace seen).
    min_frames = int(len(text) * MIN_FRAMES_PER_CHAR)

    def no_early_end(_, scores):
        if len(frames) < min_frames:
            scores[END_OF_SPEECH] = scores[END_OF_AI] = -np.inf
        return scores

    for tok in llama.generate(tokens, temp=TEMPERATURE, top_p=TOP_P, repeat_penalty=REPEAT_PENALTY, reset=True,
                              logits_processor=LogitsProcessorList([no_early_end])):
        if stop.is_set() or tok in (END_OF_SPEECH, END_OF_AI):
            break
        count += 1
        if count > budget:
            log.warning("Veena ran past its length budget - cutting the sentence short.")
            break
        if not AUDIO_BASE <= tok < AUDIO_BASE + FRAME_TOKENS * 4096:
            continue
        code = tok - AUDIO_BASE - len(frame) * 4096
        if not 0 <= code < 4096:
            frame = []  # out of step with the 7-code pattern - drop the partial frame
            continue
        frame.append(code)
        if len(frame) < FRAME_TOKENS:
            continue
        frames.append(frame)
        frame = []

        piece = FIRST_PIECE_FRAMES if emitted == 0 else PIECE_FRAMES
        if len(frames) - emitted >= piece + CONTEXT_FRAMES:
            upto = len(frames) - CONTEXT_FRAMES
            start = max(0, emitted - CONTEXT_FRAMES)
            audio = _decode(frames[start:], snac)
            yield audio[(emitted - start) * FRAME_SAMPLES:(upto - start) * FRAME_SAMPLES]
            emitted = upto

    if frames and emitted < len(frames) and not stop.is_set():
        start = max(0, emitted - CONTEXT_FRAMES)
        yield _decode(frames[start:], snac)[(emitted - start) * FRAME_SAMPLES:]


def stream(text: str, voice_id: str):
    """Generator of float32 audio pieces at SAMPLE_RATE. RuntimeError if the engine
    isn't available. Generation runs on its own thread, so a listener that goes
    away (a reply interrupted) never leaves the engine locked."""
    _load()
    if _model is None:
        raise RuntimeError(_load_error)
    pieces, stop = queue.Queue(), threading.Event()

    def work():
        try:
            with _lock:
                start, samples = time.perf_counter(), 0
                for audio in _generate(text, voice_id, stop):
                    pieces.put(audio)
                    samples += len(audio)
                took = time.perf_counter() - start
                if samples:
                    log.info("Veena: %.1f s of speech in %.1f s (%.2fx real time): %s", samples / SAMPLE_RATE, took, took * SAMPLE_RATE / samples, text[:60])
        except Exception as e:
            log.exception("Veena generation failed.")
            pieces.put(e)
        finally:
            pieces.put(None)

    threading.Thread(target=work, daemon=True).start()
    try:
        first = True
        while True:
            item = pieces.get()
            if item is None:
                return
            if isinstance(item, Exception):
                raise item
            if first:  # soften the very first sample so playback doesn't start with a click
                ramp = min(len(item), 120)
                item = item.copy()
                item[:ramp] *= np.linspace(0, 1, ramp, dtype=np.float32)
                first = False
            yield item
    finally:
        stop.set()


def warm_up():
    try:
        for _ in stream("नमस्ते।", next(iter(VOICES))):
            pass
    except Exception as e:
        log.warning("Veena warm-up skipped: %s", e)


# --- Anchor making (offline tool) ---------------------------------------------------------

def _pitch_windows(audio, sr=SAMPLE_RATE, window_s=0.5):
    """Median pitch (Hz) of each half-second of voiced audio (YIN)."""
    frame, hop, lo, hi = 1024, 240, sr // 400, sr // 70
    f0 = []
    for i in range(0, len(audio) - frame - hi, hop):
        x = audio[i:i + frame + hi]
        if np.sqrt(np.mean(x[:frame] ** 2)) < 0.02:
            f0.append(0.0)
            continue
        d = np.array([np.sum((x[:frame] - x[t:t + frame]) ** 2) for t in range(1, hi)])
        cmnd = d * np.arange(1, hi) / np.maximum(np.cumsum(d), 1e-9)
        cand = np.where(cmnd[lo - 1:] < 0.15)[0]
        if len(cand) == 0:
            f0.append(0.0)
            continue
        t = cand[0] + lo - 1
        while t + 1 < len(cmnd) and cmnd[t + 1] < cmnd[t]:
            t += 1
        f0.append(sr / (t + 1))
    f0, n, out = np.array(f0), int(window_s * sr / hop), []
    for i in range(0, len(f0), n):
        w = f0[i:i + n]
        w = w[w > 0]
        if len(w) >= 10:
            out.append(float(np.median(w)))
    return out


def _steady(windows, female):
    """The whole clip in the voice's pitch range - no stretch in the other gender's range."""
    if len(windows) < 6:
        return False
    med = float(np.median(windows))
    if female:
        return min(windows) >= 160 and 180 <= med <= 270
    return max(windows) <= 175 and 90 <= med <= 145


def make_anchor(voice_id, attempts=60):
    """Generates takes of ANCHOR_TEXT until one is steady; saves it. Returns the seed used."""
    import soundfile as sf
    _load()
    if _model is None:
        raise RuntimeError(_load_error)
    llama, snac = _model
    speaker, female = VOICES[voice_id]
    for seed in range(1, attempts + 1):
        llama.set_seed(seed)
        codes = []
        for tok in llama.generate(_turn(ANCHOR_TEXT, speaker), temp=0.4, top_p=TOP_P, repeat_penalty=1.05, reset=True):
            if tok in (END_OF_SPEECH, END_OF_AI) or len(codes) > 1000:
                break
            if AUDIO_BASE <= tok < AUDIO_BASE + FRAME_TOKENS * 4096:
                codes.append(tok)
        codes = codes[:len(codes) // FRAME_TOKENS * FRAME_TOKENS]
        frames = [[codes[i + j] - AUDIO_BASE - j * 4096 for j in range(FRAME_TOKENS)] for i in range(0, len(codes), FRAME_TOKENS)]
        if not frames or any(not 0 <= c < 4096 for f in frames for c in f):
            continue
        audio = _decode(frames, snac)
        windows = _pitch_windows(audio)
        if not _steady(windows, female):
            continue
        ANCHORS_DIR.mkdir(parents=True, exist_ok=True)
        path = ANCHORS_DIR / "anchors.json"
        anchors = json.loads(path.read_text(encoding="utf-8")) if path.is_file() else {}
        anchors[voice_id] = {"text": ANCHOR_TEXT, "codes": codes, "seed": seed, "pitch_hz": round(float(np.median(windows)))}
        path.write_text(json.dumps(anchors, ensure_ascii=False), encoding="utf-8")
        sf.write(ANCHORS_DIR / f"{voice_id}.wav", audio, SAMPLE_RATE)
        global _anchors
        _anchors = None
        return seed, round(float(np.median(windows)))
    raise RuntimeError(f"no steady take of {voice_id} in {attempts} tries")


if __name__ == "__main__":
    import sys
    if len(sys.argv) >= 2 and sys.argv[1] == "anchors":
        for vid in sys.argv[2:] or VOICES:
            try:
                print(vid, "-> seed %d, %d Hz" % make_anchor(vid), flush=True)
            except RuntimeError as e:
                print(vid, "FAILED:", e, flush=True)
    else:
        print("usage: python veena_engine.py anchors [voice ...]")
