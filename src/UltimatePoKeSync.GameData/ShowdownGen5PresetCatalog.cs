using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// Pinned Pokémon Showdown Gen 3 Random Battle data. These sets are broad role and
/// movepool references, not claims about the standard competitive metagame.
/// </summary>
public sealed class ShowdownGen5PresetCatalog : IReferencePresetCatalog
{
    /// <summary>The same pinned commit as Gen 3, so both describe one snapshot of the world.</summary>
    public const string Revision = ShowdownGen3PresetCatalog.Revision;

    private static readonly Lazy<ShowdownGen5PresetCatalog> LazyInstance =
        new(() => new ShowdownGen5PresetCatalog());

    private readonly IReadOnlyDictionary<string, ShowdownSpeciesData> _species;

    private ShowdownGen5PresetCatalog()
    {
        Dictionary<string, ShowdownSpeciesData> species =
            EmbeddedJson.Load<Dictionary<string, ShowdownSpeciesData>>(
                "gen5-showdown-randbats-sets.json");

        Validate(species);
        _species = species;
    }

    public static ShowdownGen5PresetCatalog Instance => LazyInstance.Value;

    public PokemonGeneration Generation => PokemonGeneration.Gen5;

    public string SourceName => "Pokémon Showdown Gen 5 Random Battle";

    public string SourceRevision => Revision;

    public int SpeciesCount => _species.Count;

    public IReadOnlyList<ReferencePreset> Find(string speciesName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speciesName);

        if (!_species.TryGetValue(
            ShowdownIdentifier.Normalize(speciesName),
            out ShowdownSpeciesData? species))
        {
            return [];
        }

        return [.. species.Sets.Select(set => new ReferencePreset(
            set.Role,
            set.MovePool,
            set.Abilities,
            set.PreferredTypes ?? []))];
    }

    private static void Validate(IReadOnlyDictionary<string, ShowdownSpeciesData> species)
    {
        const int expectedSpecies = 388;
        const int expectedSets = 606;

        if (species.Count != expectedSpecies || species.Sum(entry => entry.Value.Sets.Length) != expectedSets)
        {
            throw new InvalidOperationException(
                $"Unexpected Showdown Gen 5 preset dimensions; expected {expectedSpecies} species and {expectedSets} sets.");
        }

        foreach ((string key, ShowdownSpeciesData entry) in species)
        {
            if (key != ShowdownIdentifier.Normalize(key) || entry.Sets.Length == 0 ||
                entry.Sets.Any(set => string.IsNullOrWhiteSpace(set.Role) ||
                    set.MovePool.Length == 0 || set.Abilities.Length == 0))
            {
                throw new InvalidOperationException($"Invalid Showdown Gen 5 preset entry: {key}.");
            }
        }
    }

    private sealed record ShowdownSpeciesData(int Level, ShowdownPresetData[] Sets);

    private sealed record ShowdownPresetData(
        string Role,
        string[] MovePool,
        string[] Abilities,
        string[]? PreferredTypes);
}
