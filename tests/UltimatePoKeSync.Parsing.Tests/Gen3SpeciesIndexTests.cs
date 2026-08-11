using Xunit;

namespace UltimatePoKeSync.Parsing.Tests;

public sealed class Gen3SpeciesIndexTests
{
    [Theory]
    [InlineData(1, 1)]        // Bulbasaur: the first two generations agree with the dex
    [InlineData(251, 251)]    // Celebi, the last one that does
    [InlineData(252, 277)]    // Treecko, where Hoenn starts and the offset appears
    [InlineData(255, 280)]    // Torchic, still offset by 25
    [InlineData(279, 310)]    // Pelipper, where an offset of 25 would say 304 and be wrong
    [InlineData(386, 410)]    // Deoxys
    public void NationalNumbersBecomeTheIndexTheGameUses(int national, int expected) =>
        Assert.Equal(expected, Gen3SpeciesIndex.ToInternal(national));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(387)]
    public void SpeciesOutsideGen3HaveNoIndex(int national) =>
        Assert.Equal(0, Gen3SpeciesIndex.ToInternal(national));
}
