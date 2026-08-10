using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.Parsing;
using UltimatePoKeSync.Providers.MGba;
using UltimatePoKeSync.Session;

namespace UltimatePoKeSync.Cli;

/// <summary>
/// Diagnostic console: validates the mGBA -> TCP -> parsing -> tracking chain without
/// the UI.
/// </summary>
internal static class Program
{
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

            Console.WriteLine("Ctrl+C to quit.\n");

            try
            {
                await foreach (PartySnapshot party in tracker.TrackAsync(cancellation.Token))
                {
                    PrintParty(party);
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

        Console.WriteLine("└─");
    }

    private static string Format(StatBlock stats) =>
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
              --help             This message
            """);
    }
}
