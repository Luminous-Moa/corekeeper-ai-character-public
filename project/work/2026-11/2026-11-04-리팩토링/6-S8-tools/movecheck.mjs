// moved-line check: lines removed from the old files vs lines in the new files, whitespace-normalized
import fs from 'node:fs';
const [oldF, ...newFs] = process.argv.slice(2);
const norm = (l) => l.trim().replace(/\s+/g, ' ');
const old = fs.readFileSync(oldF, 'utf8').split(/\r?\n/).map(norm).filter(Boolean);
const pool = new Map(); for (const l of old) pool.set(l, (pool.get(l) || 0) + 1);
const newLines = newFs.flatMap((f) => fs.readFileSync(f, 'utf8').split(/\r?\n/).map((l, i) => [f.split('/').pop(), i + 1, norm(l)])).filter(([, , l]) => l);
const notMoved = [];
for (const [f, n, l] of newLines) { const c = pool.get(l) || 0; if (c > 0) pool.set(l, c - 1); else notMoved.push(`${f}:${n}: ${l}`); }
const dropped = [...pool].filter(([, c]) => c > 0).flatMap(([l, c]) => Array(c).fill(l));
console.log(`old lines ${old.length}, new lines ${newLines.length}, new lines not in old: ${notMoved.length}, old lines not in new: ${dropped.length}`);
console.log('--- new, not moved'); notMoved.forEach((x) => console.log(x));
console.log('--- old, gone'); dropped.forEach((x) => console.log(x));
