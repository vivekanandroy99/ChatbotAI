"""
Local offline sidecar for the Unity voice companion (localhost only - no
external network calls at runtime). Serves:
  /speak      - text-to-speech as one WAV: Kokoro-82M (English + Hindi voices, ONNX
                on GPU), or the optional Hindi engines Veena / IndicF5.
  /speak_stream - the same as raw audio while it's generated (what Unity plays).
  /retrieve   - search over each avatar's document knowledge base (knowledge.py).
  /vocabulary - distinctive terms from an avatar's documents, used to prime
                speech recognition for product/brand names.

Run: venv\\Scripts\\python.exe server.py
"""


def progress(fraction, stage):
    # Parsed by Unity's TTSProcessManager to drive the startup loading bar.
    print(f"PROGRESS|{fraction:.2f}|{stage}", flush=True)


def run_at_full_speed():
    """Windows runs windowless background processes like this one (started by Unity)
    in power-saving mode, on slower cores. Veena does per-sound-token work on the CPU
    and fell behind real time that way (1.1x vs 0.7x when run from a console). This
    opts this process - only this process - out of that, and raises its priority a step."""
    import sys
    if sys.platform != "win32":
        return
    import ctypes
    from ctypes import wintypes

    class PowerThrottlingState(ctypes.Structure):
        _fields_ = [("Version", wintypes.ULONG), ("ControlMask", wintypes.ULONG), ("StateMask", wintypes.ULONG)]

    kernel32 = ctypes.windll.kernel32
    process = kernel32.GetCurrentProcess()
    # ProcessPowerThrottling (4): take control of execution-speed throttling (1) and turn it off (0).
    state = PowerThrottlingState(1, 1, 0)
    kernel32.SetProcessInformation(process, 4, ctypes.byref(state), ctypes.sizeof(state))
    kernel32.SetPriorityClass(process, 0x8000)  # ABOVE_NORMAL_PRIORITY_CLASS


run_at_full_speed()
# Before the heavy imports below, which alone take several seconds.
progress(0.02, "Starting voice & knowledge engine")

import io
import logging
import os
import re
import struct
import threading
from pathlib import Path

import numpy as np
import soundfile as sf
import torch  # must load before onnxruntime: it provides the CUDA 12 DLLs onnxruntime-gpu runs on
import onnxruntime as ort
from fastapi import FastAPI, HTTPException
from fastapi.responses import Response, StreamingResponse
from kokoro_onnx import Kokoro
from pydantic import BaseModel

import indicf5_engine
import veena_engine
from devanagari_speech import to_devanagari_speech
from hinglish import colloquial, restore_english
from knowledge import KnowledgeBase
from speech_text import normalize

logging.basicConfig(level=logging.WARNING)
log = logging.getLogger("tts_server")
log.setLevel(logging.INFO)
logging.getLogger("knowledge").setLevel(logging.INFO)

DEVICE = "cuda" if torch.cuda.is_available() else "cpu"
MODELS_DIR = Path(__file__).resolve().parent / "models"
KNOWLEDGE_ROOT = Path(
    os.environ.get("KNOWLEDGE_ROOT")
    or Path(__file__).resolve().parent.parent / "Assets" / "StreamingAssets" / "Knowledge"
)
# Kokoro voice IDs start with their language: a = American English,
# b = British English, h = Hindi.
VOICE_LANGUAGES = {"a": "en-us", "b": "en-gb", "h": "hi"}

progress(0.10, "Loading knowledge model")
knowledge = KnowledgeBase(KNOWLEDGE_ROOT, DEVICE, use_reranker=os.environ.get("RERANKER_OFF") != "1")  # Unity: menu > AI models
progress(0.60, "Reading your documents")
knowledge.ingest_all()

progress(0.75, "Loading voice model")
# HEURISTIC: every sentence has a new length, and the default (EXHAUSTIVE) would
# benchmark cuDNN algorithms again for each one.
providers = (
    [("CUDAExecutionProvider", {"cudnn_conv_algo_search": "HEURISTIC"}), "CPUExecutionProvider"]
    if DEVICE == "cuda"
    else ["CPUExecutionProvider"]
)
# The model file is picked in Unity (TTSProcessManager's voice models).
KOKORO_MODEL = os.environ.get("KOKORO_MODEL") or "kokoro-v1.0.onnx"
if not (MODELS_DIR / KOKORO_MODEL).is_file():
    log.warning("Voice model %s not found - using kokoro-v1.0.onnx.", KOKORO_MODEL)
    KOKORO_MODEL = "kokoro-v1.0.onnx"


