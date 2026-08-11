using UltimatePoKeSync.Contracts;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

public sealed class TeamStrengthAnalyzerTests
{
    private readonly TeamAnalyzer _teamAnalyzer = new();
    private readonly TeamStrengthAnalyzer _strengthAnalyzer = new();

    [Fact]
    public void Evaluate_AttributesEveryPointToANamedFactor()
    {
        TeamStrength strength = Evaluate(AnalysisTestData.Party(
            AnalysisTestData.Member(primaryType: PokemonType.Fire, speciesName: "Torchic")));

        Assert.Equal(strength.Score, strength.Factors.Sum(factor => factor.Points));
        Assert.Equal(100, strength.MaximumScore);
        Assert.Equal(
            Enum.GetValues<TeamStrengthKind>(),
            strength.Factors.Select(factor => factor.Kind));
        Assert.All(strength.Factors, factor => Assert.False(string.IsNullOrWhiteSpace(factor.Explanation)));
    }

    [Fact]
    public void Evaluate_PenalisesAnUnevenPartyAndNamesTheLaggard()
    {
        TeamStrength strength = Evaluate(AnalysisTestData.Party(
            AnalysisTestData.Member(slot: 0, speciesName: "Blaziken", level: 50),
            AnalysisTestData.Member(slot: 1, speciesName: "Zigzagoon", level: 20)));

        TeamStrengthFactor levels = Factor(strength, TeamStrengthKind.LevelCohesion);

        Assert.False(levels.IsPerfect);
        Assert.Equal("Zigzagoon", Assert.Single(levels.Members).SpeciesName);
        Assert.Contains("Zigzagoon", levels.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_DoesNotPunishAPartyThatSimplyHasNotTrainedEffortValues()
    {
        TeamStrength strength = Evaluate(AnalysisTestData.Party(
            AnalysisTestData.Member(speciesName: "Mudkip", level: 10)));

        TeamStrengthFactor effortValues = Factor(strength, TeamStrengthKind.EffortValueFit);

        Assert.True(effortValues.IsPerfect);
        Assert.Contains("none are wasted", effortValues.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_FlagsANatureThatLowersTheStatTheRoleNeeds()
    {
        // Adamant on a special attacker: -Special Attack is exactly the stat it lives on.
        PokemonSnapshot alakazam = AnalysisTestData.Member(
            primaryType: PokemonType.Psychic,
            speciesName: "Alakazam",
            baseStats: new StatBlock(55, 50, 45, 135, 95, 120),
            moves: [AnalysisTestData.Move(94, "Psychic", PokemonType.Psychic)]) with
        {
            NatureId = 3,
            NatureName = "Adamant",
        };

        TeamStrengthFactor natures =
            Factor(Evaluate(AnalysisTestData.Party(alakazam)), TeamStrengthKind.NatureFit);

        Assert.Equal(0, natures.Points);
        Assert.Equal("Alakazam", Assert.Single(natures.Members).SpeciesName);
    }

    [Fact]
    public void Evaluate_RewardsCoverageAndPunishesItsAbsence()
    {
        PokemonSnapshot blind = AnalysisTestData.Member(
            primaryType: PokemonType.Normal,
            speciesName: "Snorlax",
            moves: [AnalysisTestData.Move(33, "Tackle", PokemonType.Normal)]);

        TeamStrength strength = Evaluate(AnalysisTestData.Party(blind));
        TeamStrengthFactor offence = Factor(strength, TeamStrengthKind.OffensiveCoverage);

        Assert.Equal(0, offence.Points);
        Assert.Contains("nothing answers", offence.Explanation, StringComparison.Ordinal);
    }

    private static TeamStrengthFactor Factor(TeamStrength strength, TeamStrengthKind kind) =>
        strength.Factors.Single(factor => factor.Kind == kind);

    private TeamStrength Evaluate(PartySnapshot party) =>
        _strengthAnalyzer.Evaluate(_teamAnalyzer.Analyze(party));
}
