using System.Globalization;

namespace UltimatePoKeSync.SoulSilverOpponent;

internal static class ConsoleOutput
{
    public static void Render(OpponentScan scan, bool diagnostics, bool clear)
    {
        if (clear && !Console.IsOutputRedirected)
        {
            Console.Write("\e[2J\e[H");
        }

        Console.WriteLine("UltimatePoKeSync · SoulSilver opponent reader (beta)");
        Console.WriteLine(new string('=', 54));

        switch (scan.State)
        {
            case ScanState.EmulatorUnavailable:
                Console.WriteLine("Waiting for melonDS. Enable its GDB stub and leave JIT off.");
                break;
            case ScanState.UnsupportedGame:
                Console.WriteLine($"Unsupported game: {scan.GameTitle} [{scan.GameCode}]. This beta requires IPGE.");
                break;
            case ScanState.InvalidRootPointer:
                Console.WriteLine("SoulSilver is detected, but its runtime state is not ready yet.");
                break;
            case ScanState.Ready when scan.Rosters.Count == 0:
                Console.WriteLine($"{scan.GameTitle} [{scan.GameCode}] rev{scan.Revision}");
                Console.WriteLine("No checksum-valid opponent is currently visible in the known battle locations.");
                break;
            case ScanState.Ready:
                Console.WriteLine($"{scan.GameTitle} [{scan.GameCode}] rev{scan.Revision}");
                Console.WriteLine();
                foreach (OpponentRoster roster in scan.Rosters)
                {
                    Render(roster);
                }
                break;
        }

        if (diagnostics && scan.Diagnostics.Count > 0)
        {
            Console.WriteLine("Diagnostics:");
            foreach (string line in scan.Diagnostics)
            {
                Console.WriteLine($"  {line}");
            }

            Console.WriteLine();
        }

        Console.WriteLine($"Last scan: {DateTimeOffset.Now:HH:mm:ss} · Ctrl+C to stop cleanly");
    }

    private static void Render(OpponentRoster roster)
    {
        Console.WriteLine($"{roster.Label.ToUpperInvariant()} · {roster.Members.Count} Pokémon");
        foreach (OpponentPokemon pokemon in roster.Members)
        {
            string speciesName = DisplayCartridgeName(pokemon.SpeciesName);
            string nickname = string.Equals(
                pokemon.Nickname, pokemon.SpeciesName, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(pokemon.Nickname)
                ? string.Empty
                : $" “{pokemon.Nickname}”";

            Console.WriteLine(
                $"  {pokemon.Slot}. {speciesName}{nickname}  Lv.{pokemon.Level}  "
                + $"HP {pokemon.CurrentHp}/{pokemon.MaximumHp}  {pokemon.Status}");
            Console.WriteLine(
                $"     {pokemon.Nature} · {pokemon.Ability} · Item: {pokemon.HeldItem}");

            string moves = pokemon.Moves.Count == 0
                ? "None"
                : string.Join(", ", pokemon.Moves.Select(move =>
                    $"{move.Name} ({move.CurrentPp}/{move.MaximumPp})"));
            Console.WriteLine($"     Moves: {moves}");
        }

        Console.WriteLine();
    }

    private static string DisplayCartridgeName(string value)
    {
        string title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        return title.EndsWith("'D", StringComparison.Ordinal) ? title[..^1] + "d" : title;
    }
}
