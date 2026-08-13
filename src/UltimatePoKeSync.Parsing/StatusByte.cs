using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Parsing;

/// <summary>
/// The status byte, which Gen 3, 4 and 5 lay out identically.
/// </summary>
/// <remarks>
/// Sleep occupies the low three bits as a turn counter, so it is present whenever any of
/// them is set rather than at a value of its own; the rest are one flag each. Badly poisoned
/// is checked before poisoned, because the game sets both.
///
/// Cross-checked against PKHeX's own <c>StatusCondition</c>. Sleep1 to Sleep7, Poison 8,
/// Burn 16, Freeze 32, Paralysis 64, PoisonBad 128. Ours is separate because Contracts must
/// not depend on PKHeX (D-007), and pinned by a test for the same reason as D-014. Shared
/// between the parsers rather than copied, because a difference between them would be a bug
/// in one of the two and nothing else.
/// </remarks>
internal static class StatusByte
{
    public static StatusCondition Read(int condition) => condition switch
    {
        _ when (condition & 0b0000_0111) != 0 => StatusCondition.Sleep,
        _ when (condition & 0b1000_0000) != 0 => StatusCondition.BadPoison,
        _ when (condition & 0b0000_1000) != 0 => StatusCondition.Poison,
        _ when (condition & 0b0001_0000) != 0 => StatusCondition.Burn,
        _ when (condition & 0b0010_0000) != 0 => StatusCondition.Freeze,
        _ when (condition & 0b0100_0000) != 0 => StatusCondition.Paralysis,
        _ => StatusCondition.None,
    };
}
