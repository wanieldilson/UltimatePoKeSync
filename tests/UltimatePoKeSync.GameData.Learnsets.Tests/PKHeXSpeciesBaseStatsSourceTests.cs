using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

public sealed class PKHeXSpeciesBaseStatsSourceTests
{
    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    private static readonly GameIdentity FireRed =
        new("BPRE", "POKEMON FIRE", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity LeafGreen =
        new("BPGE", "POKEMON LEAF", 0, PokemonGeneration.Gen3);

    [Fact]
    public void GenFive_ReturnsSerperiorsBaseStats()
    {
        StatBlock? stats = PKHeXSpeciesBaseStatsSource.Gen5.FindBaseStats(Black, 497);

        Assert.Equal(new StatBlock(75, 75, 95, 75, 95, 113), stats);
    }

    [Fact]
    public void GenThree_ReturnsBlazikensDualTyping()
    {
        SpeciesBattleProfile? profile =
            PKHeXSpeciesBaseStatsSource.Gen3.FindProfile(Emerald, 257);

        Assert.NotNull(profile);
        Assert.Equal(PokemonType.Fire, profile.PrimaryType);
        Assert.Equal(PokemonType.Fighting, profile.SecondaryType);
        Assert.Equal(new StatBlock(80, 120, 70, 110, 70, 80), profile.BaseStats);
    }

    [Fact]
    public void GenThree_ReturnsSceptilesBaseStats()
    {
        StatBlock? stats = PKHeXSpeciesBaseStatsSource.Gen3.FindBaseStats(Emerald, 254);

        Assert.Equal(new StatBlock(70, 85, 65, 105, 85, 120), stats);
    }

    [Fact]
    public void GenThree_SelectsDeoxysStatsPerGame()
    {
        Assert.Equal(
            new StatBlock(50, 95, 90, 95, 90, 180),
            PKHeXSpeciesBaseStatsSource.Gen3.FindBaseStats(Emerald, 386));
        Assert.Equal(
            new StatBlock(50, 180, 20, 180, 20, 150),
            PKHeXSpeciesBaseStatsSource.Gen3.FindBaseStats(FireRed, 386));
        Assert.Equal(
            new StatBlock(50, 70, 160, 70, 160, 90),
            PKHeXSpeciesBaseStatsSource.Gen3.FindBaseStats(LeafGreen, 386));
    }

    [Fact]
    public void EachSourceSupportsOnlyItsOwnGeneration()
    {
        Assert.True(PKHeXSpeciesBaseStatsSource.Gen3.Supports(Emerald));
        Assert.False(PKHeXSpeciesBaseStatsSource.Gen3.Supports(Black));
        Assert.True(PKHeXSpeciesBaseStatsSource.Gen5.Supports(Black));
        Assert.False(PKHeXSpeciesBaseStatsSource.Gen5.Supports(Emerald));

        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen3.FindBaseStats(Black, 254));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen5.FindBaseStats(Emerald, 497));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen3.FindProfile(Black, 254));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen5.FindProfile(Emerald, 497));
    }

    [Fact]
    public void SpeciesOutsideEachGenerationsRangeReturnNothing()
    {
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen3.FindBaseStats(Emerald, 0));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen3.FindBaseStats(Emerald, 387));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen5.FindBaseStats(Black, 0));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen5.FindBaseStats(Black, 650));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen3.FindProfile(Emerald, 387));
        Assert.Null(PKHeXSpeciesBaseStatsSource.Gen5.FindProfile(Black, 650));
    }
}
