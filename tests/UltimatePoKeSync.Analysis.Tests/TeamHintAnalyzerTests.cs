using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class TeamHintAnalyzerTests
{
    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    private static readonly StoryMilestone Starter =
        new("starter", "Starter received", 0, 0, "Choose a starter");

    private static readonly StoryMilestone RouteOne =
        new("route-1", "Route 1", 1, 0, "Reach Route 1");

    private static readonly StoryMilestone RouteTwo =
        new("route-2", "Route 2", 2, 0, "Reach Route 2");

    private static readonly StoryMilestone RouteThree =
        new("route-3", "Route 3", 3, 1, "Earn the Trio Badge");

    private static readonly StoryMilestone VictoryRoad =
        new("victory-road", "Victory Road", 20, 8, "Earn all eight badges");

    private readonly TeamHintAnalyzer _analyzer = new(PKHeXSources.All);

    [Fact]
    public void RouteTwoSnivyGetsThreeDistinctEarlyGamePlansAndNoFutureSpecies()
    {
        PokemonSnapshot snivy = Member(
            slot: 0,
            speciesId: 495,
            speciesName: "Snivy",
            level: 6,
            primaryType: PokemonType.Grass,
            move: new MoveSlot(33, "Tackle", PokemonType.Normal, 35, 35));
        PokemonSnapshot tepigEgg = Member(
            slot: 1,
            speciesId: 498,
            speciesName: "Tepig",
            level: 1,
            primaryType: PokemonType.Fire,
            isEgg: true);

        TeamHintAnalysis result = _analyzer.Analyze(
            Party(snivy, tepigEgg),
            RouteTwo,
            EarlyAndFutureEncounters());

        Assert.Equal(3, result.Plans.Count);
        Assert.All(result.Plans, plan =>
        {
            Assert.InRange(plan.Additions.Count, 1, 3);
            Assert.Null(plan.Replacement);
            Assert.NotEmpty(plan.Factors);
            Assert.Equal(plan.Score, plan.Factors.Sum(factor => factor.Points));
        });

        string[] signatures =
        [
            .. result.Plans.Select(plan => string.Join(",", plan.Additions
                .Select(candidate => candidate.SpeciesId)
                .Order())),
        ];
        Assert.Equal(3, signatures.Distinct().Count());

        TeamHintCandidate[] suggestions = [.. result.Plans.SelectMany(plan => plan.Additions)];
        Assert.All(suggestions, suggestion => Assert.Contains(
            suggestion.SpeciesId,
            new[] { 504, 506, 509, 531 }));
        Assert.DoesNotContain(suggestions, suggestion =>
            suggestion.SpeciesId is 495 or 498 or 633 or 635);
        Assert.Contains(result.Plans[0].Additions, suggestion =>
            suggestion.SpeciesName is "Patrat" or "Lillipup");
        Assert.All(suggestions, suggestion =>
        {
            Assert.False(string.IsNullOrWhiteSpace(suggestion.Location));
            Assert.False(string.IsNullOrWhiteSpace(suggestion.Reason));
            Assert.InRange(suggestion.RecommendedLevel, 1, 100);
        });

        TeamHintCandidate lillipup = Assert.Single(
            suggestions.DistinctBy(candidate => candidate.SpeciesId),
            candidate => candidate.SpeciesName == "Lillipup");
        Assert.Equal("Stoutland", lillipup.ProjectedEvolution?.SpeciesName);
    }

    [Fact]
    public void TwoEarlyCandidatesProduceUsefulSingleCatchAlternatives()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(Member(0, 495, "Snivy", 6, PokemonType.Grass)),
            RouteOne,
            [
                Encounter(504, "Patrat", RouteOne, "Route 1", 2, 4, 50),
                Encounter(506, "Lillipup", RouteOne, "Route 1", 2, 4, 50),
            ]);

        Assert.NotEmpty(result.Plans);
        Assert.Contains(result.Plans, plan =>
            plan.Additions.Count == 1 && plan.Additions[0].SpeciesName == "Patrat");
        Assert.Contains(result.Plans, plan =>
            plan.Additions.Count == 1 && plan.Additions[0].SpeciesName == "Lillipup");

        // The pair can still be one of the three plans when together it genuinely scores
        // better, but neither option is hidden behind a forced two-catch commitment.
        Assert.All(result.Plans, plan => Assert.InRange(plan.Additions.Count, 1, 2));
    }

    [Theory]
    [InlineData(509, "Purrloin", 510, "Liepard")]
    [InlineData(510, "Liepard", 509, "Purrloin")]
    public void ACarriedEvolutionFamilyIsNeverSuggestedAgain(
        int carriedSpeciesId,
        string carriedName,
        int candidateSpeciesId,
        string candidateName)
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(
                Member(0, 495, "Snivy", 15, PokemonType.Grass),
                Member(1, carriedSpeciesId, carriedName, 1, PokemonType.Dark, isEgg: true)),
            RouteTwo,
            [
                Encounter(candidateSpeciesId, candidateName, RouteTwo, "Route 2", 4, 7, 40),
                Encounter(504, "Patrat", RouteTwo, "Route 2", 4, 7, 40),
                Encounter(506, "Lillipup", RouteTwo, "Route 2", 4, 7, 40),
            ]);

        Assert.NotEmpty(result.Plans);
        Assert.DoesNotContain(
            result.Plans.SelectMany(plan => plan.Additions),
            candidate => candidate.SpeciesId == candidateSpeciesId);
    }

    [Fact]
    public void EncounterAvailabilityUsesTheSelectedMilestoneOrderOnly()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(Member(0, 495, "Snivy", 40, PokemonType.Grass)),
            RouteTwo,
            EarlyAndFutureEncounters());

        // A high-level starter does not make Route 3 or Victory Road reachable.
        Assert.DoesNotContain(
            result.Plans.SelectMany(plan => plan.Additions),
            candidate => candidate.SpeciesId is 519 or 633 or 635);
    }

    [Fact]
    public void GiftCannotBeRepresentedAsAnEncounterMethod()
    {
        Assert.DoesNotContain(
            Enum.GetNames<EncounterMethod>(),
            name => name.Contains("Gift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AFullPartyGetsExplicitSingleMemberReplacementPlans()
    {
        PartySnapshot fullParty = Party(
            Member(0, 495, "Snivy", 18, PokemonType.Grass),
            Member(1, 498, "Tepig", 18, PokemonType.Fire),
            Member(2, 501, "Oshawott", 18, PokemonType.Water),
            Member(3, 504, "Patrat", 17, PokemonType.Normal),
            Member(4, 509, "Purrloin", 17, PokemonType.Dark),
            Member(5, 519, "Pidove", 17, PokemonType.Normal, PokemonType.Flying));
        EncounterCandidate[] alternatives =
        [
            Encounter(506, "Lillipup", RouteTwo, "Route 2", 4, 7, 40),
            Encounter(531, "Audino", RouteTwo, "Route 2", 4, 7, 10,
                EncounterMethod.ShakingGrass),
            Encounter(524, "Roggenrola", RouteTwo, "Wellspring Cave", 10, 13, 30,
                EncounterMethod.Cave),
            Encounter(522, "Blitzle", RouteTwo, "Route 2", 8, 11, 20),
        ];

        TeamHintAnalysis result = _analyzer.Analyze(fullParty, RouteTwo, alternatives);

        Assert.Equal(3, result.Plans.Count);
        Assert.All(result.Plans, plan =>
        {
            TeamHintReplacement replacement = Assert.IsType<TeamHintReplacement>(plan.Replacement);
            Assert.Single(plan.Additions);
            Assert.Contains(replacement.SpeciesName, plan.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain(
                plan.Additions,
                candidate => candidate.SpeciesId == replacement.SpeciesId);
        });
    }

    [Fact]
    public void AFullPartyCanReplaceAnEggWithoutRevealingItsSpecies()
    {
        PartySnapshot fullParty = Party(
            Member(0, 495, "Snivy", 18, PokemonType.Grass),
            Member(1, 498, "Tepig", 18, PokemonType.Fire),
            Member(2, 501, "Oshawott", 18, PokemonType.Water),
            Member(3, 504, "Patrat", 17, PokemonType.Normal),
            Member(4, 509, "Purrloin", 17, PokemonType.Dark),
            Member(5, 633, "Secret Deino", 1, PokemonType.Dragon, isEgg: true));

        TeamHintAnalysis result = _analyzer.Analyze(
            fullParty,
            RouteTwo,
            [Encounter(522, "Blitzle", RouteTwo, "Route 3", 8, 11, 20)]);

        TeamHintPlan eggPlan = Assert.Single(
            result.Plans,
            plan => plan.Replacement?.SlotIndex == 5);
        Assert.Equal("Egg", eggPlan.Replacement?.SpeciesName);
        Assert.Equal(0, eggPlan.Replacement?.SpeciesId);
        Assert.Contains("Replace Egg", eggPlan.Summary, StringComparison.Ordinal);

        string publicText = string.Join(
            "\n",
            result.Plans.SelectMany(plan =>
                new[]
                {
                    plan.Summary,
                    plan.Replacement?.SpeciesName ?? string.Empty,
                }
                .Concat(plan.Factors.Select(factor => factor.Explanation))
                .Concat(plan.Additions.SelectMany(candidate => new[]
                {
                    candidate.SpeciesName,
                    candidate.EvaluatedSpeciesName,
                    candidate.Location,
                    candidate.Requirement,
                    candidate.Reason,
                    candidate.ProjectedEvolution?.SpeciesName ?? string.Empty,
                    candidate.ProjectedEvolution?.Requirements ?? string.Empty,
                }))));

        Assert.DoesNotContain("Deino", publicText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", publicText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            result.Plans,
            plan => plan.Replacement?.SpeciesId == 633);
    }

    [Fact]
    public void AFullPartyGetsNoReplacementWhenTheOnlySwapHasNoStrategicGain()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(
                Member(0, 132, "Ditto", 20, PokemonType.Normal),
                Member(1, 132, "Ditto", 20, PokemonType.Normal),
                Member(2, 132, "Ditto", 20, PokemonType.Normal),
                Member(3, 132, "Ditto", 20, PokemonType.Normal),
                Member(4, 132, "Ditto", 20, PokemonType.Normal),
                Member(5, 132, "Ditto", 20, PokemonType.Normal)),
            RouteTwo,
            [Encounter(235, "Smeargle", RouteTwo, "Test field", 20, 20, 100)]);

        Assert.Empty(result.Plans);
    }

    [Fact]
    public void ConditionalEncounterIsNotPromisedAsAvailableNow()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(Member(0, 495, "Snivy", 30, PokemonType.Grass)),
            RouteTwo,
            [Encounter(
                616,
                "Shelmet",
                RouteTwo,
                "Route 8",
                30,
                33,
                20,
                availabilityIsConditional: true)]);

        Assert.Equal(0, result.AvailableCandidateCount);
        Assert.Empty(result.Plans);
    }

    [Fact]
    public void TrainingTargetNeverFallsBelowTheDisplayedEncounterRange()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(Member(0, 495, "Snivy", 20, PokemonType.Grass)),
            RouteTwo,
            [Encounter(618, "Stunfisk", RouteTwo, "Route 8 water", 15, 35, 100,
                EncounterMethod.Surf)]);

        TeamHintCandidate stunfisk = Assert.Single(Assert.Single(result.Plans).Additions);
        Assert.True(stunfisk.RecommendedLevel >= stunfisk.MaximumEncounterLevel);
    }

    [Fact]
    public void CandidateCoverageComesFromLevelUpMovesRatherThanMachines()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(
                Member(0, 495, "Snivy", 6, PokemonType.Grass),
                Member(1, 498, "Tepig", 1, PokemonType.Fire, isEgg: true),
                Member(2, 501, "Oshawott", 1, PokemonType.Water, isEgg: true),
                Member(3, 519, "Pidove", 1, PokemonType.Flying, isEgg: true),
                Member(4, 524, "Roggenrola", 1, PokemonType.Rock, isEgg: true)),
            RouteTwo,
            [Encounter(506, "Lillipup", RouteTwo, "Route 2", 4, 7, 40)]);

        TeamHintCandidate lillipup = Assert.Single(Assert.Single(result.Plans).Additions);

        Assert.Contains(PokemonType.Normal, lillipup.LevelUpAttackTypes);
        // Lillipup can use Fighting-type machines, but has no Fighting level-up attack in
        // this early target window. Ownership is unknown, so it must not claim that reach.
        Assert.DoesNotContain(PokemonType.Fighting, lillipup.LevelUpAttackTypes);
    }

    [Fact]
    public void EvolvedFormsDoNotGainLowLevelMoveReminderCoverageForFree()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(Member(0, 495, "Snivy", 35, PokemonType.Grass)),
            RouteOne,
            [Encounter(506, "Lillipup", RouteOne, "Route 1", 2, 4, 40)]);

        TeamHintCandidate lillipup = Assert.Single(Assert.Single(result.Plans).Additions);

        Assert.Equal("Stoutland", lillipup.EvaluatedSpeciesName);
        Assert.Contains(PokemonType.Normal, lillipup.LevelUpAttackTypes);
        Assert.DoesNotContain(PokemonType.Fire, lillipup.LevelUpAttackTypes);
        Assert.DoesNotContain(PokemonType.Ice, lillipup.LevelUpAttackTypes);
        Assert.DoesNotContain(PokemonType.Electric, lillipup.LevelUpAttackTypes);
    }

    [Fact]
    public void RealisticCoverageNeverClaimsMoreMoveTypesThanFourMoveSlots()
    {
        TeamHintAnalysis result = _analyzer.Analyze(
            Party(Member(0, 495, "Snivy", 50, PokemonType.Grass)),
            RouteTwo,
            [Encounter(513, "Pansear", RouteTwo, "Pinwheel Forest", 15, 15, 10,
                EncounterMethod.ShakingGrass)]);

        TeamHintCandidate pansear = Assert.Single(Assert.Single(result.Plans).Additions);

        Assert.InRange(pansear.LevelUpAttackTypes.Count, 1, 4);
        Assert.DoesNotContain(PokemonType.Normal, pansear.LevelUpAttackTypes);
    }

    [Fact]
    public void OnePlanNeverContainsTwoAlternativesFromTheSameExclusiveGroup()
    {
        EncounterCandidate[] encounters =
        [
            Encounter(564, "Tirtouga", RouteTwo, "Relic Castle", 25, 25, 100,
                EncounterMethod.Fossil, exclusiveGroup: "relic-castle-fossil"),
            Encounter(566, "Archen", RouteTwo, "Relic Castle", 25, 25, 100,
                EncounterMethod.Fossil, exclusiveGroup: " relic-castle-fossil "),
            Encounter(506, "Lillipup", RouteTwo, "Route 2", 4, 7, 40),
            Encounter(509, "Purrloin", RouteTwo, "Route 2", 4, 5, 20),
            Encounter(531, "Audino", RouteTwo, "Route 2", 4, 7, 10,
                EncounterMethod.ShakingGrass),
        ];

        TeamHintAnalysis result = _analyzer.Analyze(
            Party(Member(0, 495, "Snivy", 20, PokemonType.Grass)),
            RouteTwo,
            encounters);

        Assert.Equal(3, result.Plans.Count);
        Assert.All(result.Plans, plan =>
        {
            Assert.InRange(plan.Additions.Count, 1, 3);
            Assert.False(
                plan.Additions.Any(candidate => candidate.SpeciesId == 564) &&
                plan.Additions.Any(candidate => candidate.SpeciesId == 566));
        });
    }

    [Fact]
    public void BridgeShadowsAndRoamersHaveExplicitPracticalityCosts()
    {
        PartySnapshot oneOpenSlot = Party(
            Member(0, 495, "Snivy", 20, PokemonType.Grass),
            Member(1, 498, "Tepig", 1, PokemonType.Fire, isEgg: true),
            Member(2, 501, "Oshawott", 1, PokemonType.Water, isEgg: true),
            Member(3, 504, "Patrat", 1, PokemonType.Normal, isEgg: true),
            Member(4, 509, "Purrloin", 1, PokemonType.Dark, isEgg: true));
        EncounterCandidate[] encounters =
        [
            Encounter(506, "Lillipup", RouteTwo, "Route 2", 20, 20, 100),
            Encounter(580, "Ducklett", RouteTwo, "Driftveil Drawbridge", 20, 20, 100,
                EncounterMethod.BridgeShadow),
            Encounter(641, "Tornadus", RouteTwo, "Unova", 20, 20, 100,
                EncounterMethod.Roaming),
        ];

        TeamHintAnalysis result = _analyzer.Analyze(oneOpenSlot, RouteTwo, encounters);

        Assert.Equal(3, result.Plans.Count);
        Dictionary<EncounterMethod, int> practicality = result.Plans.ToDictionary(
            plan => Assert.Single(plan.Additions).Method,
            plan => Assert.Single(
                plan.Factors,
                factor => factor.Kind == TeamHintScoreKind.Practicality).Points);

        Assert.Equal(12, practicality[EncounterMethod.Grass]);
        Assert.Equal(10, practicality[EncounterMethod.BridgeShadow]);
        Assert.Equal(5, practicality[EncounterMethod.Roaming]);
        Assert.True(Encounter(641, "Tornadus", RouteTwo, "Unova", 20, 20, 100,
            EncounterMethod.Roaming).IsLimited);
    }

    private static IReadOnlyList<EncounterCandidate> EarlyAndFutureEncounters() =>
    [
        Encounter(495, "Snivy", Starter, "Nuvema Town", 5, 5, 100,
            EncounterMethod.Static),
        Encounter(498, "Tepig", Starter, "Nuvema Town", 5, 5, 100,
            EncounterMethod.Static),
        Encounter(504, "Patrat", RouteOne, "Route 1", 2, 4, 50),
        Encounter(506, "Lillipup", RouteOne, "Route 1", 2, 4, 50),
        Encounter(509, "Purrloin", RouteTwo, "Route 2", 4, 5, 20),
        Encounter(531, "Audino", RouteTwo, "Route 2", 4, 7, 10,
            EncounterMethod.ShakingGrass),
        Encounter(519, "Pidove", RouteThree, "Route 3", 8, 11, 40),
        Encounter(633, "Deino", VictoryRoad, "Victory Road", 38, 40, 10,
            EncounterMethod.Cave),
        Encounter(635, "Hydreigon", VictoryRoad, "Victory Road", 64, 64, 1,
            EncounterMethod.Static),
    ];

    private static EncounterCandidate Encounter(
        int speciesId,
        string speciesName,
        StoryMilestone milestone,
        string location,
        int minimumLevel,
        int maximumLevel,
        int rate,
        EncounterMethod method = EncounterMethod.Grass,
        string? exclusiveGroup = null,
        bool availabilityIsConditional = false) =>
        new(
            speciesId,
            speciesName,
            milestone,
            location,
            method,
            minimumLevel,
            maximumLevel,
            EncounterRatePercent: rate,
            ExclusiveGroup: exclusiveGroup,
            AvailabilityIsConditional: availabilityIsConditional);

    private static PartySnapshot Party(params PokemonSnapshot[] members) =>
        new(Black, members, DateTimeOffset.UnixEpoch, 1, []);

    private static PokemonSnapshot Member(
        int slot,
        int speciesId,
        string speciesName,
        int level,
        PokemonType primaryType,
        PokemonType secondaryType = PokemonType.None,
        MoveSlot? move = null,
        bool isEgg = false) => new()
        {
            SlotIndex = slot,
            SpeciesId = (ushort)speciesId,
            SpeciesName = speciesName,
            Nickname = speciesName,
            Level = level,
            PrimaryType = primaryType,
            SecondaryType = secondaryType,
            BaseStats = PKHeXSources.BaseStats.FindBaseStats(Black, speciesId)
                ?? new StatBlock(50, 50, 50, 50, 50, 50),
            IndividualValues = new StatBlock(20, 20, 20, 20, 20, 20),
            EffortValues = new StatBlock(0, 0, 0, 0, 0, 0),
            CurrentStats = new StatBlock(50, 50, 50, 50, 50, 50),
            NatureId = 0,
            NatureName = "Hardy",
            AbilityId = 0,
            AbilityName = "-",
            HeldItemId = 0,
            HeldItemName = "-",
            Moves = move is null ? [] : [move],
            IsEgg = isEgg,
            IsShiny = false,
            PersonalityValue = (uint)(slot + 1),
            CurrentHp = isEgg ? 0 : 50,
            Status = StatusCondition.None,
            Friendship = 70,
            Experience = 0,
        };
}
