// Tree-only replay (decisions-224158 #641/#654/#661 gather list open, #700/#710 life list open).
//   node build.mjs <dry requests.jsonl of the log> <decisions log> <outdir>
// V0 as logged; V1 AIFieldChoices.TreeOnly applied to the logged options: an open tree = an expedition_nav other than
// "open" (gather/move list) or a life_nav other than "open" (life list); tree entries = its nav entries (and leaves,
// none here: the lists were at the branch level); combat = the close-fight kinds (step/combo/hit/attacknow) in the set;
// eating kept when hunger < 25.
import fs from 'fs';
import path from 'path';
const [reqFile, logFile, out] = process.argv.slice(2);
const IDS = [641, 654, 661, 700, 710];
const reqs = fs.readFileSync(reqFile, 'utf8').split('\n').filter(Boolean).map(JSON.parse).filter(r => IDS.includes(+r.id.split('#').pop()));
const kinds = new Map();
for (const line of fs.readFileSync(logFile, 'utf8').split(/\r?\n/)) {
  const m = line.match(/@(\w+) DECISION #(\d+) INPUT .*options\(\d+\): (.*)$/);
  if (!m || !IDS.includes(+m[2])) continue;
  const map = {};
  for (const x of m[3].matchAll(/(?:^|\|\| |\n| )(c\d+) \[([a-z_]+) ([^\]]*)\]/g)) map[x[1]] = { kind: x[2], key: x[3] };
  kinds.set(+m[2], map);
}
const COMBAT = ['attack', 'attacknow', 'retreat', 'away', 'step', 'hit', 'combo', 'approach', 'hold', 'doorclose'];
const op = k => (k.split(':')[2] || '');
const v0 = [], v1 = [], meta = [];
for (const r of reqs) {
  const n = +r.id.split('#').pop(); const km = kinds.get(n); const ch = r.body.questions[0].choices;
  const exp = Object.values(km).some(x => x.kind === 'expedition_nav' && op(x.key) !== 'open');
  const life = Object.values(km).some(x => x.kind === 'life_nav' && op(x.key) !== 'open');
  const combat = Object.values(km).some(x => ['step', 'combo', 'hit', 'attacknow'].includes(x.kind));
  const hunger = +((r.body.input.match(/배고픔 (\d+)/) || [])[1] || 100);
  const keep = ch.filter(c => {
    const k = km[c.value] || { kind: '?', key: '' };
    const mine = exp && k.kind === 'expedition_nav' || life && k.kind === 'life_nav';
    const cmb = combat && (COMBAT.includes(k.kind) || k.kind === 'continue' && COMBAT.includes(k.key.split(':')[0]));
    return mine || cmb || hunger < 25 && k.kind === 'eat';
  });
  const map = {};
  const b1 = JSON.parse(JSON.stringify(r.body));
  b1.questions[0].choices = keep.map((c, i) => { map['c' + (i + 1)] = km[c.value]; return { value: 'c' + (i + 1), description: c.description }; });
  v0.push(r.body); v1.push(b1); meta.push({ n, km, map, exp, life, combat, hunger, before: ch.length, after: keep.length });
  console.log(`#${n}: ${exp ? 'gather list' : ''}${life ? 'life list' : ''} open, combat ${combat}, hunger ${hunger}: ${ch.length} -> ${keep.length} options`);
}
fs.mkdirSync(out, { recursive: true });
fs.writeFileSync(path.join(out, 'V0.jsonl'), v0.map(b => JSON.stringify(b)).join('\n') + '\n');
fs.writeFileSync(path.join(out, 'V1.jsonl'), v1.map(b => JSON.stringify(b)).join('\n') + '\n');
fs.writeFileSync(path.join(out, 'meta.json'), JSON.stringify(meta));
