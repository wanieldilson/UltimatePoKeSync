namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Quello che un emulatore consegna: byte grezzi, non Pokemon.
/// </summary>
/// <remarks>
/// <para>
/// E' la scelta portante dell'architettura (D-006). Lo script dell'emulatore non
/// interpreta nulla: non decifra, non applica checksum, non mappa ID di specie. Si
/// limita a copiare una regione di memoria e a dire di che gioco si tratta.
/// </para>
/// <para>
/// Il motivo e' che la decodifica e' la parte difficile e va scritta una volta sola. Se
/// ogni script parsasse i propri Pokemon, aggiungere un emulatore significherebbe
/// duplicare quella logica in Lua, in un linguaggio senza test, e vederla divergere.
/// Cosi invece un nuovo emulatore costa un nuovo script e zero righe di dominio.
/// </para>
/// </remarks>
/// <param name="Game">La ROM da cui provengono i byte.</param>
/// <param name="PartyCount">
/// Numero di Pokemon in squadra secondo l'emulatore, 0-6. Non fidarsi ciecamente: e'
/// letto da un byte che puo' essere catturato a meta' di una scrittura. Il parser lo
/// tratta come limite superiore e valida comunque ogni slot.
/// </param>
/// <param name="PartyData">
/// Byte contigui di tutti gli slot: <c>SlotSize * 6</c>. Sono i byte cosi' come stanno
/// in RAM, quindi per la Gen 3 sono cifrati e con le sottostrutture permutate.
/// </param>
/// <param name="SlotSize">Dimensione di uno slot in byte. Gen 3: 100.</param>
/// <param name="CapturedAt">Momento della cattura, per la diagnostica di latenza.</param>
/// <param name="Sequence">
/// Contatore monotono assegnato dallo script. Permette di accorgersi di snapshot persi
/// e di scartare quelli arrivati fuori ordine.
/// </param>
public sealed record RawPartySnapshot(
    GameIdentity Game,
    int PartyCount,
    ReadOnlyMemory<byte> PartyData,
    int SlotSize,
    DateTimeOffset CapturedAt,
    ulong Sequence)
{
    /// <summary>Numero massimo di slot presenti nel blob, indipendentemente da PartyCount.</summary>
    public int SlotCapacity => SlotSize > 0 ? PartyData.Length / SlotSize : 0;

    /// <summary>
    /// Byte del singolo slot. Non fa alcuna validazione: interpretarli e' compito del parser.
    /// </summary>
    public ReadOnlyMemory<byte> GetSlot(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, SlotCapacity);
        return PartyData.Slice(index * SlotSize, SlotSize);
    }
}
