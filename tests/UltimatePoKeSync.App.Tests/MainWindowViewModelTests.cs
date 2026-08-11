using System.Text.Json;
using UltimatePoKeSync.App.Services;
using UltimatePoKeSync.App.ViewModels;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.Parsing;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// Drives the dashboard with the Italian Emerald capture instead of a live emulator.
/// </summary>
public sealed class MainWindowViewModelTests
{
    [Fact]
    public void BeforeAnythingArrives_TheSetupScreenIsWhatIsShown()
    {
        (MainWindowViewModel viewModel, _) = Create();

        Assert.False(viewModel.HasTeam);
        Assert.False(viewModel.IsConnected);
        Assert.NotEmpty(viewModel.SetupSteps);
        Assert.EndsWith("ups_bridge.lua", viewModel.ScriptPath, StringComparison.Ordinal);
    }

    /// <summary>
    /// The path handed to the user must survive being opened from Downloads on macOS,
    /// where the app itself runs from a randomised throwaway directory. See D-029.
    /// </summary>
    [Fact]
    public void TheScriptPath_IsAStableUserFolder_NotWhereverTheAppHappensToRun()
    {
        (MainWindowViewModel viewModel, _) = Create();

        Assert.True(Path.IsPathRooted(viewModel.ScriptPath));
        Assert.DoesNotContain("AppTranslocation", viewModel.ScriptPath, StringComparison.Ordinal);
        Assert.DoesNotContain(AppContext.BaseDirectory, viewModel.ScriptPath, StringComparison.Ordinal);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            viewModel.ScriptPath,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionState_ReachesTheHeader()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseState(EmulatorConnectionState.Streaming);
        Assert.True(viewModel.IsConnected);
        Assert.Equal("Connected", viewModel.ConnectionText);

        source.RaiseState(EmulatorConnectionState.Reconnecting);
        Assert.False(viewModel.IsConnected);
        Assert.Contains("retrying", viewModel.ConnectionText, StringComparison.Ordinal);
    }

