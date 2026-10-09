# Staff manual (PDF)

`docs/AltcoreBot-Staff-Manual.pdf` is built from real screenshots of the running app plus the text in `build_manual.py`.
Re-make it whenever the menu or a bot changes:

1. **Screenshots** - Unity in Edit mode, then Play mode (wait for the loading screen to finish), then:
   `unity command eval_file tools/manual/capture_manual_shots.cs` (about 5 minutes: it asks the real bots a few questions).
   Progress is in `tools/manual/shots/_capture.txt` (`ALLDONE` at the end). To redo only some pages, put their ids (see the
   `pages` list in the script; conversation pictures: `maya_en`, `maya_refuse`, `maya_hi`, `iris_chat`, `pearl_pni`) one per line in
   `tools/manual/shots/_only.txt`, run it, and delete the file afterwards. Stop Play mode when done.
   The script uses made-up sample questions for the Report / Learning pages and doesn't save conversations; it also redacts the
   default sign-in name from the pictures (see `manual_lib.py`).
2. **PDF** - `python tools/manual/build_manual.py` (needs `pip install pillow pypdf` and Microsoft Edge). Output:
   `tools/manual/out/AltcoreBot-Staff-Manual.pdf`; copy it to `docs/`.
3. **Check** - `python tools/manual/preview.py <folder> 1 50 4 70` renders page sheets (needs `pypdfium2`);
   `contact_sheet.py` shows all the captured pictures.

`ask_samples.cs` checks that the example questions printed in the manual really get answers.
`shots/` and `out/` are not in git (they are rebuilt).
