using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class ShowdownGen3MoveCatalogTests
{
    [Fact]
    public void Catalog_LoadsAllGen3MovesAndSpecies()
    {
        ShowdownGen3MoveCatalog catalog = ShowdownGen3MoveCatalog.Instance;

        Assert.Equal(354, catalog.MoveCount);
        Assert.Equal(386, catalog.SpeciesCount);
        Assert.Equal(PokemonGeneration.Gen3, catalog.Generation);
        Assert.Equal(ShowdownGen3PresetCatalog.Revision, catalog.SourceRevision);
    }

    [Fact]
    public void Find_ResolvesCanonicalAndTypedHiddenPowerMoves()
    {
        ShowdownGen3MoveCatalog catalog = ShowdownGen3MoveCatalog.Instance;

        Assert.Equal(
            new MoveReference(89, "earthquake", "Earthquake", PokemonType.Ground),
            catalog.Find("Earthquake"));
        Assert.Equal(
            new MoveReference(237, "hiddenpowergrass", "Hidden Power Grass", PokemonType.Grass),
            catalog.Find("hiddenpowergrass"));
        Assert.Equal(PokemonType.Normal, catalog.Find("Sweet Kiss")?.Type);
    }

    [Fact]
    public void FindLevelUpMoves_FiltersTreeckoAtCurrentLevel()
    {
        IReadOnlyList<LevelUpMoveReference> moves =
            ShowdownGen3MoveCatalog.Instance.FindLevelUpMoves("Treecko", 11);

        Assert.Equal(["leer", "pound", "absorb", "quickattack"], moves.Select(move => move.Move.ReferenceId));
        Assert.Equal(11, moves[^1].LearnedAtLevel);
    }

    [Fact]
    public void FindLevelUpMoves_NormalizesGenderSymbols()
    {
        Assert.NotEmpty(ShowdownGen3MoveCatalog.Instance.FindLevelUpMoves("Nidoran♀", 10));
    }
}
