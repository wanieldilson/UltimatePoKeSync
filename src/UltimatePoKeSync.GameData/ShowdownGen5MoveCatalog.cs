using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>English Gen 5 move metadata, pinned for offline recommendations. See D-041.</summary>
/// <remarks>
/// Move identity only: number, name and type. What a move does — its power and whether it
/// is physical or special — lives in <see cref="Gen5Rules"/>, because that is a battle rule
/// and this is a name table.
/// </remarks>
public sealed class ShowdownGen5MoveCatalog : IMoveReferenceCatalog
{
    private static readonly Lazy<ShowdownGen5MoveCatalog> LazyInstance =
        new(() => new ShowdownGen5MoveCatalog());

    private readonly IReadOnlyDictionary<string, MoveReference> _moves;
    private readonly IReadOnlyDictionary<int, MoveReference> _movesByNumber;

    private ShowdownGen5MoveCatalog()
    {
        MoveDataRoot moveData = EmbeddedJson.Load<MoveDataRoot>("gen5-showdown-moves.json");

        Validate(moveData);
        _moves = moveData.Moves.ToDictionary(
            move => move.Id,
            move => new MoveReference(
                move.Number,
                move.Id,
                move.Name,
                Enum.Parse<PokemonType>(move.Type)));
        _movesByNumber = _moves.Values.ToDictionary(move => move.MoveId);
    }

    public static ShowdownGen5MoveCatalog Instance => LazyInstance.Value;

    public PokemonGeneration Generation => PokemonGeneration.Gen5;

    /// <summary>The same pinned Showdown commit as Gen 3, so both describe one snapshot.</summary>
    public string SourceRevision => ShowdownGen3PresetCatalog.Revision;

    public int MoveCount => _moves.Count;

    public MoveReference? Find(string moveNameOrId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moveNameOrId);

        string key = ShowdownIdentifier.Normalize(moveNameOrId);
        if (_moves.TryGetValue(key, out MoveReference? move))
        {
            return move;
        }

        const string hiddenPower = "hiddenpower";
        if (!key.StartsWith(hiddenPower, StringComparison.Ordinal) ||
            !_moves.TryGetValue(hiddenPower, out MoveReference? baseMove))
        {
            return null;
        }

        string typeName = key[hiddenPower.Length..];
        PokemonType type = Gen3Rules.Instance.TypeChart.Types.SingleOrDefault(
            candidate => ShowdownIdentifier.Normalize(candidate.ToString()) == typeName);
        return type == default
            ? null
            : baseMove with
            {
                ReferenceId = key,
                Name = $"Hidden Power {type}",
                Type = type,
            };
    }

    public MoveReference? Find(int moveId) =>
        _movesByNumber.GetValueOrDefault(moveId);

    private static void Validate(MoveDataRoot moveData)
    {
        // Gen 5 ends at Fusion Bolt, and every number in between must be present: a gap
        // would mean a move nothing can name.
        const int expectedMoves = 559;

        if (moveData.Generation != 5 ||
            moveData.Revision != ShowdownGen3PresetCatalog.Revision ||
            moveData.Moves.Length != expectedMoves)
        {
            throw new InvalidOperationException("The embedded Showdown Gen 5 move data is malformed.");
        }

        var moveIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < moveData.Moves.Length; index++)
        {
            MoveData move = moveData.Moves[index];
            if (move.Number != index + 1 || move.Id != ShowdownIdentifier.Normalize(move.Id) ||
                string.IsNullOrWhiteSpace(move.Name) ||
                !Enum.TryParse(move.Type, out PokemonType type) ||
                !Gen5Rules.Instance.TypeChart.Types.Contains(type) ||
                !moveIds.Add(move.Id))
            {
                throw new InvalidOperationException(
                    $"Invalid Showdown Gen 5 move entry: {move.Id}.");
            }
        }
    }

    private sealed record MoveDataRoot(int Generation, string Revision, MoveData[] Moves);

    private sealed record MoveData(int Number, string Id, string Name, string Type);
}
