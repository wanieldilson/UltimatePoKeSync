using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Sprites;
using UltimatePoKeSync.Parsing;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// Fetches Pokémon sprites out of the running game's ROM, through the bridge.
/// </summary>
/// <remarks>
/// <para>
/// Nothing is bundled and nothing is guessed. The pointer tables move with every build of
/// every localisation, so they are found by scanning, and the scan needs the bytes the
/// tables live in — which is what the read command exists for. See D-033.
/// </para>
/// <para>
/// It reads a window rather than the whole cartridge. Sixteen megabytes over the wire would
/// stall the emulator for seconds; the tables have been found near ROM offset 0x300000, and
/// the window widens if they are not there. Sprite data itself is fetched per species, a
/// few kilobytes at a time, and only for what is actually in the party.
/// </para>
/// </remarks>
public sealed class RomSpriteSource
{
    private const uint RomBase = 0x08000000;
    private const int RomSize = 32 * 1024 * 1024;
    private const int ChunkSize = 64 * 1024;

    /// <summary>Enough for one compressed sprite; LZ77 stops at its own declared size.</summary>
    private const int SpriteBlockSize = 8 * 1024;

    private const int PaletteBlockSize = 512;

    /// <summary>
    /// Where the tables have been found so far, widened until they turn up. Ordered by how
    /// likely they are, so the common case costs two reads.
    /// </summary>
    private static readonly (int Start, int Length)[] SearchWindows =
    [
        (0x2C0000, 0x100000),
        (0x000000, 0x400000),
        (0x400000, 0x400000),
    ];

    private readonly IEmulatorMemoryReader _reader;
    private readonly Dictionary<int, DecodedSprite?> _cache = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    private GameIdentity? _game;
    private byte[]? _rom;
    private Gen3SpriteTables? _tables;

    /// <summary>
    /// Set only once the ROM has been read through and found to hold nothing we can use.
    /// A read that simply went unanswered must not land here: the emulator being closed
    /// for a moment is not the same as a game having no sprites, and treating it as such
    /// left the tiles blank for the rest of the session. See issue #1.
    /// </summary>
    private bool _unavailable;

