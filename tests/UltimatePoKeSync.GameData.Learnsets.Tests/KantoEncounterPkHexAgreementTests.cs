using System.Reflection;
using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

/// <summary>
/// The Kanto catalog against PKHeX's own FireRed and LeafGreen tables, the way D-057 checks
/// Hoenn. Only the encounters can be checked; the story order is conservative instead.
/// </summary>
public sealed class KantoEncounterPkHexAgreementTests
{
    private static readonly GameIdentity FireRed =
        new("BPRE", "POKEMON FIRE", 0, PokemonGeneration.Gen3);

    private static readonly EncounterMethod[] Wild =
        [EncounterMethod.Grass, EncounterMethod.Cave, EncounterMethod.Surf];

    private static readonly Dictionary<string, IReadOnlyDictionary<int, HashSet<int>>> Cache = [];

    public static TheoryData<string> Versions => ["FireRed", "LeafGreen"];

    private static (GameIdentity Game, KantoEncounterCatalog Catalog, string Field) Setup(string version) =>
        version == "LeafGreen"
            ? (FireRed with { GameCode = "BPGE", Title = "POKEMON LEAF" },
                KantoEncounterCatalog.LeafGreen, "SlotsLG")
            : (FireRed, KantoEncounterCatalog.FireRed, "SlotsFR");

