using System.Runtime.CompilerServices;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Providers.MelonDs;

/// <summary>
/// Streams party snapshots out of melonDS, for Gen 5. See D-039 and D-040.
/// </summary>
/// <remarks>
/// <para>
/// This polls where the mGBA provider is pushed to. There is no script on the other side to
/// notice a change and send it: melonDS answers questions, so the questions have to be
/// asked. Each poll is one 4-byte read to follow the pointer and six of 256 bytes for the
/// party, about 95 ms in total (D-039), which is why the default interval is a second
/// rather than a frame.
/// </para>
/// <para>
/// A snapshot is emitted only when the bytes change, so a party sitting still costs the
/// consumer nothing.
/// </para>
/// </remarks>
public sealed class MelonDsProvider : IEmulatorProvider, IEmulatorMemoryReader
{
    private readonly MelonDsMemoryReader _reader;
    private readonly TimeSpan _interval;

    private EmulatorConnectionState _state = EmulatorConnectionState.Idle;
    private ulong _sequence;

    public MelonDsProvider(
        string host = "127.0.0.1",
        int port = MelonDsMemoryReader.DefaultPort,
        TimeSpan? pollInterval = null)
    {
        _reader = new MelonDsMemoryReader(host, port);
        _interval = pollInterval ?? TimeSpan.FromSeconds(1);
    }

    public string Name => "melonDS";

    public EmulatorConnectionState State => _state;

    public event EventHandler<EmulatorConnectionState>? StateChanged;

    public bool CanRead => _reader.CanRead;

    public Task<byte[]?> ReadMemoryAsync(
        uint address,
        int length,
        CancellationToken cancellationToken = default) =>
        _reader.ReadMemoryAsync(address, length, cancellationToken);

    public async IAsyncEnumerable<RawPartySnapshot> ReadSnapshotsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        byte[]? previous = null;
        int previousCount = -1;

        SetState(EmulatorConnectionState.Connecting);

        while (!cancellationToken.IsCancellationRequested)
        {
            RawPartySnapshot? snapshot = null;

            try
            {
                snapshot = await ReadPartyAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            if (snapshot is null)
            {
                // melonDS closed, the stub is off, or the game is not one we have mapped.
                // All of them are ordinary states of the world; keep asking.
                SetState(_state == EmulatorConnectionState.Streaming
                    ? EmulatorConnectionState.Reconnecting
                    : EmulatorConnectionState.Connecting);
            }
            else
            {
                SetState(EmulatorConnectionState.Streaming);

                bool changed = previousCount != snapshot.PartyCount
                    || previous is null
                    || !previous.AsSpan().SequenceEqual(snapshot.PartyData.Span);

                if (changed)
                {
                    previous = snapshot.PartyData.ToArray();
                    previousCount = snapshot.PartyCount;
                    yield return snapshot;
                }
            }

            try
            {
                await Task.Delay(_interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// One poll: who is this game, where does its party live, and what is in it. Returns
    /// null for anything that is not a complete, plausible answer — a half-read party is
    /// worse than none, because it parses.
    /// </summary>
    private async Task<RawPartySnapshot?> ReadPartyAsync(CancellationToken cancellationToken)
    {
        byte[]? header = await _reader
            .ReadMemoryAsync(Gen5MemoryMap.CartridgeHeader, Gen5MemoryMap.HeaderLength, cancellationToken)
            .ConfigureAwait(false);

        if (header is null || Gen5MemoryMap.ReadIdentity(header) is not GameIdentity game)
        {
            return null;
        }

        if (Gen5MemoryMap.For(game.GameCode) is not Gen5MemoryMap map)
        {
            return null;
        }

        byte[]? pointer = await _reader
            .ReadMemoryAsync(map.PartyPointer, 4, cancellationToken)
            .ConfigureAwait(false);

        if (pointer is null)
        {
            return null;
        }

        uint head = BitConverter.ToUInt32(pointer);
        if (head is < 0x02000000 or >= 0x02400000)
        {
            // The pointer does not point into main RAM, so the game has not built its save
            // blocks yet — it is still on the title screen, or mid-load.
            return null;
        }

        int length = Gen5MemoryMap.FirstSlotOffset
            + (Gen5MemoryMap.SlotSize * Gen5MemoryMap.SlotCapacity);

        byte[]? block = await _reader
            .ReadMemoryAsync(head, length, cancellationToken)
            .ConfigureAwait(false);

        if (block is null)
        {
            return null;
        }

        int count = BitConverter.ToInt32(block, Gen5MemoryMap.PartyCountOffset);
        if (count is < 0 or > Gen5MemoryMap.SlotCapacity)
        {
            // Following the pointer landed somewhere that is not a party. Saying nothing is
            // the only honest answer; the alternative is a team invented out of noise.
            return null;
        }

        return new RawPartySnapshot(
            game,
            count,
            block.AsMemory(Gen5MemoryMap.FirstSlotOffset),
            Gen5MemoryMap.SlotSize,
            DateTimeOffset.UtcNow,
            ++_sequence);
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

    public ValueTask DisposeAsync() => _reader.DisposeAsync();
}
