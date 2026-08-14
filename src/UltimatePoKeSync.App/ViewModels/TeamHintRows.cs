namespace UltimatePoKeSync.App.ViewModels;

/// <summary>One of the three alternative ways to improve the party.</summary>
public sealed record TeamHintPlanRow(
    string Label,
    string Summary,
    string ScoreText,
    string GapText,
    IReadOnlyList<TeamHintPokemonRow> Pokemon);

/// <summary>
/// One acquisition inside a plan. This type formats an analysis result for the view; it
/// carries no ranking policy of its own.
/// </summary>
public sealed record TeamHintPokemonRow(
    string SpeciesName,
    string EvolutionText,
    IReadOnlyList<TypeChip> TypeChips,
    string CatchLine,
    string LevelLine,
    string CoverageText,
    string Reason,
    string ReplacementText)
{
    public bool HasEvolution => EvolutionText.Length > 0;

    public bool HasCoverage => CoverageText.Length > 0;

    public bool IsReplacement => ReplacementText.Length > 0;

    public string Initials => SpeciesName.Length <= 3
        ? SpeciesName.ToUpperInvariant()
        : SpeciesName[..3].ToUpperInvariant();
}
