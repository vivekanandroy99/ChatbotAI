"""Makes the sustained-sound clips uLipSync learns a voice from (tools/calibrate_lipsync.cs):
Assets/Avatar LipSync/Calibration/<voice>_<sound>.wav for the vowels A I U E O and the consonant
groups M (m/b/p), F (f/v), S (s/sh), spoken by the running voice server (start the app once so
the sidecar is up). Veena voices get Devanagari, Kokoro English voices get English spellings.
Prints each clip's length and a loudness envelope so a clip that came out wrong (cut short,
mumbled) is easy to spot.

    TTSServer/venv/Scripts/python.exe tools/make_lipsync_calibration.py veena_vinaya am_michael
"""
import json
import os
import sys
import urllib.request

import numpy as np
import soundfile as sf

SERVER = "http://127.0.0.1:8765/speak"
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Avatar LipSync", "Calibration")

HINDI = {
    "A": "आआआआआआआआ", "I": "ईईईईईईईई", "U": "ऊऊऊऊऊऊऊऊ", "E": "एएएएएएएए", "O": "ओओओओओओओओ",
    "M": "म्म्म्म्म्म्म्म्", "F": "फ़्फ़्फ़्फ़्फ़्फ़्फ़्", "S": "स्स्स्स्स्स्स्स्",
}
ENGLISH = {
    "A": "Aaaaaaaaaaah.", "I": "Eeeeeeeeeeee.", "U": "Oooooooooooo.", "E": "Ehhhhhhhhhhh.", "O": "Ohhhhhhhhhhh.",
    "M": "Mmmmmmmmmmmm.", "F": "Ffffffffffff.", "S": "Ssssssssssss.",
}


def speak(text, voice):
    body = json.dumps({"text": text, "voice": voice, "speed": 1}).encode("utf-8")
    req = urllib.request.Request(SERVER, body, {"Content-Type": "application/json; charset=utf-8"})
    with urllib.request.urlopen(req, timeout=180) as r:
        return r.read()


def envelope(path):
    a, sr = sf.read(path)
    if a.ndim > 1:
        a = a.mean(axis=1)
    n = sr // 20
    fr = a[: len(a) // n * n].reshape(-1, n)
    rms = np.sqrt((fr ** 2).mean(1))
    env = "".join(" .:-=+*#%@"[min(9, int(r * 60))] for r in rms)
    return f"{len(a) / sr:4.1f}s voiced {np.mean(rms > 0.03) * 100:3.0f}%  {env}"


def main(voices):
    os.makedirs(OUT, exist_ok=True)
    for voice in voices:
        texts = HINDI if voice.startswith(("veena_", "hf_", "hm_", "indicf5", "svara_hindi", "indictts_", "piper_")) else ENGLISH
        for sound, text in texts.items():
            path = os.path.join(OUT, f"{voice}_{sound}.wav")
            with open(path, "wb") as f:
                f.write(speak(text, voice))
            print(f"{voice}_{sound}: {envelope(path)}")


if __name__ == "__main__":
    main(sys.argv[1:] or ["veena_vinaya"])
