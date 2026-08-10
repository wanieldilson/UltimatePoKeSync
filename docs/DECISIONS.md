# Decision log — UltimatePoKeSync

A record of **every design choice**, with the alternatives considered and the reasoning.
Update it in the same commit as the change it describes.

Format: sequential `D-nnn`. Status: `Accepted` · `Superseded by D-xxx` · `Open`.

---

## D-001 — Reference emulator: mGBA, not BizHawk

**Status:** Accepted · 2026-08-10

BizHawk depends on 64-bit WinForms and has no official macOS support, let alone Apple
Silicon. Since macOS is a first-class target, it cannot be the starting emulator.

mGBA 0.10.5 ships a universal macOS build (Intel + ARM), plus Windows and Linux
(AppImage, including arm64), and has had stable Lua scripting since 0.10.0.

**Alternatives considered:** BizHawk (rejected: no macOS), DeSmuME (no Lua scripting
across all platforms), RetroArch network command interface (protocol too poor, cannot
read arbitrary ranges efficiently).

**Consequence:** the first target is GBA, therefore Gen 3. See D-004.

---

## D-002 — Lua → app transport: TCP socket, not a JSON file

**Status:** Accepted · 2026-08-10

The original idea allowed "TCP socket **or** a JSON file kept up to date". The file
option **is not possible**: mGBA's scripting API exposes no filesystem I/O whatsoever.
There is no `io.open`; the only file operations are `loadFile`, `loadSaveFile` and
savestate handling. TCP is therefore the only route.

Source: <https://mgba.io/docs/scripting.html>

**Consequence:** see D-003 for the direction of the connection.

---

## D-003 — The Lua script is the server, the C# app is the client

**Status:** Accepted · 2026-08-10

mGBA's socket API exposes `socket.bind()` / `listen()` / `accept()` / `poll()` /
`hasdata()`, all non-blocking, plus `"received"` and `"error"` events. `socket.connect()`,
by contrast, is documented as **blocking**: calling it from the frame callback would stall
emulation on every failed reconnect attempt.

So: Lua listens on `127.0.0.1:8888` (configurable), and the C# app connects as a client
with backoff. Retry logic belongs in the process that can afford it.

**Consequence:** multiple mGBA instances need different ports. The port is a parameter of
both the script and the app configuration.

---

## D-004 — Starting generation: Gen 3, reference game Pokémon Emerald

**Status:** Accepted · 2026-08-10 · amended 2026-08-10, see D-016

A direct consequence of D-001 (mGBA = GBA). Among the Gen 3 games, Emerald has the most
public reference material (Ironmon Tracker, pokebot-bizhawk, Archipelago), which makes
diagnosing a bad read far faster.

**Verified RAM addresses** (cross-checked between Data Crystal and `GameSettings.lua`
from 40Cakes/pokebot-bizhawk, a tool in active use):

| Game (USA)          | Party data   | Party count  | Domain |
| ------------------- | ------------ | ------------ | ------ |
| Emerald             | `0x020244EC` | `0x020244E9` | EWRAM  |
| FireRed / LeafGreen | `0x02024284` | `0x02024029` | EWRAM  |
| Ruby / Sapphire     | `0x03004360` | `0x03004350` | IWRAM  |

Layout: 6 contiguous 100-byte slots (80 stored + 20 battle stats).

Non-USA revisions have different addresses. See D-005 and D-016.

---

## D-005 — No hardcoded addresses: identify the game at runtime

**Status:** Accepted · 2026-08-10

Addresses vary per game **and per region**. Hardcoding the USA Emerald ones would mean
silently reading garbage from any other ROM.

The Lua script reads the game code from the cartridge header at `0x080000AC` (`BPEE`
Emerald USA, `BPRE` FireRed, `BPGE` LeafGreen, `AXVE` Ruby, `AXPE` Sapphire) and selects
the matching address table. If the game code is unknown, the script **refuses to read**
and says so, instead of guessing.

The game code travels in every message to the app, so the C# side always knows which game
it is interpreting.

---

## D-006 — The provider layer carries raw bytes, not Pokémon

**Status:** Accepted · 2026-08-10

This is the choice that makes the multi-emulator abstraction real rather than nominal.

The Lua script **parses nothing**: it ships the raw party bytes, the count and the game
identity. All decoding (decrypt, unshuffle, checksum, ID mapping) happens in C#, once,
shared by every provider.

Adding BizHawk or DeSmuME tomorrow costs ~150 lines of Lua and zero lines of domain logic.
If each script parsed its own Pokémon instead, every new emulator would duplicate — and
eventually diverge from — the same logic.

The contract lives in `UltimatePoKeSync.Contracts` and is the only project shared between
providers and parsing.

---

