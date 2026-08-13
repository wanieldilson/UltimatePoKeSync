using Xunit;

namespace UltimatePoKeSync.Providers.MelonDs.Tests;

/// <summary>
/// The reader the rest of the app sees: ranges longer than one packet, and an emulator that
/// is not there. See D-039.
/// </summary>
public sealed class MelonDsMemoryReaderTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ARangeLongerThanOnePacketIsStitchedBackTogether()
    {
        await using var stub = new FakeGdbStub();
        await using var reader = new MelonDsMemoryReader("127.0.0.1", stub.Port);

        // 1408 bytes is a Gen 5 party: six requests, which is the real shape of the work.
        byte[]? read = await reader.ReadMemoryAsync(0x02000000, 1408, Token);

        Assert.NotNull(read);
        Assert.Equal(stub.MemoryAt(0x02000000, 1408), read);

        // Six for the party, and one before them: connecting asks the stub something small
        // to find out whether it is a wedged one that only shakes hands. See D-039.
        Assert.Equal(7, stub.Commands.Count(command => command.StartsWith('m')));
    }

    [Fact]
    public async Task NothingIsClaimedWhenTheEmulatorIsNotRunning()
    {
        await using var reader = new MelonDsMemoryReader("127.0.0.1", 1);

        Assert.Null(await reader.ReadMemoryAsync(0x02000000, 64, Token));
        Assert.False(reader.CanRead);
    }

    /// <summary>
    /// Half a read is worse than none: those bytes would parse into a Pokémon that never
    /// existed, and nothing downstream could tell.
    /// </summary>
    [Fact]
    public async Task AFailurePartWayThroughReturnsNothingRatherThanPartOfIt()
    {
        await using var stub = new FakeGdbStub();
        await using var reader = new MelonDsMemoryReader("127.0.0.1", stub.Port);

        Assert.NotNull(await reader.ReadMemoryAsync(0x02000000, 256, Token));

        stub.RefuseReads = true;
        Assert.Null(await reader.ReadMemoryAsync(0x02000000, 1408, Token));
    }

    [Fact]
    public async Task TheConnectionIsOpenedOnceAndKept()
    {
        await using var stub = new FakeGdbStub();
        await using var reader = new MelonDsMemoryReader("127.0.0.1", stub.Port);

        for (int i = 0; i < 4; i++)
        {
            await reader.ReadMemoryAsync(0x02000000, 64, Token);
        }

        // One continue, not four: reconnecting per read would halt the game each time.
        Assert.Equal(1, stub.Commands.Count(command => command == "c"));
        Assert.True(reader.CanRead);
    }
}
