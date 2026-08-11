using System.Text.Json;
using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Learnsets;
using UltimatePoKeSync.Parsing;
using Xunit;

namespace UltimatePoKeSync.Cli.Tests;

/// <summary>
/// What the console prints, driven by the Italian Emerald capture.
/// </summary>
/// <remarks>
/// The report is the surface every analysis layer is checked through by hand (D-026), so
/// it is the one place where a silent formatting change would go unnoticed the longest.
/// </remarks>
public sealed class AnalysisReportTests
{
    [Fact]
    public void TheTeamAnalysisNamesEveryUnansweredWeakness()
    {
        TeamAnalysis analysis = new TeamAnalyzer().Analyze(LoadRealParty());

        string report = Capture(() => AnalysisReport.PrintTeamAnalysis(analysis));

        Assert.Contains("Team analysis", report, StringComparison.Ordinal);
        Assert.Contains("Defensive", report, StringComparison.Ordinal);
        Assert.Contains("TREECKO", report, StringComparison.Ordinal);

        // A lone Grass starter is weak to five types and answers none of the seventeen.
        Assert.Contains("Fire", report, StringComparison.Ordinal);
        Assert.Contains("0/17", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// Seventeen type names on one line wrap into a mess, which is what this used to do.
    /// </summary>
    [Fact]
    public void LongTypeListsAreCutShortRatherThanWrapped()
    {
        TeamAnalysis analysis = new TeamAnalyzer().Analyze(LoadRealParty());

        string report = Capture(() => AnalysisReport.PrintTeamAnalysis(analysis));

        Assert.Contains("more", report, StringComparison.Ordinal);
        Assert.All(
            report.Split('\n'),
            line => Assert.True(line.Length < 120, line));
    }

    [Fact]
    public void TheStrengthPanelAttributesEveryPointToAFactor()
    {
        TeamAnalysis analysis = new TeamAnalyzer().Analyze(LoadRealParty());
        TeamStrength strength = new TeamStrengthAnalyzer().Evaluate(analysis);

        string report = Capture(() => AnalysisReport.PrintTeamStrength(strength));

        Assert.Contains($"{strength.Score}/{strength.MaximumScore}", report, StringComparison.Ordinal);
        foreach (string factor in new[]
                 {
                     "party size", "level cohesion", "defensive coverage",
                     "offensive coverage", "nature fit", "effort value fit",
                 })
        {
            Assert.Contains(factor, report, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Every slot has to say how the move is obtained. It went missing once already,
    /// because the change reached the console and not the window.
    /// </summary>
    [Fact]
    public void EveryMoveOfTheBestSetSaysHowItIsObtained()
    {
        PartySnapshot party = LoadRealParty();
        TeamRecommendation recommendation = PokemonRecommendationEngine
            .CreateDefault(PKHeXGen3MoveLearnSource.Instance)
            .Recommend(party, RecommendationProfileKind.Playthrough);

        string report = Capture(() => AnalysisReport.PrintRecommendations(recommendation));

        Assert.Contains("Best set", report, StringComparison.Ordinal);
        Assert.Contains("already knows it", report, StringComparison.Ordinal);
        Assert.Contains("TM or HM", report, StringComparison.Ordinal);

        // And the availability of every candidate, which is the honesty boundary of D-025.
        Assert.Contains("check availability in this save", report, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCompetitiveProfileMakesNoClaimAboutTheSave()
    {
        PartySnapshot party = LoadRealParty();
        TeamRecommendation recommendation = PokemonRecommendationEngine
            .CreateDefault(PKHeXGen3MoveLearnSource.Instance)
            .Recommend(party, RecommendationProfileKind.Competitive);

        string report = Capture(() => AnalysisReport.PrintRecommendations(recommendation));

        Assert.Contains("Competitive profile", report, StringComparison.Ordinal);
        Assert.Contains("not a save claim", report, StringComparison.Ordinal);
        Assert.DoesNotContain("check availability in this save", report, StringComparison.Ordinal);
    }

    /// <summary>Runs the printer with the console redirected, and gives back what it wrote.</summary>
    private static string Capture(Action print)
    {
        TextWriter original = Console.Out;
        using var buffer = new StringWriter();
        Console.SetOut(buffer);

        try
        {
            print();
        }
        finally
        {
            Console.SetOut(original);
        }

        return buffer.ToString();
    }

    private static PartySnapshot LoadRealParty()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "emerald-it-treecko.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;

        var game = new GameIdentity(
            root.GetProperty("gameCode").GetString()!,
            root.GetProperty("title").GetString()!,
            root.GetProperty("revision").GetInt32(),
            (PokemonGeneration)root.GetProperty("generation").GetInt32());

        return new Gen3PartyParser().Parse(new RawPartySnapshot(
            game,
            root.GetProperty("partyCount").GetInt32(),
            Convert.FromBase64String(root.GetProperty("data").GetString()!),
            root.GetProperty("slotSize").GetInt32(),
            DateTimeOffset.UnixEpoch,
            1));
    }
}
