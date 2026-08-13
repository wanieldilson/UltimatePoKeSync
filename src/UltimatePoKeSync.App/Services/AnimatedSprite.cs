using SkiaSharp;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// A sprite decoded from an image file: every frame as raw pixels, and how long each lasts.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia draws a GIF's first frame and stops, so an animation has to be taken apart and
/// played frame by frame. SkiaSharp does the decoding and is already inside Avalonia
/// (<c>Avalonia.Skia</c> depends on it), so animated sprites cost no new dependency. See
/// D-045.
/// </para>
/// <para>
/// What comes out is pixels, not <c>Bitmap</c>s. Building an Avalonia bitmap needs the
/// rendering platform to be running, which would make this untestable outside a window and
/// would tie decoding to drawing; <see cref="SpriteImage"/> does the conversion where a
/// window exists. The first version returned bitmaps and could not be tested at all.
/// </para>
/// <para>
/// Frames are composed cumulatively rather than decoded independently, because a GIF frame
/// usually stores only what changed since the last one: decoding frame five on its own gives
/// five pixels of a Pokémon and a lot of nothing.
/// </para>
/// </remarks>
public sealed class AnimatedSprite
{
    /// <summary>What a GIF means by "as fast as you like", and what browsers use for it.</summary>
    private static readonly TimeSpan DefaultFrameDuration = TimeSpan.FromMilliseconds(100);

    private AnimatedSprite(int width, int height, IReadOnlyList<Frame> frames)
    {
        Width = width;
        Height = height;
        Frames = frames;
    }

    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<Frame> Frames { get; }

    public bool IsAnimated => Frames.Count > 1;

    /// <summary>One frame: its pixels in BGRA order, and how long it stays on screen.</summary>
    public sealed record Frame(byte[] Bgra, TimeSpan Duration);

    /// <summary>
    /// Decodes an image, animated or not. Returns null for bytes that are not an image at
    /// all, because a sprite folder is somebody's own directory and may hold anything.
    /// </summary>
    public static AnimatedSprite? Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
        {
            return null;
        }

        using SKData data = SKData.CreateCopy(bytes);
        using SKCodec? codec = SKCodec.Create(data);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            return null;
        }

        var info = new SKImageInfo(
            codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);

        int count = Math.Max(1, codec.FrameCount);
        var frames = new List<Frame>(count);

        // One canvas for all of them: the pixels left by the previous frame are what a
        // partial frame is drawn on top of.
        using var canvas = new SKBitmap(info);

        for (int index = 0; index < count; index++)
        {
            TimeSpan duration = DefaultFrameDuration;
            var options = new SKCodecOptions(index);

            if (codec.FrameCount > 0)
            {
                SKCodecFrameInfo frame = codec.FrameInfo[index];
                options = new SKCodecOptions(index, frame.RequiredFrame);
                if (frame.Duration > 0)
                {
                    duration = TimeSpan.FromMilliseconds(frame.Duration);
                }
            }

            if (codec.GetPixels(info, canvas.GetPixels(), options)
                is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                break;
            }

            frames.Add(new Frame(canvas.Bytes, duration));
        }

        return frames.Count == 0 ? null : new AnimatedSprite(info.Width, info.Height, frames);
    }
}
