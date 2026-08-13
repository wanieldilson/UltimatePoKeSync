using System.Text.Json;
using UltimatePoKeSync.App.Services;
using UltimatePoKeSync.App.ViewModels;
using UltimatePoKeSync.Contracts;
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

        // The candidates come from the Gen 5 tables: Grass Pledge is a move Gen 3 does not
        // have, so seeing it proves the right catalog answered.
        Assert.Contains(snivy.Candidates, candidate => candidate.Name == "Grass Pledge");
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

    private sealed class FakeSource : ILiveTeamSource
    {
        public event EventHandler<EmulatorConnectionState>? StateChanged;

        public event EventHandler<PartySnapshot>? PartyChanged;

        public int Port => 8888;

        public IEmulatorMemoryReader? MemoryReader => null;

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
}
