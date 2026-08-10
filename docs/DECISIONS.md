# Decision log — UltimatePoKeSync

Registro di **ogni scelta di progetto**, con alternative valutate e motivazione.
Va aggiornato nello stesso commit del cambiamento che descrive.

Formato: `D-nnn` progressivo. Stato: `Accettata` · `Superata da D-xxx` · `Aperta`.

---

## D-001 — Emulatore di riferimento: mGBA (non BizHawk)

**Stato:** Accettata · 2026-08-10

BizHawk dipende da WinForms 64-bit e non ha supporto ufficiale macOS, men che meno
Apple Silicon. Dato che macOS è un target di prima classe, non può essere l'emulatore
di partenza.

mGBA 0.10.5 ha build macOS universale (Intel + ARM), Windows e Linux (AppImage anche
arm64), e ha lo scripting Lua stabile dalla 0.10.0.

**Alternative valutate:** BizHawk (scartato: no macOS), DeSmuME (no scripting Lua su
tutte le piattaforme), RetroArch + network command interface (protocollo troppo povero,
non legge range arbitrari in modo efficiente).

**Conseguenza:** il primo target è GBA, quindi Gen 3. Vedi D-004.

---

## D-002 — Trasporto Lua → app: socket TCP, non file JSON

**Stato:** Accettata · 2026-08-10

L'idea iniziale prevedeva "socket TCP **oppure** file JSON aggiornato". L'opzione file
**non è realizzabile**: l'API di scripting di mGBA non espone alcun I/O su filesystem.
Non esiste `io.open`; le sole operazioni su file sono `loadFile`, `loadSaveFile` e la
gestione degli stati. Quindi TCP è l'unica strada.

Fonte: <https://mgba.io/docs/scripting.html>

**Conseguenza:** vedi D-003 per la direzione della connessione.

---

## D-003 — Lo script Lua fa da server, l'app C# da client

**Stato:** Accettata · 2026-08-10

L'API socket di mGBA espone `socket.bind()` / `listen()` / `accept()` / `poll()` /
`hasdata()`, tutte non bloccanti, più gli eventi `"received"` e `"error"`.
`socket.connect()` è invece documentato come **bloccante**: usarlo dentro il callback di
frame bloccherebbe l'emulazione a ogni tentativo di riconnessione fallito.

Quindi: Lua ascolta su `127.0.0.1:8888` (porta configurabile), l'app C# si connette come
client e gestisce il reconnect con backoff. Il retry sta nel processo che può
permetterselo.

**Conseguenza:** più istanze di mGBA richiedono porte diverse. La porta è un parametro
dello script e della configurazione dell'app.

---

## D-004 — Generazione di partenza: Gen 3, gioco di riferimento Pokémon Emerald (U)

**Stato:** Accettata · 2026-08-10

Conseguenza diretta di D-001 (mGBA = GBA). Fra i giochi Gen 3, Emerald (U) è quello con
più materiale di riferimento pubblico (Ironmon Tracker, pokebot-bizhawk, Archipelago),
il che rende molto più veloce diagnosticare una lettura sbagliata.

**Indirizzi RAM verificati** (incrocio fra Data Crystal e `GameSettings.lua` di
40Cakes/pokebot-bizhawk, tool in produzione):

| Gioco (U)           | Party data   | Party count  | Dominio |
| ------------------- | ------------ | ------------ | ------- |
| Emerald             | `0x020244EC` | `0x020244E9` | EWRAM   |
| FireRed / LeafGreen | `0x02024284` | `0x02024029` | EWRAM   |
| Ruby / Sapphire     | `0x03004360` | `0x03004350` | IWRAM   |

Struttura: 6 slot contigui da 100 byte (80 stored + 20 stat di battaglia).

Le revisioni JP hanno indirizzi diversi. Vedi D-005.

---

## D-005 — Nessun indirizzo hardcoded: identificazione del gioco a runtime

**Stato:** Accettata · 2026-08-10

Gli indirizzi cambiano per gioco **e per regione**. Hardcodare quelli di Emerald (U)
significherebbe leggere spazzatura silenziosamente su qualunque altra ROM.

Lo script Lua legge il game code dall'header della cartuccia a `0x080000AC`
(`BPEE` Emerald, `BPRE` FireRed, `BPGE` LeafGreen, `AXVE` Ruby, `AXPE` Sapphire) e
seleziona la tabella indirizzi corrispondente. Se il game code è sconosciuto, lo script
**rifiuta di leggere** e lo segnala, invece di indovinare.

Il game code viaggia in ogni messaggio verso l'app, così anche il lato C# sa sempre che
gioco sta interpretando.

---

## D-006 — Il layer provider trasporta byte grezzi, non Pokémon

**Stato:** Accettata · 2026-08-10

È la scelta che rende l'astrazione multi-emulatore reale invece che nominale.

Lo script Lua **non parsa niente**: spedisce i byte grezzi del party, il conteggio e
l'identità del gioco. Tutta la decodifica (decrypt, unshuffle, checksum, mapping degli
ID) avviene in C#, una volta sola, condivisa da tutti i provider.

