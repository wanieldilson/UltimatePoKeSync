using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

/// <summary>
/// Recommendation regressions exposed by the real early-game Snivy, expressed with small
/// snapshots so failures point at policy rather than at RAM parsing.
/// </summary>
public sealed class Gen5RecommendationTests
{
    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    private static readonly GameIdentity Emerald =
        new("BPEI", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private readonly PokemonRecommendationEngine _engine =
        PokemonRecommendationEngine.CreateDefault(PKHeXSources.All);

    [Fact]
    public void Playthrough_SnivyAtSix_ReplacesTheEarlyFillersWithVineWhip()
    {
        PokemonRecommendation result = Recommend(
            Black,
            Snivy(level: 6),
            RecommendationProfileKind.Playthrough);

        Assert.Contains(
            result.Build.Moves,
            move => move.Move.ReferenceId == "vinewhip");
        Assert.DoesNotContain(
            result.Build.Moves,
            move => move.Move.ReferenceId is "tackle" or "leer" or "wrap");
    }

    [Fact]
    public void Competitive_Snivy_TargetsSerperiorWithoutCollapsingIntoGrass()
    {
        PokemonRecommendation result = Recommend(
            Black,
            Snivy(level: 6),
            RecommendationProfileKind.Competitive);

        Assert.True(result.RoleAnalysis.IsJudgedAsEvolution);
        Assert.Equal(497, result.RoleAnalysis.JudgedSpeciesId);
        Assert.Equal("Serperior", result.RoleAnalysis.JudgedSpeciesName);

        // Random Battles has no Snivy entry. A match here therefore proves that the
        // competitive recommendation used Serperior's reference sets as its prior.
        Assert.NotNull(result.MatchedPreset);

        Assert.DoesNotContain(
            result.Build.Moves,
            move => move.Move.ReferenceId is "tackle" or "leer");

        int grassAttacks = result.Build.Moves.Count(move =>
            move.Move.Type == PokemonType.Grass &&
            Gen5Rules.Instance.GetMoveCategory(move.Move.MoveId, move.Move.Type) != MoveCategory.Status);

        Assert.InRange(grassAttacks, 0, 2);
        Assert.InRange(result.MoveCandidates.Count, 1, 16);
    }

    [Fact]
    public void Playthrough_LevelOneHundred_DoesNotAskForLevelOneHundredAndFour()
    {
        PokemonSnapshot serperior = Member(
            speciesId: 497,
            speciesName: "Serperior",
            level: 100,
            primaryType: PokemonType.Grass,
            baseStats: new StatBlock(75, 75, 95, 75, 95, 113));

        PokemonRecommendation result = Recommend(
            Black,
            serperior,
            RecommendationProfileKind.Playthrough);

        Assert.NotEmpty(result.Build.Moves);
    }

    [Fact]
    public void Playthrough_IncludesAMoveLearnedAtTheEvolutionLevel()
    {
        PokemonSnapshot treecko = Member(
            speciesId: 252,
            speciesName: "Treecko",
            level: 15,
            primaryType: PokemonType.Grass,
            baseStats: new StatBlock(40, 45, 35, 65, 55, 70),
            moves:
            [
                Move(71, "Absorb", PokemonType.Grass),
                Move(98, "Quick Attack", PokemonType.Normal),
            ]);

        PokemonRecommendation result = Recommend(
            Emerald,
            treecko,
            RecommendationProfileKind.Playthrough);

        Assert.Contains(
            result.MoveCandidates,
            move => move.Move.ReferenceId == "pursuit" &&
                move.LearnedAtLevel == 16 &&
                move.Availability == RecommendationAvailability.ArrivesWithLevelUp);
    }

    private PokemonRecommendation Recommend(
        GameIdentity game,
        PokemonSnapshot member,
        RecommendationProfileKind profile) =>
        _engine
            .Recommend(
                new PartySnapshot(game, [member], DateTimeOffset.UnixEpoch, 1, []),
                profile)
            .Members
            .Single();

    private static PokemonSnapshot Snivy(int level) => Member(
        speciesId: 495,
        speciesName: "Snivy",
        level: level,
        primaryType: PokemonType.Grass,
        baseStats: new StatBlock(45, 45, 55, 45, 55, 63),
        moves:
        [
            Move(33, "Tackle", PokemonType.Normal),
            Move(43, "Leer", PokemonType.Normal),
        ]);

    private static PokemonSnapshot Member(
        int speciesId,
        string speciesName,
        int level,
        PokemonType primaryType,
        StatBlock baseStats,
        params MoveSlot[] moves) => new()
        {
            SlotIndex = 0,
            SpeciesId = (ushort)speciesId,
            SpeciesName = speciesName,
            Nickname = speciesName,
            Level = level,
            PrimaryType = primaryType,
            SecondaryType = PokemonType.None,
            BaseStats = baseStats,
            IndividualValues = new StatBlock(31, 31, 31, 31, 31, 31),
            EffortValues = new StatBlock(0, 0, 0, 0, 0, 0),
            CurrentStats = new StatBlock(100, 100, 100, 100, 100, 100),
            NatureId = 0,
            NatureName = "Hardy",
            AbilityId = 65,
            AbilityName = "Overgrow",
            HeldItemId = 0,
            HeldItemName = "-",
            Moves = moves,
            IsEgg = false,
            IsShiny = false,
            PersonalityValue = 1,
            CurrentHp = 100,
            Status = StatusCondition.None,
            Friendship = 70,
            Experience = 0,
        };

    private static MoveSlot Move(int id, string name, PokemonType type) =>
        new(id, name, type, 10, 10);
}
