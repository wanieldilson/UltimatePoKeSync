using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace UltimatePoKeSync.App.Services;

/// <summary>Whether a newer release exists, and where to get it.</summary>
public sealed record AvailableUpdate(string Version, string Url);

/// <summary>
/// Asks GitHub once per launch whether there is a newer release.
/// </summary>
/// <remarks>
/// <para>
/// This is the app's second network call, and unlike the sprite download it happens without
/// anybody pressing anything, which is a change to what D-046 promised. It is allowed to be
/// silent and it is never allowed to be in the way: no network, no GitHub, a rate limit, a
/// shape it does not recognise, all produce null and nothing on screen. An app that cannot
/// read the internet still reads the emulator, which is the thing it is for. See D-056.
/// </para>
/// <para>
/// A build that is not a release says 0.0.0 and is skipped. Somebody running from source is
/// not somebody to tell about releases.
/// </para>
/// </remarks>
public sealed class UpdateCheck(HttpClient http)
{
    private const string LatestRelease =
        "https://api.github.com/repos/ringoliRob/UltimatePoKeSync/releases/latest";

    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <summary>The version this build was published as, or null for anything else.</summary>
    public static Version? Running
    {
        get
        {
            string? raw = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            // The informational version carries build metadata after a '+'.
            return Parse(raw?.Split('+')[0]) is Version version && version != new Version(0, 0, 0)
                ? version
                : null;
        }
    }

    /// <summary>
    /// Turns a tag or a version string into something comparable, or null. Tags are
    /// v-prefixed and versions are not, and a four-part version compares fine against three.
    /// </summary>
    public static Version? Parse(string? text)
    {
        string trimmed = (text ?? string.Empty).Trim().TrimStart('v', 'V');
        return Version.TryParse(trimmed, out Version? version) ? version : null;
    }

    /// <summary>
    /// The newest release, when it is newer than this build and has not been turned down
    /// already. Null in every other case, including every kind of failure.
    /// </summary>
    public async Task<AvailableUpdate?> FindAsync(
        Version? running,
        string? declined,
        CancellationToken cancellationToken = default)
    {
        if (running is null)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestRelease);
            request.Headers.Add("Accept", "application/vnd.github+json");

            // GitHub refuses anonymous calls without one, and saying who is asking is
            // politer than a bare default.
            request.Headers.Add("User-Agent", $"UltimatePoKeSync/{running}");

            using HttpResponseMessage response = await _http
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            Release? release = await response.Content
                .ReadFromJsonAsync<Release>(cancellationToken)
                .ConfigureAwait(false);

            if (release is null || release.Draft || release.Prerelease)
            {
                return null;
            }

            Version? latest = Parse(release.TagName);
            if (latest is null || latest <= running)
            {
                return null;
            }

            // Turned down once is turned down for good, for that version. Asking again every
            // launch is how an update notice becomes something people learn to dismiss
            // without reading.
            if (Parse(declined) is Version skipped && skipped >= latest)
            {
                return null;
            }

            string url = string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? "https://github.com/ringoliRob/UltimatePoKeSync/releases/latest"
                : release.HtmlUrl;

            return new AvailableUpdate(latest.ToString(), url);
        }
        catch (Exception)
        {
            // Offline, blocked, rate-limited, or something unrecognisable. None of it is
            // worth a word on screen: the app works without this.
            return null;
        }
    }

    /// <summary>Hands the release page to whatever the desktop uses to open links.</summary>
    public static void Open(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url).Dispose();
            }
            else
            {
                Process.Start("xdg-open", url).Dispose();
            }
        }
        catch (Exception)
        {
            // A desktop with no browser is not a crash.
        }
    }

    private sealed record Release
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }
    }
}
