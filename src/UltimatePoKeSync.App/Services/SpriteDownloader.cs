using System.Net;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// Fetches the sprite folder on the player's behalf, from inside the app. See D-046.
/// </summary>
/// <remarks>
/// <para>
/// The same files <c>tools/fetch-sprites.py</c> downloads, put in the same place, for people
/// who have an executable rather than a clone of the repository — which is almost everybody.
/// Without this the sprites were a developer feature: the release carries no <c>tools</c>
/// folder, so there was no script to run and no Python to run it with.
/// </para>
/// <para>
/// This is the app's only network call, it happens because somebody pressed a button, and
/// nothing waits on it. An app with no network works exactly as it did before, with a
/// coloured tile per Pokémon.
/// </para>
/// </remarks>
public sealed class SpriteDownloader
{
    private const string Root =
        "https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon"
        + "/versions/generation-v/black-white/animated";

    /// <summary>Gen 5 ends at Genesect, and so does this sprite style.</summary>
    public const int LastSpecies = 649;

    /// <summary>Enough to keep the link busy without hammering a public host.</summary>
    private const int Parallelism = 8;

    private readonly HttpClient _client;
    private readonly string _folder;

    public SpriteDownloader(HttpClient client, string? folder = null)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        _folder = folder ?? SpritePackSource.DefaultFolder;
    }

    /// <param name="progress">How many are done, out of how many were asked for.</param>
    public async Task<Result> DownloadAsync(
        IProgress<Progress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_folder);

        int done = 0;
        int fetched = 0;
        int missing = 0;
        int failed = 0;

        await Parallel.ForEachAsync(
            Enumerable.Range(1, LastSpecies),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = Parallelism,
                CancellationToken = cancellationToken,
            },
            async (species, token) =>
            {
                Outcome outcome = await FetchAsync(species, token).ConfigureAwait(false);

                switch (outcome)
                {
                    case Outcome.Fetched: Interlocked.Increment(ref fetched); break;
                    case Outcome.Missing: Interlocked.Increment(ref missing); break;
                    case Outcome.Failed: Interlocked.Increment(ref failed); break;
                    default: break;
                }

                progress?.Report(new Progress(Interlocked.Increment(ref done), LastSpecies));
            }).ConfigureAwait(false);

        return new Result(fetched, missing, failed);
    }

    private async Task<Outcome> FetchAsync(int species, CancellationToken cancellationToken)
    {
        string path = Path.Combine(_folder, $"{species}.gif");

        // Already there is the common case on a second run, and re-fetching 27 MB to find
        // that out would be the rudest possible way to resume.
        if (File.Exists(path) && new FileInfo(path).Length > 0)
        {
            return Outcome.Present;
        }

        try
        {
            using HttpResponseMessage response = await _client
                .GetAsync($"{Root}/{species}.gif", cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Not every number has artwork, and that is not a failure.
                return Outcome.Missing;
            }

            if (!response.IsSuccessStatusCode)
            {
                return Outcome.Failed;
            }

            byte[] bytes = await response.Content
                .ReadAsByteArrayAsync(cancellationToken)
                .ConfigureAwait(false);

            if (bytes.Length == 0)
            {
                return Outcome.Failed;
            }

            // Written beside and moved into place, so an interrupted download never leaves
            // a half a sprite behind that the next run would treat as finished.
            string partial = path + ".part";
            await File.WriteAllBytesAsync(partial, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(partial, path, overwrite: true);

            return Outcome.Fetched;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // No network, a proxy, a full disk. All of them mean "not today" rather than a
            // crash in an app whose job is showing a team.
            return Outcome.Failed;
        }
    }

    public readonly record struct Progress(int Done, int Total)
    {
        public double Fraction => Total == 0 ? 0 : (double)Done / Total;
    }

    public sealed record Result(int Fetched, int Missing, int Failed)
    {
        public bool AnyArrived => Fetched > 0;
    }

    private enum Outcome
    {
        Fetched,
        Present,
        Missing,
        Failed,
    }
}
