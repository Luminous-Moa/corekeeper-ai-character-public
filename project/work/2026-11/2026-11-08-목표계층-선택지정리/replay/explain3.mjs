// node explain3.mjs <문구최적화 dir> — re-sends the c100 non-combat requests whose E-variant answer was graded 부적절, with
// x_celeris.explain on, and prints the model's one-sentence reasons (docs.celeris.ai/decisions).
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';
const R = path.dirname(fileURLToPath(import.meta.url));
const L = process.argv[2];
process.env.OPT_DIR = L;
const G = (await import('file:///' + path.join(R, 'grades.mjs').replace(/\\/g, '/') + '?e')).default;
for (const line of fs.readFileSync(path.join(R, 'labels-S2-judge.md'), 'utf8').split(/\r?\n/)) {
  const m = line.match(/^\| (\d+)#(\d+) \| (\S+)[^|]*\| (최선|알맞음|부적절) \|/);
  if (m) { const id = `decisions-${m[1]}.log#${m[2]}`; (G[id] ||= {})[m[3]] ??= m[4]; }
}
const jl = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const cm = JSON.parse(fs.readFileSync(path.join(R, 'in', 'c100-meta.json'), 'utf8'));
const meta = JSON.parse(fs.readFileSync(path.join(R, 's3', 'c100-meta.json'), 'utf8')).E;
const bodies = fs.readFileSync(path.join(R, 's3', 'c100-E.jsonl'), 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const m2 = JSON.parse(fs.readFileSync(path.join(R, 'v', 'c100-meta.json'), 'utf8')).S2;
// E answers: changed cases from s3/out/c100-E, others from out/c100-S2
const ans = {};
for (const r of jl(path.join(R, 'out', 'c100-S2', 'results.jsonl'))) { const i = +r.id.split(':').pop() - 1; (ans[i] ||= []).push((m2[i].map[r.choice] || {}).orig); }
const sidx = JSON.parse(fs.readFileSync(path.join(R, 's3', 'out', 'c100-E', 'sent-index.json'), 'utf8'));
const nv = {}; for (const r of jl(path.join(R, 's3', 'out', 'c100-E', 'results.jsonl'))) { const i = sidx[+r.id.split(':').pop() - 1]; (nv[i] ||= []).push((meta.meta[i].map[r.choice] || {}).orig); }
Object.assign(ans, nv);
const key = process.env.CELERIS_API_KEY || execFileSync('powershell.exe', ['-NoProfile', '-Command', "[Environment]::GetEnvironmentVariable('CELERIS_API_KEY','User')"], { encoding: 'utf8' }).trim();
const out = [];
for (const [i, arr] of Object.entries(ans)) {
  if (meta.meta[i].combat) continue;
  const g = G[cm[i].id]; if (!g) continue;
  const bad = [...new Set(arr.filter(o => g[o] === '부적절'))]; if (!bad.length) continue;
  const k = meta.sentIdx.indexOf(+i); if (k < 0) continue;
  const body = { ...bodies[k], model: 'celeris-1.1-decision', x_celeris: { explain: true } };
  const r = await fetch('https://inference.celeris.ai/celeris-1-decision/v1/decisions', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + key }, body: JSON.stringify(body) });
  const j = await r.json().catch(() => ({}));
  const s = JSON.stringify(j);
  const best = Object.entries(g).filter(([, x]) => x === '최선').map(([o]) => o);
  out.push({ id: cm[i].id, gradedBad: bad, best, status: r.status, response: j });
  console.log(`${cm[i].id} bad=${bad.join(',')} best=${best.join(',')} status=${r.status}\n  ${s.slice(0, 900)}`);
}
fs.writeFileSync(path.join(R, 's3', 'explain3.json'), JSON.stringify(out, null, 1));
