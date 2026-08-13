using PKHeX.Core;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Parsing;

/// <summary>
/// Interprets raw Gen 5 (Nintendo DS) bytes using PKHeX. See D-040.
/// </summary>
/// <remarks>
/// <para>
/// The shape follows <see cref="Gen3PartyParser"/>, because the job is the same: decide
/// which slots are real, translate into the domain model, and never throw on inconsistent
/// data. <see cref="PK5"/> takes the 220 bytes exactly as they sit in RAM and handles the
/// block shuffling and decryption itself.
/// </para>
/// <para>
/// Three things genuinely differ from Gen 3, and each one is a place where copying the
/// older parser would have been wrong. Nature is stored rather than derived from the
/// personality value. The physical/special split is per move rather than per type, from
/// Gen 4 onwards, which this parser does not decide, but the rules above it do. And there
/// is no <c>FlagHasSpecies</c> to lean on: a Gen 5 slot is empty when its species is zero,
/// and nothing else says so.
/// </para>
/// </remarks>
public sealed class Gen5PartyParser : IPartyParser
{
    /// <summary>PKHeX language code. 2 = English.</summary>
    private const int Language = 2;

    private const int Generation = 5;

    /// <summary>Last Gen 5 species (Genesect). Beyond that, the data is garbage.</summary>
    private const ushort MaxGen5Species = 649;

    private readonly GameStrings _strings = GameInfo.GetStrings("en");

    public bool CanParse(GameIdentity game) =>
        game.Generation == PokemonGeneration.Gen5 && ResolvePersonalTable(game.GameCode) is not null;

    public PartySnapshot Parse(RawPartySnapshot raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        IPersonalTable? personal = ResolvePersonalTable(raw.Game.GameCode);
        if (personal is null)
        {
            return new PartySnapshot(
                raw.Game, [], raw.CapturedAt, raw.Sequence,
                [new RejectedSlot(-1, $"game not supported by the Gen 5 parser: {raw.Game.GameCode}")]);
        }

        var members = new List<PokemonSnapshot>(raw.SlotCapacity);
        var rejected = new List<RejectedSlot>();

        // Slots past the declared count are not examined, for the reason of D-019: the game
        // does not wipe a slot when a Pokemon leaves, so the leftovers there still decode
        // into a complete, checksum-valid Pokemon that is no longer on the team.
        int usableSlots = Math.Min(raw.PartyCount, raw.SlotCapacity);

        for (int slot = 0; slot < usableSlots; slot++)
        {
            PK5 pk;
            try
            {
                pk = new PK5(raw.GetSlot(slot).ToArray());
            }
            catch (Exception)
            {
                rejected.Add(new RejectedSlot(slot, "bytes could not be read as a Gen 5 Pokemon"));
                continue;
            }

            if (pk.Species == 0)
            {
                rejected.Add(new RejectedSlot(slot, "empty slot but counted in the party"));
                continue;
            }

            if (!pk.ChecksumValid)
            {
                // Usually a read caught mid-write. If it becomes frequent, the address is
                // wrong and this is what says so instead of inventing a Pokemon. See D-008.
                rejected.Add(new RejectedSlot(slot, "invalid checksum"));
                continue;
            }

            if (pk.Species > MaxGen5Species)
            {
                rejected.Add(new RejectedSlot(slot, $"species out of range for Gen 5: {pk.Species}"));
                continue;
            }

            members.Add(ToSnapshot(slot, pk, personal));
        }

        return new PartySnapshot(raw.Game, members, raw.CapturedAt, raw.Sequence, rejected);
    }

