---
name: mac-version-plan
description: "User wants a separate macOS version later (MacBook Air M5, 24 GB) - English only, offline lightweight + optional online AI via API keys; not started"
metadata:
  node_type: memory
  type: project
  originSessionId: b4e751f1-b40e-48b7-a3ee-293480808468
  modified: 2026-10-08T13:23:06.026Z
---

2026-10-08: User has a MacBook Air M5, 24 GB RAM. Wants (LATER - "dont make it now") a separate Mac version of the
companion, distinct from the Windows kiosk build:
- English only (Hindi/Veena may be dropped - "dont want hindi anyways").
- Offline mode, lightweight (Metal instead of CUDA; no Veena).
- Online mode: user enters API keys for cloud AI (brain, maybe voice) - user thinks this suits the Mac best.
Second Mac: MacBook Pro M1 Pro 16 GB (offline tight - skip reranker, maybe smaller Whisper; online easy).
Existing menu page Advanced > "Online models & API keys" (CompanionMenu.BuildOnlineAi) is a non-working PREVIEW -
user wants it made real (likely both Windows and Mac).
2026-10-08: Mac version code written (memory.txt (f), MAC-SETUP.md) and pushed to github.com/vivekanandroy99/ChatbotAI
(PUBLIC - owner said that's fine). Untested on a Mac. Claude's notes copied to docs/claude-memory for the Mac.
User has NO API keys yet (will buy later) - build the online part provider-neutral, test once a key exists.
Windows version stays as is (CUDA, Veena, kiosk).

**Why:** user wants to run/show it on their Mac; Windows build is tied to CUDA/NVIDIA and Windows-only features.
**How to apply:** when asked to start it, make a separate copy (don't touch the Windows project); keep the local
document-search gate in online mode too; fanless Air throttles under sustained load. See [[next-improvements-backlog]].
