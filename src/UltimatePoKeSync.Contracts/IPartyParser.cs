namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Turns raw bytes into Pokémon. One implementation per generation.
/// </summary>
/// <remarks>
/// It lives in Contracts rather than Parsing so the UI and the analysis can depend on the
/// abstraction without dragging in PKHeX and its licence. See D-007.
/// </remarks>
public interface IPartyParser
{
    /// <summary>Whether this implementation can interpret the given game.</summary>
    bool CanParse(GameIdentity game);

    /// <summary>
    /// Decodes the valid slots. Never throws for corrupt slots: it reports them in
    /// <see cref="PartySnapshot.RejectedSlots"/>, because with reads at 15 Hz an
    /// inconsistent slot is routine rather than exceptional. See D-008.
    /// </summary>
    PartySnapshot Parse(RawPartySnapshot raw);
}

/// <summary>
/// Picks the right parser for the current ROM.
/// </summary>
public interface IPartyParserResolver
{
    /// <summary>Returns <c>null</c> when no registered parser covers that game.</summary>
    IPartyParser? Resolve(GameIdentity game);
}
