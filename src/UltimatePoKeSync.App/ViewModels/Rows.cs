using Avalonia.Controls;
using Avalonia.Media;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.App.ViewModels;

/// <summary>One stat, with everything that produced its current value.</summary>
public sealed record StatRow(string Name, int Base, int Iv, int Ev, int Current)
{
    /// <summary>
    /// Where the base stat sits on the scale the games themselves use. Capped at 180
    /// rather than the theoretical 255: almost nothing reaches the top, so scaling to it
    /// would leave every bar looking short and equally short.
    /// </summary>
    public double Fraction => Math.Clamp(Base / 180.0, 0, 1);

    public GridLength Filled => new(Fraction, GridUnitType.Star);

    public GridLength Empty => new(1 - Fraction, GridUnitType.Star);

    /// <summary>Red through blue, the way a Pokédex has always shaded a stat.</summary>
    public IBrush Brush => Base switch
    {
        < 60 => new SolidColorBrush(Color.FromRgb(0xC0, 0x3D, 0x2E)),
        < 90 => new SolidColorBrush(Color.FromRgb(0xD9, 0x60, 0x2E)),
        < 120 => new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27)),
        < 150 => new SolidColorBrush(Color.FromRgb(0x4E, 0x9A, 0x3F)),
        _ => new SolidColorBrush(Color.FromRgb(0x3E, 0x76, 0xC4)),
    };
}

/// <summary>
/// The teaching bar for a stat. Segment lengths are point contributions at the current
/// level, all measured against the largest final stat on the screen.
/// </summary>
public sealed record StatSourceRow(
    string Name,
    int Current,
    int BaseContribution,
    int IvContribution,
    int EvContribution,
    int NatureContribution,
    int ScaleMaximum,
    string Breakdown,
    bool NatureIsNegative)
{
    private int TotalWidth => BaseContribution + IvContribution + EvContribution
        + Math.Abs(NatureContribution);

    public GridLength BaseWidth => new(BaseContribution, GridUnitType.Star);
    public GridLength IvWidth => new(IvContribution, GridUnitType.Star);
    public GridLength EvWidth => new(EvContribution, GridUnitType.Star);
    public GridLength NatureWidth => new(Math.Abs(NatureContribution), GridUnitType.Star);
    public GridLength RemainingWidth =>
        new(Math.Max(0, ScaleMaximum - TotalWidth), GridUnitType.Star);
    public bool HasNatureContribution => NatureContribution != 0;
    public double NatureOpacity => NatureIsNegative ? 0.55 : 1;
}

/// <summary>One immutable 0–31 IV, drawn as a well filled from the bottom.</summary>
public sealed record IvRow(string Name, int Value)
{
    public GridLength Empty => new(31 - Value, GridUnitType.Star);
    public GridLength Filled => new(Value, GridUnitType.Star);
    public bool IsPerfect => Value == 31;
    public string Label => $"{Name} {Value}";
    public IBrush FillBrush => Value switch
    {
        31 => new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F)),
        >= 24 => new SolidColorBrush(Color.FromRgb(0x45, 0xD0, 0xE0)),
        _ => new SolidColorBrush(Color.FromRgb(0x3A, 0x7F, 0x8C)),
    };
}

/// <summary>One controllable 0–252 EV allocation.</summary>
public sealed record EvRow(string Name, int Value, bool IsRecommended)
{
    public GridLength Filled => new(Value, GridUnitType.Star);
    public GridLength Remaining => new(252 - Value, GridUnitType.Star);
    public string ValueText => $"{Value} / 252";
    public bool IsEmpty => Value == 0;
}

/// <summary>
/// One move a Pokémon knows now. <c>Detail</c> is the line under the name — type, category
/// and power — which is what says whether a move runs off Attack or Special Attack. From
/// Gen 4 that is a property of the move rather than of its type (D-041), so it is read from
/// the generation's rules rather than guessed from the colour.
/// </summary>
public sealed record MoveRow(
    string Name,
    string Type,
    string Pp,
    string Detail,
    IBrush Brush,
    bool IsEmpty = false)
{
    public double Fade => IsEmpty ? 0.58 : 1;
}

/// <summary>A type shown as a coloured chip, with the reason it is listed.</summary>
public sealed record TypeChip(string Type, string Detail, IBrush Brush)
{
    /// <summary>
    /// The name in capitals, as the chips are set. Every chip prints its type: the colour
    /// is never the only signal, which is the rule TypePalette exists to keep.
    /// </summary>
    public string UpperType => Type.ToUpperInvariant();

    public static TypeChip For(PokemonType type, string detail) =>
        new(type.ToString(), detail, TypePalette.Brush(type));
}

/// <summary>
/// One move of a recommended build: what it is for, how it is obtained, and why it was
/// picked. Without <c>Source</c> a player is told to run Dig with no hint that it means
/// finding TM28.
/// </summary>
public sealed record BuildMoveRow(
    string Name,
    string Type,
    string Role,
    string Source,
    string Reason,
    IBrush Brush);

/// <summary>
/// A move the next few levels bring. <c>Distance</c> is the half that gets acted on: a level
/// number means nothing to someone who does not remember what level their Treecko is.
/// </summary>
public sealed record UpcomingMoveRow(
    string Name,
    string Type,
    string Level,
    string Distance,
    IBrush Brush,
    bool IsAfterEvolution = false)
{
    public double Fade => IsAfterEvolution ? 0.55 : 1;
}

/// <summary>
/// One move from the candidate pool. <c>Chosen</c> marks the four that made the build, so
/// the pool reads as a decision with its reasons rather than a list of names.
/// </summary>
public sealed record CandidateMoveRow(
    string Name,
    string Type,
    string Source,
    string Availability,
    bool Chosen,
    IBrush Brush)
{
    public double Fade => Chosen ? 1 : 0.62;

    public string Marker => Chosen ? "✓" : string.Empty;
}

/// <summary>
/// One of the four facts on the hero: a label and a value. A collection rather than four
/// copies of the same markup, so adding a fifth is a line here and nothing in the screen.
/// </summary>
public sealed record FactChip(string Label, string Value);

/// <summary>One contribution to the team strength score.</summary>
public sealed record StrengthRow(string Name, int Points, int MaximumPoints, string Explanation)
{
    public string Value => $"{Points}/{MaximumPoints}";

    public double Fraction => MaximumPoints == 0 ? 0 : (double)Points / MaximumPoints;

    public bool IsPerfect => Points >= MaximumPoints;

    /// <summary>How much this factor cost, as the rail prints it: −4.</summary>
    public string Lost => $"−{MaximumPoints - Points}";

    /// <summary>Red for a factor that scored nothing, yellow for one that scored badly.</summary>
    public IBrush LostBrush => Points == 0
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x5B, 0x4A))
        : new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F));
}
