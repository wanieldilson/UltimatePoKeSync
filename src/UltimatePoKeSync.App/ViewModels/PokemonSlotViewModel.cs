using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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

    /// <summary>
    /// The sprite from the player's own cartridge, once it has been fetched. Null until
    /// then, and null for good on a game we cannot read, which is why the coloured tile
    /// stays underneath rather than being replaced. See D-033.
    /// </summary>
    [ObservableProperty]
    private Bitmap? _sprite;

    public PokemonSlotViewModel(
        PokemonSnapshot member,
        TeamAnalysis analysis,
        PokemonRecommendation? recommendation)
    {
        Member = member;
        _recommendation = recommendation;

        TypeText = member.IsEgg
            ? "not hatched yet"
            : member.IsDualType
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

        (Weaknesses, Resistances, Immunities) = member.IsEgg
            ? ([], [], [])
            : BuildMatchups(member, analysis);

        TypeChips = member.IsEgg
            ? []
            :
        [
            TypeChip.For(member.PrimaryType, "primary type"),
            .. member.IsDualType
                ? new[] { TypeChip.For(member.SecondaryType, "secondary type") }
                : [],
        ];

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
                .. recommendation.Build.Slots.Select(slot => new BuildMoveRow(
                    slot.Move.Move.Name,
                    slot.Move.Move.Type.ToString(),
                    Humanise(slot.Role),
                    DescribeSource(slot.Move),
                    slot.Reason,
                    TypePalette.Brush(slot.Move.Move.Type))),
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

    /// <summary>
    /// An egg shows as an egg. Its species is in the bytes, and the game deliberately does
    /// not show it — telling the player what is inside would spoil the one thing hatching
    /// is for. See D-036.
    /// </summary>
    public string SpeciesName => IsEgg ? "Egg" : Member.SpeciesName;

    public bool IsEgg => Member.IsEgg;

    public bool CanBattle => Member.CanBattle;

    /// <summary>
    /// How much walking is left. For an egg the friendship byte is not friendship at all:
    /// it is the number of cycles still to run, one of which is taken off every 256 steps.
    /// It is the only progress an egg has, and it is what the issue behind D-036 asked for
    /// in place of stats.
    /// </summary>
    /// <remarks>
    /// Formatted invariantly, like the English sentence around it: on an Italian machine
    /// the current culture turns 2,560 into 2.560 in the middle of an English line.
    /// </remarks>
    public string EggProgressText => Member.Friendship == 0
        ? "Ready to hatch."
        : string.Create(
            CultureInfo.InvariantCulture,
            $"About {Member.Friendship * 256:N0} steps to go ({Member.Friendship} cycles).");

    public string LevelText => $"Lv. {Member.Level}";

    public string NicknameText =>
        IsEgg || Member.Nickname.Equals(Member.SpeciesName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : Member.Nickname;

    public bool HasNickname => NicknameText.Length > 0;

    public string TypeText { get; }

    /// <summary>The type line, the bars and the matchups are all meaningless for an egg.</summary>
    public bool ShowsBattleData => CanBattle;

    public IBrush PrimaryBrush { get; }

    public IBrush TileBrush { get; }

    public bool HasSprite => Sprite is not null;

    public string Initials => IsEgg
        ? "EGG"
        : Member.SpeciesName.Length <= 3
        ? Member.SpeciesName.ToUpperInvariant()
        : Member.SpeciesName[..3].ToUpperInvariant();

    public string AbilityText => Member.AbilityName;

    public string HeldItemText => Member.HeldItemName is "-" or "" ? "no held item" : Member.HeldItemName;

    public string NatureName => Member.NatureName;

    public bool IsShiny => Member.IsShiny;

    public string HpText => $"{Member.CurrentHp}/{Member.MaximumHp}";

    public double HpFraction => Member.HpFraction;

    /// <summary>
    /// The bar as two proportional columns rather than a ProgressBar. Fluent's has a
    /// minimum width of its own, wider than the team strip, and Avalonia does not clip
    /// children — so it drew straight through the side of the tile.
    /// </summary>
    public GridLength HpFilled => new(Member.HpFraction, GridUnitType.Star);

    public GridLength HpRemaining => new(1 - Member.HpFraction, GridUnitType.Star);

    /// <summary>
    /// Green, amber, red. The same thresholds the games use for the bar in battle, so the
    /// colour means what a player already expects it to mean.
    /// </summary>
    public IBrush HpBrush => Member.HpFraction switch
    {
        <= 0 => new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)),
        <= 0.20 => new SolidColorBrush(Color.FromRgb(0xC0, 0x3D, 0x2E)),
        <= 0.50 => new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27)),
        _ => new SolidColorBrush(Color.FromRgb(0x4E, 0x9A, 0x3F)),
    };

    public bool HasStatus => Member.Status != StatusCondition.None || Member.IsFainted;

    /// <summary>Three letters, as the games abbreviate them on the party screen.</summary>
    public string StatusText => Member.IsFainted
        ? "FNT"
        : Member.Status switch
        {
            StatusCondition.Sleep => "SLP",
            StatusCondition.Poison => "PSN",
            StatusCondition.BadPoison => "TOX",
            StatusCondition.Burn => "BRN",
            StatusCondition.Freeze => "FRZ",
            StatusCondition.Paralysis => "PAR",
            _ => string.Empty,
        };

    public IBrush StatusBrush => Member.IsFainted
        ? new SolidColorBrush(Color.FromRgb(0x5A, 0x4C, 0x45))
        : Member.Status switch
        {
            StatusCondition.Sleep => new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x7B)),
            StatusCondition.Poison or StatusCondition.BadPoison =>
                new SolidColorBrush(Color.FromRgb(0x92, 0x4A, 0x96)),
            StatusCondition.Burn => new SolidColorBrush(Color.FromRgb(0xD9, 0x60, 0x2E)),
            StatusCondition.Freeze => new SolidColorBrush(Color.FromRgb(0x59, 0x9F, 0xB0)),
            StatusCondition.Paralysis => new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27)),
            _ => new SolidColorBrush(Colors.Transparent),
        };

    public bool HasHeldItem => Member.HeldItemId > 0 && Member.HeldItemName != "-";

    public string FriendshipText => $"{Member.Friendship}/255";

    public IReadOnlyList<StatRow> Stats { get; }

    /// <summary>The Pokémon's own types, as chips for the detail header.</summary>
    public IReadOnlyList<TypeChip> TypeChips { get; }

    public IReadOnlyList<MoveRow> Moves { get; }

    public IReadOnlyList<TypeChip> Weaknesses { get; }

    public IReadOnlyList<TypeChip> Resistances { get; }

    /// <summary>
    /// Kept apart from the resistances. Taking a quarter of the damage and taking none of
    /// it are different plans: one is a Pokémon that survives the hit, the other is a
    /// Pokémon the attack cannot touch at all, which is what a free switch-in is made of.
    /// </summary>
    public IReadOnlyList<TypeChip> Immunities { get; }

    public bool HasWeaknesses => Weaknesses.Count > 0;

    public bool HasResistances => Resistances.Count > 0;

    public bool HasImmunities => Immunities.Count > 0;

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

    partial void OnSpriteChanged(Bitmap? value) => OnPropertyChanged(nameof(HasSprite));

    private static (IReadOnlyList<TypeChip> Weak,
        IReadOnlyList<TypeChip> Resisted,
        IReadOnlyList<TypeChip> Immune) BuildMatchups(
        PokemonSnapshot member,
        TeamAnalysis analysis)
    {
        var weak = new List<TypeChip>();
        var resisted = new List<TypeChip>();
        var immune = new List<TypeChip>();

        foreach (DefensiveTypeCoverage entry in analysis.DefensiveCoverage)
        {
            DefensiveMatchup? matchup = entry.Matchups
                .FirstOrDefault(candidate => candidate.Member.SlotIndex == member.SlotIndex);
            if (matchup is null || matchup.Multiplier == 1)
            {
                continue;
            }

            if (matchup.Multiplier == 0)
            {
                immune.Add(TypeChip.For(entry.AttackingType, "no damage at all"));
                continue;
            }

            string label = "×" + matchup.Multiplier.ToString("0.##", CultureInfo.InvariantCulture);
            (matchup.Multiplier > 1 ? weak : resisted).Add(TypeChip.For(entry.AttackingType, label));
        }

        return (weak, resisted, immune);
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

    /// <summary>How the Pokémon comes by the move, in the words the game uses.</summary>
    private static string DescribeSource(MoveRecommendation move) => move.Source switch
    {
        MoveCandidateSource.CurrentMoveset => "already knows it",
        MoveCandidateSource.LevelUpLearnset => move.LearnedAtLevel is int level
            ? $"learns it at level {level}"
            : "learns it by levelling",
        MoveCandidateSource.Machine => "from a TM or HM",
        MoveCandidateSource.Tutor => "from a move tutor",
        _ => "from a common set",
    };

    private static string Humanise(BuildSlotRole role) => role switch
    {
        BuildSlotRole.SameType => "Same type",
        BuildSlotRole.Coverage => "Coverage",
        BuildSlotRole.TeamSupport => "Team support",
        BuildSlotRole.Utility => "Utility",
        _ => "Filler",
    };
}
