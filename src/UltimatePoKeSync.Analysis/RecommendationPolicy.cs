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

    /// <summary>Picks the four moves to actually run, and says why for each of them.</summary>
    public static RecommendedBuild SelectBuild(
        RecommendationContext context,
        IReadOnlyList<MoveRecommendation> candidates)
    {
        const int moveSlots = 4;
        if (candidates.Count == 0)
        {
            return new RecommendedBuild([], ["No move is known for this Pokémon yet."], []);
        }

        HashSet<string> presetMoves = [.. MatchPreset(context)?.MovePool ?? []];

        var scored = candidates
            .Select((candidate, index) => new
            {
                Candidate = candidate,
                Index = index,
                Score = ScoreMove(context, candidate, presetMoves),
                Reason = ReasonFor(context, candidate, presetMoves),
            })
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Index)
            .ToArray();

        var chosen = scored.Take(moveSlots).ToArray();

        return new RecommendedBuild(
            [.. chosen.Select(entry => entry.Candidate)],
            [.. chosen.Select(entry => $"{entry.Candidate.Move.Name}: {entry.Reason}")],
            [.. scored.Skip(moveSlots).Select(entry => entry.Candidate)]);
    }

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

    private static int ScoreMove(
        RecommendationContext context,
        MoveRecommendation candidate,
        IReadOnlySet<string> presetMoves)
    {
        PokemonSnapshot member = context.RoleAnalysis.Member;
        MoveReference move = candidate.Move;
        MoveCategory category = context.Rules.GetMoveCategory(move.MoveId, move.Type);
        bool damaging = category != MoveCategory.Status &&
            context.Rules.CanProvideSuperEffectiveCoverage(move.MoveId);

        int score = 0;

        if (damaging && (move.Type == member.PrimaryType || move.Type == member.SecondaryType))
        {
            score += 6;
        }

        if (damaging && MatchesRoleCategory(context.RoleAnalysis.Role, category))
        {
            score += 5;
        }

        if (damaging && ClosesTeamGap(context, move.Type))
        {
            score += 4;
        }

        if (!damaging && WantsUtility(context.RoleAnalysis.Role))
        {
            score += 4;
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

    private static string ReasonFor(
        RecommendationContext context,
        MoveRecommendation candidate,
        IReadOnlySet<string> presetMoves)
    {
        PokemonSnapshot member = context.RoleAnalysis.Member;
        MoveReference move = candidate.Move;
        MoveCategory category = context.Rules.GetMoveCategory(move.MoveId, move.Type);
        bool damaging = category != MoveCategory.Status &&
            context.Rules.CanProvideSuperEffectiveCoverage(move.MoveId);

        if (damaging && ClosesTeamGap(context, move.Type))
        {
            return $"the only {move.Type} damage the party has for a type nothing else answers";
        }

        if (damaging && (move.Type == member.PrimaryType || move.Type == member.SecondaryType))
        {
            return MatchesRoleCategory(context.RoleAnalysis.Role, category)
                ? $"same-type damage on the {category.ToString().ToLowerInvariant()} side this Pokémon hits hardest with"
                : "same-type damage";
        }

        if (damaging && MatchesRoleCategory(context.RoleAnalysis.Role, category))
        {
            return $"{category.ToString().ToLowerInvariant()} coverage that scales with the better attacking stat";
        }

        if (!damaging)
        {
            return WantsUtility(context.RoleAnalysis.Role)
                ? "utility, which is what this Pokémon's bulk is for"
                : "utility";
        }

        return presetMoves.Contains(move.ReferenceId)
            ? "part of the matched reference set"
            : "the best of what is left";
    }

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

    /// <summary>Whether this move type answers a type the whole party currently cannot.</summary>
    private static bool ClosesTeamGap(RecommendationContext context, PokemonType moveType) =>
        context.TeamAnalysis.OffensiveGaps.Any(
            gap => context.Rules.TypeChart.GetMultiplier(moveType, gap) > 1);

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
