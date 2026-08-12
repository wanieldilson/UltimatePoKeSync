using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>
/// What is about to happen to a Pokémon: the moves the next few levels bring, and what it
/// turns into.
/// </summary>
/// <remarks>
/// The best set at level 100 answers a question a player in the middle of a story is not
/// asking. The one they are asking is whether to keep walking before the next Gym, and that
/// is answered by a level number. Both use the same per-game learn source (D-027), so this
/// costs nothing beyond the filtering. See D-037.
/// </remarks>
public sealed class PokemonProgressAnalyzer
{
    /// <summary>Four fits a card without scrolling, and a fifth is too far off to plan for.</summary>
    private const int MaximumUpcomingMoves = 4;

    private readonly IMoveLearnSource _learnsets;
    private readonly IEvolutionSource _evolutions;

    public PokemonProgressAnalyzer(IMoveLearnSource learnsets, IEvolutionSource evolutions)
    {
        ArgumentNullException.ThrowIfNull(learnsets);
        ArgumentNullException.ThrowIfNull(evolutions);

        _learnsets = learnsets;
        _evolutions = evolutions;
    }

    /// <summary>
    /// Empty progress rather than an exception when a game is unsupported: this is the one
    /// card on the page that is allowed to have nothing to say.
    /// </summary>
    public PokemonProgress Analyze(GameIdentity game, PokemonSnapshot member)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(member);

        // An egg has no level to count from and no learnset of its own to follow. See D-036.
        if (!member.CanBattle || !_learnsets.Supports(game) || !_evolutions.Supports(game))
        {
            return PokemonProgress.Nothing;
        }

        EvolutionStep[] evolutions = [.. _evolutions.FindEvolutions(game, member.SpeciesId)];
        EvolutionStep? next = ChooseNext(evolutions, member.Level);

        // A level evolution already passed has been cancelled, over and over: the game
        // offers it again on every single level up, so the very next one changes the
        // species. Nothing beyond the current level can be promised for it.
        bool evolvesOnNextLevelUp = next?.HappensByLevellingAlone == true
            && next.Level <= member.Level;

        // Beyond a level evolution the list on screen would be the wrong species' moves:
        // once it evolves the game follows the new learnset, not this one.
        int lastUsefulLevel = next?.HappensByLevellingAlone switch
        {
            true when evolvesOnNextLevelUp => member.Level,
            true => next.Level!.Value,
            _ => 100,
        };

        List<UpcomingMove> upcoming = [];
        bool cutShort = false;

        LearnableMove[] currentLearnset =
        [
            .. _learnsets
                .FindLearnableMoves(game, member.SpeciesId, 100)
                .Where(entry => entry.Method == MoveLearnMethod.LevelUp
                    && entry.LearnedAtLevel is not null)
                .OrderBy(entry => entry.LearnedAtLevel),
        ];

        foreach (LearnableMove learnable in currentLearnset
            .Where(entry => entry.LearnedAtLevel > member.Level))
        {
            int at = learnable.LearnedAtLevel!.Value;
            if (at > lastUsefulLevel)
            {
                cutShort = true;
                break;
            }

            if (upcoming.Count == MaximumUpcomingMoves)
            {
                break;
            }

            upcoming.Add(new UpcomingMove(learnable.Move, at, at - member.Level));
        }

        List<EvolutionLineStage> evolutionLine =
        [
            new(member.SpeciesId, member.SpeciesName, null, string.Empty, true, 1),
        ];
        EvolutionStep? lineStep = next;
        int lineLevel = member.Level;
        double lineOpacity = 0.80;
        while (lineStep is not null && evolutionLine.Count < 3)
        {
            int? stageLevel = lineStep.Level;
            string distance = stageLevel is int levelAt
                ? levelAt <= lineLevel
                    ? "next level"
                    : levelAt - member.Level == 1
                        ? "1 level away"
                        : $"{levelAt - member.Level} levels away"
                : lineStep.Requirement;
            evolutionLine.Add(new EvolutionLineStage(
                lineStep.IntoSpeciesId,
                lineStep.IntoSpeciesName,
                stageLevel,
                distance,
                false,
                lineOpacity));
            lineLevel = stageLevel ?? lineLevel;
            lineStep = _evolutions.FindEvolutions(game, lineStep.IntoSpeciesId)
                .OrderBy(step => step.Level ?? int.MaxValue)
                .FirstOrDefault();
            lineOpacity = 0.55;
        }

