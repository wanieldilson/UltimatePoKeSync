using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

/// <summary>
/// An egg carries a species, types and stats in its bytes, so every analysis used to treat
/// it as a sixth of a team. It cannot switch in, cannot attack and cannot be taught a move,
/// so counting it credits the party with defences and coverage it does not have. See D-036.
/// </summary>
public sealed class EggExclusionTests
{
    [Fact]
    public void AnEggIsNotOneOfTheBattlers()
    {
        PartySnapshot party = AnalysisTestData.Party(
            AnalysisTestData.Member(slot: 0),
            AnalysisTestData.Member(slot: 1, isEgg: true));

        Assert.Equal(2, party.Count);
        Assert.Single(party.Battlers);
        Assert.Equal(0, party.Battlers[0].SlotIndex);
    }

    /// <summary>
    /// The clearest case: a lone Water egg beside a lone Ground Pokémon must not make the
    /// party look like it resists Fire.
    /// </summary>
    [Fact]
    public void AnEggDoesNotDefendTheTeam()
    {
        PokemonSnapshot fighter = AnalysisTestData.Member(slot: 0, primaryType: PokemonType.Ground);
        var analyzer = new TeamAnalyzer();

        TeamAnalysis withoutEgg = analyzer.Analyze(AnalysisTestData.Party(fighter));
        TeamAnalysis withEgg = analyzer.Analyze(AnalysisTestData.Party(
            fighter,
            AnalysisTestData.Member(slot: 1, primaryType: PokemonType.Water, isEgg: true)));

        Assert.Equal(Resistances(withoutEgg), Resistances(withEgg));
        Assert.Equal(withoutEgg.DefensiveGaps, withEgg.DefensiveGaps);
    }

    [Fact]
    public void AnEggDoesNotCoverATypeTheTeamCannotHit()
    {
        MoveSlot tackle = AnalysisTestData.Move(33, "Tackle", PokemonType.Normal);
        MoveSlot surf = AnalysisTestData.Move(57, "Surf", PokemonType.Water);
        var analyzer = new TeamAnalyzer();

        TeamAnalysis withEgg = analyzer.Analyze(AnalysisTestData.Party(
            AnalysisTestData.Member(slot: 0, moves: tackle),
            AnalysisTestData.Member(slot: 1, isEgg: true, moves: surf)));

        // Surf belongs to the egg, so Rock and Ground stay unanswered.
        Assert.DoesNotContain(
            withEgg.OffensiveCoverage.Where(entry => entry.IsCovered),
            entry => entry.DefendingType is PokemonType.Rock or PokemonType.Ground);
    }

    /// <summary>
    /// A level 5 egg beside a level 40 Pokémon is not an incoherent team, and the party
    /// size factor should say five of six with an egg rather than claim six.
    /// </summary>
    [Fact]
    public void TheStrengthScoreCountsBattlersRatherThanSlots()
    {
        PartySnapshot party = AnalysisTestData.Party(
            AnalysisTestData.Member(slot: 0, level: 40),
            AnalysisTestData.Member(slot: 1, level: 5, isEgg: true));

        TeamStrength strength = new TeamStrengthAnalyzer()
            .Evaluate(new TeamAnalyzer().Analyze(party));

        TeamStrengthFactor size = strength.Factors.Single(
            factor => factor.Kind == TeamStrengthKind.PartySize);
        Assert.Contains("egg", size.Explanation, StringComparison.OrdinalIgnoreCase);

        // One battler cannot be out of step with itself.
        TeamStrengthFactor cohesion = strength.Factors.Single(
            factor => factor.Kind == TeamStrengthKind.LevelCohesion);
        Assert.Equal(cohesion.MaximumPoints, cohesion.Points);
    }

    [Fact]
    public void AnEggIsGivenNoBuildToFollow()
    {
        PartySnapshot party = AnalysisTestData.Party(
            AnalysisTestData.Member(slot: 0, speciesId: 252, speciesName: "TREECKO", level: 10),
            AnalysisTestData.Member(slot: 1, speciesId: 255, speciesName: "TORCHIC", isEgg: true));

        TeamRecommendation recommendation = PokemonRecommendationEngine
            .CreateDefault(PKHeXGen3MoveLearnSource.Instance)
            .Recommend(party, RecommendationProfileKind.Playthrough);

        Assert.Single(recommendation.Members);
        Assert.Equal(0, recommendation.Members[0].Member.SlotIndex);
    }

    private static string[] Resistances(TeamAnalysis analysis) =>
        [.. analysis.DefensiveCoverage
            .Where(entry => entry.ResistantCount > 0)
            .Select(entry => entry.AttackingType.ToString())
            .Order()];
}
