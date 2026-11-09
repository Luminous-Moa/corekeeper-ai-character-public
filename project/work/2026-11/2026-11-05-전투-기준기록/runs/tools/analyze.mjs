// analyze.mjs --player <Player.log> --decisions <decisions-*.log> --map <probe.txt>[,<probe.txt>] [--json out.json]
// Baseline rounds "AI vs 1 MushroomBrute" from the existing log lines only (no code change in the mod).
// A round starts at "TEST-ONLY arena spawn: ... MushroomBrute" and ends at the first of: brute killed, AI DOWN, spawn + 120 s.
// Run it from any folder; every path is an argument.
import fs from 'fs';
import zlib from 'zlib';

const args = Object.fromEntries(process.argv.slice(2).reduce((a, v, i, arr) => (v.startsWith('--') ? a.concat([[v.slice(2), arr[i + 1]]]) : a), []));
const read = f => (f.endsWith('.gz') ? zlib.gunzipSync(fs.readFileSync(f)).toString('utf8') : fs.readFileSync(f, 'utf8')).split(/\r?\n/);
const P = read(args.player);
const D = args.decisions ? args.decisions.split(',').flatMap(read) : [];
const LIMIT = 120;

// ---- wall map from the /ai goto probes ("x,z top=wall" / "walk-start")
const walls = new Set(), ground = new Set();
for (const f of (args.map || '').split(',').filter(Boolean)) for (const l of read(f)) {
  const m = l.match(/^(-?\d+),(-?\d+) (\S+)/); if (!m) continue;
  (m[3].includes('wall') ? walls : ground).add(m[1] + ',' + m[2]);
}
function wallDist(x, z) { // distance from a point to the nearest probed wall tile edge (tile = 1x1 centred on the integer)
  let best = Infinity;
  for (const k of walls) { const [wx, wz] = k.split(',').map(Number); const dx = Math.max(Math.abs(x - wx) - 0.5, 0), dz = Math.max(Math.abs(z - wz) - 0.5, 0); best = Math.min(best, Math.hypot(dx, dz)); }
  return best;
}

const T = l => { const m = l.match(/ t=([0-9.]+) /); return m ? +m[1] : null; };
const claude = P.filter(l => l.includes('@Claude') || l.includes('chat command from host'));

