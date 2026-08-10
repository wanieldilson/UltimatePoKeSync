import fs from "node:fs";

const revision = "db93869dcc216c0be39e7f86e9a64edcc7496d89";
const [mode, inputPath] = process.argv.slice(2);

if (!["moves", "learnsets"].includes(mode) || !inputPath) {
  throw new Error("Usage: node import-showdown-gen3-data.mjs <moves|learnsets> <input-file>");
}

const lines = fs.readFileSync(inputPath, "utf8").split(/\r?\n/);
const result = mode === "moves" ? importMoves(lines) : importLearnsets(lines);
const indentation = process.argv.includes("--compact") ? undefined : 2;
process.stdout.write(JSON.stringify(result, null, indentation) + "\n");

function importMoves(sourceLines) {
  const gen3TypeOverrides = new Map([
    ["charm", "Normal"],
    ["moonlight", "Normal"],
    ["sweetkiss", "Normal"],
  ]);
  let moves = [];
  let current;

  for (const line of sourceLines) {
    const entry = line.match(/^\t(?:"([a-z0-9]+)"|([a-z0-9]+)): \{$/);
    if (entry) {
      current = { id: entry[1] ?? entry[2] };
      continue;
    }

    if (!current) continue;

    const number = line.match(/^\t\tnum: (\d+),$/);
    const name = line.match(/^\t\tname: "(.+)",$/);
    const type = line.match(/^\t\ttype: "([A-Za-z]+)",$/);
    if (number) current.number = Number(number[1]);
    if (name) current.name = name[1].replaceAll('\\\"', '"');
    if (type) current.type = type[1];

    if (line === "\t},") {
      if (current.number >= 1 && current.number <= 354) {
        if (!current.name || !current.type) {
          throw new Error(`Move metadata is incomplete for ${current.id}.`);
        }
        moves.push(current);
      }
      current = undefined;
    }
  }

  moves = [...moves.reduce((byNumber, move) => {
    const existing = byNumber.get(move.number);
    if (!existing || move.id.length < existing.id.length) {
      byNumber.set(move.number, move);
    }
    return byNumber;
  }, new Map()).values()];
  for (const move of moves) {
    move.type = gen3TypeOverrides.get(move.id) ?? move.type;
  }
  moves.sort((left, right) => left.number - right.number);
  if (moves.length !== 354 || moves.some((move, index) => move.number !== index + 1)) {
    const numbers = new Set(moves.map(move => move.number));
    const missing = Array.from({ length: 354 }, (_, index) => index + 1)
      .filter(number => !numbers.has(number));
    throw new Error(
      `Expected the 354 consecutively numbered Gen 3 moves; found ${moves.length}, missing ${missing.join(", ")}.`,
    );
  }

  return { generation: 3, revision, moves };
}

function importLearnsets(sourceLines) {
  const species = {};
  let currentSpecies;
  let inLearnset = false;

  for (const line of sourceLines) {
    const entry = line.match(/^\t([a-z0-9]+): \{$/);
    if (entry) {
      currentSpecies = entry[1];
      inLearnset = false;
      continue;
    }

    if (line === "\t\tlearnset: {") {
      inLearnset = true;
      continue;
    }

    if (!inLearnset || !currentSpecies) continue;
    if (line === "\t\t},") {
      inLearnset = false;
      continue;
    }

    const move = line.match(/^\t\t\t([a-z0-9]+): \[(.+)\],$/);
    if (!move) continue;

    const levels = [...move[2].matchAll(/"3L(\d+)"/g)].map(match => Number(match[1]));
    if (levels.length === 0) continue;

    species[currentSpecies] ??= [];
    species[currentSpecies].push({ id: move[1], level: Math.min(...levels) });
  }

  for (const moves of Object.values(species)) {
    moves.sort((left, right) => left.level - right.level || left.id.localeCompare(right.id));
  }

  delete species.missingno;
  if (Object.keys(species).length !== 386) {
    throw new Error("Expected level-up learnsets for the 386 Gen 3 species.");
  }

  return { generation: 3, revision, species };
}