    public RomSpriteSource(IEmulatorMemoryReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    /// <summary>
    /// The front sprite for a national dex number, or <see langword="null"/> when this game
    /// cannot give one. Cached, including the failures: a species that could not be decoded
    /// once will not decode on the next party either.
    /// </summary>
    /// <summary>What came of an attempt, and whether it is worth trying again.</summary>
    private enum Outcome
    {
        /// <summary>It worked.</summary>
        Ok,

        /// <summary>The emulator did not answer. Nothing is settled; ask again later.</summary>
        NotNow,

        /// <summary>Asked and answered: there is nothing here. Stop asking.</summary>
        Never,
    }

    /// <summary>
    /// The front sprite for a national dex number in a given game.
    /// </summary>
    /// <remarks>
    /// The game is a parameter rather than a constructor argument because a player can
    /// swap the cartridge under a running app. Everything here — the ROM image, the table
    /// offsets, the decoded sprites — belongs to one game, and keeping it across a change
    /// means decoding FireRed with Ruby's tables. See issue #17.
    /// </remarks>
    public async Task<DecodedSprite?> TryGetAsync(
        GameIdentity game,
        int nationalSpeciesId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (!_reader.CanRead)
        {
            return null;
        }

        int internalId = Gen3SpeciesIndex.ToInternal(nationalSpeciesId);
        if (internalId <= 0)
        {
            return null;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!game.Equals(_game))
            {
                Forget(game);
            }

            if (_unavailable)
            {
                return null;
            }

            if (_cache.TryGetValue(internalId, out DecodedSprite? cached))
            {
                return cached;
            }

            if (await EnsureTablesAsync(cancellationToken).ConfigureAwait(false) != Outcome.Ok)
            {
                return null;
            }

            (DecodedSprite? sprite, Outcome outcome) =
                await DecodeAsync(internalId, cancellationToken).ConfigureAwait(false);

            // Only a settled answer is worth remembering. Caching a read that never came
            // back would retire the species for good over a hiccup. See issue #2.
            if (outcome != Outcome.NotNow)
            {
                _cache[internalId] = sprite;
            }

            return sprite;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Drops everything belonging to the game that was here before.</summary>
    private void Forget(GameIdentity game)
    {
        _game = game;
        _rom = null;
        _tables = null;
        _unavailable = false;
        _cache.Clear();
    }

    private async Task<Outcome> EnsureTablesAsync(CancellationToken cancellationToken)
    {
        if (_tables is not null)
        {
            return Outcome.Ok;
        }

        _rom ??= new byte[RomSize];

        // One unanswered read settles it quickly: an older script drains commands without
        // replying, and waiting out the timeout on every chunk of every window would take
        // minutes to reach the same conclusion. It settles *when to retry*, though, not
        // whether the game has sprites.
        if (await _reader.ReadMemoryAsync(RomBase, 4, cancellationToken).ConfigureAwait(false) is null)
        {
            return Outcome.NotNow;
        }

        bool searchedEverything = true;

        foreach ((int start, int length) in SearchWindows)
        {
            if (!await FetchAsync(start, length, cancellationToken).ConfigureAwait(false))
            {
                // A window we never received is a window we cannot rule out.
                searchedEverything = false;
                continue;
            }

            Outcome identified = await TryIdentifyAsync(cancellationToken).ConfigureAwait(false);
            if (identified == Outcome.Ok)
            {
                return Outcome.Ok;
            }

            if (identified == Outcome.NotNow)
            {
                searchedEverything = false;
            }
        }

        if (!searchedEverything)
        {
            return Outcome.NotNow;
        }

        // The whole ROM was read and holds nothing we recognise. That will not change.
        _unavailable = true;
        return Outcome.Never;
    }

    /// <summary>
    /// Picks the front table out of the candidates the scan found. Front and back tables
    /// are identical in shape; only their data tells them apart, and the data is elsewhere
    /// in the ROM — so each candidate's first sprite has to be fetched before it can be
    /// judged. This is the step that cannot happen inside the reader, which only ever sees
    /// the bytes it was handed. See D-033.
    /// </summary>
    private async Task<Outcome> TryIdentifyAsync(CancellationToken cancellationToken)
    {
        if (_rom is null)
        {
            return Outcome.Never;
        }

        Gen3TableCandidates candidates = Gen3SpriteReader.FindCandidates(_rom);
        if (candidates.PaletteTable == 0 || candidates.SpriteTables.Count == 0)
        {
            return Outcome.Never;
        }

        bool sawEveryCandidate = true;
        Gen3TableCandidate? firstReadable = null;

        foreach (Gen3TableCandidate candidate in candidates.SpriteTables)
        {
            int first = Gen3SpriteReader.GetEntryTarget(_rom, candidate.Offset, 0);
            if (first <= 0)
            {
                continue;
            }

            if (!await FetchAsync(first, SpriteBlockSize, cancellationToken).ConfigureAwait(false))
            {
                sawEveryCandidate = false;
                continue;
            }

            if (!Lz77.TryDecompress(_rom, first, out byte[] frames))
            {
                continue;
            }

            // Emerald animates its front sprites and not its back ones, so two frames
            // means front, unambiguously.
            if (frames.Length == Gen3SpriteReader.AnimatedFrameBytes)
            {
                Remember(candidate, candidates.PaletteTable);
                return Outcome.Ok;
            }

            firstReadable ??= candidate;
        }

        // Ruby, Sapphire, FireRed and LeafGreen do not animate, so every candidate holds
        // one frame and the test above decides nothing. The tables are emitted in
        // declaration order and the front one is declared first, so the lowest address is
        // it — which also picks Emerald's still-front table when the animated one is
        // missed. Verified on Emerald only; the other four remain unchecked. See issue #3.
        if (firstReadable is not null)
        {
            Remember(firstReadable, candidates.PaletteTable);
            return Outcome.Ok;
        }

        return sawEveryCandidate ? Outcome.Never : Outcome.NotNow;
    }

    private void Remember(Gen3TableCandidate candidate, int paletteTable) =>
        _tables = new Gen3SpriteTables(candidate.Offset, paletteTable, candidate.EntryCount);

    private async Task<(DecodedSprite? Sprite, Outcome Outcome)> DecodeAsync(
        int internalId,
        CancellationToken cancellationToken)
    {
        if (_rom is null || _tables is not Gen3SpriteTables tables)
        {
            return (null, Outcome.Never);
        }

        // The table entries are in the window already; the data they point at is not.
        int spritePointer = ReadPointer(tables.FrontPicTable + (internalId * 8));
        int palettePointer = ReadPointer(tables.PaletteTable + (internalId * 8));

        if (spritePointer <= 0 || palettePointer <= 0)
        {
            return (null, Outcome.Never);
        }

        if (!await FetchAsync(spritePointer, SpriteBlockSize, cancellationToken).ConfigureAwait(false) ||
            !await FetchAsync(palettePointer, PaletteBlockSize, cancellationToken).ConfigureAwait(false))
        {
            return (null, Outcome.NotNow);
        }

        return Gen3SpriteReader.TryReadFrontSprite(_rom, tables, internalId, out DecodedSprite sprite)
            ? (sprite, Outcome.Ok)
            : (null, Outcome.Never);
    }

    private int ReadPointer(int offset)
    {
        if (_rom is null || offset < 0 || offset + 4 > _rom.Length)
        {
            return 0;
        }

        uint address = (uint)(_rom[offset]
            | (_rom[offset + 1] << 8)
            | (_rom[offset + 2] << 16)
            | (_rom[offset + 3] << 24));

        return address >= RomBase && address < RomBase + RomSize ? (int)(address - RomBase) : 0;
    }

    /// <summary>Reads a range into the local image, in chunks the script will accept.</summary>
    private async Task<bool> FetchAsync(int offset, int length, CancellationToken cancellationToken)
    {
        if (_rom is null || offset < 0 || offset + length > _rom.Length)
        {
            return false;
        }

        for (int done = 0; done < length; done += ChunkSize)
        {
            int size = Math.Min(ChunkSize, length - done);
            byte[]? chunk = await _reader
                .ReadMemoryAsync(RomBase + (uint)(offset + done), size, cancellationToken)
                .ConfigureAwait(false);

            if (chunk is null || chunk.Length != size)
            {
                return false;
            }

            chunk.CopyTo(_rom, offset + done);
        }

        return true;
    }
}
