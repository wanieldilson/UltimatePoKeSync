using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// A type chart read from an embedded table. Gen 2 through Gen 5 all use the same one (the
/// chart changed with Fairy and with Steel losing its resistances, both in Gen 6), but each
/// generation declares its own file rather than borrowing another's, and a test pins that
/// the two we ship agree. A shared file would save a kilobyte and cost the ability to say
/// which generation a number belongs to.
/// </summary>
internal sealed class TableTypeChart : ITypeChart
{
    private readonly double[][] _multipliers;

    public TableTypeChart(TypeChartData data, PokemonGeneration generation)
    {
        ArgumentNullException.ThrowIfNull(data);
        Generation = generation;

        PokemonType[] expectedTypes = [.. Enum.GetValues<PokemonType>()
            .Where(type => type is >= PokemonType.Normal and <= PokemonType.Dark)];

        if (data.Generation != (int)generation ||
            !data.Types.SequenceEqual(expectedTypes.Select(type => type.ToString()), StringComparer.Ordinal) ||
            data.Multipliers.Length != expectedTypes.Length ||
            data.Multipliers.Any(row => row.Length != expectedTypes.Length))
        {
            throw new InvalidOperationException($"The embedded {generation} type chart is malformed.");
        }

        if (data.Multipliers.SelectMany(row => row).Any(value => value is not (0 or 0.5 or 1 or 2)))
        {
            throw new InvalidOperationException($"The embedded {generation} type chart has an invalid multiplier.");
        }

        Types = Array.AsReadOnly(expectedTypes);
        _multipliers = data.Multipliers;
    }

    public PokemonGeneration Generation { get; }

    public IReadOnlyList<PokemonType> Types { get; }

    public double GetMultiplier(PokemonType attackingType, PokemonType defendingType)
    {
        ValidateType(attackingType, nameof(attackingType));
        ValidateType(defendingType, nameof(defendingType));
        return _multipliers[(int)attackingType][(int)defendingType];
    }

    public double GetMultiplier(
        PokemonType attackingType,
        PokemonType primaryType,
        PokemonType secondaryType)
    {
        double multiplier = GetMultiplier(attackingType, primaryType);
        return secondaryType == PokemonType.None
            ? multiplier
            : multiplier * GetMultiplier(attackingType, secondaryType);
    }

    private static void ValidateType(PokemonType type, string parameterName)
    {
        if (type is < PokemonType.Normal or > PokemonType.Dark)
        {
            throw new ArgumentOutOfRangeException(
                parameterName, type, "The type is not available in Gen 3.");
        }
    }
}

internal sealed record TypeChartData(int Generation, string[] Types, double[][] Multipliers);
