// Offline tests of recall-loop.mjs (design 7.1-5): a fake chat and a fake clock drive the loop through every case.
// Run: node test/recall-loop.test.mjs
import { recallLoop, composeTurn, recallSummary, childrenOf, PrefetchStore, CAP_WORDS } from '../recall-loop.mjs';

let fails = 0, checks = 0;
const ok = (c, what, extra = '') => { checks++; if (!c) { fails++; console.log(`  [FAIL] ${what}${extra ? `\n         ${extra}` : ''}`); } else console.log(`  [OK] ${what}`); };

// fake clock and chat: each scripted answer takes `secs` of fake time
function rig(script, { start = 0, recvAt = start, agyKill = false } = {}) {
  let t = start;
  const seen = [];
  const chat = async (turn, timeoutSec) => {
    const step = script[Math.min(seen.length, script.length - 1)];
    seen.push({ turn, timeoutSec });
    // the call honours its limit like run.mjs: past it the process is killed (agy: 10 s after its limit)
    const kill = (timeoutSec + (agyKill ? 10 : 0)) * 1000, took = Math.min((step.secs ?? 5) * 1000, kill);
    t += took;
    if ((step.secs ?? 5) * 1000 > kill) return { ok: false, text: '', ms: took, detail: 'exit=-2 (timeout)' };
    if (step.fail) return { ok: false, text: '', ms: took, detail: step.fail };
    return { ok: true, text: typeof step.a === 'string' ? step.a : JSON.stringify(step.a), ms: took, usage: { input: turn.length, output: 10 } };
  };
  const runs = [];
  const runQueries = (qs, o) => { runs.push(qs); return { text: `RESULT(${JSON.stringify(qs).slice(0, 60)})`, log: (Array.isArray(qs) ? qs : []).slice(0, o?.maxQ ?? 3).map((q) => ({ q: q?.q, target: q?.target ?? null, hits: 2, chars: 30 })) }; };
  return { chat, runQueries, now: () => t, seen, runs, recvAt, advance: (ms) => { t += ms; } };
}
const FOR = { report: 7, goal_rev: 3 };
const SET = { decision: 'set', for: FOR, goal: { do: 'explore' }, why: 'x' };
const REC = (n = 1) => ({ decision: 'recall', for: FOR, recall: Array.from({ length: n }, (_, i) => ({ q: 'about', target: `o:30${i}` })), why: 'look' });
const go = (r, o = {}) => recallLoop({ chat: r.chat, runQueries: r.runQueries, baseTurn: 'BASE', recvAt: r.recvAt, now: r.now, p90Sec: 6, ...o });

console.log('recall -> set');
{ const r = rig([{ a: REC() }, { a: SET }]); const o = await go(r);
  ok(o.answer?.decision === 'set' && o.calls.length === 2 && o.searches === 1, `2 calls, 1 search (${o.calls.length}, ${o.searches})`);
  ok(!('recall' in o.answer) && !('prefetch' in o.answer), 'answer to the mod has no recall/prefetch');
  ok(r.seen[1].turn.includes('검색 결과:') && r.seen[1].turn.includes('남은 검색: 1번'), 'second call carries the results and the searches left'); }

console.log('recall -> recall -> set');
{ const r = rig([{ a: REC() }, { a: REC() }, { a: SET }]); const o = await go(r);
  ok(o.answer?.decision === 'set' && o.calls.length === 3 && o.searches === 2, `3 calls, 2 searches (${o.calls.length}, ${o.searches})`);
  ok(r.seen[2].turn.includes('(1/2)') && r.seen[2].turn.includes('(2/2)'), 'both searches shown in the last call'); }

