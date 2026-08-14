using System.Net;
using System.Text;
using UltimatePoKeSync.App.Services;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// The update notice, and mostly the cases where it must say nothing. An app that reads an
/// emulator has to keep working with no internet, so every failure here is silence. See D-056.
/// </summary>
public sealed class UpdateCheckTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Version Running = new(0, 2, 1);

    [Theory]
    [InlineData("v0.3.0", "0.3.0")]
    [InlineData("0.3.0", "0.3.0")]
    [InlineData("V1.0", "1.0")]
    public void TagsAreReadWithOrWithoutTheirV(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateCheck.Parse(tag));

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData(null)]
    public void SomethingThatIsNotAVersionIsNotGuessedAt(string? tag) =>
        Assert.Null(UpdateCheck.Parse(tag));

    [Fact]
    public async Task ANewerReleaseIsOffered()
    {
        AvailableUpdate? found = await Check(Release("v0.3.0")).FindAsync(Running, null, Token);

        Assert.NotNull(found);
        Assert.Equal("0.3.0", found.Version);
        Assert.Contains("releases", found.Url, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("v0.2.1")]
    [InlineData("v0.1.9")]
    public async Task TheSameOrAnOlderReleaseIsNotAnUpdate(string tag) =>
        Assert.Null(await Check(Release(tag)).FindAsync(Running, null, Token));

    /// <summary>
    /// Turned down once is turned down for good. Asking again every launch is how a notice
    /// becomes something people dismiss without reading.
    /// </summary>
    [Fact]
    public async Task AVersionAlreadyTurnedDownIsNotOfferedAgain() =>
        Assert.Null(await Check(Release("v0.3.0")).FindAsync(Running, "0.3.0", Token));

    /// <summary>But a release newer than the one turned down is a new question.</summary>
    [Fact]
    public async Task ALaterReleaseThanTheOneTurnedDownIsOffered()
    {
        AvailableUpdate? found = await Check(Release("v0.4.0")).FindAsync(Running, "0.3.0", Token);

        Assert.NotNull(found);
        Assert.Equal("0.4.0", found.Version);
    }

    [Fact]
    public async Task ABuildThatIsNotAReleaseIsNeverToldAboutOne() =>
        Assert.Null(await Check(Release("v9.9.9")).FindAsync(running: null, null, Token));

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]          // the anonymous rate limit
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GitHubSayingNoIsSilence(HttpStatusCode status) =>
        Assert.Null(await Check(new FakeHandler(status, "")).FindAsync(Running, null, Token));

    [Fact]
    public async Task NoNetworkIsSilence() =>
        Assert.Null(await Check(new FakeHandler(new HttpRequestException("offline")))
            .FindAsync(Running, null, Token));

    [Fact]
    public async Task NonsenseInsteadOfJsonIsSilence() =>
        Assert.Null(await Check(new FakeHandler(HttpStatusCode.OK, "<html>whoops</html>"))
            .FindAsync(Running, null, Token));

    /// <summary>Drafts and pre-releases are not what "the latest version" means to a player.</summary>
    [Theory]
    [InlineData("\"draft\": true, \"prerelease\": false")]
    [InlineData("\"draft\": false, \"prerelease\": true")]
    public async Task DraftsAndPreReleasesAreNotOffered(string flags) =>
        Assert.Null(await Check(new FakeHandler(
            HttpStatusCode.OK,
            $$"""{"tag_name": "v0.3.0", "html_url": "https://example.invalid/releases", {{flags}}}"""))
            .FindAsync(Running, null, Token));

    private static UpdateCheck Check(FakeHandler handler) => new(new HttpClient(handler));

    private static FakeHandler Release(string tag) => new(
        HttpStatusCode.OK,
        $$"""
        {"tag_name": "{{tag}}",
         "html_url": "https://github.com/ringoliRob/UltimatePoKeSync/releases/tag/{{tag}}",
         "draft": false, "prerelease": false}
        """);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body = string.Empty;
        private readonly Exception? _throws;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public FakeHandler(Exception throws) => _throws = throws;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_throws is not null)
            {
                throw _throws;
            }

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
