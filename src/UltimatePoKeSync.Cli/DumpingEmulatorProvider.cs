using System.Runtime.CompilerServices;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Cli;

/// <summary>
/// Wraps another provider and writes every snapshot to disk on the way through.
/// </summary>
/// <remarks>
/// A small demonstration that the provider abstraction earns its keep: capturing fixtures
/// needed no change to the mGBA provider, the parser or the tracker — just another
/// <see cref="IEmulatorProvider"/> in front of the real one.
/// </remarks>
internal sealed class DumpingEmulatorProvider : IEmulatorProvider
{
    private readonly IEmulatorProvider _inner;
    private readonly string _directory;
    private readonly Action<string> _onWritten;

    public DumpingEmulatorProvider(IEmulatorProvider inner, string directory, Action<string> onWritten)
    {
        _inner = inner;
        _directory = directory;
        _onWritten = onWritten;
    }

    public string Name => _inner.Name;

    public EmulatorConnectionState State => _inner.State;

    public event EventHandler<EmulatorConnectionState>? StateChanged
    {
        add => _inner.StateChanged += value;
        remove => _inner.StateChanged -= value;
    }

    public async IAsyncEnumerable<RawPartySnapshot> ReadSnapshotsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (RawPartySnapshot raw in _inner
            .ReadSnapshotsAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            _onWritten(RawSnapshotDump.Write(raw, _directory));
            yield return raw;
        }
    }

    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
