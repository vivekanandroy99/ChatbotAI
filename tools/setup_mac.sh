#!/bin/bash
# One-time setup of the Mac version (Apple silicon), run from Terminal in the project folder:
#     bash tools/setup_mac.sh
# Safe to run again: anything already there (and correct) is skipped.
#   1. Python 3.11 (via uv - a self-contained Python, also copied into Mac builds) + TTSServer/venv
#   2. The voice & knowledge server's packages (TTSServer/requirements-mac.txt)
#   3. The AI models git leaves out: Gemma (brain), Whisper (ears), Kokoro (voice), bge-m3 + reranker (search)
# Models can also be copied from the Windows PC (same paths) - copied files are checked and not downloaded again.
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
echo "== ChatbotAI Mac setup in $ROOT"

if [ "$(uname -s)" != "Darwin" ]; then echo "This script is for macOS."; exit 1; fi
if [ "$(uname -m)" != "arm64" ]; then echo "Warning: this Mac isn't Apple silicon (M1 or newer) - the AI will be very slow."; fi

# ---- 1. Python ----
export PATH="$HOME/.local/bin:$PATH"
if ! command -v uv >/dev/null 2>&1; then
    echo "== Installing uv (Python installer, from astral.sh)"
    curl -LsSf https://astral.sh/uv/install.sh | sh
    export PATH="$HOME/.local/bin:$PATH"
fi
uv python install 3.11
if [ ! -x TTSServer/venv/bin/python3 ]; then
    echo "== Making TTSServer/venv"
    uv venv --python 3.11 TTSServer/venv
fi

# ---- 2. Packages ----
echo "== Installing the server's packages (a few minutes the first time)"
uv pip install --python TTSServer/venv/bin/python3 -r TTSServer/requirements-mac.txt

# ---- 3. Models ----
# download <url> <file> <sha256>
download() {
    local url="$1" file="$2" sha="$3"
    mkdir -p "$(dirname "$file")"
    if [ -f "$file" ] && [ "$(shasum -a 256 "$file" | cut -d' ' -f1)" = "$sha" ]; then
        echo "   ok: $file"; return
    fi
    echo "== Downloading $file"
    curl -L --fail --retry 3 -C - -o "$file.part" "$url"
    mv "$file.part" "$file"
    if [ "$(shasum -a 256 "$file" | cut -d' ' -f1)" != "$sha" ]; then
        echo "   WARNING: $file isn't the exact file the Windows version uses (checksum differs) - it may still work."
    fi
}
download "https://huggingface.co/lmstudio-community/gemma-3-4b-it-GGUF/resolve/main/gemma-3-4b-it-Q4_K_M.gguf" \
    "Assets/StreamingAssets/Models/LLM/gemma-3-4b-it-Q4_K_M.gguf" be49949e48422e4547b00af14179a193d3777eea7fbbd7d6e1b0861304628a01
download "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin" \
    "Assets/StreamingAssets/Models/Whisper/ggml-large-v3-turbo-q5_0.bin" 394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2
download "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/kokoro-v1.0.onnx" \
    "TTSServer/models/kokoro-v1.0.onnx" 7d5df8ecf7d4b1878015a32686053fd0eebe2bc377234608764cc0ef3636a6c5
download "https://github.com/thewh1teagle/kokoro-onnx/releases/download/model-files-v1.0/voices-v1.0.bin" \
    "TTSServer/models/voices-v1.0.bin" bca610b8308e8d99f32e6fe4197e7ec01679264efed0cac9140fe9c29f1fbf7d

echo "== Document search models (into ~/.cache/huggingface)"
TTSServer/venv/bin/python3 - <<'PY'
from huggingface_hub import snapshot_download
files = ["config.json", "sentencepiece.bpe.model", "special_tokens_map.json", "tokenizer.json", "tokenizer_config.json"]
snapshot_download("BAAI/bge-m3", allow_patterns=files + ["pytorch_model.bin"])
snapshot_download("BAAI/bge-reranker-v2-m3", allow_patterns=files + ["model.safetensors"])
print("   ok: bge-m3 + bge-reranker-v2-m3")
PY

echo
echo "== Done. Open the project in Unity 6000.6.0f1, open Assets/Scenes/AltcoreBot_v5.unity and press Play."
echo "   The first Play takes a while: LLMUnity downloads its engine files and the documents are indexed."
