namespace UltimatePoKeSync.App.Services;

/// <summary>
/// The setup instructions shown until the emulator connects.
/// </summary>
/// <remarks>
/// mGBA has no command-line option to load a script, so this step cannot be automated
/// away — only made short and unambiguous. The script is shipped next to the executable
/// precisely so the screen can hand over a real path to copy. See D-028.
/// </remarks>
public static class SetupGuide
{
    public static string PlatformName =>
        OperatingSystem.IsMacOS() ? "macOS"
        : OperatingSystem.IsWindows() ? "Windows"
        : OperatingSystem.IsLinux() ? "Linux"
        : "this system";

    /// <summary>Absolute path of the bridge script shipped with the application.</summary>
    public static string ScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "emulator-scripts", "ups_bridge.lua");

    public static bool ScriptExists => File.Exists(ScriptPath);

    public static IReadOnlyList<string> Steps(int port) =>
    [
        OperatingSystem.IsMacOS()
            ? "Install mGBA 0.10.5 or later and open it from Applications."
            : OperatingSystem.IsWindows()
                ? "Install mGBA 0.10.5 or later and open it from the Start menu."
                : "Install mGBA 0.10.5 or later from your package manager and open it.",
        "Load your Gen 3 ROM: File → Load ROM.",
        OperatingSystem.IsMacOS()
            ? "Open Tools → Scripting… → File → Load script…"
            : "Open Tools → Scripting… then File → Load script…",
        $"Pick the script at the path below, then keep playing. This window connects on port {port} by itself.",
    ];

    /// <summary>Shown when the port is busy, which is nearly always a second mGBA.</summary>
    public static string PortHelp(int port) =>
        $"Nothing yet on port {port}. If mGBA is running with the script loaded, check its "
        + "scripting console for an error, and make sure no second copy of mGBA is holding "
        + "the port.";
}
