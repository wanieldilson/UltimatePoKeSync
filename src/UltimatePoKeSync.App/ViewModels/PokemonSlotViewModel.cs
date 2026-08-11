using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.Contracts;

namespace UltimatePoKeSync.App.ViewModels;

/// <summary>
/// One party member: the tile in the team strip, and everything the detail panel shows
/// once it is selected.
/// </summary>
public sealed partial class PokemonSlotViewModel : ObservableObject
{
    private readonly PokemonRecommendation? _recommendation;

    [ObservableProperty]
    private bool _showBuild;

    public PokemonSlotViewModel(
        PokemonSnapshot member,
        TeamAnalysis analysis,
        PokemonRecommendation? recommendation)
    {
        Member = member;
        _recommendation = recommendation;

        TypeText = member.IsDualType
            ? $"{member.PrimaryType} / {member.SecondaryType}"
            : member.PrimaryType.ToString();
        PrimaryBrush = TypePalette.Brush(member.PrimaryType);
        TileBrush = TypePalette.SoftBrush(member.PrimaryType);

        Stats =
        [
            new StatRow("HP", member.BaseStats.Hp, member.IndividualValues.Hp, member.EffortValues.Hp, member.CurrentStats.Hp),
            new StatRow("Attack", member.BaseStats.Attack, member.IndividualValues.Attack, member.EffortValues.Attack, member.CurrentStats.Attack),
            new StatRow("Defense", member.BaseStats.Defense, member.IndividualValues.Defense, member.EffortValues.Defense, member.CurrentStats.Defense),
            new StatRow("Sp. Attack", member.BaseStats.SpecialAttack, member.IndividualValues.SpecialAttack, member.EffortValues.SpecialAttack, member.CurrentStats.SpecialAttack),
            new StatRow("Sp. Defense", member.BaseStats.SpecialDefense, member.IndividualValues.SpecialDefense, member.EffortValues.SpecialDefense, member.CurrentStats.SpecialDefense),
            new StatRow("Speed", member.BaseStats.Speed, member.IndividualValues.Speed, member.EffortValues.Speed, member.CurrentStats.Speed),
        ];

        Moves =
        [
            .. member.Moves
                .Where(move => !move.IsEmpty)
                .Select(move => new MoveRow(
                    move.Name,
                    move.Type.ToString(),
                    $"{move.CurrentPp}/{move.MaxPp}",
                    TypePalette.Brush(move.Type))),
        ];

        (Weaknesses, Resistances) = BuildMatchups(member, analysis);

        RoleText = recommendation is null
            ? "—"
            : Humanise(recommendation.RoleAnalysis.Role);
        RoleReason = recommendation is null
            ? string.Empty
            : $"{recommendation.RoleAnalysis.PhysicalMoveCount} physical · "
                + $"{recommendation.RoleAnalysis.SpecialMoveCount} special · "
                + $"{recommendation.RoleAnalysis.UtilityMoveCount} utility moves";

        NatureText = recommendation is null
            ? "—"
            : string.Join(" or ", recommendation.Nature.PreferredNatures.Select(nature => nature.Name));
        NatureNote = recommendation is null
            ? string.Empty
            : recommendation.Nature.CurrentNatureIsPreferred
                ? $"{recommendation.Nature.CurrentNature.Name} already fits"
                : $"currently {recommendation.Nature.CurrentNature.Name}";

        EffortValueText = DescribeEffortValues(recommendation);
        ProjectedStatsText = recommendation?.EffortValues.ProjectedStats is StatBlock projected
            ? $"HP {projected.Hp} · Atk {projected.Attack} · Def {projected.Defense} · "
                + $"SpA {projected.SpecialAttack} · SpD {projected.SpecialDefense} · Spe {projected.Speed}"
            : string.Empty;

        BuildMoves = recommendation is null
            ? []
            :
            [
                .. recommendation.Build.Moves.Select((move, index) => new BuildMoveRow(
                    move.Move.Name,
                    move.Move.Type.ToString(),
                    index < recommendation.Build.Reasons.Count
                        ? StripName(recommendation.Build.Reasons[index], move.Move.Name)
                        : string.Empty,
                    TypePalette.Brush(move.Move.Type))),
            ];

        Alternatives = recommendation is null
            ? []
            : [.. recommendation.Build.Alternatives.Select(move => move.Move.Name)];

        Items = recommendation is null
            ? []
            : [.. recommendation.ItemCandidates.Select(item => $"{item.Name} — {Describe(item.Availability)}")];

        PresetText = recommendation?.MatchedPreset is null
            ? string.Empty
            : $"Matched reference role: {recommendation.MatchedPreset.Role}";
    }

    public PokemonSnapshot Member { get; }

    public int SlotIndex => Member.SlotIndex;

    public string SpeciesName => Member.SpeciesName;

