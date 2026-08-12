using Avalonia.Media;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.App;

/// <summary>
/// One colour per battle type, used for the slot tiles and the coverage chips.
/// </summary>
/// <remarks>
/// Colour is a shorthand, never the only signal: every chip also carries its type name,
/// so the display still reads for a colour-blind player and in a screenshot.
/// </remarks>
public static class TypePalette
{
    private static readonly IReadOnlyDictionary<PokemonType, Color> Colors = new Dictionary<PokemonType, Color>
    {
        [PokemonType.Normal] = Color.FromRgb(0xA8, 0xAA, 0xA6),
        [PokemonType.Fighting] = Color.FromRgb(0xD8, 0x50, 0x3E),
        [PokemonType.Flying] = Color.FromRgb(0x93, 0xB1, 0xEA),
        [PokemonType.Poison] = Color.FromRgb(0xA3, 0x55, 0xA8),
        [PokemonType.Ground] = Color.FromRgb(0xC1, 0x9A, 0x52),
        [PokemonType.Rock] = Color.FromRgb(0xB4, 0xA4, 0x5F),
        [PokemonType.Bug] = Color.FromRgb(0x8F, 0xA6, 0x2B),
        [PokemonType.Ghost] = Color.FromRgb(0x74, 0x68, 0xB4),
        [PokemonType.Steel] = Color.FromRgb(0x8A, 0x93, 0xA1),
        [PokemonType.Fire] = Color.FromRgb(0xE0, 0x6A, 0x2E),
        [PokemonType.Water] = Color.FromRgb(0x4A, 0x86, 0xD8),
        [PokemonType.Grass] = Color.FromRgb(0x5C, 0xBF, 0x4A),
        [PokemonType.Electric] = Color.FromRgb(0xC9, 0xA2, 0x27),
        [PokemonType.Psychic] = Color.FromRgb(0xDC, 0x5A, 0x8E),
        [PokemonType.Ice] = Color.FromRgb(0x5D, 0xB3, 0xC4),
        [PokemonType.Dragon] = Color.FromRgb(0x6A, 0x5E, 0xDC),
        [PokemonType.Dark] = Color.FromRgb(0x7A, 0x66, 0x59),
        [PokemonType.Fairy] = Color.FromRgb(0xD4, 0x86, 0xBC),
        [PokemonType.None] = Color.FromRgb(0x6B, 0x6B, 0x6B),
    };

    public static IBrush Brush(PokemonType type) =>
        new SolidColorBrush(Colors.TryGetValue(type, out Color color) ? color : Colors[PokemonType.None]);

    /// <summary>A dimmed fill, for a tile that must not fight the text on top of it.</summary>
    public static IBrush SoftBrush(PokemonType type)
    {
        Color color = Colors.TryGetValue(type, out Color found) ? found : Colors[PokemonType.None];
        return new SolidColorBrush(Color.FromArgb(0x34, color.R, color.G, color.B));
    }
}
