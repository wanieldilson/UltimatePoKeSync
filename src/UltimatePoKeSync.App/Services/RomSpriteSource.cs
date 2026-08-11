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
    private const int ChunkSize = 256 * 1024;

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

    private byte[]? _rom;
    private Gen3SpriteTables? _tables;
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
    public async Task<DecodedSprite?> TryGetAsync(
        int nationalSpeciesId,
        CancellationToken cancellationToken = default)
    {
        if (_unavailable || !_reader.CanRead)
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
            if (_cache.TryGetValue(internalId, out DecodedSprite? cached))
            {
                return cached;
            }

            if (!await EnsureTablesAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            DecodedSprite? sprite = await DecodeAsync(internalId, cancellationToken)
                .ConfigureAwait(false);
            _cache[internalId] = sprite;
            return sprite;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> EnsureTablesAsync(CancellationToken cancellationToken)
    {
        if (_tables is not null)
        {
            return true;
        }

        _rom ??= new byte[RomSize];

        // One unanswered read is enough to know: an older script drains commands without
        // replying, and waiting out the timeout on every chunk of every window would take
        // minutes to reach the same conclusion.
        if (await _reader.ReadMemoryAsync(RomBase, 4, cancellationToken).ConfigureAwait(false) is null)
        {
            _unavailable = true;
            return false;
        }

        foreach ((int start, int length) in SearchWindows)
        {
            if (!await FetchAsync(start, length, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (Gen3SpriteReader.TryFindTables(_rom, out Gen3SpriteTables tables))
            {
                _tables = tables;
                return true;
            }
        }

        // Nothing that looks like a sprite table. Say so once and stop asking: a game we
        // cannot read sprites from will not start being readable later.
        _unavailable = true;
        return false;
    }

    private async Task<DecodedSprite?> DecodeAsync(int internalId, CancellationToken cancellationToken)
    {
        if (_rom is null || _tables is not Gen3SpriteTables tables)
        {
            return null;
        }

        // The table entries are in the window already; the data they point at is not.
        int spritePointer = ReadPointer(tables.FrontPicTable + (internalId * 8));
        int palettePointer = ReadPointer(tables.PaletteTable + (internalId * 8));

        if (spritePointer <= 0 || palettePointer <= 0)
        {
            return null;
        }

        if (!await FetchAsync(spritePointer, SpriteBlockSize, cancellationToken).ConfigureAwait(false) ||
            !await FetchAsync(palettePointer, PaletteBlockSize, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return Gen3SpriteReader.TryReadFrontSprite(_rom, tables, internalId, out DecodedSprite sprite)
            ? sprite
            : null;
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
