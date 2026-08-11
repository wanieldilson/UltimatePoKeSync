using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Analysis;

/// <summary>
/// How strong the party looks, and why.
/// </summary>
/// <remarks>
/// The score is never shown on its own: every point is attributed to a named factor with
/// the fact that produced it, so a player can act on it instead of guessing. See D-028.
/// </remarks>
public sealed record TeamStrength(IReadOnlyList<TeamStrengthFactor> Factors)
{
    public int Score { get; } = Factors.Sum(factor => factor.Points);

    public int MaximumScore { get; } = Factors.Sum(factor => factor.MaximumPoints);

    /// <summary>The factors costing the most points, worst first.</summary>
    public IReadOnlyList<TeamStrengthFactor> WeakestFactors { get; } =
    [
        .. Factors
            .Where(factor => factor.Points < factor.MaximumPoints)
            .OrderBy(factor => factor.Points - factor.MaximumPoints),
    ];
}

/// <param name="Kind">Which aspect of the party this measures.</param>
/// <param name="Points">Points earned, from 0 to <paramref name="MaximumPoints"/>.</param>
/// <param name="Explanation">The fact behind the points, in the player's terms.</param>
/// <param name="Members">The party members responsible, when the factor names any.</param>
public sealed record TeamStrengthFactor(
    TeamStrengthKind Kind,
    int Points,
    int MaximumPoints,
    string Explanation,
    IReadOnlyList<PokemonSnapshot> Members)
{
    public bool IsPerfect => Points >= MaximumPoints;
}

public enum TeamStrengthKind
{
    /// <summary>Party size: an incomplete team gives up matchups it could have covered.</summary>
    PartySize = 0,

    /// <summary>Level spread: an under-levelled member is a liability, not a member.</summary>
    LevelCohesion = 1,

    /// <summary>Attacking types the party has no resistance or immunity to.</summary>
    DefensiveCoverage = 2,

    /// <summary>Defending types no known move hits super effectively.</summary>
    OffensiveCoverage = 3,

    /// <summary>Whether each nature helps the stats that member's role actually uses.</summary>
    NatureFit = 4,

    /// <summary>Whether effort values were spent on the stats that member's role uses.</summary>
    EffortValueFit = 5,
}
