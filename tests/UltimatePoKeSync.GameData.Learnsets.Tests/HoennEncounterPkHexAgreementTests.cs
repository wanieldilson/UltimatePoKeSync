using System.Reflection;
using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

/// <summary>
/// Checks the curated Emerald catalog against PKHeX's own Emerald table, the way D-053's
/// amendment does for Unova. It is the half of the catalog that can be checked: the story
/// order cannot, and is conservative instead. See D-057.
/// </summary>
public sealed class HoennEncounterPkHexAgreementTests
{
    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly EncounterMethod[] Wild =
        [EncounterMethod.Grass, EncounterMethod.Cave, EncounterMethod.Surf];

    private static readonly Lazy<IReadOnlyDictionary<int, HashSet<int>>> LazySlots = new(Load);

    private static IReadOnlyDictionary<int, HashSet<int>> Slots => LazySlots.Value;

    [Fact]
    public void EveryWildSuggestionIsActuallyCatchableInEmerald()
    {
        string[] unobtainable =
        [
            .. Candidates()
                .Where(candidate => !Slots.ContainsKey(candidate.SpeciesId))
                .Select(candidate => $"{candidate.SpeciesName} at {candidate.Location}"),
        ];

        Assert.Empty(unobtainable);
    }

    /// <summary>
    /// Both ends of every printed range must be a level the species really appears at. The
    /// levels in between are not asserted, for the reason the Unova check gives: a slot table
    /// lists discrete levels.
    /// </summary>
    [Fact]
    public void EveryPrintedLevelBoundIsALevelTheSpeciesReallyAppearsAt()
    {
        string[] wrong =
        [
            .. Candidates()
                .Where(candidate => Slots.ContainsKey(candidate.SpeciesId))
                .Where(candidate => !Slots[candidate.SpeciesId].Contains(candidate.MinimumLevel) ||
                    !Slots[candidate.SpeciesId].Contains(candidate.MaximumLevel))
                .Select(candidate =>
                    $"{candidate.SpeciesName} claims {candidate.MinimumLevel}-{candidate.MaximumLevel}"),
        ];

        Assert.Empty(wrong);
    }

    /// <summary>
    /// The timeline is the part no table can check, so what is asserted is the property that
    /// makes it safe to be wrong: it never runs backwards, and nothing needs more badges than
    /// the checkpoint that unlocks it.
    /// </summary>
    [Fact]
    public void TheStoryRunsForwardAndNoCheckpointPromisesMoreThanItsBadges()
    {
        IReadOnlyList<StoryMilestone> milestones =
            HoennEncounterCatalog.Instance.FindMilestones(Emerald);

        Assert.Equal("route-101", milestones[0].Id);
        Assert.Equal("victory-road", milestones[^1].Id);
        Assert.Equal(milestones.Count, milestones.Select(m => m.Id).Distinct().Count());
        Assert.True(milestones.Zip(milestones.Skip(1)).All(pair => pair.First.Order < pair.Second.Order));
        Assert.True(milestones.Zip(milestones.Skip(1)).All(pair => pair.First.BadgeCount <= pair.Second.BadgeCount));
        Assert.All(milestones, milestone => Assert.InRange(milestone.BadgeCount, 0, 8));
        Assert.All(milestones, milestone => Assert.False(string.IsNullOrWhiteSpace(milestone.ReachedWhen)));
    }

    /// <summary>Badge-only detection must never reach past the badges it was given.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(4, 4)]
    [InlineData(8, 8)]
    public void ABadgeCountNeverUnlocksACheckpointThatNeedsMore(int badges, int most)
    {
        StoryMilestone reached = HoennEncounterCatalog.Instance
            .FindConservativeMilestone(Emerald, badges);

        Assert.InRange(reached.BadgeCount, 0, most);
    }

    [Fact]
    public void RubyAndSapphireAreNotAnsweredFor()
    {
        Assert.True(HoennEncounterCatalog.Instance.Supports(Emerald));
        Assert.True(HoennEncounterCatalog.Instance.Supports(Emerald with { GameCode = "BPEI" }));
        Assert.False(HoennEncounterCatalog.Instance.Supports(Emerald with { GameCode = "AXVE" }));
        Assert.False(HoennEncounterCatalog.Instance.Supports(Emerald with { GameCode = "BPRE" }));
    }

    [Fact]
    public void ThePkHexTableWasActuallyRead()
    {
        Assert.InRange(Slots.Count, 80, 250);
        Assert.InRange(Candidates().Count, 60, 200);
    }

    private static IReadOnlyList<EncounterCandidate> Candidates() =>
    [
        .. HoennEncounterCatalog.Instance
            .FindEncounters(Emerald)
            .Where(candidate => Wild.Contains(candidate.Method)),
    ];

    private static IReadOnlyDictionary<int, HashSet<int>> Load()
    {
        Type holder = typeof(PersonalTable).Assembly.GetType("PKHeX.Core.Encounters3RSE")
            ?? throw new InvalidOperationException("PKHeX no longer exposes Encounters3RSE.");
        var areas = (Array)holder
            .GetField("SlotsE", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!
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
