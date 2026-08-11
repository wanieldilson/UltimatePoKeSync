using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

internal static class RecommendationPolicy
{
    public static ReferencePreset? MatchPreset(RecommendationContext context)
    {
        IReadOnlyList<ReferencePreset> presets =
            context.PresetCatalog.Find(context.RoleAnalysis.Member.SpeciesName);
        if (presets.Count == 0)
        {
            return null;
        }

        HashSet<string> currentMoves =
        [
            .. context.RoleAnalysis.Member.Moves
                .Select(move => context.MoveCatalog.Find(move.Name)?.ReferenceId)
                .OfType<string>(),
        ];

        return presets
            .Select((preset, index) => new
            {
                Preset = preset,
                Index = index,
                Score = ScorePreset(preset, context.RoleAnalysis, currentMoves),
            })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Index)
            .First()
            .Preset;
    }

    /// <summary>Picks the four moves to actually run, and says what each one is for.</summary>
    /// <remarks>
    /// Slots are filled one at a time, and every pick is judged against the slots already
    /// filled and against what the rest of the team already answers. Ranking the pool in
    /// isolation gives one Pokémon three moves of the same type, and gives six Pokémon the
    /// same coverage move six times. See D-031.
    /// </remarks>
    public static RecommendedBuild SelectBuild(
        RecommendationContext context,
        IReadOnlyList<MoveRecommendation> candidates)
    {
        const int moveSlots = 4;

        // Four attacks and no status move is not what a real set looks like, and it leaves
        // a Pokémon with no answer to anything it cannot simply out-damage.
        const int maximumAttacks = 3;

        if (candidates.Count == 0)
        {
            return new RecommendedBuild([], []);
        }

        HashSet<string> presetMoves = [.. MatchPreset(context)?.MovePool ?? []];

        List<(MoveRecommendation Candidate, int Index)> remaining =
            [.. candidates.Select((candidate, index) => (candidate, index))];
        var slots = new List<BuildSlot>(moveSlots);
        var coveredTypes = new HashSet<PokemonType>();
        int attacks = 0;

        // Starts from what the rest of the team answers and grows as slots fill, so the
        // second pick cannot claim a hole the first pick just closed.
        var answered = new HashSet<PokemonType>(context.AnsweredTypes);

        while (slots.Count < moveSlots && remaining.Count > 0)
        {
            bool utilityOnly = attacks >= maximumAttacks &&
                remaining.Any(entry => !IsDamaging(context, entry.Candidate.Move));

            var best = remaining
                .Where(entry => !utilityOnly || !IsDamaging(context, entry.Candidate.Move))
                .Select(entry => new
                {
                    entry.Candidate,
                    entry.Index,
                    Score = ScoreMove(context, entry.Candidate, presetMoves, coveredTypes, answered),
                })
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Index)
                .First();

            BuildSlotRole role = ClassifySlot(context, best.Candidate, coveredTypes, answered);
            slots.Add(new BuildSlot(
                best.Candidate,
                role,
                Explain(context, best.Candidate, role, presetMoves, answered)));

            if (IsDamaging(context, best.Candidate.Move))
            {
                coveredTypes.Add(best.Candidate.Move.Type);
                answered.UnionWith(TypesHitHard(context, best.Candidate.Move.Type));
                attacks++;
            }

            remaining.RemoveAll(entry => entry.Index == best.Index);
        }

        return new RecommendedBuild(
            slots,
            [
                .. remaining
                    .OrderByDescending(entry =>
                        ScoreMove(context, entry.Candidate, presetMoves, coveredTypes, answered))
                    .ThenBy(entry => entry.Index)
                    .Select(entry => entry.Candidate),
            ]);
    }

    private static int ScoreMove(
        RecommendationContext context,
        MoveRecommendation candidate,
        IReadOnlySet<string> presetMoves,
        IReadOnlySet<PokemonType> coveredTypes,
        IReadOnlySet<PokemonType> answered)
    {
        PokemonSnapshot member = context.RoleAnalysis.Member;
        MoveReference move = candidate.Move;
        MoveCategory category = context.Rules.GetMoveCategory(move.MoveId, move.Type);
        bool damaging = IsDamaging(context, move);
        int score = 0;

        if (damaging)
        {
            if (coveredTypes.Contains(move.Type))
            {
                // Repeating a type the build already hits costs more than the same-type
                // bonus is worth, so a duplicate has to win on other merits.
                score -= 7;
            }

            if (move.Type == member.PrimaryType || move.Type == member.SecondaryType)
            {
                score += 6;
            }

            if (MatchesRoleCategory(context.RoleAnalysis.Role, category))
            {
                score += 5;
            }

            if (ClosesTeamGap(context, move.Type, answered))
            {
                score += 4;
            }

            if (AnswersTeamWeakness(context, move.Type))
            {
                score += 3;
            }
        }
        else
        {
            // Utility is worth something to everyone, and worth more to a Pokémon whose
            // bulk is the reason it is on the team at all.
            score += WantsUtility(context.RoleAnalysis.Role) ? 4 : 2;
        }

        if (candidate.Source == MoveCandidateSource.CurrentMoveset)
        {
            score += 3;
        }

        if (presetMoves.Contains(move.ReferenceId))
        {
            score += 2;
        }

        return score;
    }

    private static BuildSlotRole ClassifySlot(
        RecommendationContext context,
        MoveRecommendation candidate,
        IReadOnlySet<PokemonType> coveredTypes,
        IReadOnlySet<PokemonType> answered)
    {
        PokemonSnapshot member = context.RoleAnalysis.Member;
        MoveReference move = candidate.Move;

        if (!IsDamaging(context, move))
        {
            return BuildSlotRole.Utility;
        }

        // Only the first move of a type may claim the gap it closes; the ones after it are
        // no longer closing anything the build has not already closed.
        if (!coveredTypes.Contains(move.Type))
        {
            if (ClosesTeamGap(context, move.Type, answered))
            {
                return BuildSlotRole.Coverage;
            }

            if (AnswersTeamWeakness(context, move.Type))
            {
                return BuildSlotRole.TeamSupport;
            }
        }

        if (!coveredTypes.Contains(move.Type) &&
            (move.Type == member.PrimaryType || move.Type == member.SecondaryType))
        {
            return BuildSlotRole.SameType;
        }

        return BuildSlotRole.Filler;
    }

    private static string Explain(
        RecommendationContext context,
        MoveRecommendation candidate,
        BuildSlotRole role,
        IReadOnlySet<string> presetMoves,
        IReadOnlySet<PokemonType> answered)
    {
        PokemonSnapshot member = context.RoleAnalysis.Member;
        MoveReference move = candidate.Move;

        return role switch
        {
            BuildSlotRole.Coverage =>
                $"nothing else on the team hits {DescribeTargets(context, move.Type, answered)} hard",
            BuildSlotRole.TeamSupport =>
                $"beats {DescribeThreats(context, move.Type)}, which the team is weak to",
            BuildSlotRole.SameType =>
                $"{member.SpeciesName} is {move.Type}, so this hits harder than the same move would elsewhere",
            BuildSlotRole.Utility when WantsUtility(context.RoleAnalysis.Role) =>
                "this Pokémon lasts long enough to make a non-damaging move pay",
            BuildSlotRole.Utility =>
                "one slot that is not an attack, so it is not helpless against what it cannot outdamage",
            _ when presetMoves.Contains(move.ReferenceId) =>
                "part of a common set for this Pokémon",
            _ => "nothing else scored higher for the last slot",
        };
    }

    /// <summary>The types this move newly reaches — not the ones already answered.</summary>
    private static string DescribeTargets(
        RecommendationContext context,
        PokemonType moveType,
        IReadOnlySet<PokemonType> answered)
    {
        PokemonType[] hit =
        [
            .. TypesHitHard(context, moveType).Where(defending => !answered.Contains(defending)).Take(3),
        ];

        return hit.Length == 0 ? moveType.ToString() : string.Join(", ", hit);
    }

    /// <summary>The team weaknesses this move punishes.</summary>
    private static string DescribeThreats(RecommendationContext context, PokemonType moveType)
    {
        PokemonType[] threats =
        [
            .. context.TeamAnalysis.DefensiveGaps
                .Where(threat => context.Rules.TypeChart.GetMultiplier(moveType, threat) > 1)
                .Take(3),
        ];

        return threats.Length == 0 ? moveType.ToString() : string.Join(", ", threats);
    }

    private static IEnumerable<PokemonType> TypesHitHard(
        RecommendationContext context,
        PokemonType moveType) =>
        context.Rules.TypeChart.Types.Where(
            defending => context.Rules.TypeChart.GetMultiplier(moveType, defending) > 1);

    public static NatureRecommendation RecommendNature(
        PokemonRoleAnalysis role,
        IGenerationRules rules)
    {
        string[] preferredNames = role.Role switch
        {
            PokemonRole.PhysicalAttacker when role.Member.BaseStats.Speed >= 90 =>
                ["Jolly", "Adamant"],
            PokemonRole.PhysicalAttacker => ["Adamant", "Jolly"],
            PokemonRole.SpecialAttacker when role.Member.BaseStats.Speed >= 90 =>
                ["Timid", "Modest"],
            PokemonRole.SpecialAttacker => ["Modest", "Timid"],
            PokemonRole.MixedAttacker => ["Naive", "Hasty"],
            PokemonRole.PhysicalWall when role.PhysicalMoveCount >= role.SpecialMoveCount =>
                ["Impish"],
            PokemonRole.PhysicalWall => ["Bold"],
            PokemonRole.SpecialWall when role.PhysicalMoveCount > role.SpecialMoveCount =>
                ["Careful"],
            PokemonRole.SpecialWall => ["Calm"],
            PokemonRole.MixedWall when role.PhysicalMoveCount > role.SpecialMoveCount =>
                ["Careful"],
            PokemonRole.MixedWall => ["Calm"],
            PokemonRole.Support when role.PhysicalMoveCount > role.SpecialMoveCount =>
                ["Careful"],
            _ => ["Calm"],
        };

        NatureInfo[] preferred =
        [
            .. preferredNames.Select(name => rules.Natures.Single(nature => nature.Name == name)),
        ];
        NatureInfo current = rules.GetNature(role.Member.NatureId);

        return new NatureRecommendation(
            preferred,
            current,
            preferred.Any(nature => nature.Id == current.Id));
    }

    public static EvRecommendation RecommendCompetitiveEvs(
        PokemonRoleAnalysis role,
        IGenerationRules rules,
        NatureInfo recommendedNature)
    {
        StatBlock spread = role.Role switch
        {
            PokemonRole.PhysicalAttacker =>
                new StatBlock(4, 252, 0, 0, 0, 252),
            PokemonRole.SpecialAttacker =>
                new StatBlock(4, 0, 0, 252, 0, 252),
            PokemonRole.MixedAttacker when role.PhysicalOffenseScore >= role.SpecialOffenseScore =>
                new StatBlock(0, 252, 0, 4, 0, 252),
            PokemonRole.MixedAttacker =>
                new StatBlock(0, 4, 0, 252, 0, 252),
            PokemonRole.PhysicalWall =>
                new StatBlock(252, 0, 252, 0, 4, 0),
            PokemonRole.SpecialWall =>
                new StatBlock(252, 0, 4, 0, 252, 0),
            _ when role.Member.BaseStats.Defense <= role.Member.BaseStats.SpecialDefense =>
                new StatBlock(252, 0, 252, 0, 4, 0),
            _ => new StatBlock(252, 0, 4, 0, 252, 0),
        };

        StatBlock projectedStats = rules.CalculateStats(
            role.Member.Level,
            role.Member.BaseStats,
            role.Member.IndividualValues,
            spread,
            recommendedNature.Id);

        return new EvRecommendation(true, spread, [], projectedStats);
    }

    public static EvRecommendation RecommendPlaythroughEvs(PokemonRole role) =>
        new(
            false,
            null,
            role switch
            {
                PokemonRole.PhysicalAttacker => [Stat.Attack, Stat.Speed],
                PokemonRole.SpecialAttacker => [Stat.SpecialAttack, Stat.Speed],
                PokemonRole.MixedAttacker => [Stat.Attack, Stat.SpecialAttack, Stat.Speed],
                PokemonRole.PhysicalWall => [Stat.Hp, Stat.Defense],
                PokemonRole.SpecialWall => [Stat.Hp, Stat.SpecialDefense],
                _ => [Stat.Hp, Stat.Defense, Stat.SpecialDefense],
            },
            null);

    public static IReadOnlyList<ItemRecommendation> RecommendCompetitiveItems(PokemonRole role)
    {
        string[] items = role switch
        {
            PokemonRole.PhysicalAttacker => ["Choice Band", "Lum Berry", "Leftovers"],
            PokemonRole.SpecialAttacker => ["Leftovers", "Lum Berry"],
            PokemonRole.MixedAttacker => ["Leftovers", "Lum Berry"],
            _ => ["Leftovers", "Lum Berry"],
        };

        return
        [
            .. items.Select(name => new ItemRecommendation(
                name,
                RecommendationAvailability.CompetitiveReference)),
        ];
    }

    public static IReadOnlyList<ItemRecommendation> RecommendPlaythroughItems(
        PokemonSnapshot member)
    {
        var items = new List<ItemRecommendation>();
        if (member.HeldItemId > 0 && member.HeldItemName != "-")
        {
            items.Add(new ItemRecommendation(
                member.HeldItemName,
                RecommendationAvailability.KnownAvailable));
        }

        string? typeBooster = member.PrimaryType switch
        {
            PokemonType.Normal => "Silk Scarf",
            PokemonType.Fighting => "Black Belt",
            PokemonType.Flying => "Sharp Beak",
            PokemonType.Poison => "Poison Barb",
            PokemonType.Ground => "Soft Sand",
            PokemonType.Rock => "Hard Stone",
            PokemonType.Bug => "SilverPowder",
            PokemonType.Ghost => "Spell Tag",
            PokemonType.Steel => "Metal Coat",
            PokemonType.Fire => "Charcoal",
            PokemonType.Water => "Mystic Water",
            PokemonType.Grass => "Miracle Seed",
            PokemonType.Electric => "Magnet",
            PokemonType.Psychic => "Twisted Spoon",
            PokemonType.Ice => "Never-Melt Ice",
            PokemonType.Dragon => "Dragon Fang",
            PokemonType.Dark => "Black Glasses",
            _ => null,
        };

        if (typeBooster is not null && items.All(item => item.Name != typeBooster))
        {
            items.Add(new ItemRecommendation(
                typeBooster,
                RecommendationAvailability.RequiresAvailabilityCheck));
        }
        items.Add(new ItemRecommendation(
            "Sitrus Berry",
            RecommendationAvailability.RequiresAvailabilityCheck));

        return items;
    }

    /// <summary>
    /// A move that deals damage <em>and</em> can gain a type multiplier. Gen 3 uses base
    /// power 1 as a sentinel for fixed-damage and one-hit knockout moves: Seismic Toss
    /// deals damage but covers nothing. See D-022.
    /// </summary>
    private static bool IsDamaging(RecommendationContext context, MoveReference move) =>
        context.Rules.GetMoveCategory(move.MoveId, move.Type) != MoveCategory.Status &&
        context.Rules.CanProvideSuperEffectiveCoverage(move.MoveId);

    private static bool MatchesRoleCategory(PokemonRole role, MoveCategory category) => role switch
    {
        PokemonRole.PhysicalAttacker or PokemonRole.PhysicalWall => category == MoveCategory.Physical,
        PokemonRole.SpecialAttacker or PokemonRole.SpecialWall => category == MoveCategory.Special,
        PokemonRole.MixedAttacker => category is MoveCategory.Physical or MoveCategory.Special,
        _ => false,
    };

    private static bool WantsUtility(PokemonRole role) =>
        role is PokemonRole.PhysicalWall or PokemonRole.SpecialWall
            or PokemonRole.MixedWall or PokemonRole.Support;

    /// <summary>
    /// Whether this move type reaches something the team still cannot. Types already
    /// answered by a build chosen for an earlier member count as answered, so six Pokémon
    /// do not each pick the same coverage move for the same hole. See D-031.
    /// </summary>
    private static bool ClosesTeamGap(
        RecommendationContext context,
        PokemonType moveType,
        IReadOnlySet<PokemonType> answered) =>
        context.TeamAnalysis.OffensiveGaps
            .Where(gap => !answered.Contains(gap))
            .Any(gap => context.Rules.TypeChart.GetMultiplier(moveType, gap) > 1);

    /// <summary>
    /// Whether this move beats a type the party is defensively weak to. Being able to knock
    /// out what threatens you is the other half of a type problem.
    /// </summary>
    private static bool AnswersTeamWeakness(RecommendationContext context, PokemonType moveType) =>
        context.TeamAnalysis.DefensiveGaps.Any(
            threat => context.Rules.TypeChart.GetMultiplier(moveType, threat) > 1);

    private static int ScorePreset(
        ReferencePreset preset,
        PokemonRoleAnalysis role,
        IReadOnlySet<string> currentMoves)
    {
        int score = preset.MovePool.Count(currentMoves.Contains) * 10;
        string presetRole = preset.Role;

        score += role.Role switch
        {
            PokemonRole.PhysicalWall or PokemonRole.SpecialWall or PokemonRole.MixedWall
                when presetRole is "Staller" or "Bulky Support" => 6,
            PokemonRole.Support when presetRole is "Bulky Support" or "Staller" => 6,
            PokemonRole.PhysicalAttacker or PokemonRole.SpecialAttacker or PokemonRole.MixedAttacker
                when presetRole is "Wallbreaker" or "Fast Attacker" or "Setup Sweeper"
                    or "Berry Sweeper" or "Bulky Attacker" => 4,
            _ when presetRole == "Generalist" => 2,
            _ => 0,
        };

        if (role.UtilityMoveCount > 0 && presetRole.Contains("Setup", StringComparison.Ordinal))
        {
            score++;
        }

        return score;
    }
}
