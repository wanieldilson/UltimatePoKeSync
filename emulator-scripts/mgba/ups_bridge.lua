--[[
  UltimatePoKeSync — bridge mGBA -> app

  Legge la squadra dalla RAM e la spedisce via TCP come byte GREZZI.
  Questo script non interpreta nulla: non decifra, non valida checksum, non
  traduce ID di specie. Tutta la decodifica avviene lato C#, una volta sola,
  condivisa da tutti gli emulatori. Vedi D-006 nel decision log.

  USO
    1. mGBA -> Tools -> Scripting... -> File -> Load script...
    2. Seleziona questo file.
    3. Avvia la ROM (o caricala prima, funziona in entrambi gli ordini).
    4. L'app si connette a 127.0.0.1:8888.

  PROTOCOLLO
    Una riga JSON per messaggio, terminata da "\n". Vedi docs/protocol.md.

  COMPATIBILITA'
    mGBA 0.10.0+ (lo scripting Lua non esiste prima).
    Questo file e' volutamente monolitico: mGBA non documenta il supporto a
    `require` per moduli locali, quindi spezzarlo in piu' file lo renderebbe
    fragile e scomodo da caricare. Vedi D-012.
]]

--------------------------------------------------------------------------------
-- Configurazione
--------------------------------------------------------------------------------

local UPS_PORT = 8888

-- Ogni quanti frame controllare la squadra. 60/4 = 15 volte al secondo: piu' che
-- sufficiente per una squadra, e a 60 Hz sprecheremmo CPU per nulla.
local UPS_POLL_INTERVAL = 4

local UPS_PROTOCOL_VERSION = 1

--------------------------------------------------------------------------------
-- Mappa di memoria Gen 3
--
-- Indirizzi verificati su tre fonti indipendenti: Data Crystal, GameSettings.lua
-- di 40Cakes/pokebot-bizhawk e res/scripts/pokemon.lua distribuito da mGBA stesso.
-- Vedi D-004.
--
-- Le revisioni Gen 3 (FireRed Rev 1, Ruby Rev 1/2...) condividono gli stessi
-- indirizzi, quindi il game code basta a identificare la mappa. Non sara' vero
-- per tutte le generazioni: per questo la chiave resta il game code completo.
--------------------------------------------------------------------------------

local GAMES = {
	["AXVE"] = { name = "Ruby (USA)",      gen = 3, party = 0x03004360, count = 0x03004350 },
	["AXPE"] = { name = "Sapphire (USA)",  gen = 3, party = 0x03004360, count = 0x03004350 },
	["BPEE"] = { name = "Emerald (USA)",   gen = 3, party = 0x020244EC, count = 0x020244E9 },
	["BPRE"] = { name = "FireRed (USA)",   gen = 3, party = 0x02024284, count = 0x02024029 },
	["BPGE"] = { name = "LeafGreen (USA)", gen = 3, party = 0x02024284, count = 0x02024029 },
}

local GEN3_SLOT_SIZE = 100 -- 80 byte "stored" + 20 byte di statistiche di battaglia
local GEN3_SLOT_COUNT = 6

--------------------------------------------------------------------------------
-- Stato
--------------------------------------------------------------------------------

local server = nil
local clients = {}
local nextClientId = 1

local game = nil        -- voce di GAMES per la ROM corrente, o nil
local gameCode = nil    -- es. "BPEE"
local gameTitle = ""
local gameRevision = 0

local frameCounter = 0
local sequence = 0
local lastHash = nil
local lastCount = -1
local lastPayload = nil -- ultimo messaggio inviato, per i client che si connettono dopo

--------------------------------------------------------------------------------
-- Utility
--------------------------------------------------------------------------------

local B64_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

