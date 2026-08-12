using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.App.Services;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;

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
        PokemonRecommendation? recommendation,
        PokemonProgress? progress = null)
    {
        Member = member;
        _recommendation = recommendation;
        progress ??= PokemonProgress.Nothing;

        // The rules of the game this Pokémon came from: what makes a move physical, and how
        // hard it hits. Null for a generation the app cannot judge, in which case the move
        // line says less rather than saying something invented.
        IGenerationRules? rules = GenerationRulesResolver.Default.Resolve(
            analysis.Party.Game.Generation);

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

        string emptyMoveHint = progress.Moves.FirstOrDefault() is UpcomingMove nextMove
            ? $"{nextMove.Move.Name} is {DescribeDistance(nextMove.LevelsAway)}"
            : "ready for the next move";

        MoveSlots =
        [
            .. member.Moves.Select(move => move.IsEmpty
                ? new MoveRow(
                    "empty slot",
                    string.Empty,
                    string.Empty,
                    emptyMoveHint,
                    TypePalette.Brush(PokemonType.None),
                    true)
                : new MoveRow(
                    move.Name,
                    move.Type.ToString(),
                    $"{move.CurrentPp}/{move.MaxPp}",
                    DescribeMove(move, rules),
                    TypePalette.Brush(move.Type))),
        ];

        Moves = [.. MoveSlots.Where(move => !move.IsEmpty)];

        (Weaknesses, Resistances, Immunities) = member.IsEgg
            ? ([], [], [])
            : BuildMatchups(member, analysis);
        MatchupNote = member.IsEgg ? string.Empty : BuildMatchupNote(member, analysis);

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

        // The whole pool, not only the four that won. Which moves were on the table is
        // how a player judges whether the four are right, and until now only the console
        // showed it. See D-038.
        HashSet<int> chosen = recommendation is null
            ? []
            : [.. recommendation.Build.Slots.Select(slot => slot.Move.Move.MoveId)];

        Candidates = recommendation is null
            ? []
            :
            [
                .. recommendation.MoveCandidates.Select(move => new CandidateMoveRow(
                    move.Move.Name,
                    move.Move.Type.ToString(),
                    DescribeSource(move),
                    Describe(move.Availability),
                    chosen.Contains(move.Move.MoveId),
                    TypePalette.Brush(move.Move.Type))),
            ];

        Items = recommendation is null
            ? []
            : [.. recommendation.ItemCandidates.Select(item => $"{item.Name} — {Describe(item.Availability)}")];

        PresetText = recommendation?.MatchedPreset is null
            ? string.Empty
            : $"Matched reference role: {recommendation.MatchedPreset.Role}";

        UpcomingMoves =
        [
            .. progress.Moves.Select(move => new UpcomingMoveRow(
                move.Move.Name,
                move.Move.Type.ToString(),
                $"Lv.{move.Level}",
                move.LevelsAway == 1 ? "next level" : $"in {move.LevelsAway} levels",
                TypePalette.Brush(move.Move.Type))),
        ];

        (EvolutionText, EvolutionNote) = DescribeEvolution(progress, member.Level);
        EvolutionSpeciesName = progress.NextEvolution?.IntoSpeciesName ?? string.Empty;
        EvolutionCountdown = progress.NextEvolution?.Level is int evolutionLevel
            ? DescribeDistance(Math.Max(1, evolutionLevel - member.Level))
            : progress.NextEvolution?.Requirement ?? string.Empty;
        OtherEvolutionsText = progress.OtherEvolutions.Count == 0
            ? string.Empty
            : "Or " + string.Join(
                ", ",
                progress.OtherEvolutions.Select(step => $"{step.IntoSpeciesName} {step.Requirement}"))
                + ".";
        HasProgress = progress.HasAnything;
    }

    public PokemonSnapshot Member { get; }

    public int SlotIndex => Member.SlotIndex;

    /// <summary>What the badge on the corner shows: slots are counted from one.</summary>
    public int SlotNumber => Member.SlotIndex + 1;

    /// <summary>Yellow for the selected member, bone white for the rest.</summary>
    public IBrush SlotBadgeBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F))
        : new SolidColorBrush(Color.FromRgb(0xE8, 0xE3, 0xF5));

    /// <summary>Set by the window when the selection changes, so the badge can follow it.</summary>
    public bool IsSelected
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SlotBadgeBrush));
        }
    }

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

    public string LevelText => $"Lv.{Member.Level}";

    public string NicknameText =>
        IsEgg || Member.Nickname.Equals(Member.SpeciesName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : Member.Nickname;

    public bool HasNickname => NicknameText.Length > 0;

    public string TypeText { get; }

    /// <summary>The type line, the bars and the matchups are all meaningless for an egg.</summary>
    public bool ShowsBattleData => CanBattle;

    /// <summary>Fainted cards stay readable, but visibly step back from battlers.</summary>
    public double CardOpacity => Member.IsFainted ? 0.60 : 1;

    /// <summary>Where it sits and which species it is: "SLOT 1 · #495".</summary>
    public string SlotAndDexText => $"SLOT {SlotNumber} · #{Member.SpeciesId}";

    /// <summary>
    /// The personality value, the closest thing a Pokémon has to a serial number: two Snivy
    /// identical in everything else still differ here.
    /// </summary>
    public string PidText => $"PID 0x{Member.PersonalityValue:X8}";

    public string HeldItemOrNone => Member.HeldItemId == 0 ? "none" : Member.HeldItemName;

    public string UpperSpeciesName => SpeciesName.ToUpperInvariant();

    /// <summary>"LV 6", the tilted badge beside the name.</summary>
    public string LevelBadge => $"LV {Member.Level}";

    /// <summary>The four facts under the HP bar.</summary>
    public IReadOnlyList<FactChip> Facts =>
    [
        new("Nature", Member.NatureName),
        new("Ability", Member.AbilityName),
        new("Item", HeldItemOrNone),
        new("Friendship", Member.Friendship.ToString(CultureInfo.InvariantCulture)),
    ];

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
        < 0.20 => new SolidColorBrush(Color.FromRgb(0xFF, 0x5B, 0x4A)),
        <= 0.50 => new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F)),
        _ => new SolidColorBrush(Color.FromRgb(0x7F, 0xF0, 0x9A)),
    };

    public bool HasStatus => Member.Status != StatusCondition.None || Member.IsFainted;

    public bool HasRailBadge => IsEgg || HasStatus;

    public string RailBadgeText => IsEgg ? "EGG" : StatusText;

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
        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x5B, 0x4A))
        : Member.Status switch
        {
            StatusCondition.Sleep => new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x7B)),
            StatusCondition.Poison or StatusCondition.BadPoison =>
                new SolidColorBrush(Color.FromRgb(0xB0, 0x6B, 0xE0)),
            StatusCondition.Burn => new SolidColorBrush(Color.FromRgb(0xE0, 0x6A, 0x2E)),
            StatusCondition.Freeze => new SolidColorBrush(Color.FromRgb(0x5D, 0xB3, 0xC4)),
            StatusCondition.Paralysis => new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x3F)),
            _ => new SolidColorBrush(Colors.Transparent),
        };

    public IBrush RailBadgeBrush => IsEgg
        ? new SolidColorBrush(Color.FromRgb(0x45, 0xD0, 0xE0))
        : StatusBrush;

    public bool HasHeldItem => Member.HeldItemId > 0 && Member.HeldItemName != "-";

    public string FriendshipText => $"{Member.Friendship}/255";

    public IReadOnlyList<StatRow> Stats { get; }

    /// <summary>The Pokémon's own types, as chips for the detail header.</summary>
    public IReadOnlyList<TypeChip> TypeChips { get; }

    public IReadOnlyList<MoveRow> Moves { get; }

    /// <summary>All four cartridge slots, including the empty ones the design teaches from.</summary>
    public IReadOnlyList<MoveRow> MoveSlots { get; }

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

    public string MatchupNote { get; }

    public bool HasMatchupNote => MatchupNote.Length > 0;

    public string RoleText { get; }

    public string RoleReason { get; }

    public string NatureText { get; }

    public string NatureNote { get; }

    public string EffortValueText { get; }

    public string ProjectedStatsText { get; }

    public bool HasProjectedStats => ProjectedStatsText.Length > 0;

    public IReadOnlyList<BuildMoveRow> BuildMoves { get; }

    /// <summary>Every move that was on the table, with the four that made it marked.</summary>
    public IReadOnlyList<CandidateMoveRow> Candidates { get; }

    public bool HasCandidates => Candidates.Count > 0;

    public string CandidatesHeading => $"All {Candidates.Count} candidates";

    public IReadOnlyList<string> Items { get; }

    public string PresetText { get; }

    public bool HasPreset => PresetText.Length > 0;

    public bool HasRecommendation => _recommendation is not null;

    /// <summary>The next few levels, which is the question a player mid-story is asking.</summary>
    public IReadOnlyList<UpcomingMoveRow> UpcomingMoves { get; }

    public bool HasUpcomingMoves => UpcomingMoves.Count > 0;

    public string EvolutionText { get; }

    public bool HasEvolution => EvolutionText.Length > 0;

    public string EvolutionSpeciesName { get; }

    public string EvolutionCountdown { get; }

    /// <summary>The caveat under the evolution line, when there is one worth making.</summary>
    public string EvolutionNote { get; }

    public bool HasEvolutionNote => EvolutionNote.Length > 0;

    public string OtherEvolutionsText { get; }

    public bool HasOtherEvolutions => OtherEvolutionsText.Length > 0;

    public bool HasProgress { get; }

    [RelayCommand]
    private void ToggleBuild() => ShowBuild = !ShowBuild;

    /// <summary>
    /// The evolution line and its caveat. The countdown is the useful half — "at Lv.16"
    /// means nothing to someone who does not remember what level their Treecko is.
    /// </summary>
    private static (string Line, string Note) DescribeEvolution(PokemonProgress progress, int level)
    {
        if (progress.NextEvolution is not EvolutionStep step)
        {
            return (string.Empty, string.Empty);
        }

        if (progress.EvolvesOnNextLevelUp)
        {
            return (
                $"Becomes {step.IntoSpeciesName} on the next level up.",
                $"It is already past Lv.{step.Level}, so the game will offer it again every "
                    + "level until it is accepted.");
        }

        if (step.Level is int at)
        {
            int away = at - level;
            string line = $"Becomes {step.IntoSpeciesName} at Lv.{at}"
                + (away == 1 ? ", one level away." : $", {away} levels away.");

            return (
                line,
                progress.MovesStopAtEvolution
                    ? "Moves past that level are not listed: from then on it follows "
                        + $"{step.IntoSpeciesName}'s learnset, not this one."
                    : string.Empty);
        }

        return ($"Becomes {step.IntoSpeciesName} {step.Requirement}.", string.Empty);
    }

    partial void OnSpriteChanged(Bitmap? value) => OnPropertyChanged(nameof(HasSprite));

    /// <summary>
    /// Plays an animated sprite, one frame at a time, at the speeds the file itself
    /// declares. A single-frame image simply becomes the sprite and no timer is started —
    /// six idle timers for six still pictures would be six timers too many. See D-045.
    /// </summary>
    public void Play(AnimatedSprite sprite, Action<Action> post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(post);

        Bitmap[] frames = [.. sprite.Frames.Select(frame => SpriteImage.From(sprite, frame))];
        Sprite = frames[0];

        if (frames.Length == 1)
        {
            return;
        }

        _ = Task.Run(
            async () =>
            {
                int index = 0;
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(sprite.Frames[index].Duration, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    index = (index + 1) % frames.Length;
                    Bitmap frame = frames[index];
                    post(() => Sprite = frame);
                }
            },
            cancellationToken);
    }

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

    private static string BuildMatchupNote(PokemonSnapshot member, TeamAnalysis analysis)
    {
        DefensiveTypeCoverage? worst = analysis.DefensiveCoverage
            .Where(entry => entry.Matchups.Any(matchup =>
                matchup.Member.SlotIndex == member.SlotIndex && matchup.Multiplier > 1))
            .OrderByDescending(entry => entry.IsGap)
            .ThenByDescending(entry => entry.WeakCount)
            .FirstOrDefault();

        if (worst is null)
        {
            return "No doubled weakness needs a switch plan.";
        }

        string type = worst.AttackingType.ToString();
        string[] answers =
        [
            .. worst.Matchups
                .Where(matchup => matchup.Member.SlotIndex != member.SlotIndex
                    && matchup.Multiplier < 1)
                .Select(matchup => matchup.Member.SpeciesName),
        ];

        if (answers.Length == 0)
        {
            return $"Team gap. Nothing else in the party resists {type}.";
        }

        return $"Switch plan. {string.Join(" or ", answers)} can take the {type} hit instead.";
    }

    private static string DescribeDistance(int levelsAway) => levelsAway == 1
        ? "1 level away"
        : $"{levelsAway} levels away";

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

    /// <summary>
    /// "Grass · special · 20 pow". Category comes from the generation's rules because from
    /// Gen 4 it belongs to the move rather than to its type (D-041); power is omitted for a
    /// status move, which has none, rather than printed as a zero.
    /// </summary>
    private static string DescribeMove(MoveSlot move, IGenerationRules? rules)
    {
        if (rules is null)
        {
            return move.Type.ToString();
        }

        MoveCategory category = rules.GetMoveCategory(move.MoveId, move.Type);
        int power = rules.GetMoveBasePower(move.MoveId);

        string kind = category switch
        {
            MoveCategory.Physical => "physical",
            MoveCategory.Special => "special",
            _ => "status",
        };

        return category == MoveCategory.Status || power <= 0
            ? $"{move.Type} · {kind}"
            : $"{move.Type} · {kind} · {power} pow";
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
