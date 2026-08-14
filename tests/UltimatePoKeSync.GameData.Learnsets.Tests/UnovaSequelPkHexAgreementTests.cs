using System.Reflection;
using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

/// <summary>
/// Black 2 and White 2 against PKHeX's own tables. The encounters are checkable; the
/// checkpoints are badges, which is the whole point of D-060.
/// </summary>
public sealed class UnovaSequelPkHexAgreementTests
{
    private static readonly GameIdentity BlackTwo =
        new("IREO", "POKEMON B2", 0, PokemonGeneration.Gen5);

    private static readonly EncounterMethod[] Wild =
    [
        EncounterMethod.Grass, EncounterMethod.Cave,
        EncounterMethod.Surf, EncounterMethod.ShakingGrass,
    ];

    private static readonly Dictionary<string, IReadOnlyDictionary<int, HashSet<int>>> Cache = [];

    public static TheoryData<string> Versions => ["BlackTwo", "WhiteTwo"];

    private static (GameIdentity Game, UnovaSequelEncounterCatalog Catalog, string Field) Setup(
        string version) =>
        version == "WhiteTwo"
            ? (BlackTwo with { GameCode = "IRDO", Title = "POKEMON W2" },
                UnovaSequelEncounterCatalog.WhiteTwo, "SlotsW2")
            : (BlackTwo, UnovaSequelEncounterCatalog.BlackTwo, "SlotsB2");

    [Theory]
    [MemberData(nameof(Versions))]
    public void EveryWildSuggestionIsActuallyCatchableInThatVersion(string version)
    {
        (GameIdentity game, UnovaSequelEncounterCatalog catalog, string field) = Setup(version);
        IReadOnlyDictionary<int, HashSet<int>> slots = Slots(field);

        Assert.Empty(Candidates(game, catalog)
            .Where(candidate => !slots.ContainsKey(candidate.SpeciesId))
            .Select(candidate => $"{candidate.SpeciesName} at {candidate.Location}"));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void EveryPrintedLevelBoundIsALevelTheSpeciesReallyAppearsAt(string version)
    {
        (GameIdentity game, UnovaSequelEncounterCatalog catalog, string field) = Setup(version);
        IReadOnlyDictionary<int, HashSet<int>> slots = Slots(field);

        Assert.Empty(Candidates(game, catalog)
            .Where(candidate => slots.ContainsKey(candidate.SpeciesId))
            .Where(candidate => !slots[candidate.SpeciesId].Contains(candidate.MinimumLevel) ||
                !slots[candidate.SpeciesId].Contains(candidate.MaximumLevel))
            .Select(candidate =>
                $"{candidate.SpeciesName} claims {candidate.MinimumLevel}-{candidate.MaximumLevel}"));
    }

    /// <summary>
    /// Nine checkpoints, one per Gym, and nothing finer. This is the deliberate difference
    /// from every other catalog and it should not drift back into route detail by accident.
    /// </summary>
    [Fact]
    public void TheCheckpointsAreTheEightBadgesAndTheStart()
    {
        IReadOnlyList<StoryMilestone> milestones =
            UnovaSequelEncounterCatalog.BlackTwo.FindMilestones(BlackTwo);

        Assert.Equal(9, milestones.Count);
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8], milestones.Select(m => m.BadgeCount));
        Assert.True(milestones.Zip(milestones.Skip(1)).All(p => p.First.Order < p.Second.Order));
        Assert.All(milestones, m => Assert.False(string.IsNullOrWhiteSpace(m.ReachedWhen)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public void ABadgeCountReachesExactlyItsOwnCheckpoint(int badges) =>
        Assert.Equal(
            badges,
            UnovaSequelEncounterCatalog.BlackTwo
                .FindConservativeMilestone(BlackTwo, badges).BadgeCount);

    [Fact]
    public void EachVersionAnswersForItsOwnGameOnly()
    {
        Assert.True(UnovaSequelEncounterCatalog.BlackTwo.Supports(BlackTwo));
        Assert.True(UnovaSequelEncounterCatalog.BlackTwo.Supports(BlackTwo with { GameCode = "IREI" }));
        Assert.False(UnovaSequelEncounterCatalog.BlackTwo.Supports(BlackTwo with { GameCode = "IRDO" }));

        Assert.True(UnovaSequelEncounterCatalog.WhiteTwo.Supports(BlackTwo with { GameCode = "IRDI" }));

        // The originals are a different catalog with a different timeline.
        Assert.All(
            new[] { UnovaSequelEncounterCatalog.BlackTwo, UnovaSequelEncounterCatalog.WhiteTwo },
            catalog => Assert.False(catalog.Supports(BlackTwo with { GameCode = "IRBO" })));
    }

    /// <summary>Post-game and timer-driven places must stay out.</summary>
    [Fact]
    public void ThePostGameAndTheHiddenGrottoAreNotSuggested()
    {
        IReadOnlyList<EncounterCandidate> candidates =
            Candidates(BlackTwo, UnovaSequelEncounterCatalog.BlackTwo);

        Assert.All(
            new[] { "Hidden Grotto", "Nature Preserve", "P2 Laboratory", "Marvelous Bridge", "Chamber" },
            name => Assert.DoesNotContain(
                candidates,
                candidate => candidate.Location.Contains(name, StringComparison.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(Versions))]
    public void ThePkHexTableWasActuallyRead(string version)
    {
        (GameIdentity game, UnovaSequelEncounterCatalog catalog, string field) = Setup(version);

        Assert.InRange(Slots(field).Count, 200, 400);
        Assert.InRange(Candidates(game, catalog).Count, 100, 300);
    }

    private static IReadOnlyList<EncounterCandidate> Candidates(
        GameIdentity game,
        UnovaSequelEncounterCatalog catalog) =>
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
        Type holder = typeof(PersonalTable).Assembly.GetType("PKHeX.Core.Encounters5B2W2")
            ?? throw new InvalidOperationException("PKHeX no longer exposes Encounters5B2W2.");
        var areas = (EncounterArea5[])holder
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

        var levels = new Dictionary<int, HashSet<int>>();
        foreach (EncounterSlot5 slot in areas.SelectMany(area => area.Slots))
        {
            if (!levels.TryGetValue(slot.Species, out HashSet<int>? seen))
            {
                levels[slot.Species] = seen = [];
            }

            for (int level = slot.LevelMin; level <= slot.LevelMax; level++)
            {
                seen.Add(level);
            }
        }

        return levels;
    }
}
