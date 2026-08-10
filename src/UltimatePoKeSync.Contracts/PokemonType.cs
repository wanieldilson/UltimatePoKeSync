namespace UltimatePoKeSync.Contracts;

/// <summary>
/// I 18 tipi. Attenzione: <see cref="Fairy"/> non esiste prima della Gen 6, e in Gen 1
/// il tipo Buio e Acciaio non esistono. La type chart e' quindi selezionata per
/// generazione, non globale. Vedi D-009.
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
