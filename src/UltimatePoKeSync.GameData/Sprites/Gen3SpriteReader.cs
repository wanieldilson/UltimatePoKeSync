namespace UltimatePoKeSync.GameData.Sprites;

/// <summary>A decoded sprite: straight RGBA, ready for whatever draws it.</summary>
public sealed record DecodedSprite(int Width, int Height, byte[] Rgba);

/// <summary>One table that has the shape of a sprite table.</summary>
public sealed record Gen3TableCandidate(int Offset, int EntryCount);

/// <summary>What a structural scan can say on its own.</summary>
public sealed record Gen3TableCandidates(
    IReadOnlyList<Gen3TableCandidate> SpriteTables,
    int PaletteTable);

/// <summary>
/// Where a Gen 3 ROM keeps its sprite tables. Found by scanning, never hard-coded.
/// </summary>
/// <param name="FrontPicTable">Offset of the front-sprite pointer table, ROM-relative.</param>
/// <param name="PaletteTable">Offset of the palette pointer table, ROM-relative.</param>
/// <param name="EntryCount">Entries in both tables.</param>
public sealed record Gen3SpriteTables(int FrontPicTable, int PaletteTable, int EntryCount);

/// <summary>
/// Reads Pokémon sprites out of a Gen 3 ROM.
/// </summary>
/// <remarks>
/// <para>
/// The table addresses are not constants. They move with every build of every
/// localisation — the Italian Emerald keeps its front-sprite table at ROM offset
/// <c>0x300DDC</c>, and no other release is obliged to agree — so shipping a table of
/// addresses would be shipping a guess. They are found instead from the shape of the data:
/// a front-pic table is a run of eight-byte records, each a valid ROM pointer, a size of
/// <c>0x800</c>, and a tag counting up from zero. Nothing else in the ROM looks like that
/// for four hundred entries. See D-033.
/// </para>
/// <para>
/// Emerald animates its front sprites, so their entries decompress to two frames while the
/// back sprites decompress to one. That is what tells the front table from the back one,
/// and it is also why this only claims to work where it has been checked: on a game whose
/// front sprites are still, the two would be indistinguishable by shape alone, and the
/// reader reports that it cannot tell rather than picking.
/// </para>
/// </remarks>
public static class Gen3SpriteReader
{
    private const int RomBase = 0x08000000;
    private const int EntrySize = 8;
    private const int SpriteBytes = 0x800;
    private const int MinimumEntries = 300;
    private const int TileSize = 8;
    private const int TilesPerRow = 8;
    private const int SpriteSide = TileSize * TilesPerRow;
    private const int PaletteColours = 16;

    /// <summary>How many frames a front sprite holds where they are animated.</summary>
    public const int AnimatedFrameBytes = SpriteBytes * 2;

    /// <summary>
    /// The candidate sprite tables and the palette table, by structure alone.
    /// </summary>
    /// <remarks>
    /// Structure cannot say which candidate holds the front sprites: front and back tables
    /// look identical until their data is decompressed, and the data lives elsewhere in the
    /// ROM. A caller reading the whole image can use <see cref="TryFindTables"/>; a caller
    /// fetching windows over a wire has to pick between these itself, because it is the one
    /// that can go and get the bytes. See D-033.
    /// </remarks>
    public static Gen3TableCandidates FindCandidates(ReadOnlySpan<byte> rom)
    {
        List<(int Offset, int Count)> spriteTables = FindTables(rom, expectSizeField: true);
        List<(int Offset, int Count)> paletteTables = FindTables(rom, expectSizeField: false);

        return new Gen3TableCandidates(
            [.. spriteTables.Select(table => new Gen3TableCandidate(table.Offset, table.Count))],
            paletteTables.Count == 0 ? 0 : paletteTables[0].Offset);
    }

    /// <summary>The ROM offset an entry of a table points at, or 0.</summary>
    public static int GetEntryTarget(ReadOnlySpan<byte> rom, int table, int index) =>
        TryReadPointer(rom, table + (index * EntrySize), out int target) ? target : 0;

