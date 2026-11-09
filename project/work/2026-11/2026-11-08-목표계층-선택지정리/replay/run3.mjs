// node run3.mjs <set> <variant> [reps] [all]  — sends the requests of s3/<set>-<variant>.jsonl that differ from stage 2
// (all: every request) through Tools/decisions-compare (Celeris only, conc 3), into s3/out/<set>-<variant>/.
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';
const R = path.dirname(fileURLToPath(import.meta.url));
const [set, v, repsArg, allArg] = process.argv.slice(2);
const reps = +(repsArg || 2);
const S2 = fs.readFileSync(path.join(R, 'v', `${set}-S2.jsonl`), 'utf8').split('\n').filter(Boolean);
const meta = JSON.parse(fs.readFileSync(path.join(R, 's3', `${set}-meta.json`), 'utf8'))[v];
const B = fs.readFileSync(path.join(R, 's3', `${set}-${v}.jsonl`), 'utf8').split('\n').filter(Boolean);
const send = [], map = [];
B.forEach((b, k) => { const i = meta.sentIdx[k]; if (allArg === 'all' || JSON.stringify(JSON.parse(S2[i])) !== JSON.stringify(JSON.parse(b))) { send.push(b); map.push(i); } });
const out = path.join(R, 's3', 'out', `${set}-${v}`);
fs.mkdirSync(out, { recursive: true });
fs.writeFileSync(path.join(out, 'sent.jsonl'), send.join('\n') + '\n');
fs.writeFileSync(path.join(out, 'sent-index.json'), JSON.stringify(map));   // line k of sent.jsonl = case index map[k]
if (!send.length) { console.log(`${set}-${v}: nothing to send`); process.exit(0); }
const tool = 'C:/Dev/CoreKeeperModSDK-celeris/Tools/decisions-compare/compare.mjs';
const res = execFileSync('node', [tool, '--out', out, '--requests', path.join(out, 'sent.jsonl'), '--providers', 'celeris', '--reps', String(reps), '--conc', '3'], { encoding: 'utf8', maxBuffer: 1 << 26 });
console.log(`${set}-${v}: sent ${send.length} x${reps} — ` + (res.split('\n').find(l => l.includes('answers')) || res.slice(0, 200)));
