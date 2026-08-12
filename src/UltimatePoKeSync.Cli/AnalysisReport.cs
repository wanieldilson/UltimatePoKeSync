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
            // Seventeen type names on one line wrap into a mess. The chips in the UI show
            // the full set; here the first few and a count carry the point.
            IEnumerable<PokemonType> shown = analysis.OffensiveGaps.Take(6);
            string more = analysis.OffensiveGaps.Count > 6
                ? $" and {analysis.OffensiveGaps.Count - 6} more"
                : string.Empty;
            Console.WriteLine($"│    no answer to {string.Join(", ", shown)}{more}");
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

    /// <summary>
    /// What the next few levels bring, for every member that can battle. Printed before the
    /// recommendations because it is the nearer question: a best set at Lv.100 matters less
    /// than a move three levels away. See D-037.
    /// </summary>
    public static void PrintUpcoming(PartySnapshot party, PokemonProgressAnalyzer analyzer)
    {
        Console.WriteLine("│");
        Console.WriteLine("├─ Coming up");

        foreach (PokemonSnapshot mon in party.Battlers)
        {
            PokemonProgress progress = analyzer.Analyze(party.Game, mon);
            if (!progress.HasAnything)
            {
                continue;
            }

            Console.WriteLine("│");
            Console.WriteLine($"│  [{mon.SlotIndex}] {mon.SpeciesName}  Lv.{mon.Level}");

            if (progress.NextEvolution is EvolutionStep step)
            {
                string when = progress.EvolvesOnNextLevelUp
                    ? "on the next level up"
                    : step.Requirement;
                Console.WriteLine($"│      Becomes  {step.IntoSpeciesName} {when}");

                foreach (EvolutionStep other in progress.OtherEvolutions)
                {
                    Console.WriteLine($"│      or       {other.IntoSpeciesName} {other.Requirement}");
                }
            }

            foreach (UpcomingMove move in progress.Moves)
            {
                string away = move.LevelsAway == 1 ? "next level" : $"in {move.LevelsAway} levels";
                Console.WriteLine(
                    $"│      Lv.{move.Level,-3}   {move.Move.Name,-16} {move.Move.Type,-8} {away}");
            }

            if (progress.MovesStopAtEvolution && progress.NextEvolution is EvolutionStep after)
            {
                Console.WriteLine(
                    $"│      (nothing listed past that: it follows {after.IntoSpeciesName}'s learnset from then on)");
            }
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

    /// <summary>The four moves to actually run, what each is for, and why.</summary>
    private static void PrintBuild(RecommendedBuild build)
    {
        if (build.Slots.Count == 0)
        {
            return;
        }

        string label = "Best set";

        foreach (BuildSlot slot in build.Slots)
        {
            Console.WriteLine(
                $"│      {label,-8} · {slot.Move.Move.Name,-16} {Describe(slot.Role),-13} "
                + $"{DescribeSource(slot.Move),-22} {slot.Reason}");
            label = string.Empty;
        }
    }

    private static string DescribeSource(MoveRecommendation move) => move.Source switch
    {
        MoveCandidateSource.CurrentMoveset => "already knows it",
        MoveCandidateSource.LevelUpLearnset => move.LearnedAtLevel is int level
            ? $"learns it at level {level}"
            : "learns it by levelling",
        MoveCandidateSource.Machine => "from a TM or HM",
        MoveCandidateSource.Tutor => "from a move tutor",
        _ => "from a common set",
    };

    private static string Describe(BuildSlotRole role) => role switch
    {
        BuildSlotRole.SameType => "same type",
        BuildSlotRole.Coverage => "coverage",
        BuildSlotRole.TeamSupport => "team support",
        BuildSlotRole.Utility => "utility",
        _ => "filler",
    };

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
                MoveCandidateSource.Machine => "TM or HM",
                MoveCandidateSource.Tutor => "move tutor",
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
