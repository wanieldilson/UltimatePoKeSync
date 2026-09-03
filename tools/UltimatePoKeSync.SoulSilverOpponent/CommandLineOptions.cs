namespace UltimatePoKeSync.SoulSilverOpponent;

internal sealed record CommandLineOptions(
    int? Port,
    TimeSpan Interval,
    bool Once,
    bool Diagnostics,
    bool Clear,
    bool ShowHelp)
{
    public static CommandLineOptions Parse(string[] args)
    {
        int? port = null;
        int intervalMilliseconds = 1000;
        bool once = false;
        bool diagnostics = false;
        bool clear = true;
        bool help = false;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--port":
                    port = ParseInt(args, ref index, "--port", 1, 65535);
                    break;
                case "--interval":
                    intervalMilliseconds = ParseInt(args, ref index, "--interval", 250, 10000);
                    break;
                case "--once":
                    once = true;
                    break;
                case "--diagnostics":
                    diagnostics = true;
                    break;
                case "--no-clear":
                    clear = false;
                    break;
                case "--help" or "-h":
                    help = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }

        return new CommandLineOptions(
            port,
            TimeSpan.FromMilliseconds(intervalMilliseconds),
            once,
            diagnostics,
            clear,
            help);
    }

    private static int ParseInt(
        IReadOnlyList<string> args,
        ref int index,
        string option,
        int minimum,
        int maximum)
    {
        if (++index >= args.Count
            || !int.TryParse(args[index], out int result)
            || result < minimum
            || result > maximum)
        {
            throw new ArgumentException(
                $"{option} requires a number from {minimum} to {maximum}.");
        }

        return result;
    }
}
