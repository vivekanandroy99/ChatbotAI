"""Page previews of out/AltcoreBot-Staff-Manual.pdf for checking the layout: python preview.py <out_dir> [first] [last] [per_sheet] [dpi]
Writes sheet_NN.png (several pages side by side) and, with per_sheet=1, one big PNG per page."""
import sys
from pathlib import Path

import pypdfium2 as pdfium
from PIL import Image

pdf = Path(__file__).parent / "out" / "AltcoreBot-Staff-Manual.pdf"
out = Path(sys.argv[1])
out.mkdir(parents=True, exist_ok=True)
doc = pdfium.PdfDocument(str(pdf))
first = int(sys.argv[2]) if len(sys.argv) > 2 else 1
last = int(sys.argv[3]) if len(sys.argv) > 3 else len(doc)
per = int(sys.argv[4]) if len(sys.argv) > 4 else 4
dpi = int(sys.argv[5]) if len(sys.argv) > 5 else 80
pages = list(range(first, last + 1))
for s in range(0, len(pages), per):
    group = pages[s:s + per]
    imgs = [doc[p - 1].render(scale=dpi / 72).to_pil().convert("RGB") for p in group]
    w = sum(i.width for i in imgs) + 10 * (len(imgs) - 1)
    sheet = Image.new("RGB", (w, max(i.height for i in imgs)), (120, 120, 120))
    x = 0
    for i in imgs:
        sheet.paste(i, (x, 0))
        x += i.width + 10
    name = f"p{group[0]:02d}.png" if per == 1 else f"sheet_{group[0]:02d}-{group[-1]:02d}.png"
    sheet.save(out / name)
print(len(doc), "pages in the PDF;", len(pages), "previewed ->", out)
