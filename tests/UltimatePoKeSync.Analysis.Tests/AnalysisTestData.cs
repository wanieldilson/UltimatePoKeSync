using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Analysis.Tests;

internal static class AnalysisTestData
{
    public static GameIdentity Emerald { get; } =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    public static PartySnapshot Party(params PokemonSnapshot[] members) =>
        new(Emerald, members, DateTimeOffset.UnixEpoch, 1, []);

    public static PokemonSnapshot Member(
        int slot = 0,
        PokemonType primaryType = PokemonType.Normal,
        PokemonType secondaryType = PokemonType.None,
        int abilityId = 0,
        params MoveSlot[] moves) => new()
    {
        SlotIndex = slot,
        SpeciesId = (ushort)(slot + 1),
        SpeciesName = $"Member {slot}",
        Level = 50,
        PrimaryType = primaryType,
        SecondaryType = secondaryType,
        BaseStats = new StatBlock(50, 50, 50, 50, 50, 50),
        IndividualValues = new StatBlock(31, 31, 31, 31, 31, 31),
        EffortValues = new StatBlock(0, 0, 0, 0, 0, 0),
        CurrentStats = new StatBlock(100, 100, 100, 100, 100, 100),
        NatureId = 0,
        NatureName = "Hardy",
        AbilityId = abilityId,
        AbilityName = "-",
        HeldItemId = 0,
        HeldItemName = "-",
        Moves = moves,
        IsEgg = false,
        IsShiny = false,
        PersonalityValue = (uint)slot,
    };

    public static MoveSlot Move(int id, string name, PokemonType type) =>
        new(id, name, type, 10, 10);
}
