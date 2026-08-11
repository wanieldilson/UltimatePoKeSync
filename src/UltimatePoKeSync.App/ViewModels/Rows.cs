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

/// <summary>One move slot as shown in the detail panel.</summary>
public sealed record MoveRow(string Name, string Type, string Pp, IBrush Brush);

/// <summary>A type shown as a coloured chip, with the reason it is listed.</summary>
public sealed record TypeChip(string Type, string Detail, IBrush Brush)
{
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

/// <summary>One contribution to the team strength score.</summary>
public sealed record StrengthRow(string Name, int Points, int MaximumPoints, string Explanation)
{
    public string Value => $"{Points}/{MaximumPoints}";

    public double Fraction => MaximumPoints == 0 ? 0 : (double)Points / MaximumPoints;

    public bool IsPerfect => Points >= MaximumPoints;
}
