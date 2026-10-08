---
name: model-comparison-2026-09-23
description: LLM comparison results for the voice companion (Gemma-3-4B chosen) and remaining follow-ups
metadata:
  node_type: memory
  type: project
  originSessionId: a6d0f108-adb8-4123-bea6-c336772cbbe8
  modified: 2026-09-24T13:34:45.519Z
---

2026-09-23/24: compared LLMs with tools/test_models.cs (Altscape/Altcore avatar + made-up "Brewline" brand in StreamingAssets/Knowledge/zz_test_brand, test-only; plus a fixed 10-sentence Hindi translation set). Winner and scene default: **Gemma-3-4B** (all answers correct, translation ~0.4 s/sentence, Hindi voice ~1.4 s, English ~1.0 s, ~10.7 GB VRAM). Qwen3.5-4B: correct but worse/slower Hindi (turned Pune into पंजे). Qwen3.5-9B: no better Hindi, 2.6x slower translation. Qwen2.5-7B: weakest Hindi. Direct-Hindi answering lost and was removed. Per-language model switching was considered: not worth it, since one model is best at both and two don't fit/help.

**Why:** user wants a universal multi-brand product, fast with good everyday Hindi.
**How to apply:** 2026-09-24 Hindi fixes done (odd Hindi came from forcing one-sentence English answers - removed). Next planned phase: the 3D avatar (CC4/ActorCore, uLipSync). Delete zz_test_brand before shipping builds.

Hindi voice (2026-09-24): user rejected IndicF5 by ear (fan-like/phasey artifacts even with clean studio references; my SNR checks passed, so they don't catch it - rely on user listening). Avatar Hindi voice restored to Kokoro hf_alpha. Sarvam's Bulbul is API-only (no offline weights). Candidates sent for listening: Meta MMS-TTS hin (instant, 16 kHz, CC-BY-NC) and Veena (maya-research, Apache 2.0, 4 studio voices, native Hinglish; Q8 GGUF via llama-cpp-python CUDA in TTSServer/models, ~90 tok/s = ~real time, needs streaming to feel instant).

2026-09-24 later: user liked Veena's accent but said Kokoro articulated English/brand words better ("experiential", "Altcore"). Fixed by respelling rare words in Devanagari from espeak IPA; Veena integrated as a streaming engine, Q4 default (Q8 lags with the app running), DefaultAvatar hindiVoice = veena_kavya. User also asked for model managers (select/add/delete) for voice and ears like the brain's - built; they may later add these to the in-app UI and try other models. Whisper transcription is a noisy judge of TTS clips (identical junk for different files) - use it only as a rough signal and let the user listen.

2026-09-24 evening: user reported Veena artefacts, English-word mispronunciation in Hindi, and female->male switches mid-sentence ("cant happen - voice and accent must be consistent"), and asked for more/best English voices. Fixed switches with pitch-verified anchor samples (a YIN pitch tracker is the reliable check for this); see CLAUDE.md for the speed traps found. Kokoro has no more v1.0 English voices; v1.1's 3 were downloaded and added. User still to judge by ear: artefacts, English words, which English voice.

2026-09-24 late: user chose Veena Kavya for English too (set on DefaultAvatar); plans to add Hindi reference documents on 2026-09-25 (Hindi docs now supported - see CLAUDE.md; they believe Hindi docs will reduce voice uncanniness - they improve reply wording, not the voice engine itself). Voice engines can be turned off without deleting.
