// analyze.mjs — combat stage 2 live run analysis (2026-11-05-전투-단계2). Reads Player.log (and optionally the decision
// log for the answer kinds) and prints per-round facts next to the baseline's columns, the prediction vs result table,
// latencies, candidate counts, composite actions and the async contract cases. Paths are arguments.
//   node analyze.mjs --player <Player.log> [--decisions <decisions-*.log>] [--json <out.json>]
import fs from 'node:fs';
import zlib from 'node:zlib';

const args = Object.fromEntries(process.argv.slice(2).reduce((a, v, i, arr) => (v.startsWith('--') ? a.concat([[v.slice(2), arr[i + 1]]]) : a), []));
const read = (p) => (p.endsWith('.gz') ? zlib.gunzipSync(fs.readFileSync(p)).toString('utf8') : fs.readFileSync(p, 'utf8'));
const lines = read(args.player).split(/\r?\n/).map((l) => { const m = l.match(/\[AIPlayerSpike\] t=(-?[\d.]+) (?:@(\S+) )?(.*)$/); return m ? { t: +m[1], who: m[2] || '', msg: m[3] } : null; }).filter(Boolean);
const q = (xs, p) => { if (!xs.length) return NaN; const s = [...xs].sort((a, b) => a - b); return s[Math.min(s.length - 1, Math.max(0, Math.ceil(p * s.length) - 1))]; };
const f2 = (x) => (Number.isFinite(x) ? x.toFixed(2) : '-');
const out = { rounds: [] };