        HashSet<int> knownMoveIds = [.. member.Moves.Where(move => !move.IsEmpty).Select(move => move.MoveId)];
        List<LearnsetTimelineEntry> timeline =
        [
            .. currentLearnset
                .Where(entry => entry.LearnedAtLevel <= member.Level || entry.LearnedAtLevel <= lastUsefulLevel)
                .Select(entry => new LearnsetTimelineEntry(
                    entry.Move,
                    entry.LearnedAtLevel!.Value,
                    knownMoveIds.Contains(entry.Move.MoveId),
                    false,
                    entry.LearnedAtLevel > member.Level
                        && entry.LearnedAtLevel == currentLearnset
                            .Where(candidate => candidate.LearnedAtLevel > member.Level)
                            .Select(candidate => candidate.LearnedAtLevel)
                            .FirstOrDefault(),
                    string.Empty)),
        ];

        if (next?.HappensByLevellingAlone == true && next.Level is int evolutionAt)
        {
            timeline.AddRange(_learnsets
                .FindLearnableMoves(game, next.IntoSpeciesId, Math.Min(100, evolutionAt + 20))
                .Where(entry => entry.Method == MoveLearnMethod.LevelUp
                    && entry.LearnedAtLevel > evolutionAt)
                .OrderBy(entry => entry.LearnedAtLevel)
                .Take(Math.Max(0, MaximumUpcomingMoves - timeline.Count(entry => !entry.IsKnown)))
                .Select(entry => new LearnsetTimelineEntry(
                    entry.Move,
                    entry.LearnedAtLevel!.Value,
                    false,
                    true,
                    false,
                    $"{next.IntoSpeciesName}'s learnset by then")));
        }

        timeline =
        [
            .. timeline
                .GroupBy(entry => (entry.Move.MoveId, entry.Level, entry.IsAfterEvolution))
                .Select(group => group.First())
                .OrderBy(entry => entry.Level)
                .Take(8),
        ];

        return new PokemonProgress(
            upcoming,
            next,
            [.. evolutions.Where(step => step != next)],
            cutShort,
            evolvesOnNextLevelUp,
            evolutionLine,
            timeline);
    }

    /// <summary>
    /// The one to name first. A level evolution still ahead wins, because it is the only
    /// one with a countdown; otherwise the first listed, which is the game's own order.
    /// </summary>
    private static EvolutionStep? ChooseNext(IReadOnlyList<EvolutionStep> evolutions, int level)
    {
        EvolutionStep? byLevel = evolutions
            .Where(step => step.HappensByLevellingAlone && step.Level > level)
            .OrderBy(step => step.Level)
            .FirstOrDefault();

        return byLevel ?? evolutions.FirstOrDefault();
    }
}

/// <param name="Moves">The next level-up moves, soonest first.</param>
/// <param name="NextEvolution">The one worth naming, or null if it does not evolve.</param>
/// <param name="OtherEvolutions">The rest of a branching line: Eevee has five.</param>
/// <param name="MovesStopAtEvolution">
/// Whether the move list was cut because the species will have changed by then.
/// </param>
/// <param name="EvolvesOnNextLevelUp">
/// Whether it is already past a level evolution it has been refusing, in which case the
/// next level up is the last one it spends as this species.
/// </param>
public sealed record PokemonProgress(
    IReadOnlyList<UpcomingMove> Moves,
    EvolutionStep? NextEvolution,
    IReadOnlyList<EvolutionStep> OtherEvolutions,
    bool MovesStopAtEvolution,
    bool EvolvesOnNextLevelUp,
    IReadOnlyList<EvolutionLineStage> EvolutionLine,
    IReadOnlyList<LearnsetTimelineEntry> Timeline)
{
    public static PokemonProgress Nothing { get; } = new([], null, [], false, false, [], []);

    public bool HasAnything => Moves.Count > 0 || NextEvolution is not null;
}

/// <param name="LevelsAway">How much walking is left, which is the actionable number.</param>
public sealed record UpcomingMove(MoveReference Move, int Level, int LevelsAway);

/// <summary>One card and its incoming arrow in the evolution line.</summary>
public sealed record EvolutionLineStage(
    int SpeciesId,
    string SpeciesName,
    int? EvolutionLevel,
    string Distance,
    bool IsCurrent,
    double Opacity);

/// <summary>One dot on the selected species' level-up rail.</summary>
public sealed record LearnsetTimelineEntry(
    MoveReference Move,
    int Level,
    bool IsKnown,
    bool IsAfterEvolution,
    bool IsNext,
    string Note);
