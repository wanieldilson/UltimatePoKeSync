using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>Pokémon Ruby, Sapphire, FireRed, LeafGreen and Emerald battle rules.</summary>
public sealed class Gen3Rules : IGenerationRules
{
    private static readonly HashSet<PokemonType> PhysicalTypes =
    [
        PokemonType.Normal,
        PokemonType.Fighting,
        PokemonType.Flying,
        PokemonType.Poison,
        PokemonType.Ground,
        PokemonType.Rock,
        PokemonType.Bug,
        PokemonType.Ghost,
        PokemonType.Steel,
    ];

    // These moves are damaging, but their battle scripts do not apply ordinary
    // super-effective damage. Variable-power moves whose data also uses power 1 (Low
    // Kick, Flail, Hidden Power, and others) are deliberately not in this set.
    private static readonly HashSet<int> NonCoverageMoveIds =
    [
        12,  // Guillotine
        32,  // Horn Drill
        49,  // Sonic Boom
        68,  // Counter
        69,  // Seismic Toss
        82,  // Dragon Rage
        90,  // Fissure
        101, // Night Shade
        117, // Bide
        149, // Psywave
        162, // Super Fang
        243, // Mirror Coat
        283, // Endeavor
        329, // Sheer Cold
    ];

    private readonly int[] _moveBasePowers;

    private Gen3Rules()
    {
        var chartData = EmbeddedJson.Load<TypeChartData>("gen3-type-chart.json");
        TypeChart = new Gen3TypeChart(chartData);

        var moveData = EmbeddedJson.Load<MovePowerData>("gen3-move-power.json");
        if (moveData.Generation != 3 || moveData.BasePowers.Length != 355 ||
            moveData.BasePowers.Any(power => power is < 0 or > 255))
        {
            throw new InvalidOperationException("The embedded Gen 3 move-power data is malformed.");
        }

        _moveBasePowers = moveData.BasePowers;
    }

    public static Gen3Rules Instance { get; } = new();

    public PokemonGeneration Generation => PokemonGeneration.Gen3;

    public ITypeChart TypeChart { get; }

    public MoveCategory GetMoveCategory(int moveId, PokemonType moveType)
    {
        if (moveId <= 0 || moveId >= _moveBasePowers.Length || _moveBasePowers[moveId] == 0)
        {
            return MoveCategory.Status;
        }

        if (!TypeChart.Types.Contains(moveType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(moveType), moveType, "The move type is not available in Gen 3.");
        }

        return PhysicalTypes.Contains(moveType) ? MoveCategory.Physical : MoveCategory.Special;
    }

    public bool CanProvideSuperEffectiveCoverage(int moveId) =>
        moveId > 0 && moveId < _moveBasePowers.Length &&
        _moveBasePowers[moveId] > 0 && !NonCoverageMoveIds.Contains(moveId);

    public double GetDefensiveMultiplier(
        PokemonType attackingType,
        PokemonType primaryType,
        PokemonType secondaryType,
        int abilityId)
    {
        double multiplier = TypeChart.GetMultiplier(attackingType, primaryType, secondaryType);

        if ((abilityId == Gen3AbilityIds.Levitate && attackingType == PokemonType.Ground) ||
            (abilityId == Gen3AbilityIds.FlashFire && attackingType == PokemonType.Fire) ||
            (abilityId == Gen3AbilityIds.VoltAbsorb && attackingType == PokemonType.Electric) ||
            (abilityId == Gen3AbilityIds.WaterAbsorb && attackingType == PokemonType.Water))
        {
            return 0;
        }

        // Wonder Guard lets through only net super-effective damaging attacks. A 2x and
        // a 0.5x component cancel, and the game treats that as blocked.
        if (abilityId == Gen3AbilityIds.WonderGuard && multiplier <= 1)
        {
            return 0;
        }

        if (abilityId == Gen3AbilityIds.ThickFat &&
            attackingType is PokemonType.Fire or PokemonType.Ice)
        {
            multiplier *= 0.5;
        }

        return multiplier;
    }
}

internal sealed record MovePowerData(int Generation, int[] BasePowers);
