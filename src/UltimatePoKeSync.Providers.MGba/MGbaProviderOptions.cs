namespace UltimatePoKeSync.Providers.MGba;

/// <summary>
/// Configurazione del provider mGBA.
/// </summary>
public sealed record MGbaProviderOptions
{
    /// <summary>
    /// Host su cui ascolta lo script Lua. Loopback: il bridge non e' pensato per essere
    /// esposto in rete e non ha alcuna autenticazione.
    /// </summary>
    public string Host { get; init; } = "127.0.0.1";

    /// <summary>Deve coincidere con <c>UPS_PORT</c> in <c>ups_bridge.lua</c>.</summary>
    public int Port { get; init; } = 8888;

    /// <summary>Attesa prima del primo tentativo di riconnessione.</summary>
    public TimeSpan InitialReconnectDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Tetto del backoff esponenziale. Con mGBA chiuso il provider ritenta a questo
    /// ritmo a tempo indefinito: e' il caso normale di "app aperta, emulatore ancora no".
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Timeout del singolo tentativo di connessione.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Versione del protocollo che questo client sa interpretare.</summary>
    public int SupportedProtocolVersion { get; init; } = 1;
}
