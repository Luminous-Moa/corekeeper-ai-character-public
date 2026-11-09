// node tally.mjs <dir>: chosen kinds per set and variant
import fs from 'fs';
import path from 'path';
const dir = process.argv[2];
for (const p of ['stuck', 'acc']) {
  const meta = JSON.parse(fs.readFileSync(path.join(dir, `${p}-meta.json`), 'utf8'));
  for (const v of ['F0', 'F1', 'F3', 'F13', 'F1b', 'F13b']) {
    const rows = fs.readFileSync(path.join(dir, `out-${p}-${v}`, 'results.jsonl'), 'utf8').split('\n').filter(Boolean).map(JSON.parse).filter(r => r.status === 200);
    const t = {};
    for (const r of rows) { const me = meta[v][+r.id.split(':').pop() - 1]; const k = (me.map[r.choice] || { kind: '?' }).kind; t[k] = (t[k] || 0) + 1; }
    console.log(`${p}-${v} (${rows.length}): ` + Object.entries(t).sort((a, b) => b[1] - a[1]).map(([k, c]) => `${k} ${c}`).join(', '));
  }
}
