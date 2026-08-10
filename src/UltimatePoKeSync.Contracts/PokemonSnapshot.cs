namespace UltimatePoKeSync.Contracts;

/// <summary>
/// A decoded party member. This is the domain model the UI and the analysis consume;
/// past this point nobody touches raw bytes or PKHeX again.
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
    /// Second type, or <see cref="PokemonType.None"/> for mono-type Pokémon. In the game
    /// data mono-types repeat the same type twice; this is already normalised here.
    /// </summary>
    public required PokemonType SecondaryType { get; init; }

    public required StatBlock BaseStats { get; init; }

    public required StatBlock IndividualValues { get; init; }

    public required StatBlock EffortValues { get; init; }

    /// <summary>Effective current stats, exactly as the game computed them.</summary>
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
    /// Sum of the EVs. In Gen 3 the limit is 510 total and 255 per stat; 252 is merely
    /// the point beyond which EVs stop yielding stat points. See D-009.
    /// </summary>
    public int TotalEffortValues => EffortValues.Total;

    public bool IsDualType => SecondaryType != PokemonType.None;

    public override string ToString() =>
        $"#{SlotIndex} {SpeciesName} Lv.{Level} ({PrimaryType}{(IsDualType ? "/" + SecondaryType : "")})";
}

/// <param name="MoveId">Move ID, 0 when the slot is empty.</param>
/// <param name="Name">Human-readable name.</param>
/// <param name="Type">Move type.</param>
/// <param name="CurrentPp">Remaining PP.</param>
/// <param name="MaxPp">Maximum PP, with PP Ups already applied.</param>
public sealed record MoveSlot(int MoveId, string Name, PokemonType Type, int CurrentPp, int MaxPp)
{
    public static MoveSlot Empty { get; } = new(0, "-", PokemonType.None, 0, 0);

    public bool IsEmpty => MoveId == 0;
}
