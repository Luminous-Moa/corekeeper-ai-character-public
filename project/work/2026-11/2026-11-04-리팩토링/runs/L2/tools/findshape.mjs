import fs from 'node:fs';
import { message, shape } from './ls-lib.mjs';
const [f, ...want] = process.argv.slice(2);
const lines = fs.readFileSync(f, 'utf8').split('\n');
const seen = {};
lines.forEach((l, i) => { const m = message(l.replace(/\r$/, '')); if (!m) return; const s = shape(m); for (const w of want) if (s.startsWith(w) && (seen[w] = (seen[w] || 0) + 1) <= 2) console.log(`${i + 1}: [${w}] ${l.slice(0, 260)}`); });
