using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

public sealed class CompetitiveRecommendationProfile : IRecommendationProfile
{
    public RecommendationProfileKind Kind => RecommendationProfileKind.Competitive;

    public PokemonRecommendation Recommend(RecommendationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ReferencePreset? preset = RecommendationPolicy.MatchPreset(context);
        NatureRecommendation nature =
            RecommendationPolicy.RecommendNature(context.RoleAnalysis, context.Rules);
        MoveRecommendation[] moves = preset is null
            ?
            [
                .. context.RoleAnalysis.Member.Moves
                    .Where(move => move.MoveId > 0)
                    .Select(move => context.MoveCatalog.Find(move.Name) ??
                        new MoveReference(move.MoveId, move.Name, move.Name, move.Type))
                    .DistinctBy(move => move.ReferenceId)
                    .Select(move => new MoveRecommendation(
                        move,
                        MoveCandidateSource.CurrentMoveset,
                        RecommendationAvailability.KnownAvailable,
                        null)),
            ]
            :
            [
                .. preset.MovePool
                    .Select(context.MoveCatalog.Find)
                    .OfType<MoveReference>()
                    .DistinctBy(move => move.ReferenceId)
                    .Select(move => new MoveRecommendation(
                        move,
                        MoveCandidateSource.ReferencePreset,
                        RecommendationAvailability.CompetitiveReference,
                        null)),
            ];

        return new PokemonRecommendation(
            context.RoleAnalysis.Member,
            Kind,
            context.RoleAnalysis,
            nature,
            RecommendationPolicy.RecommendCompetitiveEvs(
                context.RoleAnalysis,
                context.Rules,
                nature.PreferredNatures[0]),
            moves,
            RecommendationPolicy.RecommendCompetitiveItems(context.RoleAnalysis.Role),
            preset);
    }
}
