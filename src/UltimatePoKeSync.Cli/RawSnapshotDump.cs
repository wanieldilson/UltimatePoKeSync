using System.Text.Json;
using System.Text.Json.Serialization;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.Cli;

/// <summary>
/// Writes raw snapshots to disk so real RAM can become a test fixture.
/// </summary>
/// <remarks>
/// Fixtures built by hand with PKHeX prove the parser is self-consistent; they cannot
/// prove it agrees with what the game actually writes. Bytes captured from a live console
/// close that gap, and keep closing it when PKHeX is upgraded.
/// </remarks>
internal static class RawSnapshotDump
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Write(RawPartySnapshot raw, string directory)
    {
        Directory.CreateDirectory(directory);

        string fileName = $"{raw.Game.GameCode.ToLowerInvariant()}-seq{raw.Sequence:D4}.json";
        string path = Path.Combine(directory, fileName);

        var fixture = new SnapshotFixture
        {
            GameCode = raw.Game.GameCode,
            Title = raw.Game.Title,
            Revision = raw.Game.Revision,
            Generation = (int)raw.Game.Generation,
            PartyCount = raw.PartyCount,
            SlotSize = raw.SlotSize,
            Data = Convert.ToBase64String(raw.PartyData.Span),
        };

        File.WriteAllText(path, JsonSerializer.Serialize(fixture, Options));
        return path;
    }
}

internal sealed class SnapshotFixture
{
    [JsonPropertyName("gameCode")]
    public required string GameCode { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("revision")]
    public required int Revision { get; init; }

    [JsonPropertyName("generation")]
    public required int Generation { get; init; }

    [JsonPropertyName("partyCount")]
    public required int PartyCount { get; init; }

    [JsonPropertyName("slotSize")]
    public required int SlotSize { get; init; }

    [JsonPropertyName("data")]
    public required string Data { get; init; }
}
