using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

public sealed class BlackEncounterCatalogTests
{
    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    private readonly BlackEncounterCatalog _catalog = BlackEncounterCatalog.Instance;

    [Fact]
    public void SupportsOriginalBlackButNotWhiteOrBlackTwo()
    {
        Assert.True(_catalog.Supports(Black));
        Assert.True(_catalog.Supports(Black with { GameCode = "IRBE" }));
        Assert.False(_catalog.Supports(Black with { GameCode = "IRAI" }));
        Assert.False(_catalog.Supports(Black with { GameCode = "IREI" }));
    }

    [Fact]
    public void StoryRunsInOrderFromRouteOneThroughVictoryRoad()
    {
        IReadOnlyList<StoryMilestone> milestones = _catalog.FindMilestones(Black);

        Assert.Equal("route-1", milestones[0].Id);
        Assert.Equal("victory-road", milestones[^1].Id);
        Assert.Equal(milestones.Count, milestones.Select(milestone => milestone.Id).Distinct().Count());
        Assert.True(milestones.Zip(milestones.Skip(1)).All(pair => pair.First.Order < pair.Second.Order));
        Assert.Equal(8, milestones[^1].BadgeCount);
    }

    [Theory]
    [InlineData(0, "route-1")]
    [InlineData(1, "dreamyard")]
    [InlineData(2, "nacrene")]
    [InlineData(3, "route-4")]
    [InlineData(4, "route-5-16")]
    [InlineData(5, "route-6")]
    [InlineData(6, "celestial-tower")]
    [InlineData(7, "route-8")]
    [InlineData(8, "route-10")]
    public void BadgeOnlyDetectionUsesConservativeCheckpoints(int badges, string expectedId) =>
        Assert.Equal(expectedId, _catalog.FindConservativeMilestone(badges).Id);

    [Fact]
    public void ManualTimelineExposesRoutesBeforeTheFollowingGymWhileBadgeDetectionStaysSafe()
    {
        IReadOnlyDictionary<string, StoryMilestone> milestones = _catalog
            .FindMilestones(Black)
            .ToDictionary(milestone => milestone.Id);

        Assert.Equal((2, 3), (
            milestones["route-4"].BadgeCount,
            milestones["route-4"].GuaranteedWhenBadgesAtLeast));
        Assert.Equal((3, 4), (
            milestones["route-5-16"].BadgeCount,
            milestones["route-5-16"].GuaranteedWhenBadgesAtLeast));
        Assert.Equal((5, 6), (
            milestones["route-7"].BadgeCount,
            milestones["route-7"].GuaranteedWhenBadgesAtLeast));
        Assert.Equal((5, 6), (
            milestones["celestial-tower"].BadgeCount,
            milestones["celestial-tower"].GuaranteedWhenBadgesAtLeast));
        Assert.Equal((6, 7), (
            milestones["route-8"].BadgeCount,
            milestones["route-8"].GuaranteedWhenBadgesAtLeast));
    }

    [Fact]
    public void EarlyBlackEncountersMatchTheGameAndExcludeGifts()
    {
        IReadOnlyList<EncounterCandidate> encounters = _catalog.FindEncounters(Black);

        EncounterCandidate patrat = Assert.Single(encounters, encounter => encounter.SpeciesId == 504);
        Assert.Equal("Route 1", patrat.Location);
        Assert.Equal(EncounterMethod.Grass, patrat.Method);
        Assert.Equal(2, patrat.MinimumLevel);
        Assert.Equal(4, patrat.MaximumLevel);

        EncounterCandidate lillipup = Assert.Single(encounters, encounter => encounter.SpeciesId == 506);
        Assert.Equal("route-1", lillipup.EarliestMilestone.Id);

        EncounterCandidate audino = Assert.Single(encounters, encounter => encounter.SpeciesId == 531);
        Assert.Equal("dreamyard", audino.EarliestMilestone.Id);
        Assert.Equal(EncounterMethod.ShakingGrass, audino.Method);

        EncounterCandidate purrloin = Assert.Single(encounters, encounter => encounter.SpeciesId == 509);
        Assert.Equal("route-2", purrloin.EarliestMilestone.Id);
        Assert.Equal(4, purrloin.MinimumLevel);
        Assert.Equal(5, purrloin.MaximumLevel);

        EncounterCandidate munna = Assert.Single(encounters, encounter => encounter.SpeciesId == 517);
        Assert.Equal("dreamyard", munna.EarliestMilestone.Id);
        Assert.Equal(8, munna.MinimumLevel);
        Assert.Equal(10, munna.MaximumLevel);

        EncounterCandidate pidove = Assert.Single(encounters, encounter => encounter.SpeciesId == 519);
        Assert.Equal(8, pidove.MinimumLevel);
        Assert.Equal(11, pidove.MaximumLevel);

        Assert.DoesNotContain(encounters, encounter => encounter.SpeciesId is 495 or 498 or 501);
        Assert.DoesNotContain(encounters, encounter =>
            encounter.SpeciesId is 511 or 513 or 515 && encounter.EarliestMilestone.Id == "dreamyard");
    }

