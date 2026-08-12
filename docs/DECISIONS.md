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

**Status:** Accepted · 2026-08-10 · amended 2026-08-10, see D-017

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

Non-USA revisions may have different addresses. See D-005 and D-017.

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

---

## D-017 — Western Emerald localisations share the USA addresses

**Status:** Accepted · 2026-08-10 · **verified against the real ROM**

The development ROM turned out to be Italian Emerald: game code `BPEI`, not `BPEE`.
Under D-005 the script correctly refused to read it, since it was not in the table.

Evidence that the addresses are shared, from `GameSettings.lua` in
40Cakes/pokebot-bizhawk:

- Emerald **France** (`BPEF`) and **Germany** (`BPED`) map to the *same table index* as
  the USA release, i.e. the very same `pstats` / `pcount` entries.
- Emerald **Spain** (`BPES`) has its own index whose values are identical:
  `0x020244EC` / `0x020244E9`.

Every Western localisation therefore shares the EWRAM layout; only the text differs.
`BPEI` was added on that basis, together with `BPEF`, `BPED` and `BPES`.

**Verified on 2026-08-10** against a real Italian Emerald ROM running in mGBA 0.10.5. The
starter appeared correctly the moment it entered the party:

```
┌─ POKEMON EMER [BPEI] rev0  ·  seq 3
│  [0] TREECKO  Lv.5  Grass
│      Nature Bashful · Ability Overgrow · Item -
│      IVs   HP 19  Atk 14  Def 24  SpA 29  SpD 0  Spe 25
│      · Pound  Normal  35/35 PP
│      · Leer   Normal  30/30 PP
└─
```

Species, level, type, ability, moves and IVs all consistent, and **zero rejected slots
across the whole session**. Since a wrong address could not produce a slot passing
`ChecksumValid`, the inference is confirmed rather than merely plausible.

The same session incidentally validated the change detection end to end: PP dropping
during a battle (35 → 34 → 33 → 32) produced one snapshot each, and nothing in between.

Japanese releases genuinely do have different addresses (`0x02024190` / `0x0202418D` for
Emerald) and are deliberately left out until someone can test them.

**Side note:** the parser resolves species, item and ability names through PKHeX's English
string tables regardless of the ROM's language. On an Italian ROM the nicknames the player
typed come through correctly — the Gen 3 character map is shared across Western languages
— but species names display in English. Worth revisiting when the UI gets localised.

---

## D-018 — Deciding *when* a change matters is its own layer

**Status:** Accepted · 2026-08-10

The provider knows how to obtain bytes. The parser knows how to read them. Neither is in a
position to judge whether a change deserves recomputing the analysis — and that judgement
has to happen somewhere, or the UI recomputes several times per second during every battle.

`UltimatePoKeSync.Session` holds `PartyTracker`, which sits between the two and emits a
party only when something analytically meaningful changed. It depends on `Contracts`
alone, so it knows neither mGBA nor PKHeX.

**What counts as meaningful:** species, personality value, level, nature, ability, held
item, egg flag, IVs, EVs, move IDs and slot order.

**What does not:** current PP, current HP, and any other battle state. A single battle turn
moves PP; none of it changes a single recommendation about EVs, nature, moves or items.

Three further rules live here, each earning its place:

- **Out-of-order snapshots are dropped**, except when the sequence restarts at 1. Reloading
  the script or resetting the emulator restarts the counter, and mistaking that for a stale
  message would leave the tracker deaf until the sequence climbed back past the old
  high-water mark.
- **Snapshots with rejected slots are skipped** in favour of the last good party, so a torn
  read does not make the team flicker down a member and back.
- **…but only five times in a row.** A genuinely broken party — a bad egg is a real and
  permanent state — would otherwise stall the display forever. After five attempts the
  tracker concludes the problem is the party rather than the timing, and reports what it
  sees.

The comparison key is a string rather than a hash: at 15 updates per second the cost is
irrelevant, and a hash collision would silently swallow a real party change, which is
precisely the failure this layer exists to prevent.

---

## D-019 — Slots past the declared party count are never read

**Status:** Accepted · 2026-08-10

Found by capturing real RAM rather than by reasoning: in the Italian Emerald capture with
one Pokémon in the party, **none of the other five slots was all zeroes**. The game does
not reliably wipe a slot when a Pokémon leaves the team.

In that particular capture the leftovers decoded to `Species = 0`, so nothing surfaced. But
the bytes left behind by a Pokémon deposited in the PC are a complete, **checksum-valid**
Pokémon. The original implementation examined all six slots and admitted any that passed
validation, so it would eventually have shown a ghost — a team member the game no longer
had.

The parser now iterates only up to `PartyCount`. This is also what `docs/protocol.md`
already promised ("the app uses it as an upper bound"); the code simply had not implemented
its own contract.

Trusting the count is safe because the script confirms it across two consecutive reads
(D-008): a torn count byte would have to read identically twice to get through.

Covered by `Parse_ValidPokemonBeyondTheCountIsNotResurrected` and by the real-RAM fixture
test, which asserts that the leftover bytes are genuinely non-zero — so the test would
notice if a future capture stopped exercising the case.

---

## D-020 — Test fixtures captured from real RAM, not only hand-built

**Status:** Accepted · 2026-08-10

Fixtures built with PKHeX prove the parser agrees with PKHeX's own writer. They cannot
prove it agrees with what the game actually puts in memory — and that is the claim the
whole project rests on.

`upks --dump <dir>` writes every raw snapshot as a JSON fixture. The first one,
`tests/…/Fixtures/emerald-it-treecko.json`, is 600 bytes captured from Italian Emerald
running in mGBA, and it immediately paid for itself by exposing D-019.

Implementation note: `--dump` needed no change to the mGBA provider, the parser or the
tracker — just a `DumpingEmulatorProvider` wrapped around the real one. A small, concrete
demonstration that the abstraction of D-006 is doing real work.

---

## D-021 — Battle facts come from embedded, validated per-generation data

**Status:** Accepted · 2026-08-10

The type chart and move base powers are versioned as embedded JSON in `GameData` and loaded
through an `IGenerationRules` resolved by generation. The loader validates the generation,
the exact ordered type set, every matrix dimension and multiplier, and the expected move
count before exposing any data. There is no runtime network access and no silent fallback
to another generation.

The Gen 3 data is transcribed from the matching `pret/pokeemerald` decompilation tables:
`gTypeEffectiveness` in `src/battle_main.c` and `gBattleMoves` in
`src/data/battle_moves.h`. Keeping move power as data is necessary because PKHeX exposes a
move's type and PP but not its Gen 3 base power; without it a status move such as Leer would
incorrectly count as Normal offensive coverage.

`IGenerationRules` also owns the type-based physical/special split and the six defensive
ability modifiers in M5's scope. This prevents Gen 3 assumptions from leaking into the
analysis engine and gives a future generation one replacement boundary.

**Alternatives considered:** hard-coded C# dictionaries (rejected: difficult to audit as a
complete chart), a current-generation web API (rejected: not generation-stable and would
make an offline app network-dependent), treating every known move as damaging (rejected:
produces false offensive coverage), and putting ability switches directly in the analysis
engine (rejected: they are generation rules, not analysis heuristics).

---

## D-022 — Coverage is a complete fact matrix; gaps mean no available answer

**Status:** Accepted · 2026-08-10

The M5 analyzer returns one defensive and one offensive entry for every type available in
the snapshot's generation, in stable chart order. It keeps the per-Pokémon and per-move
matchups rather than collapsing them into a single score, so the UI and both recommendation
profiles can explain every result.

A **defensive gap** is an attacking type that is super effective against at least one party
member and for which no other member has an ability-adjusted resistance or immunity. A
neutral member is not considered a safe defensive answer. An empty party has no weaknesses,
not seventeen defensive gaps.

An **offensive gap** is a single defending type that none of the party's currently known,
damaging moves can hit super effectively. Coverage is not limited to STAB: a coverage move
exists precisely to answer types outside its user's own typing. Status moves are excluded
using the generation's move-power data. Fixed-damage and one-hit knockout moves are also
excluded: their battle scripts may observe immunities but do not apply a super-effective
damage multiplier. Variable-power attacks such as Low Kick and Hidden Power remain valid.
The result records the Gen 3 physical/special category as a fact for later role inference.

