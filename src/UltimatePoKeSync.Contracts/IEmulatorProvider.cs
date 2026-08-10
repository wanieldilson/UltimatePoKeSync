namespace UltimatePoKeSync.Contracts;

/// <summary>
/// A source of raw snapshots. One implementation per emulator: mGBA today, BizHawk or
/// DeSmuME tomorrow. See D-006.
/// </summary>
/// <remarks>
/// The contract is deliberately thin: no notion of a Pokémon, a generation or a memory
/// address. That is what makes the emulator replaceable without touching the rest of the
/// app.
/// </remarks>
public interface IEmulatorProvider : IAsyncDisposable
{
    /// <summary>Human-readable name shown in the UI. E.g. "mGBA".</summary>
    string Name { get; }

    /// <summary>Current connection state.</summary>
    EmulatorConnectionState State { get; }

    /// <summary>Raised on every state transition.</summary>
    event EventHandler<EmulatorConnectionState>? StateChanged;

    /// <summary>
    /// Stream of snapshots, one per detected change in the party.
    /// </summary>
    /// <remarks>
    /// The implementation handles reconnection internally: the stream does not end
    /// because the emulator was closed, it simply stops producing items until the
    /// emulator is back. It only ends when the token is cancelled.
    /// </remarks>
    IAsyncEnumerable<RawPartySnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken);
}

public enum EmulatorConnectionState
{
    /// <summary>Never started.</summary>
    Idle,

    /// <summary>Connection attempt in progress.</summary>
    Connecting,

    /// <summary>Connected, but no recognised ROM is loaded.</summary>
    ConnectedNoGame,

    /// <summary>Connected and receiving snapshots.</summary>
    Streaming,

    /// <summary>Connection lost; the provider is retrying.</summary>
    Reconnecting,

    /// <summary>Unrecoverable error: the provider has stopped retrying.</summary>
    Faulted,
}
