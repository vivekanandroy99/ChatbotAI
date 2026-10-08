"""
Optional Hindi voice engine: AI4Bharat IndicF5 (https://huggingface.co/ai4bharat/IndicF5).
Much more natural Hindi than Kokoro's four grade-C Hindi voices. It copies the
voice of a short reference clip (voices/indicf5/voices.json) and only reads
Indian scripts, so replies are respelled all-Devanagari first (devanagari_speech).

Downloaded on demand (download_voice.py, the Inspector's Download button) and
loaded the first time one of its voices is used.

Speed: the model's own wrapper runs full precision with 32 steps (4-9 s per
sentence here). This runs bfloat16 with 16 steps: ~1 s per sentence, and Whisper
reads the result back word for word, same as full precision.
"""
import json
import logging
import sys
import threading
import types
from pathlib import Path

import numpy as np

log = logging.getLogger("tts_server")

REPO = "ai4bharat/IndicF5"
SAMPLE_RATE = 24000
# Generation steps: 32 is the model's default; 16 takes half the time. (32 in
# bfloat16 trips an internal "t must be strictly increasing" check.)
STEPS = 16
VOICES_DIR = Path(__file__).resolve().parent / "voices" / "indicf5"

_model = None  # (ema_model, vocoder)
_load_error = None
_refs = {}  # voice id -> preprocessed (ref_audio, ref_text)
_lock = threading.Lock()


def voices() -> dict:
    """voice id -> {"file", "text", "female"}"""
    with open(VOICES_DIR / "voices.json", encoding="utf-8") as f:
        return {k: v for k, v in json.load(f).items() if not k.startswith("_")}


def is_downloaded() -> bool:
    from huggingface_hub import try_to_load_from_cache
    return isinstance(try_to_load_from_cache(REPO, "model.safetensors"), str)


def _load():
    global _model, _load_error
    if _model is not None or _load_error is not None:
        return
    if not is_downloaded():
        _load_error = "not downloaded - use the Download button on the avatar's Hindi voice"
        return
    try:
        # f5_tts's package __init__ imports its training code, which imports
        # `datasets` (and through it pandas, which Application Control blocks).
        # Speech generation never uses it, so a placeholder covers the import.
        stub = types.ModuleType("datasets")
        stub.Dataset = type("Dataset", (), {})
        stub.load_from_disk = lambda *a, **k: None
        sys.modules["datasets"] = stub
        import f5_tts.model  # noqa: F401
        del sys.modules["datasets"]

        # torchaudio 2.9+ reads audio through torchcodec (not installed); the
        # reference clips are plain WAVs that soundfile reads just as well.
        import soundfile as sf
        import torch
        import torchaudio

        def _read_wav(path, *args, **kwargs):
            data, sr = sf.read(path, dtype="float32", always_2d=True)
            return torch.from_numpy(data.T.copy()), sr
        torchaudio.load = _read_wav

        from huggingface_hub import hf_hub_download
        from safetensors.torch import load_file
        from transformers import AutoModel
        model = AutoModel.from_pretrained(REPO, trust_remote_code=True)
        # transformers renames checkpoint keys containing "gamma"/"beta" (an old
        # TensorFlow compatibility rule), so the text encoder's GRN layers and the
        # vocoder's layer scales come out untrained - the voice is then noise.
        # Loading the file directly keeps the original names.
        result = model.load_state_dict(load_file(hf_hub_download(REPO, "model.safetensors")), strict=False)
        if result.missing_keys:
            raise RuntimeError(f"{len(result.missing_keys)} weights missing, e.g. {result.missing_keys[0]}")
        device = "cuda" if torch.cuda.is_available() else "cpu"
        model = model.to(device).eval()
        # The wrapper torch.compile()s both parts; use the plain modules underneath.
        ema = getattr(model.ema_model, "_orig_mod", model.ema_model)
        vocoder = getattr(model.vocoder, "_orig_mod", model.vocoder)
        # bfloat16, not float16: in float16 the model overflows and outputs NaN (silence).
        if device == "cuda" and torch.cuda.is_bf16_supported():
            ema = ema.to(torch.bfloat16)
        _model = (ema, vocoder, device)
        log.info("IndicF5 Hindi voice engine loaded (%s, %d steps).", device, STEPS)
    except Exception as e:
        _load_error = f"{type(e).__name__}: {e}"
        log.exception("IndicF5 failed to load - Hindi falls back to Kokoro.")


def _reference(voice_id):
    if voice_id not in _refs:
        from f5_tts.infer.utils_infer import preprocess_ref_audio_text
        voice = voices()[voice_id]
        _refs[voice_id] = preprocess_ref_audio_text(str(VOICES_DIR / voice["file"]), voice["text"], show_info=lambda *a: None)
    return _refs[voice_id]


def synthesize(devanagari_text: str, voice_id: str, speed: float = 1.0):
    """Returns (float32 audio, sample rate); RuntimeError if the engine is unavailable."""
    with _lock:
        _load()
        if _model is None:
            raise RuntimeError(_load_error)
        from f5_tts.infer.utils_infer import infer_process
        ema, vocoder, device = _model
        ref_audio, ref_text = _reference(voice_id)
        audio, sample_rate, _ = infer_process(
            ref_audio, ref_text, devanagari_text, ema, vocoder,
            mel_spec_type="vocos", nfe_step=STEPS, speed=speed, device=device,
            show_info=lambda *a: None, progress=None,
        )
    audio = np.asarray(audio, dtype=np.float32)
    # Even loudness (-20 dBFS, like the model's own wrapper), but never boosted past
    # a safe peak - clipping the peaks is audible as harsh distortion.
    rms = float(np.sqrt(np.mean(audio ** 2))) if audio.size else 0.0
    peak = float(np.abs(audio).max()) if audio.size else 0.0
    if rms > 0 and peak > 0:
        audio = audio * min(10 ** (-20 / 20) / rms, 0.9 / peak)
    return audio, sample_rate


def warm_up():
    """Load and run once, so the first real reply doesn't pay for it."""
    try:
        synthesize("नमस्ते।", next(iter(voices())))
    except Exception as e:
        log.warning("IndicF5 warm-up skipped: %s", e)