The analyzer itself has no playthrough/competitive switch. It computes only facts; the two
profiles from D-010 will decide how to turn those facts into recommendations in M6.

**Alternatives considered:** a weighted team score (rejected: hides which member or move
caused the result), declaring a gap at an arbitrary weakness-count threshold (rejected:
profile policy masquerading as a fact), STAB-only offensive coverage (rejected: ignores the
purpose of coverage moves), and passing an analysis profile into the core analyzer (rejected:
would mix facts and recommendations in violation of D-010).

---

## D-023 — Nature and projected-stat calculations are generation facts

**Status:** Accepted · 2026-08-10

Nature definitions and projected stat calculations belong to `IGenerationRules`, not to a
recommendation profile. Both playthrough and competitive advice need the same answer to
"what would this Pokémon's stats be at this level with this spread and nature?". Profiles
may choose different inputs, but must not reimplement the formula.

The 25 Gen 3 natures are embedded, ordered by the same ID derived from `PID % 25`, and
validated at load time. The calculator uses the game's integer operations, enforces IV
`0..31`, Gen 3 EV `0..255` and total EV `<= 510`, and preserves Shedinja's one-HP special
case. It accepts a proposed level, nature and EV spread so profiles can compare alternatives
without mutating the live `PokemonSnapshot`.

**Alternatives considered:** calculate stats inside each profile (rejected: duplicated game
rules would drift), use floating-point formulae and round at the end (rejected: the games
truncate at specific intermediate steps), and return only percentage deltas (rejected: EV
and speed recommendations need exact projected values).

---

## D-024 — Recommendations combine explainable inference with offline presets

**Status:** Accepted · 2026-08-10

M6 uses a hybrid recommendation model. The core first infers a broad role from auditable
facts in the live snapshot: base stats, the Gen 3 physical/special split, and how many
current moves scale from each offensive stat or provide utility. Recommendation profiles
may then use versioned reference presets as priors for plausible roles and movepools. A
preset never overrides live Pokémon data, generation rules, move legality, team gaps, or
profile policy; missing preset data falls back to the deterministic inference.

The first reference catalog is Pokémon Showdown's Gen 3 Random Battle set data, pinned to
commit `db93869dcc216c0be39e7f86e9a64edcc7496d89` and embedded for offline use. Its 220
species and 393 sets are treated as broad expert-authored role and movepool examples, not
as standard OU usage or as complete competitive builds. The source is MIT-licensed and is
recorded in `THIRD_PARTY_NOTICES.md`.

A future competitive profile may use pinned Smogon ladder statistics whose generated data
is MIT-licensed as a weighting signal. Smogon Dex editorial sets must not be bundled
without explicit permission. No recommendation path performs a runtime network call.

**Alternatives considered:** derive every recommendation from first principles (rejected:
deterministic but unnecessarily reinvents expert movepool knowledge), reproduce one preset
verbatim (rejected: ignores the live team and confuses Random Battle with standard play),
query an online service at runtime (rejected: harms determinism and offline operation), and
bundle Smogon Dex sets without permission (rejected: their reuse terms require permission).

---

## D-025 — Recommendations expose candidates and availability, not false certainty

**Status:** Accepted · 2026-08-10

The M6 engine returns a ranked, explainable candidate pool rather than pretending that a
single four-move build is universally best. It computes team and role facts once, then
delegates nature, EV, move and item policy to one of two injectable profiles. Reference
catalogs declare their generation, so Gen 3 data cannot silently serve a later generation.

The competitive profile marks preset-derived moves and items as competitive references.
The playthrough profile distinguishes three facts:

- a currently known move or held item is known to be available;
- a level-up move at or below the current level, a type-boosting item, or a berry still
  requires an availability check;
- competitive reference candidates make no claim about story progression.

That distinction is necessary because party RAM contains no bag, badges, map progress,
Move Reminder access, or cross-game transfer history. The pinned Showdown learnset is also
generation-wide rather than Emerald-specific. Until a later data source supplies those
facts, the playthrough profile may surface these candidates but must not label them as
currently obtainable. Exact competitive EV targets and softer playthrough training
priorities are separate result shapes rather than a magic spread applied to both modes.

**Alternatives considered:** emit an opaque final build (rejected: hides trade-offs and
team context), assume every legal level-up move and common item is immediately obtainable
(rejected: false for many save states), suppress every candidate not present in the party
(rejected: safe but not useful), and parse the entire save game's progression state inside
M6 (deferred: a meaningful scope expansion that should be designed as its own input).

---

## D-026 — The diagnostic CLI is the verification surface for every analysis layer

**Status:** Accepted · 2026-08-11

M5 and M6 shipped fully tested but unreachable: nothing outside the test projects referenced
`TeamAnalyzer` or `PokemonRecommendationEngine`, and the CLI did not even reference
`UltimatePoKeSync.Analysis`. Every earlier milestone was closed by watching it work against
the real Italian Emerald; the analysis layers had never been through that check.

The CLI therefore renders every layer, behind opt-in flags so the M3 party diagnostic stays
readable: `--analyze` prints type coverage and unanswered gaps, and `--recommend
<playthrough|competitive>` prints per-Pokémon role, nature, EV, move and item candidates
with their availability labels. When a profile is selected the CLI reuses the
`TeamAnalysis` the engine already computed rather than analysing twice. Rendering lives in
`AnalysisReport` and formats only: it never re-ranks, re-filters or reinterprets a result,
so what appears on screen is exactly what a future UI will receive.

`--replay <fixture>` renders one dumped snapshot and exits. A capture from real RAM is
already the project's fixture format, so replaying one exercises the whole parse → analyse →
recommend chain with no emulator, and makes any output problem reproducible in a bug report
rather than only reproducible with a specific save.

`NotSupportedException` from an unsupported generation is caught per snapshot and printed as
a line: a 15 Hz stream must not die because one layer cannot serve the current game.

**Alternatives considered:** wait for the M7 Avalonia dashboard (rejected: leaves two
milestones unverified against real hardware and puts the first real look at the output
behind UI work), print analysis unconditionally (rejected: buries the parsing diagnostic
the CLI exists for), and let the CLI re-rank or trim candidates for readability (rejected:
the console would then verify the console's policy instead of the engine's).

---

## D-027 — Level-up learnsets are keyed by game, not by generation

**Status:** Accepted · 2026-08-11

The pinned Showdown learnset tagged every Gen 3 level-up entry as `3L`, merging Ruby and
Sapphire, Emerald, and FireRed and LeafGreen into one table. The importer resolved the
disagreements with `Math.min` over the levels, which is not a conservative choice but a
wrong one: it reports a level the running game does not use.

Measured against PKHeX, **42 of the 386 Gen 3 species** teach at least one move at a
different level depending on the game. Zubat is the clean example: Supersonic at 6 and
Astonish at 11 in RSE, exactly reversed in FRLG. On FireRed the merged table claimed a
level-6 Zubat could already know Supersonic. The failure is invisible — a plausible number
in a field that looks authoritative.

So the level-up learnset moves behind `ILevelUpLearnsetSource`, which takes a
`GameIdentity`. Move *identity* stays behind `IMoveReferenceCatalog` keyed by generation,
which is the correct granularity for it: a move's number and name are stable within a
generation, and its type changes only across generations (Charm is Normal in Gen 3 and
Fairy from Gen 6).

The implementation reads PKHeX's `LearnSource3E`, `LearnSource3RS`, `LearnSource3FR` and
`LearnSource3LG`. PKHeX is already a dependency for parsing, already GPLv3, and already
ships one learn source per game for every generation from RB to SV. Choosing it removes a
bundled dataset instead of adding one, and the same class shape will serve Gen 1 to Gen 9
unchanged — which matters, because supporting every generation is a stated goal, not a
hypothetical.

