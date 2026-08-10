using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class PokemonRoleAnalyzerTests
{
    private readonly PokemonRoleAnalyzer _analyzer = new();

    [Fact]
    public void Analyze_ClassifiesPhysicalAttackerFromStatsDespiteMixedMoves()
    {
        PokemonSnapshot member = AnalysisTestData.Member(
            baseStats: new StatBlock(95, 125, 79, 60, 100, 81),
            moves:
            [
                AnalysisTestData.Move(89, "Earthquake", PokemonType.Ground),
                AnalysisTestData.Move(57, "Surf", PokemonType.Water),
                AnalysisTestData.Move(44, "Bite", PokemonType.Dark),
            ]);

        PokemonRoleAnalysis result = _analyzer.Analyze(member, PokemonGeneration.Gen3);

        Assert.Equal(PokemonRole.PhysicalAttacker, result.Role);
        Assert.Equal(1, result.PhysicalMoveCount);
        Assert.Equal(2, result.SpecialMoveCount);
    }

    [Fact]
    public void Analyze_UsesGen3TypeSplitForSpecialAttacker()
    {
        PokemonSnapshot member = AnalysisTestData.Member(
            baseStats: new StatBlock(55, 50, 45, 135, 85, 120),
            moves:
            [
                AnalysisTestData.Move(94, "Psychic", PokemonType.Psychic),
                AnalysisTestData.Move(7, "Fire Punch", PokemonType.Fire),
                AnalysisTestData.Move(9, "Thunder Punch", PokemonType.Electric),
            ]);

        PokemonRoleAnalysis result = _analyzer.Analyze(member, PokemonGeneration.Gen3);

        Assert.Equal(PokemonRole.SpecialAttacker, result.Role);
        Assert.Equal(0, result.PhysicalMoveCount);
        Assert.Equal(3, result.SpecialMoveCount);
    }

    [Fact]
    public void Analyze_ClassifiesBalancedOffenseAsMixedAttacker()
    {
        PokemonSnapshot member = AnalysisTestData.Member(
            baseStats: new StatBlock(81, 92, 77, 85, 75, 85),
            moves:
            [
                AnalysisTestData.Move(89, "Earthquake", PokemonType.Ground),
                AnalysisTestData.Move(58, "Ice Beam", PokemonType.Ice),
            ]);

        Assert.Equal(
            PokemonRole.MixedAttacker,
            _analyzer.Analyze(member, PokemonGeneration.Gen3).Role);
    }

    [Theory]
    [InlineData(65, 80, 140, 40, 70, PokemonRole.PhysicalWall)]
    [InlineData(255, 10, 10, 75, 135, PokemonRole.SpecialWall)]
    public void Analyze_ClassifiesDefensiveRoles(
        int hp,
        int attack,
        int defense,
        int specialAttack,
        int specialDefense,
        PokemonRole expected)
    {
        PokemonSnapshot member = AnalysisTestData.Member(
            baseStats: new StatBlock(hp, attack, defense, specialAttack, specialDefense, 70),
            moves:
            [
                AnalysisTestData.Move(65, "Drill Peck", PokemonType.Flying),
                AnalysisTestData.Move(92, "Toxic", PokemonType.Poison),
                AnalysisTestData.Move(182, "Protect", PokemonType.Normal),
                AnalysisTestData.Move(191, "Spikes", PokemonType.Ground),
            ]);

        Assert.Equal(expected, _analyzer.Analyze(member, PokemonGeneration.Gen3).Role);
    }

    [Fact]
    public void Analyze_ClassifiesFourUtilityMovesAsSupport()
    {
        PokemonSnapshot member = AnalysisTestData.Member(
            baseStats: new StatBlock(55, 20, 35, 20, 45, 75),
            moves:
            [
                AnalysisTestData.Move(147, "Spore", PokemonType.Grass),
                AnalysisTestData.Move(226, "Baton Pass", PokemonType.Normal),
                AnalysisTestData.Move(164, "Substitute", PokemonType.Normal),
                AnalysisTestData.Move(97, "Agility", PokemonType.Psychic),
            ]);

        PokemonRoleAnalysis result = _analyzer.Analyze(member, PokemonGeneration.Gen3);

        Assert.Equal(PokemonRole.Support, result.Role);
        Assert.Equal(4, result.UtilityMoveCount);
    }
}
