namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Reads a region of the emulator's memory on demand.
/// </summary>
/// <remarks>
/// Separate from <see cref="IEmulatorProvider"/>, which pushes party snapshots and asks
/// for nothing. Not every emulator will be able to answer questions, and nothing that only
/// wants the party should have to care that this exists. See D-033.
/// </remarks>
public interface IEmulatorMemoryReader
{
    /// <summary>Whether a request could be served right now — that is, whether we are connected.</summary>
    bool CanRead { get; }

    /// <summary>
    /// Returns the bytes at <paramref name="address"/>, or <see langword="null"/> when the
    /// emulator cannot serve the request. ROM starts at <c>0x08000000</c>.
    /// </summary>
    Task<byte[]?> ReadMemoryAsync(
        uint address,
        int length,
        CancellationToken cancellationToken = default);
}
