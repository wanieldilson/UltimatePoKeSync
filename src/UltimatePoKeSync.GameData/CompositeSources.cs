using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// Several move sources behind one, each asked whether it recognises the game.
/// </summary>
/// <remarks>
/// The generations answer the same questions from entirely different tables, and the layers
/// above should not have to know how many there are. <see cref="IMoveLearnSource.Supports"/>
/// already existed for exactly this: dispatch is the sources' own answer, not a switch kept
/// somewhere else that has to be remembered when a generation is added. See D-043.
/// </remarks>
public sealed class CompositeMoveLearnSource : IMoveLearnSource
{
    private readonly IReadOnlyList<IMoveLearnSource> _sources;

    public CompositeMoveLearnSource(params IMoveLearnSource[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Length == 0)
        {
            throw new ArgumentException("At least one move source is needed.", nameof(sources));
        }

        _sources = sources;
    }

    public string SourceName => string.Join(" + ", _sources.Select(source => source.SourceName).Distinct());

    public bool Supports(GameIdentity game) => Find(game) is not null;

    public IReadOnlyList<LearnableMove> FindLearnableMoves(
        GameIdentity game,
        int speciesId,
        int maximumLevel) =>
        Find(game)?.FindLearnableMoves(game, speciesId, maximumLevel)
        ?? throw new NotSupportedException($"No move data is available for {game}.");

    private IMoveLearnSource? Find(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return _sources.FirstOrDefault(source => source.Supports(game));
    }
}

/// <summary>Several evolution sources behind one, dispatched the same way.</summary>
public sealed class CompositeEvolutionSource : IEvolutionSource
{
    private readonly IReadOnlyList<IEvolutionSource> _sources;

    public CompositeEvolutionSource(params IEvolutionSource[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Length == 0)
        {
            throw new ArgumentException("At least one evolution source is needed.", nameof(sources));
        }

        _sources = sources;
    }

    public string SourceName => string.Join(" + ", _sources.Select(source => source.SourceName).Distinct());

    public bool Supports(GameIdentity game) => Find(game) is not null;

    public IReadOnlyList<EvolutionStep> FindEvolutions(GameIdentity game, int speciesId) =>
        Find(game)?.FindEvolutions(game, speciesId)
        ?? throw new NotSupportedException($"No evolution data is available for {game}.");

    private IEvolutionSource? Find(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return _sources.FirstOrDefault(source => source.Supports(game));
    }
}
