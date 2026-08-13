using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.Analysis;

/// <summary>Computes shared facts once, then delegates policy to the selected profile.</summary>
public sealed class PokemonRecommendationEngine
{
    private readonly TeamAnalyzer _teamAnalyzer;
    private readonly PokemonRoleAnalyzer _roleAnalyzer;
    private readonly IGenerationRulesResolver _rulesResolver;
    private readonly IReadOnlyDictionary<PokemonGeneration, IReferencePresetCatalog> _presetCatalogs;
    private readonly IReadOnlyDictionary<PokemonGeneration, IMoveReferenceCatalog> _moveCatalogs;
    private readonly IMoveLearnSource _learnsets;
    private readonly IEvolutionSource _evolutions;
    private readonly IReadOnlyDictionary<RecommendationProfileKind, IRecommendationProfile> _profiles;

    /// <summary>
    /// The usual composition. The game data sources are injected because they are per game
    /// and PKHeX-backed, and Analysis must not depend on PKHeX. See D-007 and D-027.
    /// </summary>
    public static PokemonRecommendationEngine CreateDefault(GameDataSources sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return new(
            new TeamAnalyzer(),
            new PokemonRoleAnalyzer(sources),
            GenerationRulesResolver.Default,
            [ShowdownGen3PresetCatalog.Instance, ShowdownGen5PresetCatalog.Instance],
            [ShowdownGen3MoveCatalog.Instance, ShowdownGen5MoveCatalog.Instance],
            sources.Learnsets,
            sources.Evolutions,
            [new PlaythroughRecommendationProfile(), new CompetitiveRecommendationProfile()]);
    }

    public PokemonRecommendationEngine(
        TeamAnalyzer teamAnalyzer,
        PokemonRoleAnalyzer roleAnalyzer,
        IGenerationRulesResolver rulesResolver,
        IEnumerable<IReferencePresetCatalog> presetCatalogs,
        IEnumerable<IMoveReferenceCatalog> moveCatalogs,
        IMoveLearnSource learnsets,
        IEvolutionSource evolutions,
        IEnumerable<IRecommendationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(teamAnalyzer);
        ArgumentNullException.ThrowIfNull(roleAnalyzer);
        ArgumentNullException.ThrowIfNull(rulesResolver);
        ArgumentNullException.ThrowIfNull(presetCatalogs);
        ArgumentNullException.ThrowIfNull(moveCatalogs);
        ArgumentNullException.ThrowIfNull(learnsets);
        ArgumentNullException.ThrowIfNull(evolutions);
        ArgumentNullException.ThrowIfNull(profiles);

        _teamAnalyzer = teamAnalyzer;
        _roleAnalyzer = roleAnalyzer;
        _rulesResolver = rulesResolver;
        // Keyed by generation for the same reason the profiles are keyed by kind: a
        // catalog knows which generation it is for, so nothing else has to be told.
        _presetCatalogs = presetCatalogs.ToDictionary(catalog => catalog.Generation);
        _moveCatalogs = moveCatalogs.ToDictionary(catalog => catalog.Generation);
        _learnsets = learnsets;
        _evolutions = evolutions;
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
        if (!_presetCatalogs.TryGetValue(party.Game.Generation, out IReferencePresetCatalog? presets) ||
            !_moveCatalogs.TryGetValue(party.Game.Generation, out IMoveReferenceCatalog? moves))
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

        // Members are built in party order rather than independently, and each one is told
        // what the team already answers: the types its current moves cover, plus whatever
        // the builds chosen before it added. Without this, six Pokémon each pick the same
        // move for the same hole and the team ends up no wider than one of them. See D-031.
        var answered = new HashSet<PokemonType>(
            teamAnalysis.OffensiveCoverage
                .Where(entry => entry.IsCovered)
                .Select(entry => entry.DefendingType));

        var recommendations = new List<PokemonRecommendation>(party.Count);

        // An egg gets no advice: it has no moves to change and cannot be sent out.
        foreach (PokemonSnapshot member in party.Battlers)
        {
            PokemonRoleAnalysis role = _roleAnalyzer.Analyze(member, party.Game);
            PokemonRecommendation recommendation = profile.Recommend(new RecommendationContext(
                party.Game,
                profileKind,
                teamAnalysis,
                role,
                rules,
                presets,
                moves,
                _learnsets,
                _evolutions,
                answered));

            recommendations.Add(recommendation);
            RecordAnswers(recommendation, rules, answered);
        }

        return new TeamRecommendation(teamAnalysis, profileKind, recommendations);
    }

    /// <summary>Marks every defending type this member's build now hits super effectively.</summary>
    private static void RecordAnswers(
        PokemonRecommendation recommendation,
        IGenerationRules rules,
        HashSet<PokemonType> answered)
    {
        foreach (BuildSlot slot in recommendation.Build.Slots)
        {
            MoveReference move = slot.Move.Move;
            if (!rules.CanProvideSuperEffectiveCoverage(move.MoveId) ||
                rules.GetMoveCategory(move.MoveId, move.Type) == MoveCategory.Status)
            {
                continue;
            }

            foreach (PokemonType defending in rules.TypeChart.Types)
            {
                if (rules.TypeChart.GetMultiplier(move.Type, defending) > 1)
                {
                    answered.Add(defending);
                }
            }
        }
    }
}
