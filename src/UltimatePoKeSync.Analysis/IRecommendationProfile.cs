using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

public interface IRecommendationProfile
{
    RecommendationProfileKind Kind { get; }

    PokemonRecommendation Recommend(RecommendationContext context);
}

public sealed record RecommendationContext(
    GameIdentity Game,
    TeamAnalysis TeamAnalysis,
    PokemonRoleAnalysis RoleAnalysis,
    IGenerationRules Rules,
    IReferencePresetCatalog PresetCatalog,
    IMoveReferenceCatalog MoveCatalog,
    IMoveLearnSource Learnsets,
    IReadOnlySet<PokemonType> AnsweredTypes);
