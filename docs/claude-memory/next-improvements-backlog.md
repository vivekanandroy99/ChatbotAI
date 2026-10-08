---
name: next-improvements-backlog
description: "Improvement ideas and their status as of 2026-10-07 - what's done, parked (remember, don't build), dropped"
metadata:
  node_type: memory
  type: project
  originSessionId: b4e751f1-b40e-48b7-a3ee-293480808468
  modified: 2026-10-08T12:06:10.719Z
---

Status as of 2026-10-07.

**Done (2026-10-05 to 10-07)**:
- Documents page and voice-engine switches.
- Branding.
- Screen picker.
- 4K speed fixes.
- Touch drag-scroll.
- Bots made in the app and the new menu layout.
- Auto-restart and watchdog.
- Noise filter.
- Startup check.
- Automatic listening.
- Dictation with Speak/Keyboard buttons.
- Staff sign-in.
- Report with Excel export.
- Bot export/import.
- Slider fix.

**Tap to interrupt** already exists: tapping the mic button while the bot speaks stops it. The user wants it to stay exactly like that, in the mic button.

**Parked: the user said "remember these, but we don't want them now"** (2026-10-07). Don't build them unless asked:
- Suggested-question buttons.
- Brochure page or picture shown with an answer.
- 👍/👎 feedback.
- Bigger-text option.
- Opening hours / sleep mode.
- Visitor contact form.
- Attract mode.
- Hands-free presence greeting.

**Waiting on files**: more body language and the real male talk animation. There are no animation files yet; the user will export them from Character Creator 4 later.

**Smaller build: DONE in v5 (2026-10-07 b).** Svara, AI4Bharat and Piper were moved to Backups/AltcoreBot_v4-4, and Veena is now the only Indian Hindi engine. The v5 build hasn't been made yet; expected size is ~17.5 GB.
- The user said not to shrink the 3D characters or textures, important things, or the bge-m3 search model ("make it better if possible").
- The bge-reranker was downloaded and added (2026-10-07 c), with an on/off switch in AI models. The build will be about 19.6 GB.
- Topics field and the 6 s clear timer were added too.
- New bots Pearl and Peter for P&I (Pavilions & Interiors) were added 2026-10-07 (d).
  - "PNI" is understood via AvatarProfile.heardAs.
  - The v5 build hasn't been made yet.

Notes from before:
- The build is 25 GB:
  - TTSServer 16.6 GB (venv 6.9 incl. torch 4.1; indictts-venv 3.45; models 6.3: Veena 2.3, Svara 1.95, IndicTTS 1.54, Piper 0.18, Kokoro 0.3).
  - App data 6 GB (LLM 2.3, Whisper 0.5, LlamaLib 1.33 - mostly the cuBLAS DLLs that are needed, scene 1.6).
  - TTSRuntime 2.3 GB (bge-m3 2.2).
- Idea: ship only the voice engines that are switched on.

**Online AI is BACK (2026-10-08)**: user wants the preview page made real later (no API keys yet) + a Mac version
inside the same project, built on the Mac via copy or git - see [[mac-version-plan]].

**Dropped by the user**: logo on the stage, QR code, Audio2Face, more languages, licences.

**Why:** the user decides scope and order; they were explicit about remember-but-don't-build.

**How to apply:** when asked "what's left", list the parked items as parked. Never start them, or the smaller build, without an explicit go-ahead.
