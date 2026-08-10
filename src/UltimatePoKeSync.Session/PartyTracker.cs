using System.Runtime.CompilerServices;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Session;

/// <summary>
/// Turns a stream of raw snapshots into a stream of party changes worth reacting to.
/// </summary>
/// <remarks>
/// <para>
/// This is where the three-way split becomes useful: the provider knows how to obtain
/// bytes, the parser knows how to read them, and neither is in a position to decide
/// <em>when</em> a change deserves a recomputation of the analysis. That judgement lives
/// here. See D-018.
/// </para>
/// <para>
/// It depends on <c>Contracts</c> alone, so it is unaware of both mGBA and PKHeX.
/// </para>
/// </remarks>
public sealed class PartyTracker
{
    /// <summary>
    /// How many consecutive inconsistent snapshots to skip before giving up and emitting
    /// one anyway.
    /// </summary>
    /// <remarks>
    /// Skipping inconsistent snapshots protects against torn reads, but taken literally it
    /// would stall forever on a party that is genuinely broken — a bad egg, say, which is
    /// a real and permanent state. After this many attempts the tracker concludes the
    /// problem is the party rather than the timing, and reports what it sees.
    /// </remarks>
    private const int MaxConsecutiveInconsistent = 5;

    private readonly IEmulatorProvider _provider;
    private readonly IPartyParserResolver _resolver;

    private ulong _lastSequence;
    private string? _lastSignature;
    private int _consecutiveInconsistent;

    private ulong _received;
    private ulong _outOfOrder;
    private ulong _unparsable;
    private ulong _inconsistent;
    private ulong _volatileSuppressed;
    private ulong _emitted;

    public PartyTracker(IEmulatorProvider provider, IPartyParserResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(resolver);

        _provider = provider;
        _resolver = resolver;
    }

    /// <summary>The last party emitted, or <see cref="PartySnapshot.Empty"/>.</summary>
    public PartySnapshot Current { get; private set; } = PartySnapshot.Empty;

    public PartyTrackerDiagnostics Diagnostics => new(
        _received, _outOfOrder, _unparsable, _inconsistent, _volatileSuppressed, _emitted);

    /// <summary>
    /// Emits the party every time it meaningfully changes: a member added, removed,
    /// levelled, evolved, re-ordered, or its moves, item, EVs or IVs altered.
    /// </summary>
    /// <remarks>
    /// It does not emit for battle state. During a fight the party bytes change on almost
    /// every turn as PP and HP move, and none of that alters a single recommendation.
    /// </remarks>
    public async IAsyncEnumerable<PartySnapshot> TrackAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (RawPartySnapshot raw in _provider
            .ReadSnapshotsAsync(cancellationToken)
            .ConfigureAwait(false))
        {
            _received++;

            // The emulator can be reset or the script reloaded, which restarts the
            // sequence at 1. Treat a jump backwards to a very low number as a restart
            // rather than as a stale message.
            bool looksLikeRestart = raw.Sequence <= 1;
            if (!looksLikeRestart && raw.Sequence <= _lastSequence)
            {
                _outOfOrder++;
                continue;
            }

            _lastSequence = raw.Sequence;

            IPartyParser? parser = _resolver.Resolve(raw.Game);
            if (parser is null)
            {
                _unparsable++;
                continue;
            }

            PartySnapshot party = parser.Parse(raw);

            if (party.RejectedSlots.Count > 0
                && _consecutiveInconsistent < MaxConsecutiveInconsistent)
            {
                // Hold on to the last good party rather than briefly showing a team with a
                // member missing.
                _consecutiveInconsistent++;
                _inconsistent++;
                continue;
            }

            _consecutiveInconsistent = 0;

            string signature = PartySignature.Compute(party);
            if (signature == _lastSignature)
            {
                _volatileSuppressed++;
                continue;
            }

            _lastSignature = signature;
            Current = party;
            _emitted++;

            yield return party;
        }
    }
}
