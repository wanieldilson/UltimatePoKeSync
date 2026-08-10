namespace UltimatePoKeSync.Contracts;

/// <summary>
/// The 18 types. Note: <see cref="Fairy"/> does not exist before Gen 6, and Dark and
/// Steel do not exist in Gen 1. The type chart is therefore selected per generation
/// rather than being global. See D-009.
/// </summary>
public enum PokemonType
{
    None = -1,
    Normal = 0,
    Fighting = 1,
    Flying = 2,
    Poison = 3,
    Ground = 4,
    Rock = 5,
    Bug = 6,
    Ghost = 7,
    Steel = 8,
    Fire = 9,
    Water = 10,
    Grass = 11,
    Electric = 12,
    Psychic = 13,
    Ice = 14,
    Dragon = 15,
    Dark = 16,
    Fairy = 17,
}
