using System.Collections.Concurrent;
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
public sealed class MGbaProvider : IEmulatorProvider, IEmulatorMemoryReader
{
    private readonly MGbaProviderOptions _options;

    /// <summary>Requests waiting for a reply, by the id the script echoes back.</summary>
    private readonly ConcurrentDictionary<int, TaskCompletionSource<byte[]?>> _pending = new();

    /// <summary>One writer at a time: a command has to reach the script as a whole line.</summary>
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private static readonly UTF8Encoding NoBomUtf8 = new(encoderShouldEmitUTF8Identifier: false);

    private EmulatorConnectionState _state = EmulatorConnectionState.Idle;
    private StreamWriter? _writer;
    private int _nextRequestId;

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

            NetworkStream stream = client.GetStream();

            // No byte-order mark: the script reads lines of JSON, and a BOM in front of the
            // first one is not JSON. Both wrappers leave the stream open, so closing the
            // client is what closes it, once.
            var writer = new StreamWriter(stream, NoBomUtf8, 8192, leaveOpen: true)
            {
                AutoFlush = false,
            };
            _writer = writer;

            using (client)
            using (writer)
            using (var reader = new StreamReader(
                stream, NoBomUtf8, detectEncodingFromByteOrderMarks: false, 8192, leaveOpen: true))
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

                    // A reply belongs to whoever asked for it, not to the party stream.
                    if (TryCompleteRequest(line))
                    {
                        continue;
                    }

                    if (TryParse(line, out RawPartySnapshot? snapshot))
                    {
                        yield return snapshot;
                    }
                }
            }

            _writer = null;
            FailPendingRequests();
        }
    }

    public bool CanRead => _writer is not null && _state == EmulatorConnectionState.Streaming;

    /// <inheritdoc />
    public async Task<byte[]?> ReadMemoryAsync(
        uint address,
        int length,
        CancellationToken cancellationToken = default)
    {
        if (length <= 0 || length > _options.MaximumReadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        StreamWriter? writer = _writer;
        if (writer is null)
        {
            return null;
        }

        int id = Interlocked.Increment(ref _nextRequestId);
        var completion = new TaskCompletionSource<byte[]?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;

        try
        {
            string command = "{\"type\":\"read\",\"id\":" + id
                    + ",\"address\":" + address
                    + ",\"length\":" + length + "}";

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await writer.WriteLineAsync(command).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_options.ReadTimeout);

            return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A dropped connection, or a script that never answers. The caller asked for
            // bytes, not for an exception to handle: it gets nothing and carries on.
            return null;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private bool TryCompleteRequest(string line)
    {
        if (!MemoryMessage.TryParse(line, _options.SupportedProtocolVersion, out int id, out byte[]? data))
        {
            return false;
        }

        if (_pending.TryRemove(id, out TaskCompletionSource<byte[]?>? completion))
        {
            completion.TrySetResult(data);
        }

        // Consumed either way: a reply nobody is waiting for is still not a party.
        return true;
    }

    private void FailPendingRequests()
    {
        foreach (int id in _pending.Keys)
        {
            if (_pending.TryRemove(id, out TaskCompletionSource<byte[]?>? completion))
            {
                completion.TrySetResult(null);
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
        catch (Exception)
        {
            // Every failure to connect is the same answer: no client. The two clauses this
            // replaces left a hole between them, because one only caught while cancellation
            // had not been requested and the other only caught OperationCanceledException.
            // A connect refused at the very moment the token is cancelled throws
            // SocketException with cancellation requested, which matched neither and escaped
            // into the caller. Rare, load-dependent, and it made the test for this method
            // fail once in a full run before passing three times alone.
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
            // Accept anything up to what we understand. A version 1 script still streams
            // parties correctly; it simply cannot answer a read. Rejecting it would break
            // a bridge that works.
            || message.Version > _options.SupportedProtocolVersion
            || message.Version < 1
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
