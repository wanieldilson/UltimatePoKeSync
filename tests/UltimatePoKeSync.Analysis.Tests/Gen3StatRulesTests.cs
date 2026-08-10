using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class Gen3StatRulesTests
{
    private readonly Gen3Rules _rules = Gen3Rules.Instance;

    [Fact]
    public void LoadsAllNaturesInPersonalityValueOrder()
    {
        Assert.Equal(25, _rules.Natures.Count);
        Assert.Equal("Hardy", _rules.GetNature(0).Name);
        Assert.Equal("Adamant", _rules.GetNature(3).Name);
        Assert.Equal("Modest", _rules.GetNature(15).Name);
        Assert.Equal("Quirky", _rules.GetNature(24).Name);
    }

    [Fact]
    public void NatureUsesTheGamesIntegerModifiers()
    {
        NatureInfo adamant = _rules.GetNature(3);

        Assert.Equal(110, adamant.Apply(Stat.Attack, 100));
        Assert.Equal(90, adamant.Apply(Stat.SpecialAttack, 100));
        Assert.Equal(100, adamant.Apply(Stat.Speed, 100));
        Assert.False(adamant.IsNeutral);
        Assert.True(_rules.GetNature(0).IsNeutral);
    }

    [Fact]
    public void CalculatesACompetitiveGyaradosSpreadAtLevelFifty()
    {
        StatBlock result = _rules.CalculateStats(
            level: 50,
            baseStats: new StatBlock(95, 125, 79, 60, 100, 81),
            individualValues: new StatBlock(31, 31, 31, 31, 31, 31),
            effortValues: new StatBlock(4, 252, 0, 0, 0, 252),
            natureId: 3); // Adamant

        Assert.Equal(new StatBlock(171, 194, 99, 72, 120, 133), result);
    }

    [Fact]
    public void PreservesShedinjasOneHpSpecialCase()
    {
        StatBlock result = _rules.CalculateStats(
            level: 100,
            baseStats: new StatBlock(1, 90, 45, 30, 30, 40),
            individualValues: new StatBlock(31, 31, 31, 31, 31, 31),
            effortValues: new StatBlock(252, 252, 0, 0, 0, 4),
            natureId: 0);

        Assert.Equal(1, result.Hp);
    }

    [Fact]
    public void RejectsAnImpossibleGen3EvSpread()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _rules.CalculateStats(
            level: 50,
            baseStats: new StatBlock(50, 50, 50, 50, 50, 50),
            individualValues: new StatBlock(31, 31, 31, 31, 31, 31),
            effortValues: new StatBlock(252, 252, 252, 0, 0, 0),
            natureId: 0));
    }
}
