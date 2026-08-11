using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using UltimatePoKeSync.GameData.Sprites;

namespace UltimatePoKeSync.App;

/// <summary>
/// Turns a decoded sprite into something Avalonia can draw.
/// </summary>
/// <remarks>
/// The decoder deals in raw RGBA and knows nothing about a UI framework, which is what
/// keeps it testable without one. This is the one place that bridges the two.
/// </remarks>
public static class SpriteImage
{
    public static Bitmap From(DecodedSprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);

        var bitmap = new WriteableBitmap(
            new PixelSize(sprite.Width, sprite.Height),
            new Vector(96, 96),
            PixelFormat.Rgba8888,
            AlphaFormat.Unpremul);

        using ILockedFramebuffer buffer = bitmap.Lock();
        System.Runtime.InteropServices.Marshal.Copy(
            sprite.Rgba, 0, buffer.Address, sprite.Rgba.Length);

        return bitmap;
    }
}
