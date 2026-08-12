using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using UltimatePoKeSync.App.Services;
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

    /// <summary>
    /// One frame of an animated sprite. Its pixels are already in the order Avalonia wants,
    /// so this only wraps them.
    /// </summary>
    public static Bitmap From(AnimatedSprite sprite, AnimatedSprite.Frame frame)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(frame);

        var bitmap = new WriteableBitmap(
            new PixelSize(sprite.Width, sprite.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using ILockedFramebuffer buffer = bitmap.Lock();
        System.Runtime.InteropServices.Marshal.Copy(
            frame.Bgra, 0, buffer.Address, frame.Bgra.Length);

        return bitmap;
    }
}