It lives in its own project, `UltimatePoKeSync.GameData.Learnsets`, so D-007 still holds:
`Analysis`, `Contracts` and the UI depend on the abstraction and never on PKHeX. That
costs the engine its parameterless constructor — the default composition cannot name a
type Analysis cannot see — so `PokemonRecommendationEngine.CreateDefault` now takes the
learnset source and the composition root supplies it. `Parsing` is consequently no longer
the only project that references PKHeX; the invariant was always "the analysis layer does
not", and that is what the comment in each project now says.

**Alternatives considered:** keep Showdown and pick the maximum instead of the minimum
level (rejected: still one number for three games, wrong in the other direction), generate
per-game datasets from the `pret` disassemblies (rejected: one importer per game, Gen 3
only, and the whole exercise repeats for every future generation, while PKHeX has already
done it), and expose PKHeX types through `GameData` directly (rejected: drags the
dependency into everything that reads game data, including the UI).

---

## D-028 — The dashboard answers, and shows its working

**Status:** Accepted · 2026-08-11

M7 turns the analysis layers into an application a player can use without a terminal. Three
choices shape it.

**The window formats; it never decides.** Coverage, roles, strength and builds are computed
in `Analysis` and rendered by view models that only select and format. The dashboard and the
CLI therefore agree by construction rather than by discipline, and a disagreement between
them is a bug with one place to fix.

**A score is never shown alone.** D-022 rejected opaque numbers, and a team strength
indicator is exactly the thing that becomes one. `TeamStrength` is a list of attributed
factors — party size, level cohesion, defensive coverage, offensive coverage, nature fit,
effort-value fit — each carrying its points, the fact behind them and the members
responsible. The score is their sum, and the panel always shows the breakdown beside it.
The effort-value factor measures values spent on the *wrong* stats rather than values not
yet spent, because story play rarely trains at all and reporting that as a weakness is
noise, not advice.

**A recommended build, without dropping the candidate pool.** D-025 deliberately refused to
name one best set. A player wants an answer, so `RecommendedBuild` now picks four moves and
states the reason for each, ranked by same-type damage, the category the role actually
scales with, whether the move closes a gap nothing else on the team answers, and continuity
with what is already known. What it turned down stays visible as alternatives. The engine
still exposes every candidate; the build is one more result, not a replacement.

Sprites are placeholders: a tile in the primary type's colour with the species name and
level. Pokémon sprites are Nintendo and Game Freak's, and bundling them in a public GPLv3
repository is a licensing problem rather than an asset problem. Reading them from the
player's own ROM through the existing bridge is the clean route and is its own milestone.
Every coloured element also carries its type in text, so the display survives both colour
blindness and a screenshot.

Distribution is a GitHub Actions workflow that publishes self-contained single-file builds
for Windows, Linux and both macOS architectures and attaches them to the release. A user
needs no .NET SDK, no clone and no command line. Compression brings a build to about 62 MB,
verified locally. The macOS builds are wrapped in an unsigned `.app` bundle, so the first
launch needs right-click → Open; notarising would require a paid Apple Developer account
and is a business decision, not a technical one. The bridge script is copied next to the
executable so the setup screen can hand over a path that exists on the user's disk.

`ILiveTeamSource` exists so the dashboard can be driven by the Italian Emerald capture in a
test, and the view model takes its UI-thread dispatch as a delegate so that test needs no
Avalonia application. Without both, the only way to check the window would be to have mGBA
running — the kind of verification that never gets done.

**Alternatives considered:** put the analysis logic in the view models where it would be
convenient (rejected: two implementations of the same answers, drifting), show the strength
score alone as a headline number (rejected: unactionable, and D-022 already settled it),
have the build replace the candidate list (rejected: hides the trade-off the profiles
exist to expose), bundle a sprite pack (rejected: not ours to redistribute), and commit
prebuilt binaries to the repository (rejected: bloats history and still fails Gatekeeper).

### Amendment, 2026-08-11 — macOS builds must be produced on macOS

`v0.1.0` cross-published every runtime from one Linux runner. The Apple Silicon download
was reported as "damaged and should be moved to the Trash". That is not the Gatekeeper
warning it looks like: Apple Silicon requires every executable to carry a signature, at
minimum an ad-hoc one, and the kernel `SIGKILL`s anything without one. Reproduced locally:
stripping the signature from a working build makes it exit with 137, and re-applying
`codesign --sign -` makes it run again. Right-click → Open cannot rescue it, because the
problem is not the quarantine attribute.

`codesign` only exists on macOS, so the macOS artifacts now build on a `macos-latest`
runner — where the .NET SDK ad-hoc signs the apphost anyway — and the finished bundle is
signed and verified. Packaging uses `ditto` rather than `zip`, which preserves the
symlinks and permissions a signature covers. Verified locally end to end: sign, package,
unpack, `codesign --verify --deep --strict`, launch.

Notarising, which would remove the right-click → Open step entirely, still needs a paid
Apple Developer account and remains a business decision.

---

## D-029 — The setup screen points at a stable folder, not at the executable

**Status:** Accepted · 2026-08-11

The setup screen showed the bridge script where it shipped, next to the executable. On
macOS that produced a path like
`/private/var/folders/ch/…/AppTranslocation/005EAC16-…/d/UltimatePoKeSync-2.app/Contents/MacOS/emulator-scripts/ups_bridge.lua`.

That is App Translocation. An app opened straight from Downloads, still carrying the
quarantine attribute and never moved in Finder, is executed from a randomised read-only
copy. Everything works, but every path the app reports about itself is a throwaway. The
one step the user has to perform by hand — find this file in mGBA's open dialog — became
the hardest one in the whole product.

The script is therefore copied on startup into a per-user folder that does not move:
`~/Library/Application Support/UltimatePoKeSync` on macOS, `%APPDATA%\UltimatePoKeSync` on
Windows, `$XDG_DATA_HOME` or `~/.local/share/UltimatePoKeSync` on Linux. The copy is
refreshed whenever the shipped script differs, so updating the app updates the script.
Alongside the path there is now a **Show in Finder** button, because selecting a file in a
file manager beats retyping a path, and the macOS steps start by telling the user to move
the app into Applications, which is what stops translocation happening at all. When the app
detects it is translocated it says so directly instead of leaving the user to wonder about
the strange path.

`~/Documents` would have been the most discoverable location, but writing there triggers a
macOS privacy prompt, and asking for a permission dialog during setup trades one confusion
for another. Application Support needs no permission, and the reveal button removes the
only downside of a hidden folder.

