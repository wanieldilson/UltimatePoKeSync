using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.Parsing;
using UltimatePoKeSync.Providers.MelonDs;
using UltimatePoKeSync.Providers.MGba;
using UltimatePoKeSync.Session;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// Owns the live pipeline for the UI: providers, parser and change tracking.
/// </summary>
/// <remarks>
/// <para>
/// Both emulators are watched at once and whichever answers wins. Asking someone to pick
/// mGBA or melonDS from a menu would be asking them a question they have already answered by
/// opening one of them, and the promise this app was built on is that you open it and it
/// works. See D-042.
/// </para>
/// <para>
/// Events are raised on a background thread. The view model marshals them, so this class
/// stays free of any dependency on a UI framework.
/// </para>
/// </remarks>
public sealed class LiveTeamService : ILiveTeamSource
{
    private readonly List<Watched> _watched = [];
    private readonly CancellationTokenSource _cancellation = new();
    private readonly object _gate = new();

    private Watched? _active;
    private Task? _loops;

    public LiveTeamService()
        : this(new MGbaProviderOptions())
    {
    }

    public LiveTeamService(MGbaProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;

        var mgba = new MGbaProvider(options);
        Add(mgba, mgba, "mGBA");

        var melon = new MelonDsProvider();
        Add(melon, melon, "melonDS");
    }

    public event EventHandler<EmulatorConnectionState>? StateChanged;

    public event EventHandler<PartySnapshot>? PartyChanged;

    public MGbaProviderOptions Options { get; }

    public int Port => Options.Port;

    /// <summary>
    /// The reader belonging to whichever emulator last sent a party, so a sprite request
    /// goes to the machine the team came from rather than to the one that happens to be
    /// listed first. Null until something has arrived. See D-033.
    /// </summary>
    public IEmulatorMemoryReader? MemoryReader
    {
        get
        {
            lock (_gate)
            {
                return _active?.Reader;
            }
        }
    }

    /// <summary>The emulator currently feeding the window, for the UI to name.</summary>
    public string? ActiveEmulator
    {
        get
        {
            lock (_gate)
            {
                return _active?.Name;
            }
        }
    }

    public void Start() =>
        _loops ??= Task.WhenAll([.. _watched.Select(RunAsync)]);

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();

        if (_loops is not null)
        {
            try
            {
                await _loops;
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }

        foreach (Watched watched in _watched)
        {
            await watched.Provider.DisposeAsync();
        }

        _cancellation.Dispose();
    }

    private void Add(IEmulatorProvider provider, IEmulatorMemoryReader reader, string name)
    {
        var watched = new Watched(
            provider,
            reader,
            name,
            new PartyTracker(provider, PartyParserResolver.CreateDefault()));

        // Only the emulator that is actually feeding us gets to report a state. Otherwise
        // the one that is not running would keep announcing that it is reconnecting, over
        // the top of the one that is working perfectly well.
        provider.StateChanged += (_, state) =>
        {
            lock (_gate)
            {
                if (_active is not null && _active != watched)
                {
                    return;
                }
            }

            StateChanged?.Invoke(this, state);
        };

        _watched.Add(watched);
    }

    private async Task RunAsync(Watched watched)
    {
        try
        {
            await foreach (PartySnapshot party in watched.Tracker.TrackAsync(_cancellation.Token))
            {
                lock (_gate)
                {
                    // The most recent party wins, so swapping emulators mid-session works
                    // without anybody having to say so.
                    _active = watched;
                }

                PartyChanged?.Invoke(this, party);
            }
        }
        catch (OperationCanceledException)
        {
            // The window closed.
        }
    }

    private sealed record Watched(
        IEmulatorProvider Provider,
        IEmulatorMemoryReader Reader,
        string Name,
        PartyTracker Tracker);
}
