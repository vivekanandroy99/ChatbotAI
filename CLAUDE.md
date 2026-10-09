# Offline Unity Voice Companion — Project Brief

**`memory.txt` (project root) is the dated change log + orientation guide for people and other AIs. Add an entry
at its top for every change you make.**

**Scenes (2026-10-07): work in `Assets/Scenes/AltcoreBot_v5.unity` (copy of v4 <- v3 <- v2 <- the old DialogueTest.unity -
"DialogueTest" below means the working scene). `AltcoreBot_v1/v2/v3/v4.unity` are frozen copies - don't edit them; full
frozen snapshots are in `Backups/` (AltcoreBot_v1 ... _v4-3, _v4-4; no big models - see their RESTORE.txt).
v5 = Indian Hindi voices from Veena only: Svara, AI4Bharat Indic-TTS (+ indictts-venv) and Piper were REMOVED (moved to
Backups/AltcoreBot_v4-4/Removed voice engines - nothing deleted); old voice ids map to Veena (`VoiceCatalog.Current`,
server `RETIRED_VOICES`). The Svara/Piper/IndicTts notes below are history.
Document search has a reranker (bge-reranker-v2-m3, knowledge.py `_rerank`, 12 candidates): it only REORDERS passages -
gate scores stay bge-m3's. Menu switch = `ModelChoices.RerankerOff` -> env RERANKER_OFF. Top-1 lookups send rerank:false.
memory.txt 2026-10-07 (c).
P&I bots (2026-10-07 (d)): Pearl (pni_female) + Peter (pni_male), tools/setup_pni_bots.cs; document = owner's file minus
chatbot-team sections (tools/make_pni_document.py). `AvatarProfile.heardAs` ("PNI" -> P&I, applied in Ask),
`searchWithoutTopicNames` (on for P&I only - "P&I" is opaque to the search models), `rerankPassages` (off for P&I).
Answer agent: `WithoutDocumentNotes` + "(The CONTEXT doesn't mention X.)" in BuildPrompt; worked examples use made-up
places (real city names in examples leaked into answers).
Bot switch (2026-10-08): AvatarStage.Reveal poses the new character hidden (AvatarAnimator.PoseNow) and cuts once - don't
show a freshly spawned CC model directly (it renders folded on the floor for a few frames). Whisper's hint = bot's topic +
heardAs names first, then document vocabulary (built when listening starts).
Lighting (2026-10-08): menu > Lighting = StageBackdrop.Lighting per bot ("light/<id>"), multipliers on the scene's key light
and the Advanced fill/studio settings; applied in Play mode only (never edit the scene's Directional Light from code in
the Editor). Menu pages keep their scroll (CompanionMenu.resumeAt).
Mac version (2026-10-08, memory.txt (f), MAC-SETUP.md): same project; Windows-vs-Mac through `Platform` runtime checks
(not #if - keeps every branch compiling on both). Mac = English, no Veena, Metal/MPS; tools/setup_mac.sh + Editor/MacBuild.cs.
Git (2026-10-08 (e)): Git LFS for art; models/venvs/LlamaLib/indexes are gitignored - setup_mac.sh downloads them.
Help (2026-10-08): HelpGuides.cs = a guide per menu page (? in the sheet header; root "Help & guides"); pictures in
Resources/Help from tools/capture_help_shots.cs + import_help_shots.cs - re-run them when a menu page changes, and add
or edit a guide when you add or change a page.
Online AI (2026-10-09, memory.txt (e)): optional online brain - `OnlineBrain.Chat(role, prompt)` replaces `agent.Chat(...)` in
DialogueController (falls back to the local LLMAgent); keys in `SecretStore`; test without a key with tools/mock_online_ai.py.
Hearing, voice and document search stay local BY DEFAULT. Keep it that way unless asked (privacy: only question + passages + persona go out).
Online voice + ears + many languages (2026-10-09, memory.txt (f)): OnlineVoice.cs / OnlineEars.cs / Languages.cs; each part is a separate
menu switch with local fallback; non-English/Hindi languages = the online brain translates (DialogueController.Ask/ToOther) + an online voice speaks.
Staff manual (2026-10-09, memory.txt (a)): docs/AltcoreBot-Staff-Manual.pdf, made by tools/manual (capture_manual_shots.cs in
Play mode, then build_manual.py) - re-make it, and edit its text in build_manual.py, when a menu page or a bot changes.
v4 = studio HDRI lighting (StageBackdrop draws it) + sharper shadows (project-wide URP asset) - memory.txt (l). Check `unity command editor_status` before recompiling/entering Play: the user is often in Play mode.**
**Stage (v3): `StageBackdrop` ("Stage" object) + `StageLook` assets (Assets/Stage) replace the flat background - see
memory.txt 2026-09-30 (i)/(j): Studio = lit paper + hidden background spot + real key-light shadow. Per bot:
AvatarProfile.stage; app: Menu > Camera & Stage > Backdrop. Camera shots: `CameraFraming.Shots` (Menu > Camera).
Backups: Backups/AltcoreBot_v1, _v2, _v3. When testing the AI, back up and restore `Conversations/` (the Learning log).**
**Builds: only into `Builds/<working scene>/` (Tools > ChatbotAI > Build Windows App). PortableBuild.cs makes every
Windows build plug and play (TTSServer + TTSRuntime/python + HF models + default-settings.json). Keep LLMUnity's
cuBLAS on (flash attention needs it). Never copy PlayerPrefs through the registry while the Editor runs - memory.txt (n).**

## Performance and audio traps (2026-09-29, read before touching UI or voice)
- UI Toolkit `filter:` draws the element with its text/icons through an off-screen image - text comes out BLURRY
  (selected segmented option, mic icon, 2026-10-06): never filter an element that holds text or an icon; put a glow on
  a separate element behind it (.main-glow).
- UI Toolkit `filter: drop-shadow()` on a big element is re-rendered EVERY frame, even closed/off-screen: the
  settings sheet's 40px shadow at 1440x2560 made Veena (GPU) run 3x slower than real time -> every Hindi sentence
  broke up. Sheet/debug card now use a 1px edge; the closed sheet is `display:none` (`.sheet--gone`). The dock's
  `backdrop-filter: blur` is ~free on small screens but NOT at 4K (~20% GPU): CompanionUI "Frosted glass" Auto uses a
  lighter blur above 4.2 MP, and StageBackdrop caps the 3D render size (renderScale + FSR) - memory.txt 2026-10-06 (a). Measure Veena speed in the app (POST /speak while Play mode renders).
- `SpeechOutputController.ReadyToPlay`: a streamed sentence starts only when buffered >= D(1-r) + r*prebuffer
  (r = `PcmStream.ArrivalRate`, D from learnt seconds-per-character per voice), so slow generation delays a
  sentence instead of leaving silence mid-word. Other GPU apps (Character Creator etc.) also slow Veena.
- Open Chat Hindi: the router agent (unused in Open Chat) translates the message to English first
  (`ToEnglishPrompt`, ASCII grammar) - shown raw Hindi, a British persona replied "I don't speak Hindi".
  `SpokenOpenReply` strips stage directions and quotes; `ScriptBoundary` spaces Devanagari/Latin run-togethers.
- `WhisperLanguageDetector` sums hi/ur/mr/ne/pa/gu/bn/sa vs en (Hinglish is often scored as a neighbour).
- Menu > Camera: `CameraFraming` (distance, aimBelowEyes, side, turn, tilt) per character + tall/wide in PlayerPrefs
  (`camera/<character>/<tall|wide>`); `AvatarStage.PreviewFraming/SaveFraming/ResetFraming`.
- Loading screen (`.loading` in Companion.uxml, `CompanionUI.UpdateLoading`, `ProgressRing`): opaque from the first
  frame so the startup T-pose is never seen; fades once `StartupProgress.IsReady` (+0.5 s), then `display:none` and
  the ring stops redrawing.
- Learning (owner in charge, nothing self-learnt): `ConversationLog` (Conversations/<date>.jsonl, outcomes incl.
  Partial = reply matched `PartialAnswer` "I don't have details..."), `TaughtAnswers` ("Taught answer - *.txt" in the
  bot's StreamingAssets knowledge folder; KnowledgeSync never touches them; bots with the same document files learn
  together; open chat gets a matching one >= `taughtMatchScore` as a private note - given the whole Q&A, Iris
  answered as if the visitor had said it). Menu > Learning pages in CompanionMenu.
- 2026-09-30: `AdminAccess` (UI object; users in Inspector, default admin/admin; CompanionUI adds one if missing)
  gates the menu with a sign-in card (`.login`). `[Tunable]` (Assets/Scripts/Tunable.cs) = app-adjustable Inspector
  field; `Tunables.ApplySaved` runs AfterSceneLoad (before Start) - only mark fields read while running, or use
  `ITunableListener`. `AvatarSettingsStore` saves any AvatarProfile field by name (string/float/int/bool/enum).
  Personality details (tone, formality, humour, style, always/never, avoid topics, reply length) become
  `AvatarProfile.StyleRules()` + `{length}`/`{askBack}` in the rules. `OnScreenKeyboard` (CompanionControls) for
  the chat (Show keyboard, off by default) and sign-in. Camera: presets, lens (fieldOfView), follow (LateUpdate).
- 2026-09-30 Hindi engines added (see memory.txt (d)): `svara_engine.py` (Veena-like, anchors in voices/svara, gain per
  voice, `_svara_text` numbers), `piper_engine.py` (onnx on CPU, espeak via phonemizer directly - Kokoro's phonemize
  filters to Kokoro's vocab), `indictts_engine.py` + `indictts_worker.py` (separate venv `TTSServer/indictts-venv`, port
  8766). Unity: VoiceEngineDownloads.Svara/Piper/IndicTts (`localSubfolder`, `nonCommercial`), VoiceCatalog entries,
  VoiceModelLibrary.Engine.Svara, ModelChoices.Svara/SVARA_MODEL. A test sidecar started by hand is reused by Play mode
  (it won't get Unity's env vars) - stop it after testing.
- 2026-10-05: Menu > Conversation > Documents adds/removes a bot's documents at runtime (Windows Open dialog +
  Recycle Bin: `UI/WindowsFiles.cs`); sharers (`TaughtAnswers.LearnersWith`) get the same change. AI models: one group
  per voice engine with its own switch (`ModelChoices.OffKey`, `VoiceModelLibrary.Optional`).