    private PokemonSnapshot ToSnapshot(int slot, PK5 pk, IPersonalTable personal)
    {
        // Black/White and Black 2/White 2 have different tables of the same shape, so the
        // interface is what is held rather than either concrete one.
        PersonalInfo info = personal.GetFormEntry(pk.Species, 0);

        var primary = (PokemonType)info.Type1;
        var secondary = info.Type2 == info.Type1 ? PokemonType.None : (PokemonType)info.Type2;

        return new PokemonSnapshot
        {
            SlotIndex = slot,
            SpeciesId = pk.Species,
            SpeciesName = SpeciesName.GetSpeciesNameGeneration(pk.Species, Language, Generation),
            Nickname = pk.Nickname,
            Level = pk.Stat_Level,
            PrimaryType = primary,
            SecondaryType = secondary,
            BaseStats = new StatBlock(info.HP, info.ATK, info.DEF, info.SPA, info.SPD, info.SPE),
            IndividualValues = new StatBlock(
                pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE),
            EffortValues = new StatBlock(
                pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPA, pk.EV_SPD, pk.EV_SPE),
            CurrentStats = new StatBlock(
                pk.Stat_HPMax, pk.Stat_ATK, pk.Stat_DEF, pk.Stat_SPA, pk.Stat_SPD, pk.Stat_SPE),

            // Unlike Gen 3, nature is a field of its own here rather than the personality
            // value modulo 25: the two can disagree, and the stored one is the true one.
            NatureId = (int)pk.Nature,
            NatureName = Lookup(_strings.Natures, (int)pk.Nature, "?"),

            AbilityId = pk.Ability,
            AbilityName = Lookup(_strings.Ability, pk.Ability, "?"),
            HeldItemId = pk.HeldItem,
            HeldItemName = pk.HeldItem == 0 ? "-" : Lookup(_strings.Item, pk.HeldItem, "?"),
            Moves = ReadMoves(pk),
            IsEgg = pk.IsEgg,
            IsShiny = pk.IsShiny,
            PersonalityValue = pk.PID,
            Gender = (PokemonGender)pk.Gender,
            CurrentHp = pk.Stat_HPCurrent,
            Status = StatusByte.Read(pk.Status_Condition),
            Friendship = pk.CurrentFriendship,
            Experience = pk.EXP,
        };
    }

    private IReadOnlyList<MoveSlot> ReadMoves(PK5 pk)
    {
        var moves = new List<MoveSlot>(4);
        ReadOnlySpan<ushort> ids = [pk.Move1, pk.Move2, pk.Move3, pk.Move4];
        ReadOnlySpan<int> currentPp = [pk.Move1_PP, pk.Move2_PP, pk.Move3_PP, pk.Move4_PP];
        ReadOnlySpan<int> ppUps = [pk.Move1_PPUps, pk.Move2_PPUps, pk.Move3_PPUps, pk.Move4_PPUps];

        for (int i = 0; i < 4; i++)
        {
            ushort id = ids[i];
            if (id == 0)
            {
                moves.Add(MoveSlot.Empty);
                continue;
            }

            int basePp = MoveInfo.GetPP(EntityContext.Gen5, id);

            // Each PP Up adds 20% of the base PP, truncated.
            int maxPp = basePp + (basePp / 5 * ppUps[i]);

            moves.Add(new MoveSlot(
                id,
                Lookup(_strings.Move, id, "?"),
                (PokemonType)MoveInfo.GetType(id, EntityContext.Gen5),
                currentPp[i],
                maxPp));
        }

        return moves;
    }

    private static string Lookup(IReadOnlyList<string> table, int index, string fallback) =>
        index >= 0 && index < table.Count ? table[index] : fallback;

    /// <summary>
    /// Black and White share a table; Black 2 and White 2 have their own. The fourth letter
    /// of the code is the region, and it does not change the stats, but every code is
    /// listed rather than matched by prefix, so an unknown one is refused instead of being
    /// guessed at. Only IRBI has been run against a real cartridge. See D-040.
    /// </summary>
    private static IPersonalTable? ResolvePersonalTable(string gameCode) => gameCode switch
    {
        // Black, then White. Verified live: IRBI.
        "IRBO" or "IRBE" or "IRBJ" or "IRBI" or "IRBS" or "IRBF" or "IRBD" or "IRBK" =>
            PersonalTable.BW,
        "IRAO" or "IRAE" or "IRAJ" or "IRAI" or "IRAS" or "IRAF" or "IRAD" or "IRAK" =>
            PersonalTable.BW,

        // Black 2, then White 2.
        "IREO" or "IREE" or "IREJ" or "IREI" or "IRES" or "IREF" or "IRED" or "IREK" =>
            PersonalTable.B2W2,
        "IRDO" or "IRDE" or "IRDJ" or "IRDI" or "IRDS" or "IRDF" or "IRDD" or "IRDK" =>
            PersonalTable.B2W2,

        _ => null,
    };
}
