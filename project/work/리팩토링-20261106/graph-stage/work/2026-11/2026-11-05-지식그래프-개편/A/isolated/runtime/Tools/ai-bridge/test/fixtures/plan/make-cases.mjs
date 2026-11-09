// make-cases.mjs — writes cases.expect.json (and one <name>.json per case): the bridge replies the mod's planner reads (refactor plan 2-계획.md S1), with
// the values the mod must read from each (expect) and how the old hand reader differed (legacy).
// Envelopes copy bridge.mjs at 3c3adbe: early returns (:208-224, :325) and the 200 envelope (:280-288, key order kept).
// node make-cases.mjs   (rewrites cases.json next to this file; the expectations below are written by hand)
// Owned by the C# refactor worker (plan A.3); the bridge split (S8) only reads cases.json.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const env = ({ answer = null, error = null, detail = null, recall = '', rid = 7, rev = 3 }) => ({
  ok: !error, member: 'claude', cli: 'claude', model: 'claude-opus-5-5', effort: 'low', ms: 4200, report_id: rid, goal_rev: rev,
  answer: error ? null : answer, raw: '', error, detail,
  prompt_chars: 9000, turn_chars: 4000, answer_chars: 120, usage: null, usage_last: null, stderr_api_override: false,
  calls: 1, searches: 0, index_chars: 800, prefetch_used: false, recall_summary: recall,
});
const FOR = { report: 7, goal_rev: 3 };
const ok = (answer, recall = '') => JSON.stringify(env({ answer, recall }));
const A = (o) => ({ Decision: null, Say: null, Why: null, HasFor: false, ForReport: -1, ForGoalRev: -1, HasGoal: false, GoalDo: null, GoalItem: null, GoalCount: -1, ...o });
const top = (o) => ({ Parsed: true, ErrorRaw: null, DetailRaw: null, RetryAfterRaw: null, RecallSummaryRaw: null, Answer: null, ...o });
const okTop = (answer, recall = '') => top({ ErrorRaw: 'null', DetailRaw: 'null', RecallSummaryRaw: recall, Answer: answer });
const NONE = { Parsed: false, ErrorRaw: null, DetailRaw: null, RetryAfterRaw: null, RecallSummaryRaw: null, Answer: null };
const F = { HasFor: true, ForReport: 7, ForGoalRev: 3 };

