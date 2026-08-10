using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Parsing;

/// <summary>
/// Sceglie il parser adatto alla ROM corrente.
/// </summary>
/// <remarks>
/// Il punto in cui si aggancia una nuova generazione: si registra un parser in piu' e
/// nient'altro cambia, ne' nel provider ne' nella UI.
/// </remarks>
public sealed class PartyParserResolver : IPartyParserResolver
{
    private readonly IReadOnlyList<IPartyParser> _parsers;

    public PartyParserResolver(IEnumerable<IPartyParser> parsers)
        => _parsers = parsers.ToArray();

    /// <summary>Insieme dei parser attualmente implementati.</summary>
    public static PartyParserResolver CreateDefault() => new([new Gen3PartyParser()]);

    public IPartyParser? Resolve(GameIdentity game)
    {
        foreach (IPartyParser parser in _parsers)
        {
            if (parser.CanParse(game))
            {
                return parser;
            }
        }

        return null;
    }
}
