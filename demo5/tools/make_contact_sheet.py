"""Review contact sheet: a grid of captured stills with a small caption under each (review material, not game art).

Run:  python tools/make_contact_sheet.py <out.png> <columns> <cell width> <image>=<caption> ...
"""
import sys

from PIL import Image, ImageDraw, ImageFont


def sheet(out, columns, width, items):
    font = ImageFont.truetype("C:/Windows/Fonts/malgunbd.ttf", 22)
    thumbs = []
    for path, caption in items:
        im = Image.open(path).convert("RGB")
        thumbs.append((im.resize((width, round(im.height * width / im.width)), Image.LANCZOS), caption))
    h = thumbs[0][0].height + 40
    rows = (len(thumbs) + columns - 1) // columns
    canvas = Image.new("RGB", (columns * (width + 12) + 12, rows * (h + 12) + 12), (24, 34, 36))
    draw = ImageDraw.Draw(canvas)
    for i, (im, caption) in enumerate(thumbs):
        x, y = 12 + (i % columns) * (width + 12), 12 + (i // columns) * (h + 12)
        canvas.paste(im, (x, y))
        draw.text((x + 8, y + im.height + 6), caption, fill=(236, 228, 208), font=font)
    canvas.save(out)
    print(out, canvas.size)


if __name__ == "__main__":
    out, columns, width = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
    items = [a.split("=", 1) for a in sys.argv[4:]]
    sheet(out, columns, width, items)
