using System.Globalization;
using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Cli;

/// <summary>
/// Renders the M5 team analysis and the M6 recommendations underneath a party report.
/// It only formats; every fact and every ranking comes from the analysis layer.
/// </summary>
internal static class AnalysisReport
{
    public static void PrintTeamAnalysis(TeamAnalysis analysis)
    {
        Console.WriteLine("│");
        Console.WriteLine("├─ Team analysis");

        if (analysis.DefensiveGaps.Count == 0)
        {
            Console.WriteLine("│  Defensive  every attacking type meets a resistance or an immunity");
        }
        else
        {
            Console.WriteLine($"│  Defensive  {analysis.DefensiveGaps.Count} unanswered weakness(es):");

            foreach (DefensiveTypeCoverage entry in analysis.DefensiveCoverage.Where(e => e.IsGap))
            {
                string weak = string.Join(", ", entry.Matchups
                    .Where(matchup => matchup.Multiplier > 1)
                    .Select(matchup =>
                        $"{matchup.Member.SpeciesName} ×{FormatMultiplier(matchup.Multiplier)}"));

                Console.WriteLine($"│    · {entry.AttackingType,-8} {weak}");
            }
        }

        int covered = analysis.OffensiveCoverage.Count(entry => entry.IsCovered);
        Console.WriteLine(
            $"│  Offensive  {covered}/{analysis.OffensiveCoverage.Count} defending types are hit super effectively");

        if (analysis.OffensiveGaps.Count > 0)
        {
            Console.WriteLine($"│    no answer to {string.Join(", ", analysis.OffensiveGaps)}");
        }
    }

    public static void PrintTeamStrength(TeamStrength strength)
    {
        Console.WriteLine($"│  Strength   {strength.Score}/{strength.MaximumScore}");

        foreach (TeamStrengthFactor factor in strength.Factors)
        {
            Console.WriteLine(
                $"│    {factor.Points,2}/{factor.MaximumPoints,-3} {Describe(factor.Kind),-20} {factor.Explanation}");
        }
    }

