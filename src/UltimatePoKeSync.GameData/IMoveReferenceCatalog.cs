using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// Move identity — number, name and type. Keyed by generation, because a move's type can
/// be retconned: Charm is Normal in Gen 3 and Fairy from Gen 6.
/// </summary>
public interface IMoveReferenceCatalog
{
    PokemonGeneration Generation { get; }

    string SourceRevision { get; }

    MoveReference? Find(string moveNameOrId);

    MoveReference? Find(int moveId);
}

/// <summary>
/// Level-up learnsets, keyed by <em>game</em> rather than by generation.
/// </summary>
/// <remarks>
/// Within one generation the games disagree: 42 of the 386 Gen 3 species learn at least
/// one move at a different level in Ruby/Sapphire, Emerald and FireRed/LeafGreen. Merging
/// them produces a level that is wrong for the game actually running, and wrong in the
/// worst way — plausible. See D-027.
/// </remarks>
public interface ILevelUpLearnsetSource
{
    string SourceName { get; }

    bool Supports(GameIdentity game);

    /// <summary>
    /// Every move the species learns by level up at or below <paramref name="maximumLevel"/>
    /// in that specific game, ordered by level. Empty when the species is unknown.
    /// </summary>
    IReadOnlyList<LevelUpMoveReference> FindLevelUpMoves(
        GameIdentity game,
        int speciesId,
        int maximumLevel);
}

public sealed record MoveReference(
    int MoveId,
    string ReferenceId,
    string Name,
    PokemonType Type);

public sealed record LevelUpMoveReference(MoveReference Move, int LearnedAtLevel);
