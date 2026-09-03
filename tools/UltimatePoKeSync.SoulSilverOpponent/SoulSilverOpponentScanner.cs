using System.Text;
using PKHeX.Core;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.SoulSilverOpponent;

/// <summary>Reads IPGE opponent records without adding Gen 4 support to the main app.</summary>
internal sealed class SoulSilverOpponentScanner
{
    private const int English = 2;
    private const int Generation = 4;
    private const ushort MaximumSpecies = 493;

    private readonly IEmulatorMemoryReader _memory;
    private readonly GameStrings _strings = GameInfo.GetStrings("en");

    public SoulSilverOpponentScanner(IEmulatorMemoryReader memory)
    {
        ArgumentNullException.ThrowIfNull(memory);
        _memory = memory;
    }

    public async Task<OpponentScan> ScanAsync(CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();

        byte[]? header = await _memory.ReadMemoryAsync(
            SoulSilverMemoryMap.CartridgeHeader,
            SoulSilverMemoryMap.HeaderLength,
            cancellationToken).ConfigureAwait(false);

        if (header is null)
        {
            return Empty(ScanState.EmulatorUnavailable, diagnostics);
        }

        (string title, string code, int revision) = ReadIdentity(header);
        if (!string.Equals(code, SoulSilverMemoryMap.SupportedGameCode, StringComparison.Ordinal))
        {
            diagnostics.Add($"Expected IPGE, read {DisplayCode(code)}.");
            return new OpponentScan(
                ScanState.UnsupportedGame, code, title, revision, 0, [], diagnostics);
        }

        byte[]? rootBytes = await _memory.ReadMemoryAsync(
            SoulSilverMemoryMap.RootPointerAddress,
            sizeof(uint),
            cancellationToken).ConfigureAwait(false);

        if (rootBytes is null)
        {
            diagnostics.Add("The SoulSilver root pointer could not be read.");
            return new OpponentScan(
                ScanState.EmulatorUnavailable, code, title, revision, 0, [], diagnostics);
        }

        uint root = BitConverter.ToUInt32(rootBytes);
        if (!SoulSilverMemoryMap.IsMainRam(root))
        {
            diagnostics.Add($"0x{SoulSilverMemoryMap.RootPointerAddress:X8} pointed outside main RAM: 0x{root:X8}.");
            return new OpponentScan(
                ScanState.InvalidRootPointer, code, title, revision, root, [], diagnostics);
        }

        diagnostics.Add($"IPGE rev{revision}; root 0x{root:X8}.");

        List<OpponentRoster> rosters = await ReadTrainerLayoutAsync(
            root,
            SoulSilverMemoryMap.TrainerManagerPointerOffset,
            SoulSilverMemoryMap.PrimaryTrainerPartyOffset,
            "documented HGSS layout",
            cancellationToken,
            diagnostics).ConfigureAwait(false);

        if (rosters.Count == 0)
        {
            rosters = await ReadTrainerLayoutAsync(
                root,
                SoulSilverMemoryMap.AlternateTrainerManagerPointerOffset,
                SoulSilverMemoryMap.AlternatePrimaryTrainerPartyOffset,
                "alternate published layout",
                cancellationToken,
                diagnostics).ConfigureAwait(false);
        }

        if (rosters.Count == 0 && SoulSilverMemoryMap.TryAdd(
            root, SoulSilverMemoryMap.WildPokemonOffset, out uint wildAddress))
        {
            IReadOnlyList<OpponentPokemon> wild = await ReadPartyAsync(
                wildAddress, maximumSlots: 1, cancellationToken).ConfigureAwait(false);

            diagnostics.Add($"Wild candidate 0x{wildAddress:X8}: {Describe(wild)}.");
            if (wild.Count > 0)
            {
                rosters.Add(new OpponentRoster("Wild opponent", wildAddress, wild));
            }
        }

        return new OpponentScan(
            ScanState.Ready, code, title, revision, root, rosters, diagnostics);
    }

    private async Task<List<OpponentRoster>> ReadTrainerLayoutAsync(
        uint root,
        uint managerPointerOffset,
        uint partyOffset,
        string layout,
        CancellationToken cancellationToken,
        List<string> diagnostics)
    {
        var rosters = new List<OpponentRoster>();
        if (!SoulSilverMemoryMap.TryAdd(root, managerPointerOffset, out uint pointerAddress))
        {
            diagnostics.Add($"The {layout} manager pointer overflowed main RAM.");
            return rosters;
        }

        byte[]? pointerBytes = await _memory.ReadMemoryAsync(
            pointerAddress, sizeof(uint), cancellationToken).ConfigureAwait(false);
        if (pointerBytes is null)
        {
            diagnostics.Add($"The {layout} manager pointer at 0x{pointerAddress:X8} could not be read.");
            return rosters;
        }

        uint manager = BitConverter.ToUInt32(pointerBytes);
        if (!SoulSilverMemoryMap.IsMainRam(manager)
            || !SoulSilverMemoryMap.TryAdd(manager, partyOffset, out uint primaryAddress))
        {
            diagnostics.Add($"The {layout} manager at 0x{pointerAddress:X8} was not active (0x{manager:X8}).");
            return rosters;
        }

        IReadOnlyList<OpponentPokemon> primary = await ReadPartyAsync(
            primaryAddress, SoulSilverMemoryMap.PartyCapacity, cancellationToken).ConfigureAwait(false);
        diagnostics.Add($"The {layout} party at 0x{primaryAddress:X8}: {Describe(primary)}.");
        if (primary.Count == 0)
        {
            return rosters;
        }

        rosters.Add(new OpponentRoster("Opponent", primaryAddress, primary));

        if (SoulSilverMemoryMap.TryAdd(
            primaryAddress, SoulSilverMemoryMap.SecondTrainerPartyOffset, out uint secondAddress))
        {
            IReadOnlyList<OpponentPokemon> second = await ReadPartyAsync(
                secondAddress, SoulSilverMemoryMap.PartyCapacity, cancellationToken).ConfigureAwait(false);
            diagnostics.Add($"Second-opponent candidate 0x{secondAddress:X8}: {Describe(second)}.");
            if (second.Count > 0)
            {
                rosters.Add(new OpponentRoster("Opponent 2", secondAddress, second));
            }
        }

        return rosters;
    }

