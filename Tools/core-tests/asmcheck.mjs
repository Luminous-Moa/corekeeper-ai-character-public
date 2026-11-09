// asmcheck.mjs — checks that the assembly records (S6a: ASM-IN / ASM-OUT lines in decision logs) are complete enough
// for the S6b offline replay (plan 2-계획.md S6a/L1, review F9), and counts which situations the corpus covers.
//   node asmcheck.mjs <decisions-*.log>...
// Per build: an IN and an OUT (or "recursed"), every OUT id is a candidate of the IN lists or the added wait, every
// candidate the body filter kept has a BodyCan value, every close-fight goal-side candidate with progress has a Doable
// value. Exit code 1 when a build fails these.
import fs from 'node:fs';

const builds = new Map();
for (const f of process.argv.slice(2)) {
  for (const line of fs.readFileSync(f, 'utf8').split(/\r?\n/)) {   // the game writes CRLF
    const m = line.match(/ASM-(IN|OUT) #(\d+) (.*)$/);
    if (!m) continue;
    const b = builds.get(m[2]) || {};
    if (m[1] === 'OUT' && m[3].trim() === 'recursed') b.recursed = true;
    else { try { b[m[1]] = JSON.parse(m[3].replace(/\r$/, '')); } catch (e) { b.bad = (b.bad || '') + ` ${m[1]} not JSON (${e.message})`; } }
    builds.set(m[2], b);
  }
}
let ok = 0, bad = 0;
const cover = { goalNone: 0, closeFight: 0, closeFightKeep: 0, free: 0, follow: 0, fails: 0, left: 0, over14: 0, recursed: 0, prefetch: 0, noBody: 0 };
const problems = [];
for (const [n, b] of builds) {
  if (b.recursed) { cover.recursed++; continue; }
  const why = [];
  if (b.bad) why.push(b.bad);
  if (!b.IN) why.push('no IN'); if (!b.OUT) why.push('no OUT');
  if (b.IN && b.OUT) {
    const ids = new Set(); for (const l of Object.values(b.IN.lists)) for (const c of l) ids.add(c.id);
    const out = b.OUT.list;
    for (const c of out.slice(0, -1)) if (!ids.has(c.id)) why.push(`OUT id ${c.id} not in IN`);
    if (out.length && out[out.length - 1].kind !== 'wait') why.push('last OUT item is not wait');
    for (const c of out.slice(0, -1)) if (!(String(c.id) in b.OUT.bodyCan)) why.push(`kept ${c.id} without BodyCan`);
    if (b.IN.goal === 'None') cover.goalNone++;
    if (b.IN.closeFight && b.IN.goal !== 'None') { cover.closeFight++; if (/setLog/.test('') || (b.OUT.setLog || '').includes('close-fight') && !(b.OUT.setLog || '').includes('no goal step')) cover.closeFightKeep++; }
    if (b.IN.free) cover.free++;
    if (b.IN.goal === 'Follow') cover.follow++;
    if (b.IN.fails.length) cover.fails++;
    if (b.IN.left.length) cover.left++;
    if (out.length > 14) cover.over14++;
    if (b.IN.prefetch) cover.prefetch++;
    if (b.OUT.noBody > 0) cover.noBody++;
  }
  if (why.length) { bad++; if (problems.length < 20) problems.push(`#${n}: ${why.join('; ')}`); } else ok++;
}
console.log(`builds ${builds.size}: complete ${ok}, incomplete ${bad}, recursed ${cover.recursed}`);
console.log('covers: ' + Object.entries(cover).map(([k, v]) => `${k} ${v}`).join(', '));
for (const p of problems) console.log('  ' + p);
process.exit(bad ? 1 : 0);
