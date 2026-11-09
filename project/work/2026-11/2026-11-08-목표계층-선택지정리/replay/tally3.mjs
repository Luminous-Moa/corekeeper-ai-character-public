// node tally3.mjs <문구최적화 dir> — stage-3 replay tally (writes s3/tally3.txt and unlabeled-S3.json)
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
const R = path.dirname(fileURLToPath(import.meta.url));
const L = process.argv[2];
process.env.OPT_DIR = L;
const G = (await import('file:///' + path.join(R, 'grades.mjs').replace(/\\/g, '/') + '?x')).default;
// independent judge labels of 2026-11-08 (labels-S2-judge.md): "| 175253#91 | eat_CookedSoup … | 최선 |"
for (const line of fs.readFileSync(path.join(R, 'labels-S2-judge.md'), 'utf8').split(/\r?\n/)) {
  const m = line.match(/^\| (\d+)#(\d+) \| (\S+)[^|]*\| (최선|알맞음|부적절) \|/);
  if (m) { const id = `decisions-${m[1]}.log#${m[2]}`; (G[id] ||= {})[m[3]] ??= m[4]; }
}
const jl = f => fs.existsSync(f) ? fs.readFileSync(f, 'utf8').split('\n').filter(Boolean).map(JSON.parse) : [];
const SC = { '최선': 2, '알맞음': 1, '부적절': 0 };
const SURV = new Set(['attack', 'retreat', 'away', 'eat', 'step', 'hit', 'combo', 'approach', 'hold']);
const s3meta = set => JSON.parse(fs.readFileSync(path.join(R, 's3', `${set}-meta.json`), 'utf8'));
const s2meta = set => JSON.parse(fs.readFileSync(path.join(R, 'v', `${set}-meta.json`), 'utf8')).S2;
const lineIdx = r => +r.id.split(':').pop() - 1;
// answers per case index -> [{kind,key,orig}]
function answers(set, v) {
  const m2 = s2meta(set), by = {};
  for (const r of jl(path.join(R, 'out', `${set}-S2`, 'results.jsonl'))) if (r.status === 200) (by[lineIdx(r)] ||= []).push(m2[lineIdx(r)].map[r.choice] || { kind: '?' });
  m2.forEach((m, i) => { if (m.after === 1) by[i] = [Object.values(m.map)[0], Object.values(m.map)[0]]; });
  if (v === 'S2') return by;
  const mv = s3meta(set)[v].meta, d = path.join(R, 's3', 'out', `${set}-${v}`);
  const sidx = JSON.parse(fs.readFileSync(path.join(d, 'sent-index.json'), 'utf8'));
  const nb = {};
  for (const r of jl(path.join(d, 'results.jsonl'))) if (r.status === 200) { const i = sidx[lineIdx(r)]; (nb[i] ||= []).push(mv[i].map[r.choice] || { kind: '?' }); }
  mv.forEach((m, i) => { if (m.after === 1) nb[i] = [Object.values(m.map)[0], Object.values(m.map)[0]]; });
  const out = {}; for (const i of Object.keys(by)) out[i] = nb[i] || by[i]; for (const i of Object.keys(nb)) out[i] = nb[i];
  return out;
}
const lines = [];
// ---- 100-case set
const cm = JSON.parse(fs.readFileSync(path.join(R, 'in', 'c100-meta.json'), 'utf8'));
const combat = s3meta('c100').E.meta.map(m => m.combat);
function score(by) {
  const res = {}; for (const b of ['all', 'noncombat', 'surv']) res[b] = { s: 0, n: 0, bad: 0, un: 0 };
  const unl = [];
  cm.forEach((c, i) => {
    const g = G[c.id]; if (!g) return;
    const best = Object.entries(g).filter(([, x]) => x === '최선').map(([k]) => k);
    const surv = best.length > 0 && best.every(x => SURV.has((c.map[x] || {}).kind));
    for (const a of (by[i] || [])) {
      const o = a.orig;
      for (const b of ['all', ...(combat[i] ? [] : ['noncombat']), ...(surv ? ['surv'] : [])]) {
        const r = res[b]; if (o == null) continue;
        if (g[o] == null) { r.un++; if (b === 'all') unl.push(`${c.id} ${o}`); continue; }
        r.n++; r.s += SC[g[o]]; if (g[o] === '부적절') r.bad++;
      }
    }
  });
  return { res, unl };
}
const f = x => `${x.s}/${x.n * 2} (판정 ${x.n}, 부적절 ${x.bad}, 미판정 ${x.un})`;
const variants = ['S2', 'E', 'X', 'N', 'T', 'EXNT'];
const unlAll = {};
for (const v of variants) {
  const { res, unl } = score(answers('c100', v));
  lines.push(`c100 ${v}: 전체 ${f(res.all)} · 비전투 ${f(res.noncombat)} · 생존정답 ${f(res.surv)}`);
  if (v !== 'S2') unlAll[v] = [...new Set(unl)];
}
// regression: cases without grades — how many answers changed vs S2 (rep 1)
const s2 = answers('c100', 'S2');
for (const v of variants.slice(1)) {
  const a = answers('c100', v); let ch = 0; const diff = [];
  cm.forEach((c, i) => { if (G[c.id]) return; const x = (s2[i] || [])[0]?.orig, y = (a[i] || [])[0]?.orig; if (x !== y) { ch++; diff.push(`${c.id}: ${x} -> ${y}`); } });
  lines.push(`  ${v} 판정 없는 사례 1회차가 S2와 다름: ${ch}${diff.length ? ' — ' + diff.join(', ') : ''}`);
  unlAll[v + '-regression'] = diff;
}
// ---- order bias (SHUF, 1 rep) vs S2 rep 1, and S2 rep1 vs rep2 as the noise floor; non-combat cases
{
  const sh = answers('c100', 'SHUF');
  let n = 0, diffSh = 0, diffNoise = 0, nc = 0, dNc = 0, nNc = 0;
  cm.forEach((c, i) => {
    const a = s2[i], b = sh[i]; if (!a || !b || a.length < 2) return;
    n++; if (a[0].orig !== b[0].orig) diffSh++; if (a[0].orig !== a[1].orig) diffNoise++;
    if (!combat[i]) { nc++; if (a[0].orig !== b[0].orig) dNc++; if (a[0].orig !== a[1].orig) nNc++; }
  });
  lines.push(`순서 섞기(1회): 1순위가 S2 1회차와 다름 ${diffSh}/${n} (같은 순서 두 번 사이 차이 ${diffNoise}/${n}); 비전투 ${dNc}/${nc} (잡음 ${nNc}/${nc})`);
}
// ---- latest-format sets
const contMeta = JSON.parse(fs.readFileSync(path.join(R, 'in', 'cont-meta.json'), 'utf8'));
const COMBATKEY = /^(attack|attacknow|combo|hit|step|away|retreat|approach|hold)/;
for (const set of ['cont', 'stuck', 'halted']) {
  for (const v of ['S2', 'N', 'T', 'EXNT']) {
    const by = answers(set, v); const t = {}; let kept = 0, n = 0, keptNc = 0, nNc = 0;
    for (const [i, arr] of Object.entries(by)) for (const a of arr) {
      let k = a.kind; if (k === 'dig' && !a.orig) k = 'dig(기억한 목표 광석)';
      t[k] = (t[k] || 0) + 1;
      if (set === 'cont') { const ck = contMeta[i].contKey; const nc = !COMBATKEY.test(ck); const keep = a.kind === 'continue' || a.key === ck; n++; if (keep) kept++; if (nc) { nNc++; if (keep) keptNc++; } }
    }
    const s = Object.entries(t).sort((a, b) => b[1] - a[1]).map(([k, c]) => `${k} ${c}`).join(', ');
    lines.push(`${set} ${v}: ${s}${set === 'cont' ? ` · 지금 행동 유지 ${kept}/${n} (비전투 ${keptNc}/${nNc})` : ''}`);
  }
}
console.log(lines.join('\n'));
fs.writeFileSync(path.join(R, 's3', 'tally3.txt'), lines.join('\n') + '\n');
fs.writeFileSync(path.join(R, 'unlabeled-S3-raw.json'), JSON.stringify(unlAll, null, 1));
