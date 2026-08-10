using UltimatePoKeSync.Contracts;
using UltimatePoKeSync.Parsing;
using UltimatePoKeSync.Providers.MGba;

namespace UltimatePoKeSync.Cli;

/// <summary>
/// Console di diagnostica: valida la catena mGBA -> TCP -> parsing senza la UI.
/// E' il target della milestone M3.
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

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellation.Cancel();
        };

        await using var provider = new MGbaProvider(options);
        IPartyParserResolver resolver = PartyParserResolver.CreateDefault();

        provider.StateChanged += (_, state) => WriteState(state, options);

        Console.WriteLine($"UltimatePoKeSync — in attesa di mGBA su {options.Host}:{options.Port}");
        Console.WriteLine("Carica emulator-scripts/mgba/ups_bridge.lua in mGBA (Tools > Scripting).");
        Console.WriteLine("Ctrl+C per uscire.\n");

        try
        {
            await foreach (RawPartySnapshot raw in provider.ReadSnapshotsAsync(cancellation.Token))
            {
                IPartyParser? parser = resolver.Resolve(raw.Game);
                if (parser is null)
                {
                    Console.WriteLine($"[!] Nessun parser per {raw.Game}. Snapshot ignorato.");
                    continue;
                }

                PrintParty(parser.Parse(raw));
            }
        }
        catch (OperationCanceledException)
        {
            // Uscita richiesta dall'utente.
        }

        Console.WriteLine("\nChiusura.");
        return 0;
    }

    private static void PrintParty(PartySnapshot party)
    {
        Console.WriteLine($"┌─ {party.Game}  ·  seq {party.Sequence}  ·  {party.CapturedAt.LocalDateTime:HH:mm:ss.fff}");

        if (party.IsEmpty)
        {
            Console.WriteLine("│  (squadra vuota)");
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
                + (mon.IsEgg ? "  (uovo)" : string.Empty));
            Console.WriteLine($"│      Natura {mon.NatureName} · Abilità {mon.AbilityName} · Oggetto {mon.HeldItemName}");
            Console.WriteLine($"│      Base  {Format(mon.BaseStats)}");
            Console.WriteLine($"│      IV    {Format(mon.IndividualValues)}");
            Console.WriteLine($"│      EV    {Format(mon.EffortValues)}   (totale {mon.TotalEffortValues}/510)");
            Console.WriteLine($"│      Stat  {Format(mon.CurrentStats)}");

            foreach (MoveSlot move in mon.Moves.Where(m => !m.IsEmpty))
            {
                Console.WriteLine($"│      · {move.Name,-16} {move.Type,-8} {move.CurrentPp}/{move.MaxPp} PP");
            }
        }

        foreach (RejectedSlot rejected in party.RejectedSlots)
        {
            Console.WriteLine($"│  [!] slot {rejected.SlotIndex} scartato: {rejected.Reason}");
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
            EmulatorConnectionState.Connecting => "connessione in corso...",
            EmulatorConnectionState.Streaming => "connesso, in ascolto.",
            EmulatorConnectionState.Reconnecting =>
                $"connessione persa, riprovo su {options.Host}:{options.Port}...",
            EmulatorConnectionState.Faulted => "errore non recuperabile.",
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

              --host <indirizzo>   Host dello script Lua (default 127.0.0.1)
              --port <porta>       Porta, deve coincidere con UPS_PORT (default 8888)
              --help               Questo messaggio
            """);
    }
}
