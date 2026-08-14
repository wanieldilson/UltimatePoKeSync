using System.Reflection;
using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

/// <summary>
/// Checks the curated Black catalog against PKHeX's own Black encounter table.
/// </summary>
/// <remarks>
/// <para>
/// The rest of the catalog's tests assert the numbers the catalog itself declares, which
/// proves the shape of the data and nothing about the game. This is the check that fails if
/// a hand-entered species or level is wrong: the promise the feature makes is that the
/// player can go and catch the thing it names, and only the game's own table can settle that.
/// See D-053.
/// </para>
/// <para>
/// <c>Encounters5BW.SlotsB</c> is reached by reflection because PKHeX does not expose the
/// loaded areas publicly; the alternative is re-reading its packed resource, which would be
/// a second copy of PKHeX's own loader. A PKHeX upgrade that renames the field breaks this
/// test loudly, which is the intent: the same reason D-014 pins its type indices.
/// </para>
/// </remarks>
public sealed class BlackEncounterPkHexAgreementTests
{
    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    /// <summary>The methods that mean "it is out there in the world", as opposed to a
    /// fossil, a trade, a roamer or a one-off static that PKHeX files elsewhere.</summary>
    private static readonly EncounterMethod[] Wild =
    [
        EncounterMethod.Grass,
        EncounterMethod.DarkGrass,
        EncounterMethod.ShakingGrass,
        EncounterMethod.Cave,
        EncounterMethod.DustCloud,
        EncounterMethod.Surf,
        EncounterMethod.RipplingWater,
        EncounterMethod.Fishing,
        EncounterMethod.BridgeShadow,
    ];

    private static readonly Lazy<IReadOnlyDictionary<int, HashSet<int>>> LazyBlackSlots =
        new(LoadBlackSlots);

    private static IReadOnlyDictionary<int, HashSet<int>> BlackSlots => LazyBlackSlots.Value;

    [Fact]
    public void EveryWildSuggestionIsActuallyCatchableInBlack()
    {
        string[] unobtainable =
        [
            .. WildCandidates()
                .Where(candidate => !BlackSlots.ContainsKey(candidate.SpeciesId))
                .Select(candidate =>
                    $"{candidate.SpeciesName} ({candidate.SpeciesId}) at {candidate.Location}"),
        ];

        Assert.Empty(unobtainable);
    }

    /// <summary>
    /// The bounds are bounds: a species must really appear at both ends of the range the
    /// catalog prints. Levels in between are not asserted, because a Black grass slot lists
    /// discrete levels and a species can appear at 8 and at 10 without ever being 9.
    /// </summary>
    [Fact]
    public void EveryPrintedLevelBoundIsALevelTheSpeciesReallyAppearsAt()
    {
        string[] wrong =
        [
            .. WildCandidates()
                .Where(candidate => BlackSlots.ContainsKey(candidate.SpeciesId))
                .Select(candidate => new
                {
                    candidate.SpeciesName,
                    candidate.MinimumLevel,
                    candidate.MaximumLevel,
                    Levels = BlackSlots[candidate.SpeciesId],
                })
                .Where(entry => !entry.Levels.Contains(entry.MinimumLevel) ||
                    !entry.Levels.Contains(entry.MaximumLevel))
                .Select(entry =>
                    $"{entry.SpeciesName} claims {entry.MinimumLevel}-{entry.MaximumLevel}, " +
                    $"really {entry.Levels.Min()}-{entry.Levels.Max()}"),
        ];

        Assert.Empty(wrong);
    }

    /// <summary>
    /// A White-only species reaching a Black plan would be the worst failure this feature
    /// has: a player sent to a patch of grass that can never hold it. Pinned by name for the
    /// pairs Black and White actually split.
    /// </summary>
    [Theory]
    [InlineData(577, "Solosis")]
    [InlineData(627, "Rufflet")]
    [InlineData(642, "Thundurus")]
    [InlineData(635, "Hydreigon")]
    public void WhiteOnlyAndUnreachableSpeciesAreAbsentFromBothTheTableAndTheCatalog(
        int speciesId,
        string name)
    {
        Assert.False(BlackSlots.ContainsKey(speciesId), $"{name} is in Black's wild table");
        Assert.DoesNotContain(
            BlackEncounterCatalog.Instance.FindEncounters(Black),
            candidate => candidate.SpeciesId == speciesId);
    }

    /// <summary>Guards the reflection itself: an empty table would pass every test above.</summary>
    [Fact]
    public void ThePkHexTableWasActuallyRead()
    {
        Assert.InRange(BlackSlots.Count, 150, 400);
        Assert.NotEmpty(WildCandidates());
    }

    private static IReadOnlyList<EncounterCandidate> WildCandidates() =>
    [
        .. BlackEncounterCatalog.Instance
            .FindEncounters(Black)
            .Where(candidate => Wild.Contains(candidate.Method)),
    ];

    private static IReadOnlyDictionary<int, HashSet<int>> LoadBlackSlots()
    {
        FieldInfo field = typeof(Encounters5BW).GetField(
            "SlotsB",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "PKHeX no longer exposes Encounters5BW.SlotsB; this check needs updating.");

        var areas = (EncounterArea5[])field.GetValue(null)!;
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
