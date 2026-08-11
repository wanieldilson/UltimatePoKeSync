#!/usr/bin/env python3
"""Check whether a Gen 3 ROM uses the memory addresses we have on file for it.

    python3 tools/check-gen3-addresses.py roms/*.gba

Why this exists. Addresses cannot be read off a ROM directly, but they are compiled into
it: every routine that touches the party loads the address from a literal pool, so the
value appears in the image hundreds of times. A value the game never uses appears zero
times — a random 32-bit constant is expected 0.004 times in sixteen megabytes.

That gives a test with no false positives to speak of. Run it against a ROM whose
addresses are already verified and you get a large count; run the same address against a
different game and you get nothing. It is how the Italian Ruby, Sapphire, FireRed and
LeafGreen were added without owning a disassembly. See D-034.

It says nothing about whether the *layout* at that address is what we expect. Confirming
that still means loading the game and watching the party read correctly.
"""

import pathlib
import struct
import sys

# Same table the Lua bridge carries, keyed by the addresses rather than by the game code,
# because that is what a ROM can be asked about.
FAMILIES = {
    "Ruby / Sapphire": (0x03004360, 0x03004350, {"AXVE", "AXPE", "AXVI", "AXPI"}),
    "FireRed / LeafGreen": (0x02024284, 0x02024029, {"BPRE", "BPGE", "BPRI", "BPGI"}),
    "Emerald": (0x020244EC, 0x020244E9, {"BPEE", "BPEF", "BPED", "BPES", "BPEI"}),
}

# Below this a match is noise rather than use.
CONVINCING = 20


def check(path: pathlib.Path) -> bool:
    rom = path.read_bytes()
    code = rom[0xAC:0xB0].decode("ascii", "replace")
    revision = rom[0xBC]

    scores = {
        name: rom.count(struct.pack("<I", party))
        for name, (party, _, _) in FAMILIES.items()
    }
    best = max(scores, key=scores.get)
    expected = next((name for name, (_, _, codes) in FAMILIES.items() if code in codes), None)

    print(f"\n{code} rev{revision}  {path.name}")
    for name, count in scores.items():
        mark = "  <-- used" if count >= CONVINCING else ""
        print(f"    {name:<22} party address appears {count:>5} times{mark}")

    if scores[best] < CONVINCING:
        print("    VERDICT: no known address family is used by this ROM.")
        return False

    if expected is None:
        print(f"    VERDICT: {code} is not in the supported list, and looks like {best}.")
        print(f"             Add it to the {best} family, then verify by loading the game.")
        return False

    if expected != best:
        print(f"    VERDICT: MISMATCH. {code} is filed under {expected} but uses {best}.")
        return False

    print(f"    VERDICT: agrees with the {expected} entry it is filed under.")
    return True


def main() -> int:
    paths = [pathlib.Path(argument) for argument in sys.argv[1:]]
    if not paths:
        print(__doc__)
        return 2

    return 0 if all(check(path) for path in paths) else 1


if __name__ == "__main__":
    raise SystemExit(main())
