using PKHeX.Core;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// Gen 3 level-up learnsets, one table per game, read from PKHeX.
/// </summary>
/// <remarks>
/// <para>
/// PKHeX already ships a separate learn source for every game of every generation, and it
/// is already a dependency for parsing. Using it means no second dataset to bundle, pin
/// and keep honest, and the same class shape will serve Gen 1 through Gen 9 when those
/// generations arrive. See D-027.
/// </para>
/// <para>
/// The game-code map is deliberately duplicated from <c>Gen3PartyParser</c> rather than
/// shared: one maps a ROM to a stat table, the other maps it to a learnset table, and the
/// day a hack or a localisation needs one without the other, they must be free to
/// disagree.
/// </para>
/// </remarks>
public sealed class PKHeXGen3LevelUpLearnsets : ILevelUpLearnsetSource
{
    private const int FirstGen3Species = 1;
    private const int LastGen3Species = 386;

    private static readonly Lazy<PKHeXGen3LevelUpLearnsets> LazyInstance =
        new(() => new PKHeXGen3LevelUpLearnsets(ShowdownGen3MoveCatalog.Instance));

    private readonly IMoveReferenceCatalog _moveCatalog;

    public PKHeXGen3LevelUpLearnsets(IMoveReferenceCatalog moveCatalog)
    {
        ArgumentNullException.ThrowIfNull(moveCatalog);
        if (moveCatalog.Generation != PokemonGeneration.Gen3)
        {
            throw new ArgumentException(
                $"A Gen 3 learnset source needs a Gen 3 move catalog, got {moveCatalog.Generation}.",
                nameof(moveCatalog));
        }

        _moveCatalog = moveCatalog;
    }

    public static PKHeXGen3LevelUpLearnsets Instance => LazyInstance.Value;

    public string SourceName => "PKHeX.Core";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen3 && ResolveSource(game.GameCode) is not null;
    }

    public IReadOnlyList<LevelUpMoveReference> FindLevelUpMoves(
        GameIdentity game,
        int speciesId,
        int maximumLevel)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (maximumLevel is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLevel));
        }

        Func<ushort, Learnset>? source = ResolveSource(game.GameCode);
        if (source is null)
        {
            throw new NotSupportedException($"No Gen 3 learnset table is available for {game}.");
        }

        if (speciesId is < FirstGen3Species or > LastGen3Species)
        {
            return [];
        }

        Learnset learnset = source((ushort)speciesId);
        ReadOnlySpan<ushort> moves = learnset.GetAllMoves();
        ReadOnlySpan<byte> levels = learnset.GetAllLevels();

        // Source order is the order the game itself teaches them, which is what a player
        // sees. A move that appears twice keeps its earliest level.
        var seen = new HashSet<int>();
        var result = new List<LevelUpMoveReference>(moves.Length);

        for (int index = 0; index < moves.Length; index++)
        {
            if (levels[index] > maximumLevel || !seen.Add(moves[index]))
            {
                continue;
            }

            MoveReference? move = _moveCatalog.Find(moves[index]);
            if (move is not null)
            {
                result.Add(new LevelUpMoveReference(move, levels[index]));
            }
        }

        return result;
    }

    private static Func<ushort, Learnset>? ResolveSource(string gameCode) => gameCode switch
    {
        // Every Western Emerald localisation shares one learnset table. See D-017.
        "BPEE" or "BPEF" or "BPED" or "BPES" or "BPEI" =>
            static species => LearnSource3E.Instance.GetLearnset(species, 0),
        "BPRE" => static species => LearnSource3FR.Instance.GetLearnset(species, 0),
        "BPGE" => static species => LearnSource3LG.Instance.GetLearnset(species, 0),
        "AXVE" or "AXPE" => static species => LearnSource3RS.Instance.GetLearnset(species, 0),
        _ => null,
    };
}
