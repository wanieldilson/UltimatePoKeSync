using System.Text.Json;
using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Providers.MGba.Tests;

/// <summary>
/// The request half of protocol 2, without mGBA and without a ROM. See D-033.
/// </summary>
public sealed class MemoryReadTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AReadIsSentAsALineAndItsReplyComesBackAsBytes()
    {
        await using var bridge = FakeBridge.Start();
        var provider = new MGbaProvider(Options(bridge.Port));

        using var cancellation = new CancellationTokenSource(Patience);
        IAsyncEnumerator<RawPartySnapshot> stream =
            provider.ReadSnapshotsAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        _ = stream.MoveNextAsync();

        await bridge.FirstClientConnected.WaitAsync(Patience, cancellation.Token);
        await WaitUntilReadableAsync(provider, cancellation.Token);

        byte[] expected = [1, 2, 3, 4, 5];
        Task<byte[]?> request = provider.ReadMemoryAsync(0x08000000, expected.Length, cancellation.Token);

        string command = await bridge.ReadCommandAsync(cancellation.Token);
        using JsonDocument parsed = JsonDocument.Parse(command);

        Assert.Equal("read", parsed.RootElement.GetProperty("type").GetString());
        Assert.Equal(0x08000000u, parsed.RootElement.GetProperty("address").GetUInt32());
        Assert.Equal(expected.Length, parsed.RootElement.GetProperty("length").GetInt32());

        int id = parsed.RootElement.GetProperty("id").GetInt32();
        await bridge.SendLineAsync(
            $"{{\"v\":2,\"type\":\"memory\",\"id\":{id},\"address\":134217728,"
            + $"\"length\":{expected.Length},\"data\":\"{Convert.ToBase64String(expected)}\"}}");

        Assert.Equal(expected, await request.WaitAsync(Patience, cancellation.Token));
    }

    [Fact]
    public async Task AnErrorReplyEndsTheWaitInsteadOfLettingItTimeOut()
    {
        await using var bridge = FakeBridge.Start();
        var provider = new MGbaProvider(Options(bridge.Port));

        using var cancellation = new CancellationTokenSource(Patience);
        IAsyncEnumerator<RawPartySnapshot> stream =
            provider.ReadSnapshotsAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
        _ = stream.MoveNextAsync();

        await bridge.FirstClientConnected.WaitAsync(Patience, cancellation.Token);
        await WaitUntilReadableAsync(provider, cancellation.Token);

        Task<byte[]?> request = provider.ReadMemoryAsync(0x08000000, 16, cancellation.Token);
        string command = await bridge.ReadCommandAsync(cancellation.Token);
        int id = JsonDocument.Parse(command).RootElement.GetProperty("id").GetInt32();

        await bridge.SendLineAsync(
            $"{{\"v\":2,\"type\":\"error\",\"id\":{id},\"message\":\"unreadable range\"}}");

        Assert.Null(await request.WaitAsync(Patience, cancellation.Token));
    }

    [Fact]
    public async Task WithNothingConnectedAReadReturnsNothingRatherThanThrowing()
    {
        var provider = new MGbaProvider(Options(1));

        Assert.False(provider.CanRead);
        Assert.Null(await provider.ReadMemoryAsync(
            0x08000000, 16, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AReadLongerThanTheScriptAllowsIsRefusedBeforeItIsSent()
    {
        var provider = new MGbaProvider(Options(1));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => provider.ReadMemoryAsync(
                0x08000000, (256 * 1024) + 1, TestContext.Current.CancellationToken));
    }

    private static MGbaProviderOptions Options(int port) => new()
    {
        Port = port,
        ConnectTimeout = TimeSpan.FromSeconds(2),
        InitialReconnectDelay = TimeSpan.FromMilliseconds(20),
        MaxReconnectDelay = TimeSpan.FromMilliseconds(50),
    };

    private static async Task WaitUntilReadableAsync(
        MGbaProvider provider,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < 100 && !provider.CanRead; attempt++)
        {
            await Task.Delay(20, cancellationToken);
        }

        Assert.True(provider.CanRead);
    }
}
