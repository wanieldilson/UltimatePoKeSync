#!/usr/bin/env python3
"""Derive the application icons from the source artwork.

Run after changing src/UltimatePoKeSync.App/Assets/upsync-icon.png:

    python3 build/make-icons.py

Produces, next to the source:

  app-icon.png  1024x1024, the artwork inset and centred. The source runs almost edge to
                edge, which makes it look oversized beside other icons and turns to mush
                at 16 px. Everything else is derived from this one.
  app.ico       the Windows executable icon, 16 to 256 px.

The macOS .icns is built by build/make-icns.sh, which needs macOS tooling.
Needs Pillow:  pip install pillow
"""

from pathlib import Path

from PIL import Image

# Share of the canvas the artwork is allowed to occupy. Apple's own icons leave more room
# than this; free-standing marks like ours tolerate a tighter fit.
COVERAGE = 0.88

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]

assets = Path(__file__).resolve().parent.parent / "src" / "UltimatePoKeSync.App" / "Assets"
source = assets / "upsync-icon.png"


def inset(image: Image.Image, size: int = 1024) -> Image.Image:
    """Trim the transparent border, then centre the artwork on a square canvas."""
    box = image.split()[3].getbbox()
    if box is None:
        raise SystemExit(f"{source} is fully transparent.")

    art = image.crop(box)
    limit = round(size * COVERAGE)
    scale = min(limit / art.width, limit / art.height)
    art = art.resize((round(art.width * scale), round(art.height * scale)), Image.LANCZOS)

    canvas = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    canvas.paste(art, ((size - art.width) // 2, (size - art.height) // 2))
    return canvas


def main() -> None:
    icon = inset(Image.open(source).convert("RGBA"))
    icon.save(assets / "app-icon.png")

    # Pillow writes every requested size into the one file, so Windows can pick the
    # closest instead of scaling a 256 px image down to a 16 px tray slot.
    icon.save(assets / "app.ico", sizes=[(size, size) for size in ICO_SIZES])

    print(f"wrote {assets / 'app-icon.png'}")
    print(f"wrote {assets / 'app.ico'} ({', '.join(str(size) for size in ICO_SIZES)} px)")


if __name__ == "__main__":
    main()
