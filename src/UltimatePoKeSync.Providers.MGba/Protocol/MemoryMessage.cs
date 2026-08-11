using System.Text.Json;

namespace UltimatePoKeSync.Providers.MGba.Protocol;

/// <summary>
/// A reply to a <c>read</c> command: the bytes asked for, or an error carrying the same id.
/// </summary>
/// <remarks>
/// Both shapes are consumed here. An error still has to reach the caller waiting on that
/// id, otherwise it waits for the timeout instead of being told straight away. See D-033.
/// </remarks>
internal static class MemoryMessage
{
    /// <summary>
    /// Returns <see langword="false"/> for anything that is not a reply — a party message,
    /// most of the time — so the caller can go on treating it as one.
    /// </summary>
    public static bool TryParse(
        string line,
        int supportedVersion,
        out int id,
        out byte[]? data)
    {
        id = 0;
        data = null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out JsonElement type))
            {
                return false;
            }

            string? kind = type.GetString();
            if (kind is not ("memory" or "error"))
            {
                return false;
            }

            if (root.TryGetProperty("v", out JsonElement version) &&
                version.TryGetInt32(out int value) &&
                value > supportedVersion)
            {
                return false;
            }

            if (!root.TryGetProperty("id", out JsonElement identifier) ||
                !identifier.TryGetInt32(out id))
            {
                return false;
            }

            if (kind == "error")
            {
                // Recognised, and deliberately empty: the request failed.
                return true;
            }

            if (root.TryGetProperty("data", out JsonElement payload) &&
                payload.GetString() is string encoded)
            {
                data = Convert.FromBase64String(encoded);
            }

            return true;
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return false;
        }
    }
}
