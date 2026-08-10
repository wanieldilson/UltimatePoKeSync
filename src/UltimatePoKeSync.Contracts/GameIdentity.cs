namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Identifies the ROM loaded in the emulator.
/// </summary>
/// <remarks>
/// It travels with every snapshot because RAM addresses vary per game AND per region:
/// interpreting FireRed bytes with Emerald's map produces plausible garbage, which is the
/// worst kind of bug. See D-005.
/// </remarks>
/// <param name="GameCode">
/// Four-character code from the GBA cartridge header at 0x080000AC.
/// Examples: <c>BPEE</c> Emerald USA, <c>BPRE</c> FireRed, <c>BPGE</c> LeafGreen,
/// <c>AXVE</c> Ruby, <c>AXPE</c> Sapphire. The last character is the region
/// (<c>E</c> USA, <c>J</c> Japan, <c>I</c> Italy, <c>P</c> Europe...).
/// </param>
/// <param name="Title">Internal ROM title, for diagnostics.</param>
/// <param name="Revision">ROM revision number.</param>
/// <param name="Generation">Generation inferred from the code.</param>
public sealed record GameIdentity(
    string GameCode,
    string Title,
    int Revision,
    PokemonGeneration Generation)
{
    /// <summary>Used when the emulator is connected but has no ROM loaded yet.</summary>
    public static GameIdentity Unknown { get; } =
        new("????", string.Empty, 0, PokemonGeneration.Unknown);

    /// <summary>Region, taken from the fourth character of the game code.</summary>
    public char RegionCode => GameCode.Length == 4 ? GameCode[3] : '?';

    public override string ToString() => $"{Title} [{GameCode}] rev{Revision}";
}
