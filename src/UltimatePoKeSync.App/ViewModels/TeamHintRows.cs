using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

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
/// <remarks>
/// A class rather than a record because the picture arrives late: sprites come off the
/// player's disk on a background pass, exactly as the evolution line's do, so the row has to
/// be able to say it changed. See D-045.
/// </remarks>
public sealed partial class TeamHintPokemonRow : ObservableObject
{
    public TeamHintPokemonRow(
        int speciesId,
        string speciesName,
        string evolutionText,
        IReadOnlyList<TypeChip> typeChips,
        string catchLine,
        string levelLine,
        string coverageText,
        string reason,
        string replacementText)
    {
        SpeciesId = speciesId;
        SpeciesName = speciesName;
        EvolutionText = evolutionText;
        TypeChips = typeChips;
        CatchLine = catchLine;
        LevelLine = levelLine;
        CoverageText = coverageText;
        Reason = reason;
        ReplacementText = replacementText;
    }

    /// <summary>The species actually caught, which is what the sprite has to show: a plan
    /// may mention a Watchog, but Patrat is what is waiting in the grass.</summary>
    public int SpeciesId { get; }

    public string SpeciesName { get; }

    public string EvolutionText { get; }

    public IReadOnlyList<TypeChip> TypeChips { get; }

    public string CatchLine { get; }

    public string LevelLine { get; }

    public string CoverageText { get; }

    public string Reason { get; }

    public string ReplacementText { get; }

    public bool HasEvolution => EvolutionText.Length > 0;

    public bool HasCoverage => CoverageText.Length > 0;

    public bool IsReplacement => ReplacementText.Length > 0;

    /// <summary>Shown until the sprite arrives, and for anyone without a sprite folder.</summary>
    public string Initials => SpeciesName.Length <= 3
        ? SpeciesName.ToUpperInvariant()
        : SpeciesName[..3].ToUpperInvariant();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSprite))]
    private Bitmap? _sprite;

    public bool HasSprite => Sprite is not null;
}