- 2026-10-06: bots made in the app = `CustomBots` (StreamingAssets/Bots/<id>.json, built on a built-in profile with
  the same look) loaded by `AvatarRegistry` (CreateBot/Duplicate/Delete); menu layout = Bots / active bot pages /
  Visitors / Devices / Display / Advanced. Swipe scrolling = `DragScroll` (Unity's ScrollView didn't scroll on a swipe).
  Touch can be tested in Play mode: `PointerDownEvent.GetPooled(Touch)` with the PANEL position, `root.SendEvent`.
- 2026-10-06 (d): auto-restart (TTSProcessManager.Watch; builds: AppHeartbeat + "Start <app>.cmd"/Watchdog.ps1 from
  PortableBuild), StartupCheck, noise filter + automatic listening + Dictate() in SpeechInputController, StaffAccounts,
  ConversationReport/SimpleXlsx (Excel), BotPackage (.altbot export/import). See memory.txt 2026-10-06 (d).
- Target hardware: LARGE TOUCH SCREENS (no mouse wheel/keyboard). `TouchScrollbar.AddTo` (menu + transcript), menu
  OnScreenKeyboard on text focus, hover only under `.pointer-mouse`; avatar reset lives in Advanced > Reset settings.
  Simulated touches don't reach an unfocused editor - test on the device.
