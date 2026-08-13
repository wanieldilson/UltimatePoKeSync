using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// What a species can become, in one specific game. Kept behind an interface for the same
/// reason as <see cref="IMoveLearnSource"/>: the data comes from PKHeX, and Analysis must
/// not depend on PKHeX. See D-007 and D-037.
/// </summary>
public interface IEvolutionSource
{
    string SourceName { get; }

    bool Supports(GameIdentity game);

    /// <summary>
    /// Every evolution available to the species in that game, in the order the data lists
    /// them. Empty for a species that does not evolve, or one the source does not know.
    /// </summary>
    IReadOnlyList<EvolutionStep> FindEvolutions(GameIdentity game, int speciesId);
}

/// <summary>
/// One evolution and what it costs. <paramref name="Requirement"/> is the sentence a player
/// can act on: the trigger alone does not say which stone, or how much friendship.
/// </summary>
public sealed record EvolutionStep(
    int IntoSpeciesId,
    string IntoSpeciesName,
    EvolutionTrigger Trigger,
    int? Level,
    string Requirement,
    /// <summary>
    /// True when this species is created alongside the ordinary evolution rather than
    /// replacing the current Pokémon. Shedinja is the only such case in Gen 3 and Gen 5.
    /// </summary>
    bool IsByproduct = false,
    /// <summary>Which gender may take this route, or null when either may.</summary>
    PokemonGender? RequiredGender = null)
{
    /// <summary>
    /// Whether reaching the level is enough on its own. True for a Wurmple, whose two
    /// outcomes are decided before it hatched but one of which is certain at Lv.7; false
    /// for a Kadabra, which levels all the way to 100 and stays a Kadabra without a trade.
    /// </summary>
    public bool HappensByLevellingAlone => !IsByproduct && Level is > 0;
}

/// <summary>
/// Why an evolution happens. Grouped by what the player has to *do*, not by the game's
/// internal method: LevelUpNinjask and LevelUpShedinja are one decision to a player.
/// </summary>
public enum EvolutionTrigger
{
    /// <summary>Reaching a level, with nothing else asked.</summary>
    Level = 0,

    /// <summary>An evolution stone or other item used on it.</summary>
    Item = 1,

    /// <summary>Enough friendship, sometimes only at a certain time of day.</summary>
    Friendship = 2,

    /// <summary>A trade, sometimes while holding an item. Not possible alone.</summary>
    Trade = 3,

    /// <summary>Anything else the game asks for: beauty, a free party slot, a held item.</summary>
    Condition = 4,
}
