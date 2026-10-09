"""Contact sheets of tools/manual/shots/*.png (to review a capture at a glance): python contact_sheet.py [out_dir] [per_sheet]"""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

here = Path(__file__).parent
shots = here / "shots"
out = Path(sys.argv[1]) if len(sys.argv) > 1 else here / "shots" / "_sheets"
per = int(sys.argv[2]) if len(sys.argv) > 2 else 8
out.mkdir(parents=True, exist_ok=True)
files = sorted(p for p in shots.glob("*.png") if not p.name.startswith("_"))
H = 900
for s in range(0, len(files), per):
    group = files[s:s + per]
    thumbs = []
    for p in group:
        im = Image.open(p).convert("RGB")
        w = max(1, round(im.width * H / im.height))
        thumbs.append((p.stem, im.resize((w, H), Image.LANCZOS)))
    width = sum(t.width for _, t in thumbs) + 12 * (len(thumbs) + 1)
    sheet = Image.new("RGB", (width, H + 50), (255, 255, 255))
    d = ImageDraw.Draw(sheet)
    x = 12
    for name, t in thumbs:
        sheet.paste(t, (x, 40))
        d.text((x, 12), name, fill=(0, 0, 0))
        x += t.width + 12
    sheet.save(out / f"sheet_{s // per + 1:02d}.png")
print(len(files), "shots ->", out)
