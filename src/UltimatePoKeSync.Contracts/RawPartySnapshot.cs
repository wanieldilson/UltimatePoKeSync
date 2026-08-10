namespace UltimatePoKeSync.Contracts;

/// <summary>
/// What an emulator delivers: raw bytes, not Pokémon.
/// </summary>
/// <remarks>
/// <para>
/// This is the load-bearing architectural choice (D-006). The emulator script interprets
/// nothing: it does not decrypt, does not checksum, does not map species IDs. It copies a
/// region of memory and states which game it came from.
/// </para>
/// <para>
/// The reason is that decoding is the hard part and should be written once. If every
/// script parsed its own Pokémon, adding an emulator would mean duplicating that logic in
/// Lua, in a language with no tests, and watching the copies diverge. This way a new
/// emulator costs a new script and zero lines of domain logic.
/// </para>
/// </remarks>
/// <param name="Game">The ROM the bytes came from.</param>
/// <param name="PartyCount">
/// Party size according to the emulator, 0-6. Do not trust it blindly: it is read from a
/// byte that can be sampled mid-write. The parser treats it as an upper bound and
/// validates every slot regardless.
/// </param>
/// <param name="PartyData">
/// Contiguous bytes for all slots: <c>SlotSize * 6</c>. These are the bytes exactly as
/// they sit in RAM, so for Gen 3 they are encrypted with permuted substructures.
/// </param>
/// <param name="SlotSize">Size of one slot in bytes. Gen 3: 100.</param>
/// <param name="CapturedAt">Capture time, for latency diagnostics.</param>
/// <param name="Sequence">
/// Monotonic counter assigned by the script. Lets us notice dropped snapshots and discard
/// out-of-order ones.
/// </param>
public sealed record RawPartySnapshot(
    GameIdentity Game,
    int PartyCount,
    ReadOnlyMemory<byte> PartyData,
    int SlotSize,
    DateTimeOffset CapturedAt,
    ulong Sequence)
{
    /// <summary>Number of slots present in the blob, regardless of PartyCount.</summary>
    public int SlotCapacity => SlotSize > 0 ? PartyData.Length / SlotSize : 0;

    /// <summary>
    /// Bytes of a single slot. Performs no validation: interpreting them is the parser's
    /// job.
    /// </summary>
    public ReadOnlyMemory<byte> GetSlot(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, SlotCapacity);
        return PartyData.Slice(index * SlotSize, SlotSize);
    }
}