    [Fact]
    public void APartyFromRealRam_FillsTheTeamPanel()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadRealParty());

        Assert.True(viewModel.HasTeam);
        Assert.True(viewModel.ShowTeamPanel);
        Assert.Equal("TREECKO", Assert.Single(viewModel.Slots).SpeciesName);
        Assert.Contains("BPEI", viewModel.GameText, StringComparison.Ordinal);

        // 17 attacking types must be accounted for exactly once across the three buckets.
        Assert.Equal(
            17,
            viewModel.TeamWeaknesses.Count + viewModel.TeamNeutral.Count + viewModel.TeamResisted.Count);
        Assert.Equal(17, viewModel.TeamSuperEffective.Count + viewModel.TeamUnanswered.Count);

        Assert.Equal(6, viewModel.StrengthFactors.Count);
        Assert.InRange(viewModel.StrengthScore, 1, 100);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.StrengthHeadline));
        Assert.Empty(viewModel.AnalysisError);
    }

    [Fact]
    public void TheStripShowsTheLiveConditionAndTheSlotsStillToFill()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadRealParty());

        PokemonSlotViewModel slot = viewModel.Slots[0];
        Assert.Equal("19/19", slot.HpText);
        Assert.Equal(1, slot.HpFraction);
        Assert.False(slot.HasStatus);
        Assert.False(slot.HasHeldItem);

        // One Pokémon carried, five boxes still to fill.
        Assert.True(viewModel.HasEmptySlots);
        Assert.Equal(5, viewModel.EmptySlots.Count);
    }

    /// <summary>
    /// From the byte the game writes all the way to the three letters on the tile.
    /// </summary>
    [Fact]
    public void APoisonedPokemonReachesTheStripAsPsn()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadRealParty(statusByte: 0b0000_1000));

        PokemonSlotViewModel slot = viewModel.Slots[0];
        Assert.True(slot.HasStatus);
        Assert.Equal("PSN", slot.StatusText);
    }

    [Fact]
    public void SelectingASlot_SwapsToTheDetailPanelAndBackAgain()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();
        source.RaiseParty(LoadRealParty());

        PokemonSlotViewModel slot = viewModel.Slots[0];
        viewModel.SelectedSlot = slot;

        Assert.False(viewModel.ShowTeamPanel);
        Assert.Equal(6, slot.Stats.Count);
        Assert.Equal(["Pound", "Leer"], slot.Moves.Select(move => move.Name));
        Assert.Contains(slot.Weaknesses, chip => chip.Type == "Fire");
        Assert.True(slot.HasRecommendation);

        viewModel.ClearSelectionCommand.Execute(null);
        Assert.True(viewModel.ShowTeamPanel);
    }

    [Fact]
    public void TheBestSetIsHiddenUntilItIsAskedFor()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();
        source.RaiseParty(LoadRealParty());

        PokemonSlotViewModel slot = viewModel.Slots[0];

        Assert.False(slot.ShowBuild);
        slot.ToggleBuildCommand.Execute(null);
        Assert.True(slot.ShowBuild);
        Assert.NotEmpty(slot.BuildMoves);
        Assert.All(slot.BuildMoves, move => Assert.False(string.IsNullOrWhiteSpace(move.Reason)));
    }

    [Fact]
    public void SwitchingProfile_RecomputesWithoutANewSnapshot()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();
        source.RaiseParty(LoadRealParty());

        Assert.Equal("Playthrough", viewModel.ProfileText);
        string playthroughEffortValues = viewModel.Slots[0].EffortValueText;

        viewModel.IsCompetitive = true;

        Assert.Equal("Competitive", viewModel.ProfileText);
        Assert.NotEqual(playthroughEffortValues, viewModel.Slots[0].EffortValueText);
        Assert.True(viewModel.Slots[0].HasProjectedStats);
    }

    [Fact]
    public void AGameTheAnalysisCannotServe_StillShowsTheParty()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();
        PartySnapshot party = LoadRealParty();

        source.RaiseParty(party with
        {
            Game = party.Game with { GameCode = "AXVJ", Generation = PokemonGeneration.Gen4 },
        });

        Assert.True(viewModel.HasTeam);
        Assert.True(viewModel.HasAnalysisError);
        Assert.Empty(viewModel.StrengthFactors);
    }

    private static (MainWindowViewModel ViewModel, FakeSource Source) Create()
    {
        var source = new FakeSource();
        return (new MainWindowViewModel(source, action => action()), source);
    }

    private static PartySnapshot LoadRealParty(int? statusByte = null)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "emerald-it-treecko.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        var game = new GameIdentity(
            root.GetProperty("gameCode").GetString()!,
            root.GetProperty("title").GetString()!,
            root.GetProperty("revision").GetInt32(),
            (PokemonGeneration)root.GetProperty("generation").GetInt32());

        byte[] data = Convert.FromBase64String(root.GetProperty("data").GetString()!);
        if (statusByte is int status)
        {
            // Offset 80: the first of the twenty battle-stat bytes in a party slot.
            data[80] = (byte)status;
        }

        var raw = new RawPartySnapshot(
            game,
            root.GetProperty("partyCount").GetInt32(),
            data,
            root.GetProperty("slotSize").GetInt32(),
            DateTimeOffset.UnixEpoch,
            1);

        return new Gen3PartyParser().Parse(raw);
    }

    private sealed class FakeSource : ILiveTeamSource
    {
        public event EventHandler<EmulatorConnectionState>? StateChanged;

        public event EventHandler<PartySnapshot>? PartyChanged;

        public int Port => 8888;

        public IEmulatorMemoryReader? MemoryReader => null;

        public bool Started { get; private set; }

        public void Start() => Started = true;

        public void RaiseState(EmulatorConnectionState state) => StateChanged?.Invoke(this, state);

        public void RaiseParty(PartySnapshot party) => PartyChanged?.Invoke(this, party);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
