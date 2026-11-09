// The 100-case Korean set (2026-11-08-celeris-가상비교/100-ko/cases.json) as request bodies, with option kinds read from the
// source decisions logs (the "cN [kind key]" list of the same request).
import fs from 'fs';
import path from 'path';
const [casesFile, logDir, out] = process.argv.slice(2);
const cases = JSON.parse(fs.readFileSync(casesFile, 'utf8'));
const want = {};
for (const c of cases) { const log = c.log, n = c.n; (want[log] ||= new Set()).add(`@${c.name} DECISION #${n} INPUT`); }
const kinds = {};
for (const [log, keys] of Object.entries(want)) {
  const text = fs.readFileSync(path.join(logDir, log), 'utf8');
  for (const line of text.split(/\r?\n/)) {
    const m = line.match(/@(\w+) DECISION #(\d+) INPUT .*options\(\d+\): (.*)$/); if (!m) continue;
    const k = `@${m[1]} DECISION #${m[2]} INPUT`; if (!keys.has(k)) continue;
    const map = {}; for (const x of m[3].matchAll(/(?:^|\|\| |\n| )(c\d+) \[([a-z_]+) ([^\]]*)\]/g)) map[x[1]] = { kind: x[2], key: x[3] };
    kinds[`${log}#${m[2]}@${m[1]}`] = map;
  }
}
const bodies = [], meta = []; let miss = 0;
for (const c of cases) {
  const map = kinds[`${c.log}#${c.n}@${c.name}`] || {}; if (!kinds[`${c.log}#${c.n}@${c.name}`]) miss++;
  // older builds name each choice by its key (KeyLabel: "attack_62543", "dig_12_-32", "goto_3_4"): the kind is the prefix
  const alias = { goto: 'explore', travel: 'explore', control: 'control' };
  for (const ch of c.cands) if (!map[ch.value]) { const p = ch.value.split('_')[0]; map[ch.value] = { kind: alias[p] || p, key: ch.value.replace(/_/g, ':'), inferred: true }; }
  bodies.push({ model: '<provider model>', input: c.text, questions: [{ type: 'choice', name: 'action', instructions: c.instructions, choices: c.cands }] });
  meta.push({ id: c.id, cat: c.cat, map });
}
fs.writeFileSync(path.join(out, 'c100.jsonl'), bodies.map(b => JSON.stringify(b)).join('\n') + '\n');
fs.writeFileSync(path.join(out, 'c100-meta.json'), JSON.stringify(meta));
console.log('cases', cases.length, 'with kinds', bodies.length, 'missing', miss);
