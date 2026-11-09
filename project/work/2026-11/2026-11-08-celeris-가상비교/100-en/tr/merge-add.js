// node merge-add.js -> dict-add100.json from missing.json keys (in order) + add100-en.json English (same order); checks slot use
const fs = require('fs'), path = require('path');
const M = JSON.parse(fs.readFileSync(path.join(__dirname, 'missing.json'), 'utf8'));
const E = JSON.parse(fs.readFileSync(path.join(__dirname, 'add100-en.json'), 'utf8'));
if (M.length !== E.length) { console.error('length mismatch', M.length, E.length); process.exit(1); }
const prevP = path.join(__dirname, 'dict-add100.json');
const out = fs.existsSync(prevP) ? JSON.parse(fs.readFileSync(prevP, 'utf8')) : {};
const bad = [];
M.forEach((m, i) => {
  const n = m.slots, en = E[i], used = (en.match(/\{(\d+)\}/g) || []).map(s => +s.slice(1, -1));
  const ok = used.length === n && new Set(used).size === n && used.every(u => u < n);
  if (!ok) bad.push(`${i}: slots ${n}, used [${used}] :: ${m.key} => ${en}`);
  if (/[가-힣]/.test(en)) bad.push(`${i}: hangul in english`);
  out[m.key] = en;
});
if (bad.length) { console.error(bad.join('\n')); process.exit(1); }
fs.writeFileSync(prevP, JSON.stringify(out, null, 1));
console.log('dict-add100 keys', Object.keys(out).length);