// ---- rounds
const spawns = claude.filter(l => /TEST-ONLY arena spawn: .*spawned 1 MushroomBrute/.test(l)).map(l => ({ t: T(l), at: l.match(/at \((-?\d+),(-?\d+)\)/).slice(1).map(Number) }));
const rounds = spawns.map((s, i) => {
  const next = spawns[i + 1]?.t ?? Infinity;
  const win = claude.filter(l => { const t = T(l); return t !== null && t >= s.t && t < next; });
  let end = s.t + LIMIT, why = 'timeout';
  for (const l of win) {
    const t = T(l); if (t > s.t + LIMIT) break;
    if (/VITALS: DOWN #/.test(l)) { end = t; why = 'down'; break; }
    if (/SKILL 공격 DONE.*처치/.test(l) || /MushroomBrute hp \d+->0$/.test(l)) { end = t; why = 'kill'; break; }
  }
  return { n: i + 1, t0: s.t, spawnAt: s.at, end, why, dur: +(end - s.t).toFixed(1), lines: win.filter(l => T(l) <= end + 0.05), after: win.filter(l => T(l) > end + 0.05) };
});

// before/after state from /ai inv and /ai look (the round driver sends them around each round)
function lastBefore(re, t) { let r = null; for (const l of claude) { const tt = T(l); if (tt !== null && tt < t && re.test(l)) r = l; } return r; }
function firstAfter(re, t, until) { for (const l of claude) { const tt = T(l); if (tt !== null && tt > t && tt < until && re.test(l)) return l; } return null; }
const num = (l, re) => { const m = l && l.match(re); return m ? +m[1] : null; };

const decIn = D.filter(l => l.includes('@Claude DECISION') && l.includes(' INPUT '));
function aiSamples(r) { // AI position+hp from the decision inputs, hp also from SKILL 공격 lines, triggers, food, DOWN
  const s = [];
  for (const l of decIn) { const t = T(l); if (t < r.t0 - 0.01 || t > r.end + 0.05) continue;
    const m = l.match(/내 상태: 위치 \((-?\d+),(-?\d+)\), 체력 (\d+)\/(\d+)/); if (m) s.push({ t, x: +m[1], z: +m[2], hp: +m[3], src: 'input' }); }
  for (const l of r.lines) { const t = T(l);
    let m = l.match(/SKILL 공격: me=\((-?[0-9.]+),(-?[0-9.]+)\) myHp=(-?\d+) target MushroomBrute \((-?[0-9.]+),(-?[0-9.]+)\) d=([0-9.]+) hp=(\d+)/);
    if (m) s.push({ t, x: +m[1], z: +m[2], hp: +m[3], bx: +m[4], bz: +m[5], d: +m[6], bhp: +m[7], src: 'attack' });
    m = l.match(/trigger='(?:긴급: )?체력 감소\((\d+)→(\d+)\)'/); if (m) s.push({ t, hp: +m[2], src: 'trigger', from: +m[1] });
    m = l.match(/VITALS: DOWN #\d+ at \((-?[0-9.]+),(-?[0-9.]+)\)/); if (m) s.push({ t, x: +m[1], z: +m[2], hp: 0, src: 'down' });
  }
  return s.sort((a, b) => a.t - b.t);
}
function brutePos(r) { // brute positions: SKILL 공격 target, away facts, candidate text "적 MushroomBrute (x,z)"
  const s = [];
  for (const l of r.lines) { const t = T(l);
    let m = l.match(/target MushroomBrute \((-?[0-9.]+),(-?[0-9.]+)\)/) || l.match(/nearest enemy=MushroomBrute at \((-?[0-9.]+),(-?[0-9.]+)\)/) || l.match(/적 MushroomBrute \((-?\d+),(-?\d+)\)/);
    if (m) s.push({ t, x: +m[1], z: +m[2] });
  }
  for (const l of decIn) { const t = T(l); if (t < r.t0 || t > r.end + 0.05) continue; const m = l.match(/적 MushroomBrute \((-?\d+),(-?\d+)\)/); if (m) s.push({ t, x: +m[1], z: +m[2] }); }
  return s.sort((a, b) => a.t - b.t);
}
const at = (arr, t) => { let r = null; for (const v of arr) { if (v.t <= t + 0.001) r = v; else break; } return r; };

function category(choice) {
  if (/^attack_/.test(choice)) return 'attack';
  if (/^eat_/.test(choice)) return 'eat';
  if (/^retreat/.test(choice)) return 'retreat';
  if (/^away_/.test(choice)) return choice;
  return choice.replace(/_.*$/, '');
}
const q = (a, p) => { if (!a.length) return null; const s = [...a].sort((x, y) => x - y); return s[Math.min(s.length - 1, Math.floor(p * (s.length - 1) + 0.5))]; };

const out = [];
for (const r of rounds) {
  // damage to the brute. Explicit hits: "SKILL 공격: MushroomBrute hp a->b" (only while the attack skill runs).
  // Brute hp is also read from every other line that states it (attack START/1 s lines "hp=", candidate text "적 체력 N/540",
  // "대상 MushroomBrute 체력 N/540", decision inputs). A drop seen there but not covered by an explicit hit is a "hidden" drop:
  // damage that landed while the attack skill was not running (e.g. the swing already in flight when the skill was switched);
  // its hit count is unknown, it is bracketed by the two samples around it.
  const ev = [];
  for (const l of r.lines) { const t = T(l);
    let m = l.match(/SKILL 공격: MushroomBrute hp (\d+)->(\d+)/); if (m) { ev.push({ t, a: +m[1], b: +m[2] }); continue; }
    // samples only from lines written when the value was read: SKILL 공격 lines and DECISION SEND (RESULT/held lines repeat the SEND text)
    if (/SKILL 공격: me=/.test(l)) m = l.match(/target MushroomBrute \([^)]*\) d=[0-9.]+ hp=(\d+)/);
    else if (/DECISION #\d+ SEND/.test(l)) m = l.match(/적 MushroomBrute \([^)]*\) [0-9.]+칸, 적 체력 (\d+)\/540/) || l.match(/대상 MushroomBrute 체력 (\d+)\/540/);
    else m = null;
    if (m) ev.push({ t, s: +m[1] });
  }
  // Player.log file order is the true order (its times are rounded to 0.1 s), so no sort
  const hits = []; let hpNow = 540, lastT = r.t0, heals = 0, healSum = 0, healAt = [];
  for (const e of ev) {
    if (e.a !== undefined) {
      if (e.a < hpNow) hits.push({ t: e.t, t1: lastT, dmg: hpNow - e.a, hp: e.a, hidden: true });
      if (e.b < e.a) hits.push({ t: e.t, dmg: e.a - e.b, hp: e.b }); else if (e.b > e.a) heals++;
      hpNow = e.b; lastT = e.t;
    } else {
      if (e.s < hpNow) { hits.push({ t: e.t, t1: lastT, dmg: hpNow - e.s, hp: e.s, hidden: true }); hpNow = e.s; }
      else if (e.s > hpNow) { heals++; healSum += e.s - hpNow; healAt.push(`${(lastT - r.t0).toFixed(1)}~${(e.t - r.t0).toFixed(1)}s +${e.s - hpNow}`); hpNow = e.s; }
      lastT = e.t;
    }
  }
  if (r.why === 'kill' && hpNow > 0) { hits.push({ t: r.end, t1: lastT, dmg: hpNow, hp: 0, hidden: true }); hpNow = 0; }
  const shown = hits.filter(h => !h.hidden), hidden = hits.filter(h => h.hidden);
  const small = shown.filter(h => h.dmg <= 1), big = shown.filter(h => h.dmg > 1);
  // big-damage segments: big hits (explicit, or hidden drops > 1) with gaps <= 1.5 s and no explicit 1-damage hit between
  const segs = []; let cur = null;
  for (const h of hits) {
    if (!h.hidden && h.dmg <= 1) { cur = null; continue; }
    if (h.hidden && h.dmg <= 1) continue;
    const st = h.hidden ? h.t1 : h.t;
    if (cur && st - cur.last <= 1.5) { cur.n++; cur.sum += h.dmg; cur.last = h.t; if (h.hidden) cur.hidden++; }
    else { cur = { start: st, last: h.t, n: 1, sum: h.dmg, hidden: h.hidden ? 1 : 0 }; segs.push(cur); }
  }
  const ai = aiSamples(r), bp = brutePos(r);
  // AI-brute distance samples (SKILL 공격 d=, SEND text "대상 MushroomBrute 체력 N/540, D칸" / "적 MushroomBrute (x,z) D칸" / "가장 가까운 적 MushroomBrute와 D칸")
  const dsm = [];
  for (const l of r.lines) { const t = T(l); let m = null;
    if (/SKILL 공격: me=/.test(l)) m = l.match(/target MushroomBrute \([^)]*\) d=([0-9.]+)/);
    else if (/DECISION #\d+ SEND/.test(l)) m = l.match(/대상 MushroomBrute 체력 \d+\/540, ([0-9.]+)칸/) || l.match(/적 MushroomBrute \([^)]*\) ([0-9.]+)칸/) || l.match(/가장 가까운 적 MushroomBrute와 ([0-9.]+)칸/);
    if (m) dsm.push({ t, d: +m[1] }); }
  const dAt = t => { let b = null; for (const s of dsm) if (Math.abs(s.t - t) <= 0.5 && (!b || Math.abs(s.t - t) < Math.abs(b.t - t))) b = s; return b ? b.d : null; };
  for (const h of hits) h.d = dAt(h.t);
  // AI damage taken: drops between consecutive hp samples (merges hits closer than the sample gap)
  const taken = []; let prev = null;
  for (const s of ai) { if (prev && s.hp < prev.hp) { if (s.src === 'trigger' && s.from !== undefined && taken.length && Math.abs(taken[taken.length - 1].t - s.t) < 1 && taken[taken.length - 1].to === s.hp) { prev = s; continue; } taken.push({ t: s.t, from: prev.hp, to: s.hp, dmg: prev.hp - s.hp }); } prev = s; }
  for (const sg of segs) {
    const b = at(bp, sg.start), a = at(ai.filter(v => v.x !== undefined), sg.start);
    const hitBefore = taken.filter(k => k.t <= sg.start + 1.0 && k.t >= sg.start - 4); // AI hit from 4 s before to 1 s after the first big drop (samples lag up to ~0.5 s)
    sg.rel = +(sg.start - r.t0).toFixed(1); sg.len = +(sg.last - sg.start).toFixed(1);
    sg.aiHitBefore = hitBefore.map(k => `${(k.t - r.t0).toFixed(1)}s ${k.from}->${k.to}`).join('; ') || '없음(-4~+1초)';
    sg.brute = b ? { x: b.x, z: b.z, dt: +(sg.start - b.t).toFixed(1), wall: walls.size ? +wallDist(b.x, b.z).toFixed(1) : null } : null;
    sg.ai = a ? { x: a.x, z: a.z, dt: +(sg.start - a.t).toFixed(1), wall: walls.size ? +wallDist(a.x, a.z).toFixed(1) : null } : null;
  }
  // decisions
  const res = r.lines.filter(l => /DECISION #\d+ RESULT choice=/.test(l));
  const sends = r.lines.filter(l => /DECISION #\d+ SEND/.test(l));
  const dist = {}, apply = {}; const lat = []; let usd = 0, tok = 0;
  for (const l of res) {
    const c = category(l.match(/choice=(\S+)/)[1]); dist[c] = (dist[c] || 0) + 1;
    const m = l.match(/latency=([0-9.]+)s/); if (m) lat.push(+m[1]);
    const u = l.match(/cost=\$([0-9.]+)/); if (u) usd += +u[1];
    const k = l.match(/tokens=(\d+)/); if (k) tok += +k[1];
    const st = (l.match(/cost=\$[0-9.]+ (\S+)/) || [])[1] || '?'; apply[st] = (apply[st] || 0) + 1;
  }
  const started = {}; for (const l of r.lines) { const m = l.match(/@Claude SKILL (\S+(?: \S+)?) START/); if (m) started[m[1]] = (started[m[1]] || 0) + 1; }
  const switches = r.lines.filter(l => /@Claude SWITCH /.test(l));
  const planner = r.lines.filter(l => /PLANNER/.test(l)).length;
  const down = r.lines.find(l => /VITALS: DOWN #/.test(l));
  const invB = lastBefore(/SKILL 가방 DONE/, r.t0), invA = firstAfter(/SKILL 가방 DONE/, r.end, r.end + 60);
  const lookB = lastBefore(/SKILL 보기 DONE/, r.t0), goB = lastBefore(/TEST-ONLY arena go/, r.t0);
  const bHpEnd = hpNow;
  out.push({
    round: r.n, t0: r.t0, spawnAt: r.spawnAt, result: r.why, seconds: r.dur,
    start: { go: goB && goB.match(/teleport to \((-?\d+),(-?\d+)\)/)?.slice(1).join(','), goToSpawn: goB ? +(r.t0 - T(goB)).toFixed(1) : null,
      look: lookB && (lookB.match(/ai=\(([^)]*)\) hp=(\d+\/\d+) hunger=(\d+)/) || []).slice(1).join(' '),
      sword: num(invB, /WoodSwordx(\d+)/), food: num(invB, /FoodRationx(\d+)/) ?? 0 },
    end: { sword: num(invA, /WoodSwordx(\d+)/), food: num(invA, /FoodRationx(\d+)/) ?? 0, downAt: down && down.match(/DOWN #\d+ at \(([^)]*)\) hunger (\d+)/)?.slice(1).join(' hunger ') },
    aiTaken: { n: taken.length, sum: taken.reduce((a, k) => a + k.dmg, 0), list: taken.map(k => `${(k.t - r.t0).toFixed(1)}s ${k.from}->${k.to}`) },
    brute: { hits: shown.length, small: small.length, smallSum: small.reduce((a, h) => a + h.dmg, 0), big: big.length, bigSum: big.reduce((a, h) => a + h.dmg, 0),
      hidden: hidden.map(h => `${(h.t1 - r.t0).toFixed(1)}~${(h.t - r.t0).toFixed(1)}s -${h.dmg}`), hiddenSum: hidden.reduce((a, h) => a + h.dmg, 0), hpEnd: bHpEnd, hpUps: heals, healSum, healAt, dealt: hits.reduce((a, h) => a + h.dmg, 0),
      bigList: hits.filter(h => h.dmg > 1).map(h => `${(h.t - r.t0).toFixed(1)}s ${h.hidden ? 'hidden ' : ''}-${h.dmg} d=${h.d ?? '?'}`),
      segments: segs.map(s => ({ rel: s.rel, len: s.len, n: s.n, hidden: s.hidden, sum: s.sum, aiHitBefore: s.aiHitBefore, brute: s.brute, ai: s.ai })) },
    decisions: { sends: sends.length, results: res.length, dist, status: apply, latP50: q(lat, 0.5), latP95: q(lat, 0.95), latMax: lat.length ? Math.max(...lat) : null, usd: +usd.toFixed(6), tokens: tok, perMin: +(res.length / (r.dur / 60)).toFixed(0) },
    skills: started, switches: switches.length, emergencySwitches: switches.filter(l => /at emergency/.test(l)).length, plannerLines: planner,
  });
}

// ---- report
const pct = (a, b) => (b ? (100 * a / b).toFixed(0) + '%' : '-');
console.log('# rounds', rounds.length, 'walls in map', walls.size);
for (const o of out) {
  console.log(`\n## R${o.round} t0=${o.t0} spawn (${o.spawnAt}) -> ${o.result} ${o.seconds}s`);
  console.log(`start: go ${o.start.go} (spawn ${o.start.goToSpawn}s after go), look ${o.start.look}, sword ${o.start.sword}, food ${o.start.food}`);
  console.log(`end: sword ${o.end.sword}, food ${o.end.food}, down ${o.end.downAt ?? '-'}`);
  console.log(`AI took ${o.aiTaken.n} hits, ${o.aiTaken.sum} hp: ${o.aiTaken.list.join(' | ')}`);
  console.log(`brute: explicit hits ${o.brute.hits}, 1-dmg ${o.brute.small} (${pct(o.brute.small, o.brute.hits)}), big ${o.brute.big} sum ${o.brute.bigSum}; hidden drops ${o.brute.hidden.length} sum ${o.brute.hiddenSum} [${o.brute.hidden.join(', ')}]; hp end ${o.brute.hpEnd}, healed ${o.brute.healSum} [${o.brute.healAt.join(", ")}], dealt ${o.brute.dealt}`);
  console.log(`  big drops (time, size, AI-brute distance within 0.5 s): ${o.brute.bigList.join(' | ')}`);
  for (const s of o.brute.segments) console.log(`  segment +${s.rel}s len ${s.len}s: ${s.n} events (${s.hidden} hidden) ${s.sum} dmg; AI hit before: ${s.aiHitBefore}; brute ${s.brute ? `(${s.brute.x},${s.brute.z}) ${s.brute.dt}s old, wall ${s.brute.wall}` : '?'}; AI ${s.ai ? `(${s.ai.x},${s.ai.z}) ${s.ai.dt}s old, wall ${s.ai.wall}` : '?'}`);
  console.log(`decisions: SEND ${o.decisions.sends}, RESULT ${o.decisions.results} (${o.decisions.perMin}/min), ${JSON.stringify(o.decisions.dist)}, status ${JSON.stringify(o.decisions.status)}, latency p50 ${o.decisions.latP50} p95 ${o.decisions.latP95} max ${o.decisions.latMax}, $${o.decisions.usd}`);
  console.log(`skills started ${JSON.stringify(o.skills)}, switches ${o.switches} (emergency ${o.emergencySwitches}), PLANNER lines ${o.plannerLines}`);
}
const sum = (f) => out.reduce((a, o) => a + f(o), 0);
console.log('\n## total');
console.log(`results: ${JSON.stringify(out.reduce((a, o) => (a[o.result] = (a[o.result] || 0) + 1, a), {}))}`);
console.log(`brute explicit hits ${sum(o => o.brute.hits)}, 1-dmg ${sum(o => o.brute.small)} (${pct(sum(o => o.brute.small), sum(o => o.brute.hits))}) sum ${sum(o => o.brute.smallSum)}, big ${sum(o => o.brute.big)} sum ${sum(o => o.brute.bigSum)}, hidden drops ${sum(o => o.brute.hidden.length)} sum ${sum(o => o.brute.hiddenSum)}, dealt ${sum(o => o.brute.dealt)}, healed ${sum(o => o.brute.healSum)}, segments ${sum(o => o.brute.segments.length)}`);
console.log(`AI hits taken ${sum(o => o.aiTaken.n)}, hp ${sum(o => o.aiTaken.sum)}`);
const dist = {}; for (const o of out) for (const [k, v] of Object.entries(o.decisions.dist)) dist[k] = (dist[k] || 0) + v;
console.log(`decisions RESULT ${sum(o => o.decisions.results)}, ${JSON.stringify(dist)}, $${sum(o => o.decisions.usd).toFixed(6)}`);
const lat = []; for (const l of rounds.flatMap(r => r.lines)) { const m = /DECISION #\d+ RESULT/.test(l) && l.match(/latency=([0-9.]+)s/); if (m) lat.push(+m[1]); }
console.log(`latency all rounds p50 ${q(lat, 0.5)} p95 ${q(lat, 0.95)} max ${Math.max(...lat)} n ${lat.length}`);
if (args.json) fs.writeFileSync(args.json, JSON.stringify(out, null, 1));
