// cmp.mjs <L0 Player.log> <L1 Player.log> [L0 tstart] [L1 tstart]
// L1 refactor run vs L0: report JSON key paths, goal-end lines followed by PLANNER trigger order, mismatch counts.
import fs from 'node:fs';
const [a, b, ta = '0', tb = '0'] = process.argv.slice(2);
const lines = (f, t0) => fs.readFileSync(f, 'utf8').split('\n').map((l) => l.replace(/\r$/, ''))
  .filter((l) => { const m = l.match(/\[AIPlayerSpike\] t=([\d.]+) @Claude /); return m && +m[1] >= +t0; });
const keyPaths = (o, p = '', s = new Set()) => {
  if (Array.isArray(o)) { for (const x of o) keyPaths(x, p + '[]', s); return s; }
  if (o && typeof o === 'object') for (const k0 of Object.keys(o)) { const k = /^[a-z_0-9]+$/.test(k0) ? k0 : 'ID'; s.add(p + '.' + k); keyPaths(o[k0], p + '.' + k, s); }
  return s;
};
function reports(ls) {
  const all = new Set(); const byTrig = {}; let n = 0, bad = 0; const topOrder = new Set();
  for (const l of ls) {
    const m = l.match(/PLANNER: report #\d+ sent trigger=(\S+) .*?: (\{.*\})$/); if (!m) continue;
    try { const j = JSON.parse(m[2]); n++; topOrder.add(Object.keys(j).join(','));
      const s = keyPaths(j); for (const k of s) all.add(k); (byTrig[m[1]] ||= new Set()); for (const k of s) byTrig[m[1]].add(k);
    } catch { bad++; }
  }
  return { n, bad, all, byTrig, topOrder };
}
const shape = (s) => s.replace(/t=[\d.]+ @Claude /, '').replace(/#\d+/g, '#').replace(/rev=\d+/g, 'rev=#').replace(/after [\d.]+s/g, 'after #s')
  .replace(/\d+(\.\d+)?/g, '#').slice(0, 110);
function goalEnds(ls) {
  // each GOAL done/stalled/ended/cleared/free time line, then the next 2 PLANNER/EXP lines within 3 s
  const out = [];
  for (let i = 0; i < ls.length; i++) {
    if (!/@Claude (GOAL (done|stalled|ended|cleared|free time)|EXP: goal)/.test(ls[i])) continue;
    const t = +ls[i].match(/t=([\d.]+)/)[1]; const nx = [];
    for (let j = i + 1; j < ls.length && nx.length < 3; j++) {
      const tj = +ls[j].match(/t=([\d.]+)/)[1]; if (tj - t > 3) break;
      const m = ls[j].match(/@Claude (PLANNER: (report #\d+ sent trigger=\S+|[^:#]{0,40})|EXP: \S+|GOAL \S+)/); if (m) nx.push(m[1].replace(/#\d+/, '#'));
    }
    out.push(`${shape(ls[i].match(/@Claude (.*)/)[1]).padEnd(70)} -> ${nx.join(' | ')}`);
  }
  return out;
}
const A = lines(a, ta), B = lines(b, tb);
const ra = reports(A), rb = reports(B);
console.log(`reports: L0 ${ra.n} (bad ${ra.bad}), L1 ${rb.n} (bad ${rb.bad})`);
console.log('top-level key orders L0:', [...ra.topOrder]); console.log('top-level key orders L1:', [...rb.topOrder]);
console.log('key paths only in L0:', [...ra.all].filter((k) => !rb.all.has(k)));
console.log('key paths only in L1:', [...rb.all].filter((k) => !ra.all.has(k)));
for (const t of new Set([...Object.keys(ra.byTrig), ...Object.keys(rb.byTrig)]))
  console.log(`  trigger ${t}: L0 ${ra.byTrig[t]?.size ?? '-'} paths, L1 ${rb.byTrig[t]?.size ?? '-'} paths`);
for (const k of ['OUTCOME-MISMATCH', 'EMERGENCY-MISMATCH', 'REJECT-MISMATCH', 'TOOLSHORT-MISMATCH'])
  console.log(`${k}: L0 ${A.filter((l) => l.includes(k)).length}, L1 ${B.filter((l) => l.includes(k)).length}`);
console.log('\n== goal ends L0'); for (const x of goalEnds(A)) console.log(x);
console.log('\n== goal ends L1'); for (const x of goalEnds(B)) console.log(x);
