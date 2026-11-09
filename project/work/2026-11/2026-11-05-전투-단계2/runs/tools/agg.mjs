// agg.mjs <Player.log> <rounds.json>: totals over the brute rounds and the regression round (latency, candidates, tokens, choices)
import fs from 'node:fs';
const [,, P, R] = process.argv;
const L = fs.readFileSync(P, 'utf8').split(/\r?\n/).map((l) => { const m = l.match(/\[AIPlayerSpike\] t=(-?[\d.]+) (?:@(\S+) )?(.*)$/); return m ? { t: +m[1], msg: m[3] } : null; }).filter(Boolean);
const rounds = JSON.parse(fs.readFileSync(R, 'utf8')).rounds;
const q = (xs, p) => { const s = [...xs].sort((a, b) => a - b); return s.length ? s[Math.min(s.length - 1, Math.max(0, Math.ceil(p * s.length) - 1))] : NaN; };
for (const [name, rs] of [['brute R1-R6', rounds.filter((r) => r.kind.startsWith('MushroomBrute'))], ['regression R7', rounds.filter((r) => !r.kind.startsWith('MushroomBrute'))]]) {
  const inR = (t) => rs.some((r) => t >= r.t0 && t <= r.end + 0.05);
  const sends = new Map(); const res = [];
  for (const l of L) {
    if (!inR(l.t)) continue;
    let m = l.msg.match(/^DECISION #(\d+) SEND.*candidates=(\d+) inputChars=(\d+)/); if (m) sends.set(+m[1], { cand: +m[2], chars: +m[3] });
    m = l.msg.match(/^DECISION #(\d+) RESULT choice=(\S+).*latency=([\d.]+)s tokens=(\d+)/); if (m) res.push({ id: +m[1], choice: m[2], lat: +m[3], tok: +m[4] });
  }
  const c = [...sends.values()].map((x) => x.cand);
  const byC = {}; for (const r of res) { const s = sends.get(r.id); if (!s) continue; const b = s.cand <= 6 ? '<=6' : s.cand <= 14 ? '7-14' : s.cand <= 20 ? '15-20' : '21+'; (byC[b] = byC[b] || []).push(r); }
  console.log(`${name}: requests ${sends.size}, answers ${res.length}; candidates p50 ${q(c, .5)} p95 ${q(c, .95)} max ${Math.max(...c)}; latency p50 ${q(res.map((r) => r.lat), .5)} p95 ${q(res.map((r) => r.lat), .95)} max ${Math.max(...res.map((r) => r.lat))}; tokens p50 ${q(res.map((r) => r.tok), .5)} p95 ${q(res.map((r) => r.tok), .95)} max ${Math.max(...res.map((r) => r.tok))}`);
  for (const [b, xs] of Object.entries(byC)) console.log(`   candidates ${b}: n ${xs.length}, latency p50 ${q(xs.map((r) => r.lat), .5)} p95 ${q(xs.map((r) => r.lat), .95)}, tokens p50 ${q(xs.map((r) => r.tok), .5)}`);
  const k = {}; for (const r of res) { const kk = r.choice.replace(/_\d+$/, '').replace(/^(step_[a-z]+)_\d$/, '$1').replace(/^(combo_hit-[a-z]+)_\d$/, '$1'); k[kk] = (k[kk] || 0) + 1; }
  console.log('   choices: ' + Object.entries(k).sort((a, b) => b[1] - a[1]).map(([a, b]) => `${a} ${b}`).join(', '));
}
