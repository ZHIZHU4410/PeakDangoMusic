"""Bake numeric UI glyphs; the source font file is never bundled.

Requires Pillow. Run with --font pointing to a locally installed monospace TTF.
"""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

CHARS = "0123456789.-+ msHz"
CELL = (40, 72)
ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--font", type=Path, required=True)
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    atlas = Image.new("RGBA", (CELL[0] * len(CHARS), CELL[1]))
    draw = ImageDraw.Draw(atlas)
    font = ImageFont.truetype(str(args.font), 60)
    for i, char in enumerate(CHARS):
        draw.text((i * CELL[0] + CELL[0] / 2, CELL[1] / 2), char,
                  font=font, fill="white", anchor="mm")
        if char != " ":
            assert atlas.crop((i*CELL[0], 0, (i+1)*CELL[0], CELL[1])).getbbox(), char
    output = ROOT / "source/PeakDangoMusic/Assets/NumericGlyphs.png"
    output.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(output)
    if args.preview:
        rows = ["1.3", "1.7", "10.0", "25", "0.80", "24 Hz", "-10000 ms"]
        preview = Image.new("RGBA", (240, len(rows)*36), "#141c24")
        for row, text in enumerate(rows):
            width = min(10, 96/len(text))
            for i, char in enumerate(text):
                glyph = CHARS.index(char)
                cell = atlas.crop((glyph*CELL[0], 0, (glyph+1)*CELL[0], CELL[1]))
                height=width*CELL[1]/CELL[0]
                cell = cell.resize((round(width), round(height)), Image.Resampling.BILINEAR)
                preview.alpha_composite(cell, (round(220-width*len(text)+i*width), round(row*36+18-height/2)))
        args.preview.parent.mkdir(parents=True, exist_ok=True)
        preview.save(args.preview)
    print(f"Verified {len(CHARS)-1} visible glyphs; wrote {output}")

if __name__ == "__main__":
    main()
