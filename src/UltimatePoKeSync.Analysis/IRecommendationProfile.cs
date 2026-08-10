using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

public interface IRecommendationProfile
{
    RecommendationProfileKind Kind { get; }

    PokemonRecommendation Recommend(RecommendationContext context);
}

public sealed record RecommendationContext(
    TeamAnalysis TeamAnalysis,
    PokemonRoleAnalysis RoleAnalysis,
    IGenerationRules Rules,
    IReferencePresetCatalog PresetCatalog,
    IMoveReferenceCatalog MoveCatalog);
