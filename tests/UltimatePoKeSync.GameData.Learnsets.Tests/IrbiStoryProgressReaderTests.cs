using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

public sealed class IrbiStoryProgressReaderTests
{
    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    [Fact]
    public async Task FollowsPartyPointerAndCountsBadgeBits()
    {
        const uint partyHead = 0x022348AC;
        var memory = new FakeMemory();
        memory.Put(IrbiStoryProgressReader.PartyPointerAddress, BitConverter.GetBytes(partyHead));
        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0b1010_1101]);
        memory.Put(partyHead + IrbiStoryProgressReader.MapIdOffset, BitConverter.GetBytes(391));

        DetectedStoryProgress? progress = await new IrbiStoryProgressReader(memory)
            .ReadAsync(Black, TestContext.Current.CancellationToken);

        Assert.NotNull(progress);
        Assert.Equal(5, progress.BadgeCount);
        Assert.Equal(391, progress.MapId);
        Assert.Equal(
            [
                IrbiStoryProgressReader.PartyPointerAddress,
                partyHead + IrbiStoryProgressReader.BadgeMaskOffset,
                partyHead + IrbiStoryProgressReader.MapIdOffset,
            ],
            memory.Reads);
    }

    [Fact]
    public async Task RefusesOtherGamesWithoutReadingMemory()
    {
        var memory = new FakeMemory();
        var reader = new IrbiStoryProgressReader(memory);

        Assert.False(reader.Supports(Black with { GameCode = "IRBE" }));
        Assert.False(reader.Supports(Black with { Revision = 1 }));
        Assert.Null(await reader.ReadAsync(
            Black with { GameCode = "IRBE" },
            TestContext.Current.CancellationToken));
        Assert.Empty(memory.Reads);

        Assert.Null(await reader.ReadAsync(
            Black with { Revision = 1 },
            TestContext.Current.CancellationToken));
        Assert.Empty(memory.Reads);
    }

    [Fact]
    public async Task RefusesAReaderThatIsNotConnected()
    {
        var memory = new FakeMemory { CanRead = false };

        Assert.Null(await new IrbiStoryProgressReader(memory).ReadAsync(
            Black,
            TestContext.Current.CancellationToken));
        Assert.Empty(memory.Reads);
    }

    [Theory]
    [InlineData(0x00000000u)]
    [InlineData(0x01FFFFFFu)]
    [InlineData(0x02400000u)]
    [InlineData(0xFFFFFFFCu)]
    public async Task RefusesInvalidPartyPointers(uint pointer)
    {
        var memory = new FakeMemory();
        memory.Put(IrbiStoryProgressReader.PartyPointerAddress, BitConverter.GetBytes(pointer));

        Assert.Null(await new IrbiStoryProgressReader(memory)
            .ReadAsync(Black, TestContext.Current.CancellationToken));
        Assert.Single(memory.Reads);
    }

    [Fact]
    public async Task BadgeMaskIsRequiredButMapIdCanBeUnavailable()
    {
        const uint partyHead = 0x022348AC;
        var memory = new FakeMemory();
        memory.Put(IrbiStoryProgressReader.PartyPointerAddress, BitConverter.GetBytes(partyHead));
        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0xFF]);

        DetectedStoryProgress? progress = await new IrbiStoryProgressReader(memory)
            .ReadAsync(Black, TestContext.Current.CancellationToken);

        Assert.NotNull(progress);
        Assert.Equal(8, progress.BadgeCount);
        Assert.Null(progress.MapId);
    }

    private sealed class FakeMemory : IEmulatorMemoryReader
    {
        private readonly Dictionary<uint, byte[]> _regions = [];

        public bool CanRead { get; init; } = true;

        public List<uint> Reads { get; } = [];

        public void Put(uint address, byte[] data) => _regions[address] = data;

        public Task<byte[]?> ReadMemoryAsync(
            uint address,
            int length,
            CancellationToken cancellationToken = default)
        {
            Reads.Add(address);
            return Task.FromResult(
                _regions.TryGetValue(address, out byte[]? data) && data.Length == length
                    ? data.ToArray()
                    : null);
        }
    }
}
