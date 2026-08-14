using System.Numerics;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData.Learnsets;

/// <summary>
/// Reads the progress facts verified against the Italian Pokémon Black ROM (<c>IRBI</c>).
/// Every address is reached from the same live party pointer the melonDS provider already
/// validates; nothing here is generalised to another language or to Black 2/White 2.
/// </summary>
public sealed class IrbiStoryProgressReader : IStoryProgressReader
{
    /// <summary>
    /// Whether the offsets below have been proven against a running game. False.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They were taken as fixed distances from the party head, which assumes the save blocks
    /// sit contiguously in memory. D-040 found the opposite: the party is reached through the
    /// third entry of an eighteen-pointer directory, so each block is its own allocation and
    /// <c>Misc5BW</c> and <c>PlayerPosition5</c> have their own entries rather than living a
    /// fixed distance away. PKHeX models them as separate blocks for the same reason.
    /// </para>
    /// <para>
    /// Tested live on 2026-08-14 against Italian Black: the map id read here stayed at 317
    /// across a change of map, which the real <c>PlayerPosition5.M</c> cannot do. A badge
    /// count of zero read at the same time proves nothing either way, because a wrong address
    /// pointing at any zero byte gives the same answer.
    /// </para>
    /// <para>
    /// The reader is kept whole, and tested, because the transport and the guards are right
    /// and only the two distances are wrong. Finding the real ones means walking the pointer
    /// directory rather than adding a constant, and that needs a live differential dump: read
    /// a window, move in the game, read again, and keep what changed. Flipping this to true is
    /// the only change needed once that is done. See D-053.
    /// </para>
    /// </remarks>
    public static bool OffsetsVerifiedLive => false;

    /// <summary>The verified IRBI pointer to the live party/save-block head.</summary>
    public const uint PartyPointerAddress = 0x0224F88C;

    /// <summary>PlayerPosition5.M relative to the verified party head.</summary>
    public const uint MapIdOffset = 0x780;

    /// <summary>Misc5BW.Badges relative to the verified party head.</summary>
    public const uint BadgeMaskOffset = 0x8404;

    private const uint MainRamStart = 0x02000000;
    private const uint MainRamEnd = 0x02400000;

    private readonly IEmulatorMemoryReader _memory;

    public IrbiStoryProgressReader(IEmulatorMemoryReader memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
    }

    public bool Supports(GameIdentity game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return game.Generation == PokemonGeneration.Gen5 &&
            game.GameCode == "IRBI" &&
            game.Revision == 0;
    }

    public async Task<DetectedStoryProgress?> ReadAsync(
        GameIdentity game,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (!Supports(game))
        {
            return null;
        }
        if (!_memory.CanRead)
        {
            return null;
        }

        byte[]? pointer = await _memory
            .ReadMemoryAsync(PartyPointerAddress, sizeof(uint), cancellationToken)
            .ConfigureAwait(false);
        if (pointer is not { Length: sizeof(uint) })
        {
            return null;
        }

        uint head = BitConverter.ToUInt32(pointer);
        if (head < MainRamStart || head >= MainRamEnd ||
            head > MainRamEnd - BadgeMaskOffset - 1)
        {
            return null;
        }

        byte[]? badges = await _memory.ReadMemoryAsync(
            head + BadgeMaskOffset,
            1,
            cancellationToken).ConfigureAwait(false);
        if (badges is not { Length: 1 })
        {
            return null;
        }

        byte[]? map = await _memory.ReadMemoryAsync(
            head + MapIdOffset,
            sizeof(int),
            cancellationToken).ConfigureAwait(false);

        int? mapId = map is { Length: sizeof(int) }
            ? BitConverter.ToInt32(map)
            : null;

        return new DetectedStoryProgress(
            BitOperations.PopCount(badges[0]),
            mapId,
            "IRBI live save blocks: verified one-byte badge mask and PlayerPosition5 map id.");
    }
}
