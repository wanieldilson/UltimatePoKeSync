using UltimatePoKeSync.App.Services;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// Taking an animated GIF apart into frames. See D-045.
/// </summary>
/// <remarks>
/// The fixture is a two-frame GIF built by hand — four pixels of red, then four of blue, at
/// fifty milliseconds each — rather than a real sprite, so the test owns everything it
/// asserts and needs no Pokémon art in the repository.
/// </remarks>
public sealed class AnimatedSpriteTests
{
    /// <summary>4×4, red then blue, 50 ms a frame, looping.</summary>
    private const string TwoFrames =
        "R0lGODlhBAAEAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQABQAAACwAAAAABAAE"
        + "AAAICQABCBxIsCCAgAAh+QQBBQABACwAAAAABAAEAIEAAP8AAAAAAAAAAAAICQABCBxIsCCAgAA7";

    [Fact]
    public void EveryFrameOfAnAnimationIsKept()
    {
        AnimatedSprite? sprite = AnimatedSprite.Decode(Convert.FromBase64String(TwoFrames));

        Assert.NotNull(sprite);
        Assert.True(sprite.IsAnimated);
        Assert.Equal(2, sprite.Frames.Count);
        Assert.Equal(4, sprite.Width);
        Assert.Equal(4, sprite.Height);

        // Four pixels each way, four bytes a pixel.
        Assert.All(sprite.Frames, frame => Assert.Equal(4 * 4 * 4, frame.Bgra.Length));
    }

    /// <summary>
    /// Red then blue, and the second frame must actually be blue: a GIF frame usually stores
    /// only what changed, so decoding each one on its own gives a mostly empty picture.
    /// </summary>
    [Fact]
    public void AFrameIsDrawnOnTopOfTheOneBeforeIt()
    {
        AnimatedSprite sprite = AnimatedSprite.Decode(Convert.FromBase64String(TwoFrames))!;

        // BGRA, so the blue channel comes first and the red one third.
        byte[] first = sprite.Frames[0].Bgra;
        byte[] second = sprite.Frames[1].Bgra;

        Assert.True(first[2] > 200, "the first frame should be red");
        Assert.True(second[0] > 200, "the second frame should be blue");
    }

    /// <summary>
    /// The GIF says five hundredths of a second, and that is what has to come back: playing
    /// every sprite at one speed would make some of them wrong and all of them the same.
    /// </summary>
    [Fact]
    public void TheGifsOwnTimingIsWhatIsUsed()
    {
        AnimatedSprite sprite = AnimatedSprite.Decode(Convert.FromBase64String(TwoFrames))!;

        Assert.All(sprite.Frames, frame => Assert.Equal(50, frame.Duration.TotalMilliseconds));
    }

    [Fact]
    public void SomethingThatIsNotAnImageIsRefusedRatherThanThrowing()
    {
        // A sprite folder is somebody's own directory and may hold anything at all.
        Assert.Null(AnimatedSprite.Decode("this is not a picture"u8.ToArray()));
        Assert.Null(AnimatedSprite.Decode([]));
    }
}
