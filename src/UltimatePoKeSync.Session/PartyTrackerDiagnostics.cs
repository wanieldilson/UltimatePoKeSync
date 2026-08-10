namespace UltimatePoKeSync.Session;

/// <summary>
/// Counters describing what the tracker did with the snapshots it received.
/// </summary>
/// <remarks>
/// These are not vanity metrics. They are the only way to tell apart three situations
/// that look identical from the outside — "nothing is happening in the game", "the memory
/// map is wrong" and "the emulator is not connected" — without attaching a debugger.
/// A steady climb in <see cref="InconsistentDiscarded"/> means the addresses are wrong.
/// </remarks>
/// <param name="Received">Raw snapshots that arrived from the provider.</param>
/// <param name="OutOfOrderDiscarded">Snapshots older than one already processed.</param>
/// <param name="UnparsableDiscarded">Snapshots from a game no parser supports.</param>
/// <param name="InconsistentDiscarded">
/// Snapshots with rejected slots, dropped in favour of the last good party.
/// </param>
/// <param name="VolatileSuppressed">
/// Snapshots that changed only in battle state (PP, HP) and so were not worth emitting.
/// </param>
/// <param name="Emitted">Party changes actually handed to the caller.</param>
public sealed record PartyTrackerDiagnostics(
    ulong Received,
    ulong OutOfOrderDiscarded,
    ulong UnparsableDiscarded,
    ulong InconsistentDiscarded,
    ulong VolatileSuppressed,
    ulong Emitted)
{
    public static PartyTrackerDiagnostics Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public override string ToString() =>
        $"received {Received}, emitted {Emitted}, suppressed {VolatileSuppressed}, "
        + $"inconsistent {InconsistentDiscarded}, out-of-order {OutOfOrderDiscarded}, "
        + $"unparsable {UnparsableDiscarded}";
}
