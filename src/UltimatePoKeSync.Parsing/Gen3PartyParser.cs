using PKHeX.Core;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Parsing;

/// <summary>
/// Interprets raw Gen 3 (GBA) bytes using PKHeX.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PK3"/> does the hard part: its constructor accepts the 100 bytes exactly as
/// they sit in RAM and handles decryption (XOR with <c>PID xor OTID</c>) and reordering of
/// the four PID-permuted substructures on its own. Verified empirically, not just from the
/// documentation. See D-007.
/// </para>
/// <para>
/// Three responsibilities are left here: deciding which slots are real, translating into
/// the domain model, and never throwing on inconsistent data.
/// </para>
/// </remarks>
public sealed class Gen3PartyParser : IPartyParser
{
    /// <summary>PKHeX language code. 2 = English.</summary>
    private const int Language = 2;

    private const int Generation = 3;

    /// <summary>Last Gen 3 species (Deoxys). Beyond that, the data is garbage.</summary>
    private const ushort MaxGen3Species = 411;

    private readonly GameStrings _strings = GameInfo.GetStrings("en");

    public bool CanParse(GameIdentity game) =>
        game.Generation == PokemonGeneration.Gen3 && ResolvePersonalTable(game.GameCode) is not null;

    public PartySnapshot Parse(RawPartySnapshot raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        PersonalTable3? personal = ResolvePersonalTable(raw.Game.GameCode);
        if (personal is null)
        {
            return new PartySnapshot(
                raw.Game, [], raw.CapturedAt, raw.Sequence,
                [new RejectedSlot(-1, $"game not supported by the Gen 3 parser: {raw.Game.GameCode}")]);
        }

        var members = new List<PokemonSnapshot>(raw.SlotCapacity);
        var rejected = new List<RejectedSlot>();

        // Only slots below the declared count can be party members. Slots past it are not
        // examined at all, and deliberately so: the game does not reliably wipe a slot
        // when a Pokémon leaves the party, so leftover bytes there can still decode into a
        // complete, checksum-valid Pokémon that is no longer on the team. Reading them
        // would resurrect ghosts. See D-019.
        int usableSlots = Math.Min(raw.PartyCount, raw.SlotCapacity);

        for (int slot = 0; slot < usableSlots; slot++)
        {
            var pk = new PK3(raw.GetSlot(slot).ToArray());

            if (pk.Species == 0 || !pk.FlagHasSpecies)
            {
                rejected.Add(new RejectedSlot(slot, "empty slot but counted in the party"));
                continue;
            }

            if (!pk.ChecksumValid)
            {
                // The typical cause is a read captured mid-write. With the two-read
                // confirmation in the script this should be rare; if it becomes frequent,
                // the memory map is wrong. See D-008.
                rejected.Add(new RejectedSlot(slot, "invalid checksum"));
                continue;
            }

            if (pk.FlagIsBadEgg)
            {
                rejected.Add(new RejectedSlot(slot, "bad egg"));
                continue;
            }

            if (pk.Species > MaxGen3Species)
            {
                rejected.Add(new RejectedSlot(slot, $"species out of range for Gen 3: {pk.Species}"));
                continue;
            }

            members.Add(ToSnapshot(slot, pk, personal));
        }

        return new PartySnapshot(raw.Game, members, raw.CapturedAt, raw.Sequence, rejected);
    }

    private PokemonSnapshot ToSnapshot(int slot, PK3 pk, PersonalTable3 personal)
    {
        PersonalInfo3 info = personal[pk.Species];

        // PKHeX exposes types already normalised to modern indices (Fire = 9), not to the
        // Gen 3 internal ones where index 9 is the "???" type. Verified. See D-014.
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

            // In Gen 3 nature is not stored: it is derived from the PID (PID % 25).
            // PKHeX computes it for us.
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
            CurrentHp = pk.Stat_HPCurrent,
            Status = ReadStatus(pk.Status_Condition),
            Friendship = pk.CurrentFriendship,
            Experience = pk.EXP,
        };
    }

    /// <summary>
    /// Decodes the Gen 3 status byte. Sleep occupies the low three bits as a turn counter,
    /// so it is present whenever any of them is set rather than at a value of its own; the
    /// remaining conditions are one flag each. Badly poisoned is checked before poisoned,
    /// because the game sets both.
    /// </summary>
    /// <remarks>
    /// Cross-checked against PKHeX's own <c>StatusCondition</c>, which lays the byte out
    /// identically — Sleep1 to Sleep7, Poison 8, Burn 16, Freeze 32, Paralysis 64,
    /// PoisonBad 128. Ours is separate because Contracts must not depend on PKHeX (D-007),
    /// and pinned by a test for the same reason as D-014.
    /// </remarks>
    private static Contracts.StatusCondition ReadStatus(int condition) => condition switch
    {
        _ when (condition & 0b0000_0111) != 0 => Contracts.StatusCondition.Sleep,
        _ when (condition & 0b1000_0000) != 0 => Contracts.StatusCondition.BadPoison,
        _ when (condition & 0b0000_1000) != 0 => Contracts.StatusCondition.Poison,
        _ when (condition & 0b0001_0000) != 0 => Contracts.StatusCondition.Burn,
        _ when (condition & 0b0010_0000) != 0 => Contracts.StatusCondition.Freeze,
        _ when (condition & 0b0100_0000) != 0 => Contracts.StatusCondition.Paralysis,
        _ => Contracts.StatusCondition.None,
    };

    private IReadOnlyList<MoveSlot> ReadMoves(PK3 pk)
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

            int basePp = MoveInfo.GetPP(EntityContext.Gen3, id);

            // Each PP Up adds 20% of the base PP, truncated.
            int maxPp = basePp + (basePp / 5 * ppUps[i]);

            moves.Add(new MoveSlot(
                id,
                Lookup(_strings.Move, id, "?"),
                (PokemonType)MoveInfo.GetType(id, EntityContext.Gen3),
                currentPp[i],
                maxPp));
        }

        return moves;
    }

    /// <summary>
    /// Defensive access to the string tables: one corrupt ID must not blow up parsing of
    /// the whole party.
    /// </summary>
    private static string Lookup(IReadOnlyList<string> table, int index, string fallback) =>
        index >= 0 && index < table.Count ? table[index] : fallback;

    /// <summary>
    /// Each Gen 3 game has its own base-stat table: Emerald and FireRed/LeafGreen differ
    /// in a few entries, mostly wild held items.
    /// </summary>
    private static PersonalTable3? ResolvePersonalTable(string gameCode) => gameCode switch
    {
        // All Western Emerald localisations share one table; only the text differs.
        "BPEE" or "BPEF" or "BPED" or "BPES" or "BPEI" => PersonalTable.E,
        "BPRE" => PersonalTable.FR,
        "BPGE" => PersonalTable.LG,
        "AXVE" or "AXPE" => PersonalTable.RS,
        _ => null,
    };
}
