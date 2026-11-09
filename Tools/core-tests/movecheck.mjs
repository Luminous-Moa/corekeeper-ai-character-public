// movecheck.mjs — judges a "move" refactor step (plan 2-계획.md C.0, review F11): code moved from one place to another
// must keep its text. Takes the lines a commit range deletes and the lines it adds (all files), normalises whitespace,
// and pairs them up as a multiset. What is left is the real change: lines deleted and not added back (gone), and lines
// added that were not there before (new). For a pure move both lists hold only the declared edits (forwarders, method
// headers, a return value) — the step report lists and explains them.
//   node movecheck.mjs <from> [<to>] [-- <path>...]     (git range; without <to>: <from> against the working tree —
//   add new files with git add -N first so they show)
//   node movecheck.mjs selftest
import { execFileSync } from 'node:child_process';

const norm = (l) => l.replace(/\s+/g, ' ').trim();
const skip = (l) => l === '' || l === '{' || l === '}' || l === '};';

export function pairUp(deleted, added) {
  const pool = new Map();
  for (const l of deleted.map(norm).filter((x) => !skip(x))) pool.set(l, (pool.get(l) || 0) + 1);
  const fresh = [];
  for (const l of added.map(norm).filter((x) => !skip(x))) {
    const n = pool.get(l) || 0;
    if (n > 0) pool.set(l, n - 1); else fresh.push(l);
  }
  const gone = [];
  for (const [l, n] of pool) for (let i = 0; i < n; i++) gone.push(l);
  return { gone, fresh, moved: added.filter((x) => !skip(norm(x))).length - fresh.length };
}

function diffLines(from, to, paths) {
  const out = execFileSync('git', ['diff', '-U0', '--no-color', from, ...(to ? [to] : []), '--', ...paths], { encoding: 'utf8', maxBuffer: 1 << 28 });
  const del = [], add = [];
  for (const l of out.split('\n')) {
    if (l.startsWith('---') || l.startsWith('+++')) continue;
    if (l.startsWith('-')) del.push(l.slice(1).replace(/\r$/, ''));
    else if (l.startsWith('+')) add.push(l.slice(1).replace(/\r$/, ''));
  }
  return { del, add };
}

function selftest() {
  let fail = 0;
  const ok = (c, w) => { console.log(`  [${c ? 'OK' : 'FAIL'}] ${w}`); if (!c) fail++; };
  const r = pairUp(['    a = 1;', 'b();', '  c(x);'], ['a = 1;', '        b();', 'c(y);']);
  ok(r.gone.join() === 'c(x);' && r.fresh.join() === 'c(y);' && r.moved === 2, 'indentation ignored, a changed line shows on both sides');
  const r2 = pairUp(['x();', 'x();'], ['x();']);
  ok(r2.gone.length === 1 && r2.fresh.length === 0, 'a line deleted twice and added once: one left over');
  console.log(fail === 0 ? 'SELFTEST OK' : `SELFTEST FAILED ${fail}`);
  return fail;
}

const argv = process.argv.slice(2);
if (argv[0] === 'selftest') process.exit(selftest());
else if (argv.length) {
  const dd = argv.indexOf('--');
  const range = dd >= 0 ? argv.slice(0, dd) : argv;
  const paths = dd >= 0 ? argv.slice(dd + 1) : [];
  const { del, add } = diffLines(range[0], range[1], paths);
  const { gone, fresh, moved } = pairUp(del, add);
  console.log(`moved (same text) ${moved} lines; deleted and not added back ${gone.length}; added and new ${fresh.length}`);
  if (gone.length) { console.log('--- deleted, not added back:'); for (const l of gone) console.log('  - ' + l.slice(0, 200)); }
  if (fresh.length) { console.log('--- added, new:'); for (const l of fresh) console.log('  + ' + l.slice(0, 200)); }
}
