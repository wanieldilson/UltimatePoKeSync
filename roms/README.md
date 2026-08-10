# roms/

Metti qui le ROM per lo sviluppo e i test.

**Il contenuto di questa cartella è escluso da git** (`roms/*` in `.gitignore`, con
l'eccezione di questo file). Nessuna ROM, nessun savefile e nessuno savestate finirà mai
in un commit.

Non versionare ROM: non sono nostre da distribuire.

## Nome atteso

Qualsiasi nome va bene, ma per comodità:

```
roms/emerald.gba
```

Il gioco viene riconosciuto dal game code nell'header (`BPEE` per Emerald USA), non dal
nome del file.
