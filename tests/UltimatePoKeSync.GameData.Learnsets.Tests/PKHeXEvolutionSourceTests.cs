using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.GameData.Learnsets.Tests;

public sealed class PKHeXEvolutionSourceTests
{
    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    [Fact]
    public void ShedinjaIsMarkedAsAByproductRatherThanNincadasDestination()
    {
        IReadOnlyList<EvolutionStep> evolutions =
            PKHeXGen3EvolutionSource.Instance.FindEvolutions(Emerald, 290);

        EvolutionStep ninjask = Assert.Single(
            evolutions, step => step.IntoSpeciesName == "Ninjask");
        EvolutionStep shedinja = Assert.Single(
            evolutions, step => step.IntoSpeciesName == "Shedinja");

        Assert.False(ninjask.IsByproduct);
        Assert.True(ninjask.HappensByLevellingAlone);
        Assert.True(shedinja.IsByproduct);
        Assert.False(shedinja.HappensByLevellingAlone);
    }

    [Fact]
    public void GenFiveFeebasHasTwoRoutesToOneDestination()
    {
        IReadOnlyList<EvolutionStep> evolutions =
            PKHeXGen5EvolutionSource.Instance.FindEvolutions(Black, 349);

        Assert.Equal(2, evolutions.Count);
        Assert.All(evolutions, step => Assert.Equal(350, step.IntoSpeciesId));
        Assert.All(evolutions, step => Assert.False(step.IsByproduct));
        Assert.Contains(evolutions, step => step.Trigger == EvolutionTrigger.Condition);
        Assert.Contains(evolutions, step => step.Trigger == EvolutionTrigger.Trade);
    }

    [Fact]
    public void GenderLimitedLevelEvolutionsKeepTheirLevelAndEligibility()
    {
        EvolutionStep vespiquen = Assert.Single(
            PKHeXGen5EvolutionSource.Instance.FindEvolutions(Black, 415));

        Assert.Equal("Vespiquen", vespiquen.IntoSpeciesName);
        Assert.Equal(21, vespiquen.Level);
        Assert.Equal(PokemonGender.Female, vespiquen.RequiredGender);
        Assert.True(vespiquen.HappensByLevellingAlone);
    }
}