    private async Task<IReadOnlyList<OpponentPokemon>> ReadPartyAsync(
        uint address,
        int maximumSlots,
        CancellationToken cancellationToken)
    {
        int length = checked(SoulSilverMemoryMap.SlotSize * maximumSlots);
        byte[]? data = await _memory.ReadMemoryAsync(address, length, cancellationToken)
            .ConfigureAwait(false);
        if (data is null || data.Length != length)
        {
            return [];
        }

        var members = new List<OpponentPokemon>(maximumSlots);
        for (int slot = 0; slot < maximumSlots; slot++)
        {
            ReadOnlySpan<byte> bytes = data.AsSpan(
                slot * SoulSilverMemoryMap.SlotSize,
                SoulSilverMemoryMap.SlotSize);

            if (!TryDecode(slot, bytes, out OpponentPokemon? pokemon))
            {
                // Opponent parties are contiguous. Stopping at the first invalid slot also
                // prevents old bytes beyond the real count from resurrecting ghost members.
                break;
            }

            members.Add(pokemon);
        }

        return members;
    }

    private bool TryDecode(int slot, ReadOnlySpan<byte> bytes, out OpponentPokemon pokemon)
    {
        pokemon = null!;
        PK4 pk;
        try
        {
            pk = new PK4(bytes.ToArray());
        }
        catch (Exception)
        {
            return false;
        }

        if (pk.Species is 0 or > MaximumSpecies
            || !pk.ChecksumValid
            || pk.Stat_Level is < 1 or > 100)
        {
            return false;
        }

        pokemon = new OpponentPokemon(
            slot + 1,
            pk.Species,
            SpeciesName.GetSpeciesNameGeneration(pk.Species, English, Generation),
            pk.Nickname,
            pk.Stat_Level,
            pk.Stat_HPCurrent,
            pk.Stat_HPMax,
            ReadStatus((uint)pk.Status_Condition),
            Lookup(_strings.Natures, (int)pk.Nature, "?"),
            Lookup(_strings.Ability, pk.Ability, "?"),
            pk.HeldItem == 0 ? "None" : Lookup(_strings.Item, pk.HeldItem, "?"),
            pk.PID,
            ReadMoves(pk));
        return true;
    }

    private IReadOnlyList<OpponentMove> ReadMoves(PK4 pk)
    {
        var moves = new List<OpponentMove>(4);
        ReadOnlySpan<ushort> ids = [pk.Move1, pk.Move2, pk.Move3, pk.Move4];
        ReadOnlySpan<int> currentPp = [pk.Move1_PP, pk.Move2_PP, pk.Move3_PP, pk.Move4_PP];
        ReadOnlySpan<int> ppUps = [pk.Move1_PPUps, pk.Move2_PPUps, pk.Move3_PPUps, pk.Move4_PPUps];

        for (int index = 0; index < ids.Length; index++)
        {
            ushort id = ids[index];
            if (id == 0)
            {
                continue;
            }

            int basePp = MoveInfo.GetPP(EntityContext.Gen4, id);
            int maximumPp = basePp + ((basePp / 5) * ppUps[index]);
            moves.Add(new OpponentMove(
                id,
                Lookup(_strings.Move, id, "?"),
                currentPp[index],
                maximumPp));
        }

        return moves;
    }

    private static (string Title, string Code, int Revision) ReadIdentity(ReadOnlySpan<byte> header)
    {
        if (header.Length < SoulSilverMemoryMap.HeaderLength)
        {
            return (string.Empty, string.Empty, 0);
        }

        return (
            Encoding.ASCII.GetString(header[..12]).TrimEnd('\0', ' '),
            Encoding.ASCII.GetString(header.Slice(0x0C, 4)),
            header[0x1E]);
    }

    private static string ReadStatus(uint status) => status switch
    {
        0 => "Healthy",
        _ when (status & 0x7) != 0 => "Asleep",
        _ when (status & 0x80) != 0 => "Badly poisoned",
        _ when (status & 0x8) != 0 => "Poisoned",
        _ when (status & 0x10) != 0 => "Burned",
        _ when (status & 0x20) != 0 => "Frozen",
        _ when (status & 0x40) != 0 => "Paralyzed",
        _ => $"Unknown (0x{status:X8})",
    };

    private static string Lookup(IReadOnlyList<string> table, int index, string fallback) =>
        index >= 0 && index < table.Count ? table[index] : fallback;

    private static string Describe(IReadOnlyList<OpponentPokemon> party) =>
        party.Count == 0 ? "no checksum-valid PK4 records" : $"{party.Count} valid record(s)";

    private static string DisplayCode(string code) =>
        string.IsNullOrWhiteSpace(code) ? "an empty game code" : $"{code}";

    private static OpponentScan Empty(ScanState state, IReadOnlyList<string> diagnostics) =>
        new(state, string.Empty, string.Empty, 0, 0, [], diagnostics);
}
