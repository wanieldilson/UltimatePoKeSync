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
    private readonly IMoveLearnSource _learnsets;
    private readonly IReadOnlyDictionary<RecommendationProfileKind, IRecommendationProfile> _profiles;

    /// <summary>
    /// The usual composition. The learnset source is injected because it is per game and
    /// PKHeX-backed, and Analysis must not depend on PKHeX. See D-007 and D-027.
    /// </summary>
    public static PokemonRecommendationEngine CreateDefault(IMoveLearnSource learnsets) =>
        new(
            new TeamAnalyzer(),
            new PokemonRoleAnalyzer(),
            GenerationRulesResolver.Default,
            ShowdownGen3PresetCatalog.Instance,
            ShowdownGen3MoveCatalog.Instance,
            learnsets,
            [new PlaythroughRecommendationProfile(), new CompetitiveRecommendationProfile()]);

    public PokemonRecommendationEngine(
        TeamAnalyzer teamAnalyzer,
        PokemonRoleAnalyzer roleAnalyzer,
        IGenerationRulesResolver rulesResolver,
        IReferencePresetCatalog presetCatalog,
        IMoveReferenceCatalog moveCatalog,
        IMoveLearnSource learnsets,
        IEnumerable<IRecommendationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(teamAnalyzer);
        ArgumentNullException.ThrowIfNull(roleAnalyzer);
        ArgumentNullException.ThrowIfNull(rulesResolver);
        ArgumentNullException.ThrowIfNull(presetCatalog);
        ArgumentNullException.ThrowIfNull(moveCatalog);
        ArgumentNullException.ThrowIfNull(learnsets);
        ArgumentNullException.ThrowIfNull(profiles);

        _teamAnalyzer = teamAnalyzer;
        _roleAnalyzer = roleAnalyzer;
        _rulesResolver = rulesResolver;
        _presetCatalog = presetCatalog;
        _moveCatalog = moveCatalog;
        _learnsets = learnsets;
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

        // Learnsets are per game, not per generation: the same generation's games teach
        // the same move at different levels. See D-027.
        if (!_learnsets.Supports(party.Game))
        {
            throw new NotSupportedException($"No move data is available for {party.Game}.");
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
                    party.Game,
                    teamAnalysis,
                    role,
                    rules,
                    _presetCatalog,
                    _moveCatalog,
                    _learnsets));
            }),
        ];

        return new TeamRecommendation(teamAnalysis, profileKind, recommendations);
    }
}
