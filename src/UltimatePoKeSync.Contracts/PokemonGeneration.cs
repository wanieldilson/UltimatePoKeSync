namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Generazione dei giochi. Non e' una curiosita' anagrafica: determina le regole di
/// battaglia usate dall'analisi (numero di tipi, split fisico/speciale, cap EV).
/// Vedi D-009 nel decision log.
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
