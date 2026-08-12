using System.Net;
using UltimatePoKeSync.App.Services;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// Fetching the sprite folder from inside the app. See D-046.
/// </summary>
/// <remarks>
/// Against a stand-in for the archive, so the tests need no network and no Pokémon artwork:
/// what comes back is the same hand-made two-frame GIF the decoder tests use.
/// </remarks>
public sealed class SpriteDownloaderTests : IDisposable
{
    private const string TwoFrames =
        "R0lGODlhBAAEAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQABQAAACwAAAAABAAE"
        + "AAAICQABCBxIsCCAgAAh+QQBBQABACwAAAAABAAEAIEAAP8AAAAAAAAAAAAICQABCBxIsCCAgAA7";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        $"upks-download-{Guid.NewGuid():N}");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EverySpriteTheArchiveHasIsWrittenToTheFolder()
    {
        var archive = new FakeArchive();
        var downloader = new SpriteDownloader(new HttpClient(archive), _folder);

        SpriteDownloader.Result result = await downloader.DownloadAsync(cancellationToken: Token);

        Assert.Equal(SpriteDownloader.LastSpecies, result.Fetched);
        Assert.Equal(0, result.Failed);
        Assert.True(result.AnyArrived);
        Assert.True(File.Exists(Path.Combine(_folder, "495.gif")));

        // And the app can find them straight away.
        Assert.NotNull(new SpritePackSource(_folder).Find(495, shiny: false));
    }

    [Fact]
    public async Task ProgressIsReportedAllTheWayToTheEnd()
    {
        var archive = new FakeArchive();
        var downloader = new SpriteDownloader(new HttpClient(archive), _folder);

        var seen = new List<SpriteDownloader.Progress>();
        await downloader.DownloadAsync(
            new Progress<SpriteDownloader.Progress>(seen.Add), Token);

        // Progress is reported from a thread pool, so the last report may still be in
        // flight; what matters is that it counts up and reaches the end.
        Assert.NotEmpty(seen);
        Assert.All(seen, step => Assert.Equal(SpriteDownloader.LastSpecies, step.Total));
        Assert.Contains(seen, step => step.Done == SpriteDownloader.LastSpecies);
        Assert.Equal(1, seen.Max(step => step.Fraction));
    }

    /// <summary>Not every number has artwork, and that is not a failure.</summary>
    [Fact]
    public async Task AMissingSpriteIsNotAFailure()
    {
        var archive = new FakeArchive { MissingAbove = 100 };
        var downloader = new SpriteDownloader(new HttpClient(archive), _folder);

        SpriteDownloader.Result result = await downloader.DownloadAsync(cancellationToken: Token);

        Assert.Equal(100, result.Fetched);
        Assert.Equal(SpriteDownloader.LastSpecies - 100, result.Missing);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task NoNetworkMeansNoSpritesRatherThanACrash()
    {
        var archive = new FakeArchive { Throw = true };
        var downloader = new SpriteDownloader(new HttpClient(archive), _folder);

        SpriteDownloader.Result result = await downloader.DownloadAsync(cancellationToken: Token);

        Assert.Equal(0, result.Fetched);
        Assert.False(result.AnyArrived);
        Assert.Equal(SpriteDownloader.LastSpecies, result.Failed);
    }

    /// <summary>
    /// A second run must not re-fetch 27 MB to discover it already has them, and must not
    /// count someone else's files as its own work.
    /// </summary>
    [Fact]
    public async Task WhatIsAlreadyThereIsLeftAlone()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(
            Path.Combine(_folder, "495.gif"), Convert.FromBase64String(TwoFrames));

        var archive = new FakeArchive();
        var downloader = new SpriteDownloader(new HttpClient(archive), _folder);

        SpriteDownloader.Result result = await downloader.DownloadAsync(cancellationToken: Token);

        Assert.Equal(SpriteDownloader.LastSpecies - 1, result.Fetched);
        Assert.DoesNotContain(495, archive.Requested);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private sealed class FakeArchive : HttpMessageHandler
    {
        private readonly Lock _gate = new();

        public List<int> Requested { get; } = [];

        /// <summary>Species above this have no artwork.</summary>
        public int MissingAbove { get; init; } = int.MaxValue;

        public bool Throw { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (Throw)
            {
                throw new HttpRequestException("no network");
            }

            string name = Path.GetFileNameWithoutExtension(request.RequestUri!.AbsolutePath);
            int species = int.Parse(name);

            lock (_gate)
            {
                Requested.Add(species);
            }

            if (species > MissingAbove)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Convert.FromBase64String(TwoFrames)),
            });
        }
    }
}
