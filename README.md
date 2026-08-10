# UltimatePoKeSync

A cross-platform desktop app (Windows, Linux, macOS including Apple Silicon) that analyses
your Pokémon party in real time by reading an emulator's RAM directly. No manual entry:
load the script, play, and the team shows up.

It computes the team's aggregate weaknesses and resistances and suggests EVs, nature,
moves and held item for each Pokémon, with two analysis profiles — **playthrough** and
**competitive**.

## Status

Early development. Current target: **Gen 3 / Pokémon Emerald** via **mGBA**. The
architecture is multi-emulator and multi-generation from the first commit.

## How it works

```
mGBA + Lua script  --TCP 127.0.0.1:8888-->  .NET app  -->  PKHeX.Core  -->  analysis  -->  Avalonia UI
   (raw bytes)                               (client)      (parsing)
```

The Lua script interprets nothing: it ships the raw party bytes and the game identity. All
decoding happens on the C# side. Adding another emulator means writing a new script, not
touching the app's logic.

## Requirements

- .NET 10 SDK
- mGBA 0.10.5 or later (Lua scripting exists from 0.10.0)
- A Gen 3 ROM you own — none is included, and none ever will be

## Quick start

1. In mGBA: `Tools` → `Scripting…` → `File` → `Load script…` →
   [`emulator-scripts/mgba/ups_bridge.lua`](emulator-scripts/mgba/ups_bridge.lua)
2. Load the ROM (before or after, it does not matter).
3. `dotnet run --project src/UltimatePoKeSync.Cli`

Detailed instructions and troubleshooting:
[`emulator-scripts/mgba/README.md`](emulator-scripts/mgba/README.md).

## Layout

| Path                      | Contents |
| ------------------------- | -------- |
| `emulator-scripts/mgba/`  | Lua script that reads RAM and ships it over TCP |
| `src/…Contracts/`         | The architectural boundary: raw bytes, not Pokémon. Zero dependencies |
| `src/…Providers.MGba/`    | TCP client with reconnect. Knows nothing of PKHeX or game rules |
| `src/…Parsing/`           | Bytes → Pokémon via PKHeX. The only project that depends on PKHeX |
| `src/…GameData/`          | Per-generation type chart, natures, heuristics |
| `src/…Analysis/`          | Team coverage, roles, suggestions |
| `src/…Cli/`               | Headless diagnostic console |
| `src/…App/`               | Avalonia UI |

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
