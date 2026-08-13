using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

public sealed class PlaythroughRecommendationProfile : IRecommendationProfile
{
    /// <summary>
    /// How many ranked candidates to show. A Gen 3 Pokémon can often learn fifty or more
    /// once machines and tutors are counted; the build sees all of them, while the panel
    /// stays readable.
    /// </summary>
    private const int MoveCandidateLimit = 14;

    /// <summary>
    /// How far past the current level a level-up move still counts as a plan rather than a
    /// wish. Four levels is about one evening of play, and a Pokémon one level away from its
    /// first same-type attack should be told to keep a slot for it rather than handed a
    /// filler it already knows.
    /// </summary>
    private const int UpcomingLevels = 4;

    public RecommendationProfileKind Kind => RecommendationProfileKind.Playthrough;

    public PokemonRecommendation Recommend(RecommendationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ReferencePreset? preset = RecommendationPolicy.MatchPreset(context);
        PokemonSnapshot member = context.RoleAnalysis.Member;

        MoveRecommendation[] current =
        [
            .. member.Moves
                .Where(move => move.MoveId > 0)
                .Select(move => context.MoveCatalog.Find(move.Name) ??
                    new MoveReference(move.MoveId, move.Name, move.Name, move.Type))
                .DistinctBy(move => move.ReferenceId)
                .Select(move => new MoveRecommendation(
                    move,
                    MoveCandidateSource.CurrentMoveset,
                    RecommendationAvailability.KnownAvailable,
                    null)),
        ];

        // Level-up alone is a poor answer for a playthrough: in Gen 3 the coverage a team
        // actually needs arrives on TMs. Machines and tutors are ranked alongside, and the
        // full pool reaches the build before the visible shortlist is capped. See D-030 and
        // D-051.
        int horizon = Horizon(context, member);

        MoveRecommendation[] learnable =
        [
            .. context.Learnsets
                .FindLearnableMoves(context.Game, member.SpeciesId, horizon)
                .Where(candidate => current.All(
                    existing => existing.Move.ReferenceId != candidate.Move.ReferenceId))
                .Select(candidate => new MoveRecommendation(
                    candidate.Move,
                    Describe(candidate.Method),
                    Availability(candidate, member.Level),
                    candidate.LearnedAtLevel)),
        ];

        MoveRecommendation[] fullPool = [.. current, .. learnable];
        RecommendedBuild build = RecommendationPolicy.SelectBuild(
            context,
            fullPool,
            MoveCandidateLimit);
        MoveRecommendation[] candidates = [.. build.Moves, .. build.Alternatives];

        return new PokemonRecommendation(
            member,
            Kind,
            context.RoleAnalysis,
            RecommendationPolicy.RecommendNature(context.RoleAnalysis, context.Rules),
            RecommendationPolicy.RecommendPlaythroughEvs(context.RoleAnalysis.Role),
            candidates,
            RecommendationPolicy.RecommendPlaythroughItems(member),
            preset,
            build);
    }

    private static MoveCandidateSource Describe(MoveLearnMethod method) => method switch
    {
        MoveLearnMethod.Machine => MoveCandidateSource.Machine,
        MoveLearnMethod.Tutor => MoveCandidateSource.Tutor,
        _ => MoveCandidateSource.LevelUpLearnset,
    };

    /// <summary>
    /// The highest level whose level-up moves are worth naming: a few past the current one,
    /// but never beyond the level the species evolves at.
    /// </summary>
    /// <remarks>
    /// Past the evolution the Pokémon follows a different learnset, so this species' later
    /// entries are a false promise with a precise number attached, which is the thing D-037
    /// exists to prevent. Only an evolution that happens by levelling alone bounds anything:
    /// a Pokémon waiting for a stone or a trade may never evolve at all, and its own table
    /// keeps answering.
    /// </remarks>
    private static int Horizon(RecommendationContext context, PokemonSnapshot member)
    {
        int[] levels =
        [
            .. context.Evolutions
                .FindEvolutions(context.Game, member.SpeciesId)
                .Where(step => !step.IsByproduct && IsEligible(member, step))
                .Where(step => step.HappensByLevellingAlone)
                .Select(step => step.Level!.Value)
        ];

        int horizon = Math.Min(100, member.Level + UpcomingLevels);
        if (levels.Any(level => level <= member.Level))
        {
            // A cancelled level evolution is offered again on the next level-up, so this
            // species cannot promise any move beyond the level it has now. See D-037.
            return member.Level;
        }

        // A move learned at the evolution level is offered before the evolution resolves,
        // so that level still belongs to the current species' useful horizon.
        return levels.Length == 0 ? horizon : Math.Min(horizon, levels.Min());
    }

    /// <summary>
    /// A level-up move it has not reached yet costs only levels, so it is not something to
    /// go and check for in this save. Everything else still is. See D-025.
    /// </summary>
    private static RecommendationAvailability Availability(LearnableMove candidate, int level) =>
        candidate is { Method: MoveLearnMethod.LevelUp, LearnedAtLevel: int learnedAt } &&
        learnedAt > level
            ? RecommendationAvailability.ArrivesWithLevelUp
            : RecommendationAvailability.RequiresAvailabilityCheck;

    private static bool IsEligible(PokemonSnapshot member, EvolutionStep evolution) =>
        evolution.RequiredGender is null || evolution.RequiredGender == member.Gender;
}
