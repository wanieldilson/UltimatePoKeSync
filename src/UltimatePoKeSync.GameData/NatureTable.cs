using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// Reads the nature table out of embedded data. The twenty-five natures and what each
/// one raises and lowers have not changed since Gen 3, so the reader is shared even
/// though the files are per generation.
/// </summary>
internal static class NatureTable
{
    public static NatureInfo[] Parse(NatureData data, PokemonGeneration generation)
    {
        if (data.Generation != (int)generation || data.Natures.Length != 25)
        {
            throw new InvalidOperationException($"The embedded {generation} nature data is malformed.");
        }

        var result = new NatureInfo[data.Natures.Length];
        for (int id = 0; id < data.Natures.Length; id++)
        {
            NatureDataRow row = data.Natures[id];
            if (row.Id != id || string.IsNullOrWhiteSpace(row.Name))
            {
                throw new InvalidOperationException($"The embedded {generation} nature data is malformed.");
            }

            Stat? increased = ParseStat(row.IncreasedStat);
            Stat? decreased = ParseStat(row.DecreasedStat);
            if ((increased is null) != (decreased is null) ||
                (increased is not null && increased == decreased))
            {
                throw new InvalidOperationException($"The embedded {generation} nature data is malformed.");
            }

            result[id] = new NatureInfo(id, row.Name, increased, decreased);
        }

        return result;
    }

    private static Stat? ParseStat(string? value)
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
}
