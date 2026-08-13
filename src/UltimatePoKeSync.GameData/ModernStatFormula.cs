using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// The stat formula shared by every generation from the third onwards.
/// </summary>
/// <remarks>
/// Gen 1 and 2 are the ones that differ (they use DVs and stat experience, and Gen 1 has no
/// Special Defense at all), so this is not "the formula", it is the modern one. Shared rather
/// than copied per generation, because two copies that drift apart would be a bug in one of
/// them and nothing else.
/// </remarks>
internal static class ModernStatFormula
{
    public static StatBlock Calculate(
        int level,
        StatBlock baseStats,
        StatBlock individualValues,
        StatBlock effortValues,
        NatureInfo nature)
    {
        if (level is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }

        Validate(baseStats, 1, 255, nameof(baseStats));
        Validate(individualValues, 0, 31, nameof(individualValues));
        Validate(effortValues, 0, 255, nameof(effortValues));
        if (effortValues.Total > 510)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effortValues), "Effort values cannot exceed 510 in total.");
        }

        // Shedinja, and nothing else, has a base HP of 1 and stays on 1 HP for ever.
        int hp = baseStats.Hp == 1
            ? 1
            : Unmodified(level, baseStats.Hp, individualValues.Hp, effortValues.Hp) + level + 10;

        return new StatBlock(
            hp,
            NonHp(Stat.Attack, level, baseStats.Attack, individualValues.Attack, effortValues.Attack, nature),
            NonHp(Stat.Defense, level, baseStats.Defense, individualValues.Defense, effortValues.Defense, nature),
            NonHp(Stat.SpecialAttack, level, baseStats.SpecialAttack, individualValues.SpecialAttack, effortValues.SpecialAttack, nature),
            NonHp(Stat.SpecialDefense, level, baseStats.SpecialDefense, individualValues.SpecialDefense, effortValues.SpecialDefense, nature),
            NonHp(Stat.Speed, level, baseStats.Speed, individualValues.Speed, effortValues.Speed, nature));
    }

    private static int NonHp(
        Stat stat, int level, int baseStat, int individualValue, int effortValue, NatureInfo nature) =>
        nature.Apply(stat, Unmodified(level, baseStat, individualValue, effortValue) + 5);

    private static int Unmodified(int level, int baseStat, int individualValue, int effortValue) =>
        ((2 * baseStat + individualValue + effortValue / 4) * level) / 100;

    private static void Validate(StatBlock stats, int minimum, int maximum, string parameterName)
    {
        foreach (Stat stat in Enum.GetValues<Stat>())
        {
            if (stats[stat] < minimum || stats[stat] > maximum)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, $"{stat} must be between {minimum} and {maximum}.");
            }
        }
    }
}
