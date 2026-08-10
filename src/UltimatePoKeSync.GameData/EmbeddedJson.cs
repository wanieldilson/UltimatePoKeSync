using System.Reflection;
using System.Text.Json;

namespace UltimatePoKeSync.GameData;

internal static class EmbeddedJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static T Load<T>(string fileName)
    {
        Assembly assembly = typeof(EmbeddedJson).Assembly;
        string suffix = $".Data.{fileName}";
        string? resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(suffix, StringComparison.Ordinal));

        if (resourceName is null)
        {
            throw new InvalidOperationException($"Embedded game data not found: {fileName}");
        }

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Cannot open embedded game data: {fileName}");

        return JsonSerializer.Deserialize<T>(stream, Options)
            ?? throw new InvalidOperationException($"Embedded game data is empty: {fileName}");
    }
}