    [Theory]
    [MemberData(nameof(Versions))]
    public void EveryWildSuggestionIsActuallyCatchableInThatVersion(string version)
    {
        (GameIdentity game, KantoEncounterCatalog catalog, string field) = Setup(version);
        IReadOnlyDictionary<int, HashSet<int>> slots = Slots(field);

        string[] unobtainable =
        [
            .. Candidates(game, catalog)
                .Where(candidate => !slots.ContainsKey(candidate.SpeciesId))
                .Select(candidate => $"{candidate.SpeciesName} at {candidate.Location}"),
        ];

        Assert.Empty(unobtainable);
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void EveryPrintedLevelBoundIsALevelTheSpeciesReallyAppearsAt(string version)
    {
        (GameIdentity game, KantoEncounterCatalog catalog, string field) = Setup(version);
        IReadOnlyDictionary<int, HashSet<int>> slots = Slots(field);

        string[] wrong =
        [
            .. Candidates(game, catalog)
                .Where(candidate => slots.ContainsKey(candidate.SpeciesId))
                .Where(candidate => !slots[candidate.SpeciesId].Contains(candidate.MinimumLevel) ||
                    !slots[candidate.SpeciesId].Contains(candidate.MaximumLevel))
                .Select(candidate =>
                    $"{candidate.SpeciesName} claims {candidate.MinimumLevel}-{candidate.MaximumLevel}"),
        ];

        Assert.Empty(wrong);
    }

    /// <summary>The property that makes it safe for the curated order to be wrong.</summary>
    [Fact]
    public void TheStoryRunsForwardAndNoCheckpointPromisesMoreThanItsBadges()
    {
        IReadOnlyList<StoryMilestone> milestones =
            KantoEncounterCatalog.FireRed.FindMilestones(FireRed);

        Assert.Equal("route-1", milestones[0].Id);
        Assert.Equal("victory-road", milestones[^1].Id);
        Assert.Equal(milestones.Count, milestones.Select(m => m.Id).Distinct().Count());
        Assert.True(milestones.Zip(milestones.Skip(1)).All(p => p.First.Order < p.Second.Order));
        Assert.True(milestones.Zip(milestones.Skip(1)).All(p => p.First.BadgeCount <= p.Second.BadgeCount));
        Assert.All(milestones, m => Assert.InRange(m.BadgeCount, 0, 8));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(8, 8)]
    public void ABadgeCountNeverUnlocksACheckpointThatNeedsMore(int badges, int most) =>
        Assert.InRange(
            KantoEncounterCatalog.FireRed.FindConservativeMilestone(FireRed, badges).BadgeCount,
            0,
            most);

    /// <summary>Kanto answers for Kanto. Hoenn is a different region and a different catalog.</summary>
    [Fact]
    public void EachVersionAnswersForItsOwnGameOnly()
    {
        Assert.True(KantoEncounterCatalog.FireRed.Supports(FireRed));
        Assert.True(KantoEncounterCatalog.FireRed.Supports(FireRed with { GameCode = "BPRI" }));
        Assert.False(KantoEncounterCatalog.FireRed.Supports(FireRed with { GameCode = "BPGE" }));

        Assert.True(KantoEncounterCatalog.LeafGreen.Supports(FireRed with { GameCode = "BPGI" }));
        Assert.False(KantoEncounterCatalog.LeafGreen.Supports(FireRed));

        Assert.All(
            new[] { KantoEncounterCatalog.FireRed, KantoEncounterCatalog.LeafGreen },
            catalog => Assert.False(catalog.Supports(FireRed with { GameCode = "BPEE" })));
    }

    /// <summary>
    /// The Sevii Islands are deliberately absent, and this is the check that they stay absent:
    /// their species must not sneak in through a mainland route. Magmar lives on Mt. Ember.
    /// </summary>
    [Fact]
    public void TheSeviiIslandsAreNotSuggested()
    {
        IReadOnlyList<EncounterCandidate> candidates =
            Candidates(FireRed, KantoEncounterCatalog.FireRed);

        Assert.DoesNotContain(candidates, c => c.Location.Contains("Island", StringComparison.Ordinal)
            && !c.Location.Contains("Cinnabar", StringComparison.Ordinal)
            && !c.Location.Contains("Seafoam", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, c => c.Location.Contains("Chamber", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, c => c.Location.Contains("Cerulean Cave", StringComparison.Ordinal));
    }

    /// <summary>The two versions differ, so one shared list would be wrong for one of them.</summary>
    [Fact]
    public void TheTwoVersionsSwapTheirExclusives()
    {
        int[] fireRed = [.. Slots("SlotsFR").Keys];
        int[] leafGreen = [.. Slots("SlotsLG").Keys];

        Assert.NotEmpty(fireRed.Except(leafGreen));
        Assert.NotEmpty(leafGreen.Except(fireRed));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ThePkHexTableWasActuallyRead(string version)
    {
        (GameIdentity game, KantoEncounterCatalog catalog, string field) = Setup(version);

        Assert.InRange(Slots(field).Count, 80, 250);
        Assert.InRange(Candidates(game, catalog).Count, 50, 200);
    }

    private static IReadOnlyList<EncounterCandidate> Candidates(
        GameIdentity game,
        KantoEncounterCatalog catalog) =>
    [
        .. catalog.FindEncounters(game).Where(candidate => Wild.Contains(candidate.Method)),
    ];

    private static IReadOnlyDictionary<int, HashSet<int>> Slots(string field)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(field, out IReadOnlyDictionary<int, HashSet<int>>? loaded))
            {
                Cache[field] = loaded = Load(field);
            }

            return loaded;
        }
    }

    private static IReadOnlyDictionary<int, HashSet<int>> Load(string field)
    {
        Type holder = typeof(PersonalTable).Assembly.GetType("PKHeX.Core.Encounters3FRLG")
            ?? throw new InvalidOperationException("PKHeX no longer exposes Encounters3FRLG.");
        var areas = (Array)holder
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        var levels = new Dictionary<int, HashSet<int>>();
        foreach (object area in areas)
        {
            foreach (object slot in (Array)area.GetType().GetProperty("Slots")!.GetValue(area)!)
            {
                Type type = slot.GetType();
                int species = Convert.ToInt32(type.GetProperty("Species")!.GetValue(slot));
                int low = Convert.ToInt32(type.GetProperty("LevelMin")!.GetValue(slot));
                int high = Convert.ToInt32(type.GetProperty("LevelMax")!.GetValue(slot));

                if (!levels.TryGetValue(species, out HashSet<int>? seen))
                {
                    levels[species] = seen = [];
                }

                for (int level = low; level <= high; level++)
                {
                    seen.Add(level);
                }
            }
        }

        return levels;
    }
}
