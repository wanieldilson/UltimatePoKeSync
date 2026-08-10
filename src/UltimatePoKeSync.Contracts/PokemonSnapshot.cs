namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Un Pokemon del party, decodificato. E' il modello di dominio che UI e analisi
/// consumano; oltre questo punto nessuno tocca piu' byte grezzi ne PKHeX.
/// </summary>
public sealed record PokemonSnapshot
{
    public required int SlotIndex { get; init; }

    public required ushort SpeciesId { get; init; }

    public required string SpeciesName { get; init; }

    public string Nickname { get; init; } = string.Empty;

    public required int Level { get; init; }

    public required PokemonType PrimaryType { get; init; }

    /// <summary>
    /// Secondo tipo, oppure <see cref="PokemonType.None"/> per i mono-tipo. In Gen 3 i
    /// mono-tipo hanno lo stesso tipo ripetuto nei dati di gioco: qui e' gia' normalizzato.
    /// </summary>
    public required PokemonType SecondaryType { get; init; }

    public required StatBlock BaseStats { get; init; }

    public required StatBlock IndividualValues { get; init; }

    public required StatBlock EffortValues { get; init; }

    /// <summary>Statistiche effettive correnti, cosi' come il gioco le ha calcolate.</summary>
    public required StatBlock CurrentStats { get; init; }

    public required int NatureId { get; init; }

    public required string NatureName { get; init; }

    public required int AbilityId { get; init; }

    public required string AbilityName { get; init; }

    public required int HeldItemId { get; init; }

    public required string HeldItemName { get; init; }

    public required IReadOnlyList<MoveSlot> Moves { get; init; }

    public required bool IsEgg { get; init; }

    public required bool IsShiny { get; init; }

    public required uint PersonalityValue { get; init; }

    /// <summary>
    /// Somma degli EV. In Gen 3 il limite e' 510 sul totale e 255 per statistica; 252 e'
    /// solo la soglia oltre la quale gli EV non producono piu' punti. Vedi D-009.
    /// </summary>
    public int TotalEffortValues => EffortValues.Total;

    public bool IsDualType => SecondaryType != PokemonType.None;

    public override string ToString() =>
        $"#{SlotIndex} {SpeciesName} Lv.{Level} ({PrimaryType}{(IsDualType ? "/" + SecondaryType : "")})";
}

/// <param name="MoveId">ID della mossa, 0 se lo slot e' vuoto.</param>
/// <param name="Name">Nome leggibile.</param>
/// <param name="Type">Tipo della mossa.</param>
/// <param name="CurrentPp">PP residui.</param>
/// <param name="MaxPp">PP massimi, PP-Up gia' applicati.</param>
public sealed record MoveSlot(int MoveId, string Name, PokemonType Type, int CurrentPp, int MaxPp)
{
    public static MoveSlot Empty { get; } = new(0, "-", PokemonType.None, 0, 0);

    public bool IsEmpty => MoveId == 0;
}
