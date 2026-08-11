using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class PokemonRecommendationEngineTests
{
    private readonly PokemonRecommendationEngine _engine =
        PokemonRecommendationEngine.CreateDefault(PKHeXGen3MoveLearnSource.Instance);

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

        Assert.Equal(4, build.Slots.Count);
        Assert.All(build.Slots, slot => Assert.False(string.IsNullOrWhiteSpace(slot.Reason)));
        Assert.Equal(
            result.MoveCandidates.Count,
            build.Slots.Count + build.Alternatives.Count);
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

        Assert.Equal(PokemonType.Fighting, build.Slots[0].Move.Move.Type);
        Assert.Equal(BuildSlotRole.SameType, build.Slots[0].Role);
    }

    /// <summary>
    /// The point of reading machines at all: in Gen 3 the coverage a playthrough team is
    /// missing usually arrives on a TM, not from levelling. See D-030.
    /// </summary>
    [Fact]
    public void Playthrough_OffersMachineAndTutorMovesAlongsideLevelUp()
    {
        PokemonSnapshot charizard = AnalysisTestData.Member(
            primaryType: PokemonType.Fire,
            secondaryType: PokemonType.Flying,
            speciesName: "Charizard",
            level: 50,
            baseStats: new StatBlock(78, 84, 78, 109, 85, 100),
            speciesId: 6,
            moves: [AnalysisTestData.Move(52, "Ember", PokemonType.Fire)]);

        PokemonRecommendation result = _engine
            .Recommend(AnalysisTestData.Party(charizard), RecommendationProfileKind.Playthrough)
            .Members
            .Single();

        Assert.Contains(result.MoveCandidates, move => move.Source == MoveCandidateSource.Machine);
        Assert.Contains(result.MoveCandidates, move => move.Source == MoveCandidateSource.Tutor);

        // Everything that is not already on the Pokémon has to be checked against the save.
        Assert.All(
            result.MoveCandidates.Where(move => move.Source != MoveCandidateSource.CurrentMoveset),
            move => Assert.Equal(
                RecommendationAvailability.RequiresAvailabilityCheck,
                move.Availability));

        // And a machine move is good enough to take a slot away from Ember.
        Assert.Contains(
            result.Build.Moves,
            move => move.Source is MoveCandidateSource.Machine or MoveCandidateSource.Tutor);

        // A wide pool must not collapse into one type: each slot is judged against the
        // slots already filled, so four moves cover at least three types.
        Assert.True(
            result.Build.Moves.Select(move => move.Move.Type).Distinct().Count() >= 3,
            string.Join(", ", result.Build.Moves.Select(move => $"{move.Move.Name} ({move.Move.Type})")));
    }

    /// <summary>
    /// Four attacks is not what a real set looks like, and it leaves a Pokémon helpless
    /// against anything it cannot simply out-damage. See D-031.
    /// </summary>
    [Fact]
    public void Build_KeepsOneSlotForSomethingThatIsNotAnAttack()
    {
        PokemonSnapshot charizard = AnalysisTestData.Member(
            primaryType: PokemonType.Fire,
            secondaryType: PokemonType.Flying,
            speciesName: "Charizard",
            level: 60,
            baseStats: new StatBlock(78, 84, 78, 109, 85, 100),
            speciesId: 6,
            moves: [AnalysisTestData.Move(52, "Ember", PokemonType.Fire)]);

        RecommendedBuild build = _engine
            .Recommend(AnalysisTestData.Party(charizard), RecommendationProfileKind.Playthrough)
            .Members
            .Single()
            .Build;

        Assert.Equal(4, build.Slots.Count);
        Assert.Contains(build.Slots, slot => slot.Role == BuildSlotRole.Utility);
        Assert.Equal(3, build.Slots.Count(slot => slot.Role != BuildSlotRole.Utility));
    }

    /// <summary>
    /// Built independently, two of the same Pokémon get the same four moves and the team
    /// ends up no wider than one of them. See D-031.
    /// </summary>
    [Fact]
    public void Build_DoesNotHandTwoTeammatesTheSameAnswerToTheSameGap()
    {
        PokemonSnapshot First(int slot) => AnalysisTestData.Member(
            slot: slot,
            primaryType: PokemonType.Fire,
            secondaryType: PokemonType.Flying,
            speciesName: "Charizard",
            level: 60,
            baseStats: new StatBlock(78, 84, 78, 109, 85, 100),
            speciesId: 6,
            moves: [AnalysisTestData.Move(52, "Ember", PokemonType.Fire)]);

        IReadOnlyList<PokemonRecommendation> members = _engine
            .Recommend(
                AnalysisTestData.Party(First(0), First(1)),
                RecommendationProfileKind.Playthrough)
            .Members;

        string[] firstBuild = [.. members[0].Build.Moves.Select(move => move.Move.ReferenceId)];
        string[] secondBuild = [.. members[1].Build.Moves.Select(move => move.Move.ReferenceId)];

        Assert.NotEqual(firstBuild, secondBuild);

        // No slot may claim a hole a teammate's build already filled.
        IReadOnlyList<string> claims =
        [
            .. members
                .SelectMany(member => member.Build.Slots)
                .Where(slot => slot.Role == BuildSlotRole.Coverage)
                .Select(slot => slot.Reason),
        ];
        Assert.Equal(claims.Distinct().Count(), claims.Count);
    }

    /// <summary>Nothing from a later generation may ever be offered. See D-030.</summary>
    [Fact]
    public void OnlyMovesThatExistInTheRunningGenerationAreOffered()
    {
        const int lastGen3Move = 354;

        PokemonSnapshot blaziken = AnalysisTestData.Member(
            primaryType: PokemonType.Fire,
            secondaryType: PokemonType.Fighting,
            speciesName: "Blaziken",
            level: 70,
            baseStats: new StatBlock(80, 120, 70, 110, 70, 80),
            speciesId: 257,
            moves: [AnalysisTestData.Move(53, "Flamethrower", PokemonType.Fire)]);

        PokemonRecommendation result = _engine
            .Recommend(AnalysisTestData.Party(blaziken), RecommendationProfileKind.Playthrough)
            .Members
            .Single();

        Assert.NotEmpty(result.MoveCandidates);
        Assert.All(
            result.MoveCandidates,
            move => Assert.InRange(move.Move.MoveId, 1, lastGen3Move));
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

        Assert.Empty(build.Slots);
        Assert.Empty(build.Moves);
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
