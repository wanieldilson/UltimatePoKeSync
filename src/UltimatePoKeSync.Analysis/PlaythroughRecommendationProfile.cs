using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

public sealed class PlaythroughRecommendationProfile : IRecommendationProfile
{
    public RecommendationProfileKind Kind => RecommendationProfileKind.Playthrough;

    public PokemonRecommendation Recommend(RecommendationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ReferencePreset? preset = RecommendationPolicy.MatchPreset(context);
        HashSet<string> presetMoves = preset is null ? [] : [.. preset.MovePool];
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

        MoveRecommendation[] levelUp =
        [
            .. context.MoveCatalog.FindLevelUpMoves(member.SpeciesName, member.Level)
                .Where(candidate => current.All(
                    existing => existing.Move.ReferenceId != candidate.Move.ReferenceId))
                .OrderByDescending(candidate => presetMoves.Contains(candidate.Move.ReferenceId))
                .ThenByDescending(candidate => IsRoleAligned(
                    candidate.Move,
                    context.RoleAnalysis.Role,
                    context.Rules))
                .ThenByDescending(candidate =>
                    candidate.Move.Type == member.PrimaryType ||
                    candidate.Move.Type == member.SecondaryType)
                .ThenByDescending(candidate => candidate.LearnedAtLevel)
                .Take(Math.Max(0, 8 - current.Length))
                .Select(candidate => new MoveRecommendation(
                    candidate.Move,
                    MoveCandidateSource.LevelUpLearnset,
                    RecommendationAvailability.RequiresAvailabilityCheck,
                    candidate.LearnedAtLevel)),
        ];

        return new PokemonRecommendation(
            member,
            Kind,
            context.RoleAnalysis,
            RecommendationPolicy.RecommendNature(context.RoleAnalysis, context.Rules),
            RecommendationPolicy.RecommendPlaythroughEvs(context.RoleAnalysis.Role),
            [.. current, .. levelUp],
            RecommendationPolicy.RecommendPlaythroughItems(member),
            preset);
    }

    private static bool IsRoleAligned(
        MoveReference move,
        PokemonRole role,
        IGenerationRules rules)
    {
        MoveCategory category = rules.GetMoveCategory(move.MoveId, move.Type);
        return role switch
        {
            PokemonRole.PhysicalAttacker => category == MoveCategory.Physical,
            PokemonRole.SpecialAttacker => category == MoveCategory.Special,
            PokemonRole.MixedAttacker => category is MoveCategory.Physical or MoveCategory.Special,
            _ => category == MoveCategory.Status,
        };
    }
}
