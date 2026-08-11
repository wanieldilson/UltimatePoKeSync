using UltimatePoKeSync.GameData.Sprites;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

/// <summary>
/// Everything here is built by hand. Real sprite bytes are Nintendo's, so the repository
/// holds none of them; the reader was checked end to end against a real ROM by decoding a
/// party and looking at it, and these keep the pieces honest afterwards.
/// </summary>
public sealed class Gen3SpriteReaderTests
{
    private const int RomBase = 0x08000000;
    private const int SpriteBytes = 0x800;
    private const int PaletteBytes = 32;

    [Fact]
    public void Lz77_ReadsLiteralsAndBackReferences()
    {
        // "AB" as literals, then a back-reference three bytes long reaching back two, which
        // overlaps what it is still writing: the way the format encodes a run.
        byte[] block =
        [
            0x10, 0x05, 0x00, 0x00,
            0b0010_0000,
            (byte)'A', (byte)'B',
            0x00, 0x01,
        ];

        Assert.True(Lz77.TryDecompress(block, 0, out byte[] result));
        Assert.Equal("ABABA"u8.ToArray(), result);
    }

    [Theory]
    [InlineData(new byte[] { 0x20, 0x04, 0x00, 0x00, 0x00, 1, 2, 3, 4 })]   // wrong type byte
    [InlineData(new byte[] { 0x10, 0x00, 0x00, 0x00 })]                      // no output
    [InlineData(new byte[] { 0x10, 0x08, 0x00, 0x00, 0x00, 1, 2 })]          // truncated
    [InlineData(new byte[] { 0x10, 0x04, 0x00, 0x00, 0x80, 0x00, 0x00 })]    // reaches back past the start
    public void Lz77_RefusesWhatIsNotABlock(byte[] block)
    {
        // A wrong address looks like a valid one until it does not, so refusing has to be
        // ordinary control flow rather than an exception.
        Assert.False(Lz77.TryDecompress(block, 0, out _));
    }

    [Fact]
    public void ASpriteIsDecodedFromTilesAndPalette()
    {
        byte[] rom = BuildRom(out Gen3SpriteTables tables);

        Assert.True(Gen3SpriteReader.TryReadFrontSprite(rom, tables, 0, out DecodedSprite sprite));
        Assert.Equal(64, sprite.Width);
        Assert.Equal(64, sprite.Height);
        Assert.Equal(64 * 64 * 4, sprite.Rgba.Length);

        // Colour index 0 is the transparent one, whatever the palette says it looks like.
        Assert.Equal(0, sprite.Rgba[3]);

        // The second pixel uses index 1, which the palette below sets to pure red. Five
        // bits per channel scaled to eight, so full is 255 and not 248.
        Assert.Equal(255, sprite.Rgba[4]);
        Assert.Equal(0, sprite.Rgba[5]);
        Assert.Equal(0, sprite.Rgba[6]);
        Assert.Equal(255, sprite.Rgba[7]);
    }

    [Fact]
    public void TheTablesAreFoundFromTheShapeOfTheData()
    {
        byte[] rom = BuildRom(out Gen3SpriteTables expected);

        Assert.True(Gen3SpriteReader.TryFindTables(rom, out Gen3SpriteTables found));
        Assert.Equal(expected.FrontPicTable, found.FrontPicTable);
        Assert.Equal(expected.PaletteTable, found.PaletteTable);
        Assert.Equal(expected.EntryCount, found.EntryCount);
    }

    [Fact]
    public void ARomWithoutTheseTablesIsReportedRatherThanGuessedAt()
    {
        Assert.False(Gen3SpriteReader.TryFindTables(new byte[64 * 1024], out _));
    }

    [Fact]
    public void ASpeciesOutsideTheTableIsRefused()
    {
        byte[] rom = BuildRom(out Gen3SpriteTables tables);

        Assert.False(Gen3SpriteReader.TryReadFrontSprite(rom, tables, -1, out _));
        Assert.False(Gen3SpriteReader.TryReadFrontSprite(rom, tables, tables.EntryCount, out _));
    }

    /// <summary>
    /// A miniature ROM with the three tables a Gen 3 game has: an animated front table
    /// whose entries hold two frames, a back table holding one, and the palettes. Telling
    /// the first two apart by that difference is exactly what the reader must do.
    /// </summary>
    private static byte[] BuildRom(out Gen3SpriteTables tables)
    {
        const int entries = 320;
        const int entrySize = 8;

        byte[] frontData = Lz77Literals(new byte[SpriteBytes * 2]);
        byte[] backData = Lz77Literals(new byte[SpriteBytes]);
        byte[] paletteData = Lz77Literals(Palette());

        int frontTable = 0x1000;
        int backTable = frontTable + (entries * entrySize);
        int paletteTable = backTable + (entries * entrySize);
        int blobs = paletteTable + (entries * entrySize);

        var rom = new byte[blobs + frontData.Length + backData.Length + paletteData.Length + 16];

        int frontBlob = blobs;
        int backBlob = frontBlob + frontData.Length;
        int paletteBlob = backBlob + backData.Length;

        frontData.CopyTo(rom, frontBlob);
        backData.CopyTo(rom, backBlob);
        paletteData.CopyTo(rom, paletteBlob);

        for (int i = 0; i < entries; i++)
        {
            WriteEntry(rom, frontTable + (i * entrySize), frontBlob, SpriteBytes, i);
            WriteEntry(rom, backTable + (i * entrySize), backBlob, SpriteBytes, i);
            WriteEntry(rom, paletteTable + (i * entrySize), paletteBlob, i, 0);
        }

        tables = new Gen3SpriteTables(frontTable, paletteTable, entries);
        return rom;
    }

    private static void WriteEntry(byte[] rom, int offset, int target, int second, int third)
    {
        uint pointer = (uint)(RomBase + target);
        rom[offset] = (byte)pointer;
        rom[offset + 1] = (byte)(pointer >> 8);
        rom[offset + 2] = (byte)(pointer >> 16);
        rom[offset + 3] = (byte)(pointer >> 24);
        rom[offset + 4] = (byte)second;
        rom[offset + 5] = (byte)(second >> 8);
        rom[offset + 6] = (byte)third;
        rom[offset + 7] = (byte)(third >> 8);
    }

    /// <summary>Index 0 transparent, index 1 pure red, the rest black.</summary>
    private static byte[] Palette()
    {
        var palette = new byte[PaletteBytes];
        const int red = 31;
        palette[2] = red;
        return palette;
    }

    /// <summary>
    /// The simplest valid block: every chunk a literal. Enough to exercise the reader
    /// without needing a compressor.
    /// </summary>
    private static byte[] Lz77Literals(byte[] payload)
    {
        // The first two pixels of the first tile: index 0 then index 1, packed low nibble
        // first, so the byte is 0x10.
        if (payload.Length > 0)
        {
            payload[0] = 0x10;
        }

        var block = new List<byte>
        {
            0x10,
            (byte)payload.Length,
            (byte)(payload.Length >> 8),
            (byte)(payload.Length >> 16),
        };

        for (int i = 0; i < payload.Length; i += 8)
        {
            block.Add(0x00);
            for (int j = i; j < i + 8 && j < payload.Length; j++)
            {
                block.Add(payload[j]);
            }
        }

        return [.. block];
    }
}
