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
        string? speciesName = null,
        int level = 50,
        StatBlock? baseStats = null,
        int? speciesId = null,
        bool isEgg = false,
        params MoveSlot[] moves) => new()
        {
            SlotIndex = slot,
            // Anything that reaches a learnset needs the real national dex number.
            SpeciesId = (ushort)(speciesId ?? slot + 1),
            SpeciesName = speciesName ?? $"Member {slot}",
            Level = level,
            PrimaryType = primaryType,
            SecondaryType = secondaryType,
            BaseStats = baseStats ?? new StatBlock(50, 50, 50, 50, 50, 50),
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
            IsEgg = isEgg,
            IsShiny = false,
            PersonalityValue = (uint)slot,
            CurrentHp = 100,
            Status = StatusCondition.None,
            Friendship = 70,
            Experience = 0,
        };

    public static MoveSlot Move(int id, string name, PokemonType type) =>
        new(id, name, type, 10, 10);
}
