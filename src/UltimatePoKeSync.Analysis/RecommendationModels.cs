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

public sealed record PokemonRecommendation(
    PokemonSnapshot Member,
    RecommendationProfileKind Profile,
    PokemonRoleAnalysis RoleAnalysis,
    NatureRecommendation Nature,
    EvRecommendation EffortValues,
    IReadOnlyList<MoveRecommendation> MoveCandidates,
    IReadOnlyList<ItemRecommendation> ItemCandidates,
    ReferencePreset? MatchedPreset);

public sealed record TeamRecommendation(
    TeamAnalysis TeamAnalysis,
    RecommendationProfileKind Profile,
    IReadOnlyList<PokemonRecommendation> Members);
