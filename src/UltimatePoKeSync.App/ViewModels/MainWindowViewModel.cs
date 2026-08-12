using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls;
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
        PokemonRecommendationEngine.CreateDefault(PKHeXSources.Learnsets);

    /// <summary>
    /// What the next few levels bring. Cheap enough to run for every member on every
    /// snapshot: it reads the same tables the engine already loaded. See D-037.
    /// </summary>
    private readonly PokemonProgressAnalyzer _progressAnalyzer = new(
        PKHeXSources.Learnsets,
        PKHeXSources.Evolutions);

    private readonly RomSpriteSource? _sprites;

    /// <summary>The player's own sprite folder, when they have one. See D-045.</summary>
    private SpritePackSource _pack = new();

    private readonly Lazy<HttpClient> _http = new(() => new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30),
    });

    /// <summary>Stops the running animations when the window closes.</summary>
    private readonly CancellationTokenSource _animations = new();

    private PartySnapshot? _party;

    [ObservableProperty]
    private bool _isConnected;

    /// <summary>Which of the six screens is showing. See D-047.</summary>
    [ObservableProperty]
    private DashboardTab _selectedTab = DashboardTab.Pokemon;

    [ObservableProperty]
    private bool _isDownloadingSprites;

    /// <summary>0 to 1 while sprites are arriving, for the bar.</summary>
    [ObservableProperty]
    private double _spriteProgress;

    [ObservableProperty]
    private string _spriteStatus = string.Empty;

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
        DsSetupSteps = SetupGuide.DsSteps();
        ScriptPath = SetupGuide.ScriptPath;
        PlatformName = SetupGuide.PlatformName;
        PortHelp = SetupGuide.PortHelp(live.Port);
        RevealButtonText = SetupGuide.RevealButtonText;
        IsTranslocated = SetupGuide.IsTranslocated;
        TranslocationWarning = SetupGuide.TranslocationWarning;

        _sprites = live.MemoryReader is null ? null : new RomSpriteSource(live.MemoryReader);

        // The profile is a choice about how the app should advise, not about one session,
        // so it outlives the run that made it. See D-038.
        IsCompetitive = AppSettings.Load().CompetitiveProfile;

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

    /// <summary>
    /// Kept apart from the resisted list, as it already is in the per-Pokémon view. Taking a
    /// quarter of a hit and taking none of it are different plans, and until now the two
    /// views of the same fact disagreed with each other. See D-038.
    /// </summary>
    public ObservableCollection<TypeChip> TeamImmune { get; } = [];

    public ObservableCollection<TypeChip> TeamSuperEffective { get; } = [];

    public ObservableCollection<TypeChip> TeamUnanswered { get; } = [];

    public ObservableCollection<StrengthRow> StrengthFactors { get; } = [];

    public IReadOnlyList<string> SetupSteps { get; }

    /// <summary>The other emulator's steps, for the DS games.</summary>
    public IReadOnlyList<string> DsSetupSteps { get; }

    /// <summary>Which emulator is feeding the window, once one is.</summary>
    public string ConnectedVia => _live.ActiveEmulator ?? string.Empty;

    /// <summary>"N / 6" beside the rail's heading.</summary>
    public string PartyCountText => $"{Slots.Count} / 6";

    /// <summary>Out of what. Held rather than assumed, so a rule change shows up here.</summary>
    public int StrengthMaximum { get; private set; } = 100;

    public string StrengthMaximumText => $"/ {StrengthMaximum}";

    /// <summary>The three factors costing the most, which is what the rail prints.</summary>
    public ObservableCollection<StrengthRow> WeakestRailFactors { get; } = [];

    public GridLength StrengthFilled =>
        new(Math.Clamp(StrengthScore, 0, StrengthMaximum), GridUnitType.Star);

    public GridLength StrengthRemaining =>
        new(Math.Clamp(StrengthMaximum - StrengthScore, 0, StrengthMaximum), GridUnitType.Star);

    public bool IsPokemonTab => SelectedTab == DashboardTab.Pokemon;

    /// <summary>
    /// The screens still waiting to be rebuilt keep showing the previous dashboard, so the
    /// app stays usable between one screen and the next.
    /// </summary>
    public bool ShowsOldPanels => HasTeam && !IsPokemonTab && !IsStatsTab && !IsBuildTab
        && !IsLearnsetTab;

    public bool IsStatsTab => SelectedTab == DashboardTab.Stats;

    public bool IsBuildTab => SelectedTab == DashboardTab.Build;

    public bool IsLearnsetTab => SelectedTab == DashboardTab.Learnset;

    public bool IsTeamTab => SelectedTab == DashboardTab.Team;

    public bool IsBridgeTab => SelectedTab == DashboardTab.Bridge;

    /// <summary>
    /// The line under the app name: which game, and which emulator it came through. Both
    /// are read from the cartridge rather than configured, so this is also the proof that
    /// the bridge found something real.
    /// </summary>
    public string IdentityLine
    {
        get
        {
            if (_party is null)
            {
                return "NO GAME YET";
            }

            string via = ConnectedVia.Length > 0 ? $" · {ConnectedVia.ToUpperInvariant()}" : string.Empty;
            return $"{_party.Game.Title.ToUpperInvariant()} · {_party.Game.GameCode}{via}";
        }
    }

    /// <summary>
    /// Whether the sprite folder has anything in it. Everything works without it, so this
    /// only decides whether to offer the download. See D-046.
    /// </summary>
    public bool HasSprites => _pack.Exists;

    public bool CanOfferSprites => !HasSprites && !IsDownloadingSprites;

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

    /// <summary>
    /// One line instead of five cards. The free slots are worth stating — a party of one is
    /// a party of one — but they were taking as much of the strip as the Pokémon in it.
    /// See D-038.
    /// </summary>
    public string EmptySlotsText => EmptySlots.Count == 1
        ? "1 SLOT FREE"
        : $"{EmptySlots.Count} SLOTS FREE";

    public bool HasSuperEffective => TeamSuperEffective.Count > 0;

    /// <summary>A team with no immunity at all should not be shown an empty heading.</summary>
    public bool HasTeamImmunities => TeamImmune.Count > 0;

    public bool HasUnanswered => TeamUnanswered.Count > 0;

    public bool ShowTeamPanel => SelectedSlot is null;

    /// <summary>
    /// The Pokémon screen always has a subject. Keeping the explicit selection nullable
    /// preserves the existing whole-team state while the first rail card supplies the
    /// default detail view.
    /// </summary>
    public PokemonSlotViewModel? CurrentSlot => SelectedSlot ?? Slots.FirstOrDefault();

    public bool HasAnalysisError => AnalysisError.Length > 0;

    public void Start() => _live.Start();

    public async ValueTask DisposeAsync()
    {
        // The animations outlive nothing: a timer still swapping frames into a closed
        // window is a leak with a picture attached.
        await _animations.CancelAsync().ConfigureAwait(false);
        _animations.Dispose();

        await _live.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Returns to the whole-team view. Selection itself is the list box's job.</summary>
    [RelayCommand]
    private void ClearSelection() => SelectedSlot = null;

    /// <summary>Switches screen. The rail and the header do not move. See D-047.</summary>
    [RelayCommand]
    private void ShowTab(DashboardTab tab) => SelectedTab = tab;

    /// <summary>
    /// Picking a card selects that member and shows the Pokémon screen, because that is
    /// what somebody clicking a Pokémon is asking for.
    /// </summary>
    [RelayCommand]
    private void SelectSlot(PokemonSlotViewModel slot)
    {
        SelectedSlot = slot;
        SelectedTab = DashboardTab.Pokemon;
    }

    partial void OnSelectedTabChanged(DashboardTab value)
    {
        foreach (string name in new[]
                 {
                     nameof(IsPokemonTab), nameof(IsStatsTab), nameof(IsBuildTab),
                     nameof(IsLearnsetTab), nameof(IsTeamTab), nameof(IsBridgeTab),
                     nameof(ShowsOldPanels),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>
    /// Opens the file manager on the script. Typing that path into mGBA's dialog by hand
    /// is the one step most likely to go wrong.
    /// </summary>
    [RelayCommand]
    private static void RevealScript() => SetupGuide.RevealScript();

    /// <summary>
    /// Fetches the sprite folder. The app's only network call, made because somebody pressed
    /// this, and nothing waits on it: without sprites the team shows coloured tiles and
    /// everything else is identical. See D-046.
    /// </summary>
    [RelayCommand]
    private async Task DownloadSprites()
    {
        if (IsDownloadingSprites)
        {
            return;
        }

        IsDownloadingSprites = true;
        SpriteProgress = 0;
        SpriteStatus = "Starting…";
        RaiseSpriteState();

        var progress = new Progress<SpriteDownloader.Progress>(step => _post(() =>
        {
            SpriteProgress = step.Fraction;
            SpriteStatus = $"{step.Done} of {step.Total}";
        }));

        try
        {
            var downloader = new SpriteDownloader(_http.Value);
            SpriteDownloader.Result result = await downloader
                .DownloadAsync(progress, _animations.Token)
                .ConfigureAwait(false);

            _post(() =>
            {
                // A fresh source, because the old one cached the absence of every sprite
                // it was asked for before the folder existed.
                _pack = new SpritePackSource();

                SpriteStatus = result.AnyArrived
                    ? $"{result.Fetched} sprites ready"
                    : "Could not reach the sprite archive. The team still works without it.";

                IsDownloadingSprites = false;
                RaiseSpriteState();

                // Draw the team that is already on screen, rather than waiting for it to
                // change on its own.
                if (_party is not null)
                {
                    _ = LoadSpritesAsync(_party.Game, [.. Slots]);
                }
            });
        }
        catch (OperationCanceledException)
        {
            _post(() =>
            {
                IsDownloadingSprites = false;
                SpriteStatus = string.Empty;
                RaiseSpriteState();
            });
        }
    }

    private void RaiseSpriteState()
    {
        OnPropertyChanged(nameof(HasSprites));
        OnPropertyChanged(nameof(CanOfferSprites));
    }

    partial void OnSelectedSlotChanged(PokemonSlotViewModel? value)
    {
        OnPropertyChanged(nameof(ShowTeamPanel));
        OnPropertyChanged(nameof(CurrentSlot));

        // The rail badge follows the selection, so each card has to be told.
        foreach (PokemonSlotViewModel slot in Slots)
        {
            slot.IsSelected = ReferenceEquals(slot, CurrentSlot);
        }
    }

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
        OnPropertyChanged(nameof(ConnectedVia));
        IsConnected = state == EmulatorConnectionState.Streaming;
        ConnectionText = state switch
        {
            EmulatorConnectionState.Connecting => "Looking for mGBA and melonDS…",
            // The name is only known once a party has arrived, so the plain word has to
            // stand on its own until then rather than trailing a "via " with nothing after it.
            EmulatorConnectionState.Streaming => ConnectedVia.Length > 0
                ? $"Connected via {ConnectedVia}"
                : "Connected",
            EmulatorConnectionState.Reconnecting => "Connection lost, retrying…",
            EmulatorConnectionState.Faulted => "Stopped after an unrecoverable error",
            _ => "Waiting for an emulator…",
        };
    }

    private void OnPartyChanged(PartySnapshot party)
    {
        _party = party;
        HasReceivedParty = true;
        GameText = $"{party.Game.Title} [{party.Game.GameCode}]";
        OnPropertyChanged(nameof(IdentityLine));

        TeamAnalysis analysis;
        TeamStrength strength;

        try
        {
            analysis = _teamAnalyzer.Analyze(party);
            strength = _strengthAnalyzer.Evaluate(analysis);
        }
        catch (NotSupportedException ex)
        {
            // A game the analysis layer does not cover must not blank the party display.
            AnalysisError = ex.Message;
            ShowPartyWithoutAnalysis(party);
            return;
        }

        // Recommendations are attempted separately, and their absence costs only
        // themselves. A generation with no reference sets — Gen 5 today — used to take the
        // whole analysis down with it: the window showed a team with no matchups and a
        // strength of zero, while the console showed 32 out of 100 for the same party.
        TeamRecommendation? recommendation = null;
        string missing = string.Empty;

        if (!party.IsEmpty)
        {
            try
            {
                recommendation = _engine.Recommend(
                    party,
                    IsCompetitive
                        ? RecommendationProfileKind.Competitive
                        : RecommendationProfileKind.Playthrough);
            }
            catch (NotSupportedException ex)
            {
                missing = $"{ex.Message} Type coverage and team strength are still shown.";
            }
        }

        AnalysisError = missing;

        int selectedIndex = SelectedSlot?.SlotIndex ?? -1;

        Slots.Clear();
        foreach (PokemonSnapshot member in party.Members)
        {
            Slots.Add(new PokemonSlotViewModel(
                member,
                analysis,
                recommendation?.Members.FirstOrDefault(
                    entry => entry.Member.SlotIndex == member.SlotIndex),
                _progressAnalyzer.Analyze(party.Game, member)));
        }

        UpdateEmptySlots(party.Count);
        OnPropertyChanged(nameof(HasTeam));

        // The rail heading counts the cards, and the cards are rebuilt on every snapshot,
        // so the count has to be re-read rather than left at whatever it was at startup.
        OnPropertyChanged(nameof(PartyCountText));
        OnPropertyChanged(nameof(HasEmptyParty));
        UpdateCoverage(analysis);
        UpdateStrength(strength);
        SelectedSlot = Slots.FirstOrDefault(slot => slot.SlotIndex == selectedIndex);
        OnPropertyChanged(nameof(CurrentSlot));
        foreach (PokemonSlotViewModel slot in Slots)
        {
            slot.IsSelected = ReferenceEquals(slot, CurrentSlot);
        }

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
        TeamImmune.Clear();
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
        // The player's own sprite folder first, when they have one: it covers every
        // generation in one style, and it is the only thing that can draw a DS game at all,
        // whose ROM is not mapped into memory. See D-045.
        foreach (PokemonSlotViewModel slot in slots)
        {
            // Drawing the species inside an egg would give away the one thing the game
            // keeps from the player until it hatches. See D-036.
            if (slot.IsEgg)
            {
                continue;
            }

            if (_pack.Find(slot.Member.SpeciesId, slot.Member.IsShiny) is AnimatedSprite animated)
            {
                _post(() => slot.Play(animated, _post, _animations.Token));
            }

            foreach (EvolutionStageRow stage in slot.EvolutionStages.Where(stage => !stage.IsCurrent))
            {
                if (_pack.Find(stage.SpeciesId, shiny: false) is AnimatedSprite evolutionSprite)
                {
                    Bitmap firstFrame = SpriteImage.From(evolutionSprite, evolutionSprite.Frames[0]);
                    _post(() => stage.Sprite = firstFrame);
                }
            }
        }

        // The cartridge is the fallback, and only a GBA one can be read this way (D-033).
        // On melonDS the channel moves 15 KB a second (D-039), so hunting for tables that
        // are not there would spend half a minute finding nothing.
        if (_sprites is null || game.Generation != PokemonGeneration.Gen3)
        {
            return;
        }

        foreach (PokemonSlotViewModel slot in slots)
        {
            if (slot.IsEgg)
            {
                continue;
            }

            if (!slot.HasSprite)
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


            foreach (EvolutionStageRow stage in slot.EvolutionStages
                .Where(stage => !stage.IsCurrent && !stage.HasSprite))
            {
                try
                {
                    DecodedSprite? evolution = await _sprites
                        .TryGetAsync(game, stage.SpeciesId)
                        .ConfigureAwait(false);
                    if (evolution is not null)
                    {
                        _post(() => stage.Sprite = SpriteImage.From(evolution));
                    }
                }
                catch (Exception)
                {
                    break;
                }
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
        OnPropertyChanged(nameof(EmptySlotsText));
    }

    /// <summary>
    /// A weakness reads differently depending on whether anything can come in for it, and
    /// an immunity is worth naming separately from a resistance even here.
    /// </summary>
    private static string DescribeWeakness(DefensiveTypeCoverage entry)
    {
        if (entry.IsGap)
        {
            return $"{entry.WeakCount} weak, nothing resists";
        }

        string safe = (entry.ImmuneCount, entry.ResistantCount) switch
        {
            (0, int resist) => $"{resist} resist",
            (int immune, 0) => $"{immune} immune",
            (int immune, int resist) => $"{immune} immune, {resist} resist",
        };

        return $"{entry.WeakCount} weak, {safe}";
    }

    private static string Plural(int count, string singular, string plural) =>
        count == 1 ? $"1 {singular}" : $"{count} {plural}";

    private void UpdateCoverage(TeamAnalysis analysis)
    {
        TeamWeaknesses.Clear();
        TeamNeutral.Clear();
        TeamResisted.Clear();
        TeamImmune.Clear();
        TeamSuperEffective.Clear();
        TeamUnanswered.Clear();

        foreach (DefensiveTypeCoverage entry in analysis.DefensiveCoverage)
        {
            if (entry.WeakCount > 0)
            {
                TeamWeaknesses.Add(TypeChip.For(entry.AttackingType, DescribeWeakness(entry)));
            }
            else if (entry.ImmuneCount > 0)
            {
                // An immunity is the better switch-in, so the type is filed under it even
                // when others merely resist. The detail still counts both.
                string detail = entry.ResistantCount > 0
                    ? $"{entry.ImmuneCount} take nothing, {entry.ResistantCount} resist"
                    : Plural(entry.ImmuneCount, "takes", "take") + " nothing";
                TeamImmune.Add(TypeChip.For(entry.AttackingType, detail));
            }
            else if (entry.ResistantCount > 0)
            {
                TeamResisted.Add(TypeChip.For(
                    entry.AttackingType,
                    Plural(entry.ResistantCount, "resists", "resist")));
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
        OnPropertyChanged(nameof(HasTeamImmunities));
    }

    private void UpdateStrength(TeamStrength strength)
    {
        StrengthScore = strength.Score;
        StrengthMaximum = strength.MaximumScore;
        StrengthFactors.Clear();
        WeakestRailFactors.Clear();

        foreach (TeamStrengthFactor factor in strength.WeakestFactors.Take(3))
        {
            WeakestRailFactors.Add(new StrengthRow(
                Humanise(factor.Kind), factor.Points, factor.MaximumPoints, factor.Explanation));
        }

        foreach (string name in new[]
                 {
                     nameof(StrengthMaximum), nameof(StrengthMaximumText),
                     nameof(StrengthFilled), nameof(StrengthRemaining),
                 })
        {
            OnPropertyChanged(name);
        }


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
