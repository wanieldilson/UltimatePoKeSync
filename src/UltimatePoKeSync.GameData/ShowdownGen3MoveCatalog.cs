using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>English move metadata and level-up learnsets pinned for offline recommendations.</summary>
public sealed class ShowdownGen3MoveCatalog : IMoveReferenceCatalog
{
    private static readonly Lazy<ShowdownGen3MoveCatalog> LazyInstance =
        new(() => new ShowdownGen3MoveCatalog());

    private readonly IReadOnlyDictionary<string, MoveReference> _moves;
    private readonly IReadOnlyDictionary<string, LevelUpMoveData[]> _learnsets;

    private ShowdownGen3MoveCatalog()
    {
        MoveDataRoot moveData = EmbeddedJson.Load<MoveDataRoot>("gen3-showdown-moves.json");
        LearnsetDataRoot learnsetData =
            EmbeddedJson.Load<LearnsetDataRoot>("gen3-showdown-level-up-learnsets.json");

        Validate(moveData, learnsetData);
        _moves = moveData.Moves.ToDictionary(
            move => move.Id,
            move => new MoveReference(
                move.Number,
                move.Id,
                move.Name,
                Enum.Parse<PokemonType>(move.Type)));
        _learnsets = learnsetData.Species;
    }

    public static ShowdownGen3MoveCatalog Instance => LazyInstance.Value;

    public PokemonGeneration Generation => PokemonGeneration.Gen3;

    public string SourceRevision => ShowdownGen3PresetCatalog.Revision;

    public int MoveCount => _moves.Count;

    public int SpeciesCount => _learnsets.Count;

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

    public IReadOnlyList<LevelUpMoveReference> FindLevelUpMoves(
        string speciesName,
        int maximumLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speciesName);
        if (maximumLevel is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumLevel));
        }

        if (!_learnsets.TryGetValue(
            ShowdownIdentifier.Normalize(speciesName),
            out LevelUpMoveData[]? learnset))
        {
            return [];
        }

        return
        [
            .. learnset
                .Where(entry => entry.Level <= maximumLevel)
                .Select(entry => new LevelUpMoveReference(_moves[entry.Id], entry.Level)),
        ];
    }

    private static void Validate(MoveDataRoot moveData, LearnsetDataRoot learnsetData)
    {
        const int expectedMoves = 354;
        const int expectedSpecies = 386;

        if (moveData.Generation != 3 || learnsetData.Generation != 3 ||
            moveData.Revision != ShowdownGen3PresetCatalog.Revision ||
            learnsetData.Revision != ShowdownGen3PresetCatalog.Revision ||
            moveData.Moves.Length != expectedMoves ||
            learnsetData.Species.Count != expectedSpecies)
        {
            throw new InvalidOperationException("The embedded Showdown Gen 3 move data is malformed.");
        }

        var moveIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < moveData.Moves.Length; index++)
        {
            MoveData move = moveData.Moves[index];
            if (move.Number != index + 1 || move.Id != ShowdownIdentifier.Normalize(move.Id) ||
                string.IsNullOrWhiteSpace(move.Name) ||
                !Enum.TryParse(move.Type, out PokemonType type) ||
                !Gen3Rules.Instance.TypeChart.Types.Contains(type) ||
                !moveIds.Add(move.Id))
            {
                throw new InvalidOperationException(
                    $"Invalid Showdown Gen 3 move entry: {move.Id}.");
            }
        }

        foreach ((string species, LevelUpMoveData[] learnset) in learnsetData.Species)
        {
            if (species != ShowdownIdentifier.Normalize(species) || learnset.Length == 0 ||
                learnset.Any(entry => entry.Level is < 0 or > 100 || !moveIds.Contains(entry.Id)))
            {
                throw new InvalidOperationException(
                    $"Invalid Showdown Gen 3 level-up learnset: {species}.");
            }
        }
    }

    private sealed record MoveDataRoot(int Generation, string Revision, MoveData[] Moves);

    private sealed record MoveData(int Number, string Id, string Name, string Type);

    private sealed record LearnsetDataRoot(
        int Generation,
        string Revision,
        Dictionary<string, LevelUpMoveData[]> Species);

    private sealed record LevelUpMoveData(string Id, int Level);
}
