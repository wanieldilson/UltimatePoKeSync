using System.Text.Json.Serialization;

namespace UltimatePoKeSync.Providers.MGba.Protocol;

/// <summary>
/// Wire shape of a <c>party</c> message. See docs/protocol.md.
/// </summary>
internal sealed class PartyMessage
{
    [JsonPropertyName("v")]
    public int Version { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("seq")]
    public ulong Sequence { get; set; }

    [JsonPropertyName("frame")]
    public long Frame { get; set; }

    [JsonPropertyName("game")]
    public GameMessage? Game { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("slotSize")]
    public int SlotSize { get; set; }

    [JsonPropertyName("slots")]
    public int Slots { get; set; }

    [JsonPropertyName("data")]
    public string? Data { get; set; }
}

internal sealed class GameMessage
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("rev")]
    public int Revision { get; set; }

    [JsonPropertyName("gen")]
    public int Generation { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(PartyMessage))]
internal sealed partial class ProtocolJsonContext : JsonSerializerContext;
