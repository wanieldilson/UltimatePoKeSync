using UltimatePoKeSync.Providers.MelonDs;
using UltimatePoKeSync.SoulSilverOpponent;

CommandLineOptions options;
try
{
    options = CommandLineOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Use --help for usage.");
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(
        """
        Read opponent Pokémon from the USA/Australia Pokémon SoulSilver ROM in melonDS.

        Usage:
          dotnet run --project tools/UltimatePoKeSync.SoulSilverOpponent -- [options]

        Options:
          --port PORT       Use only this melonDS GDB port (default: try 3334, then 3333)
          --interval MS     Scan interval from 250 to 10000 ms (default: 1000)
          --once            Scan once and exit
          --diagnostics     Show candidate addresses and validation results
          --no-clear        Append output instead of redrawing the terminal
          --help, -h        Show this help
        """);
    return 0;
}

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    stopping.Cancel();
};

await using var memory = new MelonDsMemoryReader(port: options.Port);
var scanner = new SoulSilverOpponentScanner(memory);
string? previous = null;

try
{
    while (!stopping.IsCancellationRequested)
    {
        OpponentScan scan = await scanner.ScanAsync(stopping.Token);
        string signature = $"{scan.State}:{scan.GameCode}:{scan.RootAddress}:{scan.Signature}";
        if (options.Once || options.Diagnostics || !string.Equals(signature, previous, StringComparison.Ordinal))
        {
            ConsoleOutput.Render(scan, options.Diagnostics, options.Clear && !options.Once);
            previous = signature;
        }

        if (options.Once)
        {
            break;
        }

        await Task.Delay(options.Interval, stopping.Token);
    }
}
catch (OperationCanceledException)
{
    // Ctrl+C: disposing the reader sends a GDB detach so melonDS keeps running.
}

return 0;
