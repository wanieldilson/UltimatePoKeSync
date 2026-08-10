namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Identifica la ROM caricata nell'emulatore.
/// </summary>
/// <remarks>
/// Viaggia in ogni snapshot perche' gli indirizzi RAM cambiano per gioco E per regione:
/// interpretare byte di FireRed con la mappa di Emerald produce spazzatura plausibile,
/// che e' il tipo di bug peggiore. Vedi D-005.
/// </remarks>
/// <param name="GameCode">
/// Codice a 4 caratteri dall'header della cartuccia GBA a 0x080000AC.
/// Esempi: <c>BPEE</c> Emerald, <c>BPRE</c> FireRed, <c>BPGE</c> LeafGreen,
/// <c>AXVE</c> Ruby, <c>AXPE</c> Sapphire. L'ultimo carattere e' la regione
/// (<c>E</c> USA, <c>J</c> Giappone, <c>P</c> Europa...).
/// </param>
/// <param name="Title">Titolo interno della ROM, per la diagnostica.</param>
/// <param name="Revision">Numero di revisione della ROM.</param>
/// <param name="Generation">Generazione dedotta dal codice.</param>
public sealed record GameIdentity(
    string GameCode,
    string Title,
    int Revision,
    PokemonGeneration Generation)
{
    /// <summary>Usata quando l'emulatore e' connesso ma non ha ancora una ROM caricata.</summary>
    public static GameIdentity Unknown { get; } =
        new("????", string.Empty, 0, PokemonGeneration.Unknown);

    /// <summary>Regione, dedotta dal quarto carattere del game code.</summary>
    public char RegionCode => GameCode.Length == 4 ? GameCode[3] : '?';

    public override string ToString() => $"{Title} [{GameCode}] rev{Revision}";
}
