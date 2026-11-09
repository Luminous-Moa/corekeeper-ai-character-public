// recall-loop.mjs — the loop inside one plan request: ask the member's CLI; when it answers {"decision":"recall"},
// run its queries on the memory graph and ask again with the results; at most `budget` searches per decision.
// A pure function of its inputs (chat, runQueries, clock), so the tests drive it with a fake chat.
// Design: work/2026-11/2026-11-04-기억-그래프-검색/1-설계.md 5.2·5.3 (revision 2) and the re-check (참고 2).
//
// Rules:
// - The deadline counts from when the bridge got the request (recvAt): checks before the first call are inside it.
// - A search is taken only while (now - recvAt) <= searchWindowMs and the time left is enough for one more call after
//   it (p90 * 2 + reserveSec, + agyExtra for agy). Otherwise, and past the budget, the CLI is asked once more "answer
//   now, no more searches" (no queries run). A recall after that is a parse error with a plain-words detail.
// - Every call's time limit: min(maxCallSec, time left - room), where room keeps the last call's share while searches
//   remain (참고 2). agy kills its process 10 s after its limit, so 10 s less for agy. Under minCallSec: no call.
// - recall / prefetch never go to the mod: the final answer is returned without them; prefetch separately.

export const CAP_WORDS = '검색은 한 판단에';

