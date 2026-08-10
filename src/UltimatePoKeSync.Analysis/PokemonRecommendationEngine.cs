using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>Computes shared facts once, then delegates policy to the selected profile.</summary>
public sealed class PokemonRecommendationEngine
{
    private readonly TeamAnalyzer _teamAnalyzer;
    private readonly PokemonRoleAnalyzer _roleAnalyzer;
    private readonly IGenerationRulesResolver _rulesResolver;
    private readonly IReferencePresetCatalog _presetCatalog;
    private readonly IMoveReferenceCatalog _moveCatalog;
    private readonly IReadOnlyDictionary<RecommendationProfileKind, IRecommendationProfile> _profiles;

    public PokemonRecommendationEngine()
        : this(
            new TeamAnalyzer(),
            new PokemonRoleAnalyzer(),
            GenerationRulesResolver.Default,
            ShowdownGen3PresetCatalog.Instance,
            ShowdownGen3MoveCatalog.Instance,
            [new PlaythroughRecommendationProfile(), new CompetitiveRecommendationProfile()])
    {
    }

    public PokemonRecommendationEngine(
        TeamAnalyzer teamAnalyzer,
        PokemonRoleAnalyzer roleAnalyzer,
        IGenerationRulesResolver rulesResolver,
        IReferencePresetCatalog presetCatalog,
        IMoveReferenceCatalog moveCatalog,
        IEnumerable<IRecommendationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(teamAnalyzer);
        ArgumentNullException.ThrowIfNull(roleAnalyzer);
        ArgumentNullException.ThrowIfNull(rulesResolver);
        ArgumentNullException.ThrowIfNull(presetCatalog);
        ArgumentNullException.ThrowIfNull(moveCatalog);
        ArgumentNullException.ThrowIfNull(profiles);

        _teamAnalyzer = teamAnalyzer;
        _roleAnalyzer = roleAnalyzer;
        _rulesResolver = rulesResolver;
        _presetCatalog = presetCatalog;
        _moveCatalog = moveCatalog;
        _profiles = profiles.ToDictionary(profile => profile.Kind);

        foreach (RecommendationProfileKind kind in Enum.GetValues<RecommendationProfileKind>())
        {
            if (!_profiles.ContainsKey(kind))
            {
                throw new ArgumentException($"Missing recommendation profile: {kind}.", nameof(profiles));
            }
        }
    }

    public TeamRecommendation Recommend(
        PartySnapshot party,
        RecommendationProfileKind profileKind)
    {
        ArgumentNullException.ThrowIfNull(party);

        IGenerationRules rules = _rulesResolver.Resolve(party.Game.Generation)
            ?? throw new NotSupportedException(
                $"No recommendation rules are available for {party.Game.Generation}.");
        if (_presetCatalog.Generation != party.Game.Generation ||
            _moveCatalog.Generation != party.Game.Generation)
        {
            throw new NotSupportedException(
                $"No recommendation reference data is available for {party.Game.Generation}.");
        }

        TeamAnalysis teamAnalysis = _teamAnalyzer.Analyze(party);
        if (!_profiles.TryGetValue(profileKind, out IRecommendationProfile? profile))
        {
            throw new ArgumentOutOfRangeException(nameof(profileKind));
        }

        PokemonRecommendation[] recommendations =
        [
            .. party.Members.Select(member =>
            {
                PokemonRoleAnalysis role = _roleAnalyzer.Analyze(member, party.Game.Generation);
                return profile.Recommend(new RecommendationContext(
                    teamAnalysis,
                    role,
                    rules,
                    _presetCatalog,
                    _moveCatalog));
            }),
        ];

        return new TeamRecommendation(teamAnalysis, profileKind, recommendations);
    }
}
