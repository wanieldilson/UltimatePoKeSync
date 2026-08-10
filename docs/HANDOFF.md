# Handoff — state of the project

Last updated: 2026-08-10, during milestone M6.

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

The first M6 recommendation slice is operational:

```
PartySnapshot --> team + role facts --> PokemonRecommendationEngine --> selected profile
                                                                    |-> playthrough
                                                                    `-> competitive
```

It infers broad roles, calculates exact Gen 3 projected stats, proposes nature and EV
plans, and combines those facts with pinned offline Pokémon Showdown role/movepool
references. The two profiles share the fact engine but return different policy.

**101 tests green** — 60 analysis, 24 parsing, 12 session, 5 provider.

## What does not exist yet

- Save-specific playthrough availability. Party RAM does not expose the bag, badges, map
  progress, Move Reminder access or transfer history, so uncertain candidates are labelled
  as requiring an availability check (D-025).
- Game-specific level-up catalogs and competitive usage/speed-benchmark weighting. The
  current pinned learnset is generation-wide and the presets are Random Battle references,
  not standard OU statistics.
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
| **M6** | **Per-Pokémon suggestions (EVs, nature, moves, item)** | **in progress** |
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
10. **A legal candidate is not necessarily available in the current save.** Do not remove
    or reinterpret `RecommendationAvailability`: it is the honesty boundary while bag and
    progression facts are absent (D-025).

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

## M6 — implementation notes and next work

The public entry point is
`PokemonRecommendationEngine.Recommend(PartySnapshot, RecommendationProfileKind)`.
It computes `TeamAnalysis` and `PokemonRoleAnalysis` once, resolves generation rules,
then delegates policy to `IRecommendationProfile`.

Implemented:

- all 25 Gen 3 natures and exact integer stat projection, including EV limits and Shedinja;
- explainable broad roles based on base stats, current moves and the Gen 3 type-based
  physical/special split;
- playthrough priorities and competitive exact EV/nature/item candidates;
- pinned Pokémon Showdown Random Battle references: 220 species, 393 role/movepool sets,
  354 moves and generation-wide level-up data for all 386 Gen 3 species;
- deterministic fallback to the current moveset when no external preset exists;
- candidate availability labels rather than unsupported claims about the current save.

The checked-in datasets are generated by `tools/import-showdown-gen3-data.mjs`, pinned to
revision `db93869dcc216c0be39e7f86e9a64edcc7496d89`, and covered by
`THIRD_PARTY_NOTICES.md`. Smogon Dex editorial sets are not bundled; their application
reuse requires permission (D-024).

Next, decide how much of the remaining availability gap belongs in M6. The smallest useful
slice is an Emerald-specific level-up source plus explicit caller-supplied available items.
Parsing badges, bag contents and world progress is a larger input contract and should not
be smuggled into the pure analyzer. Competitive refinements can later add pinned,
MIT-licensed ladder usage weights and real speed benchmarks without changing the profile
boundary.

## Useful references

- mGBA scripting API: <https://mgba.io/docs/scripting.html>
- mGBA's own example scripts (`res/scripts/pokemon.lua`, `socketserver.lua`) — an
  authoritative source for both Gen 3 addresses and idiomatic socket usage.
- PKHeX source: <https://github.com/kwsch/PKHeX>
- Gen 3 data structure: <https://bulbapedia.bulbagarden.net/wiki/Pok%C3%A9mon_data_structure_(Generation_III)>
