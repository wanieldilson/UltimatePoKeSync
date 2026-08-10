using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class ShowdownGen3MoveCatalogTests
{
    [Fact]
    public void Catalog_LoadsAllGen3Moves()
    {
        ShowdownGen3MoveCatalog catalog = ShowdownGen3MoveCatalog.Instance;

        Assert.Equal(354, catalog.MoveCount);
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
    public void Find_ByMoveId_MatchesTheNameLookup()
    {
        ShowdownGen3MoveCatalog catalog = ShowdownGen3MoveCatalog.Instance;

        Assert.Equal(catalog.Find("Earthquake"), catalog.Find(89));
        Assert.Null(catalog.Find(0));
        Assert.Null(catalog.Find(355));
    }
}
