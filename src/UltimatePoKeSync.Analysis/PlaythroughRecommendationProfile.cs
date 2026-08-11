using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

public sealed class PlaythroughRecommendationProfile : IRecommendationProfile
{
    /// <summary>
    /// How many candidates to keep. A Gen 3 Pokémon can often learn fifty or more once
    /// machines and tutors are counted; the panel has to stay readable.
    /// </summary>
    private const int MoveCandidateLimit = 14;

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

        // Level-up alone is a poor answer for a playthrough: in Gen 3 the coverage a team
        // actually needs arrives on TMs. Machines and tutors are ranked alongside, and the
        // pool stays bounded because the build only ever picks four. See D-030.
        MoveRecommendation[] learnable =
        [
            .. context.Learnsets
                .FindLearnableMoves(context.Game, member.SpeciesId, member.Level)
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
                .ThenBy(candidate => candidate.Method)
                .ThenByDescending(candidate => candidate.LearnedAtLevel ?? 0)
                .Take(Math.Max(0, MoveCandidateLimit - current.Length))
                .Select(candidate => new MoveRecommendation(
                    candidate.Move,
                    Describe(candidate.Method),
                    RecommendationAvailability.RequiresAvailabilityCheck,
                    candidate.LearnedAtLevel)),
        ];

        MoveRecommendation[] candidates = [.. current, .. learnable];

        return new PokemonRecommendation(
            member,
            Kind,
            context.RoleAnalysis,
            RecommendationPolicy.RecommendNature(context.RoleAnalysis, context.Rules),
            RecommendationPolicy.RecommendPlaythroughEvs(context.RoleAnalysis.Role),
            candidates,
            RecommendationPolicy.RecommendPlaythroughItems(member),
            preset,
            RecommendationPolicy.SelectBuild(context, candidates));
    }

    private static MoveCandidateSource Describe(MoveLearnMethod method) => method switch
    {
        MoveLearnMethod.Machine => MoveCandidateSource.Machine,
        MoveLearnMethod.Tutor => MoveCandidateSource.Tutor,
        _ => MoveCandidateSource.LevelUpLearnset,
    };

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
