// logshape.mjs — compares the SHAPE of the mod's log lines between two live runs (refactor plan 2-계획.md, B.2).
// A refactor step must not change log line formats; the game is not deterministic, so lines are compared as shapes:
// option lists after " | " are dropped, quoted text -> '', bracket/paren contents and JSON blobs -> [] () {}, keys such
// as mine_3_-4 or gather:CopperOre -> K, numbers and ids with digits -> #, CamelCase ids (ObjectIDs) -> Id, Korean and
// other free text -> ~ (to the end of the line once it starts, unless a key=value follows).
// Shapes are aids for a person, not proof: a new shape is reported, and a person either finds its code site (then lists
// it in an allow file with the reason) or treats it as a difference.
//
//   node logshape.mjs shapes <log> [--all]                 shape counts of the watched prefixes (--all: every line)
//   node logshape.mjs diff <base log> <new log> [--allow <file>]
//        prints shapes (ERROR shapes included) of the new log that are neither in the base log nor in the allow file;
//        exit code 1 when there are any. Allow file: one shape per line, then a TAB and the reason; // comments.
//   node logshape.mjs selftest
//
// Input: Unity Player.log ("[AIPlayerSpike] t=12.3 @Claude MSG", CRLF; other lines such as stack traces are skipped)
// or a decisionlog file ("t=12.34 @Claude MSG").
import fs from 'node:fs';

export const WATCH = ['GOAL ', 'EXP: ', 'PLANNER:', 'DECISION #', 'SWITCH ', 'SKILL ', 'COMBAT #', 'SELF-DEFENCE', 'VITALS', 'BRAIN:', 'REFLEX', 'ERROR', 'ASM-', 'OUTCOME-MISMATCH', 'EMERGENCY-MISMATCH', 'REJECT-MISMATCH', 'TOOLSHORT-MISMATCH', 'TEST-ONLY', 'EarlyInit'];

// The message of one log line, or null when the line is not a mod line.
export function message(line) {
  line = line.replace(/\r$/, '');   // Player.log lines end with CRLF
  let m = line.match(/\[AIPlayerSpike\] t=-?[\d.]+ (?:@\S+ )?(.*)$/);
  if (!m) m = line.match(/^t=-?[\d.]+ (?:@\S+ )?(.*)$/);
  return m ? m[1] : null;
}

