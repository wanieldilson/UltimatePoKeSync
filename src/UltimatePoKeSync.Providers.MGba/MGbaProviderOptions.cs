namespace UltimatePoKeSync.Providers.MGba;

/// <summary>
/// Configuration for the mGBA provider.
/// </summary>
public sealed record MGbaProviderOptions
{
    /// <summary>
    /// Host the Lua script listens on. Loopback: the bridge is not meant to be exposed to
    /// a network and has no authentication whatsoever.
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Must match <c>UPS_PORT</c> in <c>ups_bridge.lua</c>.</summary>
    public int Port { get; init; } = 8888;

    /// <summary>Delay before the first reconnection attempt.</summary>
    public TimeSpan InitialReconnectDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Ceiling for the exponential backoff. With mGBA closed the provider retries at this
    /// rate indefinitely: that is the normal "app open, emulator not yet" case.
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Timeout for a single connection attempt.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Protocol version this client understands.</summary>
    public int SupportedProtocolVersion { get; init; } = 1;
}
