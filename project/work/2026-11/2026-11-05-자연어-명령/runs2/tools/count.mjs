// count.mjs <Player.log> <plan.jsonl> [sinceIso] — the live run's numbers: reports by trigger and member, answers,
// periodic gaps, fight ends vs fights, problem reports, chat shown vs hidden, and the bridge's tokens.
import fs from 'node:fs';
const [, , logPath, planPath, since] = process.argv;
const L = fs.readFileSync(logPath, 'utf8').split('\n');
const t = (l) => +((l.match(/t=([\d.]+)/) || [])[1] || 0);
const who = (l) => (l.match(/ @(\w+) /) || [])[1] || '-';
const by = {};
const sent = L.filter((l) => /PLANNER: report #\d+ sent trigger=/.test(l));
for (const l of sent) { const m = who(l), tr = l.match(/trigger=(\w+)/)[1]; (by[m] ??= {})[tr] = (by[m][tr] || 0) + 1; }
console.log('reports sent by member and trigger:', JSON.stringify(by));
const per = {};
for (const l of sent) { const m = who(l); (per[m] ??= []).push([t(l), l.match(/trigger=(\w+)/)[1]]); }
for (const [m, a] of Object.entries(per)) {
  const gaps = []; for (let i = 1; i < a.length; i++) if (a[i][1] === 'periodic') gaps.push(Math.round(a[i][0] - a[i - 1][0]));
  console.log(`${m}: periodic gaps after the previous report (s): ${gaps.join(', ') || '-'}`);
}
const ans = L.filter((l) => /PLANNER: report #\d+ APPLY/.test(l));
const dec = {}; for (const l of ans) { const m = who(l), d = l.match(/decision=(\w+)/)[1]; (dec[m] ??= {})[d] = (dec[m][d] || 0) + 1; }
console.log('applied answers by decision:', JSON.stringify(dec));
console.log('dropped answers:', L.filter((l) => /answer DROPPED/.test(l)).length, L.filter((l) => /answer DROPPED/.test(l)).map((l) => who(l) + ' ' + (l.match(/DROPPED: (.*)/) || [])[1]).join(' | '));
const fights = L.filter((l) => /EXP: fight ended:/.test(l));
const fe = L.filter((l) => /fight_end trigger #/.test(l));
console.log(`fights recorded: ${fights.length}; fight_end triggers: ${fe.length}`);
for (const l of fights) console.log('  fight', who(l), (l.match(/fight ended: (.*)/) || [])[1].slice(0, 160));
console.log('downs:', L.filter((l) => /VITALS: DOWN #/.test(l)).map((l) => who(l)).join(', ') || 0);
console.log('ORDER changes:'); for (const l of L.filter((l) => /\bORDER \w+:/.test(l))) console.log('  ' + l.replace(/^.*ORDER /, '').slice(0, 200));
console.log('PROBLEM reports:', L.filter((l) => /PROBLEM /.test(l)).length); for (const l of L.filter((l) => /PROBLEM /.test(l))) console.log('  ' + l.slice(0, 260));
const shown = L.filter((l) => /chat reply: /.test(l)), hidden = L.filter((l) => /chat hidden \(status\): /.test(l));
console.log(`chat lines shown ${shown.length}, hidden (status) ${hidden.length}`);
const kinds = {}; for (const l of shown) { const k = /\] \[?[^\]]*\]? ?AI \S+ (완료|실패|취소)/.test(l) ? 'SKILL-END-SHOWN' : /chat reply: \[\w+\] /.test(l) ? 'say-or-named' : 'other'; kinds[k] = (kinds[k] || 0) + 1; }
console.log('shown kinds:', JSON.stringify(kinds));
for (const l of shown) console.log('  shown: ' + l.replace(/^.*chat reply: /, '').slice(0, 150));
const hk = {}; for (const l of hidden) { const k = (l.match(/: (?:\[\w+\] )?(AI [^ :]+(?: [^ :]+)?)/) || [])[1] || 'other'; hk[k] = (hk[k] || 0) + 1; }
console.log('hidden kinds:', JSON.stringify(hk));
if (planPath && fs.existsSync(planPath)) {
  let P = fs.readFileSync(planPath, 'utf8').trim().split('\n').filter(Boolean).map((x) => JSON.parse(x));
  P = P.filter((x) => x.member && (!since || x.t >= since));
  const s = {};
  for (const x of P) { const o = s[x.member] ??= { n: 0, ok: 0, inp: 0, cached: 0, out: 0, ms: 0, trig: {} }; o.n++; if (x.ok) o.ok++; o.inp += x.usage?.input || 0; o.cached += x.usage?.cached || 0; o.out += (x.usage?.output || 0) + (x.usage?.reasoning || 0); o.ms += x.ms || 0; o.trig[x.trigger] = (o.trig[x.trigger] || 0) + 1; }
  for (const [m, o] of Object.entries(s)) console.log(`bridge ${m}: ${o.n} decisions (${o.ok} ok), input ${o.inp} (cached ${o.cached}), output ${o.out}, per decision input ${Math.round(o.inp / o.n)} output ${Math.round(o.out / o.n)}, mean ${Math.round(o.ms / o.n / 100) / 10}s, by trigger ${JSON.stringify(o.trig)}`);
}
