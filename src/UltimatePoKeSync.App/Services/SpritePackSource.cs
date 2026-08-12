using System.Collections.Concurrent;

namespace UltimatePoKeSync.App.Services;

/// <summary>
/// Sprites from a folder on the player's disk, one file per national dex number.
/// </summary>
/// <remarks>
/// <para>
/// The app ships no Pokémon artwork: it belongs to Nintendo, Game Freak and Creatures, and
/// the collections that gather it state no licence of their own. So the art comes from the
/// player, exactly as D-033 already had it come from their cartridge — this is the same
/// principle with a second source, and the one that works for a DS game whose ROM is not
/// mapped into memory at all. See D-045.
/// </para>
/// <para>
/// The folder is the app's own data directory by default, which is where
/// <c>tools/fetch-sprites.py</c> puts things, so a player who runs it has nothing to
/// configure afterwards. One style covers Gen 1 to Gen 5, so an Emerald team and a Black
/// team look like they belong to the same app.
/// </para>
/// </remarks>
public sealed class SpritePackSource
{
    private readonly ConcurrentDictionary<(int Species, bool Shiny), AnimatedSprite?> _cache = new();
    private readonly string _folder;

    public SpritePackSource(string? folder = null) =>
        _folder = folder is { Length: > 0 } ? folder : DefaultFolder;

    /// <summary>Beside the Lua script, in the folder of D-029 that survives updates.</summary>
    public static string DefaultFolder => Path.Combine(SetupGuide.ScriptDirectory, "sprites");

    /// <summary>Whether there is anything here at all, so the UI can offer to explain.</summary>
    public bool Exists => Directory.Exists(_folder) && Directory.EnumerateFiles(_folder, "*.gif").Any();

    public string Folder => _folder;

    /// <summary>
    /// The sprite for a species, or null when the folder does not have it. A shiny falls
    /// back to the ordinary sprite rather than showing nothing: the tile already marks
    /// shininess with a star, and a missing picture would say less than the wrong colours.
    /// </summary>
    public AnimatedSprite? Find(int nationalSpeciesId, bool shiny)
    {
        if (nationalSpeciesId <= 0)
        {
            return null;
        }

        return _cache.GetOrAdd((nationalSpeciesId, shiny), key =>
            Load(key.Species, key.Shiny) ?? (key.Shiny ? Load(key.Species, false) : null));
    }

    private AnimatedSprite? Load(int species, bool shiny)
    {
        string path = shiny
            ? Path.Combine(_folder, "shiny", $"{species}.gif")
            : Path.Combine(_folder, $"{species}.gif");

        try
        {
            return File.Exists(path) ? AnimatedSprite.Decode(File.ReadAllBytes(path)) : null;
        }
        catch (Exception)
        {
            // Somebody's own folder can hold a truncated download or a file they cannot
            // read. Neither is worth interrupting a team for.
            return null;
        }
    }
}
