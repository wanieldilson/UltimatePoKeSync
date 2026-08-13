# Emulator → app bridge protocol

Current version: **2**

Every provider speaks this protocol, whatever the emulator. It is the reason adding
BizHawk or DeSmuME costs a script rather than a refactor (D-006).

## Transport

TCP over loopback. **The emulator script is the server**, the app is the client (D-003).

- Default address: `127.0.0.1:8888`
- One message per line, terminated by `\n`
- UTF-8, single-line JSON (no embedded newlines)
- Since version 2 the app may send commands, one per line, same encoding. Version 1 had
  none, and the script drained its receive buffer purely to stop the socket stalling.

On connect, the script **immediately** sends the last known state, if it has one. Without
this, an app started while the game is idle would stay empty until the player changed
something in the party.

## The `party` message

Emitted only when the party bytes or the count **actually change**, not every frame. The
script compares a 32-bit FNV-1a of the raw bytes.

```json
{
  "v": 1,
  "type": "party",
  "seq": 42,
  "frame": 123456,
  "game": { "code": "BPEE", "title": "POKEMON EMER", "rev": 0, "gen": 3 },
  "count": 3,
  "slotSize": 100,
  "slots": 6,
  "data": "<base64>"
}
```

| Field        | Type   | Meaning |
| ------------ | ------ | ------- |
| `v`          | int    | Protocol version. The app rejects versions it does not know. |
| `type`       | string | Message discriminator. |
| `seq`        | int    | Monotonic counter. Lets the app detect dropped or out-of-order messages. |
| `frame`      | int    | Emulator frame at capture time, for diagnostics. |
| `game.code`  | string | Four-character game code from the header (`BPEE`, `BPRE`, …). See D-005. |
| `game.title` | string | Internal ROM title. |
| `game.rev`   | int    | Header revision byte, offset `0xBC`. |
| `game.gen`   | int    | Generation. |
| `count`      | int    | Party size according to the emulator, 0-6. **Treat as advisory.** |
| `slotSize`   | int    | Bytes per slot. Gen 3: 100. |
| `slots`      | int    | Number of slots in the blob. Always 6 for Gen 3. |
| `data`       | string | Base64 of `slotSize * slots` bytes, exactly as they sit in RAM. |

### Why `count` is not trustworthy

It is read from a single byte that can be sampled while the game is updating it. The app
uses it as an upper bound and validates every slot independently anyway (checksum +
stability across two reads). See D-008.

### Why the bytes are raw

In Gen 3 the data in RAM is encrypted with `PID xor OTID` and has its four substructures
permuted according to the PID. The script **does not touch it**: the `data` field contains
exactly what is in memory. Decoding is `UltimatePoKeSync.Parsing`'s job (D-007).

## The `read` command  ·  since v2

```json
{"type": "read", "id": 7, "address": 137365468, "length": 262144}
```

| Field     | Type | Meaning |
| --------- | ---- | ------- |
| `id`      | int  | Echoed back, so a reply can be matched to its request. |
| `address` | int  | Absolute address, as the emulator sees it. ROM starts at `0x08000000`. |
| `length`  | int  | Bytes to read. Capped at 256 KiB per request by `UPS_MAX_READ`. |

The reply, or an `error` message with the same `id`:

```json
{"v": 2, "type": "memory", "id": 7, "address": 137365468, "length": 262144, "data": "<base64>"}
```

```json
{"v": 2, "type": "error", "id": 7, "message": "unreadable range"}
```

### Why the app is allowed to ask

Sprite data lives in the ROM behind pointer tables whose addresses move with every build of
every localisation: the Italian Emerald keeps its front-sprite table at `0x08300DDC`, and
no other release is obliged to agree. They cannot be shipped as constants, so the app finds
them by scanning the ROM's own structure, and to scan it has to read. The same command is
what will later reach the bag and the badge flags, which is the missing input behind every
"check availability in this save" the app prints today. See D-033.

A read is answered from emulator memory as it stands; nothing is cached and nothing is
interpreted. The script still ships no meaning, only bytes (D-006).

## Error handling

The script sends no error messages over the socket: it logs to the mGBA console and stops
producing `party` messages. Cases:

- **Unrecognised ROM** → no messages. The script refuses to read rather than guess a
  memory map, because guessing produces plausible but invented Pokémon (D-005).
- **Port in use** → the server does not start, with a message explaining how to change
  `UPS_PORT`. There is no auto-increment: the client has to know where to connect.
- **Client dropped** → removed from the list, emulation continues.

## Forward compatibility

The `v` field is the explicit break point. Adding fields is backwards compatible and does
not bump `v`; changing the meaning of an existing field does.

For generations where the party is not one contiguous region, the `party` message will
gain an optional `regions` field with named blocks, keeping `data` for the contiguous case.
