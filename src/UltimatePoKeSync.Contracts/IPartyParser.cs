namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Traduce byte grezzi in Pokemon. Un'implementazione per generazione.
/// </summary>
/// <remarks>
/// Vive in Contracts, non in Parsing, cosi' che UI e analisi possano dipendere
/// dall'astrazione senza trascinarsi dietro PKHeX e la sua licenza GPL. Vedi D-007.
/// </remarks>
public interface IPartyParser
{
    /// <summary>Generazioni che questa implementazione sa interpretare.</summary>
    bool CanParse(GameIdentity game);

    /// <summary>
    /// Decodifica gli slot validi. Non lancia eccezioni per slot corrotti: li riporta in
    /// <see cref="PartySnapshot.RejectedSlots"/>, perche' con letture a 60 Hz uno slot
    /// incoerente e' la normalita', non un caso eccezionale. Vedi D-008.
    /// </summary>
    PartySnapshot Parse(RawPartySnapshot raw);
}

/// <summary>
/// Sceglie il parser giusto per la ROM corrente.
/// </summary>
public interface IPartyParserResolver
{
    /// <summary>Restituisce <c>null</c> se nessun parser registrato copre quel gioco.</summary>
    IPartyParser? Resolve(GameIdentity game);
}
