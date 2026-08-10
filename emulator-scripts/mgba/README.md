# mGBA bridge

`ups_bridge.lua` reads the party from RAM and ships it to the app over TCP.

## Requirements

mGBA **0.10.0 or later** — Lua scripting does not exist in earlier versions. Tested with
0.10.5.

## Usage

1. Start mGBA and load your Gen 3 ROM.
2. `Tools` → `Scripting…`
3. In the scripting window: `File` → `Load script…` and pick `ups_bridge.lua`.
4. The console should show:

   ```
   [UltimatePoKeSync] listening on 127.0.0.1:8888
   [UltimatePoKeSync] game recognised: Emerald (USA) [BPEE] rev0
   ```

5. Start the app:

   ```
   dotnet run --project src/UltimatePoKeSync.Cli
   ```

Order does not matter: the script works whether loaded before or after the ROM, and the
app can start before mGBA — it waits and connects on its own.

## Supported games

| Game code | Game                        |
| --------- | --------------------------- |
| `BPEE`    | Emerald (USA)               |
| `BPEI`    | Emerald (Italy)             |
| `BPEF`    | Emerald (France)            |
| `BPED`    | Emerald (Germany)           |
| `BPES`    | Emerald (Spain)             |
| `BPRE`    | FireRed (USA)               |
| `BPGE`    | LeafGreen (USA)             |
| `AXVE`    | Ruby (USA)                  |
| `AXPE`    | Sapphire (USA)              |

Every Western Emerald localisation shares the same RAM layout (D-017). Japanese releases
and the European FireRed/LeafGreen versions have not been mapped yet. On an unrecognised ROM the script
**refuses to read** and says so in the console, instead of guessing: reading with the
wrong map would produce plausible but invented Pokémon.

## Common problems

**`port 8888 already in use`** — another mGBA instance is already running the bridge, or
another program holds the port. Change `UPS_PORT` at the top of the script, reload it, and
start the app with `--port <the same port>`.

**`unsupported game`** — the ROM is not in the table. Check the game code printed in the
console.

**Nothing in the console** — the script was not loaded: the scripting window has to stay
open.

## What it does (and does not do)

It reads the party-count byte and the 600 bytes of the six slots, and ships them **raw**,
base64-encoded. It does not decrypt, validate checksums or translate IDs: all of that
happens on the C# side, once, shared across every emulator (see D-006 in
`docs/DECISIONS.md`).

It polls the party 15 times per second and transmits only when the bytes actually change.
A change is sent only after a second identical read confirms it, so a state captured while
the game was writing to memory is never transmitted (D-008).

Full protocol: [`docs/protocol.md`](../../docs/protocol.md).
