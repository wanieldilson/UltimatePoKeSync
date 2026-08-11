using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class PokemonRecommendationEngineTests
{
    private readonly PokemonRecommendationEngine _engine =
        PokemonRecommendationEngine.CreateDefault(PKHeXGen3LevelUpLearnsets.Instance);

    [Fact]
    public void CompetitiveProfile_CombinesLiveRoleWithMatchedPreset()
    {
        PokemonSnapshot gyarados = AnalysisTestData.Member(
            primaryType: PokemonType.Water,
            secondaryType: PokemonType.Flying,
            speciesName: "Gyarados",
            baseStats: new StatBlock(95, 125, 79, 60, 100, 81),
            moves:
            [
                AnalysisTestData.Move(89, "Earthquake", PokemonType.Ground),
                AnalysisTestData.Move(57, "Surf", PokemonType.Water),
                AnalysisTestData.Move(44, "Bite", PokemonType.Dark),
            ]);

        PokemonRecommendation result = _engine
            .Recommend(
                AnalysisTestData.Party(gyarados),
                RecommendationProfileKind.Competitive)
            .Members
            .Single();

        Assert.Equal(PokemonRole.PhysicalAttacker, result.RoleAnalysis.Role);
        Assert.Equal("Wallbreaker", result.MatchedPreset?.Role);
        Assert.Equal("Adamant", result.Nature.PreferredNatures[0].Name);
        Assert.Equal(new StatBlock(4, 252, 0, 0, 0, 252), result.EffortValues.TargetSpread);
        Assert.Equal(
            new StatBlock(171, 194, 99, 72, 120, 133),
            result.EffortValues.ProjectedStats);
        Assert.Contains(result.ItemCandidates, item => item.Name == "Choice Band");
        Assert.Contains(
            result.MoveCandidates,
            move => move.Move.ReferenceId == "hiddenpowerflying" &&
                move.Move.Type == PokemonType.Flying);
        Assert.All(
            result.MoveCandidates,
            move => Assert.Equal(
                RecommendationAvailability.CompetitiveReference,
                move.Availability));
    }

    [Fact]
    public void PlaythroughProfile_OnlyAddsLevelUpMovesAtOrBelowCurrentLevel()
    {
        PokemonSnapshot treecko = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Treecko",
            level: 11,
            baseStats: new StatBlock(40, 45, 35, 65, 55, 70),
            speciesId: 252,
            moves:
            [
                AnalysisTestData.Move(71, "Absorb", PokemonType.Grass),
                AnalysisTestData.Move(98, "Quick Attack", PokemonType.Normal),
            ]);

        PokemonRecommendation result = _engine
            .Recommend(
                AnalysisTestData.Party(treecko),
                RecommendationProfileKind.Playthrough)
            .Members
            .Single();

        Assert.False(result.EffortValues.IsExactTarget);
        Assert.Equal([Stat.SpecialAttack, Stat.Speed], result.EffortValues.PriorityStats);
        Assert.Contains(
            result.MoveCandidates,
            move => move.Move.ReferenceId == "absorb" &&
                move.Availability == RecommendationAvailability.KnownAvailable);
        Assert.Contains(result.MoveCandidates, move => move.Move.ReferenceId == "pound");
        Assert.DoesNotContain(result.MoveCandidates, move => move.Move.ReferenceId == "megadrain");
        Assert.Contains(
            result.ItemCandidates,
            item => item.Name == "Miracle Seed" &&
                item.Availability == RecommendationAvailability.RequiresAvailabilityCheck);
    }

    [Fact]
    public void CompetitiveProfile_MixedSpreadRespectsGen3EvCap()
    {
        PokemonSnapshot nidoking = AnalysisTestData.Member(
            primaryType: PokemonType.Poison,
            secondaryType: PokemonType.Ground,
            speciesName: "Nidoking",
            baseStats: new StatBlock(81, 92, 77, 85, 75, 85),
            moves:
            [
                AnalysisTestData.Move(89, "Earthquake", PokemonType.Ground),
                AnalysisTestData.Move(58, "Ice Beam", PokemonType.Ice),
            ]);

        PokemonRecommendation result = _engine
            .Recommend(
                AnalysisTestData.Party(nidoking),
                RecommendationProfileKind.Competitive)
            .Members
            .Single();

        Assert.Equal(PokemonRole.MixedAttacker, result.RoleAnalysis.Role);
        Assert.Equal(508, result.EffortValues.TargetSpread?.Total);
        Assert.NotNull(result.EffortValues.ProjectedStats);
    }

    [Fact]
    public void CompetitiveProfile_FallsBackToCurrentMovesWithoutPreset()
    {
        PokemonSnapshot treecko = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Treecko",
            level: 11,
            baseStats: new StatBlock(40, 45, 35, 65, 55, 70),
            moves: [AnalysisTestData.Move(71, "Absorb", PokemonType.Grass)]);

        PokemonRecommendation result = _engine
            .Recommend(
                AnalysisTestData.Party(treecko),
                RecommendationProfileKind.Competitive)
            .Members
            .Single();

        Assert.Null(result.MatchedPreset);
        MoveRecommendation move = Assert.Single(result.MoveCandidates);
        Assert.Equal("absorb", move.Move.ReferenceId);
        Assert.Equal(MoveCandidateSource.CurrentMoveset, move.Source);
        Assert.Equal(RecommendationAvailability.KnownAvailable, move.Availability);
    }

    [Fact]
    public void Build_PicksFourMovesWithAReasonEachAndKeepsTheRest()
    {
        PokemonSnapshot gyarados = AnalysisTestData.Member(
            primaryType: PokemonType.Water,
            secondaryType: PokemonType.Flying,
            speciesName: "Gyarados",
            baseStats: new StatBlock(95, 125, 79, 60, 100, 81),
            speciesId: 130,
            moves: [AnalysisTestData.Move(89, "Earthquake", PokemonType.Ground)]);

        PokemonRecommendation result = _engine
            .Recommend(AnalysisTestData.Party(gyarados), RecommendationProfileKind.Competitive)
            .Members
            .Single();

        RecommendedBuild build = result.Build;

        Assert.Equal(4, build.Moves.Count);
        Assert.Equal(build.Moves.Count, build.Reasons.Count);
        Assert.All(build.Reasons, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
        Assert.Equal(
            result.MoveCandidates.Count,
            build.Moves.Count + build.Alternatives.Count);
        Assert.Empty(build.Moves.Intersect(build.Alternatives));
    }

    [Fact]
    public void Build_PrefersSameTypeDamageOnThePhysicalSideForAPhysicalAttacker()
    {
        PokemonSnapshot machamp = AnalysisTestData.Member(
            primaryType: PokemonType.Fighting,
            speciesName: "Machamp",
            baseStats: new StatBlock(90, 130, 80, 65, 85, 55),
            speciesId: 68,
            moves:
            [
                AnalysisTestData.Move(66, "Seismic Toss", PokemonType.Fighting),
                AnalysisTestData.Move(223, "Cross Chop", PokemonType.Fighting),
                AnalysisTestData.Move(70, "Strength", PokemonType.Normal),
            ]);

        RecommendedBuild build = _engine
            .Recommend(AnalysisTestData.Party(machamp), RecommendationProfileKind.Playthrough)
            .Members
            .Single()
            .Build;

        Assert.Equal(PokemonType.Fighting, build.Moves[0].Move.Type);
        Assert.Contains("same-type", build.Reasons[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Build_IsEmptyWhenNothingIsKnown()
    {
        PokemonSnapshot blank = AnalysisTestData.Member(speciesName: "Magikarp", speciesId: 129);

        RecommendedBuild build = _engine
            .Recommend(AnalysisTestData.Party(blank), RecommendationProfileKind.Competitive)
            .Members
            .Single()
            .Build;

        Assert.Empty(build.Moves);
        Assert.Single(build.Reasons);
    }

    [Fact]
    public void Recommend_HandlesEmptyParty()
    {
        PartySnapshot party = AnalysisTestData.Party();
        TeamRecommendation result =
            _engine.Recommend(party, RecommendationProfileKind.Playthrough);

        Assert.Empty(result.Members);
        Assert.Same(party, result.TeamAnalysis.Party);
    }
}