export function firstJson(text) {
  const t = String(text || '').replace(/```(?:json)?/g, '');
  const i = t.indexOf('{');
  if (i < 0) return null;
  let depth = 0, inStr = false, esc = false;
  for (let k = i; k < t.length; k++) {
    const c = t[k];
    if (inStr) { if (esc) esc = false; else if (c === '\\') esc = true; else if (c === '"') inStr = false; continue; }
    if (c === '"') inStr = true;
    else if (c === '{') depth++;
    else if (c === '}' && --depth === 0) { try { return JSON.parse(t.slice(i, k + 1)); } catch { return null; } }
  }
  return null;
}

// The first turn: the report (as before), the memory index and what was looked up ahead (when there is any).
export function reportTurn(b, { index = '', prefetched = '', searchNote = '' } = {}) {
  return [
    `보고 번호 ${b.report_id}, 목표 수정 번호 ${b.goal_rev}.`,
    '이번 구간 보고(JSON):',
    JSON.stringify(b.report),
    ...(index ? ['', index] : []),
    ...(prefetched ? ['', prefetched] : []),
    ...(searchNote ? ['', searchNote] : []),
    '',
    `답은 JSON 하나만. "for"에는 {"report": ${b.report_id}, "goal_rev": ${b.goal_rev}}를 그대로 넣는다.`,
  ].join('\n');
}

// The text of one call: the first turn, then every search so far, then what is allowed now.
export function composeTurn(baseTurn, rounds, { searchesLeft, budget, forceFinal, forceWhy }) {
  const parts = [baseTurn];
  rounds.forEach((r, i) => {
    parts.push(`\n너의 검색 요청 (${i + 1}/${budget}): ${JSON.stringify(r.request)}`);
    parts.push(`검색 결과:\n${r.result}`);
  });
  if (forceFinal) parts.push(`\n${forceWhy || '검색은 더 할 수 없다.'} 지금까지 받은 것으로 set/keep/hold/ask 중 하나를 답한다. recall은 쓰지 않는다.`);
  else if (rounds.length) parts.push(`\n남은 검색: ${searchesLeft}번. 같은 것을 다시 찾지 않는다. 충분하면 set/keep/hold/ask로 답한다.`);
  return parts.join('\n');
}

export async function recallLoop({
  chat,                      // async (turnText, timeoutSec) -> {ok, text, ms, detail, usage}
  runQueries,                // (queries) -> {text, log}
  baseTurn,                  // the first turn (report, memory_index, prefetched)
  recvAt,                    // ms: when the bridge got the request
  now = () => Date.now(),
  deadlineMs = 120000,
  budget = 2,                // searches per decision for this member
  maxQ = 3,
  searchWindowMs = 30000,
  p90Sec = 10,
  agy = false,
  agyExtra = 10,
  reserveSec = 10,
  maxCallSec = 90,
  minCallSec = 8,
  marginSec = 3,
  aborted = () => false,
  noSearch = false,          // no searches in this decision (the mod's retry after a cap parse, no memory, recall off)
  noSearchWhy = '',
}) {
  const deadline = recvAt + deadlineMs;
  const rounds = [];         // {request, result, log, ms}
  const calls = [];          // {ms, ok, timeoutSec, chars}
  let searchesLeft = noSearch ? 0 : budget;
  let forceFinal = false, forceWhy = '', capHit = false, deadlineHit = false;
  const out = (o) => ({ rounds, calls, searches: rounds.length, capHit, deadlineHit, ...o });

  for (;;) {
    if (aborted()) return out({ error: 'aborted', detail: '모드가 연결을 끊음' });
    const left = (deadline - now()) / 1000;
    const canStillSearch = searchesLeft > 0 && !forceFinal;
    const room = canStillSearch ? p90Sec + reserveSec + (agy ? agyExtra : 0) : marginSec;
    let s = Math.min(maxCallSec, left - room);
    if (canStillSearch && s < minCallSec) s = Math.min(maxCallSec, left - marginSec);   // no room for a search anyway: this call may be the last
    if (agy) s -= agyExtra;
    s = Math.floor(s);
    if (s < minCallSec) { deadlineHit = true; return out({ error: 'timeout', detail: `판단 마감(${Math.round(deadlineMs / 1000)}초)까지 남은 시간이 모자람` }); }
    const turn = composeTurn(baseTurn, rounds, { searchesLeft, budget, forceFinal, forceWhy });
    const r = await chat(turn, s);
    calls.push({ ms: r.ms, ok: r.ok, timeoutSec: s, chars: turn.length, usage: r.usage || null });
    if (aborted()) return out({ error: 'aborted', detail: '모드가 연결을 끊음', last: r });
    if (!r.ok) return out({ error: 'cli', last: r });
    const a = firstJson(r.text);
    if (!a || a.decision !== 'recall') {
      if (!a) return out({ answer: null, last: r });
      const prefetch = Array.isArray(a.prefetch) ? a.prefetch.slice(0, maxQ) : null;
      const answer = { ...a }; delete answer.recall; delete answer.prefetch;
      return out({ answer, prefetch, last: r });
    }
    // a recall
    if (forceFinal) {
      const why = forceWhy.replace(/\.$/, '').replace(/ 검색은 더 할 수 없다$/, '');
      return out({ answer: null, error: 'parse', detail: `검색할 수 없는데 recall을 보냄(${why}). 지금까지 받은 것으로 set/keep/hold/ask 중 하나로 답한다`, last: r });
    }
    const leftNow = (deadline - now()) / 1000;
    const why = noSearch ? `${noSearchWhy || '이번 판단에는 검색을 할 수 없다'}. 검색은 더 할 수 없다.`
      : searchesLeft <= 0 ? `${CAP_WORDS} ${budget}번까지다. 검색은 더 할 수 없다.`
      : now() - recvAt > searchWindowMs ? '판단을 시작한 지 오래되어 검색은 더 할 수 없다.'
        : leftNow < p90Sec * 2 + reserveSec + (agy ? agyExtra : 0) ? '남은 시간이 모자라 검색은 더 할 수 없다.' : '';
    if (why) { forceFinal = true; forceWhy = why; if (!noSearch && searchesLeft <= 0) capHit = true; continue; }
    const t0 = now();
    const res = runQueries(a.recall, { maxQ });
    rounds.push({ request: a.recall, result: res.text, log: res.log, ms: now() - t0, why: typeof a.why === 'string' ? a.why.slice(0, 160) : '' });
    searchesLeft--;
  }
}

// A short summary of what was looked up, for the mod's plan_result event: no quotes, no "error"/"answer" words
// (the mod reads the bridge reply by hand: the first "error": / "answer": key wins).
export function recallSummary(rounds, prefetchUsed) {
  const items = rounds.flatMap((r) => (r.log || []).map((l) => `${l.q || '?'} ${l.target == null ? '' : typeof l.target === 'string' ? l.target : JSON.stringify(l.target)}${l.err ? '(틀림)' : `(${l.hits})`}`.trim()));
  const s = `${prefetchUsed ? '미리 찾음 씀; ' : ''}${items.join('; ')}`.replace(/["\\]/g, '').replace(/\berror\b|\banswer\b/gi, '').slice(0, 200);
  return s;
}

// The CLI processes of one member (members and CLIs are 1:1): killed when the mod closes that member's connection;
// the other members' calls go on (design 5.3, re-check 3).
export const childrenOf = (running, bin) => [...running].filter((c) => bin && c.spawnfile === bin);

// What an answer asked to look up for the next decision, per (world, member): used once and dropped (re-check R2-1),
// kept when a request ended before any CLI call, dropped after ttl.
export class PrefetchStore {
  constructor(ttlMs = 30 * 60000, now = () => Date.now()) { this.m = new Map(); this.ttl = ttlMs; this.now = now; }
  put(key, queries) { if (Array.isArray(queries) && queries.length) this.m.set(key, { queries: queries.slice(0, 3), atMs: this.now() }); }
  take(key) {
    const p = this.m.get(key);
    if (!p) return null;
    this.m.delete(key);
    return this.now() - p.atMs <= this.ttl ? p : null;
  }
  giveBack(key, p) { if (p && !this.m.has(key)) this.m.set(key, p); }
}

// Sum of the numeric usage fields over the calls of one decision (each call sends the whole prompt again).
export function usageSum(calls) {
  const t = {};
  for (const c of calls || []) for (const [k, v] of Object.entries(c.usage || {})) if (typeof v === 'number') t[k] = (t[k] || 0) + v;
  return Object.keys(t).length ? t : null;
}
