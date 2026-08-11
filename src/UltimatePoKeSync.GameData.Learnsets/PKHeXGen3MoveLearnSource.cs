using PKHeX.Core;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// Every Gen 3 move source — level up, machines and tutors — read per game from PKHeX.
/// </summary>
/// <remarks>
/// <para>
/// PKHeX ships a separate learn source per game for every generation, and it is already a
/// dependency for parsing. Using it means no second dataset to bundle, pin and keep honest,
/// and the same class shape will serve Gen 1 through Gen 9. See D-027 and D-030.
/// </para>
/// <para>
/// Egg moves are deliberately absent. A Pokémon already in the party cannot acquire one:
/// it had to hatch with it. Offering it would be exactly the false certainty D-025 exists
/// to prevent.
/// </para>
/// <para>
/// The game-code map is deliberately duplicated from <c>Gen3PartyParser</c> rather than
/// shared: one maps a ROM to a stat table, the other maps it to move sources, and the day
/// a hack or a localisation needs one without the other, they must be free to disagree.
/// </para>
/// </remarks>
public sealed class PKHeXGen3MoveLearnSource : IMoveLearnSource
{
    private const int FirstGen3Species = 1;
    private const int LastGen3Species = 386;

    /// <summary>Enough room for every Gen 3 move index PKHeX may flag.</summary>
    private const int MoveFlagCount = 400;

    private static readonly Lazy<PKHeXGen3MoveLearnSource> LazyInstance =
        new(() => new PKHeXGen3MoveLearnSource(ShowdownGen3MoveCatalog.Instance));

    private readonly IMoveReferenceCatalog _moveCatalog;

    public PKHeXGen3MoveLearnSource(IMoveReferenceCatalog moveCatalog)
    {
        ArgumentNullException.ThrowIfNull(moveCatalog);
        if (moveCatalog.Generation != PokemonGeneration.Gen3)
        {
            throw new ArgumentException(
                $"A Gen 3 move source needs a Gen 3 move catalog, got {moveCatalog.Generation}.",
                nameof(moveCatalog));
        }

        _moveCatalog = moveCatalog;
    }

    public static PKHeXGen3MoveLearnSource Instance => LazyInstance.Value;

    public string SourceName => "PKHeX.Core";

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen3 && ResolveSource(game.GameCode) is not null;
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
            throw new NotSupportedException($"No Gen 3 move source is available for {game}.");
        }

        if (speciesId is < FirstGen3Species or > LastGen3Species)
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
    /// Level-up moves come from the learnset itself rather than the flag scan, because it
    /// is the only source that also carries the level, and the level is what a player needs.
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

        // Source order is the order the game itself teaches them, which is what a player
        // sees. A move that appears twice keeps its earliest level.
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
        var pokemon = new PK3 { Species = species, CurrentLevel = (byte)maximumLevel };
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
        LearnSource3E emerald => emerald.GetLearnset(species, 0),
        LearnSource3RS rubySapphire => rubySapphire.GetLearnset(species, 0),
        LearnSource3FR fireRed => fireRed.GetLearnset(species, 0),
        LearnSource3LG leafGreen => leafGreen.GetLearnset(species, 0),
        _ => throw new NotSupportedException($"Unhandled learn source {source.GetType().Name}."),
    };

    private static ILearnSource? ResolveSource(string gameCode) => gameCode switch
    {
        // Every Western Emerald localisation shares one set of tables. See D-017.
        "BPEE" or "BPEF" or "BPED" or "BPES" or "BPEI" => LearnSource3E.Instance,
        "BPRE" or "BPRI" => LearnSource3FR.Instance,
        "BPGE" or "BPGI" => LearnSource3LG.Instance,
        "AXVE" or "AXPE" or "AXVI" or "AXPI" => LearnSource3RS.Instance,
        _ => null,
    };
}
