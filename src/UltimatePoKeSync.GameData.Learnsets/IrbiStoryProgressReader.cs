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
    /// and only the two distances are wrong. See D-053.
    /// </para>
    /// <para>
    /// The differential dump has since been run, and the map is found. Three readings, outside
    /// then in the starting house then outside again, isolated a four-byte field whose low half
    /// is the map: 317 for Route 1 and 390 for the house, and identical in both outdoor
    /// readings. It lives at <c>+0x10C</c> into the block pointed at by the seventh directory
    /// entry, at <see cref="MapBlockPointerAddress"/>, not at a fixed distance from the party.
    /// The two values that misled the first attempt were both real and both wrong: the old
    /// <c>+0x780</c> holds the map the player last saved at, which never changes while walking
    /// around, and <c>+0x776</c> is a counter that only ever increments.
    /// </para>
    /// <para>
    /// The badge byte is still unfound. It cannot be isolated the same way while the count is
    /// zero, because every wrong address holding a zero looks correct; it needs a dump either
    /// side of the first Gym. That is why this stays false: the map alone unlocks nothing,
    /// since D-053 gates routes on badges and treats the map as evidence only.
    /// </para>
    /// </remarks>
    public static bool OffsetsVerifiedLive => false;

    /// <summary>The verified IRBI pointer to the live party/save-block head.</summary>
    public const uint PartyPointerAddress = 0x0224F88C;

    /// <summary>
    /// The directory entry pointing at the block that holds the player's position, measured
    /// live on 2026-08-14. The party is the third entry of that directory and this is the
    /// seventh, sixteen bytes along.
    /// </summary>
    public const uint MapBlockPointerAddress = 0x0224F89C;

    /// <summary>
    /// Where the map sits inside that block. Four bytes, of which only the low sixteen are
    /// the map id; the high half changed from 5 outdoors to 0 indoors and is not understood
    /// yet, so it is masked off rather than guessed at.
    /// </summary>
    public const uint MapIdBlockOffset = 0x10C;

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
