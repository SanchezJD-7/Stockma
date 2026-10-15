import { execSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const VOSEO = [
  "podés", "tenés", "querés", "sabés", "andás", "venís", "decís", "salís",
  "volvés", "escribís", "hacés", "entendés", "sentís", "leés", "creés",
  "movés", "cambiás", "desactivás", "dejá", "agregá", "creá", "eliminá",
  "editá", "entrá", "usá", "mirá", "fijá", "probá", "empezá", "hacé",
  "mandá", "pegá", "rompé", "poné", "cerrá", "guardá", "cambiá", "intentá",
  "esperá", "revisá", "seleccioná", "completá", "acercá", "agarrá", "tirá",
  "copiá", "confirmá", "ingresá", "introducí", "conectá", "descargá", "ejecutá",
  "contestá", "recordá", "pensá", "tratá", "disfrutá", "imaginá", "decime", "contame",
  "haceme", "pasame", "mostrame", "avisame", "creeme", "fijate", "quedate", "acordate", "olvidate",
];

const PATTERN = new RegExp(`\\b(?:${VOSEO.join("|")})\\b`, "g");
const SELF = fileURLToPath(import.meta.url);

function listTrackedFiles() {
  const toplevel = execSync("git rev-parse --show-toplevel", { encoding: "utf8" }).trim();
  const out = execSync("git ls-files -z", { cwd: toplevel, encoding: "buffer" });
  return { toplevel, files: out.toString("utf8").split("\0").filter(Boolean) };
}

function main() {
  const { toplevel, files } = listTrackedFiles();
  const violations = [];

  for (const relative of files) {
    const absolute = path.join(toplevel, relative);
    if (absolute === SELF) continue;

    let buffer;
    try {
      buffer = readFileSync(absolute);
    } catch {
      continue;
    }
    if (buffer.includes(0) || buffer.length > 1_000_000) continue;

    const lines = buffer.toString("utf8").split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
      PATTERN.lastIndex = 0;
      const found = lines[i].match(PATTERN);
      if (found) violations.push(`${relative}:${i + 1}: ${[...new Set(found)].join(", ")}`);
    }
  }

  if (violations.length === 0) {
    console.log(`check-neutral-es: ok (${files.length} archivos, sin voseo).`);
    return 0;
  }

  console.error("check-neutral-es: se encontró voseo. Español neutro latinoamericano, sin excepciones:");
  for (const v of violations) console.error(`  ${v}`);
  console.error('Regla: frontend/web/README.md → "Textos de la interfaz".');
  return 1;
}

process.exit(main());
