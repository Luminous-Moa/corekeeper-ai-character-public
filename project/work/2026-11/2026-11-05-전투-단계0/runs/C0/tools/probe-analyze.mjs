// probe-analyze.mjs <bruteprobe.log> [--from HH:mm:ss] [--to HH:mm:ss] [--json out]
// Per charge: anticipation ticks, aim lock tick, charge ticks, distance, peak speed (from positions), how it ended,
// HIT target/distance, vulnerable window (maxDmg 0), damage taken in the window, walls broken; cooldown between charges;
// brute hp changes (big / positive); MOVE-START distribution.
import fs from 'fs';
const a = process.argv.slice(2); const file = a[0];
const opt = k => { const i = a.indexOf(k); return i >= 0 ? a[i + 1] : null; };
const from = opt('--from'), to = opt('--to');
const L = fs.readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean).map(l => {
  const m = l.match(/^wall=(\S+) t=([\d.]+) stk=(\d+) st=([\d.]+) (?:@(\S+) )?(?:BRUTE-PROBE )?(\S+) (.*)$/);
  return m ? { wall: m[1], t: +m[2], stk: +m[3], kind: m[6], who: m[5], body: m[7], raw: l } : null;
}).filter(x => x && (!from || x.wall >= from) && (!to || x.wall <= to));
const q = (arr, p) => { if (!arr.length) return NaN; const s = [...arr].sort((x, y) => x - y); return s[Math.min(s.length - 1, Math.max(0, Math.ceil(p * s.length) - 1))]; };
const st = arr => arr.length ? `n=${arr.length} min=${Math.min(...arr).toFixed(3)} p50=${q(arr, .5).toFixed(3)} p95=${q(arr, .95).toFixed(3)} max=${Math.max(...arr).toFixed(3)}` : 'n=0';
const num = (s, k) => { const m = s.match(new RegExp('(?:^| )' + k + '=(-?[\\d.]+)')); return m ? +m[1] : NaN; };
const pos = s => { const m = s.match(/pos=\((-?[\d.]+),(-?[\d.]+)\)/); return m ? [+m[1], +m[2]] : null; };
const str = (s, k) => { const m = s.match(new RegExp('(?:^| )' + k + '=(\\S+)')); return m ? m[1] : null; };
const B = new Map();
for (const x of L) {
  if (!['EVT', 'TICK', 'HIT', 'HIT-AFTER', 'TILE-CHANGE', 'TILES-AFTER'].includes(x.kind)) continue;
  const e = (x.body.match(/^e=(\S+)/) || [])[1]; if (!e) continue;
  if (!B.has(e)) B.set(e, []); B.get(e).push(x);
}
const charges = [], hpch = [];
for (const [e, xs] of B) {
  const samp = xs.filter(x => x.kind === 'EVT' || x.kind === 'TICK').map(x => {
    const parts = x.body.split(' | '); const b = x.kind === 'EVT' ? (parts[1] ?? '') : x.body; const why = x.kind === 'EVT' ? parts[0] : '';
    return { ...x, why, phase: str(b, 'phase'), state: str(b, 'state'), p: pos(b), dDeg: num(b, 'dDeg'), maxDmg: num(b, 'maxDmg'), hp: num(b, 'hp'), td: num(b, 'targetDist'), target: str(b, 'target'), b };
  });
  for (const s of samp) { const m = s.why.match(/hp (-?\d+)->(-?\d+) \(([+-]\d+)\)/); if (m) hpch.push({ e, wall: s.wall, t: s.t, stk: s.stk, from: +m[1], to: +m[2], d: +m[3], phase: s.phase, state: s.state, maxDmg: s.maxDmg, near: (s.raw.split(' | near ')[1] || '').slice(0, 200) }); }
  let prevEnd = null;
  for (let i = 0; i < samp.length; i++) {
    const s = samp[i];
    const isStart = s.state === 'Charge' && s.phase === 'StartCharging' && (i === 0 || !(samp[i - 1].state === 'Charge' && samp[i - 1].phase === 'StartCharging'));
    if (!isStart) continue;
    const c = { e, wall: s.wall, t: s.t, s0: s.stk, target: s.target, td0: s.td };
    let j = i, lastAim = null;
    for (; j < samp.length && samp[j].phase === 'StartCharging' && samp[j].state === 'Charge'; j++) if (samp[j].dDeg > 0.05 && j > i) lastAim = samp[j].stk;
    if (j >= samp.length || samp[j].phase !== 'Charging') { c.aborted = j < samp.length ? `${samp[j].state}/${samp[j].phase}` : 'eof'; charges.push(c); continue; }
    c.c0 = samp[j].stk; c.antTicks = c.c0 - c.s0; c.aimLastChange = lastAim == null ? null : lastAim - c.s0;
    const p0 = samp[j].p; let k = j, peak = 0, prev = samp[j];
    for (; k < samp.length && samp[k].phase === 'Charging'; k++) { if (k > j) { const dt = (samp[k].stk - prev.stk) * 0.05; const v = Math.hypot(samp[k].p[0] - prev.p[0], samp[k].p[1] - prev.p[1]) / dt; if (v > peak) peak = v; prev = samp[k]; } }
    if (k >= samp.length) continue;
    const last = samp[k - 1]; c.chargeTicks = samp[k].stk - c.c0; c.dist = Math.hypot(last.p[0] - p0[0], last.p[1] - p0[1]); c.peak = peak;
    c.end = samp[k].phase; c.tdAtEnd = samp[k].td;
    let v0 = null, v1 = null, m;
    for (m = k; m < samp.length; m++) { if (v0 == null && samp[m].maxDmg === 0) v0 = samp[m].stk; if (v0 != null && samp[m].maxDmg === 1) { v1 = samp[m].stk; break; } if (samp[m].state !== 'Charge' && v0 == null) break; }
    c.vulnTicks = v0 != null && v1 != null ? v1 - v0 : null; c.v0 = v0; c.v1 = v1;
    let leave = null; for (m = k; m < samp.length; m++) if (samp[m].state !== 'Charge') { leave = samp[m]; break; }
    c.leaveStk = leave?.stk; c.leaveState = leave?.state; c.chargeStateTicks = leave ? leave.stk - c.s0 : null;
    c.dmgInWindow = hpch.filter(h => h.e === e && v0 != null && h.stk >= v0 && h.stk <= (v1 ?? 1e12) && h.d < 0).map(h => h.d);
    const hit = xs.find(x => x.kind === 'HIT' && x.stk >= c.c0 && x.stk <= samp[k].stk + 1);
    if (hit) { c.hitTarget = (hit.body.match(/; target (\S+)/) || [])[1]; c.hitDist = +((hit.body.match(/; target \S+ dist=([\d.]+)/) || [])[1]); c.hitEntity = (hit.body.match(/hitEntity=(\S+)/) || [])[1]; c.a1 = (hit.body.match(/ahead1\([^)]*\)=(\[[^\]]*\])/) || [])[1]; }
    const ha = hit ? xs.find(x => x.kind === 'HIT-AFTER' && x.stk > hit.stk) : null; if (ha) c.hitAfter = ha.body.replace(/^e=\S+ /, '');
    c.wallsBroken = xs.filter(x => x.kind === 'TILE-CHANGE' && /wall:/.test(x.body.split(' -> ')[0]) && !/wall:/.test(x.body.split(' -> ')[1].split(';')[0]) && x.stk >= c.c0 && x.stk <= (leave?.stk ?? 1e12)).map(x => (x.body.match(/ (\(-?\d+,-?\d+\)) /) || [])[1] + '@+' + (x.stk - (hit?.stk ?? c.c0)));
    c.cooldownFromPrev = prevEnd != null ? c.s0 - prevEnd : null;
    prevEnd = leave?.stk ?? null;
    charges.push(c);
  }
}
const T = 0.05; const ok = charges.filter(c => !c.aborted);
console.log(`charges ${charges.length} (aborted ${charges.length - ok.length}: ${charges.filter(c => c.aborted).map(c => c.wall + ' ' + c.aborted).join(', ')})`);
for (const c of ok) console.log(`${c.wall} ${c.e} ant=${c.antTicks}tk aimLast=${c.aimLastChange} chg=${c.chargeTicks}tk dist=${c.dist.toFixed(2)} peak=${c.peak.toFixed(2)} end=${c.end} hit=${c.hitTarget ?? '-'}@${c.hitDist ?? '-'} ent=${c.hitEntity ?? '-'} a1=${c.a1 ?? '-'} vuln=${c.vulnTicks}tk chargeState=${c.chargeStateTicks}tk cdPrev=${c.cooldownFromPrev}tk dmg=[${c.dmgInWindow}] walls=${c.wallsBroken.join(' ')} td0=${c.td0}`);
console.log('anticipation s', st(ok.map(c => c.antTicks * T)));
console.log('aim last change (s after first StartCharging sample)', st(ok.filter(c => c.aimLastChange != null).map(c => c.aimLastChange * T)), 'charges with no aim change', ok.filter(c => c.aimLastChange == null).length);
console.log('charging s', st(ok.map(c => c.chargeTicks * T)), '| dist', st(ok.map(c => c.dist)), '| peak tiles/s', st(ok.map(c => c.peak)));
console.log('vulnerable s', st(ok.filter(c => c.vulnTicks != null).map(c => c.vulnTicks * T)));
console.log('charge state total s', st(ok.filter(c => c.chargeStateTicks != null).map(c => c.chargeStateTicks * T)));
console.log('cooldown (charge state left -> next StartCharging) s', st(ok.filter(c => c.cooldownFromPrev != null).map(c => c.cooldownFromPrev * T)));
console.log('ends', JSON.stringify(ok.reduce((m, c) => { const k = c.end + '/' + (c.hitTarget ?? 'none'); m[k] = (m[k] || 0) + 1; return m; }, {})));
console.log('hp changes |d|>=40 or positive:'); for (const h of hpch.filter(h => Math.abs(h.d) >= 40 || h.d > 0)) console.log(`  ${h.wall} ${h.e} ${h.from}->${h.to} (${h.d}) state=${h.state} phase=${h.phase} maxDmg=${h.maxDmg} near ${h.near}`);
console.log('hp change sizes (negative)', JSON.stringify(hpch.filter(h => h.d < 0).reduce((m, h) => { m[h.d] = (m[h.d] || 0) + 1; return m; }, {})));
const apply = L.filter(x => x.kind === 'MOVE-APPLY'), ms = L.filter(x => x.kind === 'MOVE-START');
const mv = ms.map(x => {
  const sk = (x.body.match(/skill=(.+?) applied/) || [])[1] || ''; const at = +((x.body.match(/applied t=([\d.]+)/) || [])[1]);
  const ap = apply.find(y => y.who === x.who && Math.abs(y.t - at) < 0.002);
  const g = r => { const m = x.body.match(r); return m ? +m[1] : null; };
  return { wall: x.wall, sk, inMs: g(/input changed \+\d+tk\/(\d+)ms/), anyMs: g(/move any \+\d+tk\/(\d+)ms/), alongMs: g(/along input \+\d+tk\/(\d+)ms/), swing: ap ? num(ap.body, 'swingLeft') : NaN, boundary: ap ? num(ap.body, 'boundary') : NaN, note: (x.body.match(/; (no move[^;]*|superseded[^;]*|the new input[^;]*)$/) || [])[1] };
});
const kind = s => (s.match(/\((\w+)[:)]/) || [])[1];
console.log(`MOVE-START ${mv.length}:`, JSON.stringify(mv.reduce((m, x) => { const k = kind(x.sk) + '/' + (x.note ? x.note.slice(0, 12) : 'moved'); m[k] = (m[k] || 0) + 1; return m; }, {})));
for (const k of ['attack', 'away', 'all']) {
  const s = mv.filter(x => (k === 'all' || kind(x.sk) === k) && x.alongMs != null);
  console.log(`  ${k}: input changed s ${st(s.filter(x => x.inMs != null).map(x => x.inMs / 1000))}; first move any s ${st(s.filter(x => x.anyMs != null).map(x => x.anyMs / 1000))}; first move along s ${st(s.map(x => x.alongMs / 1000))}`);
}
const sw = mv.filter(x => x.swing > 0 && x.alongMs != null), ns = mv.filter(x => !(x.swing > 0) && x.alongMs != null);
console.log(`  swinging at apply (swingLeft>0): first move along ${st(sw.map(x => x.alongMs / 1000))}; not swinging ${st(ns.map(x => x.alongMs / 1000))}`);
console.log(`  swinging at apply, all outcomes: ${JSON.stringify(mv.filter(x => x.swing > 0).map(x => kind(x.sk) + ':' + (x.alongMs ?? x.note?.slice(0, 10))))}`);
if (opt('--json')) fs.writeFileSync(opt('--json'), JSON.stringify({ charges, hp: hpch, moves: mv }, null, 1));
