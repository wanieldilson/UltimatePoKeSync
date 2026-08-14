using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>
/// Finds obtainable additions which improve a party, with every point tied to a fact the
/// UI can show. Encounter data is supplied by the caller so this analyzer stays pure and
/// can never infer story progress from a Pokémon's level.
/// </summary>
public sealed class TeamHintAnalyzer
{
    private const int FullPartySize = 6;
    private const int MaximumPlanSize = 3;
    private const int MaximumPlans = 3;
    private const int MaximumCombinationCandidates = 24;
    private const int MaximumEvolutionDepth = 4;

    /// <summary>
    /// How far above the party's average level an encounter may sit and still be worth
    /// naming. Beyond this it cannot be weakened without fainting the thing weakening it.
    /// </summary>
    private const int OutOfReachLevels = 8;

    private readonly GameDataSources _sources;
    private readonly IGenerationRulesResolver _rulesResolver;

    public TeamHintAnalyzer(GameDataSources sources)
        : this(sources, GenerationRulesResolver.Default)
    {
    }

    public TeamHintAnalyzer(
        GameDataSources sources,
        IGenerationRulesResolver rulesResolver)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(rulesResolver);

        _sources = sources;
        _rulesResolver = rulesResolver;
    }

    /// <summary>
    /// Produces up to three plans. An incomplete party considers one, two or three additions
    /// up to its free slots; a full party receives explicit one-for-one replacements.
    /// </summary>
    public TeamHintAnalysis Analyze(
        PartySnapshot party,
        StoryMilestone selectedMilestone,
        IReadOnlyList<EncounterCandidate> encounters)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(selectedMilestone);
        ArgumentNullException.ThrowIfNull(encounters);

        IGenerationRules rules = _rulesResolver.Resolve(party.Game.Generation)
            ?? throw new NotSupportedException(
                $"No team-hint rules are available for {party.Game.Generation}.");
        if (!_sources.Learnsets.Supports(party.Game) ||
            !_sources.Evolutions.Supports(party.Game) ||
            !_sources.BaseStats.Supports(party.Game))
        {
            throw new NotSupportedException(
                $"The species data needed for team hints is not available for {party.Game}.");
        }

        int averageLevel = party.Battlers.Count == 0
            ? 5
            : (int)Math.Round(party.Battlers.Average(member => member.Level));
        int targetLevel = Math.Clamp(averageLevel + 3, 1, 100);

        HashSet<int> carriedSpecies =
        [
            .. party.Members.Select(member => (int)member.SpeciesId),
        ];

        Baseline baseline = BaselineFor(party, rules);
        PreparedCandidate[] prepared =
        [
            .. encounters
                // Progress is an input, never a guess based on level or badges hidden in
                // another model. A future route therefore cannot leak into the ranking.
                .Where(encounter => encounter.EarliestMilestone.Order <= selectedMilestone.Order)
                // A seasonal/weather condition which this snapshot cannot verify is not a
                // safe "available now" promise. Conditional encounters stay in the catalog
                // for future UI, but never enter the main ranking without that evidence.
                .Where(encounter => !encounter.AvailabilityIsConditional)
                // And nothing far above the party. A Pokemon well over your level cannot be
                // worn down safely and will not stay in a ball; suggesting it is not advice,
                // it is a trip that ends in a blackout. See D-062.
                .Where(encounter => encounter.MinimumLevel <= averageLevel + OutOfReachLevels)
                .GroupBy(encounter => encounter.SpeciesId)
                .Select(group => group
                    .OrderBy(encounter => encounter.EarliestMilestone.Order)
                    .ThenBy(encounter => encounter.Location, StringComparer.Ordinal)
                    .First())
                // Carrying any stage makes the whole evolution line redundant. Members
                // includes eggs on purpose: an unhatched Purrloin still means the player
                // does not need to be sent after a Liepard.
                .Where(encounter => !carriedSpecies.Any(carried =>
                    AreInSameEvolutionLine(party.Game, encounter.SpeciesId, carried)))
                .Select(encounter => Prepare(
                    party.Game,
                    encounter,
                    averageLevel,
                    targetLevel,
                    baseline,
                    rules))
                .OfType<PreparedCandidate>()
                .OrderByDescending(candidate => candidate.IndividualScore)
                .ThenBy(candidate => candidate.Encounter.SpeciesId)
                .Take(MaximumCombinationCandidates),
        ];

        IReadOnlyList<TeamHintPlan> plans = party.Count < FullPartySize
            ? AdditionPlans(party, prepared, baseline, rules)
            : ReplacementPlans(party, prepared, baseline, rules);

        return new TeamHintAnalysis(
            selectedMilestone,
            averageLevel,
            targetLevel,
            prepared.Length,
            plans);
    }

    private IReadOnlyList<TeamHintPlan> AdditionPlans(
        PartySnapshot party,
        IReadOnlyList<PreparedCandidate> candidates,
        Baseline baseline,
        IGenerationRules rules)
    {
        int maximumAdditions = Math.Min(
            Math.Min(MaximumPlanSize, FullPartySize - party.Count),
            candidates.Count);
        if (maximumAdditions <= 0)
        {
            return [];
        }

        var plans = new List<TeamHintPlan>();
        for (int additions = 1; additions <= maximumAdditions; additions++)
        {
            foreach (PreparedCandidate[] combination in Combinations(candidates, additions)
                .Where(HasNoExclusiveGroupConflict)
                .Where(combination =>
                    HasNoEvolutionLineConflict(party.Game, combination)))
            {
                plans.Add(EvaluatePlan(party, combination, null, baseline, rules));
            }
        }

        return DistinctBest(plans);
    }

    private IReadOnlyList<TeamHintPlan> ReplacementPlans(
        PartySnapshot party,
        IReadOnlyList<PreparedCandidate> candidates,
        Baseline baseline,
        IGenerationRules rules)
    {
        var plans = new List<TeamHintPlan>();
        foreach (PokemonSnapshot replaced in party.Members)
        {
            foreach (PreparedCandidate candidate in candidates)
            {
                plans.Add(EvaluatePlan(party, [candidate], replaced, baseline, rules));
            }
        }

        // Convenience can break ties between genuine upgrades, but it cannot turn a
        // strategically neutral or harmful swap into a recommendation.
        return DistinctBest(plans.Where(HasPositiveStrategicDelta));
    }

    private static IReadOnlyList<TeamHintPlan> DistinctBest(IEnumerable<TeamHintPlan> plans) =>
    [
        .. plans
            .OrderByDescending(plan => plan.Score)
            .ThenBy(plan => PlanKey(plan), StringComparer.Ordinal)
            .DistinctBy(PlanKey)
            .Take(MaximumPlans),
    ];

    private static bool HasPositiveStrategicDelta(TeamHintPlan plan) =>
        plan.Factors
            .Where(factor => factor.Kind is not TeamHintScoreKind.Practicality)
            .Sum(factor => factor.Points) > 0;

    private static string PlanKey(TeamHintPlan plan)
    {
        string species = string.Join(",", plan.Additions
            .Select(candidate => candidate.SpeciesId)
            .Order());
        return $"{plan.Replacement?.SlotIndex.ToString() ?? "add"}:{species}";
    }

    private TeamHintPlan EvaluatePlan(
        PartySnapshot party,
        IReadOnlyList<PreparedCandidate> additions,
        PokemonSnapshot? replaced,
        Baseline baseline,
        IGenerationRules rules)
    {
        PokemonSnapshot[] retained =
        [
            .. party.Battlers.Where(member =>
                replaced is null || member.SlotIndex != replaced.SlotIndex),
        ];

        HashSet<PokemonType> finalDefensiveGaps = DefensiveGaps(
            retained,
            additions,
            rules);
        HashSet<PokemonType> finalOffensiveCoverage = OffensiveCoverage(
            retained,
            additions,
            rules);

        int closedDefensive = baseline.DefensiveGaps.Count(type => !finalDefensiveGaps.Contains(type));
        int introducedDefensive = finalDefensiveGaps.Count(type => !baseline.DefensiveGaps.Contains(type));
        int newOffensive = finalOffensiveCoverage.Count(type => !baseline.OffensiveCoverage.Contains(type));
        int lostOffensive = baseline.OffensiveCoverage.Count(type => !finalOffensiveCoverage.Contains(type));

        HashSet<PokemonType> finalTypes = TypesOf(retained);
        foreach (PreparedCandidate candidate in additions)
        {
            finalTypes.Add(candidate.BattleProfile.PrimaryType);
            if (candidate.BattleProfile.SecondaryType != PokemonType.None)
            {
                finalTypes.Add(candidate.BattleProfile.SecondaryType);
            }
        }

        int newTypes = finalTypes.Count(type => !baseline.Types.Contains(type));
        int lostTypes = baseline.Types.Count(type => !finalTypes.Contains(type));

        HashSet<PokemonRole> finalRoles = RolesOf(retained);
        finalRoles.UnionWith(additions.Select(candidate => candidate.Role));
        int newRoles = finalRoles.Count(role => !baseline.Roles.Contains(role));
        int lostRoles = baseline.Roles.Count(role => !finalRoles.Contains(role));

        int duplicateCoverage = Math.Max(0,
            additions.Sum(candidate => candidate.CoveredTypes.Count)
            - additions.SelectMany(candidate => candidate.CoveredTypes).Distinct().Count());
        int duplicateAnswers = Math.Max(0,
            additions.Sum(candidate => candidate.DefensiveAnswers.Count)
            - additions.SelectMany(candidate => candidate.DefensiveAnswers).Distinct().Count());
        int redundancyPenalty = (duplicateCoverage * 3) + (duplicateAnswers * 2);

        TeamHintScoreFactor[] factors =
        [
            new(
                TeamHintScoreKind.DefensiveCoverage,
                (closedDefensive * 12) - (introducedDefensive * 10),
                CoverageExplanation(
                    closedDefensive,
                    introducedDefensive,
                    "shared defensive gap",
                    "new defensive gap")),
            new(
                TeamHintScoreKind.OffensiveCoverage,
                (newOffensive * 6) - (lostOffensive * 5),
                CoverageExplanation(
                    newOffensive,
                    lostOffensive,
                    "new type hit super effectively by level-up moves",
                    "previously covered type lost")),
            new(
                TeamHintScoreKind.TypeDiversity,
                (newTypes * 3) - (lostTypes * 3),
                CoverageExplanation(newTypes, lostTypes, "new team type", "team type lost")),
            new(
                TeamHintScoreKind.RoleDiversity,
                (newRoles * 4) - (lostRoles * 4),
                CoverageExplanation(newRoles, lostRoles, "new battle role", "battle role lost")),
            new(
                TeamHintScoreKind.Practicality,
                PlanPracticalityPoints(additions),
                PracticalityExplanation(additions)),
            new(
                TeamHintScoreKind.Redundancy,
                -redundancyPenalty,
                redundancyPenalty == 0
                    ? "The additions solve different problems."
                    : $"Overlapping answers cost {redundancyPenalty} points."),
        ];

        int score = factors.Sum(factor => factor.Points);
        TeamHintCandidate[] publicCandidates =
        [
            .. additions
                .OrderByDescending(candidate => candidate.IndividualScore)
                .ThenBy(candidate => candidate.Encounter.SpeciesId)
                .Select(candidate => PublicCandidate(
                    candidate,
                    replaced is null || additions.Count > 1
                        ? candidate.IndividualScore
                        : score)),
        ];

        TeamHintReplacement? replacement = replaced is null
            ? null
            : new TeamHintReplacement(
                replaced.SlotIndex,
                replaced.IsEgg ? 0 : replaced.SpeciesId,
                replaced.IsEgg ? "Egg" : replaced.SpeciesName);

        return new TeamHintPlan(
            publicCandidates,
            replacement,
            factors,
            score,
            PlanSummary(publicCandidates, replacement, closedDefensive, newOffensive));
    }

    private PreparedCandidate? Prepare(
        GameIdentity game,
        EncounterCandidate encounter,
        int averageLevel,
        int commonTargetLevel,
        Baseline baseline,
        IGenerationRules rules)
    {
        SpeciesBattleProfile? catchProfile = _sources.BaseStats.FindProfile(game, encounter.SpeciesId);
        if (catchProfile is null)
        {
            return null;
        }

        int recommendedLevel = Math.Clamp(
            Math.Max(commonTargetLevel, encounter.MaximumLevel),
            1,
            100);
        ReachableLine reachable = ReachableAt(game, encounter, recommendedLevel);
        SpeciesBattleProfile battleProfile =
            _sources.BaseStats.FindProfile(game, reachable.SpeciesId) ?? catchProfile;
        TeamHintEvolutionProjection? finalEvolution = FinalEvolution(game, encounter.SpeciesId);
        HashSet<PokemonType> moveTypes = LevelUpAttackTypes(
            game,
            reachable.Stages,
            battleProfile,
            baseline.OffensiveGaps,
            rules);
        HashSet<PokemonType> coveredTypes = TypesCovered(moveTypes, rules);
        coveredTypes.IntersectWith(baseline.OffensiveGaps);

        HashSet<PokemonType> defensiveAnswers =
        [
            .. baseline.DefensiveGaps.Where(attackingType =>
                rules.TypeChart.GetMultiplier(
                    attackingType,
                    battleProfile.PrimaryType,
                    battleProfile.SecondaryType) < 1),
        ];

        PokemonRole role = InferRole(battleProfile.BaseStats);
        int practicality = PracticalityPoints(encounter, averageLevel);
        int newTypeCount = new[] { battleProfile.PrimaryType, battleProfile.SecondaryType }
            .Where(type => type != PokemonType.None && !baseline.Types.Contains(type))
            .Distinct()
            .Count();
        int rolePoint = baseline.Roles.Contains(role) ? 0 : 4;
        int individualScore = (defensiveAnswers.Count * 12)
            + (coveredTypes.Count * 6)
            + (newTypeCount * 3)
            + rolePoint
            + practicality;

        return new PreparedCandidate(
            encounter,
            recommendedLevel,
            reachable.SpeciesId,
            reachable.SpeciesName,
            catchProfile,
            battleProfile,
            role,
            finalEvolution,
            moveTypes,
            coveredTypes,
            defensiveAnswers,
            practicality,
            individualScore);
    }

    /// <summary>
    /// Uses only attacks a freshly caught Pokémon can realistically know or learn while
    /// reaching the plan level. Machines and tutors are legal-species facts, not proof that
    /// this save owns the TM or can reach the tutor. Likewise, a newly evolved form's
    /// low-level reminder moves are not treated as moves learned during evolution.
    /// </summary>
    private HashSet<PokemonType> LevelUpAttackTypes(
        GameIdentity game,
        IReadOnlyList<ReachableStage> stages,
        SpeciesBattleProfile battleProfile,
        IReadOnlySet<PokemonType> offensiveGaps,
        IGenerationRules rules)
    {
        var availableAttacks = new List<(MoveReference Move, int Index)>();
        var seenMoves = new HashSet<string>(StringComparer.Ordinal);
        foreach (ReachableStage stage in stages)
        {
            LearnableMove[] levelUpMoves =
            [
                .. _sources.Learnsets
                    .FindLearnableMoves(game, stage.SpeciesId, stage.MaximumLearnLevel)
                    .Where(learnable => learnable.Method == MoveLearnMethod.LevelUp &&
                        learnable.LearnedAtLevel is not null),
            ];

            // A wild Pokémon starts with at most the last four natural moves available at
            // the top of the encounter range. Later stages contribute only moves learned
            // after they are entered; Lv.1 moves on an evolved form require the Reminder.
            IEnumerable<LearnableMove> available = stage.IsCaughtStage
                ? levelUpMoves
                    .Where(move => move.LearnedAtLevel <= stage.EntryLevel)
                    .TakeLast(4)
                    .Concat(levelUpMoves.Where(move =>
                        move.LearnedAtLevel > stage.EntryLevel))
                : levelUpMoves.Where(move => move.LearnedAtLevel > stage.EntryLevel);

            foreach (LearnableMove learnable in available)
            {
                MoveReference move = learnable.Move;
                if (rules.GetMoveCategory(move.MoveId, move.Type) != MoveCategory.Status &&
                    rules.CanProvideSuperEffectiveCoverage(move.MoveId) &&
                    seenMoves.Add(move.ReferenceId))
                {
                    availableAttacks.Add((move, availableAttacks.Count));
                }
            }
        }

        return SelectAttackTypes(availableAttacks, battleProfile, offensiveGaps, rules);
    }

    /// <summary>
    /// A candidate may pass many attacks while it levels, but can carry only four. One
    /// representative per type is enough for type coverage; the greedy selection first
    /// closes as many live gaps as possible, then prefers its own types and stronger moves.
    /// </summary>
    private static HashSet<PokemonType> SelectAttackTypes(
        IReadOnlyList<(MoveReference Move, int Index)> attacks,
        SpeciesBattleProfile battleProfile,
        IReadOnlySet<PokemonType> offensiveGaps,
        IGenerationRules rules)
    {
        const int moveSlots = 4;

        MoveTypeOption[] remaining =
        [
            .. attacks
                .GroupBy(entry => entry.Move.Type)
                .Select(group =>
                {
                    (MoveReference Move, int Index) best = group
                        .OrderByDescending(entry => MovePower(entry.Move, rules))
                        .ThenBy(entry => entry.Index)
                        .First();
                    return new MoveTypeOption(
                        best.Move.Type,
                        MovePower(best.Move, rules),
                        best.Index,
                        [
                            .. offensiveGaps.Where(defending =>
                                rules.TypeChart.GetMultiplier(best.Move.Type, defending) > 1),
                        ]);
                }),
        ];

        var chosen = new HashSet<PokemonType>();
        var answered = new HashSet<PokemonType>();
        while (chosen.Count < moveSlots && remaining.Length > 0)
        {
            MoveTypeOption? best = remaining
                .Where(option => !chosen.Contains(option.Type))
                .OrderByDescending(option =>
                    option.Targets.Count(target => !answered.Contains(target)))
                .ThenByDescending(option => option.Targets.Count)
                .ThenByDescending(option =>
                    option.Type == battleProfile.PrimaryType ||
                    option.Type == battleProfile.SecondaryType)
                .ThenByDescending(option => option.Power)
                .ThenBy(option => option.SourceIndex)
                .FirstOrDefault();
            if (best is null)
            {
                break;
            }

            chosen.Add(best.Type);
            answered.UnionWith(best.Targets);
        }

        return chosen;

        static int MovePower(MoveReference move, IGenerationRules generationRules)
        {
            const int variablePower = 1;
            const int assumedVariablePower = 60;
            int power = generationRules.GetMoveBasePower(move.MoveId);
            return power == variablePower ? assumedVariablePower : power;
        }
    }

    private ReachableLine ReachableAt(
        GameIdentity game,
        EncounterCandidate encounter,
        int targetLevel)
    {
        int speciesId = encounter.SpeciesId;
        string speciesName = encounter.SpeciesName;
        var stages = new List<ReachableStage>();
        var visited = new HashSet<int> { speciesId };
        // Use the top of the encounter range. This avoids crediting a move that only the
        // lowest-level wild set would still carry when the same encounter can appear at a
        // higher level with a different four-move window.
        int entryLevel = encounter.MaximumLevel;
        bool isCaughtStage = true;

        for (int depth = 0; depth < MaximumEvolutionDepth; depth++)
        {
            IReadOnlyList<EvolutionStep> routes = UniqueDestinationRoutes(game, speciesId);
            if (routes.Count != 1)
            {
                break;
            }

            EvolutionStep? levelRoute = routes
                .Where(route => route.RequiredGender is null && route.HappensByLevellingAlone)
                .OrderBy(route => route.Level)
                .FirstOrDefault();
            if (levelRoute?.Level is not int evolutionLevel ||
                !visited.Add(levelRoute.IntoSpeciesId))
            {
                break;
            }

            // A specimen caught above its nominal evolution level still evolves on its
            // next level-up, not retroactively at the level shown in the Pokédex table.
            int evolutionAt = Math.Max(evolutionLevel, entryLevel + 1);
            if (evolutionAt > targetLevel)
            {
                break;
            }

            stages.Add(new ReachableStage(
                speciesId,
                entryLevel,
                evolutionAt,
                isCaughtStage));
            speciesId = levelRoute.IntoSpeciesId;
            speciesName = levelRoute.IntoSpeciesName;
            entryLevel = evolutionAt;
            isCaughtStage = false;
        }

        stages.Add(new ReachableStage(
            speciesId,
            entryLevel,
            targetLevel,
            isCaughtStage));
        return new ReachableLine(speciesId, speciesName, stages);
    }

    private TeamHintEvolutionProjection? FinalEvolution(GameIdentity game, int caughtSpeciesId)
    {
        int speciesId = caughtSpeciesId;
        string speciesName = string.Empty;
        var requirements = new List<string>();
        var visited = new HashSet<int> { speciesId };
        bool evolved = false;

        for (int depth = 0; depth < MaximumEvolutionDepth; depth++)
        {
            IReadOnlyList<EvolutionStep> routes = UniqueDestinationRoutes(game, speciesId);
            if (routes.Count == 0)
            {
                break;
            }

            if (routes.Count != 1)
            {
                return null;
            }

            EvolutionStep route = routes[0];
            // The encounter has no gender attached. A route available to only some of the
            // catches is not a certain projection.
            if (route.RequiredGender is not null || !visited.Add(route.IntoSpeciesId))
            {
                return null;
            }

            speciesId = route.IntoSpeciesId;
            speciesName = route.IntoSpeciesName;
            requirements.Add(route.Requirement);
            evolved = true;
        }

        if (!evolved || _sources.BaseStats.FindProfile(game, speciesId) is not SpeciesBattleProfile profile)
        {
            return null;
        }

        return new TeamHintEvolutionProjection(
            speciesId,
            speciesName,
            profile.PrimaryType,
            profile.SecondaryType,
            string.Join(" → ", requirements));
    }

    /// <summary>
    /// One entry per destination species. Two methods leading to the same result are not a
    /// branch; Eevee's several destination species are.
    /// </summary>
    private IReadOnlyList<EvolutionStep> UniqueDestinationRoutes(GameIdentity game, int speciesId) =>
    [
        .. _sources.Evolutions
            .FindEvolutions(game, speciesId)
            .Where(route => !route.IsByproduct)
            .GroupBy(route => route.IntoSpeciesId)
            .Select(group => group
                .OrderBy(route => route.RequiredGender is not null)
                .ThenByDescending(route => route.HappensByLevellingAlone)
                .ThenBy(route => route.Level ?? int.MaxValue)
                .First()),
    ];

    private Baseline BaselineFor(PartySnapshot party, IGenerationRules rules)
    {
        PokemonSnapshot[] battlers = [.. party.Battlers];
        HashSet<PokemonType> offensiveCoverage = OffensiveCoverage(battlers, [], rules);
        return new Baseline(
            DefensiveGaps(battlers, [], rules),
            offensiveCoverage,
            [.. rules.TypeChart.Types.Where(type =>
                !offensiveCoverage.Contains(type))],
            TypesOf(battlers),
            RolesOf(battlers));
    }

    // Team-hint candidates are classified from their battle-profile stats because they do
    // not have a live moveset yet. Classify current members by the same rule so a swap is
    // never awarded "role diversity" merely because the two sides used different models.
    private static HashSet<PokemonRole> RolesOf(
        IReadOnlyList<PokemonSnapshot> members) =>
    [
        .. members.Select(member => InferRole(member.BaseStats)),
    ];

    private static HashSet<PokemonType> TypesOf(IReadOnlyList<PokemonSnapshot> members) =>
    [
        .. members
            .SelectMany(member => new[] { member.PrimaryType, member.SecondaryType })
            .Where(type => type != PokemonType.None),
    ];

    private static HashSet<PokemonType> DefensiveGaps(
        IReadOnlyList<PokemonSnapshot> members,
        IReadOnlyList<PreparedCandidate> additions,
        IGenerationRules rules)
    {
        var gaps = new HashSet<PokemonType>();
        foreach (PokemonType attackingType in rules.TypeChart.Types)
        {
            double[] matchups =
            [
                .. members.Select(member => rules.GetDefensiveMultiplier(
                    attackingType,
                    member.PrimaryType,
                    member.SecondaryType,
                    member.AbilityId)),
                .. additions.Select(candidate => rules.TypeChart.GetMultiplier(
                    attackingType,
                    candidate.BattleProfile.PrimaryType,
                    candidate.BattleProfile.SecondaryType)),
            ];
            if (matchups.Any(multiplier => multiplier > 1) &&
                !matchups.Any(multiplier => multiplier < 1))
            {
                gaps.Add(attackingType);
            }
        }

        return gaps;
    }

    private static HashSet<PokemonType> OffensiveCoverage(
        IReadOnlyList<PokemonSnapshot> members,
        IReadOnlyList<PreparedCandidate> additions,
        IGenerationRules rules)
    {
        HashSet<PokemonType> attackTypes =
        [
            .. members
                .SelectMany(member => member.Moves)
                .Where(move => !move.IsEmpty &&
                    rules.GetMoveCategory(move.MoveId, move.Type) != MoveCategory.Status &&
                    rules.CanProvideSuperEffectiveCoverage(move.MoveId))
                .Select(move => move.Type),
            .. additions.SelectMany(candidate => candidate.LevelUpAttackTypes),
        ];
        return TypesCovered(attackTypes, rules);
    }

    private static HashSet<PokemonType> TypesCovered(
        IEnumerable<PokemonType> attackTypes,
        IGenerationRules rules) =>
    [
        .. rules.TypeChart.Types.Where(defendingType =>
            attackTypes.Any(attackingType =>
                rules.TypeChart.GetMultiplier(attackingType, defendingType) > 1)),
    ];

    private static int PracticalityPoints(EncounterCandidate encounter, int averageLevel)
    {
        // Levels in either direction cost something, and they do not cost the same. Below the
        // party is grinding, which is time; above it is a catch that fights back, which is
        // the harder problem, so it is charged twice as much per level. Only the far side of
        // this was counted before, which is how a Lv.40 Sudowoodo reached a Lv.5 team for
        // free. See D-062.
        int grindLevels = Math.Max(0, averageLevel - encounter.MaximumLevel);
        int reachLevels = Math.Max(0, encounter.MinimumLevel - averageLevel);
        int cost = Math.Min(10, (grindLevels / 3) + ((reachLevels * 2) / 3));
        cost += encounter.EncounterRatePercent switch
        {
            null => 1,
            < 5 => 5,
            < 10 => 3,
            < 20 => 1,
            _ => 0,
        };
        cost += encounter.Method switch
        {
            EncounterMethod.Grass or EncounterMethod.Cave => 0,
            EncounterMethod.DarkGrass or EncounterMethod.Surf => 1,
            EncounterMethod.Fishing or EncounterMethod.ShakingGrass => 2,
            EncounterMethod.DustCloud or EncounterMethod.RipplingWater => 3,
            EncounterMethod.Static => 4,
            EncounterMethod.Fossil => 5,
            EncounterMethod.InGameTrade => 6,
            EncounterMethod.BridgeShadow => 2,
            EncounterMethod.Roaming => 7,
            _ => 0,
        };
        if (!string.IsNullOrWhiteSpace(encounter.Requirement))
        {
            cost += 2;
        }

        return Math.Max(-5, 12 - cost);
    }

    private static PokemonRole InferRole(StatBlock stats)
    {
        double bulk = (stats.Hp + stats.Defense + stats.SpecialDefense) / 3.0;
        int bestOffense = Math.Max(stats.Attack, stats.SpecialAttack);
        if (bulk >= 95 && bulk >= bestOffense * 0.9)
        {
            if (stats.Defense >= stats.SpecialDefense * 1.25)
            {
                return PokemonRole.PhysicalWall;
            }

            if (stats.SpecialDefense >= stats.Defense * 1.25)
            {
                return PokemonRole.SpecialWall;
            }

            return PokemonRole.MixedWall;
        }

        if (stats.Attack >= stats.SpecialAttack * 1.15)
        {
            return PokemonRole.PhysicalAttacker;
        }

        if (stats.SpecialAttack >= stats.Attack * 1.15)
        {
            return PokemonRole.SpecialAttacker;
        }

        return PokemonRole.MixedAttacker;
    }

    private static TeamHintCandidate PublicCandidate(PreparedCandidate candidate, int score) =>
        new(
            candidate.Encounter.SpeciesId,
            candidate.Encounter.SpeciesName,
            candidate.Encounter.Location,
            candidate.Encounter.Method,
            candidate.Encounter.MinimumLevel,
            candidate.Encounter.MaximumLevel,
            candidate.RecommendedLevel,
            candidate.Encounter.Requirement,
            candidate.Encounter.EncounterRatePercent,
            candidate.Encounter.IsLimited,
            candidate.CatchProfile.PrimaryType,
            candidate.CatchProfile.SecondaryType,
            candidate.BattleSpeciesId,
            candidate.BattleSpeciesName,
            candidate.Role,
            candidate.FinalEvolution,
            SortedTypes(candidate.LevelUpAttackTypes),
            SortedTypes(candidate.CoveredTypes),
            SortedTypes(candidate.DefensiveAnswers),
            score,
            CandidateReason(candidate));

    private static string CandidateReason(PreparedCandidate candidate)
    {
        var reasons = new List<string>();
        if (candidate.DefensiveAnswers.Count > 0)
        {
            reasons.Add($"resists {TypeNames(candidate.DefensiveAnswers)}");
        }

        if (candidate.CoveredTypes.Count > 0)
        {
            reasons.Add($"level-up attacks add coverage against {TypeNames(candidate.CoveredTypes)}");
        }

        if (candidate.BattleSpeciesId != candidate.Encounter.SpeciesId)
        {
            reasons.Add($"is expected to be {candidate.BattleSpeciesName} by Lv.{candidate.RecommendedLevel}");
        }

        if (reasons.Count == 0)
        {
            reasons.Add($"adds a {candidate.Role.ToString().Replace("Attacker", " attacker", StringComparison.Ordinal)} option");
        }

        string catchLevel = candidate.Encounter.MinimumLevel == candidate.Encounter.MaximumLevel
            ? $"Lv.{candidate.Encounter.MinimumLevel}"
            : $"Lv.{candidate.Encounter.MinimumLevel}–{candidate.Encounter.MaximumLevel}";
        reasons.Add($"catchable at {candidate.Encounter.Location} around {catchLevel}");
        return char.ToUpperInvariant(reasons[0][0]) + string.Join("; ", reasons)[1..] + ".";
    }

    private static string PlanSummary(
        IReadOnlyList<TeamHintCandidate> candidates,
        TeamHintReplacement? replacement,
        int closedDefensive,
        int newOffensive)
    {
        string names = string.Join(", ", candidates.Select(candidate => candidate.SpeciesName));
        string action = replacement is null
            ? $"Add {names}"
            : $"Replace {replacement.SpeciesName} with {names}";
        return $"{action}: closes {closedDefensive} defensive gap(s) and adds "
            + $"super-effective coverage against {newOffensive} type(s).";
    }

    private static string PracticalityExplanation(IReadOnlyList<PreparedCandidate> additions)
    {
        string locations = string.Join(", ", additions
            .Select(candidate => candidate.Encounter.Location)
            .Distinct(StringComparer.Ordinal));
        int extraCatchCost = Math.Max(0, additions.Count - 1) * 2;
        return extraCatchCost == 0
            ? $"Already obtainable; catch and training cost is based on {locations}."
            : $"Average convenience across {locations}, minus {extraCatchCost} points for "
                + $"committing to {additions.Count} catches.";
    }

    /// <summary>
    /// Convenience is a quality of a plan, not a reward paid once per Pokémon. Averaging
    /// prevents three merely easy catches from beating one useful catch by arithmetic;
    /// the small commitment cost then makes each extra addition earn its place through
    /// real coverage, where gains already have diminishing returns because types are sets.
    /// </summary>
    private static int PlanPracticalityPoints(IReadOnlyList<PreparedCandidate> additions)
    {
        int averageConvenience = (int)Math.Round(
            additions.Average(candidate => candidate.PracticalityPoints));
        int commitmentCost = Math.Max(0, additions.Count - 1) * 2;
        return averageConvenience - commitmentCost;
    }

    private static string CoverageExplanation(
        int gained,
        int lost,
        string gainedName,
        string lostName)
    {
        string gain = $"{gained} {Plural(gained, gainedName)}";
        return lost == 0
            ? gain + "."
            : $"{gain}; {lost} {Plural(lost, lostName)}.";
    }

    private static string Plural(int count, string singular) =>
        count == 1 ? singular : singular + "s";

    private static IReadOnlyList<PokemonType> SortedTypes(IEnumerable<PokemonType> types) =>
    [
        .. types.Distinct().OrderBy(type => (int)type),
    ];

    private static string TypeNames(IEnumerable<PokemonType> types) =>
        string.Join(", ", SortedTypes(types));

    private static IEnumerable<PreparedCandidate[]> Combinations(
        IReadOnlyList<PreparedCandidate> source,
        int count)
    {
        var chosen = new PreparedCandidate[count];
        return Walk(0, 0);

        IEnumerable<PreparedCandidate[]> Walk(int sourceIndex, int chosenIndex)
        {
            if (chosenIndex == count)
            {
                yield return [.. chosen];
                yield break;
            }

            int remainingNeeded = count - chosenIndex;
            for (int index = sourceIndex; index <= source.Count - remainingNeeded; index++)
            {
                chosen[chosenIndex] = source[index];
                foreach (PreparedCandidate[] combination in Walk(index + 1, chosenIndex + 1))
                {
                    yield return combination;
                }
            }
        }
    }

    private static bool HasNoExclusiveGroupConflict(
        IReadOnlyList<PreparedCandidate> candidates) =>
        candidates
            .Select(candidate => candidate.Encounter.ExclusiveGroup)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Count()
        == candidates.Count(candidate =>
            !string.IsNullOrWhiteSpace(candidate.Encounter.ExclusiveGroup));

    private bool HasNoEvolutionLineConflict(
        GameIdentity game,
        IReadOnlyList<PreparedCandidate> candidates)
    {
        for (int left = 0; left < candidates.Count; left++)
        {
            for (int right = left + 1; right < candidates.Count; right++)
            {
                if (AreInSameEvolutionLine(
                    game,
                    candidates[left].Encounter.SpeciesId,
                    candidates[right].Encounter.SpeciesId))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The source exposes forward evolutions, so ancestry is checked in both directions.
    /// This covers current/evolved pairs without relying on consecutive Pokédex numbers.
    /// </summary>
    private bool AreInSameEvolutionLine(GameIdentity game, int firstSpeciesId, int secondSpeciesId) =>
        firstSpeciesId == secondSpeciesId ||
        CanEvolveInto(game, firstSpeciesId, secondSpeciesId) ||
        CanEvolveInto(game, secondSpeciesId, firstSpeciesId);

    private bool CanEvolveInto(GameIdentity game, int fromSpeciesId, int intoSpeciesId)
    {
        var frontier = new Queue<(int SpeciesId, int Depth)>();
        var visited = new HashSet<int> { fromSpeciesId };
        frontier.Enqueue((fromSpeciesId, 0));

        while (frontier.TryDequeue(out (int SpeciesId, int Depth) current))
        {
            if (current.Depth >= MaximumEvolutionDepth)
            {
                continue;
            }

            foreach (EvolutionStep step in _sources.Evolutions
                .FindEvolutions(game, current.SpeciesId))
            {
                if (step.IntoSpeciesId == intoSpeciesId)
                {
                    return true;
                }

                // Byproducts belong to the family too (Nincada and Shedinja), even though
                // they are not followed as a final-form projection.
                if (visited.Add(step.IntoSpeciesId))
                {
                    frontier.Enqueue((step.IntoSpeciesId, current.Depth + 1));
                }
            }
        }

        return false;
    }

    private sealed record Baseline(
        HashSet<PokemonType> DefensiveGaps,
        HashSet<PokemonType> OffensiveCoverage,
        HashSet<PokemonType> OffensiveGaps,
        HashSet<PokemonType> Types,
        HashSet<PokemonRole> Roles);

    private sealed record PreparedCandidate(
        EncounterCandidate Encounter,
        int RecommendedLevel,
        int BattleSpeciesId,
        string BattleSpeciesName,
        SpeciesBattleProfile CatchProfile,
        SpeciesBattleProfile BattleProfile,
        PokemonRole Role,
        TeamHintEvolutionProjection? FinalEvolution,
        HashSet<PokemonType> LevelUpAttackTypes,
        HashSet<PokemonType> CoveredTypes,
        HashSet<PokemonType> DefensiveAnswers,
        int PracticalityPoints,
        int IndividualScore);

    private sealed record ReachableLine(
        int SpeciesId,
        string SpeciesName,
        IReadOnlyList<ReachableStage> Stages);

    private sealed record ReachableStage(
        int SpeciesId,
        int EntryLevel,
        int MaximumLearnLevel,
        bool IsCaughtStage);

    private sealed record MoveTypeOption(
        PokemonType Type,
        int Power,
        int SourceIndex,
        HashSet<PokemonType> Targets);
}
