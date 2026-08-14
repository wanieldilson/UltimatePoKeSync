using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// One stable point in a game's story timeline. Encounter availability is keyed to this
/// rather than inferred from party level: a trained starter does not prove that a route is
/// reachable, and a low-level catch does not mean the player is still near the beginning.
/// </summary>
public sealed record StoryMilestone(
    string Id,
    string Name,
    int Order,
    int BadgeCount,
    string ReachedWhen,
    int? GuaranteedWhenBadgesAtLeast = null)
{
    public override string ToString() => Name;
}

/// <summary>How the suggested Pokémon can be obtained without receiving it as a gift.</summary>
public enum EncounterMethod
{
    Grass = 0,
    DarkGrass = 1,
    ShakingGrass = 2,
    Cave = 3,
    DustCloud = 4,
    Surf = 5,
    RipplingWater = 6,
    Fishing = 7,
    Static = 8,
    Fossil = 9,
    InGameTrade = 10,
    BridgeShadow = 11,
    Roaming = 12,
}

/// <summary>
/// A species the player can acquire at a known point in one game. The species is the form
/// actually acquired; an analyzer may describe a later evolution, but must not pretend the
/// final form itself is waiting in the grass.
/// </summary>
/// <param name="ExclusiveGroup">
/// Optional id shared by alternatives of one irreversible choice. A plan may contain at
/// most one candidate from the same group; the two Relic Castle fossils are the motivating
/// example.
/// </param>
public sealed record EncounterCandidate(
    int SpeciesId,
    string SpeciesName,
    StoryMilestone EarliestMilestone,
    string Location,
    EncounterMethod Method,
    int MinimumLevel,
    int MaximumLevel,
    string Requirement = "",
    int? EncounterRatePercent = null,
    string? ExclusiveGroup = null,
    bool AvailabilityIsConditional = false)
{
    public bool IsLimited => Method is EncounterMethod.Static
        or EncounterMethod.Fossil
        or EncounterMethod.InGameTrade
        or EncounterMethod.Roaming;
}

/// <summary>
/// Offline, per-game acquisition facts used by team hints. Gift Pokémon are deliberately
/// outside this contract: the feature promises suggestions the player can go and obtain,
/// and the product policy explicitly excludes gifts.
/// </summary>
public interface IEncounterCatalog
{
    string SourceName { get; }

    bool Supports(GameIdentity game);

    IReadOnlyList<StoryMilestone> FindMilestones(GameIdentity game);

    /// <summary>
    /// One earliest acquisition for every species covered by the catalog. Callers decide
    /// which milestones are already reached and whether to show the next one separately.
    /// </summary>
    IReadOnlyList<EncounterCandidate> FindEncounters(GameIdentity game);
}
