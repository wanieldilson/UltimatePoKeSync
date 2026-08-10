using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>A nature and the two non-HP stats it modifies.</summary>
public sealed record NatureInfo(
    int Id,
    string Name,
    Stat? IncreasedStat,
    Stat? DecreasedStat)
{
    public bool IsNeutral => IncreasedStat is null && DecreasedStat is null;

    /// <summary>Applies the in-game integer nature modifier to a computed stat.</summary>
    public int Apply(Stat stat, int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);

        if (stat == IncreasedStat)
        {
            return value * 110 / 100;
        }

        if (stat == DecreasedStat)
        {
            return value * 90 / 100;
        }

        return value;
    }
}
