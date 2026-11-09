import fs from 'fs'; import path from 'path';
const L = process.env.OPT_DIR || process.argv[2];
const SRC = path.join(L, '..', '2026-11-08-celeris-가상비교');
const KEY = JSON.parse(fs.readFileSync(path.join(SRC, '100-en', 'label-key.json'), 'utf8'));
const VER = { ...JSON.parse(fs.readFileSync(path.join(SRC, '100-en', 'label-1-verdicts.json'), 'utf8')), ...JSON.parse(fs.readFileSync(path.join(SRC, '100-en', 'label-2-verdicts.json'), 'utf8')) };
const G = {};
for (const [Lb, k] of Object.entries(KEY)) { const g = {}; for (const [x, v] of Object.entries(k.X)) g[v] = VER[Lb][x].grade; G[k.id] = g; }
// later judge rounds (q1/q2) for new choices, when present
for (const f of ['label-q1-verdicts.json', 'label-q2-verdicts.json']) {
  const p = path.join(L, 'judge', f); const kp = path.join(L, 'judge', 'label-q-key.json');
  if (!fs.existsSync(p) || !fs.existsSync(kp)) continue;
  const V = JSON.parse(fs.readFileSync(p, 'utf8')), K = JSON.parse(fs.readFileSync(kp, 'utf8'));
  for (const [Lb, k] of Object.entries(K)) for (const [x, v] of Object.entries(k.X || {})) if (V[Lb]?.[x]) (G[k.id] ||= {})[v] ??= V[Lb][x].grade;
}
export default G;
if (process.argv[3] && process.argv[1].endsWith("grades.mjs")) console.log(JSON.stringify(G[process.argv[3]]));
