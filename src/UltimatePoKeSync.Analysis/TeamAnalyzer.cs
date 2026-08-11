using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>Computes aggregate type facts from an immutable party snapshot.</summary>
public sealed class TeamAnalyzer
{
    private readonly IGenerationRulesResolver _rulesResolver;

    public TeamAnalyzer()
        : this(GenerationRulesResolver.Default)
    {
    }

    public TeamAnalyzer(IGenerationRulesResolver rulesResolver)
    {
        ArgumentNullException.ThrowIfNull(rulesResolver);
        _rulesResolver = rulesResolver;
    }

    public TeamAnalysis Analyze(PartySnapshot party)
    {
        ArgumentNullException.ThrowIfNull(party);

        IGenerationRules rules = _rulesResolver.Resolve(party.Game.Generation)
            ?? throw new NotSupportedException(
                $"No analysis rules are available for {party.Game.Generation}.");

        var defensiveCoverage = new List<DefensiveTypeCoverage>(rules.TypeChart.Types.Count);
        var offensiveCoverage = new List<OffensiveTypeCoverage>(rules.TypeChart.Types.Count);

        foreach (PokemonType attackingType in rules.TypeChart.Types)
        {
            DefensiveMatchup[] matchups = [.. party.Battlers.Select(member =>
                new DefensiveMatchup(
                    member,
                    rules.GetDefensiveMultiplier(
                        attackingType,
                        member.PrimaryType,
                        member.SecondaryType,
                        member.AbilityId)))];

            defensiveCoverage.Add(new DefensiveTypeCoverage(attackingType, matchups));
        }

        foreach (PokemonType defendingType in rules.TypeChart.Types)
        {
            var answers = new List<OffensiveAnswer>();

            foreach (PokemonSnapshot member in party.Battlers)
            {
                foreach (MoveSlot move in member.Moves)
                {
                    MoveCategory category = rules.GetMoveCategory(move.MoveId, move.Type);
                    if (category == MoveCategory.Status ||
                        !rules.CanProvideSuperEffectiveCoverage(move.MoveId))
                    {
                        continue;
                    }

                    double multiplier = rules.TypeChart.GetMultiplier(move.Type, defendingType);
                    if (multiplier > 1)
                    {
                        answers.Add(new OffensiveAnswer(member, move, category, multiplier));
                    }
                }
            }

            offensiveCoverage.Add(new OffensiveTypeCoverage(defendingType, answers));
        }

        return new TeamAnalysis(party, defensiveCoverage, offensiveCoverage);
    }
}
