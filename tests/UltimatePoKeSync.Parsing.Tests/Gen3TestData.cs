using PKHeX.Core;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Parsing.Tests;

/// <summary>
/// Costruisce snapshot grezzi identici a quelli che arrivano dall'emulatore: byte
/// cifrati, non decifrati. Se i test partissero da dati gia' in chiaro non
/// verificherebbero la parte che conta.
/// </summary>
internal static class Gen3TestData
{
    public const int SlotSize = 100;
    public const int SlotCount = 6;

    public static GameIdentity Emerald { get; } =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    /// <summary>Un Gyarados coerente, con checksum valido.</summary>
    public static PK3 CreateGyarados() => Finalize(new PK3
    {
        Species = 130,
        PID = 0x1A2B3C4D,
        TID16 = 1234,
        SID16 = 5678,
        Stat_Level = 55,
        CurrentLevel = 55,
        IV_HP = 31, IV_ATK = 31, IV_DEF = 20, IV_SPA = 5, IV_SPD = 22, IV_SPE = 30,
        EV_HP = 4, EV_ATK = 252, EV_SPE = 252,
        Move1 = 63, Move2 = 89, Move3 = 44, Move4 = 156,
        Move1_PP = 5, Move2_PP = 10, Move3_PP = 25, Move4_PP = 10,
        HeldItem = 202,
        Nickname = "GYARA",
        OriginalTrainerName = "RED",
        Stat_HPMax = 180, Stat_ATK = 190,
    });

    /// <summary>Pikachu: mono-tipo, serve a verificare la normalizzazione del secondo tipo.</summary>
    public static PK3 CreatePikachu() => Finalize(new PK3
    {
        Species = 25,
        PID = 0x0BADF00D,
        TID16 = 1, SID16 = 2,
        Stat_Level = 20,
        CurrentLevel = 20,
        Move1 = 84,
        Move1_PP = 30,
        Nickname = "PIKACHU",
        OriginalTrainerName = "ASH",
    });

    private static PK3 Finalize(PK3 pk)
    {
        pk.RefreshChecksum();
        return pk;
    }

    /// <summary>
    /// Impacchetta i Pokemon in un blob grezzo da 6 slot, cifrando ciascuno come fa il
    /// gioco in memoria. Gli slot non forniti restano a zero, come in RAM.
    /// </summary>
    public static RawPartySnapshot ToRawSnapshot(
        params PK3[] party) => ToRawSnapshot(Emerald, party.Length, party);

    public static RawPartySnapshot ToRawSnapshot(
        GameIdentity game,
        int declaredCount,
        params PK3[] party)
    {
        var blob = new byte[SlotSize * SlotCount];

        for (int i = 0; i < party.Length && i < SlotCount; i++)
        {
            byte[] slot = party[i].Data.ToArray();
            PokeCrypto.Encrypt3(slot);
            slot.CopyTo(blob.AsSpan(i * SlotSize, SlotSize));
        }

        return new RawPartySnapshot(
            game, declaredCount, blob, SlotSize, DateTimeOffset.UtcNow, 1);
    }

    /// <summary>Slot pieno di byte casuali: simula una lettura a meta' scrittura.</summary>
    public static RawPartySnapshot WithJunkInFirstSlot(int declaredCount = 1)
    {
        var blob = new byte[SlotSize * SlotCount];
        new Random(1234).NextBytes(blob.AsSpan(0, SlotSize));

        return new RawPartySnapshot(
            Emerald, declaredCount, blob, SlotSize, DateTimeOffset.UtcNow, 1);
    }
}
