using PKHeX.Core;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Parsing;

/// <summary>
/// Interpreta i byte grezzi della Gen 3 (GBA) usando PKHeX.
/// </summary>
/// <remarks>
/// <para>
/// Il lavoro difficile lo fa <see cref="PK3"/>: il suo costruttore accetta i 100 byte
/// cosi' come stanno in RAM e si occupa da solo di decifrarli (XOR con
/// <c>PID xor OTID</c>) e di rimettere in ordine le quattro sottostrutture permutate in
/// base al PID. Verificato empiricamente, non solo sulla documentazione. Vedi D-007.
/// </para>
/// <para>
/// Qui restano tre responsabilita': decidere quali slot sono veri, tradurre nel modello
/// di dominio e non lanciare mai eccezioni per dati incoerenti.
/// </para>
/// </remarks>
public sealed class Gen3PartyParser : IPartyParser
{
    /// <summary>Codice lingua PKHeX. 2 = inglese.</summary>
    private const int Language = 2;

    private const int Generation = 3;

    /// <summary>Ultima specie della Gen 3 (Deoxys). Oltre, i dati sono spazzatura.</summary>
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
                [new RejectedSlot(-1, $"gioco non supportato dal parser Gen 3: {raw.Game.GameCode}")]);
        }

        var members = new List<PokemonSnapshot>(raw.SlotCapacity);
        var rejected = new List<RejectedSlot>();

        for (int slot = 0; slot < raw.SlotCapacity; slot++)
        {
            var pk = new PK3(raw.GetSlot(slot).ToArray());

            // Slot oltre il conteggio dichiarato e vuoto: e' semplicemente una casella
            // libera, non c'e' niente da segnalare.
            bool beyondDeclaredCount = slot >= raw.PartyCount;
            if (pk.Species == 0 || !pk.FlagHasSpecies)
            {
                if (!beyondDeclaredCount)
                {
                    rejected.Add(new RejectedSlot(slot, "slot vuoto ma conteggiato in squadra"));
                }

                continue;
            }

            if (!pk.ChecksumValid)
            {
                // Il caso tipico e' una lettura catturata a meta' scrittura. Con la
                // conferma su due letture lato script dovrebbe essere raro; se diventa
                // frequente, la mappa di memoria e' sbagliata. Vedi D-008.
                rejected.Add(new RejectedSlot(slot, "checksum non valido"));
                continue;
            }

            if (pk.FlagIsBadEgg)
            {
                rejected.Add(new RejectedSlot(slot, "bad egg"));
                continue;
            }

            if (pk.Species > MaxGen3Species)
            {
                rejected.Add(new RejectedSlot(slot, $"specie fuori range per la Gen 3: {pk.Species}"));
                continue;
            }

            members.Add(ToSnapshot(slot, pk, personal));
        }

        return new PartySnapshot(raw.Game, members, raw.CapturedAt, raw.Sequence, rejected);
    }

    private PokemonSnapshot ToSnapshot(int slot, PK3 pk, PersonalTable3 personal)
    {
        PersonalInfo3 info = personal[pk.Species];

        // PKHeX espone i tipi gia' normalizzati agli indici moderni (Fire = 9), non a
        // quelli interni della Gen 3 dove l'indice 9 e' il tipo "???". Verificato.
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

            // In Gen 3 la natura non e' memorizzata: e' derivata dal PID (PID % 25).
            // PKHeX la calcola per noi.
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
        };
    }

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

            // Ogni PP-Up aggiunge il 20% dei PP base, troncato.
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
    /// Accesso difensivo alle tabelle di stringhe: un ID corrotto non deve far esplodere
    /// il parsing dell'intera squadra.
    /// </summary>
    private static string Lookup(IReadOnlyList<string> table, int index, string fallback) =>
        index >= 0 && index < table.Count ? table[index] : fallback;

    /// <summary>
    /// Ogni gioco Gen 3 ha la propria tabella di stat base: Emerald e FireRed/LeafGreen
    /// differiscono in alcune voci (soprattutto negli oggetti tenuti in natura).
    /// </summary>
    private static PersonalTable3? ResolvePersonalTable(string gameCode) => gameCode switch
    {
        "BPEE" => PersonalTable.E,
        "BPRE" => PersonalTable.FR,
        "BPGE" => PersonalTable.LG,
        "AXVE" or "AXPE" => PersonalTable.RS,
        _ => null,
    };
}
