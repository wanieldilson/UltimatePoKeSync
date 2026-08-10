# Bridge mGBA

`ups_bridge.lua` legge la squadra dalla RAM e la spedisce all'app via TCP.

## Requisiti

mGBA **0.10.0 o successivo** — lo scripting Lua non esiste nelle versioni precedenti.
Testato con 0.10.5.

## Uso

1. Avvia mGBA e carica la tua ROM Gen 3.
2. `Tools` → `Scripting…`
3. Nella finestra di scripting: `File` → `Load script…` e scegli `ups_bridge.lua`.
4. Nella console dovresti leggere:

   ```
   [UltimatePoKeSync] in ascolto su 127.0.0.1:8888
   [UltimatePoKeSync] gioco riconosciuto: Emerald (USA) [BPEE] rev0
   ```

5. Avvia l'app:

   ```
   dotnet run --project src/UltimatePoKeSync.Cli
   ```

L'ordine non conta: lo script funziona sia caricato prima della ROM sia dopo, e l'app può
partire prima di mGBA — resta in attesa e si connette da sola.

## Giochi supportati

| Game code | Gioco               |
| --------- | ------------------- |
| `BPEE`    | Emerald (USA)       |
| `BPRE`    | FireRed (USA)       |
| `BPGE`    | LeafGreen (USA)     |
| `AXVE`    | Ruby (USA)          |
| `AXPE`    | Sapphire (USA)      |

Le versioni non USA hanno indirizzi RAM diversi e non sono ancora mappate. Su una ROM non
riconosciuta lo script **rifiuta di leggere** e lo dice in console, invece di indovinare:
leggere con la mappa sbagliata produrrebbe Pokémon plausibili ma inventati.

## Problemi comuni

**`porta 8888 gia' in uso`** — un'altra istanza di mGBA sta già eseguendo il bridge, o un
altro programma occupa la porta. Cambia `UPS_PORT` in cima allo script, ricaricalo, e
avvia l'app con `--port <la stessa porta>`.

**`gioco non supportato`** — la ROM non è in tabella. Controlla il game code stampato in
console.

**Niente in console** — lo script non è stato caricato: la finestra di scripting deve
restare aperta.

## Cosa fa (e cosa non fa)

Legge il byte del conteggio squadra e i 600 byte dei sei slot, e li spedisce **grezzi**,
in base64. Non decifra, non valida checksum, non traduce ID: tutto questo avviene lato
C#, una volta sola, condiviso da tutti gli emulatori (vedi D-006 in `docs/DECISIONS.md`).

Controlla la squadra 15 volte al secondo e trasmette solo quando i byte cambiano davvero.
Un cambiamento viene inviato solo dopo essere stato confermato da una seconda lettura
identica, per non trasmettere uno stato catturato mentre il gioco stava scrivendo in
memoria (D-008).

Protocollo completo: [`docs/protocol.md`](../../docs/protocol.md).
