using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class PokemonRecommendationEngineTests
{
    private readonly PokemonRecommendationEngine _engine =
        PokemonRecommendationEngine.CreateDefault(PKHeXSources.All);

    [Fact]
    public void CompetitiveProfile_CombinesLiveRoleWithMatchedPreset()
    {
        PokemonSnapshot gyarados = AnalysisTestData.Member(
            primaryType: PokemonType.Water,
            secondaryType: PokemonType.Flying,
            speciesName: "Gyarados",
            speciesId: 130,
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
            new StatBlock(332, 383, 194, 140, 236, 261),
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
    public void PlaythroughProfile_LooksAheadButStopsAtTheEvolutionBoundary()
    {
        PokemonSnapshot treecko = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Treecko",
            level: 12,
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
        Assert.Contains(
            result.MoveCandidates,
            move => move.Move.ReferenceId == "pursuit" &&
                move.LearnedAtLevel == 16 &&
                move.Availability == RecommendationAvailability.ArrivesWithLevelUp);
        Assert.DoesNotContain(
            result.MoveCandidates,
            move => move.LearnedAtLevel > 16);
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
            speciesId: 34,
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

    /// <summary>
    /// An unevolved species has no reference set of its own, but competitive advice targets
    /// the unambiguous final form rather than leaving it with whatever it knows now.
    /// </summary>
    [Fact]
    public void CompetitiveProfile_UsesTheFinalFormsWholeLegalPool()
    {
        PokemonSnapshot treecko = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Treecko",
            level: 5,
            baseStats: new StatBlock(40, 45, 35, 65, 55, 70),
            speciesId: 252,
            moves: [AnalysisTestData.Move(71, "Absorb", PokemonType.Grass)]);

        PokemonRecommendation result = _engine
            .Recommend(
                AnalysisTestData.Party(treecko),
                RecommendationProfileKind.Competitive)
            .Members
            .Single();

        Assert.NotNull(result.MatchedPreset);
        Assert.Equal("Sceptile", result.RoleAnalysis.JudgedSpeciesName);
        Assert.True(result.MoveCandidates.Count > 1);

        // A level 5 Pokémon is still offered what it learns much later: nobody battles
        // with the level it was caught at.
        Assert.Contains(
            result.MoveCandidates,
            move => move.LearnedAtLevel > treecko.Level);

        Assert.All(
            result.MoveCandidates,
            move => Assert.Equal(
                RecommendationAvailability.CompetitiveReference,
                move.Availability));
    }

    /// <summary>
    /// The visible competitive shortlist has two more places than the playthrough one. It
    /// used to be narrower, which inverted what the two screens exposed. See D-032.
    /// </summary>
    [Fact]
    public void CompetitiveProfile_ShowsAtLeastAsManyCandidatesAsPlaythrough()
    {
        PokemonSnapshot treecko = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Treecko",
            level: 5,
            baseStats: new StatBlock(40, 45, 35, 65, 55, 70),
            speciesId: 252,
            moves: [AnalysisTestData.Move(33, "Pound", PokemonType.Normal)]);

        PartySnapshot party = AnalysisTestData.Party(treecko);

        int playthrough = _engine
            .Recommend(party, RecommendationProfileKind.Playthrough)
            .Members[0]
            .MoveCandidates
            .Count;
        int competitive = _engine
            .Recommend(party, RecommendationProfileKind.Competitive)
            .Members[0]
            .MoveCandidates
            .Count;

        Assert.True(competitive >= playthrough, $"competitive {competitive}, playthrough {playthrough}");
    }

    /// <summary>A best set does not put a 20-power move where a 120-power one fits.</summary>
    [Fact]
    public void Build_PrefersTheStrongerMoveWhenTheRestIsEqual()
    {
        PokemonSnapshot treecko = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Treecko",
            level: 50,
            baseStats: new StatBlock(40, 45, 35, 65, 55, 70),
            speciesId: 252,
            moves: [AnalysisTestData.Move(71, "Absorb", PokemonType.Grass)]);

        RecommendedBuild build = _engine
            .Recommend(AnalysisTestData.Party(treecko), RecommendationProfileKind.Competitive)
            .Members
            .Single()
            .Build;

        Assert.DoesNotContain(build.Moves, move => move.Move.Name == "Absorb");
    }

    [Fact]
    public void Build_PicksFourMovesWithAReasonInsideTheBoundedShortlist()
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

    [Fact]
    public void Build_TreatsSeismicTossAsDamageWithoutInventingCoverage()
    {
        PokemonSnapshot blissey = AnalysisTestData.Member(
            primaryType: PokemonType.Normal,
            speciesName: "Blissey",
            speciesId: 242,
            level: 84,
            baseStats: new StatBlock(255, 10, 10, 75, 135, 55),
            moves: [AnalysisTestData.Move(69, "Seismic Toss", PokemonType.Fighting)]);

        PokemonRecommendation result = _engine
            .Recommend(AnalysisTestData.Party(blissey), RecommendationProfileKind.Competitive)
            .Members
            .Single();

        BuildSlot seismicToss = Assert.Single(
            result.Build.Slots,
            slot => slot.Move.Move.ReferenceId == "seismictoss");

        Assert.Equal(BuildSlotRole.DirectDamage, seismicToss.Role);
        Assert.DoesNotContain("non-damaging", seismicToss.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.RoleAnalysis.PhysicalMoveCount > 0);
    }

    [Fact]
    public void CompetitiveProjectionIsForTheTrainedLevelHundredFinalForm()
    {
        PokemonSnapshot treecko = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Treecko",
            speciesId: 252,
            level: 5,
            baseStats: new StatBlock(40, 45, 35, 65, 55, 70),
            moves: [AnalysisTestData.Move(71, "Absorb", PokemonType.Grass)]);

        PokemonRecommendation result = _engine
            .Recommend(AnalysisTestData.Party(treecko), RecommendationProfileKind.Competitive)
            .Members
            .Single();

        Assert.Equal("Sceptile", result.RoleAnalysis.JudgedSpeciesName);
        Assert.True(result.EffortValues.ProjectedStats!.Value.Hp >= 200);
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

        // Machines and tutors still have to be checked against the save.
        Assert.All(
            result.MoveCandidates.Where(move =>
                move.Source is MoveCandidateSource.Machine or MoveCandidateSource.Tutor),
            move => Assert.Equal(
                RecommendationAvailability.RequiresAvailabilityCheck,
                move.Availability));

        Assert.Contains(
            result.MoveCandidates,
            move => move.Availability == RecommendationAvailability.ArrivesWithLevelUp);

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

    /// <summary>
    /// A Pokémon with an empty moveset is not out of options: its learnset is. Only a
    /// species the learn source cannot place leaves the build genuinely empty.
    /// </summary>
    [Fact]
    public void Build_FallsBackToTheLearnsetBeforeGivingUp()
    {
        PokemonSnapshot magikarp = AnalysisTestData.Member(speciesName: "Magikarp", speciesId: 129);

        RecommendedBuild fromLearnset = _engine
            .Recommend(AnalysisTestData.Party(magikarp), RecommendationProfileKind.Competitive)
            .Members
            .Single()
            .Build;

        Assert.NotEmpty(fromLearnset.Slots);

        PokemonSnapshot unknown = AnalysisTestData.Member(speciesName: "Nothing", speciesId: 999);

        RecommendedBuild empty = _engine
            .Recommend(AnalysisTestData.Party(unknown), RecommendationProfileKind.Competitive)
            .Members
            .Single()
            .Build;

        Assert.Empty(empty.Slots);
        Assert.Empty(empty.Moves);
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
