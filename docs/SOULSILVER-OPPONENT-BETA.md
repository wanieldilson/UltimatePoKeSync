# SoulSilver opponent reader: beta test guide

This experimental command-line tool reads opponent Pokémon from a running, unmodified
USA/Australia copy of Pokémon SoulSilver. It is deliberately separate from the dashboard:
the goal of the beta is to verify SoulSilver's live opponent memory layout before any larger
Gen 4 feature is built.

The tool is read-only. It sends memory-read commands to melonDS and never writes to the game,
ROM or save file. No ROM, save or Nintendo artwork is included.

## Supported setup

- Pokémon SoulSilver, USA/Australia, internal game code `IPGE`
- The unmodified No-Intro release is the reference image. Its decrypted SHA-1 is
  `F8DC38EA20C17541A43B58C5E6D18C1732C7E582`.
- melonDS 1.1 or later
- .NET 10 SDK

ROM hacks, randomisers, translations, HeartGold and non-English releases are not supported by
this beta. The tool checks the internal game code and refuses anything except `IPGE`.

## Windows setup

1. Install the [.NET 10 SDK for Windows](https://dotnet.microsoft.com/download/dotnet/10.0).
   Choose the SDK, not just the Desktop Runtime, and use the x64 download unless the Windows
   machine is ARM-based.
2. Open a new PowerShell window and confirm the installation:

   ```powershell
   dotnet --version
   ```

   The version should begin with `10.`. If PowerShell still says `dotnet` is not recognised,
   sign out of Windows and back in, then try again.
3. [Download the `soul-silver-opponents` branch as a ZIP](https://github.com/wanieldilson/UltimatePoKeSync/archive/refs/heads/soul-silver-opponents.zip),
   extract it, and open PowerShell in the extracted repository folder. The folder should
   contain `UltimatePoKeSync.slnx`.
4. Open melonDS and load SoulSilver.
5. Open **Config → Emu settings**, enable **GDB stub**, and leave the JIT recompiler off.
6. Restart the game after changing those settings.
7. In PowerShell, from the repository folder, run:

   ```powershell
   dotnet run --project .\tools\UltimatePoKeSync.SoulSilverOpponent -- --diagnostics
   ```

   The first run downloads the required NuGet packages and may take a minute. Later runs use
   the local package cache.
8. Enter a wild or trainer battle. The terminal redraws when validated opponent data changes.
   Press **Ctrl+C** to stop the reader cleanly.

If Windows Defender Firewall asks about melonDS, allow it on **Private networks**. The reader
only connects to melonDS on the same computer through `127.0.0.1`, using GDB port 3334 or 3333.

## macOS and Linux setup

1. Open melonDS and load SoulSilver.
2. Open **Config → Emu settings**.
3. Enable **GDB stub** and leave the JIT recompiler off.
4. Restart the game after changing those settings.
5. From the repository root, run:

   ```bash
   dotnet run --project tools/UltimatePoKeSync.SoulSilverOpponent
   ```

6. Enter a wild or trainer battle. The terminal redraws when the validated opponent data
   changes. Press **Ctrl+C** to stop.

If `dotnet` is not installed on macOS, install the .NET 10 SDK with Homebrew, then retry the
command:

```bash
brew install dotnet
```

## Useful beta options

These examples work in Windows PowerShell:

```powershell
# Include candidate addresses and validation results in the display.
dotnet run --project .\tools\UltimatePoKeSync.SoulSilverOpponent -- --diagnostics

# Produce one append-only sample that can be pasted into a bug report.
dotnet run --project .\tools\UltimatePoKeSync.SoulSilverOpponent -- --once --diagnostics --no-clear

# Select one GDB stub explicitly if the automatic 3334/3333 fallback is not wanted.
dotnet run --project .\tools\UltimatePoKeSync.SoulSilverOpponent -- --port 3333
```

Run `--help` for the complete option list. The default one-second interval is intentional:
melonDS limits each GDB memory reply, and polling far faster makes the game less pleasant
without making opponent changes meaningfully clearer.

## What to test

Please try these separately and note which ones work:

- Start the reader in the overworld, then enter and leave a wild battle.
- Enter a trainer battle with one Pokémon.
- Enter a trainer battle whose party contains several Pokémon.
- Let the opponent lose HP, use a move, switch, and faint.
- Enter a normal double battle.
- If available, enter a battle against two separate opposing trainers.
- Close and reopen the ROM while the reader keeps running.
- Stop the reader with Ctrl+C and confirm the game continues normally.

The most valuable report includes:

- melonDS version and operating system;
- whether the ROM is the standard `IPGE` release or patched;
- the exact command used;
- what was on the game screen and what the terminal showed;
- the output from `--once --diagnostics --no-clear` taken during the problem.

Do **not** attach or commit a ROM, save file, savestate or full RAM dump. Diagnostic output
contains addresses and Pokémon facts only. If a captured Pokémon nickname or original trainer
name is personally identifying, redact it before posting.

## Known beta limitations

- The complete opponent party may be present in RAM before the game reveals it. The tool shows
  every validated record it finds, including unrevealed party members, moves and held items.
- Temporary battle effects such as stat stages, confusion, Substitute and Transform are not
  read. These live in a different battle-only structure.
- Published HGSS tools disagree about one opponent-manager offset. This reader tests both known
  layouts, accepts only checksum-valid Gen 4 records and names the successful candidate in
  diagnostic output. Real `IPGE` reports are needed to remove the fallback.
- Battle allocations may retain old bytes briefly. If an opponent remains after returning to
  the overworld, report it: battle-state detection is intentionally part of this beta rather
  than an unverified hardcoded flag.
- Battle Frontier, link, Wi-Fi, Safari Zone and scripted tutorial battles are not yet verified.

## Troubleshooting

**The reader says it is waiting for melonDS.** Confirm the GDB stub is enabled, JIT is off and
melonDS was restarted after the setting changed. Another debugger may already own the stub. On
Windows, also check that melonDS is allowed through Defender Firewall on Private networks.

**The game freezes after the terminal is killed.** Ctrl+C performs a clean detach. Force-killing
the process can leave a melonDS GDB stub wedged; reload the ROM to clear it, then start the
reader again.

**It detects SoulSilver but finds no opponent.** Run the one-shot diagnostic command during a
battle and include its output in the report. A checksum failure is evidence of a wrong layout,
not a Pokémon the tool should guess at.
