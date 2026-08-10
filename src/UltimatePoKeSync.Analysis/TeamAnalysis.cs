using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>
/// Generation-correct facts about one party. It contains no recommendation-profile
/// decisions; playthrough and competitive heuristics consume these facts later.
/// </summary>
public sealed record TeamAnalysis(
    PartySnapshot Party,
    IReadOnlyList<DefensiveTypeCoverage> DefensiveCoverage,
    IReadOnlyList<OffensiveTypeCoverage> OffensiveCoverage)
{
    /// <summary>Weak attacking types for which the party has no resistant switch-in.</summary>
    public IReadOnlyList<PokemonType> DefensiveGaps { get; } =
        [.. DefensiveCoverage.Where(entry => entry.IsGap).Select(entry => entry.AttackingType)];

    /// <summary>Defending types no known damaging move can hit super effectively.</summary>
    public IReadOnlyList<PokemonType> OffensiveGaps { get; } =
        [.. OffensiveCoverage.Where(entry => !entry.IsCovered).Select(entry => entry.DefendingType)];
}

/// <summary>How one attacking type fares against every current party member.</summary>
public sealed record DefensiveTypeCoverage(
    PokemonType AttackingType,
    IReadOnlyList<DefensiveMatchup> Matchups)
{
    public int WeakCount => Matchups.Count(matchup => matchup.Multiplier > 1);

    public int NeutralCount => Matchups.Count(matchup => matchup.Multiplier == 1);

    public int ResistantCount => Matchups.Count(
        matchup => matchup.Multiplier is > 0 and < 1);

    public int ImmuneCount => Matchups.Count(matchup => matchup.Multiplier == 0);

    public bool HasDefensiveAnswer => ResistantCount > 0 || ImmuneCount > 0;

    /// <summary>
    /// At least one member is weak, and no member resists or is immune to the type.
    /// </summary>
    public bool IsGap => WeakCount > 0 && !HasDefensiveAnswer;
}

public sealed record DefensiveMatchup(PokemonSnapshot Member, double Multiplier);

/// <summary>Known damaging moves that hit one defending type super effectively.</summary>
public sealed record OffensiveTypeCoverage(
    PokemonType DefendingType,
    IReadOnlyList<OffensiveAnswer> Answers)
{
    public bool IsCovered => Answers.Count > 0;
}

public sealed record OffensiveAnswer(
    PokemonSnapshot Member,
    MoveSlot Move,
    MoveCategory Category,
    double Multiplier);
