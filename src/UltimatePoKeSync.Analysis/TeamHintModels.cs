using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>The attributed parts of a team-hint score.</summary>
public enum TeamHintScoreKind
{
    DefensiveCoverage = 0,
    OffensiveCoverage = 1,
    TypeDiversity = 2,
    RoleDiversity = 3,
    Practicality = 4,
    Redundancy = 5,
}

/// <summary>One visible contribution to a plan's score.</summary>
public sealed record TeamHintScoreFactor(
    TeamHintScoreKind Kind,
    int Points,
    string Explanation);

/// <summary>
/// The certain end of an evolution line. This is deliberately separate from the species
/// being caught: a Watchog may be the long-term destination, but Patrat is what is waiting
/// in the grass.
/// </summary>
public sealed record TeamHintEvolutionProjection(
    int SpeciesId,
    string SpeciesName,
    PokemonType PrimaryType,
    PokemonType SecondaryType,
    string Requirements);

/// <summary>One Pokémon the player can go and obtain, and what it adds to this party.</summary>
public sealed record TeamHintCandidate(
    int SpeciesId,
    string SpeciesName,
    string Location,
    EncounterMethod Method,
    int MinimumEncounterLevel,
    int MaximumEncounterLevel,
    int RecommendedLevel,
    string Requirement,
    int? EncounterRatePercent,
    bool IsLimited,
    PokemonType PrimaryType,
    PokemonType SecondaryType,
    int EvaluatedSpeciesId,
    string EvaluatedSpeciesName,
    PokemonRole Role,
    TeamHintEvolutionProjection? ProjectedEvolution,
    IReadOnlyList<PokemonType> LevelUpAttackTypes,
    IReadOnlyList<PokemonType> CoveredTypes,
    IReadOnlyList<PokemonType> DefensiveAnswers,
    int Score,
    string Reason);

/// <summary>The current member removed by a full-party plan.</summary>
public sealed record TeamHintReplacement(
    int SlotIndex,
    int SpeciesId,
    string SpeciesName);

/// <summary>One complete, independently actionable way to improve the party.</summary>
public sealed record TeamHintPlan(
    IReadOnlyList<TeamHintCandidate> Additions,
    TeamHintReplacement? Replacement,
    IReadOnlyList<TeamHintScoreFactor> Factors,
    int Score,
    string Summary);

/// <summary>The three best distinct plans at one explicit point in the story.</summary>
public sealed record TeamHintAnalysis(
    StoryMilestone Milestone,
    int TeamAverageLevel,
    int TargetLevel,
    int AvailableCandidateCount,
    IReadOnlyList<TeamHintPlan> Plans);
