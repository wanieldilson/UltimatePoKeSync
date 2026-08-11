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
/// Every way a species can come by a move, keyed by <em>game</em> rather than by generation.
/// </summary>
/// <remarks>
/// Within one generation the games disagree, and not only on levels: 42 of the 386 Gen 3
/// species learn at least one move at a different level in Ruby/Sapphire, Emerald and
/// FireRed/LeafGreen, and the tutor lists differ far more than that — Charizard has twenty
/// tutor moves in Emerald and one in FireRed. Merging them produces an answer that is wrong
/// for the game actually running, and wrong in the worst way: plausible. See D-027 and D-030.
/// </remarks>
public interface IMoveLearnSource
{
    string SourceName { get; }

    bool Supports(GameIdentity game);

    /// <summary>
    /// Every move the species can have in that specific game: level-up moves at or below
    /// <paramref name="maximumLevel"/>, then machines, then tutors. Empty when the species
    /// is unknown.
    /// </summary>
    IReadOnlyList<LearnableMove> FindLearnableMoves(
        GameIdentity game,
        int speciesId,
        int maximumLevel);
}

/// <summary>How a Pokémon comes by a move. Each costs the player something different.</summary>
public enum MoveLearnMethod
{
    /// <summary>Learned by levelling, or re-learned from the Move Reminder.</summary>
    LevelUp = 0,

    /// <summary>A TM or HM. In Gen 3 a TM is consumed, so owning one is not enough.</summary>
    Machine = 1,

    /// <summary>A move tutor. Several are one-time-only, and the list differs per game.</summary>
    Tutor = 2,
}

public sealed record LearnableMove(
    MoveReference Move,
    MoveLearnMethod Method,
    int? LearnedAtLevel);

public sealed record MoveReference(
    int MoveId,
    string ReferenceId,
    string Name,
    PokemonType Type);
