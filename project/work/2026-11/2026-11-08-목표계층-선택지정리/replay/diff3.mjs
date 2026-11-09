// node diff3.mjs <문구최적화 dir> <variant...> — per changed c100 case: S2 answers vs variant answers with grades
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
const R = path.dirname(fileURLToPath(import.meta.url));
const [L, ...vs] = process.argv.slice(2);
process.env.OPT_DIR = L;
const G = (await import('file:///' + path.join(R, 'grades.mjs').replace(/\\/g, '/') + '?z')).default;
for (const line of fs.readFileSync(path.join(R, 'labels-S2-judge.md'), 'utf8').split(/\r?\n/)) {
  const m = line.match(/^\| (\d+)#(\d+) \| (\S+)[^|]*\| (최선|알맞음|부적절) \|/);
  if (m) { const id = `decisions-${m[1]}.log#${m[2]}`; (G[id] ||= {})[m[3]] ??= m[4]; }
}
const jl = f => fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const cm = JSON.parse(fs.readFileSync(path.join(R, 'in', 'c100-meta.json'), 'utf8'));
const m2 = JSON.parse(fs.readFileSync(path.join(R, 'v', 'c100-meta.json'), 'utf8')).S2;
const s2 = {}; for (const r of jl(path.join(R, 'out', 'c100-S2', 'results.jsonl'))) { const i = +r.id.split(':').pop() - 1; (s2[i] ||= []).push((m2[i].map[r.choice] || {}).orig); }
for (const v of vs) {
  const mv = JSON.parse(fs.readFileSync(path.join(R, 's3', 'c100-meta.json'), 'utf8'))[v].meta;
  const d = path.join(R, 's3', 'out', `c100-${v}`); const sidx = JSON.parse(fs.readFileSync(path.join(d, 'sent-index.json'), 'utf8'));
  const nv = {}; for (const r of jl(path.join(d, 'results.jsonl'))) { const i = sidx[+r.id.split(':').pop() - 1]; (nv[i] ||= []).push((mv[i].map[r.choice] || {}).orig); }
  for (const i of Object.keys(nv)) {
    const g = G[cm[i].id] || {}; const a = (s2[i] || []).map(o => `${o}:${g[o] || '?'}`).join(' '), b = nv[i].map(o => `${o}:${g[o] || '?'}`).join(' ');
    console.log(`${v} ${cm[i].id}${mv[i].combat ? ' [전투]' : ''}  S2 ${a}  ->  ${b}`);
  }
}