**Alternatives considered:** keep using the executable's folder and tell people to move the
app first (rejected: the instruction arrives after the broken path is already on screen),
write next to the ROM (rejected: the app does not know where the ROM is, and that folder is
the user's, not ours), and embed the script and write it to a temporary file on demand
(rejected: a temporary path is exactly the problem being fixed).

---

## D-030 — Machines and tutors are move sources, and a build is chosen one slot at a time

**Status:** Accepted · 2026-08-11

Recommendations only ever proposed level-up moves. In Gen 3 that is close to useless for a
playthrough: the coverage a team is actually short of arrives on a TM. A Charizard was
being told to consider what it learns by levelling while Flamethrower, Earthquake and Fire
Blast sat in the machine list, unmentioned.

PKHeX answers this through the learn sources already in use. `ILearnSource.GetAllMoves`
takes a `MoveSourceType` and is public, so machines and tutors need no new bundled data,
and — crucially — the answer is per game. Charizard has twenty tutor moves in Emerald and
one in FireRed. Merging that would have repeated exactly the mistake D-027 fixed, on a much
larger scale than levels ever did.

`ILevelUpLearnsetSource` therefore becomes `IMoveLearnSource`, returning level-up moves at
or below the current level, then machines, then tutors, each tagged with how it is obtained.
Level-up entries still come from `GetLearnset` rather than the flag scan, because it is the
only source that carries the level, and the level is what a player acts on.

**Egg moves are deliberately excluded.** A Pokémon already in the party cannot acquire one:
it had to hatch with it. Listing them would be the false certainty D-025 exists to prevent.

Everything that is not already on the Pokémon keeps
`RecommendationAvailability.RequiresAvailabilityCheck`. That stays honest for machines for a
reason particular to this generation: a Gen 3 TM is consumed when used, so even owning one
is not a guarantee.

Widening the pool immediately exposed a flaw in `SelectBuild`. Ranking every candidate in
isolation and taking the top four gave a Treecko three Grass moves, each explaining itself
as "the only Grass damage the party has for a type nothing else answers" — three times, for
one gap. Selection is now greedy: after each pick, damaging candidates repeating a type the
build already hits lose more than the same-type bonus is worth, and only the first move of a
type may claim the gap. A second same-type move can still win a slot, but it says plainly
that it is there because nothing else scored higher.

**Alternatives considered:** embed a TM/HM and tutor table of our own (rejected: PKHeX
already has one that is correct per game, and hand-entered tables are how invented data gets
in — see D-013), reach into PKHeX's internal `Tutor_E` and `GetIsTM` by reflection
(rejected: silently breaks on upgrade, which is the failure mode D-014 exists to prevent),
mark machine moves as available since most players eventually own the TM (rejected: single
use in Gen 3, and the label would be a guess), and cap the candidate pool by simply taking
fewer level-up moves (rejected: hides the machines that are the whole point).

---

## D-031 — A build is chosen for a team, not for a Pokémon in isolation

**Status:** Accepted · 2026-08-11

Once machines and tutors widened the candidate pool (D-030), three faults in `SelectBuild`
became impossible to miss. All three came from the same mistake: scoring each move on its
own and taking the top four.

**One Pokémon repeated itself.** A Treecko was handed Solar Beam, Giga Drain and Bullet
Seed, each explaining itself as the answer to the same gap. Selection is now greedy, and a
damaging move repeating a type the build already hits loses more than the same-type bonus is
worth.

**Six Pokémon repeated each other.** Members were built independently against one fixed
`TeamAnalysis`, so every one of them saw the same holes and reached for the same move. The
engine now builds in party order and threads a set of answered types through: it starts
from what the party's current moves already cover, and grows with every build chosen. The
same set also grows *inside* a build, which is what stops the second slot claiming a hole
the first slot just closed — the version before this genuinely printed "nothing else hits
Water hard" one line under a move that hits Water hard.

**Every slot was an attack.** Utility scored nothing unless the Pokémon was a wall, so
attackers got four attacks — not what a real set looks like, and helpless against anything
it cannot out-damage. Non-damaging moves now score for everyone, more for a Pokémon whose
bulk is the reason it is on the team, and a build takes at most three attacking moves while
a utility candidate remains.

Two smaller changes come with it. Moves that beat a type the party is *defensively* weak to
now score: being able to knock out what threatens you is the other half of a type problem,
and it is what "optimise against the rest of the team" means in practice. And every slot
carries a `BuildSlotRole` — same type, coverage, team support, utility, filler — so the
answer is scannable before it is read. The parallel `Moves`/`Reasons` lists that could drift
out of step are gone; a slot is one object.

Move candidates were already generation-correct, because the move catalog holds only the 354
Gen 3 moves and anything outside it resolves to nothing. That was incidental rather than
stated, so it is now pinned by a test.

**Alternatives considered:** score the whole party jointly and optimise across it (rejected:
the result stops being explainable per Pokémon, which is the property D-022 and D-028 are
built on), let each member see the others' *candidate pools* rather than their chosen builds
(rejected: a candidate is not a commitment, so the coordination would be based on something
that may never happen), and simply forbid a second move of a type outright (rejected:
sometimes a physical and a special move of the same type are both right — it should cost,
not be banned).

---

## D-032 — The competitive pool is the widest one, and every move says how it is obtained

**Status:** Accepted · 2026-08-11

Two faults found by using the app rather than reading it.

**The profiles were inverted.** The competitive profile drew its candidates *only* from the
pinned Random Battle sets, falling back to whatever the Pokémon happened to know when no set
matched. The playthrough profile drew from the full learn source. So a level 5 Treecko — no
Random Battle entry, because it is unevolved — was told to run Pound and Leer competitively,
while the playthrough profile offered it Solar Beam and Thunder Punch. Exactly backwards:
story play is the constrained case, battling is the one where any legal move is reachable.

The competitive profile now reads the whole learn source, **at level 100**, and keeps the
reference sets as a ranking prior rather than as the pool. A competitive Pokémon is a
trained one; nobody battles with the level it was caught at, and a move it learns at 45 is a
move it will have. Reference-set moves the learn source does not reach — egg moves, mostly —
are still kept, marked as coming from the set rather than from the game.

**A build never said how to get the move.** The panel told a player to run Dig with no hint
that it means finding TM28, and Thunder Punch with no hint that it means a move tutor. Every
slot now carries its source in the player's terms: *already knows it*, *learns it at level
16*, *from a TM or HM*, *from a move tutor*, *from a common set*.

One thing followed from widening the pool: with every legal move in reach, the build picked
Absorb over Solar Beam, because nothing in the score cared about how hard a move hits. Base
power was in `Gen3Rules` but private, so `IGenerationRules` now exposes it and a move earns
up to four points for it — enough to separate a 120-power move from a 20-power one, not
enough to outweigh coverage. Gen 3's power-1 sentinel for run-time-decided moves is read as
average rather than as almost nothing.

**Alternatives considered:** keep the competitive profile preset-only and accept that
unevolved species get nothing useful (rejected: it is the case where advice is most wanted,
since the player is deciding whether to evolve), use the Pokémon's current level for the
competitive pool (rejected: the profile already proposes exact EV spreads that require
training, so pretending the level is fixed is inconsistent), and rank strictly by base power
(rejected: it would hand every Pokémon four 120-power moves and ignore what the team needs).

---

## D-033 — Sprites come from the player's own cartridge, so the bridge learned to answer

**Status:** Accepted · 2026-08-11

Pokémon sprites are Nintendo's, and a public GPLv3 repository is not the place to
redistribute them. The player already owns a copy of every sprite they will ever see: it is
in the ROM they are playing. Reading them from there is the one route that is both
authentic and clean, and it needs nothing bundled.

**Nothing is hard-coded, because nothing can be.** The pointer tables move with every build
of every localisation — the Italian Emerald keeps its front-sprite table at `0x08300DDC`,
and no other release is obliged to agree — so a table of addresses would be a table of
guesses. They are found from the shape of the data instead: a run of eight-byte records,
each a valid ROM pointer with a size of `0x800` and a tag counting up from zero. Nothing
else in sixteen megabytes looks like that four hundred times over. Emerald animates its
front sprites and not its back ones, so the front table is the one whose entries decompress
to two frames; where that does not hold, the reader says it cannot tell rather than picking,
and the coloured tile stays.

**The bridge had to become two-way.** Scanning means reading, and the script only ever
spoke. Protocol 2 adds one command — `read`, an address and a length, answered with base64
bytes, capped at 256 KiB. The script still ships bytes and no meaning (D-006); it now ships
them on request as well as on change. This revises D-003's "the app sends no commands",
which was written when nothing needed to ask.

It reads a window, not the cartridge. Sixteen megabytes over the wire would stall emulation
for seconds; the tables sit near offset `0x300000`, so two reads normally suffice, widening
if they do not, and sprite data is fetched per species a few kilobytes at a time — only for
what is in the party, cached, failures included.

The same command is the missing input behind every "check availability in this save" the app
prints. The bag and the badge flags are in the same memory, and reaching them is now a
matter of knowing where rather than of building a channel.

Two things were nearly got wrong by assuming, and were caught by checking. Gen 3's internal
species order is not the national one plus a constant: Treecko is 252 and 277, which looks
like an offset of 25 until Pelipper turns out to be 279 and 310. The conversion comes from
PKHeX's table, and the test names the species where arithmetic would have been plausible
and wrong. And `StreamWriter` puts a byte-order mark in front of the first line it writes,
which is not JSON — found by a test, not by a user.

The decoder was verified the only way that really counts: six species decoded from the
Italian Emerald and looked at. Its unit tests are entirely synthetic, because no sprite byte
belongs in this repository.

**Alternatives considered:** bundle a sprite pack (rejected: not ours to redistribute),
have the script send the whole ROM on connect (rejected: twenty-one megabytes of base64 per
connection to use less than one), ask the user to locate the ROM file (rejected: it makes
someone hunt for a file already open in the emulator beside them, and it does nothing for
the bag), and hard-code the tables per game code (rejected: they differ per localisation, so
the constants would be wrong for most players and wrong invisibly).

### Amendment, 2026-08-11 — what the first live run cost

Everything above was written before the bridge had ever answered a real request. Three
things only showed up once it did.

**A socket takes what fits and tells you how much that was.** The script sent each line
with a single `client:send` and discarded the remainder, which for a 350 KB reply is most
of it. Two reads in twelve failed — always the first two, which are the ones the app needs
to start. There is an outbox per client now, pushed as far as the socket allows and flushed
on the next frame. Sixteen consecutive 256 KB reads succeed where ten of twelve did before.
The party stream had the same latent flaw and is fixed by the same queue.

**Telling the front table from the back one needs bytes the app has not fetched.** The
reader decompresses the first entry to count frames, and that data lives elsewhere in the
ROM — outside the window. The offline check passed for a reason the app did not have: the
whole cartridge was in memory. The choice moved to the caller, which is the only party that
can go and get more; the reader now reports candidates and stops there.

**The script already had a base64 encoder** for the party payload. The one added for `read`
was a second copy of it.

Verified live afterwards: Treecko, Charizard and Pikachu decoded through the production
path against the running emulator, 1.1 s for the first — a megabyte of window and the table
identification — and about 30 ms each after that.

### Amendment, 2026-08-11 — three things the first day of use found

**An unanswered read is not an answer.** The source treated a failed probe as "this game
has no sprites" and latched it, so closing the emulator for a moment left the tiles blank
for the rest of the session. Failure now says either *not now* or *never*, and only the
second is remembered — reached when the ROM has been read through and holds nothing we
recognise. The per-species cache follows the same rule: a decode that never received its
bytes is not recorded as a species without a sprite.

**The frame count only works where there are frames to count.** Emerald animates its front
sprites, so two frames means front; Ruby, Sapphire, FireRed and LeafGreen do not, and there
every candidate holds one frame. The tables are emitted in declaration order with the front
one first, so the lowest address is taken when the frame count decides nothing. That also
picks Emerald's still-front table if the animated one is ever missed, which is a fine
sprite. Verified on Emerald; the other four games remain unverified, and a wrong guess
there shows a back sprite rather than nothing — a visible, reportable failure rather than a
silent one.

---

## D-034 — A ROM can be asked which addresses it uses

**Status:** Accepted · 2026-08-11

Four more games arrived — Italian Ruby, Sapphire, FireRed and LeafGreen — and none of them
was supported. The table held `AXVE`, `AXPE`, `BPRE` and `BPGE`, the USA releases; the
Italian ones are `AXVI`, `AXPI`, `BPRI` and `BPGI`. Emerald had five localisations because
D-017 established they share a layout; the other four games had one apiece.

Adding them meant answering whether an Italian release keeps the USA addresses, and the
sources D-013 cites do not list these codes. So the question went to the ROMs themselves.

An address cannot be read off a ROM, but it is compiled into it: every routine that touches
the party loads it from a literal pool, so the value appears in the image again and again.
A value the game never uses appears zero times — a random 32-bit constant is expected 0.004
times in sixteen megabytes. That makes a clean test, and the Italian Emerald calibrates it,
because its address is already verified.

The result is diagonal, which is what a real signal looks like:

| ROM | Emerald address | FireRed/LeafGreen | Ruby/Sapphire |
| --- | --- | --- | --- |
| BPEI Emerald | **1091** | 1 | 3 |
| BPRI FireRed | 0 | **740** | 0 |
| BPGI LeafGreen | 0 | **740** | 0 |
| AXVI Ruby | 0 | 0 | **57** |
| AXPI Sapphire | 0 | 0 | **57** |

Each game uses exactly one family and nothing else. The four codes are therefore filed with
their USA counterparts, and `tools/check-gen3-addresses.py` reproduces the check for any ROM
— including one already on file, where a mismatch would mean the entry is wrong.

What this does *not* establish is that the layout at that address is what the parser
expects. Only loading the game shows that, and the parser fails loudly if it is wrong:
checksum validation rejects slots rather than inventing Pokémon (D-008).

The same four ROMs settled the open question from D-033. Only Emerald animates its front
sprites, so the frame count decides nothing elsewhere; all four turned out to hold exactly
two candidate tables, and decoding Bulbasaur from the lower-addressed one gives the front
sprite in every case. The fallback is now verified on five games rather than assumed on one.

**Alternatives considered:** add the codes and hope (rejected: that is how invented data
enters, and D-005 exists because of it), leave them unsupported until an external source
lists them (rejected: the evidence is stronger than a third-party table, and it is in the
user's own hand), and infer the address at run time by scanning for it (rejected: the scan
needs to know what it is looking for, which is the question).

---

## D-035 — A localisation can move the party, and one sample cannot say why

**Status:** Accepted · 2026-08-11

An Italian Ruby with a Mudkip in the party was reported as having none. The bridge was
connected, the game recognised, the read successful, and the answer zero — the most
convincing kind of failure, because nothing anywhere looks wrong.

Reading the live memory found it at once. The count byte the table pointed at held `00`;
sixteen bytes further on it held `01`, with the party data sixteen bytes after that.

That ROM is revision 1, so the first conclusion was that revisions move the party, and the
table gained per-revision overrides. **That conclusion was wrong.** The next game tried was
an Italian Sapphire, revision 0, and its party sits at exactly the same shifted address. One
sample supported two explanations and the wrong one was picked; a second sample was enough
to tell them apart.

The truth is that the Italian Ruby and Sapphire keep their party sixteen bytes above the USA
ones. A localisation can move what a revision does not — the opposite of D-017, where five
Emerald localisations share one layout. There is no rule here to generalise from, which is
the point: each game code is its own entry, and the ones that have been run are the ones
that are known.

| | USA, from the sources of D-013 | Italian, verified live |
| --- | --- | --- |
| count | `0x03004350` | `0x03004360` |
| party | `0x03004360` | `0x03004370` |

The revision machinery was removed with the conclusion that produced it. Nothing used it,
and a mechanism kept for a reason that turned out false is worse than no mechanism.

Two things are worth keeping from how this was found. It was found with the app's own
two-way bridge (D-033): thirty-two kilobytes of IWRAM in four requests, then the real parser
slid across every four-byte offset until `MUDKIP Lv.5 HP 21/21` appeared. A tool built for
sprites diagnosed the parser.

And it shows the limit of D-034's literal counting. That method proved these ROMs use the
Ruby/Sapphire address family and it was right — `0x03004360` genuinely appears fifty-seven
times, because in these releases it is the *count*. Counting a constant tells you the game
uses it. It cannot tell you what for.

**Alternatives considered:** keep the per-revision overrides in case they are needed later
(rejected: no entry uses them and the evidence for them evaporated), and find the party at
run time by scanning memory for something that parses (rejected: it works, as this
investigation shows, but searching for data that merely looks valid is how a wrong answer
becomes a confident one — D-005 and D-019 both exist because of that).

## D-036 — An egg fills a slot, not a place on the team

An egg in the party is a full Pokémon record on disk. It has a species, a personality value,
IVs, EVs, a nature, base stats and a level, and the parser reads every one of them. So every
layer above the parser treated it as a team member: its types were counted towards the
party's resistances, its moves towards the party's coverage, its level towards the team's
cohesion, and it was handed a role, a nature, an EV spread and a best moveset.

None of that is true of an egg. It cannot be sent out, cannot attack, cannot be taught a
move and cannot be given an item. A party of five Pokémon and an egg is a party of five.
Counting the egg credits the team with a switch-in it does not have, which is the worst kind
of wrong answer here: it is wrong in the player's favour, and it hides a real gap.

The contracts now draw the line once, and everything above reads it:

```csharp
public bool CanBattle => !IsEgg;                       // PokemonSnapshot
public IReadOnlyList<PokemonSnapshot> Battlers { get; } // PartySnapshot, the ones that can
```

`TeamAnalyzer`, `TeamStrengthAnalyzer` and `PokemonRecommendationEngine` iterate `Battlers`.
`Members` still exists and still holds all six slots, because the window has to draw the egg
— it is in the party and the player can see it there.

The second half is what the tile is allowed to say. The species inside an egg is in the
bytes, and the game deliberately does not show it: not knowing is the whole point of
hatching one. The window therefore shows an egg as **Egg** with no nickname, no types, no HP
bar, no stats, no matchups, no moves and no sprite, and one line saying why it is not part
of the team's numbers. The recommendation card is simply absent, because the engine produced
nothing for that slot; slots are paired to recommendations by `SlotIndex`, so a shorter list
lines up correctly rather than shifting everyone by one.

In place of the stats it does not have, the egg card shows the one number it does: for an
egg the friendship byte is the count of cycles still to run, one taken off every 256 steps,
so the card says how far there is left to walk.

The strength panel says what it left out rather than staying quiet about it — *"5 of 6 able
to battle; one slot holds an egg"* — because a player looking at six occupied slots and a
five-Pokémon score deserves to know which of the two the app believes.

The CLI keeps printing everything, egg included. It is the diagnostic surface of D-026, its
audience is whoever is debugging a capture, and hiding data there would remove the one place
the raw truth can be checked.

**Alternatives considered:** count the egg but weight it down (rejected: a fraction of a
resistance is not a thing that exists in a battle; either it can switch in or it cannot),
show the species with a spoiler warning (rejected: the app's job is to reflect the game, and
the game hides it), and drop eggs from `Members` entirely so they never reach the window
(rejected: the slot is occupied, and a party that shows five tiles when the game shows six
looks like the bug this fixed rather than the fix).

## D-037 — The next three levels beat the perfect level 100

**Status:** Accepted · 2026-08-12

The app could say what a Pokémon's best possible moveset is (D-031, D-032) and could not say
what it learns tomorrow. Those are not the same question, and for anyone playing through a
story the second one is the one being asked: whether to walk a bit more before the Gym,
whether to keep a weak move for two more levels, whether the Treecko is about to stop being
a Treecko. The optimal build answers a question you ask once; the next level answers one you
ask every evening.

It cost almost nothing. The per-game learn source of D-027 already knows the level of every
level-up move, so the moves half is a filter. The evolution half needed a new source, kept
behind `IEvolutionSource` in GameData for the same reason as the learnsets: the data is
PKHeX's, and Analysis must not depend on PKHeX (D-007).

**Where the honesty boundary falls.** A Treecko at Lv.10 learns Absorb at 6, Quick Attack at
11 and Pursuit at 16, and its table also lists Agility at 23 — but at 16 it becomes a Grovyle
and follows Grovyle's learnset from then on. Listing Agility would be a false promise with a
precise number attached, which is worse than saying nothing (D-025). So the list stops at the
evolution level and says why it stopped.

The case that made the model honest was found by a test rather than by reasoning. A Treecko
still a Treecko at Lv.30 has cancelled the evolution at every level since 16, and the game
offers it again at 31 — so its future is *one level long*, and the first version of this
happily listed its Lv.35 move. `EvolvesOnNextLevelUp` exists for that Pokémon.

**What a level means, per trigger.** The evolution table's Level column is only trustworthy
for some triggers, and its Argument column means a different thing for each: an item index
for UseItem and TradeHeldItem, a beauty value for Feebas, and a copy of the level everywhere
else. A level is recorded only where reaching it is genuinely sufficient — plain level ups,
the Tyrogue stat split, the Wurmple personality fork, and Ninjask. It is deliberately absent
for Shedinja (Lv.20 is when it *can* appear, not when it will), for stones, trades,
friendship and beauty. Kadabra levels to 100 and stays a Kadabra; a card that counts down to
a level it will never honour is worse than one that says "when traded".

The trigger is grouped by what the player has to *do* rather than by the game's internal
method, because LevelUpFriendshipMorning and LevelUpFriendshipNight are one decision to a
player and two rows to a ROM.

**Alternatives considered:** show the whole remaining learnset (rejected: it is the wrong
species' learnset past the evolution, and a scrolling list is not a decision), reach through
the evolution and show what the *evolved* form learns (rejected: it is a different Pokémon
with different moves at different levels, and merging the two is how a player ends up
waiting for a move that never comes — worth revisiting as a separate "after it evolves"
section), and put this in the recommendation engine (rejected: it is a fact about a
Pokémon, not a judgement about a build, and it must show for a game with no reference data).

## D-038 — Four small window decisions, taken together

**Status:** Accepted · 2026-08-12

Issues #12 to #15 were all about the same surface, so they were done as one pass. Each one
is small; what they have in common is worth writing down.

**The whole candidate pool is shown, not the four that won (#12).** The console printed every
candidate with how it is obtained and whether it needs checking; the window showed the four
chosen moves and the bare names of the rest. That is the half that cannot be argued with. A
player judges a recommendation by what it turned down — a set that skips Thunderbolt reads
very differently once you can see Thunderbolt was on the table and comes from a TM. The pool
now lists every candidate with its type, its source and its availability, with the four that
made the build ticked and fully lit while the rest stay legible but dimmer.

What is still missing is a stated *reason* for each rejection. The engine ranks candidates
and keeps the top four; it does not record a sentence per loser. Inventing one after the fact
would be a plausible story rather than the real cause, so the pool shows the facts it has.

**Immunities are their own list in the team panel (#13).** The per-Pokémon view already
separated them; the team view said "Resisted or immune". Both readings were defensible, but
the two views of the same fact disagreed, and disagreement is what a reader notices. A type
with an immunity is now filed under Immune even when others merely resist, because the
immunity is the better switch-in, and the detail counts both. The weakness line splits them
too: "2 weak, 1 immune, 1 resist" says something "2 safe" does not.

**Empty slots say the same thing in a tenth of the space (#15).** Five placeholders at 72 px
each filled the strip for a party of one, repeating "empty" five times. One bordered line
with a small marker per slot and "5 slots free" carries the same information without
implying the free slots are as interesting as the Pokémon.

**The window remembers where it was, and which profile was chosen (#14).** Size, position and
profile are stored in `settings.json`, beside the Lua script in the folder of D-029 that
survives updates and is never translocated. Three details are deliberate: a stored size below
the minimum is ignored, because a window saved at 40×20 by a crash reopens with no way out of
it; a maximised window stores the *pre-maximise* bounds and a flag, because saving the screen
bounds leaves no size to un-maximise back to; and every read and write failure ends in
defaults rather than a dialog, because a settings file is not worth a crash on startup.

The profile is restored because it is a statement about how the player wants to be advised,
not about one session — someone who plays competitively is still playing competitively
tomorrow.

**Alternatives considered:** make the candidate pool collapsible behind another toggle
(rejected: it already sits behind the best-set toggle, and a second one would hide the
honest part twice), keep one "safe" list and only fix the wording (rejected: it leaves the
two views phrased differently for no reason), drop the empty slots entirely (rejected: a
party of one is worth seeing as a party of one), and remember the layout in the OS-native
way per platform (rejected: three implementations of a file with five numbers in it).

## D-039 — melonDS answers, but one frame at a time

**Status:** Accepted · 2026-08-12

Gen 4 and 5 are Nintendo DS games, so D-001's emulator cannot run them. melonDS can, ships a
universal macOS build, and offers a GDB stub where mGBA offers Lua. Everything below was
measured against melonDS 1.1 running a real Pokémon Black, because the source told a
different story from the machine in three separate places.

**The JIT is not a price.** The run loop picks the JIT over the GDB stub, so reading memory
means running the interpreter — which sounded like the cost of the whole approach. It is not:
`JIT.Enable` has no entry in melonDS's defaults table, so a fresh install already runs the
interpreter, and on macOS Intel the JIT is not even compiled in. Measured on an Apple Silicon
Mac: **180 FPS** uncapped against a 60 FPS target, and a steady 60 while being hammered with
back-to-back reads.

**The ARM9 stub is broken; the ARM7 stub is not.** The obvious choice is the ARM9, which runs
the game. It completes the handshake and then closes the connection on the first command,
whatever the command is — `?`, `qSupported`, a memory read, every time, on fresh connections.
The ARM7 stub on port 3334 answers correctly, and main RAM is shared between the processors,
so it reaches everything we need. A defect in the tool we depend on, routed around rather
than fixed.

**Two protocol details that the source states wrongly.** A client must send a bare `+` as its
first byte: melonDS waits one second for it and swallows whatever arrives first, so a client
that opens with a packet loses its `$` to the handshake and gets hung up on. And the
documented read limit of 576 bytes is not real — 576 bytes become 1152 hex characters, which
no longer leave room for framing in a response buffer of exactly 1152. Reads near the limit
time out. 256 bytes works.

**The ceiling is one request per frame.** Each read takes ~16 ms, which is 1/60th of a second
on a 60 FPS target, and the measured throughput is 60 reads/s — the stub is polled once per
frame, so no amount of pipelining goes faster.

| | mGBA + Lua (D-002) | melonDS + GDB stub |
| --- | --- | --- |
| party read | pushed on change, ~600 B | 1408 B in 6 requests, **95 ms** |
| throughput | ~350 KB in one reply | **15 KB/s** |
| cost while polling | none observed | none observed, 60 FPS held |

95 ms per party is comfortable at one or two polls a second. The 15 KB/s ceiling is not
comfortable for everything: **sprites read from the cartridge (D-033) take ~350 KB in Gen 3,
which would be 23 seconds here.** Gen 4 and 5 will need a different answer for sprites, and
that is a difference in what the app can do per generation rather than a detail of the
provider.

Two behaviours the provider must own. Connecting *halts* the emulated CPU until a continue
arrives, and dropping the socket without detaching leaves the game frozen for whoever is
playing — which is exactly what happened repeatedly while measuring this. The provider
connects once and keeps the connection, rather than opening one per read.

**Alternatives considered:** wait for melonDS's Lua support (rejected: the pull request has
been open since April 2023, and the API in the fork that develops it has no memory read at
all, so it would not help even if it landed), BizHawk with the melonDS core (rejected: its
own README says Apple Silicon is not available — D-001 again, three years on), and running DS
games under a 3DS emulator (rejected: a 3DS switches to separate DS hardware rather than
emulating it, and Azahar's loader accepts no `.nds` file).

## D-040 — Pokémon Black, found the way Ruby was found

**Status:** Accepted · 2026-08-12

The first Gen 5 game read end to end, and the method is the one D-035 left behind: dump the
memory, slide a parser across every four-byte offset, and believe whatever survives a
checksum. 4 MB of main RAM came out of melonDS in 273 seconds with no lost blocks, and five
records passed PKHeX's `PK5` validation. Four were noise — a 16-bit checksum over a million
offsets produces coincidences, and a level 100 Wurmple with a nickname in Chinese characters
is one. The fifth was the Snivy that was actually in the party, level 6, HP 17/22, nickname
and all.

The finder was tested before it was trusted: a `PK5` we built ourselves, encrypted the way
the game writes it, was hidden in random bytes and had to be found. Without that, "nothing
found" would have meant either "no Pokémon" or "broken tool", and there would have been no
way to tell.

**The layout.** A party is a header and six fixed slots, with `PK5`'s 220-byte party form:

```
head + 0   capacity, always 6
head + 4   how many are carried
head + 8   slot 0, then 220 bytes each
```

**The address is reached through a pointer, not hardcoded.** The head sits at `0x022348AC`,
and exactly one word in all of main RAM points at it: `0x0224F88C`, the third entry in a
table of eighteen pointers that is plainly the game's own directory of save blocks. The
duplicate copy of the party found at `0x02268494` is pointed at by nothing, which is what
made it identifiable as an orphan rather than an alternative.

Both survived a ROM restart unchanged, so this game's heap is laid out deterministically and
a hardcoded address would work today. The pointer is still what gets followed, for the same
reason as D-005: it is the route the game itself takes, it costs one extra read of four
bytes, and it is the only one of the two that can still be right if the allocation ever
differs. What is read is then checked — capacity 6, count at most 6 — so a pointer that goes
somewhere wrong fails loudly rather than producing a party out of noise (D-008).

**The game says who it is, as in Gen 3.** The DS firmware copies the cartridge header into
main RAM at `0x027FFE00`, which mirrors to `0x023FFE00` in the 4 MB a DS actually has. Title
at +0, four-character game code at +0x0C, revision at +0x1E. This cartridge reads
`POKEMON B` / **`IRBI`** — Black, Italian — which is the same identification mechanism as
D-005, at a different address.

**What is not known.** One ROM, one language. D-035 exists because a single sample supported
two explanations and the wrong one was picked, so nothing here is generalised to White, to
Black 2 and White 2, or to any other language: `0x0224F88C` is recorded as the entry for
`IRBI` and for nothing else. The next Gen 5 cartridge is what turns one address into a rule
or into a table.

**Alternatives considered:** hardcode the party address (rejected: it works today and says
nothing about tomorrow, and the pointer costs one frame), search memory at run time for
something that parses (rejected for the third time, D-005 and D-019 and D-035 all exist
because data that merely looks valid is how a wrong answer becomes a confident one), and
trust the community's published Black/White addresses (rejected: they are for other regions,
and this cartridge is Italian — exactly the case that moved the party in D-035).

## D-041 — Gen 5 rules, and the one line that is not a copy

**Status:** Accepted · 2026-08-12

Gen 5 battle rules exist now, and most of them are the Gen 3 ones: the type chart did not
change between the second generation and the fifth, the twenty-five natures did not change,
and the stat formula did not change. What did change is the thing that decides every
recommendation the app makes.

**The physical/special split moved from the type to the move.** In Gen 3, Bite was a special
attack because Dark was a special type; from Gen 4 onwards Bite is physical because Bite is.
Copying `Gen3Rules` and swapping the number would have sent every Dark and Ghost attacker off
the wrong stat, and with it the role, the nature and the EV spread recommended for it — an
error with no visible symptom, since the output would still look like advice. `Gen5Rules`
reads a category per move, and a test asserts that the two generations disagree about Bite
and agree about Earthquake.

**Base power is per generation as well.** Tackle is 35 in Emerald, 50 in Black, and 40 today.
Showdown stores the current generation's numbers and walks backwards through per-generation
mods, so reading `data/moves.ts` alone gives Gen 9 values wearing Gen 5 names. The importer
applies gen8, gen7, gen6 and gen5 overrides in that order; it was Tackle that proved the
chain works, arriving at 50 through the gen6 mod because gen5 does not mention it.

**What is duplicated on purpose.** The type chart and the nature table ship as one file per
generation even though the two are byte-identical, so a number can always be traced to the
generation it belongs to instead of borrowing another's. A test compares the two charts entry
by entry, which turns the duplication from a risk into a checked fact. What is *not*
duplicated is code: the stat formula and the nature reader are shared, because two copies of
a calculation that drift apart are a bug in one of them and nothing else.

**Found by running it.** The console crashed on the first real Gen 5 party, because the team
analyser walks all four move slots and an empty slot carries no type — `Gen5Rules` validated
the type before the move id, where Gen 3 does the opposite. The tests all passed at the time.
A generation is not supported because its rules compile; it is supported when a party from a
real cartridge goes through the whole chain, which this one now does: Snivy, Grass, five
unanswered weaknesses, 32 out of 100.

**What Gen 5 still does not have.** No reference presets and no learn source, so the
competitive profile and the "Coming up" card have nothing to work from and say so rather than
guessing. Recommendations are Gen 3 only until a Gen 5 preset catalog exists.

**Alternatives considered:** derive the category from the type as Gen 3 does and accept the
error for Dark and Ghost (rejected: it is wrong for roughly half the movepool and invisible
in the output), take move data from PKHeX (rejected: it carries move type and PP but neither
base power nor category, so it cannot answer the question), and share one type chart file
between the generations (rejected: it saves a kilobyte and costs the ability to say which
generation a multiplier belongs to — the duplication is checked by a test instead).

## D-042 — Both emulators are watched at once, and nobody is asked which

**Status:** Accepted · 2026-08-12

With two emulators supported the obvious move is a menu: pick mGBA or melonDS. That question
has already been answered by whoever opened one of them, and the promise the app was built on
is that you open it and it works — no configuration, no 300 steps.

So `LiveTeamService` runs both pipelines side by side and adopts whichever produces a party.
Nothing is asked, nothing is remembered, and swapping from a GBA game to a DS one mid-session
needs no announcement: the most recent party wins, so the next poll simply arrives from the
other emulator and the window follows it.

Three details that only matter once both are running:

**The memory reader follows the active emulator.** Sprites are fetched from the machine the
team came from, not from whichever provider happens to be constructed first.

**Only the active emulator reports its state.** Otherwise the one that is *not* running keeps
announcing that it is reconnecting, over the top of the one that is working. Before either
has answered they both report, which is harmless — they are both connecting.

**Sprites stay a Gen 3 feature.** `RomSpriteSource` reads a GBA cartridge at GBA addresses
(D-033), so it is skipped entirely for a DS game. Without that guard it would hunt for tables
that are not there, down a channel that moves 15 KB a second (D-039), and spend half a minute
finding nothing.

The setup screen now carries both sets of instructions under their own headings, because the
two could not be less alike: mGBA needs a Lua script found and loaded by hand, while melonDS
needs a checkbox ticked and the JIT left off, and no file at all. Telling a Black player to
load `ups_bridge.lua` would be sending them looking for something that cannot help them.

Verified against the real composition with mGBA closed and melonDS running: `Connecting`,
`Connecting`, `Streaming`, then `connected via melonDS`, POKEMON B [IRBI], Snivy Lv.6.

**Alternatives considered:** a dropdown in the UI (rejected: it asks a question the user has
already answered by opening an emulator), remembering the last emulator used and trying it
first (rejected: it saves nothing — both are already tried at once — and it fails on the day
somebody switches), and picking by the ROM the app is told about (rejected: the app is not
told about a ROM, it finds one, which is the point of D-005).

## D-043 — Generations dispatch themselves

**Status:** Accepted · 2026-08-12

Gen 5 learnsets and evolutions now come from PKHeX the same way Gen 3's do, and adding them
raised a question that had not existed with one generation: who decides which tables answer.

`IMoveLearnSource.Supports(game)` and `IEvolutionSource.Supports(game)` already existed, so
the answer is the sources' own. `CompositeMoveLearnSource` and `CompositeEvolutionSource`
hold several and ask each in turn; `PKHeXSources` lists them in one place. Adding Gen 4 will
mean writing its source and adding one line, with nothing above it changed and nothing to
remember. The alternative — a switch on the generation somewhere in Analysis — is a second
place to update and the one people forget.

What the composite must never do is answer for the wrong generation, and the numbers are
close enough that a mix-up would look plausible: Treecko evolves at 16 in Emerald and Snivy
at 17 in Black. A test asks both and checks both answers.

Gen 5 also brings evolutions that no amount of levelling reaches. Karrablast becomes
Escavalier only by being traded for a Shelmet, so the card names the requirement and refuses
to count down to a level that will never arrive — the honesty boundary of D-037, meeting the
first generation that really tests it.

**What this does not fix.** Recommendations are still Gen 3 only. The engine needs a
reference preset catalog as well as a learn source, and there is no Gen 5 one: the window now
says so on screen rather than showing an empty card. Learnsets were the cheaper half and they
were worth having first — knowing that a Snivy learns Vine Whip next level is useful to
someone playing tonight, in a way that an optimal level 100 build is not.

**Alternatives considered:** one source per generation resolved by a registry keyed on
`PokemonGeneration` (rejected: a game code, not a generation, is what identifies tables —
Black and Black 2 are both Gen 5 and teach differently), and giving the analysers a list of
sources to try (rejected: it puts the dispatch in every caller instead of in one place).

## D-044 — Recommendations pick their reference data by generation

**Status:** Accepted · 2026-08-12

Gen 5 gets recommendations. The engine used to hold one preset catalog and one move catalog
and refuse anything that did not match them; it now holds them keyed by generation and looks
up the party's, in the same way it already keyed the two profiles by kind. A catalog knows
which generation it is for, so nothing else has to be told, and Gen 4 will be two more
entries.

The data is Showdown's Gen 5 Random Battle sets from the commit already pinned for Gen 3 —
388 species, 606 sets — and it carries exactly the standing D-024 gave the Gen 3 ones:
expert-authored examples of what a species can do, not standard competitive play and not a
claim about this save.

The Gen 5 tables answering rather than the Gen 3 ones is checked by something only they
have: Grass Pledge appears among a Snivy's tutor candidates, and there is no such move in
Emerald.

**A test had to be rewritten rather than kept.** An hour earlier the dashboard asserted that
a Gen 5 party gets no build and is told so on screen. That was true when it was written and
is now false, so it was replaced by the opposite assertion rather than worked around. A test
that describes a limitation has a shelf life; what it was really guarding — that the window
and the analysis agree about what exists — is what the replacement checks.

**Alternatives considered:** keep one catalog and construct a second engine for Gen 5
(rejected: two engines mean two places for a policy change to be forgotten), and resolve
catalogs from the game code rather than the generation (rejected: unlike learnsets, reference
sets do not differ between Black and Black 2 — and inventing a distinction that the data does
not make is how a table grows entries nobody can justify).

## D-045 — The artwork comes from the player, animated, and we ship none of it

**Status:** Accepted · 2026-08-12

D-033 reads sprites out of the player's own cartridge, which works because a Game Boy Advance
maps its ROM into memory. A Nintendo DS does not: the cartridge is read in blocks through a
register interface, so there is no address to point at, and what the emulator will answer for
moves at 15 KB/s (D-039). The Gen 5 dashboard therefore had no picture in it.

Decoding the sprites out of the DS ROM file was attempted and got most of the way — the
filesystem, the `a/0/0/4` archive, LZ11 decompression and the NCGR headers all read correctly,
96×96 at 4bpp — and then stopped at the pixel layout, which is neither of the two obvious
conventions. That work is not wasted but it is not finished either, and it would only ever
have solved Gen 5.

**What was done instead keeps D-033's principle and covers everything.** The player supplies a
folder of sprites; the app reads it. One Black and White style set covers Gen 1 through Gen 5,
so an Emerald team and a Black team look like they belong to the same app. The cartridge stays
the fallback for a GBA game, so nothing that worked before stopped working.

**No Pokémon artwork enters this repository or any release.** It belongs to Nintendo, Game
Freak and Creatures, and the collections that gather it — Project Pokémon's, PokeAPI's — state
no licence of their own. `tools/fetch-sprites.py` downloads it to the player's disk on their
own machine, because the alternative was clicking a thousand times; the tool contains no art
and the app ships none. The test fixtures are a hand-made two-frame GIF, red then blue, for
the same reason.

**Animated, at the file's own speed.** Avalonia draws a GIF's first frame and stops, so the
frames are taken apart with SkiaSharp — already inside Avalonia, so no new dependency — and
played at the durations the file declares rather than at one invented rate, which would make
every sprite move alike and some of them wrong. Frames compose cumulatively, because a GIF
frame stores only what changed. A still image starts no timer at all, and closing the window
cancels the ones that are running.

**Size was measured rather than argued about.** 649 fronts are 27 MB and the shinies another
27. Animated WebP was tried and saves **6%**: a small palette animation is precisely what GIF
is good at, so recompression is not the lever. The levers are fetching fewer files, so shinies
are now opt-in — a missing shiny falls back to the ordinary sprite, and the tile marks it with
a star regardless — and `--up-to 386` fetches Gen 1 to 3 for 13 MB. None of this touches the
47 MB download, which carries no sprites at all.

**Alternatives considered:** bundle the sprites (rejected: it is the one thing the project has
been careful never to do, and no licence permits it), fetch them from inside the app on demand
(rejected: it would put a network call in an app that works offline, and the saving is a few
hundred kilobytes against a one-off 27 MB), finish the NARC decoder first (rejected: weeks of
format archaeology for one generation, when a folder solves five), and ship static PNGs
instead of animations (rejected: they are the same size, and the animation is the reason to
have them).
