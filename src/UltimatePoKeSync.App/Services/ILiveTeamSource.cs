using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// Where the window gets its live party from.
/// </summary>
/// <remarks>
/// An interface so the dashboard can be driven by a captured snapshot in a test. Without
/// it the only way to check the view model would be to have mGBA running, which is exactly
/// the kind of verification that never actually gets done. See D-028.
/// </remarks>
public interface ILiveTeamSource : IAsyncDisposable
{
    event EventHandler<EmulatorConnectionState>? StateChanged;

    event EventHandler<PartySnapshot>? PartyChanged;

    /// <summary>The port the user has to match in the bridge script.</summary>
    int Port { get; }

    /// <summary>
    /// Reads emulator memory, when the bridge can. Null for a source that only replays
    /// what it was given. See D-033.
    /// </summary>
    IEmulatorMemoryReader? MemoryReader { get; }

    void Start();
}
