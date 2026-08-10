using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

internal sealed class Gen3TypeChart : ITypeChart
{
    private readonly double[][] _multipliers;

    public Gen3TypeChart(TypeChartData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        PokemonType[] expectedTypes = [.. Enum.GetValues<PokemonType>()
            .Where(type => type is >= PokemonType.Normal and <= PokemonType.Dark)];

        if (data.Generation != 3 ||
            !data.Types.SequenceEqual(expectedTypes.Select(type => type.ToString()), StringComparer.Ordinal) ||
            data.Multipliers.Length != expectedTypes.Length ||
            data.Multipliers.Any(row => row.Length != expectedTypes.Length))
        {
            throw new InvalidOperationException("The embedded Gen 3 type chart is malformed.");
        }

        if (data.Multipliers.SelectMany(row => row).Any(value => value is not (0 or 0.5 or 1 or 2)))
        {
            throw new InvalidOperationException("The embedded Gen 3 type chart has an invalid multiplier.");
        }

        Types = Array.AsReadOnly(expectedTypes);
        _multipliers = data.Multipliers;
    }

    public PokemonGeneration Generation => PokemonGeneration.Gen3;

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
