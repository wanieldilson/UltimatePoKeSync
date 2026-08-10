namespace UltimatePoKeSync.GameData;

/// <summary>Versioned, offline reference sets that recommendation profiles may use as priors.</summary>
public interface IReferencePresetCatalog
{
    string SourceName { get; }

    string SourceRevision { get; }

    IReadOnlyList<ReferencePreset> Find(string speciesName);
}

public sealed record ReferencePreset(
    string Role,
    IReadOnlyList<string> MovePool,
    IReadOnlyList<string> AbilityNames,
    IReadOnlyList<string> PreferredTypes);
