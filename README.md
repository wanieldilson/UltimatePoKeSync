# UltimatePoKeSync

A cross-platform desktop app (Windows, Linux, macOS including Apple Silicon) that analyses
your Pokémon party in real time by reading an emulator's RAM directly. No manual entry:
load the script, play, and the team shows up.

It computes the team's aggregate weaknesses and resistances and suggests EVs, nature,
moves and held item for each Pokémon, with two analysis profiles — **playthrough** and
**competitive**.

## Status

Early development, but usable. **M7 is complete:** there is a real desktop app. It shows
your party live, the team's 17-type coverage with an attributed strength score, and per
Pokémon the role, nature, effort values and a recommended set — for the story or for
competitive play. Current target: **Gen 3 / Pokémon Emerald** via **mGBA**. The
architecture is multi-emulator and multi-generation from the first commit.

## Install

Download the file for your system from the [releases page](../../releases), unpack it and
run it. Nothing else to install — the .NET runtime is inside.

| System | File |
| ------ | ---- |
| Windows | `UltimatePoKeSync-windows-x64.zip` |
| macOS, Apple Silicon | `UltimatePoKeSync-macos-apple-silicon.zip` |
| macOS, Intel | `UltimatePoKeSync-macos-intel.zip` |
| Linux | `UltimatePoKeSync-linux-x64.tar.gz` |

On macOS the app is signed ad-hoc rather than by Apple, so the first launch has to be
right-click → Open → Open. Every launch after that is a normal double-click.

If macOS says the app is **damaged and should be moved to the Trash**, the download predates
`v0.1.1` and has no signature at all, which Apple Silicon refuses to run. Get a newer
release, or repair the copy you have:

```bash
xattr -dr com.apple.quarantine /path/to/UltimatePoKeSync.app
codesign --force --deep --sign - /path/to/UltimatePoKeSync.app
```

The app opens on a setup screen with the steps for your system and the path of the script
to load into mGBA. Once the script is running, the team appears by itself.

## How it works

```
mGBA + Lua script  --TCP 127.0.0.1:8888-->  .NET app  -->  PKHeX.Core  -->  analysis  -->  Avalonia UI
   (raw bytes)                               (client)      (parsing)
```

The Lua script interprets nothing: it ships the raw party bytes and the game identity. All
decoding happens on the C# side. Adding another emulator means writing a new script, not
touching the app's logic.

## Requirements

- mGBA 0.10.5 or later (Lua scripting exists from 0.10.0)
- A Gen 3 ROM you own — none is included, and none ever will be
- The .NET 10 SDK, only if you build from source

## Running from source

Needs the .NET 10 SDK.

```bash
dotnet run --project src/UltimatePoKeSync.App   # the dashboard
dotnet run --project src/UltimatePoKeSync.Cli   # the diagnostic console
```

Either way, load [`emulator-scripts/mgba/ups_bridge.lua`](emulator-scripts/mgba/ups_bridge.lua)
in mGBA through `Tools` → `Scripting…` → `File` → `Load script…`, with the ROM loaded
before or after — it does not matter.

Add `--analyze` for team coverage and `--recommend playthrough` (or `competitive`) for
per-Pokémon suggestions. `--replay <fixture.json>` renders a snapshot dumped with `--dump`
and exits, so the whole chain can be checked without the emulator:

```bash
dotnet run --project src/UltimatePoKeSync.Cli -- \
  --replay tests/UltimatePoKeSync.Parsing.Tests/Fixtures/emerald-it-treecko.json \
  --analyze --recommend playthrough
```

Detailed instructions and troubleshooting:
[`emulator-scripts/mgba/README.md`](emulator-scripts/mgba/README.md).

## Layout

| Path                      | Contents |
| ------------------------- | -------- |
| `emulator-scripts/mgba/`  | Lua script that reads RAM and ships it over TCP |
| `src/…Contracts/`         | The architectural boundary: raw bytes, not Pokémon. Zero dependencies |
| `src/…Providers.MGba/`    | TCP client with reconnect. Knows nothing of PKHeX or game rules |
| `src/…Parsing/`           | Bytes → Pokémon via PKHeX |
| `src/…GameData/`          | Per-generation type chart, natures, heuristics |
| `src/…GameData.Learnsets/`| Per-game level-up learnsets, read from PKHeX |
| `src/…Analysis/`          | Team coverage, roles, suggestions |
| `src/…Cli/`               | Headless diagnostic console |
| `src/…App/`               | Avalonia dashboard |

## Documentation

- [`docs/HANDOFF.md`](docs/HANDOFF.md) — current state, environment quirks, what works and
  what is next. Read this first if you are picking the project up.
- [`docs/DECISIONS.md`](docs/DECISIONS.md) — a record of every design choice, with the
  alternatives considered and the reasoning. The authority on why anything is the way it is.
- [`docs/protocol.md`](docs/protocol.md) — the emulator-to-app protocol.

## Licence

GPLv3. See [`LICENSE`](LICENSE).

The project uses [PKHeX.Core](https://github.com/kwsch/PKHeX) (GPL-3.0-or-later) to parse
Pokémon data structures; its copyleft extends through linking, so the whole app is GPLv3.
See D-007 in the decision log.
