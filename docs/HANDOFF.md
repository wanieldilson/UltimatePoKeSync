# Handoff — state of the project

Last updated: 2026-08-10, after milestone M5.

Read this first, then [`DECISIONS.md`](DECISIONS.md) for the reasoning behind every choice.
`DECISIONS.md` is the authority; this file is orientation.

---

## What works today

The full chain is **live and verified against real hardware**:

```
mGBA + Lua script  --TCP--> MGbaProvider --> Gen3PartyParser (PKHeX) --> PartyTracker --> CLI
   raw bytes                 reconnect        decode + validate          decide changes
```

Verified on 2026-08-10 with **Pokémon Emerald (Italy)** running in mGBA 0.10.5: the starter
appeared with correct species, level, type, ability, moves and IVs, with zero rejected
slots. PP dropping during a battle produced exactly one snapshot per change.

The M5 analysis chain is also complete:

```
PartySnapshot --> Gen3Rules (embedded chart + move data) --> TeamAnalyzer --> TeamAnalysis
```

It reports all 17 defensive matchups (including ability modifiers), all offensive coverage
from currently known damaging moves, and the unanswered gaps. Results keep the contributing
Pokémon and moves so the UI can explain them rather than displaying an opaque score.

**75 tests green** — 34 analysis, 24 parsing, 12 session, 5 provider.

## What does not exist yet

- Per-Pokémon role inference and suggestions (EVs, nature, moves, item) — M6.
- Playthrough and competitive profile heuristics — M6; the M5 engine deliberately produces
  profile-independent facts only.
- `UltimatePoKeSync.App` — Avalonia placeholder window only. Real dashboard is M7.

## Milestones

| # | Scope | State |
| - | ----- | ----- |
| M0 | Environment, solution, contracts | done |
| M1 | Lua bridge for mGBA | done |
| M2 | TCP transport with reconnect | done |
| M3 | Gen 3 parsing via PKHeX, CLI output | done, verified on real RAM |
| M4 | Party tracking, change suppression, real-RAM fixtures | done |
| M5 | Gen 3 type chart + team analysis | done |
| **M6** | **Per-Pokémon suggestions (EVs, nature, moves, item)** | **next** |
| M7 | Avalonia dashboard | not started |
| M8 | Second provider or generation, to prove the abstraction | not started |

---

## Environment (important)

- **.NET 10 SDK is installed at `~/.dotnet` and is NOT on the default PATH.** Every shell
  that runs dotnet needs `export PATH="$HOME/.dotnet:$PATH"`. There is also an unrelated
  .NET at `/usr/local/share/dotnet` belonging to VS Code's C# Dev Kit — do not confuse them.
- mGBA 0.10.5 at `/Applications/mGBA.app`.
- The development ROM is Italian Emerald in `roms/` (git-ignored, never commit it).
- macOS, Apple Silicon (arm64).

### Process hygiene — this bit matters

On 2026-08-10 accumulated .NET tooling processes reached ~5 GB of RAM and the session had
to be force-quit. Every `dotnet build` / `test` / `run` leaves persistent MSBuild nodes and
a `VBCSCompiler` alive for 15 minutes, and `pkill -f upks` kills the app but not the parent
`dotnet run` or the build servers.

So:

```bash
export PATH="$HOME/.dotnet:$PATH"
export MSBUILDDISABLENODEREUSE=1
dotnet build -m:1
dotnet build-server shutdown   # after a batch of builds
```

Avoid long-lived background `dotnet run`. Keep live runs short and foreground, and check
with `ps` afterwards that nothing survived.

### Running it live

mGBA has **no command-line option to load a script** (checked `--help` on 0.10.5), so the
script must be loaded through the GUI:

1. `open -a mGBA roms/<rom>.gba`
2. In mGBA: `Tools` → `Scripting…` → `File` → `Load script…` →
   `emulator-scripts/mgba/ups_bridge.lua`
3. `dotnet run --project src/UltimatePoKeSync.Cli`

`--dump <dir>` writes every raw snapshot as a JSON fixture.

---

## Working agreements

- **Commits:** commit often. **Never push** — Roberto does that. **Never** add a
  `Co-Authored-By` trailer.
- **Language:** everything in the repo is English — code, comments, docs, commit messages,
  CLI output, test names. Conversation with Roberto is in Italian.
- **Decision log:** every design choice goes into `docs/DECISIONS.md`, in the same commit as
  the change, with the alternatives considered and the reasoning.

---

## Things that will bite you

Each of these cost real time to discover. They are all recorded in `DECISIONS.md`.

1. **mGBA has no filesystem I/O in Lua.** No `io.open`. TCP is the only transport (D-002).
2. **`socket.connect` in Lua is blocking**, so the script is the server and the app is the
   client (D-003).
