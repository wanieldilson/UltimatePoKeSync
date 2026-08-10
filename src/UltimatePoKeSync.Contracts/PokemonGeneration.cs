namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Game generation. Not trivia: it determines the battle rules the analysis uses
/// (number of types, physical/special split, EV caps). See D-009 in the decision log.
/// </summary>
public enum PokemonGeneration
{
    Unknown = 0,
    Gen1 = 1,
    Gen2 = 2,
    Gen3 = 3,
    Gen4 = 4,
    Gen5 = 5,
    Gen6 = 6,
    Gen7 = 7,
    Gen8 = 8,
    Gen9 = 9,
}
