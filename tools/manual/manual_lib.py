"""Helpers for build_manual.py: pictures (shots/*.png -> out/img/*.jpg, with redactions and crops), numbered marks drawn over
them, the page layout (CSS) and the PDF (Microsoft Edge, headless). No third-party packages except Pillow (+ pypdf for the
table of contents page numbers)."""
import html
import json
import re
import subprocess
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).parent
SHOTS = HERE / "shots"
OUT = HERE / "out"
IMG = OUT / "img"
ROOT = HERE.parent.parent
FONTS = ROOT / "Assets" / "UI" / "Companion" / "Fonts"
LOGO = ROOT / "Assets" / "UI" / "Companion" / "Brand" / "splash-logo.png"
EDGE = [r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"]

E = html.escape


# ------------------------------------------------------------------ pictures

class Shots:
    """The captured pictures and where each marked option is (manifest.json)."""

    def __init__(self):
        IMG.mkdir(parents=True, exist_ok=True)
        entries = json.loads((SHOTS / "manifest.json").read_text(encoding="utf-8"))
        self.info = {e["id"]: e for e in entries}
        self.done = {}

    def exists(self, pid):
        return pid in self.info

    def views(self, pid):
        """Pictures of a page: `pid`, or `pid_1`, `pid_2`... in order."""
        if pid in self.info:
            return [pid]
        found = sorted((k for k in self.info if re.fullmatch(re.escape(pid) + r"_\d+", k)), key=lambda k: int(k.rsplit("_", 1)[1]))
        return found

    def numbers(self, pid):
        """label -> number, over all pictures of a page."""
        out = {}
        for v in self.views(pid):
            for m in self.info[v]["marks"]:
                if not m["redact"]:
                    out[m["label"]] = m["n"]
        return out

    def prepare(self, sid, width=None, crop=None, name=None, quality=86, frame=True):
        """-> (file name, width px, height px, marks) of picture `sid` as a JPEG in out/img. crop = (x0, y0, x1, y1) in
        source pixels; marks are re-mapped to the crop (marks outside it are dropped). frame: a thin rounded grey border is
        drawn into the picture itself (a CSS border left stray lines at page breaks in the PDF)."""
        key = (sid, width, crop, name, frame)
        if key in self.done:
            return self.done[key]
        meta = self.info[sid]
        im = Image.open(SHOTS / f"{sid}.png").convert("RGB")
        W, H = im.size
        marks = [dict(m) for m in meta["marks"]]
        # Redactions (the sign-in name of the default account): painted into the picture itself.
        d = ImageDraw.Draw(im)
        for m in marks:
            if not m["redact"]:
                continue
            x0, y0 = int(m["x"] * W) + 6, int(m["y"] * H) + 6
            x1, y1 = int((m["x"] + m["w"]) * W * 0.86), int((m["y"] + m["h"]) * H) - 6
            if m["label"].startswith("starts:Sign out"):
                # "Sign out (<name>)": the name is replaced by the word "name".
                x0, x1 = int(m["x"] * W) + 6, int((m["x"] + m["w"]) * W) - 6
                region = im.crop((x0, y0, x1, y1))
                ink = max(region.getdata(), key=lambda px: px[0] - px[2])        # the orange of the text
                bg = im.getpixel((x0 + 2, y0 + 2))
                d.rectangle([x0, y0, x1, y1], fill=bg)
                from PIL import ImageFont
                font = ImageFont.truetype(str(FONTS / "Inter-Regular.ttf"), max(10, int((y1 - y0) * 0.50)))
                text = "Sign out (name)"
                box = d.textbbox((0, 0), text, font=font)
                d.text(((x0 + x1) / 2 - (box[2] - box[0]) / 2 - box[0], (y0 + y1) / 2 - (box[3] - box[1]) / 2 - box[1]), text, fill=ink, font=font)
                continue
            bg = im.getpixel((min(W - 1, x1 + 4), min(H - 1, y0 + 4)))
            d.rectangle([x0, y0, x1, y1], fill=bg)
        marks = [m for m in marks if not m["redact"]]
        if crop:
            x0, y0, x1, y1 = crop
            im = im.crop(crop)
            cw, ch = x1 - x0, y1 - y0
            kept = []
            for m in marks:
                mx0, my0 = m["x"] * W - x0, m["y"] * H - y0
                mx1, my1 = mx0 + m["w"] * W, my0 + m["h"] * H
                mx0, my0, mx1, my1 = max(0, mx0), max(0, my0), min(cw, mx1), min(ch, my1)
                if mx1 - mx0 < 8 or my1 - my0 < 8:
                    continue
                kept.append({**m, "x": mx0 / cw, "y": my0 / ch, "w": (mx1 - mx0) / cw, "h": (my1 - my0) / ch})
            marks = kept
        if width and im.width > width:
            im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
        if frame:
            r, bw = max(6, int(im.width * 0.022)), max(2, im.width // 330)
            mask = Image.new("L", im.size, 0)
            ImageDraw.Draw(mask).rounded_rectangle([0, 0, im.width - 1, im.height - 1], r, fill=255)
            plate = Image.new("RGB", im.size, (255, 255, 255))
            plate.paste(im, (0, 0), mask)
            im = plate
            ImageDraw.Draw(im).rounded_rectangle([0, 0, im.width - 1, im.height - 1], r, outline=(207, 201, 195), width=bw)
        fname = f"{name or sid}.jpg"
        im.save(IMG / fname, "JPEG", quality=quality, optimize=True, subsampling=0 if im.width < 1100 else 2)
        res = (fname, im.width, im.height, marks)
        self.done[key] = res
        return res


def figure(shots, sid, width_mm, width=None, crop=None, name=None, marks=True, caption=None, small=False, only_marks=None, numbers=None):
    """HTML of one picture with its numbered outlines drawn over it. numbers = label -> number to show (marks whose label
    isn't in it are left out); without it the numbers the capture gave are used."""
    fname, w, h, ms = shots.prepare(sid, width=width, crop=crop, name=name)
    boxes = ""
    if marks:
        for m in ms:
            if only_marks is not None and m["label"] not in only_marks:
                continue
            n = m["n"]
            if numbers is not None:
                if m["label"] not in numbers:
                    continue
                n = numbers[m["label"]]
            boxes += (f'<div class="mk" style="left:{m["x"] * 100:.2f}%;top:{m["y"] * 100:.2f}%;width:{m["w"] * 100:.2f}%;'
                      f'height:{m["h"] * 100:.2f}%"><i>{n}</i></div>')
    cap = f'<figcaption>{caption}</figcaption>' if caption else ""
    return (f'<figure class="shot{" sm" if small else ""}" style="width:{width_mm}mm"><div class="frame"><img src="img/{fname}" alt="">'
            f'{boxes}</div>{cap}</figure>')


def plain_image(path, name, width=None, quality=88):
    """A picture from anywhere (logo etc.) as out/img/<name>."""
    im = Image.open(path)
    if width and im.width > width:
        im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
    im.save(IMG / name)
    return f"img/{name}"


# ------------------------------------------------------------------ layout

CSS = r"""
@font-face { font-family: 'Inter'; font-weight: 400; src: url('FONTDIR/Inter-Regular.ttf'); }
@font-face { font-family: 'Inter'; font-weight: 600; src: url('FONTDIR/Inter-SemiBold.ttf'); }
@font-face { font-family: 'Inter'; font-weight: 700; src: url('FONTDIR/Inter-SemiBold.ttf'); }
:root { --ink:#1d1b1a; --muted:#625c57; --orange:#e8590c; --orange2:#f26a1b; --soft:#fdf0e8; --line:#e4dfda; --dark:#161413; --grey:#f4f2ef; }
@page { size: A4; margin: 17mm 16mm 19mm 16mm;
  @bottom-left { content: "AltcoreBot  ·  Staff manual"; font: 600 7.5pt Inter, sans-serif; color: #8b847e; }
  @bottom-right { content: counter(page); font: 600 8pt Inter, sans-serif; color: #8b847e; } }
@page cover { margin: 0; @bottom-left { content: none; } @bottom-right { content: none; } }
* { box-sizing: border-box; }
html { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
body { font: 400 9.6pt/1.5 Inter, 'Nirmala UI', 'Segoe UI', sans-serif; color: var(--ink); margin: 0; }
p { margin: 0 0 2.4mm; }
b, strong { font-weight: 600; }
a { color: inherit; text-decoration: none; }
code, .k { font: 600 8.6pt Consolas, 'Cascadia Mono', monospace; background: var(--grey); border: .2mm solid var(--line); border-radius: 1mm; padding: 0 1.2mm; white-space: nowrap; }
.tok { font-size: 1px; line-height: 0; color: transparent; display: block; height: 0; overflow: hidden; }

/* cover */
.cover { page: cover; width: 210mm; height: 297mm; background: #fff; color: var(--ink); position: relative; overflow: hidden; break-after: page; }
.cover::before { content: ''; position: absolute; left: 0; top: 0; right: 0; height: 5mm; background: var(--orange2); }
.cover .logo { position: absolute; left: 22mm; top: 24mm; width: 74mm; }
.cover h1 { position: absolute; left: 22mm; top: 78mm; margin: 0; font: 600 40pt/1.08 Inter, sans-serif; letter-spacing: -.5pt; border: 0; padding: 0; color: var(--ink); }
.cover h1 span { color: var(--orange2); }
.cover .sub { position: absolute; left: 22mm; top: 124mm; width: 140mm; font-size: 12.5pt; line-height: 1.5; color: #4a443f; }
.cover .rule { position: absolute; left: 22mm; top: 116mm; width: 28mm; height: 1.2mm; background: var(--orange2); border-radius: 1mm; }
.cover .faces { position: absolute; left: 22mm; right: 22mm; top: 164mm; display: flex; gap: 6mm; }
.cover .faces img { width: 54.5mm; border-radius: 3mm; display: block; }
.cover .foot { position: absolute; left: 22mm; bottom: 16mm; font-size: 9pt; color: #7a736d; }

/* headings */
h1.chapter { break-before: page; margin: 0 0 6mm; padding: 0 0 4mm; border-bottom: .8mm solid var(--orange2); font: 600 24pt/1.15 Inter, sans-serif; letter-spacing: -.3pt; }
h1.chapter small { display: block; font: 600 8.5pt Inter, sans-serif; color: var(--orange); letter-spacing: 1.2pt; text-transform: uppercase; margin-bottom: 2mm; }
h1.chapter + .lead { font-size: 11pt; color: var(--muted); margin-bottom: 7mm; }
h2 { font: 600 14.5pt/1.25 Inter, sans-serif; margin: 8mm 0 3mm; break-after: avoid; }
h2::before { content: ""; display: inline-block; width: 1.6mm; height: 4.4mm; background: var(--orange2); border-radius: .6mm; margin-right: 2.4mm; vertical-align: -.7mm; }
h3 { font: 600 12pt/1.3 Inter, sans-serif; margin: 6mm 0 1.5mm; break-after: avoid; }
h4 { font: 600 9pt Inter, sans-serif; margin: 4mm 0 1.2mm; text-transform: uppercase; letter-spacing: .8pt; color: var(--orange); break-after: avoid; }
.crumb { font: 600 7.4pt Inter, sans-serif; letter-spacing: .6pt; text-transform: uppercase; color: #8b847e; margin: 0 0 .5mm; break-after: avoid; }
.lead { font-size: 10.4pt; color: #3b3632; }
ul, ol { margin: 0 0 3mm; padding-left: 5mm; }
li { margin: 0 0 1.2mm; }
.nobreak { break-inside: avoid; }

/* boxes */
.note { break-inside: avoid; border-left: 1.2mm solid var(--orange2); background: var(--soft); border-radius: 0 1.6mm 1.6mm 0; padding: 2.6mm 4mm; margin: 3mm 0 4mm; }
.note > :last-child { margin-bottom: 0; }
.note .t { font: 600 7.6pt Inter, sans-serif; letter-spacing: .9pt; text-transform: uppercase; color: var(--orange); display: block; margin-bottom: .6mm; }
.note.info { border-color: #4a78b5; background: #edf3fa; } .note.info .t { color: #35618f; }
.note.warn { border-color: #c62828; background: #fdecec; } .note.warn .t { color: #b02020; }
.note.ok { border-color: #2e7d4f; background: #ebf6ef; } .note.ok .t { color: #25683f; }

/* tables */
table { border-collapse: collapse; width: 100%; margin: 2mm 0 4mm; font-size: 8.8pt; }
th { text-align: left; font: 600 7.8pt Inter, sans-serif; letter-spacing: .5pt; text-transform: uppercase; color: var(--muted); border-bottom: .5mm solid var(--ink); padding: 1.4mm 2mm; }
td { padding: 1.7mm 2mm; border-bottom: .25mm solid var(--line); vertical-align: top; }
tr { break-inside: avoid; }
td:first-child { font-weight: 600; }
table.plain td:first-child { font-weight: 400; }

/* pictures with numbered outlines */
figure.shot { margin: 0; }
figure.shot .frame { position: relative; line-height: 0; }
figure.shot img { width: 100%; display: block; }
figure.shot figcaption { font-size: 7.6pt; line-height: 1.35; color: var(--muted); margin-top: 1.4mm; text-align: center; }
.mk { position: absolute; border: .55mm solid var(--orange2); border-radius: 1.2mm; background: rgba(242,106,27,.09); }
.mk i { position: absolute; left: -2.2mm; top: -2.2mm; width: 4.4mm; height: 4.4mm; border-radius: 50%; background: var(--orange2); color: #fff; font: 700 7.2pt/4.4mm Inter, sans-serif; text-align: center; font-style: normal; box-shadow: 0 0 0 .5mm #fff; }
figure.sm .mk i { width: 3.8mm; height: 3.8mm; left: -1.9mm; top: -1.9mm; font-size: 6.4pt; line-height: 3.8mm; }

/* a menu page: the picture beside its numbered explanation */
.mp { margin: 0 0 7mm; }
.mp.side { break-inside: avoid; display: grid; grid-template-columns: var(--figw) 1fr; column-gap: 8mm; align-items: start; }
.mp .views { break-inside: avoid; display: flex; gap: 4mm; align-items: flex-start; margin-bottom: 4mm; }
.mp .text > :first-child { margin-top: 0; }
ol.co { list-style: none; padding: 0; margin: 2mm 0 3mm; }
ol.co li { position: relative; padding-left: 8mm; margin-bottom: 2.2mm; break-inside: avoid; }
ol.co li .n { position: absolute; left: 0; top: .3mm; width: 5mm; height: 5mm; border-radius: 50%; background: var(--orange2); color: #fff; font: 700 7.6pt/5mm Inter, sans-serif; text-align: center; }
ol.co li .n.dot { background: #c9c2bb; }
ol.co li b { font-weight: 600; }
.grid { display: grid; gap: 5mm; }
.g2 { grid-template-columns: 1fr 1fr; } .g3 { grid-template-columns: 1fr 1fr 1fr; }
.cell { break-inside: avoid; }
.cell h4 { margin-top: 0; }
.flow { display: flex; align-items: stretch; gap: 0; margin: 3mm 0 5mm; break-inside: avoid; }
.flow .box { flex: 1; border: .3mm solid var(--line); background: var(--grey); border-radius: 2mm; padding: 2.4mm 2.6mm; font-size: 8.2pt; line-height: 1.35; }
.flow .box b { display: block; color: var(--orange); font-size: 7.6pt; letter-spacing: .6pt; text-transform: uppercase; margin-bottom: .6mm; }
.flow .arr { width: 5mm; display: flex; align-items: center; justify-content: center; color: var(--orange2); font-weight: 700; }
.bots { display: grid; grid-template-columns: repeat(3, 1fr); gap: 5mm; margin: 3mm 0 4mm; break-inside: avoid; }
.bots .bot { break-inside: avoid; }
.bots figure { margin: 0; }
.bots img { width: 100%; border-radius: 2.2mm; display: block; }
h2 + p, h2 + .lead { break-after: avoid; }
.bots h4 { margin: 2mm 0 .5mm; color: var(--ink); font-size: 10.5pt; text-transform: none; letter-spacing: 0; }
.bots p { font-size: 8.4pt; color: var(--muted); margin: 0 0 1mm; line-height: 1.4; }
.bots .tag { display: inline-block; font: 600 7pt Inter, sans-serif; letter-spacing: .6pt; text-transform: uppercase; background: var(--soft); color: var(--orange); border-radius: 1mm; padding: .3mm 1.6mm; margin-bottom: 1.2mm; }
.states { display: grid; grid-template-columns: 1fr 1fr; gap: 5mm 6mm; margin: 3mm 0 4mm; break-inside: avoid; }
.states figure { break-inside: avoid; }
.steps { counter-reset: s; list-style: none; padding: 0; }
.steps > li { counter-increment: s; position: relative; padding-left: 9mm; margin-bottom: 2.6mm; break-inside: avoid; }
.steps > li::before { content: counter(s); position: absolute; left: 0; top: -.2mm; width: 6mm; height: 6mm; border-radius: 50%; background: var(--ink); color: #fff; font: 600 8pt/6mm Inter, sans-serif; text-align: center; }
.q { font-style: italic; color: #3b3632; }
.chip { display: inline-block; font: 600 7.6pt Inter, sans-serif; background: var(--grey); border: .25mm solid var(--line); border-radius: 5mm; padding: .2mm 2.2mm; }

/* contents */
.toc { columns: 2; column-gap: 10mm; font-size: 8.8pt; }
.toc div { break-inside: avoid; display: flex; align-items: baseline; gap: 1.5mm; margin: 0 0 .9mm; }
.toc .l1 { font-weight: 600; margin-top: 3mm; font-size: 9.6pt; }
.toc .l2 { padding-left: 3mm; }
.toc .l3 { padding-left: 7mm; font-size: 8.2pt; color: var(--muted); }
.toc .dots { flex: 1; border-bottom: .25mm dotted #b9b2ab; transform: translateY(-.8mm); }
.toc .pg { font-variant-numeric: tabular-nums; }
"""


def page_html(body, title="AltcoreBot - Staff manual"):
    css = CSS.replace("FONTDIR", FONTS.as_posix().replace(" ", "%20"))
    return (f'<!doctype html><html lang="en"><head><meta charset="utf-8"><title>{E(title)}</title><style>{css}</style></head>'
            f'<body>{body}</body></html>')


def edge():
    for p in EDGE:
        if Path(p).exists():
            return p
    raise SystemExit("Microsoft Edge not found")


def print_pdf(html_file, pdf_file):
    pdf_file.parent.mkdir(parents=True, exist_ok=True)
    if pdf_file.exists():
        pdf_file.unlink()
    cmd = [edge(), "--headless=new", "--disable-gpu", "--no-pdf-header-footer", "--allow-file-access-from-files",
           "--virtual-time-budget=30000", f"--print-to-pdf={pdf_file}", html_file.resolve().as_uri()]
    subprocess.run(cmd, check=True, timeout=300, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    if not pdf_file.exists():
        raise SystemExit("Edge did not write the PDF")


def pdf_pages_text(pdf_file):
    from pypdf import PdfReader
    return [p.extract_text() or "" for p in PdfReader(str(pdf_file)).pages]