3. **PKHeX normalises types to modern indices.** Gen 3 internal IDs put `???` at index 9, so
   internally Fire is 10 — but `PersonalTable.E[id].Type1` returns 9 for Charizard. Do not
   write a conversion. Pinned by a test, because a PKHeX upgrade could change it silently
   and every type calculation would be wrong with no visible error (D-014).
4. **`ChecksumValid` alone is not enough.** An all-zero slot passes it. And PKHeX's `Valid`
   property stays `true` even for random bytes, so it is useless as a filter (D-008).
5. **Unused party slots are not zeroed in real RAM.** Leftover bytes from a Pokémon
   deposited in the PC are a complete, checksum-valid Pokémon. Never read past the declared
   party count or you will show ghost team members (D-019).
6. **Gen 3 nature is derived from the PID** (`PID % 25`), not stored. Setting `Nature` on a
   `PK3` with a fixed PID does nothing — remember this when building fixtures (D-014).
7. **Mono-type Pokémon repeat their type** in both fields. Normalise the second to `None` or
   it counts twice in defensive maths (D-015).
8. **The two-read confirmation must stay in the Lua script.** The script only transmits on
   change, so a second identical read never reaches C# — the check is impossible there by
   construction (D-008).
9. **Damaging does not always mean type coverage.** Gen 3 uses base power `1` as a sentinel
   for fixed-damage, one-hit knockout and variable-power moves. Seismic Toss and Fissure do
   not gain super-effective damage; Low Kick and Hidden Power do. `Gen3Rules` distinguishes
   them explicitly (D-022).

---

## M5 — implementation notes

The public entry point is `TeamAnalyzer.Analyze(PartySnapshot)`. It resolves
`IGenerationRules` from the snapshot's generation and fails explicitly when unsupported.

`TeamAnalysis` always contains 17 defensive and 17 offensive entries for Gen 3, plus:

- `DefensiveGaps`: a party weakness with no resistant or immune switch-in.
- `OffensiveGaps`: a defending type no current damaging move hits super effectively.

The Gen 3 chart and all 355 move base-power values are embedded JSON under
`UltimatePoKeSync.GameData/Data`. Both were mechanically cross-checked against the matching
`pret/pokeemerald` source before M5 was committed. See D-021 and D-022.

Ability adjustments implemented: Levitate, Wonder Guard, Flash Fire, Volt Absorb, Water
Absorb and Thick Fat.

## M6 — what comes next

Goal: infer each Pokémon's role and suggest EVs, nature, moves and held item.

Two Gen 3 rules that **change the answers**, not just the numbers. Build them in from the
start; M5 already exposes them through `IGenerationRules` (D-009).

**1. Seventeen types, no Fairy.** The type chart must be selected per generation. Also
relevant for Gen 3: Ghost is not resisted the same way as in Gen 1, and `PokemonType.Fairy`
must simply never appear.

**2. The physical/special split is by TYPE, not by move.** Every move of a given type uses
the same attack stat, whatever the move is. In Gen 3:

| Category | Types |
| -------- | ----- |
| Physical | Normal, Fighting, Flying, Poison, Ground, Rock, Bug, Ghost, Steel |
| Special  | Fire, Water, Grass, Electric, Psychic, Ice, Dragon, Dark |

So a Gyarados (base Atk 125, base SpA 60) gets nothing from a Water move in Gen 3, because
Water is special. Role inference and therefore recommended nature and EVs follow completely
different logic from Gen 4 onwards.

Existing placement:

- `UltimatePoKeSync.GameData` — embedded Gen 3 chart and move powers, `ITypeChart`,
  `IGenerationRules`, the physical/special split and ability modifiers.
- `UltimatePoKeSync.Analysis` — pure functions over `PartySnapshot`, no I/O.
- `UltimatePoKeSync.Analysis.Tests` — rules and team-coverage tests.

M6 still needs nature data and the recommendation heuristics. Prefer adding facts to the
core analysis before profile policy consumes them; do not make the core analyzer accept a
profile.

Remember D-010: two profiles, **playthrough** and **competitive**, sharing one engine. The
engine computes facts (role, coverage, projected stats); the profile decides what to
recommend from those facts. Keep the separation from the first line of code.

## Useful references

- mGBA scripting API: <https://mgba.io/docs/scripting.html>
- mGBA's own example scripts (`res/scripts/pokemon.lua`, `socketserver.lua`) — an
  authoritative source for both Gen 3 addresses and idiomatic socket usage.
- PKHeX source: <https://github.com/kwsch/PKHeX>
- Gen 3 data structure: <https://bulbapedia.bulbagarden.net/wiki/Pok%C3%A9mon_data_structure_(Generation_III)>