def session_options():
    """Kokoro runs on the GPU; onnxruntime's pool of CPU threads only spins, competing with
    Veena's per-token CPU work (Veena slowed from 0.73x to 1.04x real time). One thread,
    no spinning - Kokoro itself got faster too (0.27 s vs 0.38 s per sentence)."""
    options = ort.SessionOptions()
    options.intra_op_num_threads = 1
    options.add_session_config_entry("session.intra_op.allow_spinning", "0")
    options.add_session_config_entry("session.inter_op.allow_spinning", "0")
    return options


tts = Kokoro.from_session(
    ort.InferenceSession(str(MODELS_DIR / KOKORO_MODEL), session_options(), providers=providers),
    str(MODELS_DIR / "voices-v1.0.bin"),
)
VOICES = set(tts.get_voices())

# Kokoro v1.1 (a Chinese-focused model) adds three English voices of its own. Optional
# (download_voice.py kokoro11); loaded the first time one of them is used.
KOKORO11_VOICES = {"af_maple", "af_sol", "bf_vale"}
_tts11 = None


def kokoro11():
    global _tts11
    if _tts11 is None:
        model, voices, config = (MODELS_DIR / n for n in ("kokoro-v1.1-zh.onnx", "voices-v1.1-zh.bin", "kokoro-v1.1-zh.config.json"))
        if not (model.is_file() and voices.is_file() and config.is_file()):
            raise RuntimeError("Kokoro v1.1 isn't downloaded")
        options = session_options()
        options.log_severity_level = 3  # its graph logs harmless CUDA warnings on every load
        import json
        _tts11 = Kokoro.from_session(ort.InferenceSession(str(model), options, providers=providers), str(voices),
                                     vocab_config=json.loads(config.read_text(encoding="utf-8")))
        log.info("Kokoro v1.1 voices loaded.")
    return _tts11


# espeak-ng (Kokoro's pronunciation step) keeps global state - one request at a time.
tts_lock = threading.Lock()
# First run compiles GPU kernels; pay that now rather than on the first real reply.
tts.create("Ready.", voice="af_heart", lang="en-us")
log.info("Voice model loaded on %s.", tts.sess.get_providers()[0])
progress(0.95, "Starting server")

app = FastAPI()




LATIN_RUN = re.compile(r"[A-Za-z][A-Za-z0-9'&\-]*(?:[ \t]+[A-Za-z][A-Za-z0-9'&\-]*)*")


def phonemize_hinglish(text: str) -> str:
    """Hinglish replies keep English words in Latin script. Phonemizing the
    whole reply as Hindi would read those words with Hindi letter rules, so
    each English stretch is phonemized as English and the rest as Hindi - the
    Hindi voice then speaks both, giving natural Indian-English pronunciation."""
    parts, last = [], 0
    for match in LATIN_RUN.finditer(text):
        if match.start() > last:
            parts.append((text[last:match.start()], "hi"))
        parts.append((match.group(), "en-us"))
        last = match.end()
    if last < len(text):
        parts.append((text[last:], "hi"))
    phonemes = (tts.tokenizer.phonemize(chunk, lang) for chunk, lang in parts if chunk.strip())
    return " ".join(p for p in phonemes if p)


class SpeakRequest(BaseModel):
    text: str
    voice: str = "af_heart"
    speed: float = 1.0


class RetrieveRequest(BaseModel):
    avatar: str
    text: str
    top_k: int = 4
    rerank: bool = True
    rank_text: str = ""  # the question as searched for ranking only (gate scores use text)


@app.get("/health")
def health():
    return {"status": "ok"}


# Optional engines switched off in Unity (kept on disk, not used): never loaded, and
# their voices speak with a Kokoro voice of the same gender instead.
ENGINES_OFF = {e for e in os.environ.get("VOICE_ENGINES_OFF", "").split(",") if e}

