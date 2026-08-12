#!/usr/bin/env python3
"""Downloads the animated Black and White sprites, for your own copy of the app.

The app never ships Pokémon artwork and never will: it belongs to Nintendo, Game Freak and
Creatures, and the collections that gather it state no licence of their own. What the app
does is read sprites you already have — from your cartridge where it can (D-033), and
otherwise from a folder on your disk. This fetches that folder.

The sprites come from PokeAPI's public repository, in the Black and White animated style, so
one set covers every generation from the first to the fifth. The whole set of fronts is
27 MB, and the shiny ones another 27 MB — which is why shinies are not fetched unless asked
for: a shiny falls back to the ordinary sprite, and the tile marks it with a star anyway.

Recompressing was measured and abandoned: animated WebP saves 6% on these files, because a
small palette animation is exactly what GIF is good at. The only real savings are fetching
fewer of them, which is what the two options below do.

By default they land where the app already looks, so nothing needs configuring afterwards:

    macOS    ~/Library/Application Support/UltimatePoKeSync/sprites
    Windows  %APPDATA%\\UltimatePoKeSync\\sprites
    Linux    ~/.local/share/UltimatePoKeSync/sprites

Usage:
    python3 tools/fetch-sprites.py                  # every front sprite, 27 MB
    python3 tools/fetch-sprites.py --up-to 386      # Gen 1 to 3 only, 13 MB
    python3 tools/fetch-sprites.py --shiny          # add the shiny ones, 27 MB more
    python3 tools/fetch-sprites.py --into ~/sprites # somewhere else
"""

import argparse
import os
import sys
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

BASE = ("https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon"
        "/versions/generation-v/black-white/animated")

# The fifth generation ends at Genesect, and so does this sprite style. Later Pokémon exist
# in the collection as fan-made art, which is somebody else's work again and not needed for
# the generations the app reads.
LAST_SPECIES = 649


def default_folder() -> Path:
    home = Path.home()
    if sys.platform == "darwin":
        root = home / "Library" / "Application Support"
    elif sys.platform.startswith("win"):
        root = Path(os.environ.get("APPDATA", home))
    else:
        root = Path(os.environ.get("XDG_DATA_HOME", home / ".local" / "share"))

    return root / "UltimatePoKeSync" / "sprites"


def fetch(url: str, target: Path) -> str:
    """Downloads one sprite. Missing ones are normal: not every number has artwork."""
    if target.exists() and target.stat().st_size > 0:
        return "skipped"

    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            data = response.read()
    except urllib.error.HTTPError as error:
        return "missing" if error.code == 404 else f"failed {error.code}"
    except OSError as error:
        return f"failed {error}"

    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    return "downloaded"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--into", type=Path, default=None)
    parser.add_argument(
        "--shiny", action="store_true", help="also fetch shiny sprites, doubling the size")
    parser.add_argument(
        "--up-to", type=int, default=LAST_SPECIES, metavar="N",
        help="stop at this dex number: 386 covers Gen 1-3 (13 MB), 493 covers Gen 4")
    parser.add_argument("--workers", type=int, default=8)
    arguments = parser.parse_args()

    folder = (arguments.into or default_folder()).expanduser()
    print(f"Downloading into {folder}")

    last = max(1, min(arguments.up_to, LAST_SPECIES))
    jobs = [(f"{BASE}/{i}.gif", folder / f"{i}.gif") for i in range(1, last + 1)]
    if arguments.shiny:
        jobs += [(f"{BASE}/shiny/{i}.gif", folder / "shiny" / f"{i}.gif")
                 for i in range(1, last + 1)]

    tally = {"downloaded": 0, "skipped": 0, "missing": 0}
    failures = []

    with ThreadPoolExecutor(max_workers=arguments.workers) as pool:
        for index, outcome in enumerate(pool.map(lambda job: fetch(*job), jobs), start=1):
            if outcome in tally:
                tally[outcome] += 1
            else:
                failures.append(outcome)

            if index % 200 == 0:
                print(f"  {index}/{len(jobs)}…")

    size = sum(f.stat().st_size for f in folder.rglob("*.gif")) / (1024 * 1024)
    print(f"\n{tally['downloaded']} downloaded, {tally['skipped']} already there, "
          f"{tally['missing']} with no artwork, {len(failures)} failed")
    print(f"{size:.0f} MB in {folder}")

    if failures:
        print(f"First failure: {failures[0]}")
        return 1

    print("\nNothing else to do: the app looks here on its own.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
