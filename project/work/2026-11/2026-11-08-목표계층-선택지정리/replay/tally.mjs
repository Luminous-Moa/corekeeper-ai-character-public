// node tally.mjs <replay dir> <문구최적화 dir>
import fs from 'fs'; import path from 'path';
const [R, L] = process.argv.slice(2);
const G = (await import('file://' + path.join(R, 'grades.mjs').replace(/\\/g, '/') + '?x')).default;
const rows = v => fs.readFileSync(path.join(R, 'out', v, 'results.jsonl'), 'utf8').split('\n').filter(Boolean).map(JSON.parse).filter(r => r.status === 200);
const idx = r => +r.id.split(':').pop() - 1;
const s = o => Object.entries(o).sort((a, b) => b[1] - a[1]).map(([k, c]) => `${k} ${c}`).join(', ');
const out = [];
for (const set of ['stuck', 'halted', 'cont']) {
  const meta = JSON.parse(fs.readFileSync(path.join(R, 'v', `${set}-meta.json`), 'utf8')).S2;
  const t = {}, inmeta = JSON.parse(fs.readFileSync(path.join(R, 'in', `${set}-meta.json`), 'utf8'));
  let kept = 0, n = 0;
  for (const r of rows(`${set}-S2`)) {
    const m = meta[idx(r)].map[r.choice] || { kind: '?' }; let k = m.kind;
    if (k === 'dig' && !m.orig) k = 'dig(기억한 목표 광석, 직접)';
    t[k] = (t[k] || 0) + 1;
    if (set === 'cont') { n++; const ck = inmeta[idx(r)].contKey; if (m.kind === 'continue' || m.key === ck) kept++; }
  }
  out.push(`${set}-S2 (${Object.values(t).reduce((a, b) => a + b, 0)}): ${s(t)}${set === 'cont' ? ` · 지금 행동 유지 ${kept}/${n}` : ''}`);
}
// 100-case set: S0 = the earlier logged-text run (100-ko results.json, celeris run 0), S1, S2
const SRC = path.join(L, '..', '2026-11-08-celeris-가상비교', '100-ko');
const old = JSON.parse(fs.readFileSync(path.join(SRC, 'results.json'), 'utf8'));
const cmeta = JSON.parse(fs.readFileSync(path.join(R, 'in', 'c100-meta.json'), 'utf8'));
const vmeta = JSON.parse(fs.readFileSync(path.join(R, 'v', 'c100-meta.json'), 'utf8'));
const SC = { '최선': 2, '알맞음': 1, '부적절': 0 };
const SURV = new Set(['attack', 'retreat', 'away', 'eat', 'step', 'hit', 'combo', 'approach', 'hold']);
function score(choiceOf) {   // choiceOf(caseIndex) -> [orig values per rep]
  const res = { all: { s: 0, n: 0, bad: 0, un: 0 }, surv: { s: 0, n: 0, bad: 0, un: 0 } };
  cmeta.forEach((c, i) => {
    const g = G[c.id]; if (!g) return;
    const best = Object.entries(g).filter(([, v]) => v === '최선').map(([k]) => k);
    const survCase = best.length > 0 && best.every(v => SURV.has((c.map[v] || {}).kind));
    for (const v of choiceOf(i)) for (const bucket of survCase ? ['all', 'surv'] : ['all']) {
      const b = res[bucket];
      if (v == null) continue;
      if (g[v] == null) { b.un++; continue; }
      b.n++; b.s += SC[g[v]]; if (g[v] === '부적절') b.bad++;
    }
  });
  return res;
}
const oldBy = {}; for (const r of old) oldBy[r.id] = r.runs.celeris.map(x => x.choice);
const S0 = score(i => (oldBy[cmeta[i].id] || []).slice(0, 2));
const by = v => { const o = {}; for (const r of rows(`c100-${v}`)) { const m = vmeta[v][idx(r)]; (o[m.id] ||= []).push((m.map[r.choice] || {}).orig ?? null); } return o; };
const b1 = by('S1'), b2 = by('S2');
// single-option lists are not sent by the replay tool: their one option is the answer
vmeta.S2.forEach(m => { if (m.after === 1) b2[m.id] = [Object.values(m.map)[0].orig, Object.values(m.map)[0].orig]; });
const S1 = score(i => b1[cmeta[i].id] || []), S2 = score(i => b2[cmeta[i].id] || []);
const f = x => `${x.s}/${x.n * 2}(판정 ${x.n}, 부적절 ${x.bad}, 미판정 ${x.un})`;
out.push(`100건 판정 49건(2회): S0 원문 ${f(S0.all)} · S1 완료 제거 ${f(S1.all)} · S2 ${f(S2.all)}`);
out.push(`  그중 정답이 생존 행동(공격·물러나기·먹기 등)인 사례: S0 ${f(S0.surv)} · S1 ${f(S1.surv)} · S2 ${f(S2.surv)}`);
// graded best choices that S2 removed
const lost = [];
cmeta.forEach((c, i) => { const g = G[c.id]; if (!g) return; const keptOrig = new Set(Object.values(vmeta.S2[i].map).map(x => x.orig)); for (const [v, gr] of Object.entries(g)) if (gr === '최선' && !keptOrig.has(v)) lost.push(`${c.id} ${v}`); });
out.push(`S2가 뺀 '최선' 판정 선택: ${lost.length ? lost.join(', ') : '없음'}`);
// regression: ungraded cases whose four earlier runs agreed
const reg = old.filter(r => !G[r.id] && r.runs.openai[0].choice === r.runs.celeris[0].choice);
let same = 0, diff = [];
for (const r of reg) { const a = b2[r.id] || []; if (a[0] === r.runs.celeris[0].choice) same++; else diff.push(`${r.id}: ${r.runs.celeris[0].choice} -> ${a[0]}`); }
out.push(`회귀(판정 없음·이전 실행 일치 ${reg.length}건, S2 1회차): 같음 ${same}, 바뀜 ${diff.length}`);
// changed graded choices that are unlabelled (for an independent judge)
const unl = [];
cmeta.forEach((c, i) => { const g = G[c.id]; if (!g) return; for (const v of (b2[c.id] || [])) if (v != null && g[v] == null) unl.push(`${c.id} ${v}`); });
fs.writeFileSync(path.join(R, 'unlabeled-S2.json'), JSON.stringify({ unlabeled: [...new Set(unl)], regressionChanged: diff }, null, 1));
out.push(`판정 없는 새 선택 ${new Set(unl).size}개, 회귀 바뀜 ${diff.length}개 → unlabeled-S2.json`);
console.log(out.join('\n'));
fs.writeFileSync(path.join(R, 'tally.txt'), out.join('\n') + '\n');
