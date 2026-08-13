using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

public enum RecommendationProfileKind
{
    Playthrough = 0,
    Competitive = 1,
}

public enum RecommendationAvailability
{
    KnownAvailable = 0,
    RequiresAvailabilityCheck = 1,
    CompetitiveReference = 2,

    /// <summary>
    /// A level-up move the Pokémon has not reached yet, close enough to be a plan. Nothing
    /// has to be found or bought for it, only levels walked, so "check availability" would
    /// be the wrong warning. Only used inside the horizon that stops at the evolution: past
    /// that the species follows a different learnset and the promise would be false. See
    /// D-051 and D-037.
    /// </summary>
    ArrivesWithLevelUp = 3,
}

public enum MoveCandidateSource
{
    CurrentMoveset = 0,
    LevelUpLearnset = 1,
    ReferencePreset = 2,
    Machine = 3,
    Tutor = 4,
}

public sealed record NatureRecommendation(
    IReadOnlyList<NatureInfo> PreferredNatures,
    NatureInfo CurrentNature,
    bool CurrentNatureIsPreferred);

public sealed record EvRecommendation(
    bool IsExactTarget,
    StatBlock? TargetSpread,
    IReadOnlyList<Stat> PriorityStats,
    StatBlock? ProjectedStats);

public sealed record MoveRecommendation(
    MoveReference Move,
    MoveCandidateSource Source,
    RecommendationAvailability Availability,
    int? LearnedAtLevel);

public sealed record ItemRecommendation(
    string Name,
    RecommendationAvailability Availability);

/// <summary>What a move is doing in a build. The job of the slot, in the player's terms.</summary>
public enum BuildSlotRole
{
    /// <summary>Damage of the Pokémon's own type, which it hits hardest with.</summary>
    SameType = 0,

    /// <summary>Reaches something the rest of the team cannot touch.</summary>
    Coverage = 1,

    /// <summary>Beats a type the team is weak to, so it answers a shared problem.</summary>
    TeamSupport = 2,

    /// <summary>Status, setup or recovery. A team of four attacks each is not a team.</summary>
    Utility = 3,

    /// <summary>Nothing better was available for the slot.</summary>
    Filler = 4,

    /// <summary>
    /// Deals fixed, reflected or one-hit-knockout damage without ordinary type coverage.
    /// It is an attack, but cannot honestly claim STAB or a super-effective matchup.
    /// </summary>
    DirectDamage = 5,
}

/// <summary>One move in a build, and what it is there for.</summary>
public sealed record BuildSlot(MoveRecommendation Move, BuildSlotRole Role, string Reason);

/// <summary>
/// One concrete answer to "what should this Pokémon run", drawn from the candidate pool.
/// </summary>
/// <remarks>
/// The candidate pool stays visible next to it. Picking four moves is a policy decision
/// that depends on the rest of the party, so every slot states its job and its reason, and
/// the build keeps what it turned down. See D-028 and D-031.
/// </remarks>
public sealed record RecommendedBuild(
    IReadOnlyList<BuildSlot> Slots,
    IReadOnlyList<MoveRecommendation> Alternatives)
{
    public IReadOnlyList<MoveRecommendation> Moves { get; } = [.. Slots.Select(slot => slot.Move)];
}

public sealed record PokemonRecommendation(
    PokemonSnapshot Member,
    RecommendationProfileKind Profile,
    PokemonRoleAnalysis RoleAnalysis,
    NatureRecommendation Nature,
    EvRecommendation EffortValues,
    IReadOnlyList<MoveRecommendation> MoveCandidates,
    IReadOnlyList<ItemRecommendation> ItemCandidates,
    ReferencePreset? MatchedPreset,
    RecommendedBuild Build);

public sealed record TeamRecommendation(
    TeamAnalysis TeamAnalysis,
    RecommendationProfileKind Profile,
    IReadOnlyList<PokemonRecommendation> Members);
