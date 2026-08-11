using UltimatePoKeSync.App.Services;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Sprites;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// The sprite source against a ROM built by hand and a reader that can be made to fail on
/// demand. No game bytes and no emulator.
/// </summary>
public sealed class RomSpriteSourceTests
{
    private const uint RomBase = 0x08000000;
    private const int TableOffset = 0x2C1000;
    private const int Entries = 320;
    private const int EntrySize = 8;
    private const int SpriteBytes = 0x800;

    /// <summary>Bulbasaur: national and internal agree, so no conversion is in the way.</summary>
    private const int Species = 1;

    private static readonly GameIdentity Ruby =
        new("AXVE", "POKEMON RUBY", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity FireRed =
        new("BPRE", "POKEMON FIRE", 0, PokemonGeneration.Gen3);

    [Fact]
    public async Task ASpriteIsDecodedThroughTheReader()
    {
        var reader = new FakeReader(BuildRom());
        var source = new RomSpriteSource(reader);

        DecodedSprite? sprite = await source.TryGetAsync(Ruby, Species, TestContext.Current.CancellationToken);

        Assert.NotNull(sprite);
        Assert.Equal(64, sprite.Width);
        Assert.Equal(64, sprite.Height);
    }

    /// <summary>
    /// Issue #1. Closing the emulator for a moment used to disable sprites for the rest of
    /// the session, because an unanswered read was recorded as "this game has none".
    /// </summary>
    [Fact]
    public async Task AMomentaryFailureDoesNotDisableSpritesForGood()
    {
        var reader = new FakeReader(BuildRom()) { Offline = true };
        var source = new RomSpriteSource(reader);

        Assert.Null(await source.TryGetAsync(Ruby, Species, TestContext.Current.CancellationToken));

        reader.Offline = false;

        Assert.NotNull(await source.TryGetAsync(Ruby, Species, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Issue #2. A species whose data never arrived must not be remembered as having no
    /// sprite, or it is retired for the session over a hiccup.
    /// </summary>
    [Fact]
    public async Task AFailureWhileFetchingASpriteIsNotRemembered()
    {
        byte[] rom = BuildRom();
        var reader = new FakeReader(rom);
        var source = new RomSpriteSource(reader);

        // Let the tables be found, then cut the connection before the sprite arrives.
        Assert.NotNull(await source.TryGetAsync(Ruby, Species, TestContext.Current.CancellationToken));

        reader.Offline = true;
        Assert.Null(await source.TryGetAsync(Ruby, Species + 1, TestContext.Current.CancellationToken));

        reader.Offline = false;
        Assert.NotNull(await source.TryGetAsync(Ruby, Species + 1, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Issue #17. Swapping the cartridge under a running app left the tables and the
    /// decoded sprites of the previous game in place, so the next one was decoded with the
    /// wrong offsets — or served someone else's sprite outright.
    /// </summary>
    [Fact]
    public async Task ChangingGameThrowsAwayWhatBelongedToTheLastOne()
    {
        // Two ROMs holding the same species at different table offsets: the second paints
        // the first pixel, the first leaves it transparent.
        var reader = new SwitchableReader(BuildRom(paintFirstPixel: false), BuildRom(paintFirstPixel: true));
        var source = new RomSpriteSource(reader);

        DecodedSprite? first = await source.TryGetAsync(Ruby, Species, TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        Assert.Equal(0, first.Rgba[7]);

        reader.Swap();

        DecodedSprite? second = await source.TryGetAsync(FireRed, Species, TestContext.Current.CancellationToken);
        Assert.NotNull(second);
        Assert.Equal(255, second.Rgba[7]);
    }

    [Fact]
    public async Task ARomWithNothingRecognisableGivesNoSprite()
    {
        var reader = new FakeReader(new byte[0x400000]);
        var source = new RomSpriteSource(reader);

        Assert.Null(await source.TryGetAsync(Ruby, Species, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Issue #3. Only Emerald animates its front sprites; elsewhere every table holds one
    /// frame and the frame count decides nothing. The front table is declared first, so
    /// the lowest address is taken.
    /// </summary>
    [Fact]
    public async Task WithoutAnimationTheFirstTableIsTakenAsTheFrontOne()
    {
        byte[] rom = BuildRom(animatedFront: false);
        var source = new RomSpriteSource(new FakeReader(rom));

        DecodedSprite? sprite = await source.TryGetAsync(Ruby, Species, TestContext.Current.CancellationToken);

        Assert.NotNull(sprite);

        // The two tables differ in their first pixel: the front one paints it, the back
        // one leaves it transparent. Without that the test would pass whichever table was
        // believed, which is no test at all.
        Assert.Equal(255, sprite.Rgba[7]);
    }

    /// <summary>
    /// Two sprite tables and one palette table, laid out as a Gen 3 ROM lays them out. The
    /// first table is the front one; whether it animates is what the tests vary.
    /// </summary>
    private static byte[] BuildRom(bool animatedFront = true, bool paintFirstPixel = true)
    {
        int frontTable = TableOffset;
        int backTable = frontTable + (Entries * EntrySize);
        int paletteTable = backTable + (Entries * EntrySize);
        int blobs = paletteTable + (Entries * EntrySize);

        byte[] front = Lz77Literals(
            new byte[animatedFront ? SpriteBytes * 2 : SpriteBytes],
            paintFirstPixel ? (byte)0x10 : (byte)0x00);
        byte[] back = Lz77Literals(new byte[SpriteBytes], 0x00);
        byte[] palette = Lz77Literals(RedPalette(), null);

        var rom = new byte[0x400000];
        int frontBlob = blobs;
        int backBlob = frontBlob + front.Length;
        int paletteBlob = backBlob + back.Length;

        front.CopyTo(rom, frontBlob);
        back.CopyTo(rom, backBlob);
        palette.CopyTo(rom, paletteBlob);

        for (int i = 0; i < Entries; i++)
        {
            WriteEntry(rom, frontTable + (i * EntrySize), frontBlob, SpriteBytes, i);
            WriteEntry(rom, backTable + (i * EntrySize), backBlob, SpriteBytes, i);
            WriteEntry(rom, paletteTable + (i * EntrySize), paletteBlob, i, 0);
        }

        return rom;
    }

    private static void WriteEntry(byte[] rom, int offset, int target, int second, int third)
    {
        uint pointer = RomBase + (uint)target;
        rom[offset] = (byte)pointer;
        rom[offset + 1] = (byte)(pointer >> 8);
        rom[offset + 2] = (byte)(pointer >> 16);
        rom[offset + 3] = (byte)(pointer >> 24);
        rom[offset + 4] = (byte)second;
        rom[offset + 5] = (byte)(second >> 8);
        rom[offset + 6] = (byte)third;
        rom[offset + 7] = (byte)(third >> 8);
    }

    private static byte[] RedPalette()
    {
        var palette = new byte[32];
        palette[2] = 31;
        return palette;
    }

    /// <param name="marker">
    /// The first byte of the payload, which for tile data is the first two pixels: 0x10 is
    /// index 0 then index 1, so the second pixel is painted. Null leaves the payload alone,
    /// for a palette.
    /// </param>
    private static byte[] Lz77Literals(byte[] payload, byte? marker)
    {
        if (marker is byte value && payload.Length > 0)
        {
            payload[0] = value;
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

    /// <summary>An emulator whose cartridge can be changed underneath.</summary>
    private sealed class SwitchableReader(byte[] first, byte[] second) : IEmulatorMemoryReader
    {
        private byte[] _rom = first;

        public bool CanRead => true;

        public void Swap() => _rom = second;

        public Task<byte[]?> ReadMemoryAsync(
            uint address,
            int length,
            CancellationToken cancellationToken = default)
        {
            var offset = (int)(address - RomBase);
            var slice = new byte[length];
            for (int i = 0; i < length; i++)
            {
                int source = offset + i;
                slice[i] = source >= 0 && source < _rom.Length ? _rom[source] : (byte)0;
            }

            return Task.FromResult<byte[]?>(slice);
        }
    }

    /// <summary>An emulator that can be taken away and given back.</summary>
    private sealed class FakeReader(byte[] rom) : IEmulatorMemoryReader
    {
        public bool Offline { get; set; }

        public bool CanRead => true;

        public Task<byte[]?> ReadMemoryAsync(
            uint address,
            int length,
            CancellationToken cancellationToken = default)
        {
            if (Offline)
            {
                return Task.FromResult<byte[]?>(null);
            }

            var offset = (int)(address - RomBase);
            var slice = new byte[length];

            for (int i = 0; i < length; i++)
            {
                int source = offset + i;
                slice[i] = source >= 0 && source < rom.Length ? rom[source] : (byte)0;
            }

            return Task.FromResult<byte[]?>(slice);
        }
    }
}
