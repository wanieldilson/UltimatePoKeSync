using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

/// <summary>
/// What the next few levels bring, against the real Gen 3 tables. See D-037.
/// </summary>
public sealed class PokemonProgressAnalyzerTests
{
    private static readonly PokemonProgressAnalyzer Analyzer = new(
        PKHeXGen3MoveLearnSource.Instance,
        PKHeXGen3EvolutionSource.Instance);

    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    [Fact]
    public void ATreeckoAtLevelTenIsSixLevelsFromGrovyle()
    {
        PokemonProgress progress = Analyze(speciesId: 252, level: 10);

        Assert.NotNull(progress.NextEvolution);
        Assert.Equal("Grovyle", progress.NextEvolution!.IntoSpeciesName);
        Assert.Equal(EvolutionTrigger.Level, progress.NextEvolution.Trigger);
        Assert.Equal(16, progress.NextEvolution.Level);
        Assert.Equal("at Lv.16", progress.NextEvolution.Requirement);
    }

    [Fact]
    public void TheMovesAreTheOnesStillAhead_WithHowFarAway()
    {
        PokemonProgress progress = Analyze(speciesId: 252, level: 10);

        Assert.NotEmpty(progress.Moves);
        Assert.All(progress.Moves, move => Assert.True(move.Level > 10));
        Assert.All(progress.Moves, move => Assert.Equal(move.Level - 10, move.LevelsAway));

        // Sorted, soonest first, so the first line of the card is the next thing to happen.
        Assert.Equal(
            [.. progress.Moves.Select(move => move.Level).Order()],
            [.. progress.Moves.Select(move => move.Level)]);
    }

    /// <summary>
    /// The honesty boundary of this card. Treecko learns Agility at Lv.23, but by Lv.23 it
    /// is a Grovyle following a different learnset, so promising it would be a lie told
    /// with a precise number attached.
    /// </summary>
    [Fact]
    public void NothingIsPromisedBeyondTheLevelItEvolves()
    {
        PokemonProgress progress = Analyze(speciesId: 252, level: 10);

        Assert.All(progress.Moves, move => Assert.True(move.Level <= 16, $"Lv.{move.Level}"));
        Assert.True(progress.MovesStopAtEvolution);
    }

    [Fact]
    public void AFullyEvolvedPokemonKeepsItsWholeList()
    {
        // Sceptile evolves into nothing, so nothing cuts the list short.
        PokemonProgress progress = Analyze(speciesId: 254, level: 40);

        Assert.Null(progress.NextEvolution);
        Assert.Empty(progress.OtherEvolutions);
        Assert.NotEmpty(progress.Moves);
        Assert.False(progress.MovesStopAtEvolution);
    }

    [Fact]
    public void AnEvolutionThatNeedsAStoneSaysWhichStone()
    {
        // Pikachu never evolves by levelling, however long it is walked.
        PokemonProgress progress = Analyze(speciesId: 25, level: 30);

        Assert.NotNull(progress.NextEvolution);
        Assert.Equal("Raichu", progress.NextEvolution!.IntoSpeciesName);
        Assert.Equal(EvolutionTrigger.Item, progress.NextEvolution.Trigger);
        Assert.Null(progress.NextEvolution.Level);
        Assert.Equal("with a Thunder Stone", progress.NextEvolution.Requirement);
        Assert.False(progress.NextEvolution.HappensByLevellingAlone);
    }

    /// <summary>Eevee is the case that breaks any model with one evolution per species.</summary>
    [Fact]
    public void ABranchingLineNamesOneAndKeepsTheRest()
    {
        PokemonProgress progress = Analyze(speciesId: 133, level: 20);

        Assert.NotNull(progress.NextEvolution);
        Assert.Equal(4, progress.OtherEvolutions.Count);

        string[] all =
        [
            progress.NextEvolution!.IntoSpeciesName,
            .. progress.OtherEvolutions.Select(step => step.IntoSpeciesName),
        ];
        Assert.Equal(
            ["Espeon", "Flareon", "Jolteon", "Umbreon", "Vaporeon"],
            [.. all.Order()]);

        // The two friendship ones are the only ones that need no item.
        Assert.Equal(
            2,
            progress.OtherEvolutions.Count(step => step.Trigger == EvolutionTrigger.Friendship)
                + (progress.NextEvolution.Trigger == EvolutionTrigger.Friendship ? 1 : 0));
    }

    [Fact]
    public void ATradeEvolutionSaysSoRatherThanNamingALevel()
    {
        PokemonProgress progress = Analyze(speciesId: 64, level: 40);

        Assert.Equal(EvolutionTrigger.Trade, progress.NextEvolution!.Trigger);
        Assert.Equal("when traded", progress.NextEvolution.Requirement);
    }

    /// <summary>
    /// A Treecko still a Treecko at Lv.30 has cancelled the evolution every level since 16,
    /// and the game will offer it again at 31. Listing its Lv.35 move would be promising
    /// something only a player who keeps pressing B will ever see.
    /// </summary>
    [Fact]
    public void APokemonPastItsEvolutionLevelIsOneLevelFromChanging()
    {
        PokemonProgress progress = Analyze(speciesId: 252, level: 30);

        Assert.True(progress.EvolvesOnNextLevelUp);
        Assert.Equal(16, progress.NextEvolution!.Level);
        Assert.Empty(progress.Moves);
        Assert.True(progress.MovesStopAtEvolution);
    }

    [Fact]
    public void AnEggIsNotGivenAFuture()
    {
        PokemonProgress progress = Analyze(speciesId: 252, level: 5, isEgg: true);

        Assert.Same(PokemonProgress.Nothing, progress);
        Assert.False(progress.HasAnything);
    }

    [Fact]
    public void NothingIsInventedForAMaxedOutPokemon()
    {
        PokemonProgress progress = Analyze(speciesId: 254, level: 100);

        Assert.Empty(progress.Moves);
        Assert.False(progress.HasAnything);
    }

    private static PokemonProgress Analyze(int speciesId, int level, bool isEgg = false) =>
        Analyzer.Analyze(
            Emerald,
            AnalysisTestData.Member(speciesId: speciesId, level: level, isEgg: isEgg));
}
