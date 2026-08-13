using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

/// <summary>
/// What the next levels bring in Gen 5, and that asking for one generation never answers
/// with another's tables. See D-043.
/// </summary>
public sealed class Gen5ProgressTests
{
    private static readonly PokemonProgressAnalyzer Analyzer =
        new(PKHeXSources.Learnsets, PKHeXSources.Evolutions);

    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    [Fact]
    public void ASnivyIsElevenLevelsFromServine()
    {
        PokemonProgress progress = Analyze(Black, speciesId: 495, level: 6);

        Assert.Equal("Servine", progress.NextEvolution!.IntoSpeciesName);
        Assert.Equal(17, progress.NextEvolution.Level);
        Assert.Equal(EvolutionTrigger.Level, progress.NextEvolution.Trigger);

        Assert.Equal("Vine Whip", progress.Moves[0].Move.Name);
        Assert.Equal(7, progress.Moves[0].Level);
        Assert.Equal(1, progress.Moves[0].LevelsAway);
        Assert.All(progress.Moves, move => Assert.True(move.Level <= 17));
        Assert.True(progress.MovesStopAtEvolution);
    }

    /// <summary>
    /// The composite must route by game, not by whichever source is listed first. A Gen 5
    /// species asked of the Gen 3 tables would either be missing or, worse, be a different
    /// Pokémon: 495 is Snivy in Black and nothing at all in Emerald.
    /// </summary>
    [Fact]
    public void EachGenerationIsAnsweredFromItsOwnTables()
    {
        Assert.True(PKHeXSources.Learnsets.Supports(Black));
        Assert.True(PKHeXSources.Learnsets.Supports(Emerald));
        Assert.True(PKHeXSources.Evolutions.Supports(Black));
        Assert.True(PKHeXSources.Evolutions.Supports(Emerald));

        // Treecko evolves at 16 in Emerald; Snivy at 17 in Black. Different tables, and
        // the numbers are close enough that a mix-up would look plausible.
        Assert.Equal(16, Analyze(Emerald, 252, 5).NextEvolution!.Level);
        Assert.Equal(17, Analyze(Black, 495, 5).NextEvolution!.Level);
    }

    [Fact]
    public void AGameNobodyHasMappedIsRefusedRatherThanGuessedAt()
    {
        var unknown = new GameIdentity("XXXX", "SOMETHING", 0, PokemonGeneration.Gen4);

        Assert.False(PKHeXSources.Learnsets.Supports(unknown));
        Assert.False(PKHeXSources.Evolutions.Supports(unknown));

        // And the analyser says nothing rather than throwing at the window.
        Assert.Same(PokemonProgress.Nothing, Analyze(unknown, 495, 6));
    }

    /// <summary>
    /// Gen 5 is where an evolution can need something no level ever provides. Karrablast
    /// only becomes Escavalier by being traded for a Shelmet, and the card has to say that
    /// rather than count down to a level that will never arrive.
    /// </summary>
    [Fact]
    public void AnEvolutionThatNeedsATradeSaysSo()
    {
        PokemonProgress progress = Analyze(Black, speciesId: 588, level: 30);

        Assert.NotNull(progress.NextEvolution);
        Assert.Equal("Escavalier", progress.NextEvolution!.IntoSpeciesName);
        Assert.Equal(EvolutionTrigger.Trade, progress.NextEvolution.Trigger);
        Assert.Contains("Shelmet", progress.NextEvolution.Requirement, StringComparison.Ordinal);
        Assert.Null(progress.NextEvolution.Level);
        Assert.False(progress.NextEvolution.HappensByLevellingAlone);
    }

    [Theory]
    [InlineData(PokemonGender.Male, false)]
    [InlineData(PokemonGender.Female, true)]
    public void OnlyFemaleCombeeIsPromisedVespiquen(
        PokemonGender gender,
        bool evolves)
    {
        PokemonSnapshot combee = AnalysisTestData.Member(
            speciesId: 415,
            level: 19,
            gender: gender);

        PokemonProgress progress = Analyzer.Analyze(Black, combee);

        Assert.Equal(evolves, progress.NextEvolution?.IntoSpeciesName == "Vespiquen");
    }

    [Fact]
    public void TheMovesAreGenFiveMovesRatherThanGenThreeOnes()
    {
        // Leaf Tornado is move 437, introduced in Gen 5. A Gen 3 catalog cannot name it.
        PokemonProgress progress = Analyze(Black, speciesId: 495, level: 13);

        Assert.Contains(progress.Moves, move => move.Move.Name == "Leaf Tornado");
        Assert.All(progress.Moves, move => Assert.NotEqual("?", move.Move.Name));
    }

    private static PokemonProgress Analyze(GameIdentity game, int speciesId, int level) =>
        Analyzer.Analyze(game, AnalysisTestData.Member(speciesId: speciesId, level: level));
}
