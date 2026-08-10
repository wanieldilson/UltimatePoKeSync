using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// Battle rules that can change the result of an analysis between generations.
/// </summary>
public interface IGenerationRules
{
    PokemonGeneration Generation { get; }

    ITypeChart TypeChart { get; }

    /// <summary>
    /// Classifies a move using the generation's damage rules. In Gen 3 a damaging
    /// move's category is determined by its type, not by the individual move.
    /// </summary>
    MoveCategory GetMoveCategory(int moveId, PokemonType moveType);

    /// <summary>
    /// Whether the move can gain a super-effective damage multiplier. Fixed-damage and
    /// one-hit knockout moves can be damaging without providing type coverage.
    /// </summary>
    bool CanProvideSuperEffectiveCoverage(int moveId);

    /// <summary>
    /// Combines the type chart with generation-specific defensive ability effects.
    /// </summary>
    double GetDefensiveMultiplier(
        PokemonType attackingType,
        PokemonType primaryType,
        PokemonType secondaryType,
        int abilityId);
}

public enum MoveCategory
{
    Status = 0,
    Physical = 1,
    Special = 2,
}
