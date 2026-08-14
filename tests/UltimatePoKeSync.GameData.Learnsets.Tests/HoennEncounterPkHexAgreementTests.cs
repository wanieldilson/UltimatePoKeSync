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

    /// <summary>Each version against its own table. Crossing them is the mistake to catch.</summary>
    public static TheoryData<string> Versions => ["Emerald", "Ruby", "Sapphire"];

    private static (GameIdentity Game, HoennEncounterCatalog Catalog, string Field) Setup(string version) =>
        version switch
        {
            "Ruby" => (Emerald with { GameCode = "AXVE", Title = "POKEMON RUBY" },
                HoennEncounterCatalog.Ruby, "SlotsR"),
            "Sapphire" => (Emerald with { GameCode = "AXPE", Title = "POKEMON SAPP" },
                HoennEncounterCatalog.Sapphire, "SlotsS"),
            _ => (Emerald, HoennEncounterCatalog.Emerald, "SlotsE"),
        };

    private static readonly EncounterMethod[] Wild =
        [EncounterMethod.Grass, EncounterMethod.Cave, EncounterMethod.Surf];

    private static readonly Dictionary<string, IReadOnlyDictionary<int, HashSet<int>>> Cache = [];

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

    [Theory]
    [MemberData(nameof(Versions))]
    public void EveryWildSuggestionIsActuallyCatchableInThatVersion(string version)
    {
        (GameIdentity game, HoennEncounterCatalog catalog, string field) = Setup(version);
        IReadOnlyDictionary<int, HashSet<int>> slots = Slots(field);

        string[] unobtainable =
        [
            .. Candidates(game, catalog)
                .Where(candidate => !slots.ContainsKey(candidate.SpeciesId))
                .Select(candidate => $"{candidate.SpeciesName} at {candidate.Location}"),
        ];

        Assert.Empty(unobtainable);
    }

    /// <summary>
    /// Both ends of every printed range must be a level the species really appears at. The
    /// levels in between are not asserted, for the reason the Unova check gives: a slot table
    /// lists discrete levels.
    /// </summary>
    [Theory]
    [MemberData(nameof(Versions))]
    public void EveryPrintedLevelBoundIsALevelTheSpeciesReallyAppearsAt(string version)
    {
        (GameIdentity game, HoennEncounterCatalog catalog, string field) = Setup(version);
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

    /// <summary>
    /// The timeline is the part no table can check, so what is asserted is the property that
    /// makes it safe to be wrong: it never runs backwards, and nothing needs more badges than
    /// the checkpoint that unlocks it.
    /// </summary>
    [Fact]
    public void TheStoryRunsForwardAndNoCheckpointPromisesMoreThanItsBadges()
    {
        IReadOnlyList<StoryMilestone> milestones =
            HoennEncounterCatalog.Emerald.FindMilestones(Emerald);

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
        StoryMilestone reached = HoennEncounterCatalog.Emerald
            .FindConservativeMilestone(Emerald, badges);

        Assert.InRange(reached.BadgeCount, 0, most);
    }

    /// <summary>
    /// Each catalog answers for its own game only, in every language, and for nothing else.
    /// Kanto is a different region entirely and belongs to no Hoenn catalog.
    /// </summary>
    [Fact]
    public void EachVersionAnswersForItsOwnGameOnly()
    {
        Assert.True(HoennEncounterCatalog.Emerald.Supports(Emerald));
        Assert.True(HoennEncounterCatalog.Emerald.Supports(Emerald with { GameCode = "BPEI" }));
        Assert.False(HoennEncounterCatalog.Emerald.Supports(Emerald with { GameCode = "AXVE" }));

        Assert.True(HoennEncounterCatalog.Ruby.Supports(Emerald with { GameCode = "AXVI" }));
        Assert.False(HoennEncounterCatalog.Ruby.Supports(Emerald with { GameCode = "AXPE" }));

        Assert.True(HoennEncounterCatalog.Sapphire.Supports(Emerald with { GameCode = "AXPI" }));
        Assert.False(HoennEncounterCatalog.Sapphire.Supports(Emerald with { GameCode = "AXVE" }));

        // FireRed is Kanto.
        Assert.All(
            new[] { HoennEncounterCatalog.Emerald, HoennEncounterCatalog.Ruby, HoennEncounterCatalog.Sapphire },
            catalog => Assert.False(catalog.Supports(Emerald with { GameCode = "BPRE" })));
    }

    /// <summary>
    /// The three tables really do differ, so one list shared between them would be wrong for
    /// two of the three. Emerald carries species the other two never had.
    /// </summary>
    [Fact]
    public void EmeraldIsNotJustRubyWithADifferentName()
    {
        int[] emerald = [.. Candidates(Emerald, HoennEncounterCatalog.Emerald).Select(c => c.SpeciesId)];
        int[] ruby =
        [
            .. Candidates(Emerald with { GameCode = "AXVE" }, HoennEncounterCatalog.Ruby)
                .Select(c => c.SpeciesId),
        ];

        Assert.NotEqual(emerald.Length, ruby.Length);
        Assert.NotEmpty(emerald.Except(ruby));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ThePkHexTableWasActuallyRead(string version)
    {
        (GameIdentity game, HoennEncounterCatalog catalog, string field) = Setup(version);

        Assert.InRange(Slots(field).Count, 80, 250);
        Assert.InRange(Candidates(game, catalog).Count, 60, 200);
    }

    private static IReadOnlyList<EncounterCandidate> Candidates(
        GameIdentity game,
        HoennEncounterCatalog catalog) =>
    [
        .. catalog.FindEncounters(game).Where(candidate => Wild.Contains(candidate.Method)),
    ];

    private static IReadOnlyDictionary<int, HashSet<int>> Load(string field)
    {
        Type holder = typeof(PersonalTable).Assembly.GetType("PKHeX.Core.Encounters3RSE")
            ?? throw new InvalidOperationException("PKHeX no longer exposes Encounters3RSE.");
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
