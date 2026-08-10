--[[
  UltimatePoKeSync — mGBA -> app bridge

  Reads the party from RAM and ships it over TCP as RAW bytes.
  This script interprets nothing: it does not decrypt, does not validate checksums,
  does not translate species IDs. All decoding happens on the C# side, once, shared
  across every emulator. See D-006 in the decision log.

  USAGE
    1. mGBA -> Tools -> Scripting... -> File -> Load script...
    2. Pick this file.
    3. Start the ROM (or load it first, either order works).
    4. The app connects to 127.0.0.1:8888.

  PROTOCOL
    One JSON line per message, terminated by "\n". See docs/protocol.md.

  COMPATIBILITY
    mGBA 0.10.0+ (Lua scripting does not exist before that).
    This file is deliberately monolithic: mGBA does not document `require` support for
    local modules, so splitting it up would make it fragile and awkward to load.
    See D-012.
]]

--------------------------------------------------------------------------------
-- Configuration
--------------------------------------------------------------------------------

local UPS_PORT = 8888

-- How many frames between party checks. 60/4 = 15 times per second: plenty for a
-- party, and polling at 60 Hz would burn CPU for nothing.
local UPS_POLL_INTERVAL = 4

local UPS_PROTOCOL_VERSION = 1

--------------------------------------------------------------------------------
-- Gen 3 memory map
--
-- Addresses cross-checked against three independent sources: Data Crystal,
-- GameSettings.lua from 40Cakes/pokebot-bizhawk, and res/scripts/pokemon.lua shipped
-- by mGBA itself. See D-004 and D-013.
--
-- Gen 3 revisions share the same addresses (FireRed Rev 1, Ruby Rev 1/2...), so the
-- game code alone identifies the map. That will not hold for every generation, which
-- is why the key stays the full game code.
--------------------------------------------------------------------------------

local GAMES = {
	["AXVE"] = { name = "Ruby (USA)",      gen = 3, party = 0x03004360, count = 0x03004350 },
	["AXPE"] = { name = "Sapphire (USA)",  gen = 3, party = 0x03004360, count = 0x03004350 },
	["BPEE"] = { name = "Emerald (USA)",   gen = 3, party = 0x020244EC, count = 0x020244E9 },
	["BPRE"] = { name = "FireRed (USA)",   gen = 3, party = 0x02024284, count = 0x02024029 },
	["BPGE"] = { name = "LeafGreen (USA)", gen = 3, party = 0x02024284, count = 0x02024029 },
}

local GEN3_SLOT_SIZE = 100 -- 80 "stored" bytes + 20 bytes of battle stats
local GEN3_SLOT_COUNT = 6

--------------------------------------------------------------------------------
-- State
--------------------------------------------------------------------------------

local server = nil
local clients = {}
local nextClientId = 1

local game = nil     -- entry from GAMES for the current ROM, or nil
local gameCode = nil -- e.g. "BPEE"
local gameTitle = ""
local gameRevision = 0

local frameCounter = 0
local sequence = 0
local lastPayload = nil -- last message sent, replayed to clients that connect later

-- Confirmation across two reads, see D-008.
-- "confirmed" is the last state already sent; "pending" is a change seen once and not
-- yet confirmed.
local confirmedHash, confirmedCount = nil, -1
local pendingHash, pendingCount = nil, -1

--------------------------------------------------------------------------------
-- Utilities
--------------------------------------------------------------------------------

local B64_ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"

--- Encodes a byte string as base64.
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

