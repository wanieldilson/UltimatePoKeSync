using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>
/// Turns the coverage facts and the per-member roles into one attributed score.
/// </summary>
/// <remarks>
/// Every factor carries the fact that produced it. A player who sees 62/100 must be able
/// to see, in the same glance, which member is dragging it down and why. See D-028.
/// </remarks>
public sealed class TeamStrengthAnalyzer
{
    private const int FullParty = 6;

    /// <summary>Level spread tolerated before the party counts as uneven.</summary>
    private const int ToleratedLevelSpread = 3;

    /// <summary>Effort values below this are treated as untrained rather than misplaced.</summary>
    private const int UntrainedEffortValues = 12;

    private readonly PokemonRoleAnalyzer _roleAnalyzer;
    private readonly IGenerationRulesResolver _rulesResolver;

    public TeamStrengthAnalyzer()
        : this(new PokemonRoleAnalyzer(), GenerationRulesResolver.Default)
    {
    }

    public TeamStrengthAnalyzer(
        PokemonRoleAnalyzer roleAnalyzer,
        IGenerationRulesResolver rulesResolver)
    {
        ArgumentNullException.ThrowIfNull(roleAnalyzer);
        ArgumentNullException.ThrowIfNull(rulesResolver);

        _roleAnalyzer = roleAnalyzer;
        _rulesResolver = rulesResolver;
    }

    public TeamStrength Evaluate(TeamAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        PartySnapshot party = analysis.Party;
        IGenerationRules rules = _rulesResolver.Resolve(party.Game.Generation)
            ?? throw new NotSupportedException(
                $"No analysis rules are available for {party.Game.Generation}.");

        PokemonRoleAnalysis[] roles =
        [
            .. party.Members.Select(member => _roleAnalyzer.Analyze(member, party.Game.Generation)),
        ];

        return new TeamStrength(
        [
            EvaluatePartySize(party),
            EvaluateLevelCohesion(party),
            EvaluateDefensiveCoverage(analysis),
            EvaluateOffensiveCoverage(analysis),
            EvaluateNatureFit(roles, rules),
            EvaluateEffortValueFit(roles),
        ]);
    }

    private static TeamStrengthFactor EvaluatePartySize(PartySnapshot party)
    {
        const int maximum = 15;
        int points = Math.Min(party.Count, FullParty) * maximum / FullParty;

        return new TeamStrengthFactor(
            TeamStrengthKind.PartySize,
            points,
            maximum,
            party.Count >= FullParty
                ? "A full party of six."
                : $"{party.Count} of {FullParty} slots filled.",
            []);
    }

    private static TeamStrengthFactor EvaluateLevelCohesion(PartySnapshot party)
    {
        const int maximum = 15;
        if (party.Count < 2)
        {
            return new TeamStrengthFactor(
                TeamStrengthKind.LevelCohesion,
                maximum,
                maximum,
                "Nothing to compare with a single Pokémon.",
                []);
        }

        int highest = party.Members.Max(member => member.Level);
        int lowest = party.Members.Min(member => member.Level);
        int spread = highest - lowest;
        int excess = Math.Max(0, spread - ToleratedLevelSpread);
        int points = Math.Max(0, maximum - (excess * 2));

        PokemonSnapshot[] laggards =
        [
            .. party.Members
                .Where(member => member.Level < highest - ToleratedLevelSpread)
                .OrderBy(member => member.Level),
        ];

        return new TeamStrengthFactor(
            TeamStrengthKind.LevelCohesion,
            points,
            maximum,
            laggards.Length == 0
                ? $"Levels {lowest} to {highest}: the party is even."
                : $"Levels {lowest} to {highest}: {Names(laggards)} will be outsped and knocked out first.",
            laggards);
    }

    private static TeamStrengthFactor EvaluateDefensiveCoverage(TeamAnalysis analysis)
    {
        const int maximum = 25;
        const int penaltyPerGap = 5;
        IReadOnlyList<PokemonType> gaps = analysis.DefensiveGaps;
        int points = Math.Max(0, maximum - (gaps.Count * penaltyPerGap));

        PokemonSnapshot[] exposed =
        [
            .. analysis.DefensiveCoverage
                .Where(entry => entry.IsGap)
                .SelectMany(entry => entry.Matchups
                    .Where(matchup => matchup.Multiplier > 1)
                    .Select(matchup => matchup.Member))
                .DistinctBy(member => member.SlotIndex),
        ];

        return new TeamStrengthFactor(
            TeamStrengthKind.DefensiveCoverage,
            points,
            maximum,
            gaps.Count == 0
                ? "Every attacking type meets a resistance or an immunity."
                : $"No switch-in resists {Summarise(gaps)}.",
            exposed);
    }

