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
    private readonly NatureInfo[] _natures;

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

        var natureData = EmbeddedJson.Load<NatureData>("gen3-natures.json");
        _natures = ParseNatures(natureData);
        Natures = Array.AsReadOnly(_natures);
    }

    public static Gen3Rules Instance { get; } = new();

    public PokemonGeneration Generation => PokemonGeneration.Gen3;

    public ITypeChart TypeChart { get; }

    public IReadOnlyList<NatureInfo> Natures { get; }

    public NatureInfo GetNature(int natureId)
    {
        if ((uint)natureId >= _natures.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(natureId));
        }

        return _natures[natureId];
    }

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

    public StatBlock CalculateStats(
        int level,
        StatBlock baseStats,
        StatBlock individualValues,
        StatBlock effortValues,
        int natureId)
    {
        if (level is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        ValidateStats(baseStats, 1, 255, nameof(baseStats));
        ValidateStats(individualValues, 0, 31, nameof(individualValues));
        ValidateStats(effortValues, 0, 255, nameof(effortValues));
        if (effortValues.Total > 510)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effortValues), "Gen 3 effort values cannot exceed 510 in total.");
        }

        NatureInfo nature = GetNature(natureId);

        int hp = baseStats.Hp == 1
            ? 1 // Shedinja is the only Gen 3 species with this special case.
            : CalculateUnmodifiedStat(
                level, baseStats.Hp, individualValues.Hp, effortValues.Hp) + level + 10;

        return new StatBlock(
            hp,
            CalculateNonHpStat(
                Stat.Attack, level, baseStats.Attack, individualValues.Attack,
                effortValues.Attack, nature),
            CalculateNonHpStat(
                Stat.Defense, level, baseStats.Defense, individualValues.Defense,
                effortValues.Defense, nature),
            CalculateNonHpStat(
                Stat.SpecialAttack, level, baseStats.SpecialAttack,
                individualValues.SpecialAttack, effortValues.SpecialAttack, nature),
            CalculateNonHpStat(
                Stat.SpecialDefense, level, baseStats.SpecialDefense,
                individualValues.SpecialDefense, effortValues.SpecialDefense, nature),
            CalculateNonHpStat(
                Stat.Speed, level, baseStats.Speed, individualValues.Speed,
                effortValues.Speed, nature));
    }

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

    private static NatureInfo[] ParseNatures(NatureData data)
    {
        if (data.Generation != 3 || data.Natures.Length != 25)
        {
            throw new InvalidOperationException("The embedded Gen 3 nature data is malformed.");
        }

        var result = new NatureInfo[data.Natures.Length];
        for (int id = 0; id < data.Natures.Length; id++)
        {
            NatureDataRow row = data.Natures[id];
            if (row.Id != id || string.IsNullOrWhiteSpace(row.Name))
            {
                throw new InvalidOperationException("The embedded Gen 3 nature data is malformed.");
            }

            Stat? increased = ParseNatureStat(row.IncreasedStat);
            Stat? decreased = ParseNatureStat(row.DecreasedStat);
            if ((increased is null) != (decreased is null) ||
                (increased is not null && increased == decreased))
            {
                throw new InvalidOperationException("The embedded Gen 3 nature data is malformed.");
            }

            result[id] = new NatureInfo(id, row.Name, increased, decreased);
        }

        return result;
    }

    private static Stat? ParseNatureStat(string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (!Enum.TryParse(value, ignoreCase: false, out Stat stat) || stat == Stat.Hp)
        {
            throw new InvalidOperationException($"Invalid nature stat: {value}");
        }

        return stat;
    }

    private static int CalculateNonHpStat(
        Stat stat,
        int level,
        int baseStat,
        int individualValue,
        int effortValue,
        NatureInfo nature)
    {
        int unmodified = CalculateUnmodifiedStat(
            level, baseStat, individualValue, effortValue) + 5;
        return nature.Apply(stat, unmodified);
    }

    private static int CalculateUnmodifiedStat(
        int level,
        int baseStat,
        int individualValue,
        int effortValue) =>
        ((2 * baseStat + individualValue + effortValue / 4) * level) / 100;

    private static void ValidateStats(
        StatBlock stats,
        int minimum,
        int maximum,
        string parameterName)
    {
        foreach (Stat stat in Enum.GetValues<Stat>())
        {
            if (stats[stat] < minimum || stats[stat] > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"{stat} must be between {minimum} and {maximum}.");
            }
        }
    }
}

internal sealed record MovePowerData(int Generation, int[] BasePowers);

internal sealed record NatureData(int Generation, NatureDataRow[] Natures);

internal sealed record NatureDataRow(
    int Id,
    string Name,
    string? IncreasedStat,
    string? DecreasedStat);
