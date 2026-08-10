using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

public sealed class PKHeXGen3LevelUpLearnsetsTests
{
    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity ItalianEmerald =
        new("BPEI", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity FireRed =
        new("BPRE", "POKEMON FIRE", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity RubySapphire =
        new("AXVE", "POKEMON RUBY", 0, PokemonGeneration.Gen3);

    private readonly PKHeXGen3LevelUpLearnsets _learnsets = PKHeXGen3LevelUpLearnsets.Instance;

    [Fact]
    public void Supports_CoversEveryGen3GameTheParserCovers()
    {
        Assert.All(
            new[] { "BPEE", "BPEF", "BPED", "BPES", "BPEI", "BPRE", "BPGE", "AXVE", "AXPE" },
            code => Assert.True(
                _learnsets.Supports(new GameIdentity(code, "", 0, PokemonGeneration.Gen3)),
                code));

        Assert.False(_learnsets.Supports(new GameIdentity("BPEJ", "", 0, PokemonGeneration.Gen3)));
        Assert.False(_learnsets.Supports(new GameIdentity("BPEE", "", 0, PokemonGeneration.Gen4)));
    }

    /// <summary>
    /// The reason this source exists. Zubat learns Supersonic at 6 and Astonish at 11 in
    /// Ruby, Sapphire and Emerald, and exactly the other way round in FireRed and
    /// LeafGreen. A learnset merged across the generation has to pick one and be wrong
    /// somewhere. See D-027.
    /// </summary>
    [Fact]
    public void FindLevelUpMoves_DisagreesBetweenGamesOfTheSameGeneration()
    {
        const int zubat = 41;

        Assert.Equal(6, LevelOf(Emerald, zubat, "Supersonic"));
        Assert.Equal(11, LevelOf(Emerald, zubat, "Astonish"));
        Assert.Equal(6, LevelOf(RubySapphire, zubat, "Supersonic"));

        Assert.Equal(11, LevelOf(FireRed, zubat, "Supersonic"));
        Assert.Equal(6, LevelOf(FireRed, zubat, "Astonish"));
    }

    [Fact]
    public void FindLevelUpMoves_StopsAtTheGivenLevel()
    {
        const int treecko = 252;

        IReadOnlyList<LevelUpMoveReference> atEleven =
            _learnsets.FindLevelUpMoves(Emerald, treecko, 11);

        Assert.Equal(
            ["pound", "leer", "absorb", "quickattack"],
            atEleven.Select(move => move.Move.ReferenceId));
        Assert.Equal([1, 1, 6, 11], atEleven.Select(move => move.LearnedAtLevel));

        Assert.DoesNotContain(
            _learnsets.FindLevelUpMoves(Emerald, treecko, 15),
            move => move.Move.ReferenceId == "pursuit");
        Assert.Contains(
            _learnsets.FindLevelUpMoves(Emerald, treecko, 16),
            move => move.Move.ReferenceId == "pursuit");
    }

    [Fact]
    public void FindLevelUpMoves_TreatsEveryWesternEmeraldAsTheSameGame()
    {
        Assert.Equal(
            _learnsets.FindLevelUpMoves(Emerald, 252, 40),
            _learnsets.FindLevelUpMoves(ItalianEmerald, 252, 40));
    }

    [Fact]
    public void FindLevelUpMoves_ReturnsNothingForSpeciesOutsideGen3()
    {
        Assert.Empty(_learnsets.FindLevelUpMoves(Emerald, 387, 50));
        Assert.Empty(_learnsets.FindLevelUpMoves(Emerald, 0, 50));
    }

    [Fact]
    public void FindLevelUpMoves_RejectsAnUnsupportedGameAndAnImpossibleLevel()
    {
        Assert.Throws<NotSupportedException>(() => _learnsets.FindLevelUpMoves(
            new GameIdentity("BPEJ", "", 0, PokemonGeneration.Gen3),
            252,
            10));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _learnsets.FindLevelUpMoves(Emerald, 252, 101));
    }

    private int LevelOf(GameIdentity game, int species, string moveName) =>
        _learnsets.FindLevelUpMoves(game, species, 100)
            .Single(move => move.Move.Name == moveName)
            .LearnedAtLevel;
}
