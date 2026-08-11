using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.App.Services;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Learnsets;
using UltimatePoKeSync.GameData.Sprites;

namespace UltimatePoKeSync.App.ViewModels;

/// <summary>
/// The dashboard. Holds the live party, the team facts and the current selection.
/// </summary>
/// <remarks>
/// It formats and selects; it never decides. Coverage, roles, strength and builds all come
/// from the analysis layer, so the window shows what the CLI shows. See D-028.
/// </remarks>
public sealed partial class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly ILiveTeamSource _live;
    private readonly Action<Action> _post;
    private readonly TeamAnalyzer _teamAnalyzer = new();
    private readonly TeamStrengthAnalyzer _strengthAnalyzer = new();
    private readonly PokemonRecommendationEngine _engine =
        PokemonRecommendationEngine.CreateDefault(PKHeXGen3MoveLearnSource.Instance);

    private readonly RomSpriteSource? _sprites;

    private PartySnapshot? _party;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionText = "Waiting for mGBA…";

    [ObservableProperty]
    private string _gameText = string.Empty;

    [ObservableProperty]
    private PokemonSlotViewModel? _selectedSlot;

    [ObservableProperty]
    private bool _isCompetitive;

    [ObservableProperty]
    private int _strengthScore;

    [ObservableProperty]
    private string _strengthHeadline = string.Empty;

    [ObservableProperty]
    private string _analysisError = string.Empty;

    /// <summary>
    /// Whether a party has ever arrived, empty or not. Without this an empty party looks
    /// exactly like a bridge that is not talking: the setup steps come back and the header
    /// says Connected, and the two contradict each other. See issue #16.
    /// </summary>
    [ObservableProperty]
    private bool _hasReceivedParty;

    public MainWindowViewModel()
        : this(new LiveTeamService(), action => Dispatcher.UIThread.Post(action))
    {
    }

    /// <param name="post">
    /// How to get back onto the UI thread. Injected so a test can run the pipeline
    /// synchronously without an Avalonia application behind it.
    /// </param>
    public MainWindowViewModel(ILiveTeamSource live, Action<Action> post)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(post);

        _live = live;
        _post = post;

        SetupSteps = SetupGuide.Steps(live.Port);
        ScriptPath = SetupGuide.ScriptPath;
        PlatformName = SetupGuide.PlatformName;
        PortHelp = SetupGuide.PortHelp(live.Port);
        RevealButtonText = SetupGuide.RevealButtonText;
        IsTranslocated = SetupGuide.IsTranslocated;
        TranslocationWarning = SetupGuide.TranslocationWarning;

        _sprites = live.MemoryReader is null ? null : new RomSpriteSource(live.MemoryReader);

        _live.StateChanged += (_, state) => _post(() => OnStateChanged(state));
        _live.PartyChanged += (_, party) => _post(() => OnPartyChanged(party));
    }

    public ObservableCollection<PokemonSlotViewModel> Slots { get; } = [];

    /// <summary>
    /// The slots the party has not filled, rendered as placeholders. Six greyed boxes say
    /// "you are carrying two Pokémon" faster than a number does, and party size is one of
    /// the things the strength score counts.
    /// </summary>
    public ObservableCollection<int> EmptySlots { get; } = [];

    public ObservableCollection<TypeChip> TeamWeaknesses { get; } = [];

    public ObservableCollection<TypeChip> TeamNeutral { get; } = [];

    public ObservableCollection<TypeChip> TeamResisted { get; } = [];

    public ObservableCollection<TypeChip> TeamSuperEffective { get; } = [];

    public ObservableCollection<TypeChip> TeamUnanswered { get; } = [];

    public ObservableCollection<StrengthRow> StrengthFactors { get; } = [];

    public IReadOnlyList<string> SetupSteps { get; }

    public string ScriptPath { get; }

    public string PlatformName { get; }

    public string PortHelp { get; }

    public string RevealButtonText { get; }

    /// <summary>macOS is running us from a randomised throwaway copy. See D-029.</summary>
    public bool IsTranslocated { get; }

    public string TranslocationWarning { get; }

    public string ProfileText => IsCompetitive ? "Competitive" : "Playthrough";


    /// <summary>Green once bytes are actually arriving, amber while they are not.</summary>
    public IBrush ConnectionBrush => IsConnected
        ? new SolidColorBrush(Color.FromRgb(0x4E, 0x9A, 0x3F))
        : new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27));

    public bool HasTeam => Slots.Count > 0;

    /// <summary>Connected and read, and there is genuinely nothing in the party.</summary>
    public bool HasEmptyParty => HasReceivedParty && Slots.Count == 0;

    /// <summary>The setup steps belong on screen only until the bridge answers.</summary>
    public bool ShowSetup => !HasReceivedParty;

    public bool HasEmptySlots => EmptySlots.Count > 0;

    public bool HasSuperEffective => TeamSuperEffective.Count > 0;

    public bool HasUnanswered => TeamUnanswered.Count > 0;

    public bool ShowTeamPanel => SelectedSlot is null;

    public bool HasAnalysisError => AnalysisError.Length > 0;

    public void Start() => _live.Start();

    public ValueTask DisposeAsync() => _live.DisposeAsync();

    /// <summary>Returns to the whole-team view. Selection itself is the list box's job.</summary>
    [RelayCommand]
    private void ClearSelection() => SelectedSlot = null;

    /// <summary>
    /// Opens the file manager on the script. Typing that path into mGBA's dialog by hand
    /// is the one step most likely to go wrong.
    /// </summary>
    [RelayCommand]
    private static void RevealScript() => SetupGuide.RevealScript();

    partial void OnSelectedSlotChanged(PokemonSlotViewModel? value) =>
        OnPropertyChanged(nameof(ShowTeamPanel));

    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectionBrush));

    [RelayCommand]
    private void ChooseProfile(string? profile) =>
        IsCompetitive = string.Equals(profile, "competitive", StringComparison.OrdinalIgnoreCase);

    partial void OnIsCompetitiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ProfileText));
        if (_party is not null)
        {
            OnPartyChanged(_party);
        }
    }

    partial void OnAnalysisErrorChanged(string value) => OnPropertyChanged(nameof(HasAnalysisError));

    partial void OnHasReceivedPartyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSetup));
        OnPropertyChanged(nameof(HasEmptyParty));
    }

    private void OnStateChanged(EmulatorConnectionState state)
    {
        IsConnected = state == EmulatorConnectionState.Streaming;
        ConnectionText = state switch
        {
            EmulatorConnectionState.Connecting => "Looking for mGBA…",
            EmulatorConnectionState.Streaming => "Connected",
            EmulatorConnectionState.Reconnecting => "Connection lost, retrying…",
            EmulatorConnectionState.Faulted => "Stopped after an unrecoverable error",
            _ => "Waiting for mGBA…",
        };
    }

    private void OnPartyChanged(PartySnapshot party)
    {
        _party = party;
        HasReceivedParty = true;
        GameText = $"{party.Game.Title} [{party.Game.GameCode}]";

        TeamAnalysis analysis;
        TeamStrength strength;
        TeamRecommendation? recommendation = null;

        try
        {
            analysis = _teamAnalyzer.Analyze(party);
            strength = _strengthAnalyzer.Evaluate(analysis);

            if (!party.IsEmpty)
            {
                recommendation = _engine.Recommend(
                    party,
                    IsCompetitive
                        ? RecommendationProfileKind.Competitive
                        : RecommendationProfileKind.Playthrough);
            }

            AnalysisError = string.Empty;
        }
        catch (NotSupportedException ex)
        {
            // A game the analysis layer does not cover must not blank the party display.
            AnalysisError = ex.Message;
            ShowPartyWithoutAnalysis(party);
            return;
        }

        int selectedIndex = SelectedSlot?.SlotIndex ?? -1;

        Slots.Clear();
        foreach (PokemonSnapshot member in party.Members)
        {
            Slots.Add(new PokemonSlotViewModel(
                member,
                analysis,
                recommendation?.Members.FirstOrDefault(
                    entry => entry.Member.SlotIndex == member.SlotIndex)));
        }

        UpdateEmptySlots(party.Count);
        OnPropertyChanged(nameof(HasTeam));
        OnPropertyChanged(nameof(HasEmptyParty));
        UpdateCoverage(analysis);
        UpdateStrength(strength);
        SelectedSlot = Slots.FirstOrDefault(slot => slot.SlotIndex == selectedIndex);

        // Sprites arrive later and are worth waiting for, not worth waiting on: the party
        // is on screen already, and each tile swaps its coloured box for the real thing as
        // the bytes come back.
        _ = LoadSpritesAsync(party.Game, [.. Slots]);
    }

    private void ShowPartyWithoutAnalysis(PartySnapshot party)
    {
        var empty = new TeamAnalysis(party, [], []);

        Slots.Clear();
        foreach (PokemonSnapshot member in party.Members)
        {
            Slots.Add(new PokemonSlotViewModel(member, empty, null));
        }

        TeamWeaknesses.Clear();
        TeamNeutral.Clear();
        TeamResisted.Clear();
        TeamSuperEffective.Clear();
        TeamUnanswered.Clear();
        StrengthFactors.Clear();

        UpdateEmptySlots(party.Count);
        OnPropertyChanged(nameof(HasTeam));
        OnPropertyChanged(nameof(HasEmptyParty));
        SelectedSlot = null;
    }

    private async Task LoadSpritesAsync(
        GameIdentity game,
        IReadOnlyList<PokemonSlotViewModel> slots)
    {
        if (_sprites is null)
        {
            return;
        }

        foreach (PokemonSlotViewModel slot in slots)
        {
            DecodedSprite? decoded;
            try
            {
                decoded = await _sprites.TryGetAsync(game, slot.Member.SpeciesId).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A sprite is decoration. Losing one must never take the party down with it.
                return;
            }

            if (decoded is not null)
            {
                _post(() => slot.Sprite = SpriteImage.From(decoded));
            }
        }
    }

    private void UpdateEmptySlots(int filled)
    {
        const int fullParty = 6;

        EmptySlots.Clear();
        for (int slot = filled; slot < fullParty; slot++)
        {
            EmptySlots.Add(slot);
        }

        OnPropertyChanged(nameof(HasEmptySlots));
    }

    private void UpdateCoverage(TeamAnalysis analysis)
    {
        TeamWeaknesses.Clear();
        TeamNeutral.Clear();
        TeamResisted.Clear();
        TeamSuperEffective.Clear();
        TeamUnanswered.Clear();

        foreach (DefensiveTypeCoverage entry in analysis.DefensiveCoverage)
        {
            if (entry.WeakCount > 0)
            {
                string detail = entry.IsGap
                    ? $"{entry.WeakCount} weak, nothing resists"
                    : $"{entry.WeakCount} weak, {entry.ResistantCount + entry.ImmuneCount} safe";
                TeamWeaknesses.Add(TypeChip.For(entry.AttackingType, detail));
            }
            else if (entry.HasDefensiveAnswer)
            {
                TeamResisted.Add(TypeChip.For(
                    entry.AttackingType,
                    $"{entry.ResistantCount + entry.ImmuneCount} resist or are immune"));
            }
            else
            {
                TeamNeutral.Add(TypeChip.For(entry.AttackingType, "no weakness, no resistance"));
            }
        }

        foreach (OffensiveTypeCoverage entry in analysis.OffensiveCoverage)
        {
            if (entry.IsCovered)
            {
                OffensiveAnswer best = entry.Answers.MaxBy(answer => answer.Multiplier)!;
                TeamSuperEffective.Add(TypeChip.For(
                    entry.DefendingType,
                    $"{best.Move.Name} ×{best.Multiplier.ToString("0.##", CultureInfo.InvariantCulture)}"));
            }
            else
            {
                TeamUnanswered.Add(TypeChip.For(entry.DefendingType, "no super-effective move"));
            }
        }

        OnPropertyChanged(nameof(HasSuperEffective));
        OnPropertyChanged(nameof(HasUnanswered));
    }

    private void UpdateStrength(TeamStrength strength)
    {
        StrengthScore = strength.Score;
        StrengthFactors.Clear();

        foreach (TeamStrengthFactor factor in strength.Factors)
        {
            StrengthFactors.Add(new StrengthRow(
                Humanise(factor.Kind),
                factor.Points,
                factor.MaximumPoints,
                factor.Explanation));
        }

        // Name the area and what it costs. Repeating the factor's own explanation here
        // would print the same sentence twice, once above the list and once inside it.
        TeamStrengthFactor? worst = strength.WeakestFactors.FirstOrDefault();
        StrengthHeadline = worst is null
            ? "Nothing is holding this party back."
            : $"{Humanise(worst.Kind)} is costing the most: "
                + $"{worst.MaximumPoints - worst.Points} of {worst.MaximumPoints} points.";
    }

    private static string Humanise(TeamStrengthKind kind) => kind switch
    {
        TeamStrengthKind.PartySize => "Party size",
        TeamStrengthKind.LevelCohesion => "Level cohesion",
        TeamStrengthKind.DefensiveCoverage => "Defensive coverage",
        TeamStrengthKind.OffensiveCoverage => "Offensive coverage",
        TeamStrengthKind.NatureFit => "Nature fit",
        _ => "Effort value fit",
    };
}