    public static void PrintRecommendations(TeamRecommendation recommendation)
    {
        Console.WriteLine("│");
        Console.WriteLine($"├─ Recommendations · {recommendation.Profile} profile");

        foreach (PokemonRecommendation member in recommendation.Members)
        {
            PokemonSnapshot mon = member.Member;
            PokemonRoleAnalysis role = member.RoleAnalysis;

            Console.WriteLine("│");
            Console.WriteLine($"│  [{mon.SlotIndex}] {mon.SpeciesName}  Lv.{mon.Level}  role {role.Role}");
            Console.WriteLine(
                $"│      because  {role.PhysicalMoveCount} physical / {role.SpecialMoveCount} special / "
                + $"{role.UtilityMoveCount} utility moves · offence {role.PhysicalOffenseScore} phys vs "
                + $"{role.SpecialOffenseScore} spec · bulk {role.BulkScore.ToString("0.##", CultureInfo.InvariantCulture)}");

            PrintNature(member.Nature);
            PrintEffortValues(member.EffortValues);
            PrintBuild(member.Build);
            PrintMoves(member.MoveCandidates);
            PrintItems(member.ItemCandidates);

            if (member.MatchedPreset is not null)
            {
                Console.WriteLine($"│      Preset   reference role \"{member.MatchedPreset.Role}\"");
            }
        }
    }

    private static void PrintNature(NatureRecommendation nature)
    {
        string preferred = string.Join(" or ", nature.PreferredNatures.Select(Describe));
        string current = nature.CurrentNatureIsPreferred
            ? $"current {nature.CurrentNature.Name} already fits"
            : $"current {Describe(nature.CurrentNature)}";

        Console.WriteLine($"│      Nature   {preferred}   ({current})");
    }

    private static void PrintEffortValues(EvRecommendation effortValues)
    {
        if (effortValues.IsExactTarget && effortValues.TargetSpread is StatBlock spread)
        {
            Console.WriteLine($"│      EVs      {FormatSpread(spread)}");

            if (effortValues.ProjectedStats is StatBlock projected)
            {
                Console.WriteLine($"│               projected  {Program.Format(projected)}");
            }

            return;
        }

        string priorities = effortValues.PriorityStats.Count == 0
            ? "no priority"
            : string.Join(", ", effortValues.PriorityStats.Select(Abbreviate));

        Console.WriteLine($"│      EVs      train {priorities}   (no exact spread outside competitive play)");
    }

    /// <summary>The four moves to actually run, and why each one earned its slot.</summary>
    private static void PrintBuild(RecommendedBuild build)
    {
        if (build.Moves.Count == 0)
        {
            return;
        }

        string label = "Best set";

        for (int index = 0; index < build.Moves.Count; index++)
        {
            string reason = index < build.Reasons.Count ? build.Reasons[index] : string.Empty;
            Console.WriteLine($"│      {label,-8} · {reason}");
            label = string.Empty;
        }
    }

    private static void PrintMoves(IReadOnlyList<MoveRecommendation> moves)
    {
        if (moves.Count == 0)
        {
            Console.WriteLine("│      Moves    no candidate");
            return;
        }

        string label = "Moves   ";

        foreach (MoveRecommendation move in moves)
        {
            string origin = move.Source switch
            {
                MoveCandidateSource.CurrentMoveset => "known",
                MoveCandidateSource.LevelUpLearnset => $"level-up @{move.LearnedAtLevel}",
                _ => "reference set",
            };

            Console.WriteLine(
                $"│      {label} · {move.Move.Name,-16} {move.Move.Type,-8} {origin,-14} {Describe(move.Availability)}");
            label = "        ";
        }
    }

    private static void PrintItems(IReadOnlyList<ItemRecommendation> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        string label = "Items   ";

        foreach (ItemRecommendation item in items)
        {
            Console.WriteLine($"│      {label} · {item.Name,-16} {Describe(item.Availability)}");
            label = "        ";
        }
    }

    private static string Describe(NatureInfo nature) => nature.IsNeutral
        ? $"{nature.Name} (neutral)"
        : $"{nature.Name} (+{Abbreviate(nature.IncreasedStat!.Value)} -{Abbreviate(nature.DecreasedStat!.Value)})";

    private static string Describe(TeamStrengthKind kind) => kind switch
    {
        TeamStrengthKind.PartySize => "party size",
        TeamStrengthKind.LevelCohesion => "level cohesion",
        TeamStrengthKind.DefensiveCoverage => "defensive coverage",
        TeamStrengthKind.OffensiveCoverage => "offensive coverage",
        TeamStrengthKind.NatureFit => "nature fit",
        _ => "effort value fit",
    };

    private static string Describe(RecommendationAvailability availability) => availability switch
    {
        RecommendationAvailability.KnownAvailable => "already available",
        RecommendationAvailability.RequiresAvailabilityCheck => "check availability in this save",
        _ => "competitive reference, not a save claim",
    };

    private static string FormatSpread(StatBlock spread)
    {
        string[] parts =
        [
            .. Enum.GetValues<Stat>()
                .Where(stat => spread[stat] > 0)
                .Select(stat => $"{spread[stat]} {Abbreviate(stat)}"),
        ];

        return parts.Length == 0 ? "no EVs" : string.Join(" / ", parts);
    }

    private static string Abbreviate(Stat stat) => stat switch
    {
        Stat.Hp => "HP",
        Stat.Attack => "Atk",
        Stat.Defense => "Def",
        Stat.SpecialAttack => "SpA",
        Stat.SpecialDefense => "SpD",
        _ => "Spe",
    };

    private static string FormatMultiplier(double multiplier) =>
        multiplier.ToString("0.##", CultureInfo.InvariantCulture);
}
