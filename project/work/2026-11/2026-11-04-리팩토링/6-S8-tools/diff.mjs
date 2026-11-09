// Compare two compare.mjs outputs: raw texts with only timing fields masked ("t", "ms", "total_ms") and the temp folder
// path replaced; key order at every depth is part of the raw text, so equal text = equal keys, order and values.
import fs from 'node:fs';
const [a, b] = process.argv.slice(2).map((f) => JSON.parse(fs.readFileSync(f, 'utf8')));
const mask = (s, T) => s.split(T).join('<T>').split(T.replace(/\\/g, '\\\\')).join('<T>')
  .replace(/"(ms|total_ms)":\d+/g, '"$1":0').replace(/"t":"[^"]*"/g, '"t":"T"').replace(/"(quotaReadAt|authAt)":\d+/g, '"$1":0');
const keyPaths = (o, p = '$', out = []) => {
  if (o && typeof o === 'object') {
    if (!Array.isArray(o)) out.push(`${p}{${Object.keys(o).join(',')}}`);
    for (const [k, v] of Object.entries(o)) keyPaths(v, Array.isArray(o) ? `${p}[]` : `${p}.${k}`, out);
  }
  return out;
};
let diffs = 0;
const cmp = (label, xa, xb) => {
  const ma = mask(xa, a.T), mb = mask(xb, b.T);
  if (ma !== mb) { diffs++; console.log(`DIFF ${label}\n  old: ${ma.slice(0, 600)}\n  new: ${mb.slice(0, 600)}`); return false; }
  return true;
};
console.log(`replies: old ${a.replies.length}, new ${b.replies.length}`);
let same = 0;
const statuses = {};
const keySets = new Set();
for (let i = 0; i < Math.max(a.replies.length, b.replies.length); i++) {
  const x = a.replies[i], y = b.replies[i];
  if (!x || !y || x.name !== y.name || x.status !== y.status) { diffs++; console.log(`DIFF reply ${i}: ${x?.name} ${x?.status} / ${y?.name} ${y?.status}`); continue; }
  statuses[x.status] = (statuses[x.status] || 0) + 1;
  try { const j = JSON.parse(y.text); keyPaths(j).filter((k) => k.startsWith('${')).forEach((k) => keySets.add(k)); } catch { /* aborted: empty */ }
  if (cmp(`reply ${x.name}`, x.text, y.text)) same++;
}
console.log(`replies identical (timing masked): ${same}/${a.replies.length}; statuses ${JSON.stringify(statuses)}; distinct top-level key orders: ${keySets.size}`);
for (const k of keySets) console.log(`  ${k}`);
for (const f of ['plan1', 'recall1', 'plan2', 'recall2']) {
  let s = 0;
  if (a[f].length !== b[f].length) { diffs++; console.log(`DIFF ${f}: ${a[f].length} vs ${b[f].length} lines`); }
  for (let i = 0; i < Math.min(a[f].length, b[f].length); i++) if (cmp(`${f}[${i}]`, a[f][i], b[f][i])) s++;
  const ko = new Set(b[f].map((l) => Object.keys(JSON.parse(l)).join(',')));
  console.log(`${f}: ${s}/${a[f].length} lines identical (timing masked), ${ko.size} distinct key orders`);
}
cmp('stdout phase 1', a.stdout1, b.stdout1); cmp('stdout phase 2', a.stdout2, b.stdout2);
console.log(diffs ? `${diffs} DIFFERENCES` : 'NO DIFFERENCES');