console.log('over the cap -> "answer now" -> set; again recall -> parse');
{ const r = rig([{ a: REC() }, { a: REC() }, { a: REC() }, { a: SET }]); const o = await go(r);
  ok(o.answer?.decision === 'set' && o.calls.length === 4 && o.searches === 2 && r.runs.length === 2, `4 calls, the 3rd recall ran no queries (${o.calls.length}, runs ${r.runs.length})`);
  ok(r.seen[3].turn.includes('검색은 더 할 수 없다') && r.seen[3].turn.includes(CAP_WORDS), 'the extra call says no more searches');
  const r2 = rig([{ a: REC() }, { a: REC() }, { a: REC() }, { a: REC() }]); const o2 = await go(r2);
  ok(o2.error === 'parse' && o2.capHit && o2.detail.includes(CAP_WORDS) && !/["]/.test(o2.detail), `then recall again -> parse, plain detail (${o2.detail})`);
  ok(o2.calls.length === 4 && r2.runs.length === 2, 'no new loop: 4 calls, 2 searches');
  const r3 = rig([{ a: REC() }, { a: SET }]); const o3 = await go(r3, { noSearch: true });
  ok(o3.answer?.decision === 'set' && r3.runs.length === 0 && o3.calls.length === 2, "the mod's retry after the cap: 0 searches (asked to answer now)"); }

console.log('30 s window, too little time');
{ const r = rig([{ a: REC(), secs: 31 }, { a: SET }]); const o = await go(r);
  ok(r.runs.length === 0 && o.answer?.decision === 'set', 'first answer after 31 s: no search, asked to answer now');
  ok(r.seen[1].turn.includes('오래되어'), 'the reason is said');
  const r2 = rig([{ a: REC(), secs: 5 }, { a: SET }], { start: 0, recvAt: -95000 }); const o2 = await go(r2, { searchWindowMs: 200000 });
  ok(r2.runs.length === 0 && r2.seen[1].turn.includes('남은 시간이 모자라'), 'little time left: no search');
  ok(o2.answer?.decision === 'set', 'and still an answer'); }

console.log('per-call limits, agy, a long check before the first call');
{ const r = rig([{ a: REC(), secs: 10 }, { a: SET }]); await go(r, { p90Sec: 10 });
  ok(r.seen[0].timeoutSec === 90, `first call: min(90, 120 - room) = 90 (${r.seen[0].timeoutSec})`);
  ok(r.seen[1].timeoutSec <= 120 - 10 - 3 && r.seen[1].timeoutSec >= 80, `second call gets the time left (${r.seen[1].timeoutSec})`);
  const a = rig([{ a: SET }]); await go(a, { agy: true, p90Sec: 14 });
  ok(a.seen[0].timeoutSec === 120 - (14 + 10 + 10) - 10, `agy: room for a last call (p90 14 + 10 + agy 10) and 10 s less for its kill (${a.seen[0].timeoutSec})`);
  const late = rig([{ a: SET }], { start: 0, recvAt: -50000 }); await go(late, { p90Sec: 10 });
  ok(late.seen[0].timeoutSec === 120 - 50 - 20, `45-50 s of checks first: the first call gets ${late.seen[0].timeoutSec} s (deadline from the request)`);
  const none = rig([{ a: SET }], { start: 0, recvAt: -115000 }); const o = await go(none);
  ok(o.error === 'timeout' && none.seen.length === 0 && o.deadlineHit, 'under 8 s left: no call, timeout'); }

console.log('slow CLI: the deadline holds');
{ // every combination: checks before the first call 0..110 s, answers at 10..150% of the limit, p90 1..40 s, agy on/off
  let worst = 0, under8 = 0, runs = 0;
  for (const pre of [0, 20, 45, 80, 110]) for (const frac of [0.1, 0.5, 0.99, 1.5]) for (const p90 of [1, 10, 40]) for (const agy of [false, true]) {
    const script = [0, 1, 2, 3].map(() => ({ a: REC(), secs: 0 }));
    const r = rig(script, { start: 0, recvAt: -pre * 1000, agyKill: agy });
    const chat = (turn, s) => { script[r.seen.length] = { a: r.seen.length < 3 ? REC() : SET, secs: s * frac }; return r.chat(turn, s); };
    await recallLoop({ chat, runQueries: r.runQueries, baseTurn: 'B', recvAt: r.recvAt, now: r.now, p90Sec: p90, agy });
    worst = Math.max(worst, r.now() - r.recvAt); under8 += r.seen.filter((c) => c.timeoutSec < 8).length; runs++;
  }
  ok(worst <= 120000, `${runs} combinations: the decision always ends within 120 s (latest ${(worst / 1000).toFixed(1)} s, review 14)`);
  ok(under8 === 0, 'no call under 8 s in any of them'); }

console.log('usage per call, the real reason in the parse detail, the first turn says the searches');
{ const r = rig([{ a: REC() }, { a: SET }]); const o = await go(r);
  ok(o.calls.every((c) => c.usage && c.usage.input > 0) && o.calls.length === 2, 'each call keeps its usage (review 3)');
  const { usageSum } = await import('../recall-loop.mjs');
  const u = usageSum(o.calls); ok(u.input === o.calls[0].usage.input + o.calls[1].usage.input && u.output === 20, `usageSum adds the calls (${JSON.stringify(u)})`);
  const w = rig([{ a: REC(), secs: 31 }, { a: REC() }]); const ow = await go(w);
  ok(ow.error === 'parse' && /오래되어/.test(ow.detail) && !ow.capHit, `after the 30 s window the detail says so, no cap (review 11): ${ow.detail}`);
  const n = rig([{ a: REC() }, { a: REC() }]); const on = await go(n, { noSearch: true, noSearchWhy: '기억을 읽을 수 없어' });
  ok(on.error === 'parse' && /기억을 읽을 수 없어/.test(on.detail) && !on.capHit && n.runs.length === 0, `no memory: its own reason, no cap (${on.detail})`);
  const { reportTurn } = await import('../recall-loop.mjs');
  ok(reportTurn({ report_id: 1, goal_rev: 2, report: {} }, { searchNote: '이번 판단의 검색: 최대 1번' }).includes('최대 1번'), 'the first turn can say the searches of this decision (review 12)'); }

console.log('bad queries, 4 queries, recall with goal, empty recall, no for');
{ const r = rig([{ a: { decision: 'recall', for: FOR, recall: [] } }, { a: SET }]); const o = await go(r);
  ok(r.runs.length === 1 && o.searches === 1, 'empty recall counts as a search (usage shown in the result)');
  const r4 = rig([{ a: REC(4) }, { a: SET }]); await go(r4);
  ok(r4.runs[0].length === 4, 'the 4 queries go to runQueries (it runs 3 and says so)');
  const rg = rig([{ a: { ...REC(), goal: { do: 'explore' } } }, { a: SET }]); const og = await go(rg);
  ok(rg.runs.length === 1 && og.answer?.decision === 'set', 'recall with a goal: the recall goes first');
  const nf = rig([{ a: { decision: 'set', goal: { do: 'explore' } } }]); const on = await go(nf);
  ok(on.answer && !on.answer.for, 'an answer without for passes through (the mod rejects it)');
  const bad = rig([{ a: 'no json here' }]); const ob = await go(bad);
  ok(ob.answer === null && !ob.error, 'no JSON: answer null (the bridge reports parse)'); }

console.log('prefetch');
{ const r = rig([{ a: { ...SET, prefetch: [{ q: 'near', place: 'here' }, { q: 'a' }, { q: 'b' }, { q: 'c' }] } }]); const o = await go(r);
  ok(o.prefetch?.length === 3 && !('prefetch' in o.answer), 'prefetch carried apart, 3 at most, not in the answer');
  let t = 0; const st = new PrefetchStore(30 * 60000, () => t);
  st.put('w/claude', o.prefetch);
  const p1 = st.take('w/claude'); const p2 = st.take('w/claude');
  ok(p1 && !p2, 'used once: the second request does not get it again (R2-1)');
  st.giveBack('w/claude', p1); ok(st.take('w/claude') === p1, 'given back when the request ended before any CLI call');
  st.put('w/claude', o.prefetch); t += 31 * 60000; ok(st.take('w/claude') === null, 'dropped after 30 min'); }

console.log('abort, two members');
{ let gone = false;
  const r = rig([{ a: REC(), secs: 5 }, { a: SET }]);
  const chat = async (turn, s) => { const x = await r.chat(turn, s); gone = true; return x; };
  const o = await recallLoop({ chat, runQueries: r.runQueries, baseTurn: 'B', recvAt: 0, now: r.now, aborted: () => gone });
  ok(o.error === 'aborted' && r.seen.length === 1 && r.runs.length === 0, 'closed after the first call: stops, no search, no second call');
  const A = rig([{ a: REC(), secs: 5 }, { a: SET }]), B = rig([{ a: REC(), secs: 5 }, { a: SET }]);
  let goneA = false;
  const [oa, ob] = await Promise.all([
    recallLoop({ chat: async (t, s) => { const x = await A.chat(t, s); goneA = true; return x; }, runQueries: A.runQueries, baseTurn: 'A', recvAt: 0, now: A.now, aborted: () => goneA }),
    recallLoop({ chat: B.chat, runQueries: B.runQueries, baseTurn: 'B', recvAt: 0, now: B.now, aborted: () => false }),
  ]);
  ok(oa.error === 'aborted' && ob.answer?.decision === 'set', 'two members: one closes, the other finishes');
  const running = new Set([{ pid: 1, spawnfile: 'C:/x/claude.exe' }, { pid: 2, spawnfile: 'C:/y/agy.cmd' }, { pid: 3, spawnfile: 'C:/x/claude.exe' }]);
  const k = childrenOf(running, 'C:/x/claude.exe').map((c) => c.pid);
  ok(k.join() === '1,3', `only that member's CLI processes are killed (${k})`);
  ok(childrenOf(running, undefined).length === 0, 'unknown member: nothing killed'); }

console.log('summary for the mod');
{ const s = recallSummary([{ log: [{ q: 'about', target: 'o:3050', hits: 3 }, { q: 'game', target: 'say "error": x', hits: 0, err: 'target' }] }], true);
  ok(!/["\\]/.test(s) && !/\berror\b|\banswer\b/i.test(s) && s.startsWith('미리 찾음'), `no quotes/keys (${s})`); }

console.log('compose');
{ const t = composeTurn('B', [{ request: [{ q: 'about' }], result: 'R' }], { searchesLeft: 1, budget: 2 });
  ok(t.startsWith('B') && t.includes('(1/2)') && t.includes('남은 검색: 1번'), 'the turn keeps the base and adds the search'); }

console.log(fails ? `${fails}/${checks} FAILED` : `ALL ${checks} CHECKS OK`);
process.exit(fails ? 1 : 0);
