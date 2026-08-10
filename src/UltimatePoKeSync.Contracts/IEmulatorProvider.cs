namespace UltimatePoKeSync.Contracts;

/// <summary>
/// Sorgente di snapshot grezzi. Un'implementazione per emulatore: mGBA oggi, BizHawk o
/// DeSmuME domani. Vedi D-006.
/// </summary>
/// <remarks>
/// Il contratto e' deliberatamente povero: nessun concetto di Pokemon, di generazione o
/// di indirizzo di memoria. E' quello che rende sostituibile l'emulatore senza toccare
/// il resto dell'app.
/// </remarks>
public interface IEmulatorProvider : IAsyncDisposable
{
    /// <summary>Nome leggibile, mostrato nella UI. Es. "mGBA".</summary>
    string Name { get; }

    /// <summary>Stato corrente della connessione.</summary>
    EmulatorConnectionState State { get; }

    /// <summary>Segnalato a ogni transizione di stato.</summary>
    event EventHandler<EmulatorConnectionState>? StateChanged;

    /// <summary>
    /// Flusso di snapshot, uno per ogni cambiamento rilevato nella squadra.
    /// </summary>
    /// <remarks>
    /// L'implementazione gestisce la riconnessione internamente: il flusso non termina
    /// perche' l'emulatore e' stato chiuso, si limita a smettere di produrre elementi
    /// finche' non torna disponibile. Termina solo alla cancellazione del token.
    /// </remarks>
    IAsyncEnumerable<RawPartySnapshot> ReadSnapshotsAsync(CancellationToken cancellationToken);
}

public enum EmulatorConnectionState
{
    /// <summary>Mai avviato.</summary>
    Idle,

    /// <summary>Tentativo di connessione in corso.</summary>
    Connecting,

    /// <summary>Connesso, ma nessuna ROM riconosciuta caricata.</summary>
    ConnectedNoGame,

    /// <summary>Connesso e in ricezione di snapshot.</summary>
    Streaming,

    /// <summary>Connessione persa; il provider sta ritentando.</summary>
    Reconnecting,

    /// <summary>Errore non recuperabile: il provider ha smesso di ritentare.</summary>
    Faulted,
}
