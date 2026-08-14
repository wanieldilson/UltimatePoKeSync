using System.Text.Json;
using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.App.Services;
using UltimatePoKeSync.App.ViewModels;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using UltimatePoKeSync.Parsing;
using Xunit;

namespace UltimatePoKeSync.App.Tests;

/// <summary>
/// The dashboard driven by the real Gen 5 capture, and specifically by what Gen 5 does not
/// have. See D-042.
/// </summary>
/// <remarks>
/// Written after the window showed a Snivy with no matchups and a strength of zero while the
/// console showed 32 out of 100 for the same party. Recommendations and analysis shared one
/// try block, so a generation with no reference sets took the whole analysis down with it.
/// </remarks>
public sealed class Gen5DashboardTests
{
    [Fact]
    public void AGenerationWithoutRecommendationsStillGetsItsAnalysis()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadBlack());

        // The team arrived.
        PokemonSlotViewModel snivy = Assert.Single(viewModel.Slots);
        Assert.Equal("Snivy", snivy.SpeciesName);

        // And so did everything that does not need reference sets.
        Assert.True(viewModel.StrengthScore > 0);
        Assert.Equal(6, viewModel.StrengthFactors.Count);
        Assert.Equal(
            17,
            viewModel.TeamWeaknesses.Count + viewModel.TeamNeutral.Count
                + viewModel.TeamResisted.Count + viewModel.TeamImmune.Count);
        Assert.NotEmpty(viewModel.TeamWeaknesses);
        Assert.NotEmpty(snivy.Weaknesses);
    }

    /// <summary>
    /// Gen 5 has reference sets of its own now, so the window offers a build for it and
    /// reports nothing missing. This test used to assert the opposite, which was true for
    /// about an hour; what it guards has not changed: that the window and the analysis
    /// agree about what exists.
    /// </summary>
    [Fact]
    public void AGenFivePokemonGetsABuildOfItsOwn()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadBlack());

        PokemonSlotViewModel snivy = viewModel.Slots[0];
        Assert.True(snivy.HasRecommendation);
        Assert.False(viewModel.HasAnalysisError);

        snivy.ToggleBuildCommand.Execute(null);
        Assert.NotEmpty(snivy.BuildMoves);
        Assert.All(snivy.BuildMoves, move => Assert.NotEqual("?", move.Name));
        Assert.DoesNotContain(
            snivy.BuildMoves,
            move => move.Name is "Tackle" or "Leer" or "Wrap");

        // The candidates come from the Gen 5 tables: Energy Ball is a move Gen 3 does not
        // have, so seeing it proves the right catalog answered even after the visible pool
        // is capped to the strongest alternatives.
        Assert.Contains(snivy.Candidates, candidate => candidate.Name == "Energy Ball");
    }

    [Fact]
    public void HiddenPowerVariantsAreNotAllMarkedAsChosen()
    {
        PartySnapshot party = LoadBlack();
        TeamRecommendation team = PokemonRecommendationEngine
            .CreateDefault(PKHeXSources.All)
            .Recommend(party, RecommendationProfileKind.Competitive);
        PokemonRecommendation recommendation = Assert.Single(team.Members);

        MoveReference baseHiddenPower = ShowdownGen5MoveCatalog.Instance.Find("hiddenpower")!;
        MoveRecommendation[] candidates =
        [
            .. recommendation.MoveCandidates.Where(
                move => move.Move.ReferenceId != baseHiddenPower.ReferenceId),
            new(
                baseHiddenPower,
                MoveCandidateSource.Machine,
                RecommendationAvailability.CompetitiveReference,
                null),
        ];

        var snivy = new PokemonSlotViewModel(
            recommendation.Member,
            team.TeamAnalysis,
            recommendation with { MoveCandidates = candidates });

        CandidateMoveRow typed = Assert.Single(
            snivy.Candidates,
            candidate => candidate.Name == "Hidden Power Fire");
        CandidateMoveRow untypedRow = Assert.Single(
            snivy.Candidates,
            candidate => candidate.Name == "Hidden Power");

        Assert.True(typed.Chosen);
        Assert.False(untypedRow.Chosen);
    }

    /// <summary>
    /// What the next levels bring, in the window rather than the console. Snivy becomes
    /// Servine at 17, which is one level later than the Gen 3 starters, a difference that
    /// only shows if the Gen 5 tables are the ones answering. See D-043.
    /// </summary>
    [Fact]
    public void TheGenFiveEvolutionIsCountedDownTo()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadBlack());

        PokemonSlotViewModel snivy = viewModel.Slots[0];
        Assert.True(snivy.HasProgress);
        Assert.Equal("Becomes Servine at Lv.17, 11 levels away.", snivy.EvolutionText);
        Assert.Contains(snivy.UpcomingMoves, move => move.Name == "Vine Whip");
    }

    /// <summary>
    /// Once an emulator has answered, the header names it: with two being watched, "connected"
    /// alone leaves the player guessing which one the app found.
    /// </summary>
    [Fact]
    public void TheHeaderNamesTheEmulatorThatAnswered()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseState(EmulatorConnectionState.Streaming);

        Assert.Equal("Connected via melonDS", viewModel.ConnectionText);
    }

    [Fact]
    public void BlackGetsManualStrictTeamHintsWhenLiveProgressCannotBeRead()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadBlack());

        Assert.True(viewModel.TeamHintsSupported);
        Assert.False(viewModel.UseAutomaticTeamHintProgress);
        Assert.Equal("route-1", viewModel.SelectedTeamHintMilestone?.Id);
        Assert.NotEmpty(viewModel.TeamHintPlans);
        Assert.Contains(
            viewModel.TeamHintPlans.SelectMany(plan => plan.Pokemon),
            pokemon => pokemon.SpeciesName is "Patrat" or "Lillipup");
        Assert.DoesNotContain(
            viewModel.TeamHintPlans.SelectMany(plan => plan.Pokemon),
            pokemon => pokemon.SpeciesName is "Deino" or "Hydreigon");
        Assert.DoesNotContain(
            viewModel.TeamHintPlans.SelectMany(plan => plan.Pokemon),
            pokemon => pokemon.CatchLine.Contains("Gift", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AManualRouteTwoCheckpointUnlocksOnlyCurrentAndEarlierEncounters()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();
        source.RaiseParty(LoadBlack());

        viewModel.SelectedTeamHintMilestone = Assert.Single(
            viewModel.TeamHintMilestones,
            milestone => milestone.Id == "route-2");

        TeamHintPokemonRow[] suggestions =
        [
            .. viewModel.TeamHintPlans.SelectMany(plan => plan.Pokemon),
        ];
        Assert.Equal(3, viewModel.TeamHintPlans.Count);
        Assert.Contains(suggestions, pokemon => pokemon.SpeciesName == "Purrloin");
        Assert.DoesNotContain(
            suggestions,
            pokemon => pokemon.SpeciesName is "Pidove" or "Deino" or "Hydreigon");
        Assert.Contains("Dreamyard", viewModel.TeamHintSoonText, StringComparison.Ordinal);
    }

    [Fact]
    public void TeamHintsStayUnavailableForAnUnmappedGame()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadEmerald());

        Assert.False(viewModel.TeamHintsSupported);
        Assert.Empty(viewModel.TeamHintMilestones);
        Assert.Empty(viewModel.TeamHintPlans);
        Assert.False(viewModel.HasTeamHintSoon);
    }

    [Fact]
    public async Task ItalianBlackUsesAConservativeBadgeCheckpointFromLiveMemory()
    {
        const uint partyHead = 0x022348AC;
        var memory = new FakeMemory();
        memory.Put(IrbiStoryProgressReader.PartyPointerAddress, BitConverter.GetBytes(partyHead));
        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0b0001_1111]);
        memory.Put(partyHead + IrbiStoryProgressReader.MapIdOffset, BitConverter.GetBytes(391));
        var source = new FakeSource(memory);
        var viewModel = new MainWindowViewModel(source, action => action());

        source.RaiseParty(LoadBlack());
        await WaitUntilAsync(() => viewModel.TeamHintProgressText.StartsWith(
            "Detected 5 badges",
            StringComparison.Ordinal));

        Assert.True(viewModel.UseAutomaticTeamHintProgress);
        Assert.Equal("route-6", viewModel.SelectedTeamHintMilestone?.Id);
        Assert.Contains("Live map 391", viewModel.TeamHintStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AutomaticProgressRefreshesAfterAnotherPartySnapshot()
    {
        const uint partyHead = 0x022348AC;
        var memory = new FakeMemory();
        memory.Put(IrbiStoryProgressReader.PartyPointerAddress, BitConverter.GetBytes(partyHead));
        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0b0000_0001]);
        memory.Put(partyHead + IrbiStoryProgressReader.MapIdOffset, BitConverter.GetBytes(32));
        var source = new FakeSource(memory);
        var viewModel = new MainWindowViewModel(source, action => action());

        PartySnapshot first = LoadBlack();
        source.RaiseParty(first);
        await WaitUntilAsync(() => viewModel.SelectedTeamHintMilestone?.Id == "dreamyard");

        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0b0001_1111]);
        source.RaiseParty(first with { Sequence = first.Sequence + 1 });
        await WaitUntilAsync(() => viewModel.SelectedTeamHintMilestone?.Id == "route-6");

        Assert.StartsWith("Detected 5 badges", viewModel.TeamHintProgressText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturningToTeamHintsRefreshesProgressWithoutAPartyChange()
    {
        const uint partyHead = 0x022348AC;
        var memory = new FakeMemory();
        memory.Put(IrbiStoryProgressReader.PartyPointerAddress, BitConverter.GetBytes(partyHead));
        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0b0000_0001]);
        memory.Put(partyHead + IrbiStoryProgressReader.MapIdOffset, BitConverter.GetBytes(32));
        var source = new FakeSource(memory);
        var viewModel = new MainWindowViewModel(source, action => action());

        source.RaiseParty(LoadBlack());
        await WaitUntilAsync(() => viewModel.SelectedTeamHintMilestone?.Id == "dreamyard");

        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0b0001_1111]);
        viewModel.SelectedTab = DashboardTab.Team;
        viewModel.SelectedTab = DashboardTab.TeamHints;
        await WaitUntilAsync(() => viewModel.SelectedTeamHintMilestone?.Id == "route-6");

        Assert.StartsWith("Detected 5 badges", viewModel.TeamHintProgressText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANewAutomaticReadHidesOldLateGamePlansUntilProgressIsKnown()
    {
        const uint partyHead = 0x022348AC;
        var memory = new FakeMemory();
        memory.Put(IrbiStoryProgressReader.PartyPointerAddress, BitConverter.GetBytes(partyHead));
        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0xFF]);
        memory.Put(partyHead + IrbiStoryProgressReader.MapIdOffset, BitConverter.GetBytes(23));
        var source = new FakeSource(memory);
        var viewModel = new MainWindowViewModel(source, action => action());
        PartySnapshot party = LoadBlack();

        source.RaiseParty(party);
        await WaitUntilAsync(() => viewModel.SelectedTeamHintMilestone?.Id == "route-10");
        Assert.NotEmpty(viewModel.TeamHintPlans);

        memory.Put(partyHead + IrbiStoryProgressReader.BadgeMaskOffset, [0b0000_0001]);
        memory.ReadGate = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        source.RaiseParty(party with { Sequence = party.Sequence + 1 });

        Assert.StartsWith("Reading story progress", viewModel.TeamHintProgressText, StringComparison.Ordinal);
        Assert.Empty(viewModel.TeamHintPlans);
        Assert.False(viewModel.HasTeamHintSoon);

        memory.ReadGate.SetResult(true);
        await WaitUntilAsync(() => viewModel.SelectedTeamHintMilestone?.Id == "dreamyard");
    }

    /// <summary>A Gen 3 party must not start reporting a missing-data notice of its own.</summary>
    [Fact]
    public void TheGenerationThatHasEverythingSaysNothingIsMissing()
    {
        (MainWindowViewModel viewModel, FakeSource source) = Create();

        source.RaiseParty(LoadEmerald());

        Assert.True(viewModel.Slots[0].HasRecommendation);
        Assert.False(viewModel.HasAnalysisError);
        Assert.Empty(viewModel.AnalysisError);
    }

    private static (MainWindowViewModel, FakeSource) Create()
    {
        var source = new FakeSource();
        return (new MainWindowViewModel(source, action => action()), source);
    }

    private static PartySnapshot LoadBlack() => Load("black-it-snivy.json");

    private static PartySnapshot LoadEmerald() => Load("emerald-it-treecko.json");

    private static PartySnapshot Load(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        var game = new GameIdentity(
            root.GetProperty("gameCode").GetString()!,
            root.GetProperty("title").GetString()!,
            root.GetProperty("revision").GetInt32(),
            (PokemonGeneration)root.GetProperty("generation").GetInt32());

        var raw = new RawPartySnapshot(
            game,
            root.GetProperty("partyCount").GetInt32(),
            Convert.FromBase64String(root.GetProperty("data").GetString()!),
            root.GetProperty("slotSize").GetInt32(),
            DateTimeOffset.UnixEpoch,
            1);

        return PartyParserResolver.CreateDefault().Resolve(game)!.Parse(raw);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.True(condition());
    }

    private sealed class FakeSource(IEmulatorMemoryReader? memoryReader = null) : ILiveTeamSource
    {
        public event EventHandler<EmulatorConnectionState>? StateChanged;

        public event EventHandler<PartySnapshot>? PartyChanged;

        public int Port => 8888;

        public IEmulatorMemoryReader? MemoryReader => memoryReader;

        public string? ActiveEmulator => "melonDS";

        public void Start()
        {
        }

        public void RaiseParty(PartySnapshot party) => PartyChanged?.Invoke(this, party);

        public void RaiseState(EmulatorConnectionState state) => StateChanged?.Invoke(this, state);

        public ValueTask DisposeAsync()
        {
            StateChanged?.Invoke(this, EmulatorConnectionState.Idle);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeMemory : IEmulatorMemoryReader
    {
        private readonly Dictionary<uint, byte[]> _regions = [];

        public bool CanRead => true;

        public TaskCompletionSource<bool>? ReadGate { get; set; }

        public void Put(uint address, byte[] bytes) => _regions[address] = bytes;

        public async Task<byte[]?> ReadMemoryAsync(
            uint address,
            int length,
            CancellationToken cancellationToken = default)
        {
            if (ReadGate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            return _regions.TryGetValue(address, out byte[]? bytes) && bytes.Length == length
                ? bytes.ToArray()
                : null;
        }
    }
}
