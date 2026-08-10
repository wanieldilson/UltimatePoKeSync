using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.Providers.MGba.Protocol;

namespace UltimatePoKeSync.Providers.MGba;

/// <summary>
/// Receives raw snapshots from the Lua script running inside mGBA.
/// </summary>
/// <remarks>
/// The client lives here and the server lives in the script (D-003): Lua's
/// <c>socket.connect</c> is blocking, and a failed reconnection attempt would stall
/// emulation. Retry logic belongs in the process that can afford it, which is this one.
/// </remarks>
public sealed class MGbaProvider : IEmulatorProvider
{
    private readonly MGbaProviderOptions _options;
    private EmulatorConnectionState _state = EmulatorConnectionState.Idle;

    public MGbaProvider(MGbaProviderOptions? options = null)
        => _options = options ?? new MGbaProviderOptions();

    public string Name => "mGBA";

    public EmulatorConnectionState State => _state;

    public event EventHandler<EmulatorConnectionState>? StateChanged;

    /// <summary>Number of messages discarded as unreadable, for diagnostics.</summary>
    public int MalformedMessageCount { get; private set; }

    public async IAsyncEnumerable<RawPartySnapshot> ReadSnapshotsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var delay = _options.InitialReconnectDelay;

        while (!cancellationToken.IsCancellationRequested)
        {
            SetState(_state == EmulatorConnectionState.Idle
                ? EmulatorConnectionState.Connecting
                : EmulatorConnectionState.Reconnecting);

            TcpClient? client = await TryConnectAsync(cancellationToken).ConfigureAwait(false);

            if (client is null)
            {
                // mGBA closed, or the script not loaded: the normal state when the app
                // starts first, not an error. Retry with backoff, indefinitely.
                if (!await SafeDelayAsync(delay, cancellationToken).ConfigureAwait(false))
                {
                    yield break;
                }

                delay = NextDelay(delay);
                continue;
            }

            delay = _options.InitialReconnectDelay;
            SetState(EmulatorConnectionState.Streaming);

            using (client)
            using (var reader = new StreamReader(client.GetStream(), Encoding.UTF8, false, 8192))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string? line = await ReadLineOrNullAsync(reader, cancellationToken)
                        .ConfigureAwait(false);

                    if (line is null)
                    {
                        break; // connection closed or failed: reconnect
                    }

                    if (line.Length == 0)
                    {
                        continue;
                    }

                    if (TryParse(line, out RawPartySnapshot? snapshot))
                    {
                        yield return snapshot;
                    }
                }
            }
        }
    }

    private async Task<TcpClient?> TryConnectAsync(CancellationToken cancellationToken)
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ConnectTimeout);

            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token)
                .ConfigureAwait(false);

            return client;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            client.Dispose();
            return null;
        }
        catch (OperationCanceledException)
        {
            client.Dispose();
            return null;
        }
    }

    private static async Task<string?> ReadLineOrNullAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Emulator closed mid-line, socket reset, script reloaded: in every case the
            // only sensible response is to reconnect.
            return null;
        }
    }

    private bool TryParse(string line, out RawPartySnapshot snapshot)
    {
        snapshot = null!;

        PartyMessage? message;
        try
        {
            message = JsonSerializer.Deserialize(line, ProtocolJsonContext.Default.PartyMessage);
        }
        catch (JsonException)
        {
            MalformedMessageCount++;
            return false;
        }

        if (message is null
            || message.Version != _options.SupportedProtocolVersion
            || !string.Equals(message.Type, "party", StringComparison.Ordinal)
            || message.Game?.Code is not { Length: > 0 } code
            || message.Data is not { Length: > 0 } data
            || message.SlotSize <= 0
            || message.Slots <= 0)
        {
            MalformedMessageCount++;
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(data);
        }
        catch (FormatException)
        {
            MalformedMessageCount++;
            return false;
        }

        // A blob of unexpected length means script and client disagree about the format:
        // better to discard it than to interpret it arbitrarily.
        if (bytes.Length != message.SlotSize * message.Slots)
        {
            MalformedMessageCount++;
            return false;
        }

        var game = new GameIdentity(
            code,
            message.Game.Title ?? string.Empty,
            message.Game.Revision,
            ToGeneration(message.Game.Generation));

        snapshot = new RawPartySnapshot(
            game,
            Math.Clamp(message.Count, 0, message.Slots),
            bytes,
            message.SlotSize,
            // Timestamped here rather than in the script: mGBA guarantees no system clock,
            // and loopback latency is negligible.
            DateTimeOffset.UtcNow,
            message.Sequence);

        return true;
    }

    private static PokemonGeneration ToGeneration(int generation) =>
        Enum.IsDefined(typeof(PokemonGeneration), generation)
            ? (PokemonGeneration)generation
            : PokemonGeneration.Unknown;

    private TimeSpan NextDelay(TimeSpan current)
    {
        var doubled = TimeSpan.FromTicks(current.Ticks * 2);
        return doubled > _options.MaxReconnectDelay ? _options.MaxReconnectDelay : doubled;
    }

    private static async Task<bool> SafeDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void SetState(EmulatorConnectionState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;
        StateChanged?.Invoke(this, state);
    }

    public ValueTask DisposeAsync()
    {
        SetState(EmulatorConnectionState.Idle);
        return ValueTask.CompletedTask;
    }
}
