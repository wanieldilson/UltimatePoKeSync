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
