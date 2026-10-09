---
name: mac-version-plan
description: "Mac version (M5 Air 24 GB, M1 Pro 16 GB) + online AI - what exists now, what's untested, Indian-language options for Mac"
metadata:
  node_type: memory
  type: project
  originSessionId: b4e751f1-b40e-48b7-a3ee-293480808468
  modified: 2026-10-09T13:22:29.191Z
---

Owner machines: MacBook Air M5 24 GB, MacBook Pro M1 Pro 16 GB. Windows kiosk stays CUDA + Veena (Hindi).

**Done (2026-10-08/09), pushed except the last item:**
- Mac code in the same project (Platform.cs runtime checks, tools/setup_mac.sh, Editor/MacBuild.cs, MAC-SETUP.md, requirements-mac.txt) - written on Windows,
  NEVER run on a Mac yet. Expect a round of fixes on first run.
- Online AI Phase 1 (2026-10-09, memory.txt (e)): the BRAIN can use OpenAI / Claude / Gemini / OpenAI-style server (OnlineBrain.cs, SecretStore.cs,
  Menu > Advanced > AI models > Online models & API keys). Tested with tools/mock_online_ai.py only - owner has NO real API key yet, will buy one later.
- Online VOICE + EARS + many languages (2026-10-09 (f), same day): OnlineVoice.cs (Sarvam/OpenAI/ElevenLabs/Gemini/Other), OnlineEars.cs (Groq free tier/Sarvam/
  OpenAI/ElevenLabs/Gemini/Other), Languages.cs (44 languages, add a line to add one). Mock-tested only. Owner is fine with free tiers for testing (Gemini, Groq, Sarvam credits).
  Not yet: "don't load the local brain/Whisper when online" (needed to save GPU/RAM on a Mac), streaming, staff manual pages for voice/ears/More languages.

**Owner wants on Mac:** English first; Indian languages "and fast" if possible (asked 2026-10-09); both an offline lightweight mode and an online (API key) mode.

**Indian languages on Mac - what I told the owner (facts checked 2026-10-09):**
- The speed problem is the VOICE model type. LLM-style voices (Veena - Hindi+English only; Svara; IndicF5; Indic Parler-TTS, 20 languages but slow) need ~real-time
  token generation; a fanless Air can't keep up. Small VITS/FastPitch voices (MMS-TTS many languages but 16 kHz "phone" quality, Piper Hindi, AI4Bharat IndicTTS) are fast on CPU.
- Hearing (Whisper large-v3-turbo) covers Hindi/Marathi/Tamil/Telugu/Bengali/Gujarati/Punjabi/Urdu... and runs on Metal.
- Gemma-3-4B is decent in Hindi, weak in other Indian languages -> online brain is the real fix.
- Best quality AND fast = online voice (Sarvam Bulbul v3: Hindi, Bengali, Tamil, Telugu, Gujarati, Kannada, Malayalam, Marathi, Punjabi, Odia + Indian English; ~Rs 30 per 10k chars) - a later phase.
- Kokoro has 4 Hindi voices (fast, plainer) - already in the project.

**How to apply:** Mac work waits for the owner to try it on the Mac and report errors. For Indian languages beyond Hindi, build "online voice" (Sarvam) before
touching offline engines. Never start Mac/online phases unasked. See [[next-improvements-backlog]].
