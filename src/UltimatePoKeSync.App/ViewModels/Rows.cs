using Avalonia.Media;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.App.ViewModels;

/// <summary>One stat, with everything that produced its current value.</summary>
public sealed record StatRow(string Name, int Base, int Iv, int Ev, int Current);

/// <summary>One move slot as shown in the detail panel.</summary>
public sealed record MoveRow(string Name, string Type, string Pp, IBrush Brush);

/// <summary>A type shown as a coloured chip, with the reason it is listed.</summary>
public sealed record TypeChip(string Type, string Detail, IBrush Brush)
{
    public static TypeChip For(PokemonType type, string detail) =>
        new(type.ToString(), detail, TypePalette.Brush(type));
}

/// <summary>One move of a recommended build: what it is for, and why it was picked.</summary>
public sealed record BuildMoveRow(string Name, string Type, string Role, string Reason, IBrush Brush);

/// <summary>One contribution to the team strength score.</summary>
public sealed record StrengthRow(string Name, int Points, int MaximumPoints, string Explanation)
{
    public string Value => $"{Points}/{MaximumPoints}";

    public double Fraction => MaximumPoints == 0 ? 0 : (double)Points / MaximumPoints;

    public bool IsPerfect => Points >= MaximumPoints;
}
