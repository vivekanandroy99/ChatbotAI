# Mac version – setup

The same project runs on Windows (the kiosk version) and on a Mac. The Mac version is **English only and lighter**:

| Part | Windows | Mac |
|---|---|---|
| Brain (Gemma 3 4B) | NVIDIA (CUDA) | Apple GPU (Metal) |
| Ears (Whisper large-v3-turbo) | GPU | Apple GPU (Metal) |
| Voice | Kokoro + Veena (Hindi) | Kokoro only (Veena voices fall back to Kokoro) |
| Document search (bge-m3 + reranker) | NVIDIA | Apple GPU (MPS) |
| Add/remove documents | Windows Open dialog / Recycle Bin | Finder Open dialog / Trash |
| Auto-restart watchdog | yes | no |

Needs: a Mac with Apple silicon (M1 or newer), 16 GB memory or more (24 GB recommended), about 25 GB of free disk space.

## 1. Install the tools (once)

1. **Git**: in Terminal run `xcode-select --install` (Apple's developer tools include Git).
2. **Git LFS** (for the 3D characters and textures): download the macOS installer from https://git-lfs.com, or with Homebrew run `brew install git-lfs`. Then run `git lfs install`.
3. **Unity Hub** from https://unity.com/download, then install the editor **6000.6.0f1** (Apple silicon). Mac Build Support comes with the Mac editor.

## 2. Get the project

**From GitHub** (keeps the PC and the Mac in step):

```
git clone https://github.com/vivekanandroy99/ChatbotAI.git
cd ChatbotAI
```

GitHub asks you to sign in the first time.

**Or from a backup drive**: copy the project folder, but leave out `Library`, `Builds`, `Backups` and `TTSServer/venv` (that one is the Windows Python and won't run on a Mac).

## 3. Set up the AI (once)

In Terminal, in the project folder:

```
bash tools/setup_mac.sh
```

The script installs a private Python 3.11, the voice and search server's packages, and downloads the models that git leaves out. That's about 6 GB: Gemma, Whisper, Kokoro, bge-m3 and the reranker. Models you have already copied from the PC are checked and not downloaded again. You can run it again at any time.

## 4. Open and run

1. Unity Hub > **Add** > the project folder. The first open takes a long time (Unity builds its Library folder).
2. Open `Assets/Scenes/AltcoreBot_v5.unity` and press **Play**. On the first Play, LLMUnity downloads its engine files and the documents are indexed, so give it a few minutes.
3. Allow the microphone when macOS asks (for the Unity Editor, and later for the app).

On a 16 GB Mac (M1 Pro), if it's slow or runs out of memory, turn off the reranker in the app: Menu > Advanced > AI models.

## 5. Build the Mac app

**Tools > ChatbotAI > Build Mac App (Builds folder)** → `Builds/AltcoreBot_v5_mac/`. It contains `AltcoreBot.app` plus `TTSServer` and `TTSRuntime` (keep them together) and a README. The first time you open the app, macOS may block it because it isn't from the App Store: right-click it, choose **Open**, then **Open** again.

## Not tested yet

This was written on the Windows PC. These parts can only be checked on the Mac:

- Gemma and Whisper on Metal, document search on MPS
- the Finder file dialog and Trash
- the Mac build
- speed on an M5 Air (fanless) and an M1 Pro 16 GB

Report anything that fails (the Unity Console, and the `[TTS]` lines for the voice server) and it gets fixed in the same project.