--- Codifica una stringa di byte in base64.
local function base64(data)
	local out = {}
	local n = #data
	local i = 1

	while i + 2 <= n do
		local a, b, c = data:byte(i, i + 2)
		local triple = a << 16 | b << 8 | c
		out[#out + 1] = B64_ALPHABET:sub((triple >> 18 & 63) + 1, (triple >> 18 & 63) + 1)
			.. B64_ALPHABET:sub((triple >> 12 & 63) + 1, (triple >> 12 & 63) + 1)
			.. B64_ALPHABET:sub((triple >> 6 & 63) + 1, (triple >> 6 & 63) + 1)
			.. B64_ALPHABET:sub((triple & 63) + 1, (triple & 63) + 1)
		i = i + 3
	end

	local remaining = n - i + 1
	if remaining == 1 then
		local a = data:byte(i)
		local triple = a << 16
		out[#out + 1] = B64_ALPHABET:sub((triple >> 18 & 63) + 1, (triple >> 18 & 63) + 1)
			.. B64_ALPHABET:sub((triple >> 12 & 63) + 1, (triple >> 12 & 63) + 1)
			.. "=="
	elseif remaining == 2 then
		local a, b = data:byte(i, i + 1)
		local triple = a << 16 | b << 8
		out[#out + 1] = B64_ALPHABET:sub((triple >> 18 & 63) + 1, (triple >> 18 & 63) + 1)
			.. B64_ALPHABET:sub((triple >> 12 & 63) + 1, (triple >> 12 & 63) + 1)
			.. B64_ALPHABET:sub((triple >> 6 & 63) + 1, (triple >> 6 & 63) + 1)
			.. "="
	end

	return table.concat(out)
end

--- FNV-1a 32 bit. Serve solo a capire se i byte sono cambiati, non e' crittografico.
local function hash32(data)
	local h = 2166136261
	for _, byte in ipairs({ data:byte(1, #data) }) do
		h = h ~ byte
		h = (h * 16777619) & 0xFFFFFFFF
	end
	return h
end

--- Escape del minimo indispensabile: nei nostri campi entrano solo titoli ROM e base64.
local function jsonEscape(text)
	return (text:gsub('[%c"\\]', function(c)
		if c == '"' then return '\\"' end
		if c == '\\' then return '\\\\' end
		return string.format('\\u%04x', c:byte())
	end))
end

local function log(message)
	console:log("[UltimatePoKeSync] " .. message)
end

local function logError(message)
	console:error("[UltimatePoKeSync] " .. message)
end

--------------------------------------------------------------------------------
-- Rete
--------------------------------------------------------------------------------

local function dropClient(id, reason)
	local client = clients[id]
	if not client then return end
	clients[id] = nil
	pcall(function() client:close() end)
	log("client " .. id .. " disconnesso (" .. reason .. ")")
end

local function broadcast(line)
	for id, client in pairs(clients) do
		local ok, err = client:send(line)
		if not ok and err ~= socket.ERRORS.AGAIN then
			dropClient(id, tostring(err))
		end
	end
end

--- Svuota il buffer di ricezione. Non accettiamo comandi: se non leggessimo,
--- il buffer del socket si riempirebbe e la connessione si bloccherebbe.
local function drainClient(id)
	local client = clients[id]
	if not client then return end
	while true do
		local data, err = client:receive(1024)
		if not data then
			if err and err ~= socket.ERRORS.AGAIN then
				dropClient(id, tostring(err))
			end
			return
		end
	end
end

local function acceptClient()
	local client, err = server:accept()
	if err then
		logError("accept fallita: " .. tostring(err))
		return
	end

	local id = nextClientId
	nextClientId = id + 1
	clients[id] = client

	client:add("received", function() drainClient(id) end)
	client:add("error", function() dropClient(id, "errore socket") end)

	log("client " .. id .. " connesso")

	-- Stato corrente subito, senza aspettare il prossimo cambiamento: altrimenti
	-- un'app avviata a partita ferma resterebbe vuota a tempo indefinito.
	if lastPayload then
		client:send(lastPayload)
	end
end

local function startServer()
	local err
	server, err = socket.bind(nil, UPS_PORT)
	if err then
		if err == socket.ERRORS.ADDRESS_IN_USE then
			logError("porta " .. UPS_PORT .. " gia' in uso. Un'altra istanza di mGBA sta "
				.. "gia' eseguendo il bridge? Cambia UPS_PORT in cima allo script "
				.. "(e nell'app) e ricarica.")
		else
			logError("bind fallita: " .. tostring(err))
		end
		server = nil
		return
	end

	local ok
	ok, err = server:listen()
	if err then
		logError("listen fallita: " .. tostring(err))
		server:close()
		server = nil
		return
	end

	server:add("received", acceptClient)
	log("in ascolto su 127.0.0.1:" .. UPS_PORT)
end

--------------------------------------------------------------------------------
-- Rilevamento del gioco
--------------------------------------------------------------------------------

local function detectGame()
	game, gameCode, gameTitle, gameRevision = nil, nil, "", 0
	lastHash, lastCount, lastPayload = nil, -1, nil

	if not emu then return end

	-- getGameCode() restituisce il codice con prefisso di piattaforma, es. "AGB-BPEE".
	local raw = emu:getGameCode()
	if not raw or raw == "" then
		logError("nessun game code nell'header: ROM non standard o non caricata.")
		return
	end

	gameCode = raw:match("([A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9])$") or raw
	gameTitle = emu:getGameTitle() or ""
	gameRevision = emu:read8(0x080000BC) -- byte "software version" dell'header GBA

	game = GAMES[gameCode]
	if not game then
		-- Rifiutiamo esplicitamente invece di indovinare: leggere con la mappa
		-- sbagliata produrrebbe Pokemon plausibili ma inventati. Vedi D-005.
		logError("gioco non supportato: " .. tostring(raw) .. " (\"" .. gameTitle .. "\"). "
			.. "Nessuna lettura verra' effettuata.")
		return
	end

	log("gioco riconosciuto: " .. game.name .. " [" .. gameCode .. "] rev" .. gameRevision)
end

--------------------------------------------------------------------------------
-- Lettura e invio
--------------------------------------------------------------------------------

local function buildPayload(count, data)
	sequence = sequence + 1
	return string.format(
		'{"v":%d,"type":"party","seq":%d,"frame":%d,"game":{"code":"%s","title":"%s","rev":%d,"gen":%d},'
			.. '"count":%d,"slotSize":%d,"slots":%d,"data":"%s"}\n',
		UPS_PROTOCOL_VERSION,
		sequence,
		frameCounter,
		jsonEscape(gameCode),
		jsonEscape(gameTitle),
		gameRevision,
		game.gen,
		count,
		GEN3_SLOT_SIZE,
		GEN3_SLOT_COUNT,
		base64(data))
end

local function pollParty()
	frameCounter = frameCounter + 1
	if frameCounter % UPS_POLL_INTERVAL ~= 0 then return end
	if not game or not emu then return end
	if next(clients) == nil then return end -- nessuno in ascolto: non sprecare cicli

	local count = emu:read8(game.count)
	if count > GEN3_SLOT_COUNT then
		-- Byte catturato a meta' scrittura, o mappa sbagliata. In entrambi i casi
		-- non e' un valore su cui basarsi: lo limitiamo e lasciamo che sia il lato
		-- C# a validare slot per slot. Vedi D-008.
		count = GEN3_SLOT_COUNT
	end

	local data = emu:readRange(game.party, GEN3_SLOT_SIZE * GEN3_SLOT_COUNT)
	if not data or #data ~= GEN3_SLOT_SIZE * GEN3_SLOT_COUNT then
		return
	end

	local hash = hash32(data)
	if hash == lastHash and count == lastCount then return end
	lastHash, lastCount = hash, count

	lastPayload = buildPayload(count, data)
	broadcast(lastPayload)
end

--------------------------------------------------------------------------------
-- Avvio
--------------------------------------------------------------------------------

callbacks:add("start", detectGame)
callbacks:add("reset", detectGame)
callbacks:add("frame", pollParty)

startServer()

-- Lo script puo' essere caricato con la ROM gia' in esecuzione: in quel caso il
-- callback "start" non scattera' mai, quindi rileviamo subito.
if emu then
	detectGame()
end
