using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>
/// Answers for battling: every move the species can legally end up with, ranked with the
/// reference sets as a prior.
/// </summary>
/// <remarks>
/// The pool is deliberately wider than the playthrough one, and read at level 100. A
/// competitive Pokémon is a trained one: nobody battles with the level 5 it was caught at,
/// and a move it learns at 45 is a move it will have. Ranking against only the reference
/// sets, as this profile used to, left every species without a Random Battle entry holding
/// whatever it happened to know. See D-032.
/// </remarks>
public sealed class CompetitiveRecommendationProfile : IRecommendationProfile
{
    /// <summary>A built Pokémon is a trained one, so the whole learnset is in reach.</summary>
    private const int TrainedLevel = 100;

    /// <summary>Enough to rank against without turning the alternatives list into a wall.</summary>
    private const int MoveCandidateLimit = 16;

    public RecommendationProfileKind Kind => RecommendationProfileKind.Competitive;

    public PokemonRecommendation Recommend(RecommendationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        PokemonSnapshot member = context.RoleAnalysis.Member;
        ReferencePreset? preset = RecommendationPolicy.MatchPreset(context);
        HashSet<string> presetMoves = preset is null ? [] : [.. preset.MovePool];
        NatureRecommendation nature =
            RecommendationPolicy.RecommendNature(context.RoleAnalysis, context.Rules);

        MoveRecommendation[] known =
        [
            .. member.Moves
                .Where(move => move.MoveId > 0)
                .Select(move => context.MoveCatalog.Find(move.Name) ??
                    new MoveReference(move.MoveId, move.Name, move.Name, move.Type))
                .DistinctBy(move => move.ReferenceId)
                .Select(move => new MoveRecommendation(
                    move,
                    MoveCandidateSource.CurrentMoveset,
                    RecommendationAvailability.CompetitiveReference,
                    null)),
        ];

        var seen = new HashSet<string>(known.Select(move => move.Move.ReferenceId));

        MoveRecommendation[] learnable =
        [
            .. context.Learnsets
                .FindLearnableMoves(context.Game, member.SpeciesId, TrainedLevel)
                .Where(candidate => seen.Add(candidate.Move.ReferenceId))
                .OrderByDescending(candidate => presetMoves.Contains(candidate.Move.ReferenceId))
                .ThenBy(candidate => candidate.Method)
                .Select(candidate => new MoveRecommendation(
                    candidate.Move,
                    Describe(candidate.Method),
                    RecommendationAvailability.CompetitiveReference,
                    candidate.LearnedAtLevel)),
        ];

        // A reference set can name a move the learn source does not reach: an egg move,
        // most often. Kept, and marked as coming from the set rather than from the game.
        MoveRecommendation[] fromPreset =
        [
            .. presetMoves
                .Select(context.MoveCatalog.Find)
                .OfType<MoveReference>()
                .Where(move => seen.Add(move.ReferenceId))
                .Select(move => new MoveRecommendation(
                    move,
                    MoveCandidateSource.ReferencePreset,
                    RecommendationAvailability.CompetitiveReference,
                    null)),
        ];

        MoveRecommendation[] candidates =
        [
            .. known,
            .. fromPreset,
            .. learnable.Take(Math.Max(0, MoveCandidateLimit - known.Length - fromPreset.Length)),
        ];

        return new PokemonRecommendation(
            member,
            Kind,
            context.RoleAnalysis,
            nature,
            RecommendationPolicy.RecommendCompetitiveEvs(
                context.RoleAnalysis,
                context.Rules,
                nature.PreferredNatures[0]),
            candidates,
            RecommendationPolicy.RecommendCompetitiveItems(context.RoleAnalysis.Role),
            preset,
            RecommendationPolicy.SelectBuild(context, candidates));
    }

    private static MoveCandidateSource Describe(MoveLearnMethod method) => method switch
    {
        MoveLearnMethod.Machine => MoveCandidateSource.Machine,
        MoveLearnMethod.Tutor => MoveCandidateSource.Tutor,
        _ => MoveCandidateSource.LevelUpLearnset,
    };
}
