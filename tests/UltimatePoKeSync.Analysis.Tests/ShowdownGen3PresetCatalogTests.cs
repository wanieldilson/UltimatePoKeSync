using UltimatePoKeSync.GameData;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class ShowdownGen3PresetCatalogTests
{
    [Fact]
    public void Catalog_LoadsPinnedDimensionsAndSourceIdentity()
    {
        ShowdownGen3PresetCatalog catalog = ShowdownGen3PresetCatalog.Instance;

        Assert.Equal(220, catalog.SpeciesCount);
        Assert.Equal("Pokémon Showdown Gen 3 Random Battle", catalog.SourceName);
        Assert.Equal(ShowdownGen3PresetCatalog.Revision, catalog.SourceRevision);
    }

    [Fact]
    public void Find_ReturnsGyaradosRoleAndMovePoolReferences()
    {
        IReadOnlyList<ReferencePreset> presets = ShowdownGen3PresetCatalog.Instance.Find("Gyarados");

        Assert.Equal(2, presets.Count);
        Assert.Contains(presets, preset => preset.Role == "Wallbreaker");
        Assert.Contains(presets, preset => preset.Role == "Setup Sweeper");
        Assert.All(presets, preset => Assert.Contains("earthquake", preset.MovePool));
    }

    [Theory]
    [InlineData("Mr. Mime")]
    [InlineData("Farfetch'd")]
    [InlineData("Ho-Oh")]
    public void Find_NormalizesDisplayNames(string speciesName)
    {
        Assert.NotEmpty(ShowdownGen3PresetCatalog.Instance.Find(speciesName));
    }

    [Fact]
    public void Find_ReturnsEmptyForUnknownSpecies()
    {
        Assert.Empty(ShowdownGen3PresetCatalog.Instance.Find("MissingNo"));
    }
}
