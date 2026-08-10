import fs from "node:fs";

// Level-up learnsets used to be imported here too. They are not any more: Showdown
// merges Ruby/Sapphire, Emerald and FireRed/LeafGreen under one "3L" tag, so the import
// had to pick one level for three games that disagree. They now come per game from
// PKHeX. See D-027.
const revision = "db93869dcc216c0be39e7f86e9a64edcc7496d89";
const [mode, inputPath] = process.argv.slice(2);

if (mode !== "moves" || !inputPath) {
  throw new Error("Usage: node import-showdown-gen3-data.mjs moves <input-file>");
}

const lines = fs.readFileSync(inputPath, "utf8").split(/\r?\n/);
const result = importMoves(lines);
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
