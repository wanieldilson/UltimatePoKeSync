# Protocollo bridge emulatore → app

Versione corrente: **1**

Ogni provider parla questo protocollo, indipendentemente dall'emulatore. È il motivo per
cui aggiungere BizHawk o DeSmuME costa uno script e non un refactoring (D-006).

## Trasporto

TCP su loopback. **Lo script dell'emulatore è il server**, l'app è il client (D-003).

- Indirizzo di default: `127.0.0.1:8888`
- Un messaggio per riga, terminato da `\n`
- Codifica UTF-8, JSON su una sola riga (nessun newline interno)
- L'app non invia comandi. Lo script svuota comunque il buffer di ricezione: se non lo
  facesse, il buffer del socket si riempirebbe e la connessione si bloccherebbe.

Alla connessione, lo script invia **subito** l'ultimo stato noto, se ne ha uno. Senza
questo, un'app avviata a partita ferma resterebbe vuota finché il giocatore non cambia
qualcosa nella squadra.

## Messaggio `party`

Emesso solo quando i byte della squadra o il conteggio **cambiano davvero** — non a ogni
frame. Lo script confronta un FNV-1a a 32 bit dei byte grezzi.

```json
{
  "v": 1,
  "type": "party",
  "seq": 42,
  "frame": 123456,
  "game": { "code": "BPEE", "title": "POKEMON EMER", "rev": 0, "gen": 3 },
  "count": 3,
  "slotSize": 100,
  "slots": 6,
  "data": "<base64>"
}
```

| Campo        | Tipo   | Significato |
| ------------ | ------ | ----------- |
| `v`          | int    | Versione del protocollo. L'app rifiuta le versioni che non conosce. |
| `type`       | string | Discriminatore del messaggio. |
| `seq`        | int    | Contatore monotono. Permette di rilevare messaggi persi o fuori ordine. |
| `frame`      | int    | Frame dell'emulatore alla cattura, per la diagnostica. |
| `game.code`  | string | Game code a 4 caratteri dall'header (`BPEE`, `BPRE`, …). Vedi D-005. |
| `game.title` | string | Titolo interno della ROM. |
| `game.rev`   | int    | Byte di revisione dell'header, offset `0xBC`. |
| `game.gen`   | int    | Generazione. |
| `count`      | int    | Pokémon in squadra secondo l'emulatore, 0-6. **Da trattare come indicativo.** |
| `slotSize`   | int    | Byte per slot. Gen 3: 100. |
| `slots`      | int    | Numero di slot nel blob. Sempre 6 in Gen 3. |
| `data`       | string | Base64 di `slotSize * slots` byte, così come stanno in RAM. |

### Perché `count` non è affidabile

È letto da un singolo byte che può essere campionato mentre il gioco lo sta aggiornando.
L'app lo usa come limite superiore e valida comunque ogni slot per conto proprio
(checksum + stabilità su due letture). Vedi D-008.

### Perché i byte sono grezzi

In Gen 3 i dati in RAM sono cifrati con `PID xor OTID` e hanno le quattro sottostrutture
permutate in base al PID. Lo script **non** li tocca: il campo `data` contiene esattamente
ciò che c'è in memoria. La decodifica è compito di `UltimatePoKeSync.Parsing` (D-007).

## Gestione degli errori

Lo script non invia messaggi di errore sul socket: registra su console mGBA e smette di
produrre `party`. Casi:

- **ROM non riconosciuta** → nessun messaggio. Lo script rifiuta di leggere invece di
  indovinare una mappa di memoria, perché indovinare produce Pokémon plausibili ma
  inventati (D-005).
- **Porta occupata** → il server non parte, con un messaggio che spiega come cambiare
  `UPS_PORT`. Non c'è auto-incremento: il client deve sapere dove connettersi.
- **Client caduto** → rimosso dalla lista, l'emulazione continua.

## Compatibilità futura

Il campo `v` è il punto di rottura esplicito. Aggiungere campi è retrocompatibile e non
incrementa `v`; cambiare il significato di un campo esistente lo incrementa.

Per generazioni in cui la squadra non è una regione contigua, il messaggio `party`
guadagnerà un campo opzionale `regions` con blocchi denominati, mantenendo `data` per il
caso contiguo.