    [Fact]
    public void SpecialMethodsCarryTheirRealPrerequisites()
    {
        IReadOnlyList<EncounterCandidate> encounters = _catalog.FindEncounters(Black);

        EncounterCandidate ducklett = Assert.Single(encounters, encounter => encounter.SpeciesId == 580);
        Assert.Equal(EncounterMethod.BridgeShadow, ducklett.Method);
        Assert.Equal("driftveil", ducklett.EarliestMilestone.Id);

        EncounterCandidate basculin = Assert.Single(encounters, encounter => encounter.SpeciesId == 550);
        Assert.Equal(EncounterMethod.InGameTrade, basculin.Method);
        Assert.Contains("Minccino", basculin.Requirement, StringComparison.Ordinal);

        EncounterCandidate tirtouga = Assert.Single(encounters, encounter => encounter.SpeciesId == 564);
        EncounterCandidate archen = Assert.Single(encounters, encounter => encounter.SpeciesId == 566);
        Assert.Equal(EncounterMethod.Fossil, tirtouga.Method);
        Assert.Equal(tirtouga.ExclusiveGroup, archen.ExclusiveGroup);
        Assert.False(string.IsNullOrWhiteSpace(tirtouga.ExclusiveGroup));

        EncounterCandidate petilil = Assert.Single(encounters, encounter => encounter.SpeciesId == 548);
        Assert.Equal("pinwheel-inner", petilil.EarliestMilestone.Id);
        Assert.Contains("Cottonee", petilil.Requirement, StringComparison.Ordinal);

        EncounterCandidate frillish = Assert.Single(encounters, encounter => encounter.SpeciesId == 592);
        Assert.Equal(EncounterMethod.Surf, frillish.Method);
        Assert.Equal((5, 15), (frillish.MinimumLevel, frillish.MaximumLevel));

        EncounterCandidate alomomola = Assert.Single(encounters, encounter => encounter.SpeciesId == 594);
        Assert.Equal(EncounterMethod.RipplingWater, alomomola.Method);
        Assert.Equal("surf-detours", alomomola.EarliestMilestone.Id);

        EncounterCandidate shelmet = Assert.Single(encounters, encounter => encounter.SpeciesId == 616);
        Assert.Contains("winter", shelmet.Requirement, StringComparison.OrdinalIgnoreCase);
        Assert.True(shelmet.AvailabilityIsConditional);

        EncounterCandidate stunfisk = Assert.Single(encounters, encounter => encounter.SpeciesId == 618);
        Assert.Equal(EncounterMethod.Surf, stunfisk.Method);
        Assert.Equal((15, 35), (stunfisk.MinimumLevel, stunfisk.MaximumLevel));
        Assert.Equal(100, stunfisk.EncounterRatePercent);
    }

    [Fact]
    public void SwordsOfJusticeAreStaticCatchesWithExplicitStoryPrerequisites()
    {
        IReadOnlyList<EncounterCandidate> encounters = _catalog.FindEncounters(Black);

        EncounterCandidate cobalion = Assert.Single(encounters, encounter => encounter.SpeciesId == 638);
        EncounterCandidate virizion = Assert.Single(encounters, encounter => encounter.SpeciesId == 640);
        EncounterCandidate terrakion = Assert.Single(encounters, encounter => encounter.SpeciesId == 639);

        Assert.All([cobalion, virizion, terrakion], encounter =>
        {
            Assert.Equal(EncounterMethod.Static, encounter.Method);
            Assert.Equal((42, 42), (encounter.MinimumLevel, encounter.MaximumLevel));
            Assert.False(string.IsNullOrWhiteSpace(encounter.Requirement));
        });
        Assert.Equal("legendary-detours", cobalion.EarliestMilestone.Id);
        Assert.Equal("legendary-detours", virizion.EarliestMilestone.Id);
        Assert.Contains("Cobalion", virizion.Requirement, StringComparison.Ordinal);
        Assert.Equal("victory-road", terrakion.EarliestMilestone.Id);
    }

    [Fact]
    public void CatalogUsesBlackEncountersAndExcludesGiftsEventsAndRedundantLaterStages()
    {
        IReadOnlyList<EncounterCandidate> encounters = _catalog.FindEncounters(Black);
        int[] species = [.. encounters.Select(encounter => encounter.SpeciesId)];

        Assert.All(new[] { 574, 629, 641 }, speciesId => Assert.Contains(speciesId, species));
        Assert.All(new[] { 577, 627, 642 }, speciesId => Assert.DoesNotContain(speciesId, species));
        Assert.All(new[] { 494, 495, 498, 501, 570, 571, 636 },
            speciesId => Assert.DoesNotContain(speciesId, species));
        Assert.All(new[] { 510, 520, 523, 525, 533, 536, 575, 611 },
            speciesId => Assert.DoesNotContain(speciesId, species));

        EncounterCandidate throh = Assert.Single(encounters, encounter => encounter.SpeciesId == 538);
        Assert.Equal("pinwheel-outer", throh.EarliestMilestone.Id);
        Assert.Equal(EncounterMethod.ShakingGrass, throh.Method);
    }

    [Fact]
    public void HydreigonLineDoesNotAppearBeforeVictoryRoad()
    {
        EncounterCandidate deino = Assert.Single(
            _catalog.FindEncounters(Black),
            encounter => encounter.SpeciesId == 633);

        Assert.Equal("victory-road", deino.EarliestMilestone.Id);
        Assert.Equal(38, deino.MinimumLevel);
        Assert.Equal(40, deino.MaximumLevel);
        Assert.DoesNotContain(_catalog.FindEncounters(Black), encounter => encounter.SpeciesId == 635);
    }

    [Fact]
    public void EverySpeciesHasExactlyOneEarliestAcquisition()
    {
        IReadOnlyList<EncounterCandidate> encounters = _catalog.FindEncounters(Black);

        Assert.True(encounters.Count >= 50);
        Assert.Equal(encounters.Count, encounters.Select(encounter => encounter.SpeciesId).Distinct().Count());
        Assert.All(encounters, encounter =>
        {
            Assert.InRange(encounter.MinimumLevel, 1, 100);
            Assert.InRange(encounter.MaximumLevel, encounter.MinimumLevel, 100);
            Assert.InRange(encounter.EarliestMilestone.Order, 10, 230);
        });
    }
}
