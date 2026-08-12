using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Providers.MelonDs;

/// <summary>
/// Reads Nintendo DS memory out of melonDS through its GDB stub, for Gen 4 and 5. See D-039.
/// </summary>
/// <remarks>
/// <para>
/// Three melonDS facts shape this class. It connects to the **ARM7** stub, on port 3334,
/// because the ARM9 one closes the connection on every command; main RAM is shared between
/// the processors, so nothing is lost. Reads are capped at 256 bytes each and longer ranges
/// are split here rather than by the caller. And one connection is held open for the whole
/// session, because connecting halts the emulated CPU and disconnecting badly leaves the
/// game frozen.
/// </para>
/// <para>
/// A read returns null rather than throwing when the emulator is not there, matching the
/// mGBA reader: a missing emulator is an ordinary state of the world, not an error.
/// </para>
/// </remarks>
public sealed class MelonDsMemoryReader : IEmulatorMemoryReader, IAsyncDisposable
{
    /// <summary>melonDS's default ARM7 stub port. The ARM9's 3333 does not work. See D-039.</summary>
    public const int DefaultPort = 3334;

    private readonly SemaphoreSlim _connecting = new(1, 1);
    private readonly string _host;
    private readonly int _port;

    private GdbRemoteClient? _client;
    private bool _disposed;

    public MelonDsMemoryReader(string host = "127.0.0.1", int port = DefaultPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        _host = host;
        _port = port;
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
                _client = await GdbRemoteClient
                    .ConnectAsync(_host, _port, cancellationToken)
                    .ConfigureAwait(false);
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
