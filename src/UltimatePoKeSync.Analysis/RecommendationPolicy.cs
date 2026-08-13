using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

internal static class RecommendationPolicy
{
    public static ReferencePreset? MatchPreset(RecommendationContext context)
    {
        // Advice about nature, EVs and a competitive build is aimed at the same species.
        // For a straight evolution line that is the final form; for a branch it remains the
        // member in front of us. A Snivy therefore gets Serperior's useful prior instead of
        // falling back to Tackle and Leer merely because Random Battles has no Snivy entry.
        IReadOnlyList<ReferencePreset> presets =
            context.PresetCatalog.Find(context.RoleAnalysis.JudgedSpeciesName);
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
        IReadOnlyList<MoveRecommendation> candidates,
        int candidateLimit)
    {
        const int moveSlots = 4;

        // A good setup, recovery or disruption move deserves a slot. A status move merely
        // being present does not: without effect metadata Leer is indistinguishable from
        // Leech Seed, so the species' reference sets are the honesty boundary for forcing one.
        const int maximumAttacks = 3;

        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidateLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateLimit));
        }

        if (candidates.Count == 0)
        {
            return new RecommendedBuild([], []);
        }

        HashSet<string> presetMoves = [.. MatchPreset(context)?.MovePool ?? []];
        HashSet<string> trustedUtilityMoves =
        [
            .. context.PresetCatalog
                .Find(context.RoleAnalysis.JudgedSpeciesName)
                .SelectMany(preset => preset.MovePool),
        ];
        HashSet<string> trustedDamagingMoves =
        [
            .. trustedUtilityMoves.Where(referenceId =>
                context.MoveCatalog.Find(referenceId) is MoveReference move &&
                DealsDamage(context, move)),
        ];

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
                remaining.Any(entry =>
                    IsTrustedUtility(context, entry.Candidate, trustedUtilityMoves));

            // A competitive reference explicitly naming an attack is stronger evidence
            // than arbitrary machine coverage from a huge legal pool. Guarantee one such
            // attack before the generic ranking can fill all three attacking slots.
            bool trustedAttackOnly = context.Profile == RecommendationProfileKind.Competitive &&
                attacks == maximumAttacks - 1 &&
                !slots.Any(slot => trustedDamagingMoves.Contains(slot.Move.Move.ReferenceId)) &&
                remaining.Any(entry =>
                    trustedDamagingMoves.Contains(entry.Candidate.Move.ReferenceId));

            var best = remaining
                .Where(entry =>
                    (!utilityOnly || IsTrustedUtility(context, entry.Candidate, trustedUtilityMoves)) &&
                    (!trustedAttackOnly ||
                        trustedDamagingMoves.Contains(entry.Candidate.Move.ReferenceId)))
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

            if (DealsDamage(context, best.Candidate.Move))
            {
                attacks++;

                if (ProvidesCoverage(context, best.Candidate.Move))
                {
                    coveredTypes.Add(best.Candidate.Move.Type);
                    answered.UnionWith(TypesHitHard(context, best.Candidate.Move.Type));
                }
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
                    .Take(Math.Max(0, candidateLimit - slots.Count))
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
        MoveReference move = candidate.Move;
        MoveCategory category = context.Rules.GetMoveCategory(move.MoveId, move.Type);
        bool damaging = DealsDamage(context, move);
        bool providesCoverage = ProvidesCoverage(context, move);
        int score = 0;

        if (damaging)
        {
            if (providesCoverage && coveredTypes.Contains(move.Type))
            {
                // Repeating a type the build already hits costs more than the same-type
                // bonus is worth, so a duplicate has to win on other merits.
                score -= 7;
            }

            if (providesCoverage && HasSameTypeBonus(context, move.Type))
            {
                score += 6;
            }
            else if (providesCoverage && !TypesHitHard(context, move.Type).Any() &&
                !presetMoves.Contains(move.ReferenceId))
            {
                // An off-type move that can never hit super effectively is filler unless a
                // real set gives us a reason to keep it. This is what stops Wrap replacing
                // Tackle in name only on an early Snivy build.
                score -= 3;
            }

            if (MatchesRoleCategory(context.RoleAnalysis.Role, category))
            {
                score += 5;
            }

            if (providesCoverage && ClosesTeamGap(context, move.Type, answered))
            {
                score += 4;
            }

            if (providesCoverage && AnswersTeamWeakness(context, move.Type))
            {
                score += 3;
            }

            score += providesCoverage ? PowerBonus(context, move) : FixedDamageBonus(move);
        }
        else
        {
            // Utility is worth something to everyone, and worth more to a Pokémon whose
            // bulk is the reason it is on the team at all.
            score += WantsUtility(context.RoleAnalysis.Role) ? 4 : 2;

            if (presetMoves.Contains(move.ReferenceId))
            {
                // This is the only effect-quality signal the pinned data currently carries.
                // It separates setup, recovery and disruption used in real sets from early
                // filler such as Leer without pretending every status move is equivalent.
                score += 3;
            }
        }

        // Keeping a move it already knows is worth something while playing, where changing
        // one costs a TM or a trip to the Move Reminder. It is worth nothing to a build that
        // assumes a trained Pokémon: nobody battles at level 100 with the Tackle it was
        // caught with, and paying for continuity there is how Tackle reached a Serperior's
        // recommended set. See D-051.
        if (damaging && candidate.Source == MoveCandidateSource.CurrentMoveset &&
            context.Profile == RecommendationProfileKind.Playthrough)
        {
            score += 1;
        }

        // A move that needs only levels beats one that needs a machine found somewhere in a
        // save the app cannot see. Kept small, so a much stronger machine move still wins.
        score += candidate.Availability switch
        {
            RecommendationAvailability.ArrivesWithLevelUp => 3,
            RecommendationAvailability.RequiresAvailabilityCheck => -3,
            _ => 0,
        };

        if (presetMoves.Contains(move.ReferenceId))
        {
            // A reference set is the strongest effect-quality signal the bundled data
            // carries. Damaging moves get the same prior utility moves already receive;
            // otherwise broad learnsets can push a proven move such as Seismic Toss out
            // of Blissey's build in favour of arbitrary machine coverage.
            score += damaging ? 5 : 2;
        }

        return score;
    }

    /// <summary>
    /// Weak moves are not best moves. Kept small enough to break ties rather than override
    /// coverage: Solar Beam earns four points here, Giga Drain none, Tackle loses two.
    /// </summary>
    private static int PowerBonus(RecommendationContext context, MoveReference move)
    {
        const int variablePower = 1;
        const int assumedVariablePower = 60;

        int power = context.Rules.GetMoveBasePower(move.MoveId);
        power = power == variablePower ? assumedVariablePower : power;

        return power switch
        {
            <= 60 => -2,
            <= 80 => 0,
            <= 100 => 2,
            _ => 4,
        };
    }

    private static bool IsTrustedUtility(
        RecommendationContext context,
        MoveRecommendation candidate,
        IReadOnlySet<string> presetMoves) =>
        !DealsDamage(context, candidate.Move) &&
        presetMoves.Contains(candidate.Move.ReferenceId);

    private static BuildSlotRole ClassifySlot(
        RecommendationContext context,
        MoveRecommendation candidate,
        IReadOnlySet<PokemonType> coveredTypes,
        IReadOnlySet<PokemonType> answered)
    {
        MoveReference move = candidate.Move;

        if (!DealsDamage(context, move))
        {
            return BuildSlotRole.Utility;
        }

        if (!ProvidesCoverage(context, move))
        {
            return BuildSlotRole.DirectDamage;
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
            HasSameTypeBonus(context, move.Type))
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
        MoveReference move = candidate.Move;

        return role switch
        {
            BuildSlotRole.Coverage =>
                $"nothing else on the team hits {DescribeTargets(context, move.Type, answered)} hard",
            BuildSlotRole.TeamSupport =>
                $"beats {DescribeThreats(context, move.Type)}, which the team is weak to",
            BuildSlotRole.SameType =>
                $"{MoveTargetSpeciesName(context)} is {move.Type}, so this hits harder than the same move would elsewhere",
            BuildSlotRole.Utility when WantsUtility(context.RoleAnalysis.Role) =>
                "this Pokémon lasts long enough to make a non-damaging move pay",
            BuildSlotRole.Utility =>
                "one slot that is not an attack, so it is not helpless against what it cannot outdamage",
            BuildSlotRole.DirectDamage =>
                "deals damage without relying on an ordinary type matchup",
            _ when presetMoves.Contains(move.ReferenceId) =>
                "part of a common set for this Pokémon",
            _ => "nothing else scored higher for the last slot",
        };
    }

    /// <summary>The types this move newly reaches, not the ones already answered.</summary>
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
            PokemonRole.PhysicalAttacker when role.JudgedBaseStats.Speed >= 90 =>
                ["Jolly", "Adamant"],
            PokemonRole.PhysicalAttacker => ["Adamant", "Jolly"],
            PokemonRole.SpecialAttacker when role.JudgedBaseStats.Speed >= 90 =>
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
            _ when role.JudgedBaseStats.Defense <= role.JudgedBaseStats.SpecialDefense =>
                new StatBlock(252, 0, 252, 0, 4, 0),
            _ => new StatBlock(252, 0, 4, 0, 252, 0),
        };

        StatBlock projectedStats = rules.CalculateStats(
            100,
            role.JudgedBaseStats,
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
    /// Whether the move deals damage at all. This must stay separate from type coverage:
    /// Seismic Toss is an attack, but it never gains a super-effective multiplier.
    /// </summary>
    private static bool DealsDamage(RecommendationContext context, MoveReference move) =>
        context.Rules.GetMoveCategory(move.MoveId, move.Type) != MoveCategory.Status;

    private static bool ProvidesCoverage(RecommendationContext context, MoveReference move) =>
        context.Rules.CanProvideSuperEffectiveCoverage(move.MoveId);

    /// <summary>
    /// Fixed and reflected damage has no useful base-power number in the table. A real
    /// preset naming it is already the quality signal; this neutral value prevents the
    /// power sentinel from treating it as a weak status move.
    /// </summary>
    private static int FixedDamageBonus(MoveReference move) => move.MoveId switch
    {
        12 or 32 or 90 or 329 => -2, // one-hit-KO moves are unreliable
        _ => 0,
    };

    /// <summary>
    /// Playthrough moves have to work on the Pokémon in front of the player. Competitive
    /// moves are for the trained final form, whose typing can be different (Torchic gains
    /// Fighting when it becomes Blaziken). See D-052.
    /// </summary>
    private static bool HasSameTypeBonus(RecommendationContext context, PokemonType moveType)
    {
        PokemonRoleAnalysis role = context.RoleAnalysis;
        PokemonType primary = context.Profile == RecommendationProfileKind.Competitive
            ? role.JudgedPrimaryType
            : role.Member.PrimaryType;
        PokemonType secondary = context.Profile == RecommendationProfileKind.Competitive
            ? role.JudgedSecondaryType
            : role.Member.SecondaryType;

        return moveType == primary || moveType == secondary;
    }

    private static string MoveTargetSpeciesName(RecommendationContext context) =>
        context.Profile == RecommendationProfileKind.Competitive
            ? context.RoleAnalysis.JudgedSpeciesName
            : context.RoleAnalysis.Member.SpeciesName;

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
