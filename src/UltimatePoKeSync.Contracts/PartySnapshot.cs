namespace UltimatePoKeSync.Contracts;

/// <summary>
/// La squadra decodificata in un dato istante.
/// </summary>
/// <param name="Game">ROM di provenienza.</param>
/// <param name="Members">Solo gli slot validi, in ordine. Puo' essere vuota.</param>
/// <param name="CapturedAt">Momento della cattura dei byte originali.</param>
/// <param name="Sequence">Contatore dello snapshot grezzo da cui deriva.</param>
/// <param name="RejectedSlots">
/// Slot scartati e perche'. Non e' un errore: durante una lettura a meta' scrittura, o
/// con un uovo in squadra, e' normale che qualche slot non passi la validazione. Tenerne
/// traccia serve a distinguere "il gioco sta scrivendo" da "la mappa di memoria e' sbagliata".
/// </param>
public sealed record PartySnapshot(
    GameIdentity Game,
    IReadOnlyList<PokemonSnapshot> Members,
    DateTimeOffset CapturedAt,
    ulong Sequence,
    IReadOnlyList<RejectedSlot> RejectedSlots)
{
    public static PartySnapshot Empty { get; } = new(
        GameIdentity.Unknown, [], DateTimeOffset.MinValue, 0, []);

    public int Count => Members.Count;

    public bool IsEmpty => Members.Count == 0;
}

/// <param name="SlotIndex">Indice dello slot scartato.</param>
/// <param name="Reason">Motivo leggibile, per la diagnostica.</param>
public sealed record RejectedSlot(int SlotIndex, string Reason);
