using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Session.Tests;

public sealed class PartyTrackerTests
{
    [Fact]
    public async Task EmitsTheFirstParty()
    {
        var parser = new ScriptedParser(raw => Build.Party(raw.Sequence, [Build.Mon()]));
        var tracker = new PartyTracker(new FakeProvider(Build.Raw(1)), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.Single(emitted);
        Assert.Equal(1UL, tracker.Diagnostics.Emitted);
    }

    [Fact]
    public async Task SuppressesChangesThatAreOnlyBattleState()
    {
        // The whole point of the tracker. During a fight the party bytes change on nearly
        // every turn because PP drops, and the script duly reports each one. Recomputing
        // the analysis for that would be pure waste: no EV, nature, move or item advice
        // depends on how much PP is left.
        int pp = 15;
        var parser = new ScriptedParser(raw =>
            Build.Party(raw.Sequence, [Build.Mon(currentPp: pp--)]));

        var tracker = new PartyTracker(
            new FakeProvider(Build.Raw(1), Build.Raw(2), Build.Raw(3), Build.Raw(4)), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.Single(emitted);
        Assert.Equal(3UL, tracker.Diagnostics.VolatileSuppressed);
    }

    [Theory]
    [MemberData(nameof(MeaningfulChanges))]
    public async Task EmitsWhenSomethingWorthReanalysingChanges(
        string description, PokemonSnapshot changed)
    {
        Assert.NotEmpty(description);

        bool first = true;
        var parser = new ScriptedParser(raw =>
        {
            PokemonSnapshot mon = first ? Build.Mon() : changed;
            first = false;
            return Build.Party(raw.Sequence, [mon]);
        });

        var tracker = new PartyTracker(new FakeProvider(Build.Raw(1), Build.Raw(2)), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.Equal(2, emitted.Count);
    }

    public static TheoryData<string, PokemonSnapshot> MeaningfulChanges() => new()
    {
        { "levelled up", Build.Mon(level: 51) },
        { "evolved", Build.Mon(species: 129) },
        { "gained a held item", Build.Mon(heldItem: 234) },
        { "moved slot", Build.Mon(slot: 1) },
    };

    [Fact]
    public async Task DiscardsOutOfOrderSnapshots()
    {
        var parser = new ScriptedParser(raw =>
            Build.Party(raw.Sequence, [Build.Mon(level: (int)raw.Sequence + 40)]));

        var tracker = new PartyTracker(
            new FakeProvider(Build.Raw(5), Build.Raw(3), Build.Raw(6)), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.Equal([5UL, 6UL], emitted.Select(p => p.Sequence));
        Assert.Equal(1UL, tracker.Diagnostics.OutOfOrderDiscarded);
    }

    [Fact]
    public async Task TreatsASequenceRestartAsAReload()
    {
        // Reloading the script or resetting the emulator restarts the counter at 1. That
        // must not be mistaken for a stale message, or the tracker would go deaf until the
        // sequence climbed back past the old high-water mark.
        var parser = new ScriptedParser(raw =>
            Build.Party(raw.Sequence, [Build.Mon(level: (int)raw.Sequence + 40)]));

        var tracker = new PartyTracker(
            new FakeProvider(Build.Raw(9), Build.Raw(1)), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.Equal([9UL, 1UL], emitted.Select(p => p.Sequence));
        Assert.Equal(0UL, tracker.Diagnostics.OutOfOrderDiscarded);
    }

    [Fact]
    public async Task HoldsTheLastGoodPartyWhenASlotIsRejected()
    {
        // A torn read must not make the team flicker down to five members and back.
        var parser = new ScriptedParser(raw => raw.Sequence == 2
            ? Build.Party(raw.Sequence, [], [new RejectedSlot(0, "invalid checksum")])
            : Build.Party(raw.Sequence, [Build.Mon(level: (int)raw.Sequence + 40)]));

        var tracker = new PartyTracker(
            new FakeProvider(Build.Raw(1), Build.Raw(2), Build.Raw(3)), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.Equal([1UL, 3UL], emitted.Select(p => p.Sequence));
        Assert.Equal(1UL, tracker.Diagnostics.InconsistentDiscarded);
        Assert.Equal(41, Assert.Single(emitted[0].Members).Level);
    }

    [Fact]
    public async Task StopsHoldingOutIfTheProblemIsPersistent()
    {
        // Skipping inconsistent snapshots is right for a torn read, but a genuinely broken
        // party (a bad egg, say) is permanent. Held literally, the rule would stall the
        // display forever, so after a few attempts the tracker reports what it sees.
        var parser = new ScriptedParser(raw =>
            Build.Party(raw.Sequence, [Build.Mon()], [new RejectedSlot(1, "bad egg")]));

        RawPartySnapshot[] snapshots = [.. Enumerable.Range(1, 8).Select(i => Build.Raw((ulong)i))];
        var tracker = new PartyTracker(new FakeProvider(snapshots), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.NotEmpty(emitted);
        Assert.Equal(6UL, emitted[0].Sequence);
    }

    [Fact]
    public async Task IgnoresSnapshotsFromAnUnsupportedGame()
    {
        var parser = new ScriptedParser(raw => Build.Party(raw.Sequence)) { Supported = false };
        var tracker = new PartyTracker(new FakeProvider(Build.Raw(1), Build.Raw(2)), parser);

        List<PartySnapshot> emitted = await Collect(tracker);

        Assert.Empty(emitted);
        Assert.Equal(2UL, tracker.Diagnostics.UnparsableDiscarded);
        Assert.True(tracker.Current.IsEmpty);
    }

    [Fact]
    public async Task ExposesTheLastEmittedPartyAsCurrent()
    {
        var parser = new ScriptedParser(raw =>
            Build.Party(raw.Sequence, [Build.Mon(level: (int)raw.Sequence + 40)]));

        var tracker = new PartyTracker(new FakeProvider(Build.Raw(1), Build.Raw(2)), parser);

        await Collect(tracker);

        Assert.Equal(42, Assert.Single(tracker.Current.Members).Level);
    }

    private static async Task<List<PartySnapshot>> Collect(PartyTracker tracker)
    {
        var emitted = new List<PartySnapshot>();
        await foreach (PartySnapshot party in tracker.TrackAsync(TestContext.Current.CancellationToken))
        {
            emitted.Add(party);
        }

        return emitted;
    }
}