const cases = [
  { name: 'ok-set-gather', status: 200, legacy: 'same',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'gather', item: 'CopperOre', count: 3 }, say: '구리 캐러 갈게요', why: '곡괭이 준비됨' }, 'about o:3050(2)'),
    expect: okTop(A({ Decision: 'set', Say: '구리 캐러 갈게요', Why: '곡괭이 준비됨', ...F, HasGoal: true, GoalDo: 'gather', GoalItem: 'CopperOre', GoalCount: 3 }), 'about o:3050(2)') },
  { name: 'ok-set-craft', status: 200, legacy: 'same',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'craft', item: 'WoodenWorkBench', count: 1 }, why: '작업대' }),
    expect: okTop(A({ Decision: 'set', Why: '작업대', ...F, HasGoal: true, GoalDo: 'craft', GoalItem: 'WoodenWorkBench', GoalCount: 1 })) },
  { name: 'ok-set-store-item', status: 200, legacy: 'same',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'store', item: 'Wood' }, say: '정리할게요' }),
    expect: okTop(A({ Decision: 'set', Say: '정리할게요', ...F, HasGoal: true, GoalDo: 'store', GoalItem: 'Wood' })) },
  { name: 'ok-set-store-null-item', status: 200, legacy: 'same',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'store', item: null } }),
    expect: okTop(A({ Decision: 'set', ...F, HasGoal: true, GoalDo: 'store', GoalItem: 'null' })) },
  { name: 'ok-set-explore-quoted-why', status: 200, legacy: 'escape',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'explore' }, why: 'error "answer" test' }, 'about o:3050(1)'),
    expect: okTop(A({ Decision: 'set', Why: 'error "answer" test', ...F, HasGoal: true, GoalDo: 'explore' }), 'about o:3050(1)'),
    note: 'the wiring test answer: why holds escaped quotes and the words error/answer; the old reader cut why at the escaped quote' },
  { name: 'ok-hold', status: 200, legacy: 'same',
    body: ok({ decision: 'hold', for: FOR, say: '잠깐 쉴게요', why: '체력 낮음' }),
    expect: okTop(A({ Decision: 'hold', Say: '잠깐 쉴게요', Why: '체력 낮음', ...F })) },
  { name: 'ok-ask', status: 200, legacy: 'same',
    body: ok({ decision: 'ask', for: FOR, say: '기지는 어디로 할까요?' }),
    expect: okTop(A({ Decision: 'ask', Say: '기지는 어디로 할까요?', ...F })) },
  { name: 'err-limit', status: 429, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'limit', detail: 'usage 99% > 85%', retry_after_s: 1800 }),
    expect: top({ ErrorRaw: 'limit', DetailRaw: 'usage 99% > 85%', RetryAfterRaw: '1800' }) },
  { name: 'err-limit-float-retry', status: 429, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'limit', detail: 'x', retry_after_s: 1800.5 }),
    expect: top({ ErrorRaw: 'limit', DetailRaw: 'x', RetryAfterRaw: '1800.5' }) },
  { name: 'err-auth', status: 503, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'auth', detail: 'codex: authMethod=apikey (refusing: not the subscription)' }),
    expect: top({ ErrorRaw: 'auth', DetailRaw: 'codex: authMethod=apikey (refusing: not the subscription)' }) },
  { name: 'err-login', status: 503, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'login', detail: 'not logged in (LoginGate)' }),
    expect: top({ ErrorRaw: 'login', DetailRaw: 'not logged in (LoginGate)' }) },
  { name: 'err-spawn', status: 503, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'spawn', detail: 'agy CLI not found' }),
    expect: top({ ErrorRaw: 'spawn', DetailRaw: 'agy CLI not found' }) },
  { name: 'err-unknown-member', status: 400, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'unknown member', detail: 'members: claude, gpt, gemini, grok' }),
    expect: top({ ErrorRaw: 'unknown member', DetailRaw: 'members: claude, gpt, gemini, grok' }) },
  { name: 'err-busy', status: 409, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'busy' }),
    expect: top({ ErrorRaw: 'busy' }) },
  { name: 'err-bridge', status: 500, legacy: 'same',
    body: JSON.stringify({ ok: false, error: 'bridge', detail: 'Unexpected end of JSON input' }),
    expect: top({ ErrorRaw: 'bridge', DetailRaw: 'Unexpected end of JSON input' }) },
  { name: 'env-parse', status: 200, legacy: 'same',
    body: JSON.stringify(env({ error: 'parse', detail: 'bad decision' })),
    expect: top({ ErrorRaw: 'parse', DetailRaw: 'bad decision', RecallSummaryRaw: '' }) },
  { name: 'env-timeout', status: 200, legacy: 'same',
    body: JSON.stringify(env({ error: 'timeout', detail: '판단 마감(120초)까지 남은 시간이 모자람' })),
    expect: top({ ErrorRaw: 'timeout', DetailRaw: '판단 마감(120초)까지 남은 시간이 모자람', RecallSummaryRaw: '' }) },
  { name: 'env-aborted', status: 200, legacy: 'same',
    body: JSON.stringify(env({ error: 'aborted', detail: '모드가 연결을 끊음' })),
    expect: top({ ErrorRaw: 'aborted', DetailRaw: '모드가 연결을 끊음', RecallSummaryRaw: '' }) },
  { name: 'env-error-detail-null', status: 200, legacy: 'same',
    body: JSON.stringify(env({ error: 'spawn', detail: null })),
    expect: top({ ErrorRaw: 'spawn', DetailRaw: 'null', RecallSummaryRaw: '' }) },
  { name: 'esc-say', status: 200, legacy: 'escape',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'explore' }, say: '그는 "이동"이라고 했다\n다음 줄 \\ 끝', why: 'w' }),
    expect: okTop(A({ Decision: 'set', Say: '그는 "이동"이라고 했다\n다음 줄 \\ 끝', Why: 'w', ...F, HasGoal: true, GoalDo: 'explore' })) },
  { name: 'nested-memo-first', status: 200, legacy: 'nested',
    body: ok({ memo: { why: 'memo why', decision: 'hold', goal: { do: 'explore' } }, decision: 'set', for: FOR, goal: { do: 'gather', item: 'Wood', count: 2 }, why: 'real why' }),
    expect: okTop(A({ Decision: 'set', Why: 'real why', ...F, HasGoal: true, GoalDo: 'gather', GoalItem: 'Wood', GoalCount: 2 })) },
  { name: 'nested-error-in-answer', status: 200, legacy: 'nested',
    body: ok({ decision: 'hold', for: FOR, error: 'my own field' }),
    expect: okTop(A({ Decision: 'hold', ...F })),
    note: "the old reader took the answer's own error key (it comes before the envelope's) and treated the reply as a failure" },
  { name: 'for-string-number', status: 200, legacy: 'same',
    body: ok({ decision: 'hold', for: { report: '7', goal_rev: '3' } }),
    expect: okTop(A({ Decision: 'hold', ...F })) },
  { name: 'for-missing', status: 200, legacy: 'same',
    body: ok({ decision: 'set', goal: { do: 'explore' } }),
    expect: okTop(A({ Decision: 'set', HasGoal: true, GoalDo: 'explore' })) },
  { name: 'for-null', status: 200, legacy: 'malformed',
    body: ok({ decision: 'set', for: null, goal: { do: 'explore' } }),
    expect: okTop(A({ Decision: 'set', HasGoal: true, GoalDo: 'explore' })),
    note: 'old: "for" found, the next object (goal) read as for -> report -1; new: no for. Both are rejected by Check (답의 for가 보낸 보고와 다름); only the reason text differs (-1 vs empty)' },
  { name: 'count-out-of-range', status: 200, legacy: 'same',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'gather', item: 'Wood', count: 1e10 } }),
    expect: okTop(A({ Decision: 'set', ...F, HasGoal: true, GoalDo: 'gather', GoalItem: 'Wood', GoalCount: -1 })) },
  { name: 'decision-number', status: 200, legacy: 'same',
    body: ok({ decision: 5, for: FOR }),
    expect: okTop(A({ Decision: '5', ...F })) },
  { name: 'count-float', status: 200, legacy: 'unreachable',
    body: ok({ decision: 'set', for: FOR, goal: { do: 'gather', item: 'Wood', count: 333 } }).replace('"count":333', '"count":3.0'),
    expect: okTop(A({ Decision: 'set', ...F, HasGoal: true, GoalDo: 'gather', GoalItem: 'Wood', GoalCount: 3 })),
    note: 'old -1, new 3. The bridge re-serialises the answer with JSON.stringify, so 3.0 never reaches the mod' },
  { name: 'dup-key', status: 200, legacy: 'unreachable',
    body: ok({ decision: 'DUP', for: FOR }).replace('"decision":"DUP"', '"decision":"hold","decision":"set"'),
    expect: okTop(A({ Decision: 'set', ...F })),
    note: 'old first value, new last value. The bridge parses and re-serialises the answer (one key each), so duplicates never reach the mod' },
  { name: 'cut-body', status: 200, legacy: 'unreachable',
    body: '{"ok":false,"error":"limit","detail":"usage 99',
    expect: NONE,
    note: 'old read error=limit out of the cut text; new reads nothing. UnityWebRequest gives the whole body and the bridge always sends whole JSON' },
  { name: 'empty-body', status: 0, legacy: 'same', body: '', expect: NONE },
  { name: 'html-body', status: 502, legacy: 'same', body: '<html><body>502 Bad Gateway</body></html>', expect: NONE },
];

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  // cases.expect.json: every case with its body, expectations and legacy class (core-tests reads this one).
  // <name>.json: one file per case for the bridge's protocol test (S8), which reads every *.json except *.expect.json:
  // {status, body} when the body is JSON, else the body text itself (not JSON: the protocol test skips it).
  const out = {
    note: 'Bridge replies to POST /v1/plan and what the mod reads from them (AIPlanProtocol.Read; refactor plan 2-계획.md S1). body: the exact reply text. expect: the PlanReply fields. legacy: how the old hand reader (AIPlanner.Field/ObjectAt, 3c3adbe) compared: same | escape | nested (the accepted bug fix) | malformed | unreachable (listed for HEAD). Answer fields are compared only where the old code read the answer (no error, not 409). Generated by make-cases.mjs; owned by the C# refactor worker (plan A.3).',
    cases,
  };
  for (const f of fs.readdirSync(HERE)) if (f.endsWith('.json')) fs.unlinkSync(path.join(HERE, f));
  fs.writeFileSync(path.join(HERE, 'cases.expect.json'), JSON.stringify(out, null, 1) + '\n');
  for (const c of cases) {
    let body = null; try { body = JSON.parse(c.body); } catch { body = null; }
    const file = path.join(HERE, `${c.name}.json`);
    if (body && typeof body === 'object') fs.writeFileSync(file, JSON.stringify({ status: c.status, body }, null, 1) + '\n');
    else fs.writeFileSync(file, c.body);
  }
  console.log(`cases.expect.json + ${cases.length} case files`);
}
