using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData;
using UltimatePoKeSync.GameData.Learnsets;
using Xunit;

namespace UltimatePoKeSync.Analysis.Tests;

/// <summary>
/// A Pokémon far above the party is not a suggestion. It cannot be worn down without
/// fainting whatever is wearing it down, and it will not stay in a ball. See D-062.
/// </summary>
public sealed class TeamHintReachTests
{
    private static readonly GameIdentity BlackTwo =
        new("IREO", "POKEMON B2", 0, PokemonGeneration.Gen5);

    [Fact]
    public void NothingWildlyAboveTheTeamIsOffered()
    {
        TeamHintAnalysis analysis = Analyse(partyLevel: 5);

        Assert.All(
            analysis.Plans.SelectMany(plan => plan.Additions),
            candidate => Assert.InRange(candidate.MinimumEncounterLevel, 1, 5 + 8));
    }

    /// <summary>
    /// And a stronger team is offered what a weaker one could not reach, so this is a rule
    /// about the gap rather than a ban on higher-level places. The candidate count cannot show
    /// it, because it saturates at the analyzer's own cap; the level of what is offered can.
    /// </summary>
    [Fact]
    public void AStrongerTeamIsOfferedHigherCatches()
    {
        int early = HighestOffered(partyLevel: 5);
        int later = HighestOffered(partyLevel: 40);

        Assert.True(early <= 5 + 8, $"a Lv.5 team was offered a Lv.{early} catch");
        Assert.True(later > early, $"expected higher catches at Lv.40 than at Lv.5, got {later} and {early}");
    }

    private static int HighestOffered(int partyLevel) => Analyse(partyLevel)
        .Plans
        .SelectMany(plan => plan.Additions)
        .Max(candidate => candidate.MinimumEncounterLevel);

    private static TeamHintAnalysis Analyse(int partyLevel)
    {
        IEncounterCatalog catalog = UnovaSequelEncounterCatalog.BlackTwo;

        // The last checkpoint, so the milestone gate is wide open and only the reach rule
        // can be doing the filtering.
        StoryMilestone last = catalog.FindMilestones(BlackTwo)[^1];

        return new TeamHintAnalyzer(PKHeXSources.All).Analyze(
            Party(partyLevel),
            last,
            catalog.FindEncounters(BlackTwo));
    }

    private static PartySnapshot Party(int level) => AnalysisTestData.Party(
        AnalysisTestData.Member(
            speciesId: 495,
            speciesName: "Snivy",
            primaryType: PokemonType.Grass,
            level: level,
            baseStats: new StatBlock(45, 45, 55, 45, 55, 63),
            moves: AnalysisTestData.Move(33, "Tackle", PokemonType.Normal))) with
    {
        Game = BlackTwo,
    };
}