--- FNV-1a 32-bit. Only used to tell whether the bytes changed; not cryptographic.
local function hash32(data)
	local h = 2166136261
	for _, byte in ipairs({ data:byte(1, #data) }) do
		h = h ~ byte
		h = (h * 16777619) & 0xFFFFFFFF
	end
	return h
end

--- Escapes the bare minimum: our fields only ever hold ROM titles and base64.
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
-- Networking
--------------------------------------------------------------------------------

local function dropClient(id, reason)
	local client = clients[id]
	if not client then return end
	clients[id] = nil
	pcall(function() client:close() end)
	log("client " .. id .. " disconnected (" .. reason .. ")")
end

local function broadcast(line)
	for id, client in pairs(clients) do
		local ok, err = client:send(line)
		if not ok and err ~= socket.ERRORS.AGAIN then
			dropClient(id, tostring(err))
		end
	end
end

--- Drains the receive buffer. We accept no commands, but if we never read, the socket
--- buffer would fill up and the connection would stall.
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
		logError("accept failed: " .. tostring(err))
		return
	end

	local id = nextClientId
	nextClientId = id + 1
	clients[id] = client

	client:add("received", function() drainClient(id) end)
	client:add("error", function() dropClient(id, "socket error") end)

	log("client " .. id .. " connected")

	-- Send the current state right away rather than waiting for the next change:
	-- otherwise an app started while the game sits idle would stay empty indefinitely.
	if lastPayload then
		client:send(lastPayload)
	end
end

local function startServer()
	local err
	server, err = socket.bind(nil, UPS_PORT)
	if err then
		if err == socket.ERRORS.ADDRESS_IN_USE then
			logError("port " .. UPS_PORT .. " already in use. Is another mGBA instance "
				.. "already running the bridge? Change UPS_PORT at the top of this "
				.. "script (and in the app), then reload.")
		else
			logError("bind failed: " .. tostring(err))
		end
		server = nil
		return
	end

	local ok
	ok, err = server:listen()
	if err then
		logError("listen failed: " .. tostring(err))
		server:close()
		server = nil
		return
	end

	server:add("received", acceptClient)
	log("listening on 127.0.0.1:" .. UPS_PORT)
end

--------------------------------------------------------------------------------
-- Game detection
--------------------------------------------------------------------------------

local function detectGame()
	game, gameCode, gameTitle, gameRevision = nil, nil, "", 0
	lastPayload = nil
	confirmedHash, confirmedCount = nil, -1
	pendingHash, pendingCount = nil, -1

	if not emu then return end

	-- getGameCode() returns the code with a platform prefix, e.g. "AGB-BPEE".
	local raw = emu:getGameCode()
	if not raw or raw == "" then
		logError("no game code in the header: non-standard ROM, or no ROM loaded.")
		return
	end

	gameCode = raw:match("([A-Z0-9][A-Z0-9][A-Z0-9][A-Z0-9])$") or raw
	gameTitle = emu:getGameTitle() or ""
	gameRevision = emu:read8(0x080000BC) -- GBA header "software version" byte

	game = GAMES[gameCode]
	if not game then
		-- Refuse explicitly rather than guess: reading with the wrong map would produce
		-- plausible but invented Pokemon. See D-005.
		logError("unsupported game: " .. tostring(raw) .. " (\"" .. gameTitle .. "\"). "
			.. "No reads will be performed.")
		return
	end

	log("game recognised: " .. game.name .. " [" .. gameCode .. "] rev" .. gameRevision)
end

--------------------------------------------------------------------------------
-- Read and send
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
	if next(clients) == nil then return end -- nobody listening: do not waste cycles

	local count = emu:read8(game.count)
	if count > GEN3_SLOT_COUNT then
		-- Byte sampled mid-write, or the wrong map. Either way it is not a value to
		-- rely on: clamp it and let the C# side validate slot by slot. See D-008.
		count = GEN3_SLOT_COUNT
	end

	local data = emu:readRange(game.party, GEN3_SLOT_SIZE * GEN3_SLOT_COUNT)
	if not data or #data ~= GEN3_SLOT_SIZE * GEN3_SLOT_COUNT then
		return
	end

	local hash = hash32(data)

	-- Nothing new compared to what we already sent.
	if hash == confirmedHash and count == confirmedCount then
		pendingHash, pendingCount = nil, -1
		return
	end

	if hash == pendingHash and count == pendingCount then
		-- Same content across two consecutive reads: the game was not writing halfway
		-- through the structure. Safe to send now. See D-008.
		confirmedHash, confirmedCount = hash, count
		pendingHash, pendingCount = nil, -1

		lastPayload = buildPayload(count, data)
		broadcast(lastPayload)
	else
		-- First sighting: wait for the next read to confirm it. Costs one polling
		-- interval of latency (~66 ms), which is nothing for a party.
		pendingHash, pendingCount = hash, count
	end
end

--------------------------------------------------------------------------------
-- Startup
--------------------------------------------------------------------------------

callbacks:add("start", detectGame)
callbacks:add("reset", detectGame)
callbacks:add("frame", pollParty)

startServer()

-- The script can be loaded with the ROM already running, in which case the "start"
-- callback never fires, so detect immediately.
if emu then
	detectGame()
end
