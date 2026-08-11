using UltimatePoKeSync.Analysis;
using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.GameData.Learnsets;
using UltimatePoKeSync.Parsing;
using UltimatePoKeSync.Providers.MGba;
using UltimatePoKeSync.Session;

namespace UltimatePoKeSync.Cli;

/// <summary>
/// Diagnostic console: validates the mGBA -> TCP -> parsing -> tracking chain without
/// the UI, and optionally the analysis and recommendation layers on top of it.
/// </summary>
internal static class Program
{
    private static readonly TeamStrengthAnalyzer StrengthAnalyzer = new();

    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return 0;
        }

        var options = new MGbaProviderOptions
        {
            Host = GetOption(args, "--host") ?? "127.0.0.1",
            Port = int.TryParse(GetOption(args, "--port"), out int port) ? port : 8888,
        };

        string? dumpDirectory = GetOption(args, "--dump");
        bool analyze = args.Contains("--analyze");

        string? profileName = GetOption(args, "--recommend");
        RecommendationProfileKind? profileKind = null;
        if (args.Contains("--recommend"))
        {
            if (!Enum.TryParse(profileName, ignoreCase: true, out RecommendationProfileKind parsed))
            {
                Console.Error.WriteLine(
                    $"Unknown recommendation profile: {profileName ?? "(missing)"}. "
                    + $"Expected one of {string.Join(", ", Enum.GetNames<RecommendationProfileKind>())}.");
                return 1;
            }

            profileKind = parsed;
        }

        string? replayPath = GetOption(args, "--replay");
        if (replayPath is not null)
        {
            return Replay(replayPath, analyze, profileKind);
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        var mgba = new MGbaProvider(options);
        IEmulatorProvider provider = dumpDirectory is null
            ? mgba
            : new DumpingEmulatorProvider(mgba, dumpDirectory, path =>
                Console.WriteLine($"[dump] {path}"));

        await using (provider)
        {
            var tracker = new PartyTracker(provider, PartyParserResolver.CreateDefault());

            mgba.StateChanged += (_, state) => WriteState(state, options);

            Console.WriteLine($"UltimatePoKeSync — waiting for mGBA on {options.Host}:{options.Port}");
            Console.WriteLine("Load emulator-scripts/mgba/ups_bridge.lua in mGBA (Tools > Scripting).");
            if (dumpDirectory is not null)
            {
                Console.WriteLine($"Dumping raw snapshots to {dumpDirectory}");
            }

            if (analyze || profileKind is not null)
            {
                Console.WriteLine("Analysis enabled"
                    + (profileKind is null ? string.Empty : $", {profileKind} recommendations"));
            }

            Console.WriteLine("Ctrl+C to quit.\n");

            var teamAnalyzer = new TeamAnalyzer();
            PokemonRecommendationEngine recommendationEngine =
                PokemonRecommendationEngine.CreateDefault(PKHeXGen3MoveLearnSource.Instance);

            try
            {
                await foreach (PartySnapshot party in tracker.TrackAsync(cancellation.Token))
                {
                    PrintParty(party);
                    PrintAnalysis(party, analyze, profileKind, teamAnalyzer, recommendationEngine);
                    Console.WriteLine("└─");
                }
            }
            catch (OperationCanceledException)
            {
                // User asked to quit.
            }

            Console.WriteLine($"\n{tracker.Diagnostics}");
        }

        Console.WriteLine("Shutting down.");
        return 0;
    }

    /// <summary>
    /// Renders one dumped snapshot without touching the emulator. Keeps the analysis
    /// layers checkable against captured real RAM, and reproducible in a bug report.
    /// </summary>
    private static int Replay(
        string path,
        bool analyze,
        RecommendationProfileKind? profileKind)
    {
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Snapshot fixture not found: {path}");
            return 1;
        }

        RawPartySnapshot raw = RawSnapshotDump.Read(path);
        IPartyParser? parser = PartyParserResolver.CreateDefault().Resolve(raw.Game);
        if (parser is null)
        {
            Console.Error.WriteLine($"No parser covers {raw.Game}.");
            return 1;
        }

        PartySnapshot party = parser.Parse(raw);
        PrintParty(party);
        PrintAnalysis(
            party,
            analyze,
            profileKind,
            new TeamAnalyzer(),
            PokemonRecommendationEngine.CreateDefault(PKHeXGen3MoveLearnSource.Instance));
        Console.WriteLine("└─");
        return 0;
    }

    private static void PrintParty(PartySnapshot party)
    {
        Console.WriteLine($"┌─ {party.Game}  ·  seq {party.Sequence}  ·  {party.CapturedAt.LocalDateTime:HH:mm:ss.fff}  ·  {party.Count} in party");

        if (party.IsEmpty)
        {
            Console.WriteLine("│  (empty party)");
        }

        foreach (PokemonSnapshot mon in party.Members)
        {
            string types = mon.IsDualType ? $"{mon.PrimaryType}/{mon.SecondaryType}" : mon.PrimaryType.ToString();
            string nickname = mon.Nickname.Equals(mon.SpeciesName, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : $" \"{mon.Nickname}\"";

            Console.WriteLine($"│");
            Console.WriteLine($"│  [{mon.SlotIndex}] {mon.SpeciesName}{nickname}  Lv.{mon.Level}  {types}"
                + (mon.IsShiny ? "  ✦shiny" : string.Empty)
                + (mon.IsEgg ? "  (egg)" : string.Empty));
            Console.WriteLine($"│      Nature {mon.NatureName} · Ability {mon.AbilityName} · Item {mon.HeldItemName}");
            Console.WriteLine($"│      Base  {Format(mon.BaseStats)}");
            Console.WriteLine($"│      IVs   {Format(mon.IndividualValues)}");
            Console.WriteLine($"│      EVs   {Format(mon.EffortValues)}   (total {mon.TotalEffortValues}/510)");
            Console.WriteLine($"│      Stats {Format(mon.CurrentStats)}");

            foreach (MoveSlot move in mon.Moves.Where(m => !m.IsEmpty))
            {
                Console.WriteLine($"│      · {move.Name,-16} {move.Type,-8} {move.CurrentPp}/{move.MaxPp} PP");
            }
        }

        foreach (RejectedSlot rejected in party.RejectedSlots)
        {
            Console.WriteLine($"│  [!] slot {rejected.SlotIndex} rejected: {rejected.Reason}");
        }
    }

    /// <summary>
    /// Renders the requested analysis layers. A generation without analysis rules must
    /// report itself and keep the stream alive, not end the session.
    /// </summary>
    private static void PrintAnalysis(
        PartySnapshot party,
        bool analyze,
        RecommendationProfileKind? profileKind,
        TeamAnalyzer teamAnalyzer,
        PokemonRecommendationEngine recommendationEngine)
    {
        if (party.IsEmpty || (!analyze && profileKind is null))
        {
            return;
        }

        try
        {
            if (profileKind is null)
            {
                TeamAnalysis only = teamAnalyzer.Analyze(party);
                AnalysisReport.PrintTeamAnalysis(only);
                AnalysisReport.PrintTeamStrength(StrengthAnalyzer.Evaluate(only));
                return;
            }

            // The engine already computes the team analysis, so reuse it rather than
            // running the same work twice.
            TeamRecommendation recommendation = recommendationEngine.Recommend(
                party,
                profileKind.Value);

            if (analyze)
            {
                AnalysisReport.PrintTeamAnalysis(recommendation.TeamAnalysis);
                AnalysisReport.PrintTeamStrength(
                    StrengthAnalyzer.Evaluate(recommendation.TeamAnalysis));
            }

            AnalysisReport.PrintRecommendations(recommendation);
        }
        catch (NotSupportedException ex)
        {
            Console.WriteLine("│");
            Console.WriteLine($"├─ Analysis unavailable: {ex.Message}");
        }
    }

    internal static string Format(StatBlock stats) =>
        $"HP {stats.Hp,3}  Atk {stats.Attack,3}  Def {stats.Defense,3}  "
        + $"SpA {stats.SpecialAttack,3}  SpD {stats.SpecialDefense,3}  Spe {stats.Speed,3}";

    private static void WriteState(EmulatorConnectionState state, MGbaProviderOptions options)
    {
        string message = state switch
        {
            EmulatorConnectionState.Connecting => "connecting...",
            EmulatorConnectionState.Streaming => "connected, listening.",
            EmulatorConnectionState.Reconnecting =>
                $"connection lost, retrying on {options.Host}:{options.Port}...",
            EmulatorConnectionState.Faulted => "unrecoverable error.",
            _ => state.ToString(),
        };

        Console.WriteLine($"[mGBA] {message}");
    }

    private static string? GetOption(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            UltimatePoKeSync CLI

              --host <address>   Host of the Lua script (default 127.0.0.1)
              --port <port>      Port, must match UPS_PORT (default 8888)
              --dump <dir>       Write every raw snapshot to <dir> as a test fixture
              --replay <file>    Render one dumped snapshot and exit, without mGBA
              --analyze        Print team type coverage and unanswered gaps
              --recommend <p>    Print per-Pokemon suggestions with profile <p>:
                                 playthrough or competitive
              --help             This message
            """);
    }
}