    public string LevelText => $"Lv. {Member.Level}";

    public string NicknameText =>
        Member.Nickname.Equals(Member.SpeciesName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : Member.Nickname;

    public bool HasNickname => NicknameText.Length > 0;

    public string TypeText { get; }

    public IBrush PrimaryBrush { get; }

    public IBrush TileBrush { get; }

    public string Initials => Member.SpeciesName.Length <= 3
        ? Member.SpeciesName.ToUpperInvariant()
        : Member.SpeciesName[..3].ToUpperInvariant();

    public string AbilityText => Member.AbilityName;

    public string HeldItemText => Member.HeldItemName is "-" or "" ? "no held item" : Member.HeldItemName;

    public string NatureName => Member.NatureName;

    public bool IsShiny => Member.IsShiny;

    public IReadOnlyList<StatRow> Stats { get; }

    public IReadOnlyList<MoveRow> Moves { get; }

    public IReadOnlyList<TypeChip> Weaknesses { get; }

    public IReadOnlyList<TypeChip> Resistances { get; }

    public string RoleText { get; }

    public string RoleReason { get; }

    public string NatureText { get; }

    public string NatureNote { get; }

    public string EffortValueText { get; }

    public string ProjectedStatsText { get; }

    public bool HasProjectedStats => ProjectedStatsText.Length > 0;

    public IReadOnlyList<BuildMoveRow> BuildMoves { get; }

    public IReadOnlyList<string> Alternatives { get; }

    public bool HasAlternatives => Alternatives.Count > 0;

    public IReadOnlyList<string> Items { get; }

    public string PresetText { get; }

    public bool HasPreset => PresetText.Length > 0;

    public bool HasRecommendation => _recommendation is not null;

    [RelayCommand]
    private void ToggleBuild() => ShowBuild = !ShowBuild;

    private static (IReadOnlyList<TypeChip> Weak, IReadOnlyList<TypeChip> Resisted) BuildMatchups(
        PokemonSnapshot member,
        TeamAnalysis analysis)
    {
        var weak = new List<TypeChip>();
        var resisted = new List<TypeChip>();

        foreach (DefensiveTypeCoverage entry in analysis.DefensiveCoverage)
        {
            DefensiveMatchup? matchup = entry.Matchups
                .FirstOrDefault(candidate => candidate.Member.SlotIndex == member.SlotIndex);
            if (matchup is null || matchup.Multiplier == 1)
            {
                continue;
            }

            string label = "×" + matchup.Multiplier.ToString("0.##", CultureInfo.InvariantCulture);
            (matchup.Multiplier > 1 ? weak : resisted).Add(TypeChip.For(entry.AttackingType, label));
        }

        return (weak, resisted);
    }

    private static string DescribeEffortValues(PokemonRecommendation? recommendation)
    {
        if (recommendation is null)
        {
            return "—";
        }

        EvRecommendation effortValues = recommendation.EffortValues;
        if (effortValues.IsExactTarget && effortValues.TargetSpread is StatBlock spread)
        {
            string[] parts =
            [
                .. Enum.GetValues<Stat>()
                    .Where(stat => spread[stat] > 0)
                    .Select(stat => $"{spread[stat]} {Abbreviate(stat)}"),
            ];
            return parts.Length == 0 ? "no effort values" : string.Join(" / ", parts);
        }

        return effortValues.PriorityStats.Count == 0
            ? "no priority"
            : "train " + string.Join(", ", effortValues.PriorityStats.Select(Abbreviate));
    }

    private static string Abbreviate(Stat stat) => stat switch
    {
        Stat.Hp => "HP",
        Stat.Attack => "Atk",
        Stat.Defense => "Def",
        Stat.SpecialAttack => "SpA",
        Stat.SpecialDefense => "SpD",
        _ => "Spe",
    };

    private static string Describe(RecommendationAvailability availability) => availability switch
    {
        RecommendationAvailability.KnownAvailable => "already available",
        RecommendationAvailability.RequiresAvailabilityCheck => "check availability in this save",
        _ => "competitive reference",
    };

    private static string Humanise(PokemonRole role) => role switch
    {
        PokemonRole.PhysicalAttacker => "Physical attacker",
        PokemonRole.SpecialAttacker => "Special attacker",
        PokemonRole.MixedAttacker => "Mixed attacker",
        PokemonRole.PhysicalWall => "Physical wall",
        PokemonRole.SpecialWall => "Special wall",
        PokemonRole.MixedWall => "Mixed wall",
        _ => "Support",
    };

    /// <summary>The engine prefixes each reason with the move name; the UI already shows it.</summary>
    private static string StripName(string reason, string moveName) =>
        reason.StartsWith(moveName + ": ", StringComparison.Ordinal)
            ? reason[(moveName.Length + 2)..]
            : reason;
}
