using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Providers.MelonDs;

/// <summary>
/// Reads Nintendo DS memory out of melonDS through its GDB stub, for Gen 4 and 5. See D-039.
/// </summary>
/// <remarks>
/// <para>
/// Three melonDS facts shape this class. Either CPU's stub can serve a party, because they
/// share main RAM, so both are tried — a stub whose last client vanished without detaching
/// still shakes hands and then hangs up, and the other one is usually fine. Reads are capped
/// at 256 bytes each and longer ranges are split here rather than by the caller. And one
/// connection is held open for the whole session, because connecting halts the emulated CPU
/// and disconnecting badly is what wedges a stub in the first place.
/// </para>
/// <para>
/// A read returns null rather than throwing when the emulator is not there, matching the
/// mGBA reader: a missing emulator is an ordinary state of the world, not an error.
/// </para>
/// </remarks>
public sealed class MelonDsMemoryReader : IEmulatorMemoryReader, IAsyncDisposable
{
    /// <summary>melonDS's two stub ports: the ARM9 and the ARM7. See D-039.</summary>
    public const int Arm9Port = 3333;

    public const int Arm7Port = 3334;

    /// <summary>Kept for callers that name a port; the reader tries both by default.</summary>
    public const int DefaultPort = Arm7Port;

    private readonly SemaphoreSlim _connecting = new(1, 1);
    private readonly string _host;
    private readonly IReadOnlyList<int> _ports;

    private GdbRemoteClient? _client;
    private bool _disposed;

    /// <summary>
    /// Talks to whichever of melonDS's two stubs answers. Both CPUs share main RAM, so
    /// either can read a party — and either can be left wedged by a client that died
    /// without detaching, in which case it completes the handshake and then hangs up on
    /// the first command. Trying both is what makes that survivable without asking the
    /// player to restart their emulator. See D-039.
    /// </summary>
    public MelonDsMemoryReader(string host = "127.0.0.1", int? port = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        _host = host;
        _ports = port is int chosen ? [chosen] : [Arm7Port, Arm9Port];
    }

    public string Name => "melonDS";

    public bool CanRead => _client is not null && !_disposed;

    public async Task<byte[]?> ReadMemoryAsync(
        uint address,
        int length,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ObjectDisposedException.ThrowIf(_disposed, this);

        GdbRemoteClient? client = await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        if (client is null)
        {
            return null;
        }

        var result = new byte[length];

        try
        {
            for (int done = 0; done < length;)
            {
                int chunk = Math.Min(GdbRemoteClient.MaximumReadLength, length - done);
                byte[] part = await client
                    .ReadMemoryAsync((uint)(address + done), chunk, cancellationToken)
                    .ConfigureAwait(false);

                part.CopyTo(result, done);
                done += chunk;
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The emulator went away, or refused. Drop the connection so the next call
            // reconnects instead of talking into a dead socket, and report nothing read:
            // a partly-filled buffer would parse into a Pokemon that never existed.
            await DropAsync().ConfigureAwait(false);
            return null;
        }

        return result;
    }

    private async Task<GdbRemoteClient?> EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client is { } existing)
        {
            return existing;
        }

        await _connecting.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_client is null && !_disposed)
            {
                _client = await ConnectToWorkingStubAsync(cancellationToken).ConfigureAwait(false);
            }

            return _client;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // melonDS is not running, or the stub is switched off. Both are things the
            // person at the keyboard fixes, not things to crash over.
            return null;
        }
        finally
        {
            _connecting.Release();
        }
    }

    /// <summary>
    /// Opens each stub in turn and asks it something, because a wedged one still shakes
    /// hands. The question is the cartridge header, which every DS game has.
    /// </summary>
    private async Task<GdbRemoteClient?> ConnectToWorkingStubAsync(CancellationToken cancellationToken)
    {
        foreach (int port in _ports)
        {
            GdbRemoteClient? candidate = null;

            try
            {
                candidate = await GdbRemoteClient
                    .ConnectAsync(_host, port, cancellationToken)
                    .ConfigureAwait(false);

                await candidate.ReadMemoryAsync(0x02000000, 16, cancellationToken)
                    .ConfigureAwait(false);

                return candidate;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                if (candidate is not null)
                {
                    await candidate.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        return null;
    }

    private async Task DropAsync()
    {
        GdbRemoteClient? client = Interlocked.Exchange(ref _client, null);
        if (client is not null)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        await DropAsync().ConfigureAwait(false);
        _connecting.Dispose();
    }
}