// rounds: an arena spawn starts one; the end is the first DOWN, brute death (hp ->0) or the self-defence end, else +120 s
const spawns = lines.filter((l) => l.msg.startsWith('TEST-ONLY arena spawn: cleared'));
spawns.forEach((sp, i) => {
  const t0 = sp.t, tNext = i + 1 < spawns.length ? spawns[i + 1].t : Infinity;
  const brute = /MushroomBrute/.test(sp.msg);
  const win = lines.filter((l) => l.t >= t0 && l.t < Math.min(tNext, t0 + 125));
  let end = null, result = 'timeout';
  for (const l of win) {
    if (l.msg.startsWith('VITALS: DOWN #')) { end = l.t; result = 'down'; break; }
    if (brute && /^BRUTE-HP e=\d+ \d+->0 /.test(l.msg)) { end = l.t; result = 'kill'; break; }
    if (!brute && /^SELF-DEFENCE #\d+ end/.test(l.msg)) { end = l.t; result = 'fight over'; break; }
  }
  end = end ?? t0 + 120;
  const W = win.filter((l) => l.t <= end + 0.05);
  const r = { n: i + 1, kind: brute ? 'MushroomBrute 1' : sp.msg.replace(/.*spawned /, ''), t0, end, dur: end - t0, result };
  // AI health: the hits it took
  r.aiHits = W.filter((l) => /^VITALS: one hit|^VITALS: DOWN/.test(l.msg)).map((l) => l.msg.slice(0, 90));
  // brute damage
  const hp = W.filter((l) => l.msg.startsWith('BRUTE-HP')).map((l) => { const m = l.msg.match(/BRUTE-HP e=\d+ (\d+)->(\d+) vuln=(\d) phase=(\w+)(?: vulnLeft=([\d.]+))? charge#(\d+)/); return m && { t: l.t, a: +m[1], b: +m[2], vuln: +m[3], phase: m[4], charge: +m[6] }; }).filter(Boolean);
  const drops = hp.filter((h) => h.b < h.a), rises = hp.filter((h) => h.b > h.a);
  r.bruteDamage = drops.reduce((s, h) => s + h.a - h.b, 0);
  r.oneDamage = drops.filter((h) => h.a - h.b === 1).length;
  r.bigHits = drops.filter((h) => h.a - h.b > 1).length; r.bigDamage = drops.filter((h) => h.a - h.b > 1).reduce((s, h) => s + h.a - h.b, 0);
  r.bruteRegen = rises.map((h) => `${h.a}->${h.b}@+${(h.t - t0).toFixed(1)}`);
  r.bruteEndHp = hp.length ? hp[hp.length - 1].b : 540;
  // vulnerability windows (Vulnerable phase of a charge) and hits in each
  const ch = W.filter((l) => l.msg.startsWith('CHARGE e='));
  const windows = [];
  for (const l of ch) {
    const m = l.msg.match(/CHARGE e=\d+ #(\d+) (\w+)->(\w+)/); if (!m) continue;
    if (m[3] === 'Vulnerable') windows.push({ charge: +m[1], t: l.t, hits: 0, dmg: 0 });
  }
  for (const w of windows) {
    const ws = drops.filter((h) => h.t >= w.t && h.t <= w.t + 3.65);
    w.hits = ws.filter((h) => h.a - h.b > 1).length; w.dmg = ws.reduce((s, h) => s + h.a - h.b, 0);
  }
  r.windows = windows.map((w) => `#${w.charge}: ${w.hits} big hits, ${w.dmg}`);
  // charges aimed at the AI: hit / not hit; first move; the last pick before the charge resolved, its prediction
  r.charges = [];
  // charges that started (anticipation) before the round end, with their end line up to 6 s later (a charge that downs
  // the AI ends after the down)
  const started = new Set(ch.filter((x) => /Ready->Anticipation/.test(x.msg)).map((x) => x.msg.match(/#(\d+) /)[1]));
  const chEnd = win.filter((l) => l.t <= end + 6 && l.msg.startsWith('CHARGE e=') && / end: /.test(l.msg) && started.has((l.msg.match(/#(\d+) end/) || [])[1]));
  for (const l of chEnd) {
    const m = l.msg.match(/#(\d+) end: target=(\S+) hp (-?\d+)->(-?\d+) \((HIT|not hit)\), connected=(\S+)(?: run=([\d.]+))?, lasted ([\d.]+)s; firstMove=(\S+); picks=\[(.*)\]$/);
    if (!m) continue;
    const vul = ch.find((x) => x.msg.startsWith(`CHARGE e=`) && x.msg.includes(` #${m[1]} Charging->`) && x.t <= l.t && x.t >= l.t - 8);
    const sinceVul = vul ? +(vul.msg.match(/since-anticipation=([\d.]+)/) || [0, NaN])[1] : NaN;
    const picks = m[10] ? m[10].split('; ').map((p) => { const pm = p.match(/#(\d+) (\S+) pred=(\S+) @\+([\d.]+)s phase=(\w+)/); return pm && { req: +pm[1], key: pm[2], pred: pm[3], at: +pm[4], phase: pm[5] }; }).filter(Boolean) : [];
    const before = picks.filter((p) => (p.phase === 'Anticipation' || p.phase === 'Charging') && (!Number.isFinite(sinceVul) || p.at <= sinceVul));
    const last = before[before.length - 1];
    r.charges.push({ charge: +m[1], target: m[2], hit: (+m[3] - +m[4]) >= 30, liveHitText: m[5], run: m[7] ? +m[7] : null, firstMove: m[9], connectAt: sinceVul, picksInCharge: before.map((p) => `#${p.req} ${p.key} ${p.pred} @+${p.at}`), lastPick: last ? `${last.key} ${last.pred}` : '-', predHit: last ? last.pred.startsWith('hit') : null, predWall: last ? /wall/.test(last.pred) : null });
  }
  // decisions in the round
  const res = W.filter((l) => /^DECISION #\d+ RESULT/.test(l.msg)).map((l) => { const m = l.msg.match(/choice=(\S+).*latency=([\d.]+)s tokens=(\d+)/); return m && { choice: m[1], lat: +m[2], tok: +m[3] }; }).filter(Boolean);
  const sends = W.filter((l) => /^DECISION #\d+ SEND/.test(l.msg)).map((l) => +(l.msg.match(/candidates=(\d+)/) || [0, 0])[1]);
  r.decisions = res.length; r.latP50 = q(res.map((x) => x.lat), 0.5); r.latP95 = q(res.map((x) => x.lat), 0.95); r.latMax = Math.max(...res.map((x) => x.lat));
  r.candMax = Math.max(...sends); r.candP95 = q(sends, 0.95); r.candP50 = q(sends, 0.5); r.tokP50 = q(res.map((x) => x.tok), 0.5); r.tokP95 = q(res.map((x) => x.tok), 0.95);
  const kinds = {}; for (const x of res) { const k = x.choice.replace(/_\d+$/, '').replace(/_-?\d+$/, '').replace(/_[a-z]+_\d$/, (s) => s); kinds[k] = (kinds[k] || 0) + 1; }
  r.choices = Object.entries(kinds).sort((a, b) => b[1] - a[1]).map(([k, v]) => `${k} ${v}`).join(', ');
  r.urgent = W.filter((l) => /^DECISION urgent: charge/.test(l.msg)).length;
  r.urgentAborted = W.filter((l) => /^DECISION urgent: charge.*dropped/.test(l.msg)).length;
  r.discard = W.filter((l) => /DISCARD: /.test(l.msg)).map((l) => l.msg.replace(/.*DISCARD: /, '')).reduce((a, s) => { const k = s.replace(/#\d+/g, '#').replace(/\(.*/, ''); a[k] = (a[k] || 0) + 1; return a; }, {});
  r.failed = W.filter((l) => /^DECISION #\d+ (failed|no usable|HTTP)/.test(l.msg)).map((l) => l.msg.slice(0, 120));
  r.combos = W.filter((l) => /^DECISION #\d+ PICK combo:/.test(l.msg)).map((l) => l.msg.replace(/^DECISION #\d+ PICK /, '').replace(/ pred=.*/, ''));
  r.comboStages = W.filter((l) => /^EXEC #\d+ combo/.test(l.msg)).map((l) => `+${(l.t - t0).toFixed(2)} ${l.msg}`);
  r.picksStartFail = W.filter((l) => /^SKILL (짧은 이동|한 번 치기|사거리까지 접근|복합 행동) FAILED after 0\.0s/.test(l.msg)).map((l) => l.msg.slice(0, 140));
  r.shortEnds = W.filter((l) => /^SKILL (짧은 이동|한 번 치기|사거리까지 접근|복합 행동|제자리 유지) (DONE|FAILED|CANCELLED)/.test(l.msg)).reduce((a, l) => { const m = l.msg.match(/^SKILL (\S+(?: \S+)*?) (DONE|FAILED|CANCELLED)/); const k = `${m[1]} ${m[2]}`; a[k] = (a[k] || 0) + 1; return a; }, {});
  r.moveStarts = W.filter((l) => /^EXEC #\d+ step move-start: prior/.test(l.msg)).map((l) => { const m = l.msg.match(/prior=(\w+) input \+([\d.]+)s server-input \+(\S+)s along \+([\d.]+)s/); return m && { prior: m[1], server: +m[3], along: +m[4] }; }).filter(Boolean);
  out.rounds.push(r);
});

// composite hit -> move transition vs. two single actions (hit once, then a step chosen by the next decision)
const trans = { combo: [], single: [] };
for (const l of lines) {
  const m = l.msg.match(/^EXEC #(\d+) combo .* stage 2\/2 start 짧은 이동 .*, ([\d.]+)s after the swing started/);
  if (m) { const run = m[1]; const ms = lines.find((x) => x.t >= l.t && x.t <= l.t + 2 && x.msg.startsWith(`EXEC #${run} step move-start: prior`)); if (ms) { const a = +(ms.msg.match(/along \+([\d.]+)s/) || [0, NaN])[1]; trans.combo.push(+m[2] + a); } }
}
const hitDone = lines.filter((l) => /^SKILL 한 번 치기 DONE .*휘두르기 시작 \+([\d.]+)초/.test(l.msg));
for (const h of hitDone) {
  const dur = +(h.msg.match(/after ([\d.]+)s/) || [0, NaN])[1]; const st = +(h.msg.match(/휘두르기 시작 \+([\d.]+)초/) || [0, NaN])[1];
  const swingAt = h.t - dur + st;
  const next = lines.find((x) => x.t >= h.t && x.t <= h.t + 1.5 && /^SKILL /.test(x.msg) && / START /.test(x.msg));
  if (!next || !/^SKILL 짧은 이동 START/.test(next.msg)) continue;
  const ms = lines.find((x) => x.t >= next.t && x.t <= next.t + 1.5 && /^EXEC #\d+ step move-start: prior/.test(x.msg));
  if (ms) trans.single.push(ms.t - swingAt);
}
out.transition = { combo: trans.combo, single: trans.single };

for (const r of out.rounds) {
  console.log(`R${r.n} ${r.kind}: ${r.result} at +${r.dur.toFixed(1)}s; brute end hp ${r.bruteEndHp}, damage ${r.bruteDamage} (1-damage ${r.oneDamage}, big ${r.bigHits} = ${r.bigDamage})${r.bruteRegen.length ? ', regen ' + r.bruteRegen.join(' ') : ''}`);
  console.log(`   AI hits: ${r.aiHits.join(' | ') || '-'}`);
  console.log(`   windows: ${r.windows.join('; ') || '-'}`);
  for (const c of r.charges) console.log(`   charge #${c.charge} -> ${c.target}: ${c.hit ? 'HIT' : 'not hit'}, connected after ${f2(c.connectAt)}s run ${c.run ?? '-'}, first move ${c.firstMove}, last pick ${c.lastPick} (${c.picksInCharge.length} picks in it)`);
  console.log(`   decisions ${r.decisions}, latency p50 ${f2(r.latP50)} p95 ${f2(r.latP95)} max ${f2(r.latMax)}; candidates p50 ${r.candP50} p95 ${r.candP95} max ${r.candMax}; tokens p50 ${r.tokP50} p95 ${r.tokP95}; urgent ${r.urgent} (request dropped ${r.urgentAborted})`);
  console.log(`   choices: ${r.choices}`);
  console.log(`   short action ends: ${JSON.stringify(r.shortEnds)}; start failures ${r.picksStartFail.length}; discards ${JSON.stringify(r.discard)}; failed calls ${r.failed.length}`);
  if (r.combos.length) console.log(`   composite picks: ${r.combos.join(', ')}`);
  const ms = r.moveStarts; if (ms.length) console.log(`   step move-start along p50 ${f2(q(ms.map((x) => x.along), 0.5))} p95 ${f2(q(ms.map((x) => x.along), 0.95))} (n ${ms.length}; prior ${Object.entries(ms.reduce((a, x) => ((a[x.prior] = (a[x.prior] || 0) + 1), a), {})).map(([k, v]) => k + ' ' + v).join(', ')})`);
}
// prediction vs result over all brute charges at the AI with a pick made during the charge
const all = out.rounds.flatMap((r) => r.charges.filter((c) => c.target === 'AI:Claude'));
const withPick = all.filter((c) => c.predHit !== null);
const tab = { predHit_actHit: 0, predHit_actMiss: 0, predMiss_actHit: 0, predMiss_actMiss: 0 };
for (const c of withPick) tab[`pred${c.predHit ? 'Hit' : 'Miss'}_act${c.hit ? 'Hit' : 'Miss'}`]++;
out.predict = { charges: all.length, hit: all.filter((c) => c.hit).length, withPick: withPick.length, ...tab };
console.log(`charges at the AI ${all.length}: hit ${out.predict.hit}, dodged ${all.length - out.predict.hit}; with a pick during the charge ${withPick.length}: ${JSON.stringify(tab)}`);
const fm = all.map((c) => (c.firstMove === '-' ? NaN : +c.firstMove.replace(/[+s]/g, ''))).filter(Number.isFinite);
console.log(`anticipation -> first move along a chosen move: n ${fm.length}/${all.length}, p50 ${f2(q(fm, 0.5))} p95 ${f2(q(fm, 0.95))} max ${f2(Math.max(...fm))}`);
console.log(`hit -> move transition (swing start -> first move along): composite n ${trans.combo.length} p50 ${f2(q(trans.combo, 0.5))} max ${f2(Math.max(...trans.combo))}; two single actions n ${trans.single.length} p50 ${f2(q(trans.single, 0.5))} max ${f2(Math.max(...trans.single))}`);
if (args.json) fs.writeFileSync(args.json, JSON.stringify(out, null, 1));
