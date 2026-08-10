using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// Type effectiveness for one game generation.
/// </summary>
public interface ITypeChart
{
    PokemonGeneration Generation { get; }

    /// <summary>Battle types available in this generation, in stable display order.</summary>
    IReadOnlyList<PokemonType> Types { get; }

    /// <summary>Returns the multiplier for one attacking and one defending type.</summary>
    double GetMultiplier(PokemonType attackingType, PokemonType defendingType);

    /// <summary>
    /// Returns the combined multiplier against a mono- or dual-type defender.
    /// </summary>
    double GetMultiplier(
        PokemonType attackingType,
        PokemonType primaryType,
        PokemonType secondaryType);
}