## D-007 — Parsing via PKHeX.Core; the app is licensed GPLv3

**Status:** Accepted · 2026-08-10

PKHeX.Core (NuGet, latest 26.7.7) targets `net10.0`, has **zero dependencies** and no
WinForms references: PKHeX's GUI is a separate project. It is cross-platform without
reservation.

The part that matters, from [PK3.cs](https://github.com/kwsch/PKHeX/blob/master/PKHeX.Core/PKM/PK3.cs):
the `PK3(Memory<byte>)` constructor **decrypts automatically** when needed
(`DecryptParty()`), so the 100 bytes read from RAM become an object exposing `Species`,
`IV_*`, `EV_*`, `Nature`, `Ability`, `Move1..4`, `HeldItem`, `Stat_Level` directly. On top
of that, `PersonalTable3` provides base stats, types and abilities, and `Learnset` gives
learnable moves.

This avoids hand-writing XOR decryption, substructure permutation and checksums — and,
more importantly, avoids redoing all of it for every future generation, where the format
is considerably more complex.

**Accepted cost:** PKHeX.Core is **GPL-3.0-or-later** and its copyleft extends through
linking. UltimatePoKeSync is therefore licensed **GPLv3**. Confirmed by Roberto on
2026-08-10, having considered the alternative of a hand-written Gen 3 parser (~250 lines)
under a permissive licence, rejected because it does not scale to later generations.

**Known limitation:** PKHeX.Core carries no *competitive* data (tiers, common spreads,
metagame items). Those remain our own datasets. See D-009.

---

## D-008 — Defences against inconsistent RAM reads

**Status:** Accepted · 2026-08-10 · corrected during implementation

Reading every frame can capture a snapshot **while the game is writing** to that region
(a torn read), yielding a Pokémon that never existed.

Two defences, at two different levels.

**In the Lua script — confirmation across two reads.** A change seen once is not sent: it
is held as "pending" and only transmitted if the following read produces the same hash.
This costs one polling interval of latency (~66 ms), imperceptible for a party.

This defence **must** live in the script, not the app: the script only transmits on
change, so a second identical read would never reach the C# side and the comparison there
would be impossible by construction. This was caught during implementation — the original
version of this decision placed the check in C#.

**In the C# parser — per-slot validation.** Each slot must pass, in order:
`Species != 0`, `FlagHasSpecies`, `ChecksumValid`, `!FlagIsBadEgg`, species ≤ 411.

The empirical findings that produced that list:

| Input                      | `ChecksumValid` | `FlagHasSpecies` | `Valid` |
| -------------------------- | --------------- | ---------------- | ------- |
| Real encrypted Pokémon     | `true`          | `true`           | `true`  |
| 100 random bytes           | **`false`**     | `false`          | `true`  |
| Empty slot (all zeroes)    | **`true`**      | `false`          | —       |

Two non-obvious consequences: `ChecksumValid` **is not sufficient on its own**, because an
all-zero slot passes it; and PKHeX's `Valid` property is useless as a filter, because it
stays `true` even for random bytes.

An empty slot *beyond* the declared count is not an error and is not reported; an empty
slot *within* the declared count is, because it signals a genuine inconsistency.

---

## D-009 — Analysis is generation-aware from day one

**Status:** Accepted · 2026-08-10

Gen 3 rules differ from modern ones in ways that **change the suggestions themselves**,
not just the numbers:

- **17 types, no Fairy.** The type chart must be per generation.
- **The physical/special split is by TYPE, not by move.** In Gen 3 every Water move is
  special and every Fighting move is physical, regardless of the move. Role inference
  (physical vs special attacker) and therefore the recommended nature and EVs follow
  different rules from Gen 4 onwards.
- **No hidden abilities**; the ability is a single bit.
- **EVs**: 510 total, 255 cap per stat (252 is merely the efficiency threshold).

Treating these as "details to fix later" would force a rewrite of the suggestion engine
when Gen 4 is added. The rules therefore sit behind a per-generation abstraction from the
start.

---

## D-010 — Two analysis profiles: playthrough and competitive

**Status:** Accepted · 2026-08-10

Roberto's choice, 2026-08-10.

- **Playthrough**: moves already available *now* in the learnset, obtainable items,
  realistic EVs, coverage against Gym Leaders and the League.
- **Competitive**: 252/252/4 spreads, speed benchmarks, metagame natures/items/EVs.

These are two sets of heuristics on top of the **same** analysis engine, switchable in the
UI. The engine computes facts (role, coverage, projected stats); the profile decides *what
to recommend* from those facts. The separation is binding from the start, otherwise the
two modes interleave and become unmaintainable.

---

## D-011 — Application stack: .NET 10 + Avalonia

**Status:** Accepted · 2026-08-10

.NET 10 is forced by PKHeX.Core 26.x, which targets `net10.0` (D-007).

Avalonia for the UI: it runs natively on Windows, Linux and macOS including Apple Silicon,
unlike WPF. MVVM via CommunityToolkit.Mvvm.

**Alternatives considered:** MAUI (no Linux desktop support), Uno Platform (more setup, no
advantage here), web UI with a local backend (adds a browser and an HTTP layer for no
benefit in a local single-user app).

**Implementation note:** `Avalonia.Diagnostics` (DevTools) is **not published for 12.x** —
it stops at 11.3.20. Removed from the dependencies. Revisit when the Avalonia 12
equivalent ships.

---

## D-012 — The Lua script is a single file, not modules

**Status:** Accepted · 2026-08-10

mGBA's documentation **does not state** whether `require` and `package.path` work for
local modules next to the loaded script. Splitting the bridge into `ups/server.lua`,
`ups/games.lua` and so on would make it depend on undocumented behaviour, and would be
awkward in practice: the user loads *one* file from mGBA's menu.

`emulator-scripts/mgba/ups_bridge.lua` is therefore monolithic but sectioned. That is
acceptable because the script is deliberately dumb (D-006): it stays under ~300 lines and
will never contain domain logic.

---

## D-013 — Gen 3 addresses cross-checked against three sources

**Status:** Accepted · 2026-08-10

The addresses in D-004 are confirmed by three independent sources, the last of which is
decisive:

1. Data Crystal (FireRed/LeafGreen RAM map).
2. `GameSettings.lua` from `40Cakes/pokebot-bizhawk`, a tool in active use.
3. **`res/scripts/pokemon.lua` shipped by mGBA itself** (tag 0.10.5) — identical values,
   including `_partyMonSize = 100`.

The same source also provided the idiomatic socket-server pattern
(`res/scripts/socketserver.lua`), which the bridge follows: `socket.bind(nil, port)` →
`listen()` → `add("received", accept)`, with `socket.ERRORS.AGAIN` meaning "no data".

A useful finding: in Gen 3 **revisions share the same addresses** (FireRed Rev 1, Ruby
Rev 1/2 inherit the base revision's). The game code alone is therefore enough to pick the
map, with no need for the ROM's CRC32. That may not hold for later generations, which is
why the key remains the full game code rather than just the title.

---

## D-014 — PKHeX normalises types to modern indices: verified, not assumed

**Status:** Accepted · 2026-08-10

A classic Gen 3 trap: internal type IDs **do not** match modern ones, because index 9 in
Gen 3 is the `???` (Mystery) type. Internally Fire is 10 and Water is 11; in the modern
scheme they are 9 and 10.

Getting this wrong would produce an entirely false type analysis **with no visible
error**: every Pokémon would take its neighbour's type.

Verified empirically against PKHeX 26.7.7:

| Pokémon   | `PersonalTable.E[id].Type1` | Reading |
| --------- | --------------------------- | ------- |
| Charizard | 9                           | Modern Fire ✓ (the Gen 3 internal ID would be 10) |
| Gyarados  | 10                          | Modern Water ✓ |
| Magnemite | 12                          | Modern Electric ✓ |

The same holds for `MoveInfo.GetType(id, EntityContext.Gen3)`. **PKHeX normalises**: no
conversion to write, and `PokemonType` can be cast directly.

The assumption is pinned by the `Parse_ReadsModernTypeIndicesNotGen3Internal` test,
because it is exactly the kind of thing a PKHeX update could change silently.

**Another confirmed Gen 3 detail:** nature is **not stored**, it is derived from the PID
(`PID % 25`). PKHeX computes it. Setting `Nature` on a `PK3` with a fixed PID has no
effect — worth remembering when building test fixtures.

---

## D-015 — Mono-type Pokémon are normalised to `SecondaryType = None`

**Status:** Accepted · 2026-08-10

In the game data a mono-type Pokémon repeats the same type in both fields (Pikachu:
`Type1 = Type2 = 12`). Propagating that as-is would **double that type's weight** in every
aggregate defensive calculation.

The parser normalises the second type to `None` when it equals the first. Covered by the
`Parse_MonoTypeNormalisesSecondaryToNone` test.

---

## D-016 — The repository is written in English

**Status:** Accepted · 2026-08-10

Everything committed is in English: commit messages, identifiers, comments, XML docs,
Markdown, CLI output, UI strings and test names.

Roberto's decision, 2026-08-10, after the first six commits had been written in Italian.
It is a GPLv3 open-source project intended to be readable by contributors who do not speak
Italian. The working tree was translated in one pass; the earlier commit *messages* were
left as they were, since rewriting unpushed history is not worth the churn.