# Svara, AI4Bharat Indic-TTS and Piper were removed in v5 (2026-10-07): their voices -> the same-gender Veena voice
# (Unity's VoiceCatalog.Current does the same for saved settings; this catches anything older).
RETIRED_VOICES = {
    "svara_hindi_female": "veena_kavya", "svara_english_female": "veena_kavya",
    "svara_hindi_male": "veena_vinaya", "svara_english_male": "am_michael",
    "indictts_female": "veena_kavya", "indictts_male": "veena_vinaya",
    "piper_priyamvada": "veena_kavya", "piper_rohan": "veena_vinaya", "piper_pratham": "veena_vinaya",
}


def _engine_of(voice):
    if voice in veena_engine.VOICES:
        return "veena"
    if voice in KOKORO11_VOICES:
        return "kokoro11"
    if voice.startswith("indicf5"):
        return "indicf5"
    return "kokoro"


def _kokoro_stand_in(voice, text):
    """Same-gender Kokoro voice in the reply's language."""
    female = True  # Kokoro v1.1's three English voices are all female
    if voice in veena_engine.VOICES:
        female = veena_engine.VOICES[voice][1]
    if voice.startswith("indicf5"):
        female = indicf5_engine.voices().get(voice, {}).get("female", True)
    if re.search(r"[\u0900-\u097F]", text):
        return "hf_alpha" if female else "hm_omega"
    return "af_heart" if female else "am_michael"


def _speech(req: SpeakRequest):
    """-> (sample rate, iterator of float32 audio pieces). Veena yields pieces as it
    generates them; the other engines yield the whole sentence at once."""
    speed = min(max(req.speed, 0.5), 2.0)
    voice = RETIRED_VOICES.get(req.voice, req.voice)
    if _engine_of(voice) in ENGINES_OFF:
        voice = _kokoro_stand_in(voice, req.text)
    if voice.startswith("veena"):
        if voice not in veena_engine.VOICES:
            raise HTTPException(status_code=400, detail=f"unknown voice {voice!r}")
        try:
            pieces = veena_engine.stream(_veena_text(req.text), voice)  # (Veena has no speed control)
            first = next(pieces, None)  # surfaces a load failure here, while Kokoro can still step in
            if first is None:
                return veena_engine.SAMPLE_RATE, [np.zeros(0, dtype=np.float32)]
            return veena_engine.SAMPLE_RATE, _chain(first, pieces)
        except RuntimeError as e:
            voice = "hf_alpha" if veena_engine.VOICES[voice][1] else "hm_omega"
            log.warning("Veena unavailable (%s) - speaking with %s.", e, voice)
    audio, sample_rate = _synthesize(req.text, voice, speed)
    return sample_rate, [audio]


def _chain(first, rest):
    yield first
    yield from rest


def _veena_text(text):
    text = normalize(text, "hi" if re.search(r"[\u0900-\u097F]", text) else "en-us")
    veena_engine._load()
    if veena_engine._model is None:
        return text
    with tts_lock:  # espeak (used to respell rare English words) is not thread-safe
        return veena_engine.prepare(text, tts.tokenizer.phonemize)


@app.post("/speak")
def speak(req: SpeakRequest):
    sample_rate, pieces = _speech(req)
    return _wav(np.concatenate(list(pieces)), sample_rate)


@app.post("/speak_stream")
def speak_stream(req: SpeakRequest):
    """A 4-byte little-endian sample rate, then 16-bit mono PCM as it's generated."""
    sample_rate, pieces = _speech(req)

    def body():
        yield struct.pack("<I", sample_rate)
        for audio in pieces:
            yield (np.clip(np.asarray(audio, dtype=np.float32), -1, 1) * 32767).astype("<i2").tobytes()

    return StreamingResponse(body(), media_type="application/octet-stream")


def _synthesize(text, voice, speed):
    """Kokoro or IndicF5, one whole sentence -> (audio, sample rate)."""
    if voice.startswith("indicf5"):
        engine_voices = indicf5_engine.voices()
        if voice not in engine_voices:
            raise HTTPException(status_code=400, detail=f"unknown voice {voice!r}")
        try:
            with tts_lock:  # espeak (used to respell English words) is not thread-safe
                spoken = to_devanagari_speech(normalize(text, "hi"), tts.tokenizer.phonemize)
            return indicf5_engine.synthesize(spoken, voice, speed)
        except RuntimeError as e:
            # Not downloaded / failed to load: same-gender Kokoro Hindi voice instead.
            voice = "hf_alpha" if engine_voices[voice]["female"] else "hm_omega"
            log.warning("IndicF5 unavailable (%s) - speaking with %s.", e, voice)

    engine = tts
    if voice in KOKORO11_VOICES:
        try:
            with tts_lock:
                engine = kokoro11()
        except Exception as e:
            log.warning("Kokoro v1.1 unavailable (%s) - speaking with af_heart.", e)
            voice = "af_heart"
    lang = VOICE_LANGUAGES.get(voice[:1])
    if lang is None or (voice not in VOICES and engine is tts):
        raise HTTPException(status_code=400, detail=f"unknown voice {voice!r}")

    text = normalize(text, lang)
    with tts_lock:
        if lang == "hi":
            return tts.create(phonemize_hinglish(text), voice=voice, lang=lang, speed=speed, is_phonemes=True)
        return engine.create(text, voice=voice, lang=lang, speed=speed)


