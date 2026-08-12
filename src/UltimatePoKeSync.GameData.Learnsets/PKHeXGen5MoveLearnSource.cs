using PKHeX.Core;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// Every Gen 5 move source — level up, machines and tutors — read per game from PKHeX.
/// </summary>
/// <remarks>
/// <para>
/// The same shape as the Gen 3 source (D-027), and per game for the same reason: Black and
/// White teach a species at one level, Black 2 and White 2 at another, and merging them would
/// mean picking one and being wrong about the other.
/// </para>
/// <para>
/// Egg moves are absent here too. A Pokémon already in the party cannot acquire one — it had
/// to hatch with it — so offering it would be exactly the false certainty D-025 exists to
/// prevent.
/// </para>
/// <para>
/// Tutors are where the two pairs really diverge: Black and White have almost none, while
/// Black 2 and White 2 have four shops full of them. That falls out of asking the right
/// game rather than being special-cased.
/// </para>
/// </remarks>
public sealed class PKHeXGen5MoveLearnSource : IMoveLearnSource
{
    private const int FirstGen5Species = 1;
    private const int LastGen5Species = 649;

    /// <summary>Enough room for every Gen 5 move index PKHeX may flag.</summary>
    private const int MoveFlagCount = 600;

    private static readonly Lazy<PKHeXGen5MoveLearnSource> LazyInstance =
        new(() => new PKHeXGen5MoveLearnSource(ShowdownGen5MoveCatalog.Instance));

    private readonly IMoveReferenceCatalog _moveCatalog;

    public PKHeXGen5MoveLearnSource(IMoveReferenceCatalog moveCatalog)
    {
        ArgumentNullException.ThrowIfNull(moveCatalog);
        if (moveCatalog.Generation != PokemonGeneration.Gen5)
        {
            throw new ArgumentException(
                $"A Gen 5 move source needs a Gen 5 move catalog, got {moveCatalog.Generation}.",
                nameof(moveCatalog));
        }

        _moveCatalog = moveCatalog;
    }

    public static PKHeXGen5MoveLearnSource Instance => LazyInstance.Value;

    public string SourceName => "PKHeX.Core";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen5 && ResolveSource(game.GameCode) is not null;
    }

    public IReadOnlyList<LearnableMove> FindLearnableMoves(
        GameIdentity game,
        int speciesId,
        int maximumLevel)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (maximumLevel is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLevel));
        }

        ILearnSource? source = ResolveSource(game.GameCode);
        if (source is null)
        {
            throw new NotSupportedException($"No Gen 5 move source is available for {game}.");
        }

        if (speciesId is < FirstGen5Species or > LastGen5Species)
        {
            return [];
        }

        var species = (ushort)speciesId;
        var seen = new HashSet<int>();
        var result = new List<LearnableMove>();

        AddLevelUpMoves(source, species, maximumLevel, seen, result);
        AddFlagged(source, species, maximumLevel, MoveSourceType.Machine, MoveLearnMethod.Machine, seen, result);
        AddFlagged(source, species, maximumLevel, MoveSourceType.AllTutors, MoveLearnMethod.Tutor, seen, result);

        return result;
    }

    /// <summary>
    /// Level-up moves come from the learnset rather than the flag scan, because it is the
    /// only source that also carries the level, and the level is what a player needs.
    /// </summary>
    private void AddLevelUpMoves(
        ILearnSource source,
        ushort species,
        int maximumLevel,
        HashSet<int> seen,
        List<LearnableMove> result)
    {
        Learnset learnset = GetLearnset(source, species);
        ReadOnlySpan<ushort> moves = learnset.GetAllMoves();
        ReadOnlySpan<byte> levels = learnset.GetAllLevels();

        for (int index = 0; index < moves.Length; index++)
        {
            if (levels[index] > maximumLevel || !seen.Add(moves[index]))
            {
                continue;
            }

            MoveReference? move = _moveCatalog.Find(moves[index]);
            if (move is not null)
            {
                result.Add(new LearnableMove(move, MoveLearnMethod.LevelUp, levels[index]));
            }
        }
    }

    private void AddFlagged(
        ILearnSource source,
        ushort species,
        int maximumLevel,
        MoveSourceType sourceType,
        MoveLearnMethod method,
        HashSet<int> seen,
        List<LearnableMove> result)
    {
        var pokemon = new PK5 { Species = species, CurrentLevel = (byte)maximumLevel };
        var evolution = new EvoCriteria
        {
            Species = species,
            Form = 0,
            LevelMin = 1,
            LevelMax = (byte)maximumLevel,
        };

        Span<bool> flags = stackalloc bool[MoveFlagCount];
        source.GetAllMoves(flags, pokemon, evolution, sourceType);

        for (int moveId = 0; moveId < flags.Length; moveId++)
        {
            if (!flags[moveId] || !seen.Add(moveId))
            {
                continue;
            }

            MoveReference? move = _moveCatalog.Find(moveId);
            if (move is not null)
            {
                result.Add(new LearnableMove(move, method, null));
            }
        }
    }

    private static Learnset GetLearnset(ILearnSource source, ushort species) => source switch
    {
        LearnSource5BW blackWhite => blackWhite.GetLearnset(species, 0),
        LearnSource5B2W2 sequel => sequel.GetLearnset(species, 0),
        _ => throw new NotSupportedException($"Unhandled learn source {source.GetType().Name}."),
    };

    /// <summary>
    /// Language does not change a learnset, so every localisation of a pair shares one
    /// source — but each code is listed rather than matched by prefix, because an
    /// unrecognised game must be refused rather than guessed at (D-005).
    /// </summary>
    private static ILearnSource? ResolveSource(string gameCode) => gameCode switch
    {
        // Black, then White.
        "IRBO" or "IRBE" or "IRBJ" or "IRBI" or "IRBS" or "IRBF" or "IRBD" or "IRBK" or
        "IRAO" or "IRAE" or "IRAJ" or "IRAI" or "IRAS" or "IRAF" or "IRAD" or "IRAK" =>
            LearnSource5BW.Instance,

        // Black 2, then White 2.
        "IREO" or "IREE" or "IREJ" or "IREI" or "IRES" or "IREF" or "IRED" or "IREK" or
        "IRDO" or "IRDE" or "IRDJ" or "IRDI" or "IRDS" or "IRDF" or "IRDD" or "IRDK" =>
            LearnSource5B2W2.Instance,

        _ => null,
    };
}