Aggiungere BizHawk o DeSmuME domani costa ~150 righe di Lua e zero righe di logica di
dominio. Se invece ogni script parsasse i suoi Pokémon, ogni nuovo emulatore
duplicherebbe — e sfaserebbe — la stessa logica.

Il contratto vive in `UltimatePoKeSync.Contracts` ed è l'unico progetto condiviso fra
provider e parsing.

---

## D-007 — Parsing tramite PKHeX.Core; l'app è licenziata GPLv3

**Stato:** Accettata · 2026-08-10

PKHeX.Core (NuGet, ultima 26.7.7) ha target `net10.0`, **zero dipendenze** e nessun
riferimento a WinForms: la GUI di PKHeX è un progetto separato. È cross-platform senza
riserve.

Il costruttore `PK3(Memory<byte>)` **decripta automaticamente** i dati (`DecryptParty()`),
quindi i 100 byte letti dalla RAM diventano direttamente un oggetto con `Species`,
`IV_*`, `EV_*`, `Nature`, `Ability`, `Move1..4`, `HeldItem`, `Stat_Level`. In più
`PersonalTable3` fornisce stat base, tipi e abilità, e i `Learnset` le mosse imparabili.

Evita di reimplementare a mano decifratura XOR, permutazione delle sottostrutture e
checksum — e soprattutto evita di rifarlo per ogni generazione futura, dove il formato è
molto più complesso.

**Costo accettato:** PKHeX.Core è **GPL-3.0-or-later** e il copyleft si estende per
linking. UltimatePoKeSync è quindi licenziata **GPLv3**. Scelta confermata da Roberto il
2026-08-10, valutata l'alternativa di un parser Gen 3 proprietario (~250 righe) con
licenza permissiva, scartata perché non scala alle generazioni successive.

**Limite noto:** PKHeX.Core non contiene dati *competitivi* (tier, spread comuni, item da
metagame). Quelli restano dataset nostri. Vedi D-009.

---

## D-008 — Difese contro le letture inconsistenti della RAM

**Stato:** Accettata · 2026-08-10

Leggendo a ogni frame si può catturare uno snapshot **mentre il gioco sta scrivendo** in
quella regione (torn read), ottenendo un Pokémon che non è mai esistito.

Tre difese, tutte lato C#:

1. **Checksum**: la struttura Gen 3 ha un checksum a offset `0x1C`; PKHeX lo espone come
   `ChecksumValid`. Snapshot non valido → scartato, non mostrato.
2. **Stabilità su due letture**: uno snapshot è accettato solo se l'hash dei byte grezzi
   è identico in due letture consecutive.
3. **Deduplica**: si emette un evento solo quando l'hash del party cambia davvero,
   evitando di ricalcolare l'analisi 60 volte al secondo.

---

## D-009 — L'analisi è generation-aware fin dal primo giorno

**Stato:** Accettata · 2026-08-10

Le regole Gen 3 differiscono da quelle moderne in modi che **cambiano il risultato dei
suggerimenti**, non solo i numeri:

- **17 tipi, niente Fairy.** La type chart deve essere per generazione.
- **Lo split fisico/speciale è per TIPO, non per mossa.** In Gen 3 qualunque mossa Acqua
  è speciale e qualunque mossa Lotta è fisica, a prescindere dalla mossa. La deduzione
  del ruolo (attaccante fisico vs speciale) e di conseguenza natura ed EV consigliati
  seguono regole diverse da Gen 4+.
- **Nessuna abilità nascosta**; l'abilità è un singolo bit.
- **EV**: 510 totali, cap 255 per stat (252 è solo la soglia di efficienza).

Trattare queste come "dettagli da sistemare dopo" costringerebbe a riscrivere il motore
di suggerimenti quando si aggiunge Gen 4. Le regole stanno quindi dietro un'astrazione
per generazione fin dall'inizio.

---

## D-010 — Due profili di analisi: playthrough e competitivo

**Stato:** Accettata · 2026-08-10

Scelta di Roberto, 2026-08-10.

- **Playthrough**: mosse già disponibili *ora* nel learnset, item ottenibili nel gioco,
  EV realistici, copertura contro Capipalestra e Lega.
- **Competitivo**: spread 252/252/4, benchmark di velocità, natura/item/EV da metagame.

Sono due set di euristiche sopra lo **stesso** motore di analisi, selezionabili nella UI.
Il motore calcola i fatti (ruolo, coverage, stat proiettate); il profilo decide *cosa
consigliare* a partire da quei fatti. La separazione è vincolante fin dall'inizio,
altrimenti le due modalità si intrecciano e diventano impossibili da mantenere.

---

## D-011 — Stack applicativo: .NET 10 + Avalonia

**Stato:** Accettata · 2026-08-10

.NET 10 è imposto da PKHeX.Core 26.x, che ha target `net10.0` (D-007).

Avalonia per la UI: gira nativamente su Windows, Linux e macOS incluso Apple Silicon,
a differenza di WPF. MVVM con CommunityToolkit.Mvvm.

**Alternative valutate:** MAUI (supporto Linux desktop assente), Uno Platform (più
complesso da configurare, nessun vantaggio qui), UI web + backend locale (aggiunge un
browser e un layer HTTP per nessun beneficio in un'app locale single-user).
