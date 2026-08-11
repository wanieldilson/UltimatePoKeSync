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

/// <summary>
/// One concrete answer to "what should this Pokémon run", drawn from the candidate pool.
/// </summary>
/// <remarks>
/// The candidate pool stays visible next to it. Picking four moves is a policy decision
/// that depends on the rest of the party, so the build states the reason for each pick and
/// keeps what it turned down. See D-028.
/// </remarks>
public sealed record RecommendedBuild(
    IReadOnlyList<MoveRecommendation> Moves,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<MoveRecommendation> Alternatives);

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
