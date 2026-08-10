using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.Parsing;
using Xunit;

namespace UltimatePoKeSync.Providers.MGba.Tests;

public sealed class MGbaProviderTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ReceivesAValidSnapshot()
    {
        await using var bridge = FakeBridge.Start();
        using var cancellation = new CancellationTokenSource(Timeout);

        await using var provider = new MGbaProvider(OptionsFor(bridge));
        IAsyncEnumerator<RawPartySnapshot> stream =
            provider.ReadSnapshotsAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        // ReadSnapshotsAsync is a lazy iterator: it does not connect until the first
        // element is requested. It has to be kicked off before waiting for the connection.
        ValueTask<bool> next = stream.MoveNextAsync();
        await bridge.FirstClientConnected.WaitAsync(cancellation.Token);
        await bridge.SendLineAsync(PartyLine(new byte[600]));

        Assert.True(await next);
        RawPartySnapshot snapshot = stream.Current;

        Assert.Equal("BPEE", snapshot.Game.GameCode);
        Assert.Equal(PokemonGeneration.Gen3, snapshot.Game.Generation);
        Assert.Equal(100, snapshot.SlotSize);
        Assert.Equal(6, snapshot.SlotCapacity);
        Assert.Equal(600, snapshot.PartyData.Length);
    }

    [Fact]
    public async Task RawBytesOnTheWireBecomeADecodedPokemon()
    {
        // The whole path: encrypted as in RAM -> base64 -> TCP -> JSON -> PK3.
        var gyarados = new PK3
        {
            Species = 130,
            PID = 0x1A2B3C4D,
            TID16 = 1234,
            SID16 = 5678,
            Stat_Level = 55,
            CurrentLevel = 55,
            IV_ATK = 31,
            EV_ATK = 252,
            Move1 = 63,
            Move1_PP = 5,
            Nickname = "GYARA",
            OriginalTrainerName = "RED",
        };
        gyarados.RefreshChecksum();

        byte[] encrypted = gyarados.Data.ToArray();
        PokeCrypto.Encrypt3(encrypted);

        var blob = new byte[600];
        encrypted.CopyTo(blob.AsSpan(0, 100));

        await using var bridge = FakeBridge.Start();
        using var cancellation = new CancellationTokenSource(Timeout);

        await using var provider = new MGbaProvider(OptionsFor(bridge));
        IAsyncEnumerator<RawPartySnapshot> stream =
            provider.ReadSnapshotsAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        ValueTask<bool> next = stream.MoveNextAsync();
        await bridge.FirstClientConnected.WaitAsync(cancellation.Token);
        await bridge.SendLineAsync(PartyLine(blob, count: 1));

        Assert.True(await next);

        IPartyParser? parser = PartyParserResolver.CreateDefault().Resolve(stream.Current.Game);
        Assert.NotNull(parser);

        PokemonSnapshot mon = Assert.Single(parser.Parse(stream.Current).Members);
        Assert.Equal("GYARADOS", mon.SpeciesName);
        Assert.Equal(55, mon.Level);
        Assert.Equal(PokemonType.Water, mon.PrimaryType);
    }

    [Fact]
    public async Task SkipsUnreadableMessagesAndKeepsTheReadableOne()
    {
        await using var bridge = FakeBridge.Start();
        using var cancellation = new CancellationTokenSource(Timeout);

        var provider = new MGbaProvider(OptionsFor(bridge));
        await using var _ = provider;
        IAsyncEnumerator<RawPartySnapshot> stream =
            provider.ReadSnapshotsAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        ValueTask<bool> next = stream.MoveNextAsync();
        await bridge.FirstClientConnected.WaitAsync(cancellation.Token);

        await bridge.SendLineAsync("this is not JSON");
        await bridge.SendLineAsync("""{"v":99,"type":"party"}""");         // future version
        await bridge.SendLineAsync("""{"v":1,"type":"hello"}""");           // unknown type
        await bridge.SendLineAsync(PartyLine(new byte[600], data: "!!!"));  // broken base64
        await bridge.SendLineAsync(PartyLine(new byte[100]));               // inconsistent length
        await bridge.SendLineAsync(PartyLine(new byte[600], sequence: 7));  // good

        Assert.True(await next);

        Assert.Equal(7UL, stream.Current.Sequence);
        Assert.Equal(5, provider.MalformedMessageCount);
    }

    [Fact]
    public async Task ReconnectsWhenTheEmulatorGoesAway()
    {
        await using var bridge = FakeBridge.Start();
        using var cancellation = new CancellationTokenSource(Timeout);

        await using var provider = new MGbaProvider(OptionsFor(bridge));
        IAsyncEnumerator<RawPartySnapshot> stream =
            provider.ReadSnapshotsAsync(cancellation.Token).GetAsyncEnumerator(cancellation.Token);

        ValueTask<bool> first = stream.MoveNextAsync();
        await bridge.FirstClientConnected.WaitAsync(cancellation.Token);
        await bridge.SendLineAsync(PartyLine(new byte[600], sequence: 1));
        Assert.True(await first);

        // mGBA vanishes: the provider must reconnect on its own, without anyone restarting
        // the stream.
        bridge.DropAllClients();

        RawPartySnapshot? received = null;
        var pump = Task.Run(async () =>
        {
            if (await stream.MoveNextAsync())
            {
                received = stream.Current;
            }
        }, cancellation.Token);

        // Keep resending until it picks up: backoff makes the exact moment non-deterministic.
        while (!pump.IsCompleted && !cancellation.IsCancellationRequested)
        {
            await bridge.SendLineAsync(PartyLine(new byte[600], sequence: 2));
            await Task.Delay(100, cancellation.Token);
        }

        await pump;
        Assert.NotNull(received);
        Assert.Equal(2UL, received.Sequence);
    }

    [Fact]
    public async Task DoesNotFailWhenTheEmulatorIsNotThereYet()
    {
        // Entirely normal case: the app starts before mGBA. It must wait, not die.
        var options = new MGbaProviderOptions
        {
            Port = 1, // nobody listening
            ConnectTimeout = TimeSpan.FromMilliseconds(100),
            InitialReconnectDelay = TimeSpan.FromMilliseconds(20),
            MaxReconnectDelay = TimeSpan.FromMilliseconds(50),
        };

        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var provider = new MGbaProvider(options);

        var states = new List<EmulatorConnectionState>();
        provider.StateChanged += (_, state) => states.Add(state);

        await foreach (RawPartySnapshot _ in provider.ReadSnapshotsAsync(cancellation.Token))
        {
            Assert.Fail("no snapshot can arrive from a closed port");
        }

        Assert.Contains(EmulatorConnectionState.Connecting, states);
        Assert.Contains(EmulatorConnectionState.Reconnecting, states);
    }

    private static MGbaProviderOptions OptionsFor(FakeBridge bridge) => new()
    {
        Port = bridge.Port,
        ConnectTimeout = TimeSpan.FromSeconds(2),
        InitialReconnectDelay = TimeSpan.FromMilliseconds(20),
        MaxReconnectDelay = TimeSpan.FromMilliseconds(100),
    };

    private static string PartyLine(
        byte[] blob,
        int count = 0,
        ulong sequence = 1,
        string? data = null)
    {
        string encoded = data ?? Convert.ToBase64String(blob);
        return $$"""
            {"v":1,"type":"party","seq":{{sequence}},"frame":1234,"game":{"code":"BPEE","title":"POKEMON EMER","rev":0,"gen":3},"count":{{count}},"slotSize":100,"slots":6,"data":"{{encoded}}"}
            """;
    }
}
