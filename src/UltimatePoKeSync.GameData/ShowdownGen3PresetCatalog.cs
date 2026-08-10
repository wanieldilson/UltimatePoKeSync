using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.GameData;

/// <summary>
/// Pinned Pokémon Showdown Gen 3 Random Battle data. These sets are broad role and
/// movepool references, not claims about the standard competitive metagame.
/// </summary>
public sealed class ShowdownGen3PresetCatalog : IReferencePresetCatalog
{
    public const string Revision = "db93869dcc216c0be39e7f86e9a64edcc7496d89";

    private static readonly Lazy<ShowdownGen3PresetCatalog> LazyInstance =
        new(() => new ShowdownGen3PresetCatalog());

    private readonly IReadOnlyDictionary<string, ShowdownSpeciesData> _species;

    private ShowdownGen3PresetCatalog()
    {
        Dictionary<string, ShowdownSpeciesData> species =
            EmbeddedJson.Load<Dictionary<string, ShowdownSpeciesData>>(
                "gen3-showdown-randbats-sets.json");

        Validate(species);
        _species = species;
    }

    public static ShowdownGen3PresetCatalog Instance => LazyInstance.Value;

    public PokemonGeneration Generation => PokemonGeneration.Gen3;

    public string SourceName => "Pokémon Showdown Gen 3 Random Battle";

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
        const int expectedSpecies = 220;
        const int expectedSets = 393;

        if (species.Count != expectedSpecies || species.Sum(entry => entry.Value.Sets.Length) != expectedSets)
        {
            throw new InvalidOperationException(
                $"Unexpected Showdown Gen 3 preset dimensions; expected {expectedSpecies} species and {expectedSets} sets.");
        }

        foreach ((string key, ShowdownSpeciesData entry) in species)
        {
            if (key != ShowdownIdentifier.Normalize(key) || entry.Sets.Length == 0 ||
                entry.Sets.Any(set => string.IsNullOrWhiteSpace(set.Role) ||
                    set.MovePool.Length == 0 || set.Abilities.Length == 0))
            {
                throw new InvalidOperationException($"Invalid Showdown Gen 3 preset entry: {key}.");
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
