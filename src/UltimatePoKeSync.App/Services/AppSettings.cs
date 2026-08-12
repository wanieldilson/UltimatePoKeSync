using System.Text.Json;
using System.Text.Json.Serialization;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// The handful of things the window should remember between runs: where it was, how big it
/// was, and which profile was chosen. See D-038.
/// </summary>
/// <remarks>
/// Stored next to the Lua script, in the folder of D-029 that survives updates and is never
/// translocated. Nothing here is important enough to warn about if it goes missing, so every
/// failure ends in defaults rather than in a dialog: a settings file is not worth a crash on
/// startup.
/// </remarks>
public sealed record AppSettings
{
    public const int MinimumWidth = 900;
    public const int MinimumHeight = 600;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public double? WindowWidth { get; init; }

    public double? WindowHeight { get; init; }

    public int? WindowX { get; init; }

    public int? WindowY { get; init; }

    public bool WindowMaximised { get; init; }

    public bool CompetitiveProfile { get; init; }

    public static string FilePath => Path.Combine(SetupGuide.ScriptDirectory, "settings.json");

    /// <param name="path">Overridden by the tests, so a run never touches the real file.</param>
    public static AppSettings Load(string? path = null)
    {
        path ??= FilePath;

        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception)
        {
            // A corrupt or unreadable settings file means the defaults, not a broken start.
            return new AppSettings();
        }
    }

    public void Save(string? path = null)
    {
        path ??= FilePath;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
        }
        catch (Exception)
        {
            // Losing the layout is not worth interrupting someone who is closing the app.
        }
    }

    /// <summary>
    /// Whether a stored size is worth restoring. A window saved at 40×20 — from a crash, a
    /// disconnected second monitor, or a hand-edited file — would reopen unusable.
    /// </summary>
    public bool HasUsableSize =>
        WindowWidth >= MinimumWidth && WindowHeight >= MinimumHeight;

    public bool HasPosition => WindowX is not null && WindowY is not null;
}
