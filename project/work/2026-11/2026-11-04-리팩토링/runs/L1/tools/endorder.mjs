import fs from 'node:fs';
const [f, t0] = process.argv.slice(2);
const ls = fs.readFileSync(f, 'utf8').split('\n').map((l) => l.replace(/\r$/, '')).filter((l) => /\[AIPlayerSpike\] t=[\d.]+ @Claude /.test(l))
  .map((l) => { const m = l.match(/t=([\d.]+) @Claude (.*)/); return { t: +m[1], s: m[2] }; }).filter((x) => x.t >= +t0);
const norm = (s) => s.replace(/(chat reply: AI [^:]{0,30}).*/, '$1').replace(/^(SKILL \S+ \S+).*/, '$1').replace(/^(EXP: \S+ \S+).*/, '$1').replace(/^(PLANNER: report #\d+ \S+( trigger=\S+)?).*/, '$1').replace(/^(GOAL \S+).*/, '$1').replace(/#\d+/g, '#').slice(0, 60);
for (let i = 0; i < ls.length; i++) {
  const m = ls[i].s.match(/^GOAL (done|stalled|ended) rev=\d+: (\S+)/); if (!m) continue;
  const seq = []; for (let j = i; j < ls.length && ls[j].t - ls[i].t <= 0.3; j++) if (!/^(DECISION|BRAIN|SWITCH|ASM|AI character|MEMORY|FOOD|TUNNEL)/.test(ls[j].s)) seq.push(norm(ls[j].s));
  console.log(`${m[1]} ${m[2].slice(0, 14)}: ${seq.join(' > ')}`);
}
