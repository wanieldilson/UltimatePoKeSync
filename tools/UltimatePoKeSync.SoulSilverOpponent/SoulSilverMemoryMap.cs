namespace UltimatePoKeSync.SoulSilverOpponent;

/// <summary>
/// Published RAM routes for the USA/Australia SoulSilver release. They remain candidates
/// until real-memory beta reports settle which opponent layout this exact build uses.
/// Every record reached through them is independently decrypted and checksum-validated.
/// </summary>
internal static class SoulSilverMemoryMap
{
    public const string SupportedGameCode = "IPGE";

    /// <summary>The DS cartridge header mirrored into the console's 4 MB main RAM.</summary>
    public const uint CartridgeHeader = 0x023FFE00;

    public const int HeaderLength = 32;

    /// <summary>Pointer to SoulSilver's main runtime state, for the IPGE release.</summary>
    public const uint RootPointerAddress = 0x0211186C;

    public const int SlotSize = 236;

    public const int PartyCapacity = 6;

    /// <summary>
    /// Route used by long-standing HGSS Lua overlays: dereference this field, then add
    /// <see cref="PrimaryTrainerPartyOffset"/>.
    /// </summary>
    public const uint TrainerManagerPointerOffset = 0x37970;

    public const uint PrimaryTrainerPartyOffset = 0x1C70;

    /// <summary>Second opposing trainer in multi battles, relative to the primary party.</summary>
    public const uint SecondTrainerPartyOffset = 0x1438;

    /// <summary>A single wild Pokémon in the standard HGSS battle allocation.</summary>
    public const uint WildPokemonOffset = 0x38540;

    /// <summary>
    /// An alternate published table assigns this pointer slot to HGSS. Some older tools
    /// assign it to Platinum, so it is a validated fallback rather than an assumption.
    /// </summary>
    public const uint AlternateTrainerManagerPointerOffset = 0x352F4;

    public const uint AlternatePrimaryTrainerPartyOffset = 0x7A0;

    public static bool IsMainRam(uint address) => address is >= 0x02000000 and < 0x02400000;

    public static bool TryAdd(uint left, uint right, out uint result)
    {
        ulong sum = (ulong)left + right;
        result = (uint)sum;
        return sum <= uint.MaxValue && IsMainRam(result);
    }
}
