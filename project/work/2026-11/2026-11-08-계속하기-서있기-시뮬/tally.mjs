// node tally.mjs <dir>: per variant, the kinds Celeris chose (and for O, whether the current action was kept)
import fs from 'fs';
import path from 'path';
const dir = process.argv[2];
const meta = JSON.parse(fs.readFileSync(path.join(dir, 'meta.json'), 'utf8'));
const combat = new Set();   // H requests in a fight: an attack option exists
for (const v of ['H-v0', 'H-v1', 'H-v2', 'H-v3', 'O-v0', 'O-v1']) {
  const m = meta[v.replace('-', '')];
  const rows = fs.readFileSync(path.join(dir, 'out-' + v, 'results.jsonl'), 'utf8').split('\n').filter(Boolean).map(JSON.parse);
  const tally = {}, tallyFight = {}, tallyCalm = {};
  let keep = 0, n = 0;
  const detail = {};
  for (const r of rows) {
    const i = +r.id.split(':').pop() - 1; const me = m[i];
    const k = me.map[r.choice] || { kind: '?', key: '' };
    let label = k.kind;
    if (k.kind === 'attack') label = k.key === me.contKey ? 'attack(same target)' : 'attack(other target)';
    const fight = Object.values(me.map).some(x => x.kind === 'attack');
    tally[label] = (tally[label] || 0) + 1;
    (fight ? tallyFight : tallyCalm)[label] = ((fight ? tallyFight : tallyCalm)[label] || 0) + 1;
    if (v.startsWith('O')) {
      n++;
      const kept = k.kind === 'continue' || k.key === me.contKey;
      if (kept) keep++;
      (detail[me.contKey.split(':')[0]] ||= []).push(label);
    }
  }
  const s = o => Object.entries(o).sort((a, b) => b[1] - a[1]).map(([k, c]) => `${k} ${c}`).join(', ');
  console.log(`== ${v} (${rows.length} answers)`);
  if (v.startsWith('H')) { console.log('  fight: ' + s(tallyFight)); console.log('  calm : ' + s(tallyCalm)); }
  else { console.log('  ' + s(tally)); console.log(`  current action kept: ${keep}/${n}`); for (const [k, l] of Object.entries(detail)) { const c = {}; l.forEach(x => c[x] = (c[x] || 0) + 1); console.log(`   ${k}: ${s(c)}`); } }
}
