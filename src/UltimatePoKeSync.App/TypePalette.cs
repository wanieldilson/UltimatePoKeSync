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
        [PokemonType.Normal] = Color.FromRgb(0x9F, 0xA1, 0x9F),
        [PokemonType.Fighting] = Color.FromRgb(0xC0, 0x3D, 0x2E),
        [PokemonType.Flying] = Color.FromRgb(0x7C, 0x9B, 0xE0),
        [PokemonType.Poison] = Color.FromRgb(0x92, 0x4A, 0x96),
        [PokemonType.Ground] = Color.FromRgb(0xB2, 0x8B, 0x4A),
        [PokemonType.Rock] = Color.FromRgb(0x9A, 0x8A, 0x50),
        [PokemonType.Bug] = Color.FromRgb(0x81, 0x96, 0x24),
        [PokemonType.Ghost] = Color.FromRgb(0x64, 0x5A, 0x9B),
        [PokemonType.Steel] = Color.FromRgb(0x77, 0x7F, 0x8C),
        [PokemonType.Fire] = Color.FromRgb(0xD9, 0x60, 0x2E),
        [PokemonType.Water] = Color.FromRgb(0x3E, 0x76, 0xC4),
        [PokemonType.Grass] = Color.FromRgb(0x4E, 0x9A, 0x3F),
        [PokemonType.Electric] = Color.FromRgb(0xC9, 0xA2, 0x27),
        [PokemonType.Psychic] = Color.FromRgb(0xC7, 0x4B, 0x7C),
        [PokemonType.Ice] = Color.FromRgb(0x59, 0x9F, 0xB0),
        [PokemonType.Dragon] = Color.FromRgb(0x5B, 0x50, 0xC0),
        [PokemonType.Dark] = Color.FromRgb(0x5A, 0x4C, 0x45),
        [PokemonType.Fairy] = Color.FromRgb(0xC4, 0x74, 0xA8),
        [PokemonType.None] = Color.FromRgb(0x6B, 0x6B, 0x6B),
    };

    public static IBrush Brush(PokemonType type) =>
        new SolidColorBrush(Colors.TryGetValue(type, out Color color) ? color : Colors[PokemonType.None]);

    /// <summary>A dimmed fill, for a tile that must not fight the text on top of it.</summary>
    public static IBrush SoftBrush(PokemonType type)
    {
        Color color = Colors.TryGetValue(type, out Color found) ? found : Colors[PokemonType.None];
        return new SolidColorBrush(Color.FromArgb(0x3C, color.R, color.G, color.B));
    }
}
