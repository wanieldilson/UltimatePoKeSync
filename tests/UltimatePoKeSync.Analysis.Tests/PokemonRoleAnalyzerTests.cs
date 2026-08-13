using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class PokemonRoleAnalyzerTests
{
    private static readonly GameIdentity Emerald =
        new("BPEE", "POKEMON EMER", 0, PokemonGeneration.Gen3);

    private static readonly GameIdentity Black =
        new("IRBI", "POKEMON B", 0, PokemonGeneration.Gen5);

    // No game data sources: these fix the arithmetic on the stats given, with no
    // evolution chain to follow. The final-form behaviour has its own tests.
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

        PokemonRoleAnalysis result = _analyzer.Analyze(member, Emerald);

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

        PokemonRoleAnalysis result = _analyzer.Analyze(member, Emerald);

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
            _analyzer.Analyze(member, Emerald).Role);
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

        Assert.Equal(expected, _analyzer.Analyze(member, Emerald).Role);
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

        PokemonRoleAnalysis result = _analyzer.Analyze(member, Emerald);

        Assert.Equal(PokemonRole.Support, result.Role);
        Assert.Equal(4, result.UtilityMoveCount);
    }

    [Fact]
    public void Analyze_TargetsTheFinalFormWhenTheLineIsUnambiguous()
    {
        PokemonSnapshot snivy = AnalysisTestData.Member(
            primaryType: PokemonType.Grass,
            speciesName: "Snivy",
            speciesId: 495,
            level: 6,
            baseStats: new StatBlock(45, 45, 55, 45, 55, 63),
            moves:
            [
                AnalysisTestData.Move(33, "Tackle", PokemonType.Normal),
                AnalysisTestData.Move(43, "Leer", PokemonType.Normal),
            ]);

        PokemonRoleAnalysis result = new PokemonRoleAnalyzer(PKHeXSources.All)
            .Analyze(snivy, Black);

        Assert.True(result.IsJudgedAsEvolution);
        Assert.Equal(497, result.JudgedSpeciesId);
        Assert.Equal("Serperior", result.JudgedSpeciesName);
        Assert.Equal(new StatBlock(75, 75, 95, 75, 95, 113), result.JudgedBaseStats);
        Assert.Equal(PokemonType.Grass, result.JudgedPrimaryType);
        Assert.Equal(PokemonType.None, result.JudgedSecondaryType);
        Assert.Equal(PokemonRole.MixedAttacker, result.Role);
    }

    [Fact]
    public void Analyze_TargetsTheFinalFormsTypingToo()
    {
        PokemonSnapshot torchic = AnalysisTestData.Member(
            primaryType: PokemonType.Fire,
            speciesName: "Torchic",
            speciesId: 255,
            level: 5,
            baseStats: new StatBlock(45, 60, 40, 70, 50, 45),
            moves: [AnalysisTestData.Move(52, "Ember", PokemonType.Fire)]);

        PokemonRoleAnalysis result = new PokemonRoleAnalyzer(PKHeXSources.All)
            .Analyze(torchic, Emerald);

        Assert.Equal("Blaziken", result.JudgedSpeciesName);
        Assert.Equal(PokemonType.Fire, result.JudgedPrimaryType);
        Assert.Equal(PokemonType.Fighting, result.JudgedSecondaryType);
    }

    [Fact]
    public void Analyze_TreatsTwoMethodsToMiloticAsOneDestination()
    {
        PokemonSnapshot feebas = AnalysisTestData.Member(
            primaryType: PokemonType.Water,
            speciesName: "Feebas",
            speciesId: 349,
            baseStats: new StatBlock(20, 15, 20, 10, 55, 80),
            moves: [AnalysisTestData.Move(55, "Water Gun", PokemonType.Water)]);

        PokemonRoleAnalysis result = new PokemonRoleAnalyzer(PKHeXSources.All)
            .Analyze(feebas, Black);

        Assert.Equal(350, result.JudgedSpeciesId);
        Assert.Equal("Milotic", result.JudgedSpeciesName);
        Assert.Equal(new StatBlock(95, 60, 79, 100, 125, 81), result.JudgedBaseStats);
    }

    [Fact]
    public void Analyze_TreatsShedinjaAsAByproductOfNincadasEvolution()
    {
        PokemonSnapshot nincada = AnalysisTestData.Member(
            primaryType: PokemonType.Bug,
            secondaryType: PokemonType.Ground,
            speciesName: "Nincada",
            speciesId: 290,
            baseStats: new StatBlock(31, 45, 90, 30, 30, 40),
            moves: [AnalysisTestData.Move(10, "Scratch", PokemonType.Normal)]);

        PokemonRoleAnalysis result = new PokemonRoleAnalyzer(PKHeXSources.All)
            .Analyze(nincada, Emerald);

        Assert.Equal(291, result.JudgedSpeciesId);
        Assert.Equal("Ninjask", result.JudgedSpeciesName);
        Assert.Equal(PokemonType.Bug, result.JudgedPrimaryType);
        Assert.Equal(PokemonType.Flying, result.JudgedSecondaryType);
    }

    [Theory]
    [InlineData(PokemonGender.Male, 415, "Combee")]
    [InlineData(PokemonGender.Female, 416, "Vespiquen")]
    public void Analyze_RespectsGenderLimitedEvolutionRoutes(
        PokemonGender gender,
        int expectedSpeciesId,
        string expectedName)
    {
        PokemonSnapshot combee = AnalysisTestData.Member(
            primaryType: PokemonType.Bug,
            secondaryType: PokemonType.Flying,
            speciesName: "Combee",
            speciesId: 415,
            gender: gender,
            moves: [AnalysisTestData.Move(16, "Gust", PokemonType.Flying)]);

        PokemonRoleAnalysis result = new PokemonRoleAnalyzer(PKHeXSources.All)
            .Analyze(combee, Black);

        Assert.Equal(expectedSpeciesId, result.JudgedSpeciesId);
        Assert.Equal(expectedName, result.JudgedSpeciesName);
    }

    [Fact]
    public void Analyze_DoesNotInventTheDestinationOfABranchingLine()
    {
        PokemonSnapshot eevee = AnalysisTestData.Member(
            primaryType: PokemonType.Normal,
            speciesName: "Eevee",
            speciesId: 133,
            baseStats: new StatBlock(55, 55, 50, 45, 65, 55),
            moves: [AnalysisTestData.Move(33, "Tackle", PokemonType.Normal)]);

        PokemonRoleAnalysis result = new PokemonRoleAnalyzer(PKHeXSources.All)
            .Analyze(eevee, Emerald);

        Assert.False(result.IsJudgedAsEvolution);
        Assert.Equal(eevee.SpeciesId, result.JudgedSpeciesId);
        Assert.Equal(eevee.BaseStats, result.JudgedBaseStats);
        Assert.Equal(eevee.PrimaryType, result.JudgedPrimaryType);
        Assert.Equal(eevee.SecondaryType, result.JudgedSecondaryType);
    }

    [Fact]
    public void Analyze_FallsBackToTheCurrentSpeciesWhenALaterStageBranches()
    {
        PokemonSnapshot poliwag = AnalysisTestData.Member(
            primaryType: PokemonType.Water,
            speciesName: "Poliwag",
            speciesId: 60,
            baseStats: new StatBlock(40, 50, 40, 40, 40, 90),
            moves: [AnalysisTestData.Move(55, "Water Gun", PokemonType.Water)]);

        PokemonRoleAnalysis result = new PokemonRoleAnalyzer(PKHeXSources.All)
            .Analyze(poliwag, Emerald);

        Assert.False(result.IsJudgedAsEvolution);
        Assert.Equal(poliwag.SpeciesId, result.JudgedSpeciesId);
        Assert.Equal(poliwag.BaseStats, result.JudgedBaseStats);
    }
}
