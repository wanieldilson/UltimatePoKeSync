using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.Parsing;
using UltimatePoKeSync.Providers.MGba;
using UltimatePoKeSync.Session;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// Owns the live pipeline for the UI: provider, parser and change tracking.
/// </summary>
/// <remarks>
/// Events are raised on a background thread. The view model marshals them, so this class
/// stays free of any dependency on a UI framework.
/// </remarks>
public sealed class LiveTeamService : ILiveTeamSource
{
    private readonly MGbaProvider _provider;
    private readonly PartyTracker _tracker;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _loop;

    public LiveTeamService()
        : this(new MGbaProviderOptions())
    {
    }

    public LiveTeamService(MGbaProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Options = options;
        _provider = new MGbaProvider(options);
        _tracker = new PartyTracker(_provider, PartyParserResolver.CreateDefault());
        _provider.StateChanged += (_, state) => StateChanged?.Invoke(this, state);
    }

    public event EventHandler<EmulatorConnectionState>? StateChanged;

    public event EventHandler<PartySnapshot>? PartyChanged;

    public MGbaProviderOptions Options { get; }

    public int Port => Options.Port;

    public IEmulatorMemoryReader MemoryReader => _provider;

    public void Start() => _loop ??= Task.Run(RunAsync);

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync();

        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }

        await _provider.DisposeAsync();
        _cancellation.Dispose();
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (PartySnapshot party in _tracker.TrackAsync(_cancellation.Token))
            {
                PartyChanged?.Invoke(this, party);
            }
        }
        catch (OperationCanceledException)
        {
            // The window closed.
        }
    }
}
