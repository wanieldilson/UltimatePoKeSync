using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Session.Tests;

/// <summary>Replays a fixed list of snapshots, standing in for a real emulator.</summary>
internal sealed class FakeProvider(params RawPartySnapshot[] snapshots) : IEmulatorProvider
{
    public string Name => "fake";

    public EmulatorConnectionState State => EmulatorConnectionState.Streaming;

    public event EventHandler<EmulatorConnectionState>? StateChanged
    {
        add { }
        remove { }
    }

    public async IAsyncEnumerable<RawPartySnapshot> ReadSnapshotsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (RawPartySnapshot snapshot in snapshots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return snapshot;
        }

        await Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Returns whatever the test tells it to, so tracker behaviour can be exercised without
/// any real byte decoding.
/// </summary>
internal sealed class ScriptedParser(Func<RawPartySnapshot, PartySnapshot> parse)
    : IPartyParser, IPartyParserResolver
{
    public bool Supported { get; set; } = true;

    public bool CanParse(GameIdentity game) => Supported;

    public PartySnapshot Parse(RawPartySnapshot raw) => parse(raw);

    public IPartyParser? Resolve(GameIdentity game) => Supported ? this : null;
}

internal static class Build
{
    public static GameIdentity Game { get; } =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    public static RawPartySnapshot Raw(ulong sequence) =>
        new(Game, 1, new byte[600], 100, DateTimeOffset.UtcNow, sequence);

    public static PartySnapshot Party(
        ulong sequence,
        IReadOnlyList<PokemonSnapshot>? members = null,
        IReadOnlyList<RejectedSlot>? rejected = null) =>
        new(Game, members ?? [], DateTimeOffset.UtcNow, sequence, rejected ?? []);

    public static PokemonSnapshot Mon(
        int slot = 0,
        ushort species = 130,
        int level = 50,
        int currentPp = 15,
        int heldItem = 0) => new()
    {
        SlotIndex = slot,
        SpeciesId = species,
        SpeciesName = "GYARADOS",
        Nickname = "GYARADOS",
        Level = level,
        PrimaryType = PokemonType.Water,
        SecondaryType = PokemonType.Flying,
        BaseStats = new StatBlock(95, 125, 79, 60, 100, 81),
        IndividualValues = new StatBlock(31, 31, 20, 5, 22, 30),
        EffortValues = new StatBlock(4, 252, 0, 0, 0, 252),
        CurrentStats = new StatBlock(180, 190, 100, 80, 120, 110),
        CurrentHp = 180,
        Status = StatusCondition.None,
        Friendship = 70,
        Experience = 125000,
        NatureId = 3,
        NatureName = "Adamant",
        AbilityId = 22,
        AbilityName = "Intimidate",
        HeldItemId = heldItem,
        HeldItemName = heldItem == 0 ? "-" : "Leftovers",
        Moves = [new MoveSlot(63, "Hyper Beam", PokemonType.Normal, currentPp, 15)],
        IsEgg = false,
        IsShiny = false,
        PersonalityValue = 0x1A2B3C4D,
    };
}
