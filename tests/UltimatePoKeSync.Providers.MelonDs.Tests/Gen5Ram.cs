using System.Text;

namespace UltimatePoKeSync.Providers.MelonDs.Tests;

/// <summary>
/// A stand-in for a Nintendo DS's main RAM with a Gen 5 game in it: a cartridge header, a
/// pointer where the game keeps one, and a party where the pointer says. See D-040.
/// </summary>
/// <remarks>
/// Laid out at the real addresses rather than convenient ones, so the test exercises the
/// arithmetic the provider actually does — including the mirror the cartridge header is read
/// through.
/// </remarks>
internal static class Gen5Ram
{
    public const uint BaseAddress = 0x02000000;

    public const int Size = 4 * 1024 * 1024;

    /// <summary>Where the verified Italian Black keeps its pointer to the party.</summary>
    public const uint PartyPointer = 0x0224F88C;

    public const uint PartyHead = 0x022348AC;

    /// <summary>A recognisable byte at the start of slot 0, to prove it arrives whole.</summary>
    public const byte SlotMarker = 0x5A;

    public static byte[] Build(
        string gameCode = "IRBI",
        int partyCount = 1,
        uint? pointsTo = null)
    {
        var ram = new byte[Size];

        Header(gameCode, "POKEMON B", revision: 0)
            .CopyTo(ram, (int)(Gen5MemoryMap.CartridgeHeader - BaseAddress));

        uint head = pointsTo ?? PartyHead;
        BitConverter.GetBytes(head).CopyTo(ram, (int)(PartyPointer - BaseAddress));

        int at = (int)(head - BaseAddress);
        BitConverter.GetBytes(Gen5MemoryMap.SlotCapacity).CopyTo(ram, at);
        BitConverter.GetBytes(partyCount).CopyTo(ram, at + Gen5MemoryMap.PartyCountOffset);
        ram[at + Gen5MemoryMap.FirstSlotOffset] = SlotMarker;

        return ram;
    }

    public static byte[] Header(string gameCode, string title, byte revision)
    {
        var header = new byte[Gen5MemoryMap.HeaderLength];
        Encoding.ASCII.GetBytes(title).CopyTo(header, 0);
        Encoding.ASCII.GetBytes(gameCode).CopyTo(header, 0x0C);
        header[0x1E] = revision;
        return header;
    }
}
