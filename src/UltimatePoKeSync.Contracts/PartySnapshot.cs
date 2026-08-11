namespace UltimatePoKeSync.Contracts;

/// <summary>
/// The decoded party at a point in time.
/// </summary>
/// <param name="Game">Source ROM.</param>
/// <param name="Members">Valid slots only, in order. May be empty.</param>
/// <param name="CapturedAt">When the underlying bytes were captured.</param>
/// <param name="Sequence">Sequence of the raw snapshot this came from.</param>
/// <param name="RejectedSlots">
/// Slots that were discarded, and why. Not an error in itself: during a mid-write read,
/// or with an egg in the party, it is normal for a slot to fail validation. Tracking them
/// is what distinguishes "the game is writing" from "the memory map is wrong".
/// </param>
public sealed record PartySnapshot(
    GameIdentity Game,
    IReadOnlyList<PokemonSnapshot> Members,
    DateTimeOffset CapturedAt,
    ulong Sequence,
    IReadOnlyList<RejectedSlot> RejectedSlots)
{
    public static PartySnapshot Empty { get; } = new(
        GameIdentity.Unknown, [], DateTimeOffset.MinValue, 0, []);

    public int Count => Members.Count;

    public bool IsEmpty => Members.Count == 0;

    /// <summary>
    /// The members that can actually fight. Every analysis wants this rather than
    /// <see cref="Members"/>; the display wants <see cref="Members"/>, because an egg is
    /// still something the player is carrying. See D-036.
    /// </summary>
    public IReadOnlyList<PokemonSnapshot> Battlers { get; } =
        [.. Members.Where(member => member.CanBattle)];
}

/// <param name="SlotIndex">Index of the rejected slot.</param>
/// <param name="Reason">Human-readable reason, for diagnostics.</param>
public sealed record RejectedSlot(int SlotIndex, string Reason);
