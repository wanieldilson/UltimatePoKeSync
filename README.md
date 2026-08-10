# UltimatePoKeSync

App desktop cross-platform (Windows, Linux, macOS incluso Apple Silicon) che analizza in
tempo reale la squadra Pokémon del giocatore leggendo direttamente la RAM di un
emulatore. Nessun inserimento manuale: colleghi lo script, giochi, e il team compare.

Calcola debolezze e resistenze aggregate della squadra e suggerisce EV, natura, mosse e
oggetto per ogni Pokémon, con due profili di analisi — **playthrough** e **competitivo**.

## Stato

In sviluppo iniziale. Target corrente: **Gen 3 / Pokémon Emerald (U)** tramite **mGBA**.
L'architettura è multi-emulatore e multi-generazione fin dal primo commit.

## Come funziona

```
mGBA + script Lua  --TCP 127.0.0.1:8888-->  app .NET  -->  PKHeX.Core  -->  analisi  -->  UI Avalonia
   (byte grezzi)                             (client)      (parsing)
```

Lo script Lua non interpreta nulla: spedisce i byte grezzi del party e l'identità del
gioco. Tutta la decodifica avviene lato C#. Aggiungere un altro emulatore significa
scrivere un nuovo script, non toccare la logica dell'app.

## Requisiti

- .NET 10 SDK
- mGBA 0.10.5 o successivo (lo scripting Lua esiste dalla 0.10.0)
- Una ROM Gen 3 di tua proprietà — non è inclusa e non lo sarà mai

## Avvio rapido

1. In mGBA: `Tools` → `Scripting…` → `File` → `Load script…` →
   [`emulator-scripts/mgba/ups_bridge.lua`](emulator-scripts/mgba/ups_bridge.lua)
2. Carica la ROM (prima o dopo, indifferente).
3. `dotnet run --project src/UltimatePoKeSync.Cli`

Istruzioni dettagliate e diagnostica dei problemi:
[`emulator-scripts/mgba/README.md`](emulator-scripts/mgba/README.md).

## Struttura

| Percorso                  | Contenuto |
| ------------------------- | --------- |
| `emulator-scripts/mgba/`  | Script Lua che legge la RAM e la spedisce via TCP |
| `src/…Contracts/`         | Il confine dell'architettura: byte grezzi, non Pokémon. Zero dipendenze |
| `src/…Providers.MGba/`    | Client TCP con reconnect. Non conosce PKHeX né le regole di gioco |
| `src/…Parsing/`           | Byte → Pokémon via PKHeX. L'unico progetto che dipende da PKHeX |
| `src/…GameData/`          | Type chart per generazione, nature, euristiche |
| `src/…Analysis/`          | Coverage del team, ruoli, suggerimenti |
| `src/…Cli/`               | Console di diagnostica headless |
| `src/…App/`               | UI Avalonia |

## Documentazione

- [`docs/DECISIONS.md`](docs/DECISIONS.md) — registro di ogni scelta di progetto, con le
  alternative valutate e il perché. È il punto di partenza per capire il codice.
- [`docs/protocol.md`](docs/protocol.md) — protocollo fra emulatore e app.

## Licenza

GPLv3. Vedi [`LICENSE`](LICENSE).

Il progetto usa [PKHeX.Core](https://github.com/kwsch/PKHeX) (GPL-3.0-or-later) per il
parsing delle strutture dati Pokémon; il copyleft si estende per linking, quindi l'intera
app è GPLv3. Vedi D-007 nel decision log.
