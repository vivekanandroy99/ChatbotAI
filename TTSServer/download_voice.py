"""
Downloads an optional voice engine for the sidecar. Run with the venv's python
(the Unity Inspector's Download button does this):

    python download_voice.py veena|kokoro11|indicf5

Prints "PROGRESS <0-1> <message>" lines and ends with "DONE" or "FAILED <reason>".
Needs the internet; the sidecar itself stays offline and loads from the cache.
"""
import os
import shutil
import sys
import urllib.request

os.environ.pop("HF_HUB_OFFLINE", None)
os.environ.setdefault("PYTHONIOENCODING", "utf-8")

MODELS_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "models")

# Each entry: a whole Hugging Face repo (into the HF cache), (repo, file) put in models/,
# or (url, file name) put in models/.
KOKORO_FILES = "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.1/"
ENGINES = {
    # Kokoro v1.1: three more English voices (af_maple, af_sol, bf_vale). Its vocabulary
    # file comes from the original model repo.
    "kokoro11": [(KOKORO_FILES + "kokoro-v1.1-zh.onnx", "kokoro-v1.1-zh.onnx"),
                 (KOKORO_FILES + "voices-v1.1-zh.bin", "voices-v1.1-zh.bin"),
                 ("hexgrad/Kokoro-82M-v1.1-zh", "config.json", "kokoro-v1.1-zh.config.json")],
    # Veena (Maya Research, Apache 2.0): Hindi/Hinglish voices. GGUF build for llama.cpp,
    # plus the SNAC audio decoder. Q4 keeps up with real time while the app runs; Q8\n    # (slightly better, too slow alongside the app here) and others are in the same repo.
    "veena": [("Mungert/Veena-GGUF", "Veena-q4_k_m.gguf"), "hubertsiuzdak/snac_24khz"],
    # IndicF5 (AI4Bharat): Hindi voice engine. Gated - accept the terms at
    # https://huggingface.co/ai4bharat/IndicF5 while logged in first.
    "indicf5": ["ai4bharat/IndicF5", "charactr/vocos-mel-24khz"],
}


def say(line):
    print(line, flush=True)


def main():
    if len(sys.argv) != 2 or sys.argv[1] not in ENGINES:
        say(f"FAILED usage: download_voice.py {'|'.join(ENGINES)}")
        return 1
    from huggingface_hub import hf_hub_download, snapshot_download
    from huggingface_hub.utils import GatedRepoError

    repos = ENGINES[sys.argv[1]]
    for i, item in enumerate(repos):
        repo = item[0] if isinstance(item, tuple) else item
        say(f"PROGRESS {i / len(repos):.2f} Downloading {repo} ...")
        try:
            if isinstance(item, tuple) and repo.startswith("https://"):
                urllib.request.urlretrieve(repo, os.path.join(MODELS_DIR, item[1]))
            elif isinstance(item, tuple) and len(item) == 3:
                shutil.copy(hf_hub_download(repo, item[1]), os.path.join(MODELS_DIR, item[2]))
            elif isinstance(item, tuple):
                hf_hub_download(repo, item[1], local_dir=MODELS_DIR)
            else:
                snapshot_download(repo)
        except GatedRepoError:
            say(f"FAILED {repo} needs its terms accepted: open https://huggingface.co/{repo} while logged in and click agree.")
            return 1
        except Exception as e:  # network, disk
            say(f"FAILED {repo}: {e}")
            return 1
    say("PROGRESS 1.00 Done")
    say("DONE")
    return 0


if __name__ == "__main__":
    sys.exit(main())