    private static TeamStrengthFactor EvaluateOffensiveCoverage(TeamAnalysis analysis)
    {
        const int maximum = 25;
        int total = analysis.OffensiveCoverage.Count;
        int covered = total - analysis.OffensiveGaps.Count;
        int points = total == 0 ? maximum : covered * maximum / total;

        return new TeamStrengthFactor(
            TeamStrengthKind.OffensiveCoverage,
            points,
            maximum,
            analysis.OffensiveGaps.Count == 0
                ? $"Known moves hit all {total} types super effectively."
                : $"{covered} of {total} types hit super effectively; nothing answers "
                    + Summarise(analysis.OffensiveGaps),
            []);
    }

    private static TeamStrengthFactor EvaluateNatureFit(
        IReadOnlyList<PokemonRoleAnalysis> roles,
        IGenerationRules rules)
    {
        const int maximum = 10;
        if (roles.Count == 0)
        {
            return new TeamStrengthFactor(
                TeamStrengthKind.NatureFit,
                0,
                maximum,
                "No Pokémon to judge.",
                []);
        }

        var harmed = new List<PokemonSnapshot>();
        double earned = 0;

        foreach (PokemonRoleAnalysis role in roles)
        {
            IReadOnlyList<Stat> priorities =
                RecommendationPolicy.RecommendPlaythroughEvs(role.Role).PriorityStats;
            NatureInfo nature = rules.GetNature(role.Member.NatureId);

            bool helps = nature.IncreasedStat is Stat raised && priorities.Contains(raised);
            bool hurts = nature.DecreasedStat is Stat lowered && priorities.Contains(lowered);

            if (hurts)
            {
                harmed.Add(role.Member);
                continue;
            }

            earned += helps ? 1 : 0.5;
        }

        return new TeamStrengthFactor(
            TeamStrengthKind.NatureFit,
            (int)Math.Round(earned / roles.Count * maximum),
            maximum,
            harmed.Count == 0
                ? "No nature works against its Pokémon's role."
                : $"{Names(harmed)} {(harmed.Count == 1 ? "has a nature that lowers" : "have natures that lower")} a stat their role depends on.",
            harmed);
    }

    private static TeamStrengthFactor EvaluateEffortValueFit(IReadOnlyList<PokemonRoleAnalysis> roles)
    {
        const int maximum = 10;

        // Untrained Pokémon score full: this factor measures effort values spent on the
        // wrong stats, not effort values not yet spent. Story play rarely trains at all,
        // and reporting that as a weakness would be noise rather than advice.
        var misplaced = new List<PokemonSnapshot>();

        foreach (PokemonRoleAnalysis role in roles)
        {
            PokemonSnapshot member = role.Member;
            if (member.TotalEffortValues < UntrainedEffortValues)
            {
                continue;
            }

            IReadOnlyList<Stat> priorities =
                RecommendationPolicy.RecommendPlaythroughEvs(role.Role).PriorityStats;
            int useful = priorities.Sum(stat => member.EffortValues[stat]);

            if (useful * 2 < member.TotalEffortValues)
            {
                misplaced.Add(member);
            }
        }

        int trained = roles.Count(role => role.Member.TotalEffortValues >= UntrainedEffortValues);

        return new TeamStrengthFactor(
            TeamStrengthKind.EffortValueFit,
            misplaced.Count == 0 ? maximum : Math.Max(0, maximum - (misplaced.Count * 4)),
            maximum,
            trained == 0
                ? "No effort values invested yet, so none are wasted."
                : misplaced.Count == 0
                    ? "Effort values match the inferred roles."
                    : $"{Names(misplaced)} spent most effort values outside the stats their role uses.",
            misplaced);
    }

    private static string Names(IReadOnlyList<PokemonSnapshot> members) =>
        string.Join(", ", members.Select(member => member.SpeciesName));

    /// <summary>
    /// Names the first few types and counts the rest. A party with no offensive moves at
    /// all is missing seventeen of them, and spelling every one out turns an explanation
    /// into a wall that pushes the rest of the panel around.
    /// </summary>
    private static string Summarise(IReadOnlyList<PokemonType> types, int shown = 4)
    {
        if (types.Count <= shown)
        {
            return string.Join(", ", types);
        }

        return string.Join(", ", types.Take(shown)) + $" and {types.Count - shown} more";
    }
}