def _wav(audio, sample_rate):
    buf = io.BytesIO()
    sf.write(buf, np.asarray(audio, dtype=np.float32), sample_rate, format="WAV", subtype="PCM_16")
    return Response(content=buf.getvalue(), media_type="audio/wav")


@app.post("/retrieve")
def retrieve(req: RetrieveRequest):
    try:
        best_score, best_sentence, chunks = knowledge.retrieve(req.avatar, req.text, req.top_k, req.rerank, req.rank_text)
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))
    return {"best_score": best_score, "best_sentence_score": best_sentence, "chunks": chunks}


class RestoreEnglishRequest(BaseModel):
    english: str
    hinglish: str
    names_only: bool = False


@app.post("/restore_english")
def restore_english_words(req: RestoreEnglishRequest):
    """English words the translator wrote in Devanagari go back to English letters -
    the model garbles them that way (commercial -> कॉर्पोरेट) and the Hindi voice then
    mispronounces them. Real Hindi translations are left alone.
    names_only (open conversation): only names - capitalised words that don't start a
    sentence - may come back; everyday words ("cooking", "know") matched Hindi verbs
    (कहूँगा, कहना) by their consonants."""
    english = req.english
    if req.names_only:
        english = re.sub(r"(^|[.!?]\s+)\S+", r"\1", english)  # sentence-initial words are capitalised anyway
    protect = (lambda w: w[:1].isupper()) if req.names_only else None
    text, swaps = restore_english(english, req.hinglish, protect)
    if swaps:
        log.info("Kept in English: %s", ", ".join(f"{a}->{b}" for a, b in swaps))
    text, swaps = colloquial(text)
    if swaps:
        log.info("Everyday Hindi: %s", ", ".join(f"{a}->{b}" for a, b in swaps))
    return {"text": text}


class WarmVoiceRequest(BaseModel):
    voice: str


@app.post("/warm_voice")
def warm_voice(req: WarmVoiceRequest):
    """Load an optional voice engine in the background, before the first reply needs it."""
    voice = RETIRED_VOICES.get(req.voice, req.voice)
    if _engine_of(voice) in ENGINES_OFF:
        return {"warming": False}
    if voice.startswith("indicf5") and voice in indicf5_engine.voices():
        threading.Thread(target=indicf5_engine.synthesize, args=("नमस्ते।", voice), daemon=True).start()
        return {"warming": True}
    if voice in veena_engine.VOICES:
        threading.Thread(target=veena_engine.warm_up, daemon=True).start()
        return {"warming": True}
    if voice in KOKORO11_VOICES:
        threading.Thread(target=_synthesize, args=("Ready.", voice, 1.0), daemon=True).start()
        return {"warming": True}
    return {"warming": False}


@app.get("/voice_engines")
def voice_engines():
    """Optional voice engines and whether they're downloaded."""
    return {
        "indicf5": {"downloaded": indicf5_engine.is_downloaded(), "voices": list(indicf5_engine.voices())},
        "veena": {"downloaded": veena_engine.is_downloaded(), "voices": list(veena_engine.VOICES)},
        "switched_off": sorted(ENGINES_OFF),
    }


@app.get("/topics")
def topics(avatar: str, limit: int = 3):
    try:
        return {"words": knowledge.topic_names(avatar, limit)}
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))


@app.get("/vocabulary")
def vocabulary(avatar: str, limit: int = 40):
    try:
        return {"words": knowledge.vocabulary(avatar, limit)}
    except ValueError as e:
        raise HTTPException(status_code=400, detail=str(e))


if __name__ == "__main__":
    import uvicorn

    uvicorn.run(app, host="127.0.0.1", port=8765)
