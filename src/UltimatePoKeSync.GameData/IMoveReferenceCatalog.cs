using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

public interface IMoveReferenceCatalog
{
    PokemonGeneration Generation { get; }

    string SourceRevision { get; }

    MoveReference? Find(string moveNameOrId);

    IReadOnlyList<LevelUpMoveReference> FindLevelUpMoves(string speciesName, int maximumLevel);
}

public sealed record MoveReference(
    int MoveId,
    string ReferenceId,
    string Name,
    PokemonType Type);

public sealed record LevelUpMoveReference(MoveReference Move, int LearnedAtLevel);
