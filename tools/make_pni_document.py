"""Makes the P&I bots' document from the owner's P&I knowledge base (2026-10-07): the same file without the sections
written for the chatbot team (Contents, 1 How to use, 2 Core chatbot instructions, 13 Information gaps, 14 Pre-launch
evaluation prompts, 15 Source register) - Gemma-4B read their notes out to visitors. Section 2's rules are in the bots'
Personality (alwaysDo / neverDo, tools/setup_pni_bots.cs).
Run:  TTSServer\venv\Scripts\python.exe tools\make_pni_document.py "<original .docx>" "Assets\KnowledgeDocuments\pni_female\Pavilions and Interiors Knowledge Base.docx"
then tools/setup_pni_bots.cs (copies it to both bots' knowledge folders)."""
import sys, re, docx
from docx.text.paragraph import Paragraph
src, dst = sys.argv[1], sys.argv[2]
d = docx.Document(src)
# Sections written for the chatbot team, not visitors (read out by the small offline brain).
DROP = re.compile(r"^(Contents|1\.\s|2\.\s|13\.\s|14\.\s|15\.\s)")
body = d.element.body
dropping, removed, kept_sections = False, [], []
for block in list(body.iterchildren()):
    tag = block.tag.rsplit("}", 1)[-1]
    if tag == "p":
        p = Paragraph(block, d)
        style = p.style.name if p.style is not None else ""
        if style in ("Heading 1", "Heading 2") and p.text.strip():
            dropping = bool(DROP.match(p.text.strip()))
            (removed if dropping else kept_sections).append(p.text.strip())
    if tag == "sectPr":
        continue
    if dropping:
        body.remove(block)
d.save(dst)
print("removed:", removed)
print("kept:", kept_sections)

