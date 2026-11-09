// extra.mjs <Player.log> <rounds.json> : the ② checks over the brute rounds — standing gaps after a short action ended by
// itself, continue answers dropped because the action had ended, "hit again" swings, left/right picks (tie / differs /
// alone), the continuing attack's prediction during a charge, the kill-intent line.
import fs from 'node:fs';
const [,, P, R] = process.argv;
const L = fs.readFileSync(P, 'utf8').split(/\r?\n/).map((l) => { const m = l.match(/\[AIPlayerSpike\] t=(-?[\d.]+) (?:@(\S+) )?(.*)$/); return m ? { t: +m[1], msg: m[3] } : null; }).filter(Boolean);
const rounds = JSON.parse(fs.readFileSync(R, 'utf8')).rounds.filter((r) => r.kind.startsWith('MushroomBrute'));
const inR = (t) => rounds.some((r) => t >= r.t0 && t <= r.end + 0.05);
const q = (xs, p) => { const s = [...xs].sort((a, b) => a - b); return s.length ? s[Math.min(s.length - 1, Math.max(0, Math.ceil(p * s.length) - 1))] : NaN; };
const W = L.filter((l) => inR(l.t));
// standing gaps: a short / composite action ends DONE by itself -> the next SKILL START
const gaps = [];
for (let i = 0; i < W.length; i++) {
  const l = W[i];
  if (!/^SKILL (짧은 이동|한 번 치기|사거리까지 접근|제자리 유지|복합 행동) DONE/.test(l.msg)) continue;
  const nx = W.slice(i + 1).find((x) => /^SKILL \S+.* START /.test(x.msg) && !/보기|가방/.test(x.msg));
  if (nx && nx.t - l.t < 5) gaps.push(nx.t - l.t);
}
const answers = W.filter((l) => /^DECISION #\d+ RESULT/.test(l.msg)).length;
const staleEnded = W.filter((l) => /DISCARD: 계속할 기술이 이미 끝남|DISCARD: 계속하라던 실행 #\d+이 이미 끝남/.test(l.msg)).length;
const contFact = W.filter((l) => /^DECISION #\d+ RESULT/.test(l.msg) && /남은 시간: /.test(l.msg)).length;
const lr = { tie: 0, differs: 0, alone: 0 }, lrSide = { tie: { left: 0, right: 0 }, differs: { left: 0, right: 0 }, alone: { left: 0, right: 0 } };
for (const l of W) { const m = l.msg.match(/^DECISION #\d+ PICK (\S+) .* lr=(tie|differs|alone)/); if (m) { lr[m[2]]++; lrSide[m[2]][/left/.test(m[1]) ? 'left' : 'right']++; } }
const nextApply = L.filter((l) => /next swing APPLY/.test(l.msg)).length, nextQueued = L.filter((l) => /QUEUED \(next swing/.test(l.msg)).length;
const kill = L.filter((l) => /kill intent line on/.test(l.msg)).length;
console.log(`answers ${answers}; continue answers dropped because the action had ended: ${staleEnded} (${(100 * staleEnded / Math.max(1, answers)).toFixed(1)}% of answers)`);
console.log(`standing gap after a short/composite action ended by itself -> next start: n ${gaps.length}, p50 ${q(gaps, .5)?.toFixed(2)} p95 ${q(gaps, .95)?.toFixed(2)} mean ${(gaps.reduce((a, b) => a + b, 0) / Math.max(1, gaps.length)).toFixed(2)} s, sum ${gaps.reduce((a, b) => a + b, 0).toFixed(1)} s`);
console.log(`hit again: queued ${nextQueued}, next swing APPLY ${nextApply}`);
console.log(`left/right picks: ${JSON.stringify(lr)} by side ${JSON.stringify(lrSide)}`);
console.log(`kill intent spawns ${kill}`);