- UI shows a frozen light "Getting ready" screen / white menu groups? A second UI drawer on the Companion UI object
  (a PanelRenderer was found next to the UIDocument, 2026-09-30). CompanionUI.Awake destroys it; check the panel's
  visualTree has ONE root. No runtime PanelSettings copy (the scaling is set on the asset and restored in OnDestroy).
- Removed (Recycle Bin, 2026-09-29): Qwen LLMs, Whisper Hindi medium, Veena Q8, Kokoro v1.1-zh (af_maple/af_sol/
  bf_vale gone), HF-cache Parler/IndicF5/MMS/HF-Whisper. IndicF5 notes below are history.

## What this is
A Unity3D character that listens (mic), understands speech, replies out loud, and lip-syncs —
entirely offline, no network calls, no cloud APIs. It behaves like a customer-service bot: it only
answers questions on a fixed, pre-defined list of topics, and gives a quirky, in-character refusal
for anything outside that list (even if it "knows" the answer).

Target platform: desktop (Windows/macOS/Linux).
Input languages: Hindi and English, including Indian-accented English (required for launch).
Output language: English at launch; architecture is built so more languages are additive later.
Character: a realistic, fully rigged 3D human avatar with facial animation.

## Final tech stack (locked in — not staged/MVP choices)

| Layer | Tool | Notes |
|---|---|---|
| Hears (STT) | [whisper.unity](https://github.com/Macoron/whisper.unity) running GGML `whisper-large-v3-turbo` for both languages. `WhisperLanguageDetector` picks English vs Hindi from Whisper's per-language probabilities (Urdu counted as Hindi), then transcribes with that language forced — never Urdu script. `vasista22/whisper-hindi-medium` stays available as an Inspector toggle but tested worse (junk prefixes, invented sentences, 3x slower). Runs natively inside Unity, no sidecar. |
| Thinks (dialogue) | [LLMUnity](https://github.com/undreamai/LLMUnity) (wraps llama.cpp) running **Gemma-3-4B-it** (Qwen2.5 1.5B/3B/7B and Qwen3.5 4B/9B selectable via `LLMModelSelector`), GGUF Q4_K_M, CUDA. Chosen 2026-09-24 by `tools/test_models.cs` (two brands, 25 questions + a fixed Hindi translation set): all answers correct, best Hindi, fastest (Hindi voice ~1.4 s, English ~1.0 s), least VRAM (~10.6 GB total). One model does both languages - routing/answering is English-only, so a separate Hindi model bought nothing. Runs natively inside Unity, no sidecar. |
| Speaks (TTS) | [Kokoro-82M](https://huggingface.co/hexgrad/Kokoro-82M) via [kokoro-onnx](https://github.com/thewh1teagle/kokoro-onnx) on onnxruntime-gpu — English + Hindi voices, ~0.2 s per sentence; Hindi preferably via the optional [Veena](https://huggingface.co/maya-research/Veena) engine (see below). (Replaced Indic Parler-TTS: slow, and its output was unintelligible.) **Not Unity-native** — runs in the local Python sidecar (`TTSServer/`, also hosts the knowledge base), Unity talks to it over localhost; freeze it with PyInstaller for distribution so end users never need Python installed. Note: Windows Application Control on the dev machine blocks some unsigned native Python modules (spaCy, scikit-learn), which is why the non-ONNX `kokoro` package can't be used. |
| Lip-syncs | [uLipSync](https://github.com/hecomi/uLipSync) — free, MFCC-based, drives blendshapes live from whatever audio the TTS produces. |
| Body / character | [Reallusion Character Creator 4](https://www.reallusion.com/character-creator/) + [ActorCore marketplace](https://actorcore.reallusion.com/) for a ready-made realistic human, rigged with the free [AccuRig](https://www.reallusion.com/auto-rig/accurig/), imported via the free [Auto Setup for Unity](https://www.reallusion.com/auto-setup/unity/default.html) plugin (gives skeleton + ARKit-style facial blendshapes + skin/hair shaders). |
| Animation | Body idle/gesture clips from [Mixamo](https://www.mixamo.com/) (free) or Reallusion ActorCore motion packs; [Unity Animation Rigging](https://docs.unity3d.com/Packages/com.unity.animation.rigging@latest) layered on top for procedural gaze/blink. Facial/mouth motion is never baked — uLipSync generates it live every time. |

## Growth path for more languages (not built yet, but the architecture assumes it)
Insert [AI4Bharat IndicTrans2](https://github.com/AI4Bharat/IndicTrans2) (offline English ↔ 22
Indic languages) on both sides of the dialogue brain: translate non-English input to English
before the LLM sees it, translate the English reply back to the target language afterward. This
keeps one English-only brain/topic-gate to maintain — each new language is "add a translation pair
+ a TTS voice," not a rebuild.

## The topic gate (the core product behavior — read this before touching the dialogue code)
There is no hand-written topic list. Each avatar's knowledge is the documents (.pdf, .docx,
.pptx, .txt, .md) in its `AvatarProfile.knowledgeDocuments` list (drag-and-drop in the Inspector;
files dropped from Explorer land in `Assets/KnowledgeDocuments/<avatarId>/`). The editor script
`KnowledgeSync` copies them to `Assets/StreamingAssets/Knowledge/<avatarId>/` — a generated
folder, don't hand-edit it — where the sidecar (`TTSServer/knowledge.py`) extracts, chunks and
embeds them with multilingual bge-m3, hot-reloading on changes. A system prompt alone is NOT considered reliable enough, so
`DialogueController` enforces three checks:
1. **Before the LLM is called** — the question is embedded and matched against the documents.
   Below `AvatarProfile.relevanceThreshold`, skip the LLM entirely and return a refusal.
2. **The LLM only sees the matching passages** and must answer from them or reply `NO_ANSWER`
   (→ refusal).
3. **After the LLM replies, before TTS speaks it** — the draft is matched against the documents
   again; below `replyGroundingThreshold`, swap in a refusal instead of speaking it.

Refusals are picked from a written bank of in-character lines per language that steer back to the
avatar's `topicNames` ("I can't help with that one, but ask me anything about {topics}!"), rotated
randomly, never freely generated by the LLM.

Around the gate (all agents share the one LLM, see `DialogueController`):
- **Router** (first): classifies small talk (greeting, thanks, who-are-you...) — answered from the
  written lines in `SmallTalk.cs`, never LLM text, because it once "answered" a brand question as
  small talk with an invented fact — or rewrites the message as a standalone English question
  (translates Hindi, resolves "does *it* work on mobile?" against the previous question).
- **Answer agent**: always English, from the passages only; partial answers say what isn't known.
- **Search helper**: on NO_ANSWER, writes a guessed answer to search with once more.
- The router also marks general-knowledge questions (news, weather, sports, jokes) GENERAL: those need
  the full relevance score. A customer question just under the threshold still goes to the answer
  agent, which decides from the passages. Passages are also ranked by their best single sentence
  (knowledge.py), so one small fact isn't drowned out by the rest of its passage.
- `topicNames` left empty on an avatar are detected from its documents (sidecar `/topics`).
- **Hindi documents** work (tested 2026-09-24 with a made-up Hindi bakery doc): bge-m3 scored English
  questions against it like against its English version (0.68 vs 0.72; off-topic 0.27-0.36); sentences
  split on the danda. The answer agent copies the language of the passages, so `BuildPrompt` adds
  "(Answer in English.)" for English questions over Hindi passages, and for Hindi questions over Hindi
  passages "(Answer in Hindi, in the CONTEXT's own words.)" - translating an English answer back had
  turned केसर पिस्ता into "white and almond". Already-Hindi sentences skip the translator (faster:
  ~1.7 s first sound). Hindi PDFs often extract scrambled (or Kruti Dev fonts -> Latin gibberish):
  knowledge.py logs a warning when extracted Hindi looks broken - prefer .docx/.txt for Hindi.
- `tools/test_models.cs` uses a made-up brand in `StreamingAssets/Knowledge/zz_test_brand` —
  test-only; delete that folder before shipping a build.
- **Translator** (Hindi questions only): English answer → everyday spoken Hindi, one sentence at
  a time, each spoken as soon as it's ready. Then the sidecar's `/restore_english` puts English
  words the model wrote in Devanagari back into English letters and swaps bookish Hindi for
  everyday words (`hinglish.py`). Hindi questions get the same full English answer as English ones —
  squeezing it into one sentence made the model distort facts. (Answering directly in Hindi was
  tested and lost: romanized Hindi, invented facts, no faster.) Router and translator run at
  temperature 0. If a follow-up's rewrite drops the brand the previous question named, the previous
  question is appended as context.

Voice & pronunciation (per avatar, Inspector): English/Hindi voices are picked from Kokoro's own
voices (`VoiceCatalog.cs`, Preview button in Play mode); `pronunciations` respells words the voice
gets wrong (spoken text only — e.g. Koregaon → "Koray-gown", Hindi "कोरेगांव").

Optional Hindi engine Veena (Maya Research, Apache 2.0) - voices `veena_kavya`, `veena_maitri` (female),
`veena_vinaya` (male, less tested; `agastya` dropped - never held a male voice). Also offered as English
voices so an avatar sounds like one person in both languages. DefaultAvatar Hindi = `veena_kavya`.
`TTSServer/veena_engine.py`: 3B Llama GGUF on llama-cpp-python (CUDA wheel; import torch + add its lib
dir first) writes SNAC codes -> 24 kHz, streamed (`/speak_stream`: 4-byte rate + PCM16; Unity `PcmStream`).
Hard-won facts (measure with a pitch tracker, the user hears these):
- The speaker tag doesn't hold the voice: female->male->female mid-sentence in 7-13/24 sentences. Every
  sentence continues a fixed, pitch-verified sample ("anchor", `voices/veena/anchors.json` + .wav, made by
  `python veena_engine.py anchors`) -> 0/24. `ANCHOR_STYLE = "continue"`: ONE turn "<anchor text> <new text>"
  whose speech is prefilled with the anchor audio. The earlier separate-turn anchor sometimes re-read the
  anchor sentence, mumbled or stopped early: Whisper heard back 45% of words vs 75% (English words 21% vs 55%).
  Ending is blocked until len(text)*0.45 frames and cut at len*1.3+15 frames (babble).
- SpeechOutputController splits long sentences (>100 chars) only at the comma/conjunction nearest the middle,
  pieces >= 35 chars: splitting at every comma made fragments ("जिसमें residential,") that came out as babble.
- Q4_K_M is the default (Q8 can't keep up with the app running). ~0.73x real time in the app.
- Things that silently made it 1.1x (audible gaps): onnxruntime's spinning CPU threads in the same
  process (Kokoro sessions use 1 thread, no spinning - `session_options()`), Windows power-throttling the
  windowless sidecar (`run_at_full_speed()`), and Unity drawing ~900 fps (`FrameRateCap`, 60).
- Unity reads ~0.86 s ahead when a streamed clip starts: `streamPrebufferSeconds` 0.9, else a gap 0.5 s in.
- Pronunciation: ordinary English words stay in Latin (Veena's own reading is good; respelling them via
  espeak gave American vowels). Only Capitalised names it doesn't know whole (Altcore) are respelled -
  Devanagari from espeak IPA in Hindi, split "Alt-core" in English. Acronyms left alone. Indian names
  (Koregaon) need the avatar's pronunciation list. Numbers -> Hindi words in Hindi replies.
- Hindi first sound ~3.1-3.8 s after the question (brain+translation ~2 s, Veena ~1.2 s incl. the anchor prefill). Veena ignores speechSpeed.
DefaultAvatar speaks English with `veena_kavya` too (one voice in both languages; English first sound
~2.2 s vs ~1.3 s with Kokoro). Optional engines can be "Turned off" in TTSProcess > Voice models (kept on
disk, never loaded; VOICE_ENGINES_OFF -> their voices speak with a same-gender Kokoro voice).
English: all 28 Kokoro v1.0 English voices are listed; Kokoro v1.1 (optional download `kokoro11`) adds its
only 3 English voices af_maple / af_sol / bf_vale (less English training; af_sol may repeat words).
IndicF5 (AI4Bharat, gated) is kept as "experimental": the user heard a watery/fan-like texture on every
voice, even with clean references - don't offer it as the good option. Voices `indicf5_*` copy a
reference clip in `TTSServer/voices/indicf5/voices.json`. Gotchas handled in `indicf5_engine.py`:
transformers renames gamma/beta weights (load safetensors directly), float16 gives NaN (bfloat16), torchaudio
needs torchcodec (soundfile instead), `datasets`/pandas stubbed (Application Control). Sarvam's TTS
(Bulbul) is API-only - no offline weights. Meta MMS-TTS Hindi was tested: instant but 16 kHz phone quality.

Model managers (Inspector; runtime libraries usable from in-app UI later): Brain = `LLMModelSelector`
(`LLMModelLibrary`), Voice = `TTSProcessManager` (`VoiceModelLibrary`: Kokoro .onnx / Veena .gguf in
`TTSServer/models`, IndicF5 in the HF cache; selection passed as KOKORO_MODEL / VEENA_MODEL env vars when
the sidecar starts), Ears = `SpeechInputController` (`WhisperModelLibrary`, StreamingAssets/Models/Whisper,
GGML magic checked; "General" and "Hindi" slots set the WhisperManagers' modelPath). Each lists files
with sizes, Use/Delete (refuses what's in use), and "Add ... file" (copies a file in; a new KIND of voice
model needs an engine adapter in the sidecar first). The fine-tuned Hindi Whisper is no longer loaded
unless selected (saves ~1.7 GB VRAM - needed to fit Veena).
Models: `LLMModelSelector` picks any .gguf in `StreamingAssets/Models/LLM`; its Inspector lists them
with sizes and deletes unused ones (`LLMModelLibrary`, also usable from in-app UI). Only the selected
model is marked includeInBuild, but everything in StreamingAssets ships — delete what you don't use.

## Avatar (started 2026-09-28)
- Cast (2026-09-29, replaced Party F and Kevin - the user deleted their folder): four CC4 actors in
  `Assets/ActorCore Model/Actors/{Ethan,Iris,Leo,Maya}` (CC4 exports with .json, 150-160 face shapes, no Eye_Relax),
  built by `tools/import_actors.cs` (the CC/iC importer's own batch path: CharacterInfo + Importer.Import(true), HQ
  materials; ~40 s each - the CLI times out after 5 s but Unity keeps going; wait for Prefabs/<name>.prefab).
  `tools/fix_cc5_materials.cs` (now any list of prefabs) makes CC4 Scalp_Transparency / *_Brow_Base_Transparency
  alpha-blended (their diffuse alpha is a real mask; opaque they were grey/black forehead bands and brow patches) and
  hides Maya's `Dress` (worn under her turtleneck + skirt, showed through in patches). `tools/setup_four_bots.cs` builds
  everything else: characters/motion sets/lip-sync in `Assets/Characters` (outside the model folder), the 4 profiles.
  | Bot | Profile | Mode | Voices (English / Hindi) |
  | Maya (female) | Altcore Female - Maya, altcore_female | Knowledge only | af_heart / veena_kavya (v5, 2026-10-07) |
  | Ethan (male) | Altcore Male - Ethan, altcore_male | Knowledge only | am_michael / veena_vinaya (v5) |
  | Iris (female) | Chat Female - Iris, chat_female | Open chat | af_bella / hf_alpha (Maitri was least clear, 50-75%) |
  | Leo (male) | Chat Male - Leo, chat_male | Open chat | am_fenrir / veena_vinaya (v5) |
  | Pearl (female) | PNI Female - Pearl, pni_female | Knowledge only (P&I) | af_heart / veena_kavya (2026-10-07) |
  | Peter (male) | PNI Male - Peter, pni_male | Knowledge only (P&I) | am_michael / veena_vinaya (2026-10-07) |
  Registry lists them in that order (app starts with Maya). New voices borrow the closest calibrated lip-sync profile
  (am_fenrir <- am_michael, af_*/veena_maitri <- veena_kavya) in LipSyncVoiceProfiles. Resting eyelids 0.65 (blink ~24%;
  15% still stared). MOTIONS: `Assets/ActorCore Model/Motion/<Idle|Talk><n>_<Male|Female>/` - setup_four_bots.cs finds
  them by folder name (gender-strict; checked 2026-09-29 - BUT Talk_Male_Motion.Fbx is an exact copy of the female
  talk, 0.0 cm apart, and idle 3 male ~ female 2 cm: the user must re-export). Now 3 idles + 1 talk per gender (male idles 25.0/23.1/18.5 s, female 30.0/30.0/18.5 s, talks
  24.7 s - the same talk performance for both; loops join within 0-1 cm, female idle 2 at 3-6 cm). CC export that
  WORKS: Export FBX, Unity 3D preset, FBX option Motion, Include Motion = Current Animation (the take comes in as
  "<id>_TempMotion" plus a 0.02 s 0_T-Pose). The first exports had Include Motion = Calibration: every file held CC's
  "Calibration" take (T-pose + range of motion) - such takes are skipped and `Placeholder Idle/Talk.anim`
  (tools/make_placeholder_motions.cs, muscle-value stand pose, footIK off - hand-made clips have no foot IK goals, with
  foot IK the legs were pulled into a stride) fill in. Prepare re-prepares a file whose take name changed.
  AvatarAnimator starts a looping clip longer than 8 s at a random point (not when the next clip follows on), so each
  reply doesn't open with the talk clip's same arms-crossed gesture.
- **How a character is put together (built for many characters):**
  `AvatarProfile.character` (persona picks its look) -> `AvatarCharacter` asset = prefab + `AvatarAnimationSet`
  (states Idle/Talk/Listen/Think..., any number of clips each, blend/speed/loop; or its own Animator Controller)
  + `AvatarLipSyncTuning` (mouth shapes per sound, jaw/mouth strength, speeds, loudness range). Sets and tunings
  are shareable between characters on the same rig/face; left empty, the stage's fallback set / an auto-map of the
  face's blendshape names is used. Scene: "Avatar" = `AvatarStage` - spawns the character's prefab as a child and
  wires `AvatarAnimator`, `AvatarLipSync`, `AvatarBlink`, `AvatarEyes` (all on the stage, so their settings survive a
  swap) to it; follows the active profile at runtime, `Show(character)` for an in-app picker later. Character
  Inspector checks the prefab (Humanoid rig, eye bones, mouth/eyelid shapes) and creates the missing set/tuning.
  Changing a profile's character in edit mode swaps the model in the open scene. Party F = `Assets/ActorCore
  Model/Female Model/Party F.asset`. `Avatar Animator.controller` is unused (AvatarAnimator plays clips through
  the Playables API and sets the Animator's controller to none at runtime).
- ActorCore motions are skeleton-only FBX files in a separate Motion folder; the CC importer only handles
  motions named `<character>_<name>_Motion.fbx` next to the character, so it skips them (Generic, no avatar).
  The animation set's "Prepare clips" button (and `tools/setup_avatar_scene.cs` for the first two) sets them
  Humanoid (bone map copied from the character), keeps only the real take (each file also has a 0.02 s
  `0_T-Pose` take) and loops them in place. The setup script only adds what's missing (assets, stage) and
  converts an old-style scene Avatar into a stage, keeping component settings.
- (Earlier cast, deleted: Party F - ActorScan, one mesh; Kevin - CC5, `tools/add_kevin_character.cs`.) CC3+/CC4/CC5
  characters spread the face shapes over ~12 meshes (body, teeth, tongue, lashes, brows, stubble, hair, eye
  occlusion, tearline) - `AvatarFace.FaceRig` sets a shape by name on every mesh that has it (lip-sync, blink,
  eyelid-follow), else teeth/lashes stay still. CC/iC Unity Tools 2.2.6 doesn't know CC5's HD brow layers
  (Brows_Base/Brows_Color: all-white opacity maps -> solid patches) or the hair cap (Hair_Clap: grey scalp, then a
  light band on the forehead): `tools/fix_cc5_materials.cs` hides those meshes and exported props (Sphere01,
  obj_default); the brow and hair strands are separate meshes and stay. An importer update may handle them properly.
- Avatars live in `Resources/Avatars` (the four above; older tools/*.cs still name DefaultAvatar.asset / Kevin.asset -
  those files were renamed to the Altcore profiles). veena_vinaya is pitch-checked male throughout (93-150 Hz). In-app picker:
  the app screen's menu (see App screen below; the older rough `AvatarPickerUI` is kept inactive) lists every
  registered avatar and calls `AvatarRegistry.SetActive` - `OnActiveChanged`: DialogueAudioBridge stops the old voice and warms the new
  one, DialogueController re-applies persona/prompts on the next question, AvatarStage swaps the character.
  Session-only; the app starts with the first avatar.
- Open Chat mode (`AvatarProfile.mode`; Iris and Leo): no documents/gate/router/written
  small talk - the persona + `OpenChatRules` answers anything, remembers `openChatMemory` exchanges (cleared after the
  follow-up window = new visitor), `openChatCreativity` = temperature. Always answers in English (Hindi messages get a
  note, else it prefixed "(Translating: ...)"), then the usual per-sentence translator. `SpokenOpenReply` strips
  markdown. Tested: in character, remembers follow-ups, admits it can't know live news; first sound ~0.8 s English,
  ~2 s Hindi. The Altscape avatars still refuse off-topic questions. The persona is warmed when the avatar changes.
- Translator lessons (both modes): it ANSWERED short questions ("What about you?" -> "मैं करता हूँ reading") - every
  input is now "ENGLISH: <text>" (examples too) and tag questions use `FixedPhrases` (it flipped "और आप?" to "और मैं?").
  `/restore_english` with names_only (Open Chat): everyday English words matched Hindi verbs by consonants
  (कहूँगा -> "cooking", कहना/खाना -> "know"); only capitalised non-sentence-initial words may come back.
- App screen (2026-09-29): `CompanionUI` + `CompanionMenu` (Scripts/UI), UI Toolkit files in `Assets/UI/Companion`
  (Companion.uxml/.uss, CompanionTheme.tss, CompanionPanel.asset, Inter fonts copied from the Unity install - OFL),
  added by `tools/setup_companion_ui.cs` (old uGUI Canvas/TestHarness/Avatar Picker UI kept, inactive). Apple-like,
  light/dark tokens on the root (.theme-light/.theme-dark), state class on the root (.state-starting/-ready/-listening/
  -thinking/-speaking/-error). Drawn OVER the full-screen 3D scene (the user rejected a round portrait - a stage
  backdrop comes later): no title/top bar (user: not needed) - only the menu button, floating top-right in the same glass;
  the last exchange + controls sit in a frosted glass panel (.dock: --panel
  colour + backdrop-filter blur), the rest of the scene is clear. Two arrangements, picked from the window shape
  (CompanionUI toggles .layout-portrait when height > width): wide 16:9 = a 430-wide card on the RIGHT beside the
  avatar (a full-width bottom panel hid her whole torso); tall 9:16 phone-style = panel along the bottom. The panel
  has a FIXED height (470 wide / 380 tall) - it used to grow with the text, which the user found weird; text scrolls
  inside and new blocks fade in (.message--enter removed a frame later). Framing per shape on AvatarCharacter:
  cameraDistance/aimBelowEyes (wide, 2 / 0.25) and portraitCameraDistance/portraitAimBelowEyes (tall: Party F 3.4, Kevin 3.8 /
  0.65 - talking gestures swing the hands ~1 m apart; at 2.4 m they left the frame, measured with the hand/forearm bones);
  AvatarStage re-frames by itself when the camera's aspect flips. Target is a PORTRAIT 2K/4K TV: tall screens are scaled to their
  WIDTH (720 units wide, `tallScreenWidthUnits`; runtime copy of the PanelSettings) - scaled to the height like wide screens,
  the text looked huge and the panel took 46% of the screen (now 30%). To test tall in the editor:
  PlayModeWindow.SetCustomRenderingResolution(1080, 1920, ...), restore GameView.selectedSizeIndex afterwards. The camera clears to the theme's
  --bg until there's a backdrop (a solid colour, not a skybox; dark = soft grey #1E1E21). Dark is the default (user prefers it); light was toned
  down to greys (#C9CCD2). Mic on = the main button turns red with a ripple (.mic-pulse, animated in CompanionUI).
  Characters are framed head-to-waist so body language shows: cameraDistance 2, aimBelowEyes 0.25 (was 1.3 / 0.12
  head-and-shoulders). A menu mode change is saved per avatar - AltBot was once found in Open chat that way and
  invented Altscape facts ("core data platform... geological surveys"); the menu now warns about it. Icons, switch, segmented control, slider are drawn in code
  (`CompanionControls.cs`, no image files). Play-mode FrameCamera faces the way the character stands (the head turns
  with the animation). `-unity-text-generator: advanced` shapes Devanagari (Hindi
  renders correctly with the OS fallback font). No default UITK theme: ScrollView clipping/scrolling needs the
  `.unity-scroll-view*` rules in Companion.uss, and the document root needs flex-grow (the .tss). Space = push-to-talk
  (buttons are non-focusable so Space doesn't also press them). Menu: avatar, voices (tap = sample), speed, mode,
  name & persona, memory/creativity or topic strictness, listening language, light/dark, debug. The registry runs on
  runtime copies of the Avatar Profiles, so menu changes never edit the assets; they're kept per avatar in PlayerPrefs
  (`AvatarSettingsStore`, only the changed fields - later asset edits still come through; "Reset" forgets them).
  `DialogueController.PersonaChanged()` re-applies prompts after an edit. Startup progress: `StartupProgress`.
- Debug overlay (`DebugOverlay`, F1 or the menu; drawn as a card by CompanionUI, IMGUI on its own): avatar/mode/voices/lip-sync profile, mic/brain/voice/body
  state, last turn (heard + language, asked, reply, timings: reply ready / first sound / done, transcription time),
  live lip-sync (sound, level, jaw), and the brain's log lines (router, knowledge-gate scores, open chat, warnings).
- Lip-sync per voice: a profile learnt from Kavya heard Kevin's male voices mostly as SILENCE (69-88% of voiced
  frames -> mouth shut). Each voice has its own `uLipSync-Profile-<voice>.asset` (clips:
  `tools/make_lipsync_calibration.py <voice>...`, then `tools/calibrate_lipsync.cs` in Play mode; vinaya 83% of
  sounds right, am_michael 46% - Kokoro's drawn-out sounds are weak samples, still far better than 7%);
  `LipSyncVoiceProfiles` on SpeechOutput/LipSync switches uLipSync's profile to `SpeechOutputController.CurrentVoice`
  (Inspector "Find calibrated profiles"; fallback Kavya). A new voice needs these two steps.
- CC3+/CC4/CC5 mouths open with the jaw BONE (CC_Base_JawRoot carries lower teeth + tongue); their exported
  V_Open/Jaw_Open shapes barely part the lips. `AvatarLipSyncTuning.jawBoneDegrees` = degrees on a full loud "aa"
  x Jaw Strength (Kevin 14 x 0.8: jaw p50 ~1 deg between sounds, p90 6, max 10); 0 for ActorScan/ARKit faces (Party F).
  Kevin's stare was his own neutral face (lids rest above the iris - same with AvatarEyes off; its eyelid-follow
  shapes were only 4-5%): `AvatarCharacter.restingEyelids` (AvatarBlink applies it) rests the lids - CC4/CC5 Eye_Relax_L/R
  (also lifts the lower lid), else the blink shape at 37% of it. Kevin 0.4 (0.7+ looks sleepy), Party F 0.
  Turned in LateUpdate after the body animation. Kevin's male idle clip poses his jaw 9.5 deg from bind (closed);
  the stand-talk clip leaves it at bind.
  CC exports can carry a scene camera (Kevin's "render_focus_") - fix_cc5_materials.cs switches cameras/lights off.
- SpeechOutput's AudioSource had Play On Awake: `isPlaying` was true with no clip, so the avatar sat in Talk from
  startup. `IsSpeaking` now requires a playing sentence (`PlayingStream`), and Play On Awake is turned off in Awake.
- In DialogueTest: `Avatar` + uLipSync on `SpeechOutput` (reads the voice AudioSource; profile calibrated to
  Veena Kavya by `tools/calibrate_lipsync.cs` from sustained vowels in `Assets/Avatar LipSync/Calibration` -
  vowel frames right 45% -> 70%, with standardization + cosine; E stays weak, it looks like I anyway; consonant
  groups M (m/b/p) 82%, F (f/v) 54%, S (s/sh) 64% added so lips press/touch teeth) -> `AvatarLipSync` on the
  stage (replaced uLipSyncBlendShape; tuning panel with Preview per sound, Auto-map, "Say test sentence"
  in Play mode; jaw shapes scaled separately, jaw now peaks ~18% open); mouth shapes found by name by
  `AvatarFace` (CC/ActorCore, ARKit, Meta visemes, VRM - the base for plug-and-play characters), jaw kept to
  ~30% open at most (user asked twice for subtler). Lip-sync measured with `tools/test_lipsync_measure.cs`
  (per-frame trace; before -> after): mouth 105 ms behind the voice -> ~20 ms, open in silence 60% -> 19%,
  jitter 5.2 -> 3.6, jaw >20% open 34% -> 16% of speech. Fixes: volume range -2.1..-0.95 (Veena speech runs
  -1.84..-1.02; the old -2.5..-1.2 left it "fully open" half the time), smoothness 0.025, usePhonemeBlend,
  and `LipSyncLookahead`: uLipSync sits on SpeechOutput/LipSync (a child, so it doesn't hear the live output)
  and is fed the speech 70 ms AHEAD of playback from the PcmStream, lined up by the audio clock. Don't use
  PlayScheduled for the streamed clip - it read seconds ahead and played silence where generation lagged.
  Rare multi-second mid-sentence silences were seen twice and not reproduced; PcmStream logs each gap
  ("the voice fell behind ... [ms: clip at, had]") to catch it.
- Body: `AvatarAnimator` picks the state - Talk while a sentence plays (held 0.8 s through pauses inside a
  reply), Listen while the mic records, Think while transcribing/answering, else Idle; a state without clips
  falls back to Idle. Leaves Talk ~0.02 s after the voice stops. `AvatarBlink` (random blinks),
  `AvatarEyes` (eyeballs are real geometry on CC_Base_L/R_Eye - the Animator resets them each
  frame, so gaze is set in LateUpdate: at the camera, small flicks, glances aside/down; eye-look blendshapes
  A06-A13 at 0.7 so lids follow; upward gaze capped at 6 deg - looking up showed white under the iris).
- Listening aid (Claude can't hear audio): `SpeechReviewRecorder` on SpeechOutput saves every spoken sentence
  (WAV + text) to `SpeechReview/<session>/`, F8 flags the last reply; `tools/review_speech.cs` transcribes the
  latest session (flagged replies only if any) and lists words that didn't come through. Pad clips with ~0.5 s
  silence before Whisper - its voice detection drops clips that start at once (the "flaky Whisper" of earlier
  tests: an English reply went from 82% to 97% of words heard back just by padding).
  Sharper check: `TTSServer/speech_check.py` (Unity: Tools > Voice > Check Recorded Speech) - a phoneme
  recogniser (facebook/wav2vec2-xlsr-53-espeak-cv-ft, HF cache) aligned against espeak's expected sounds in
  broad groups flags skipped/unclear WORDS; UTMOS22 (torch hub cache, tarepan/SpeechMOS v1.2.0) scores
  naturalness 1-5 (English-trained: compare, don't grade). Validated: it ranked the Veena anchor styles like
  Whisper did (clarity 82% vs 69%). Baseline 2026-09-28: Veena English 92% / 4.28, Veena Hindi 82% / 3.84.
  Also takes any folder with manifest.json {"file.wav": "text"} for A/B tests.
  2026-09-28 Hindi pass (29 real reply sentences): worst were web addresses/emails ("altscape.in" 53%) -> text
  cleanup moved to `TTSServer/speech_text.py` (shared by server and checker): websites "Altscape dot in", name
  parts capitalised so Veena respells them, "&" -> और/and, spaced dashes -> pauses. Sampling (temp 0.1/0.25/0.4)
  and Q8 vs Q4 made no difference (78-81%). English words inside Hindi replies come through better (82%) than
  the Hindi words (76%, much of it espeak-vs-natural-speech strictness); remaining flags are mostly कर/पर. Key light from the
  face's front. Test: `tools/test_avatar_lipsync.cs` (mouth moving ~80% of speech, Talk 98%, back to Idle).

## Current project state (as of last session)
- Unity project already exists at this path: `6000.6.0f1`, URP, git-initialized (`Initial check-in` commit).
- `Packages/manifest.json` already has the three git-URL packages added:
  `ai.undream.llm` (LLMUnity), `com.whisper.unity` (whisper.unity), `com.hecomi.ulipsync` (uLipSync).
  **These still need Package Manager to resolve them** — requires Git installed on this machine.
- Folder skeleton created under `Assets/`: `Scripts/Dialogue`, `Scripts/Audio`, `Scripts/Avatar`,
  `Prefabs`, `StreamingAssets/Models/Whisper`, `StreamingAssets/Models/LLM`.
- No character, no scripts, no models downloaded yet — this is all still ahead.

## Build order
0. Confirm the three packages resolved cleanly in Package Manager (check Console for errors).
1. Get the CC4/ActorCore character imported and rendering with its blendshapes intact — no AI yet.
2. Add Mixamo idle/gesture clips + Animation Rigging for gaze/blink so it's not static.
3. Wire uLipSync to one manually generated Indic Parler-TTS audio clip; confirm mouth tracking.
4. Build the dialogue brain with LLMUnity: real topic list, persona, refusal bank, the two-layer
   gate above. Test entirely through a text field first — this is the step that proves the product
   actually stays on-topic, before audio is even involved.
5. Wire up whisper.unity (both GGML models) + push-to-talk mic capture, feeding transcripts into
   step 4's brain.
6. Stand up the Indic Parler-TTS local server; Unity sends it reply text over localhost, gets a WAV
   back, feeds it into the AudioSource + uLipSync.
7. Run it end to end: speak → hear → gate → answer/refuse → speak → lips move.

## Things to watch
- Git must be installed on this Windows machine for Unity to resolve the git-URL packages.
- Check license terms before shipping: the chosen LLM weights, any purchased CC4/ActorCore
  character, and any Mixamo motion pack.
- Generic ASR is noticeably weaker on Indian-accented English than US/UK accents — test with real
  speakers early, not just clean studio audio.