export function shape(msg) {
  let s = msg.split(/ \|\|? /)[0];                          // option lists after " | " / " || " (SEND, FULL): texts, not format
  s = s.replace(/'[^']*'/g, "''").replace(/\{.*\}/g, '{}'); // quoted texts, JSON blobs
  for (let i = 0; i < 4; i++) s = s.replace(/\[[^[\]]*\]/g, '[]');   // lists (they hold value=probability pairs)
  // head: the fixed words before the first free text, parenthesis, colon or key=value
  const head = s.split(/[:(]|[^\u0000-\u007F]|\s\S+=/)[0]
    .replace(/\b[a-z]+(?:[_:,][\w#.,-]*)+/g, 'K').replace(/\b\w*\d\w*\b/g, '#')
    .replace(/\b[A-Z][a-z0-9]+(?:[A-Z][A-Za-z0-9]*)+\b/g, 'Id').replace(/\s+/g, ' ').trim();
  const keys = [...s.matchAll(/(?:^|[\s(,;])([A-Za-z_][\w-]*)=/g)].map((m) => m[1]);   // key names in order
  const flags = [];                                          // status words in order (APPLY, HOLD, QUEUED, DISCARD, DROPPED …)
  for (const m of s.matchAll(/\b[A-Z][A-Z-]{2,}\b/g)) if (flags[flags.length - 1] !== m[0]) flags.push(m[0]);
  return head + ' | ' + keys.join(' ') + ' | ' + flags.join(' ');
}

export function shapes(text, all = false) {
  const out = new Map();
  for (const line of text.split('\n')) {
    const msg = message(line);
    if (msg == null) continue;
    if (!all && !WATCH.some((w) => msg.startsWith(w))) continue;
    const sh = shape(msg);
    out.set(sh, (out.get(sh) || 0) + 1);
  }
  return out;
}

export function readAllow(file) {
  const set = new Set();
  if (!file) return set;
  for (const l of fs.readFileSync(file, 'utf8').split('\n')) {
    const t = l.replace(/\r$/, '');
    if (!t.trim() || t.trim().startsWith('//')) continue;
    set.add(t.split('\t')[0]);
  }
  return set;
}

export function diff(baseText, newText, allow = new Set()) {
  const a = shapes(baseText), b = shapes(newText);
  const fresh = [];
  for (const [sh, n] of b) if (!a.has(sh) && !allow.has(sh)) fresh.push({ shape: sh, n });
  return fresh;
}

function selftest() {
  let fail = 0;
  const ok = (c, what) => { console.log(`  [${c ? 'OK' : 'FAIL'}] ${what}`); if (!c) fail++; };
  const base = [
    '[AIPlayerSpike] t=12.3 @Claude GOAL set rev=3 source=host: 흙벽 블록(WallDirtBlock) 5개 모으기 (start count 0)\r',
    'UnityEngine.Debug:Log (object)',
    '[AIPlayerSpike] t=15.0 @Claude DECISION #4 RESULT choice=mine_3_-4 (캐기 — 벽) top=[mine_3_-4=0.91] confidence=0.91 latency=0.25s tokens=1200 cached=0 cost=$0.000120 APPLY',
    '[AIPlayerSpike] t=16.0 @Gemini PLANNER: report #2 sent trigger=goal_done rev=4 reportChars=1800 buildMs=3.2: {"trigger":"goal_done","x":{"y":1}}',
    '[AIPlayerSpike] t=17.0 @Claude ERROR brain tick failed',
    '[AIPlayerSpike] t=18.0 @Claude EXP: goal run 1a2b.17 ended: gather:CopperOre -> blocked/tool_weak got 0/3, 120s, downs 1',
  ].join('\n');
  const same = [
    '[AIPlayerSpike] t=99.9 @Grok GOAL set rev=12 source=host: 구리 광석(CopperOre) 3개 모으기 (start count 4)',
    't=100.00 @Claude DECISION #77 RESULT choice=attack_123 (공격 — 적 MushroomEnemy (3,4) 2.1칸) top=[attack_123=0.5, wait=0.2] confidence=0.50 latency=0.31s tokens=900 cached=512 cost=$0.000090 APPLY',
    '[AIPlayerSpike] t=16.0 @Claude PLANNER: report #12 sent trigger=back_up rev=9 reportChars=2300 buildMs=1.0: {"trigger":"back_up"}',
    '[AIPlayerSpike] t=18.0 @Claude EXP: goal run b9c.3 ended: craft:WoodenWorkBench -> blocked/cannot_craft got 0/1, 30s, downs 0',
  ].join('\n');
  ok(diff(base, same).length === 0, 'same formats with other values, names, Korean text and a decisionlog line: no new shape');
  const changed = '[AIPlayerSpike] t=1.0 @Claude GOAL set revision=3 from=host: 흙 5';
  ok(diff(base, changed).length === 1, 'a changed key name is a new shape');
  const err = '[AIPlayerSpike] t=1.0 @Claude ERROR planner: tick failed';
  const d = diff(base, err);
  ok(d.length === 1 && d[0].shape.startsWith('ERROR'), 'a new ERROR line is reported');
  ok(diff(base, err, new Set([d[0].shape])).length === 0, 'an allowed shape is not reported');
  ok(message('UnityEngine.Debug:Log (object)') === null, 'stack trace lines are skipped');
  ok(message('[AIPlayerSpike] t=3.0 EarlyInit: x\r') === 'EarlyInit: x', 'CRLF line without a character name');
  console.log(fail === 0 ? 'SELFTEST OK' : `SELFTEST FAILED ${fail}`);
  return fail;
}

const [cmd, ...rest] = process.argv.slice(2);
const opt = (k) => { const i = rest.indexOf(`--${k}`); return i >= 0 ? rest[i + 1] : undefined; };
if (cmd === 'selftest') process.exit(selftest());
else if (cmd === 'shapes') {
  const m = shapes(fs.readFileSync(rest[0], 'utf8'), rest.includes('--all'));
  for (const [sh, n] of [...m].sort((x, y) => y[1] - x[1])) console.log(`${String(n).padStart(6)}  ${sh}`);
} else if (cmd === 'diff') {
  const fresh = diff(fs.readFileSync(rest[0], 'utf8'), fs.readFileSync(rest[1], 'utf8'), readAllow(opt('allow')));
  for (const f of fresh) console.log(`NEW ${String(f.n).padStart(5)}  ${f.shape}`);
  console.log(fresh.length === 0 ? 'no new shapes' : `${fresh.length} new shape(s): find each one's code site, then allow it with a reason or treat it as a difference`);
  process.exit(fresh.length ? 1 : 0);
} else if (cmd) {
  console.log('usage: node logshape.mjs shapes <log> [--all] | diff <base> <new> [--allow <file>] | selftest');
  process.exit(2);
}
