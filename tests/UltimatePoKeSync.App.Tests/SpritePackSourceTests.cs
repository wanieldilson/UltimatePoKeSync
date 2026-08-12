using UltimatePoKeSync.App.Services;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// Sprites from the player's own folder. See D-045.
/// </summary>
/// <remarks>
/// The fixture is the same hand-made two-frame GIF the decoder tests use, written into a
/// temporary folder: the repository holds no Pokémon artwork, and neither does its test
/// suite.
/// </remarks>
public sealed class SpritePackSourceTests : IDisposable
{
    private const string TwoFrames =
        "R0lGODlhBAAEAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQABQAAACwAAAAABAAE"
        + "AAAICQABCBxIsCCAgAAh+QQBBQABACwAAAAABAAEAIEAAP8AAAAAAAAAAAAICQABCBxIsCCAgAA7";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        $"upks-sprites-{Guid.NewGuid():N}");

    [Fact]
    public void ASpeciesIsFoundByItsNumber()
    {
        Write("495.gif");
        var pack = new SpritePackSource(_folder);

        Assert.True(pack.Exists);
        AnimatedSprite? sprite = pack.Find(495, shiny: false);

        Assert.NotNull(sprite);
        Assert.Equal(2, sprite.Frames.Count);
    }

    [Fact]
    public void ASpeciesWithNoFileIsSimplyAbsent()
    {
        Write("495.gif");
        var pack = new SpritePackSource(_folder);

        Assert.Null(pack.Find(1, shiny: false));
        Assert.Null(pack.Find(0, shiny: false));
        Assert.Null(pack.Find(-1, shiny: false));
    }

    [Fact]
    public void AShinyComesFromItsOwnFolder()
    {
        Write("495.gif");
        Write(Path.Combine("shiny", "495.gif"));
        var pack = new SpritePackSource(_folder);

        Assert.NotNull(pack.Find(495, shiny: true));
    }

    /// <summary>
    /// A missing shiny falls back to the ordinary sprite. The tile already marks shininess
    /// with a star, so the wrong colours say more than an empty box would.
    /// </summary>
    [Fact]
    public void AMissingShinyFallsBackToTheOrdinarySprite()
    {
        Write("495.gif");
        var pack = new SpritePackSource(_folder);

        Assert.NotNull(pack.Find(495, shiny: true));
    }

    [Fact]
    public void AFolderThatIsNotThereIsNotAnError()
    {
        var pack = new SpritePackSource(Path.Combine(_folder, "nowhere"));

        Assert.False(pack.Exists);
        Assert.Null(pack.Find(495, shiny: false));
    }

    /// <summary>Somebody's own folder can hold a truncated download.</summary>
    [Fact]
    public void AFileThatIsNotAnImageIsIgnored()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "495.gif"), "half a download");

        Assert.Null(new SpritePackSource(_folder).Find(495, shiny: false));
    }

    private void Write(string name)
    {
        string path = Path.Combine(_folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Convert.FromBase64String(TwoFrames));
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