    /// <summary>
    /// Finds the tables in a complete ROM image. Needs the whole thing, because telling the
    /// front table from the back one means decompressing a sprite.
    /// </summary>
    public static bool TryFindTables(ReadOnlySpan<byte> rom, out Gen3SpriteTables tables)
    {
        tables = new Gen3SpriteTables(0, 0, 0);
        Gen3TableCandidates candidates = FindCandidates(rom);

        if (candidates.SpriteTables.Count == 0 || candidates.PaletteTable == 0)
        {
            return false;
        }

        // Animated front sprites decompress to two frames; back sprites to one.
        foreach (Gen3TableCandidate candidate in candidates.SpriteTables)
        {
            if (DecompressedSizeOfFirstEntry(rom, candidate.Offset) == AnimatedFrameBytes)
            {
                tables = new Gen3SpriteTables(
                    candidate.Offset, candidates.PaletteTable, candidate.EntryCount);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Decodes one species' front sprite. <paramref name="internalSpeciesId"/> is the
    /// game's own index, which is not the national number: Hoenn starts at 277.
    /// </summary>
    public static bool TryReadFrontSprite(
        ReadOnlySpan<byte> rom,
        Gen3SpriteTables tables,
        int internalSpeciesId,
        out DecodedSprite sprite)
    {
        sprite = new DecodedSprite(0, 0, []);

        if (internalSpeciesId < 0 || internalSpeciesId >= tables.EntryCount)
        {
            return false;
        }

        if (!TryReadPointer(rom, tables.FrontPicTable + (internalSpeciesId * EntrySize), out int tiles) ||
            !TryReadPointer(rom, tables.PaletteTable + (internalSpeciesId * EntrySize), out int palette))
        {
            return false;
        }

        if (!Lz77.TryDecompress(rom, tiles, out byte[] pixels) ||
            !Lz77.TryDecompress(rom, palette, out byte[] colours) ||
            pixels.Length < SpriteBytes ||
            colours.Length < PaletteColours * 2)
        {
            return false;
        }

        sprite = Compose(pixels, colours);
        return true;
    }

    /// <summary>
    /// Turns 4bpp tiles and a 16-colour palette into RGBA. The GBA stores an image as
    /// 8×8 tiles in reading order, two pixels to a byte, low nibble first; colour zero is
    /// the transparent one.
    /// </summary>
    private static DecodedSprite Compose(ReadOnlySpan<byte> tiles, ReadOnlySpan<byte> palette)
    {
        var rgba = new byte[SpriteSide * SpriteSide * 4];

        Span<byte> red = stackalloc byte[PaletteColours];
        Span<byte> green = stackalloc byte[PaletteColours];
        Span<byte> blue = stackalloc byte[PaletteColours];

        for (int colour = 0; colour < PaletteColours; colour++)
        {
            int value = palette[colour * 2] | (palette[(colour * 2) + 1] << 8);

            // Five bits per channel, so each is scaled to eight rather than shifted, which
            // would leave white at 248 instead of 255.
            red[colour] = (byte)((value & 31) * 255 / 31);
            green[colour] = (byte)(((value >> 5) & 31) * 255 / 31);
            blue[colour] = (byte)(((value >> 10) & 31) * 255 / 31);
        }

        for (int tile = 0; tile < TilesPerRow * TilesPerRow; tile++)
        {
            int originX = tile % TilesPerRow * TileSize;
            int originY = tile / TilesPerRow * TileSize;

            for (int row = 0; row < TileSize; row++)
            {
                for (int pair = 0; pair < TileSize / 2; pair++)
                {
                    byte packed = tiles[(tile * 32) + (row * 4) + pair];

                    for (int half = 0; half < 2; half++)
                    {
                        int index = (packed >> (4 * half)) & 0x0F;
                        int x = originX + (pair * 2) + half;
                        int destination = (((originY + row) * SpriteSide) + x) * 4;

                        rgba[destination] = red[index];
                        rgba[destination + 1] = green[index];
                        rgba[destination + 2] = blue[index];
                        rgba[destination + 3] = index == 0 ? (byte)0 : (byte)255;
                    }
                }
            }
        }

        return new DecodedSprite(SpriteSide, SpriteSide, rgba);
    }

    private static List<(int Offset, int Count)> FindTables(
        ReadOnlySpan<byte> rom,
        bool expectSizeField)
    {
        var found = new List<(int, int)>();

        for (int offset = 0; offset + EntrySize <= rom.Length; offset += 4)
        {
            if (!IsEntry(rom, offset, 0, expectSizeField))
            {
                continue;
            }

            int count = 0;
            while (IsEntry(rom, offset + (count * EntrySize), count, expectSizeField))
            {
                count++;
            }

            if (count >= MinimumEntries)
            {
                found.Add((offset, count));
                offset += count * EntrySize;
            }
        }

        return found;
    }

    /// <summary>
    /// A table entry: a pointer into this ROM, then either a size and a tag (sprites) or a
    /// tag and two bytes of padding (palettes). The tag counts up, which is what makes a
    /// run of them recognisable.
    /// </summary>
    private static bool IsEntry(
        ReadOnlySpan<byte> rom,
        int offset,
        int expectedTag,
        bool expectSizeField)
    {
        if (offset < 0 || offset + EntrySize > rom.Length ||
            !TryReadPointer(rom, offset, out _))
        {
            return false;
        }

        int second = rom[offset + 4] | (rom[offset + 5] << 8);
        int third = rom[offset + 6] | (rom[offset + 7] << 8);

        return expectSizeField
            ? second == SpriteBytes && third == expectedTag
            : second == expectedTag && third == 0;
    }

    /// <summary>Reads a ROM pointer and converts it to an offset into the image.</summary>
    private static bool TryReadPointer(ReadOnlySpan<byte> rom, int offset, out int target)
    {
        target = 0;

        if (offset < 0 || offset + 4 > rom.Length)
        {
            return false;
        }

        long address = (uint)(rom[offset]
            | (rom[offset + 1] << 8)
            | (rom[offset + 2] << 16)
            | (rom[offset + 3] << 24));

        if (address < RomBase || address >= RomBase + rom.Length)
        {
            return false;
        }

        target = (int)(address - RomBase);
        return true;
    }

    private static int DecompressedSizeOfFirstEntry(ReadOnlySpan<byte> rom, int table) =>
        TryReadPointer(rom, table, out int data) && Lz77.TryDecompress(rom, data, out byte[] bytes)
            ? bytes.Length
            : 0;
}
