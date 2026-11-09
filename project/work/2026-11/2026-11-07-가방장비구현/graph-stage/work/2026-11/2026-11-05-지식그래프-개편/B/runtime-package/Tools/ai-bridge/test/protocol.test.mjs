// The bridge's reply contract (protocol.mjs) and the decision's early returns (plan.mjs, preflight.mjs) without a CLI,
// a game or a server: shape() and cliErr() tables, the 200 envelope's keys in order, every error envelope's status, keys
// and values, plan() with a mocked preflight and a fake chat (statuses, the limit log line, the plan.jsonl line's keys in
// order), preflight with mocked CLI adapters. The expected lists are copied from bridge.mjs at 3c3adbe/rf-S0 (:206-300,
// :324-326): a change here is a change of the contract with the mod (AIPlanner.cs).
// When test/fixtures/plan/ exists (refactor S1, owned by the C# worker), each fixture envelope is checked against the
// same key lists (PLAN_FIXTURES=<dir> points elsewhere, e.g. the ai-refactor worktree); otherwise that part says skipped.
// Run: node test/protocol.test.mjs
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { shape, cliErr, envelope, errUnknownMember, errBusy, errSpawn, errLogin, errAuth, errLimit, errNotFound, errBridge } from '../protocol.mjs';
import { createPlan } from '../plan.mjs';
import { createPreflight } from '../preflight.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
let fails = 0, checks = 0;
const ok = (c, what, extra = '') => { checks++; if (!c) { fails++; console.log(`  [FAIL] ${what}${extra ? `\n         ${extra}` : ''}`); } else console.log(`  [OK] ${what}`); };
const same = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const keys = (o) => Object.keys(o);

// ---- the contract, as bridge.mjs had it (rf-S0)
const ENVELOPE_KEYS = ['ok', 'member', 'cli', 'model', 'effort', 'ms', 'report_id', 'goal_rev', 'answer', 'raw', 'error', 'detail',
  'prompt_chars', 'turn_chars', 'answer_chars', 'usage', 'usage_last', 'stderr_api_override', 'calls', 'searches', 'index_chars', 'prefetch_used', 'recall_summary'];
const PLAN_LOG_KEYS = ['member', 'cli', 'model', 'ms', 'ok', 'error', 'world', 'report_id', 'goal_rev', 'trigger', 'turn_chars', 'prompt_chars', 'answer_chars', 'usage', 'quota_pct',
  'rounds', 'recall', 'calls', 'prefetch_used', 'index_chars', 'total_ms', 'deadline_hit', 'cap_hit', 'no_search', 'mem_note', 'answer', 'prefetch'];   // mem_note/prefetch only when set
const LIMIT_LOG_KEYS = ['member', 'ok', 'error', 'detail', 'report_id', 'trigger'];
const ERRORS = {   // name -> [status, body] for given inputs
  unknownMember: [errUnknownMember({ claude: {}, gpt: {}, gemini: {}, grok: {} }), 400, { ok: false, error: 'unknown member', detail: 'members: claude, gpt, gemini, grok' }],
  busy: [errBusy(), 409, { ok: false, error: 'busy' }],
  spawn: [errSpawn('agy'), 503, { ok: false, error: 'spawn', detail: 'agy CLI not found' }],
  login: [errLogin({ authNote: 'not logged in (LoginGate)' }), 503, { ok: false, error: 'login', detail: 'not logged in (LoginGate)' }],
  auth: [errAuth('claude', { authMethod: 'apiKey' }), 503, { ok: false, error: 'auth', detail: 'claude: authMethod=apiKey (refusing: not the subscription)' }],
  limit: [errLimit({ skipWhy: 'usage 91% > 85%' }), 429, { ok: false, error: 'limit', detail: 'usage 91% > 85%', retry_after_s: 1800 }],
  notFound: [errNotFound(), 404, { ok: false, error: 'not found' }],
  bridge: [errBridge(new SyntaxError('Unexpected token x' + 'y'.repeat(400))), 500, { ok: false, error: 'bridge', detail: ('Unexpected token x' + 'y'.repeat(400)).slice(0, 300) }],
};

console.log('shape()');
const SHAPE = [
  [null, 'no-json'], ['text', 'no-json'], [{}, 'bad decision'], [{ decision: 'recall' }, 'bad decision'],
  [{ decision: 'hold' }, null], [{ decision: 'ask', say: '어디로 갈까요?' }, null], [{ decision: 'hold', say: 3 }, 'bad say'], [{ decision: 'hold', say: null }, null],
  // 자연어 명령: keep, and the optional order about the host instruction
  [{ decision: 'keep' }, null], [{ decision: 'keep', say: '계속 캘게', order: 'follow' }, null], [{ decision: 'hold', order: 'defer' }, null], [{ decision: 'set', goal: { do: 'explore' }, order: 'drop' }, null], [{ decision: 'keep', order: 'done' }, null], [{ decision: 'keep', order: 'later' }, null], [{ decision: 'keep', problem: '버섯 야수에게 세 번 짐' }, null], [{ decision: 'keep', problem: 3 }, 'bad problem'],
  // 검수 작은 지적 2: an order outside the four is dropped (no re-ask); 합의 3e: a plan of goal steps
  [{ decision: 'set', plan: [{ do: 'gather', item: 'Wood', count: 15 }, { do: 'craft', item: 'WoodHelm', count: 1 }, { do: 'home' }] }, null],
  [{ decision: 'set', plan: [] }, 'plan must be a list of 1..10 goals'], [{ decision: 'set', plan: 'gather' }, 'plan must be a list of 1..10 goals'],
  [{ decision: 'set', plan: Array(11).fill({ do: 'explore' }) }, 'plan must be a list of 1..10 goals'],
  [{ decision: 'set', plan: [{ do: 'explore' }, { do: 'gather', item: 'Wood' }] }, 'plan[1]: gather needs item and integer count'],
  [{ decision: 'set', plan: [{ do: 'build' }] }, 'plan[0]: bad goal.do'],
  [{ decision: 'set' }, 'bad goal.do'], [{ decision: 'set', goal: { do: 'build' } }, 'bad goal.do'],
  ...['explore', 'follow', 'home', 'farm', 'cook'].map((d) => [{ decision: 'set', goal: { do: d } }, null]),
  [{ decision: 'set', goal: { do: 'store' } }, null], [{ decision: 'set', goal: { do: 'store', item: 'Wood' } }, null], [{ decision: 'set', goal: { do: 'store', item: 5 } }, 'store item must be a string'],
  [{ decision: 'set', goal: { do: 'gather', item: 'Wood', count: 5 } }, null], [{ decision: 'set', goal: { do: 'gather', item: 'Wood' } }, 'gather needs item and integer count'],
  [{ decision: 'set', goal: { do: 'gather', item: 'Wood', count: 2.5 } }, 'gather needs item and integer count'], [{ decision: 'set', goal: { do: 'gather', item: 'Wood', count: '5' } }, 'gather needs item and integer count'],
  [{ decision: 'set', goal: { do: 'craft', count: 1 } }, 'craft needs item and integer count'], [{ decision: 'set', goal: { do: 'craft', item: 'CopperBar', count: 1 } }, null],
  [{ decision: 'set', goal: { do: 'gather', item: 'Wood', count: 3.0 } }, null],
  // 제작대-놓기: goto needs a where; place an item, where / count optional
  [{ decision: 'set', goal: { do: 'goto', where: 'ore:CopperOre@-40,12' } }, null], [{ decision: 'set', goal: { do: 'goto' } }, 'goto needs where (a place)'],
  [{ decision: 'set', goal: { do: 'place', item: 'CopperAnvil' } }, null], [{ decision: 'set', goal: { do: 'place', item: 'CopperAnvil', where: 'base' } }, null],
  [{ decision: 'set', goal: { do: 'place', item: 'WoodBridge', where: 'line:3,4,7,4', count: 5 } }, null], [{ decision: 'set', goal: { do: 'place', where: 'base' } }, 'place needs item (where a string, count an integer when given)'],
  [{ decision: 'set', goal: { do: 'place', item: 'Torch', where: 3 } }, 'place needs item (where a string, count an integer when given)'],
  [{ decision: 'set', plan: [{ do: 'place', item: 'CopperAnvil', where: 'base' }, { do: 'craft', item: 'CopperSword', count: 1 }] }, null],
];
const shapeBad = SHAPE.filter(([a, want]) => shape(a) !== want);
ok(!shapeBad.length, `shape() table (${SHAPE.length} cases)`, shapeBad.map(([a, w]) => `${JSON.stringify(a)}: ${shape(a)} != ${w}`).join('; '));
{ const a = { decision: 'keep', order: 'later' }; shape(a); ok(!('order' in a), 'shape() drops an order outside follow/defer/drop/done (검수 작은 지적 2)'); }
{ const a = { decision: 'keep', say: '체력 80이야', order: 'answered' }; ok(shape(a) === null && a.order === 'answered', 'shape() keeps order answered (실게임3 M2)'); }
{ const a = { decision: 'keep', say: null, why: null, problem: null, order: null }; ok(shape(a) === null && !('say' in a) && !('problem' in a) && !('order' in a) && !('why' in a), 'shape() drops JSON null text fields (재검수 작은 지적 5)'); }
{ const a = { decision: 'set', goal: { do: 'explore' }, order: 'answered', orders: { 1: 'follow', 2: 'later', x: 'drop', '0': 'done', 3: 'defer' } }; ok(shape(a) === null && JSON.stringify(a.orders) === '{"1":"follow","3":"defer"}', 'shape() keeps orders entries with an id and a known value (재검수 R3)', JSON.stringify(a.orders)); }
{ const a = { decision: 'keep', orders: ['follow'] }; const b = { decision: 'keep', orders: { x: 'drop' } }; ok(shape(a) === null && !('orders' in a) && shape(b) === null && !('orders' in b), 'shape() drops orders that is not an object or has nothing left'); }

// The same goals are parsed by Core/AIPlanProtocol in GoalContractTests.
{
  const contract = JSON.parse(fs.readFileSync(path.join(HERE, 'fixtures/goal-contract.json'), 'utf8'));
  for (const c of contract.cases) {
    const got = shape({ decision: 'set', goal: structuredClone(c.goal) });
    ok(got === c.bridgeError, 'shared goal contract: ' + c.name, String(got));
  }
  const protocolSource = fs.readFileSync(path.join(HERE, '../protocol.mjs'), 'utf8');
  const allowed = protocolSource.match(/const GOAL_DO = (\[[^;]+\]);/)[1];
  ok(same(JSON.parse(allowed.replaceAll("'", '"')), contract.wireNames), 'bridge and C# share the exact external vocabulary');
}
console.log('cliErr()');
{
  const contract = JSON.parse(fs.readFileSync(path.join(HERE, 'fixtures/life-contract.json'), 'utf8'));
  for (const row of contract.cases) {
    const answer = { decision: 'set', goal: structuredClone(row.goal) };
    const got = shape(answer);
    ok((got === null) === row.bridgeValid, 'shared life selector: ' + row.name, String(got));
  }
  for (const count of [-1, 0, 10000, 2.5, '6']) ok(shape({ decision: 'set', goal: { do: 'farm', mode: 'plant', count } }) !== null, 'farm invalid count type/range: ' + count);
  ok(shape({ decision: 'set', goal: { do: 'cook', mode: 3 } }) !== null && shape({ decision: 'set', goal: { do: 'cook', mode: 'put', second: 3 } }) !== null, 'selector strings require string types');
}
const CLIERR = [
  ['', 'spawn'], ['spawn ENOENT', 'spawn'], ['timeout after 90 s', 'timeout'], ['Timeout', 'spawn'], ['not logged in', 'login'],
  ['You hit your usage limit', 'limit'], ['rate_limit_error', 'limit'], ['Rate-limit', 'limit'], ['HTTP 429', 'limit'], ['402 Payment', 'limit'], ['quota exhausted', 'limit'],
  ['Limit reached', 'limit'], ['quota exceeded', 'limit'], ['you hit your limit', 'limit'], ['x4290', 'spawn'], ['timeout; not logged in', 'timeout'],
];
const cliBad = CLIERR.filter(([d, want]) => cliErr(d) !== want);
ok(!cliBad.length, `cliErr() table (${CLIERR.length} cases)`, cliBad.map(([d, w]) => `"${d}": ${cliErr(d)} != ${w}`).join('; '));

console.log('200 envelope');
const loopOk = { calls: [{ ms: 20, ok: true, chars: 900 }, { ms: 20, ok: true, chars: 1200 }], searches: 1, rounds: [{ log: [{ q: 'about', target: 'o:3050', hits: 2 }] }] };
const rOk = { ok: true, text: '{"decision":"hold"}', model: 'm', effort: 'low', detail: '', usage: { input: 10 } };
const base = { member: 'claude', cli: 'claude', r: rOk, totalMs: 1234, body: { report_id: 7, goal_rev: 3 }, loop: loopOk, brief: 'b'.repeat(100), lastTurnChars: 1200, usage: { input: 20 }, index: 'i'.repeat(50), pfUsed: { queries: [] } };
const eOk = envelope({ ...base, error: null, answer: { decision: 'hold' }, bad: null });
ok(same(keys(eOk), ENVELOPE_KEYS), 'success: keys and order as bridge.mjs:280-288', keys(eOk).join(','));
ok(same(eOk, { ok: true, member: 'claude', cli: 'claude', model: 'm', effort: 'low', ms: 1234, report_id: 7, goal_rev: 3, answer: { decision: 'hold' }, raw: '{"decision":"hold"}', error: null, detail: null,
  prompt_chars: 1300, turn_chars: 1200, answer_chars: 19, usage: { input: 20 }, usage_last: { input: 10 }, stderr_api_override: false, calls: 2, searches: 1, index_chars: 50, prefetch_used: true,
  recall_summary: '미리 찾음 씀; about o:3050(2)' }), 'success: values');
const txt = JSON.stringify(eOk);
ok(txt.indexOf('"answer":') < txt.indexOf('"error":') && txt.indexOf('"error":') < txt.indexOf('"recall_summary":') && txt.endsWith(`"recall_summary":"${eOk.recall_summary}"}`), 'serialized: answer, then error, recall_summary last');
const eParse = envelope({ ...base, error: 'parse', answer: { decision: 'nope' }, bad: 'bad decision' });
ok(eParse.ok === false && eParse.answer === null && eParse.detail === 'bad decision' && same(keys(eParse), ENVELOPE_KEYS), 'parse: answer null, detail = the shape error, same keys');
const eLoop = envelope({ ...base, error: 'timeout', answer: null, bad: null, loop: { ...loopOk, error: 'timeout', detail: '판단 마감' } });
ok(eLoop.detail === '판단 마감' && eLoop.error === 'timeout', 'loop error (not cli): detail from the loop');
const eCli = envelope({ ...base, error: 'limit', answer: null, bad: null, loop: { ...loopOk, error: 'cli' }, r: { ok: false, text: '', detail: 'usage limit ' + 'x'.repeat(400) + ' API_KEY' } });
ok(eCli.detail === ('usage limit ' + 'x'.repeat(400)).slice(0, 300) && eCli.stderr_api_override === true && eCli.raw === '' && eCli.answer_chars === 0, 'cli error: detail = CLI detail cut at 300, api-key note seen');
ok(envelope({ ...base, error: null, answer: {}, bad: null, r: { text: 'x'.repeat(2000) } }).raw.length === 1500, 'raw cut at 1500');

console.log('error envelopes');
for (const [name, [got, status, body]] of Object.entries(ERRORS)) {
  ok(got.status === status && same(keys(got), ['status', 'out']) && same(got.out, body) && same(keys(got.out), keys(body)), `${name}: ${status} ${JSON.stringify(body).slice(0, 90)}`, JSON.stringify(got));
}

// ---- plan() with a mocked preflight and a fake chat: the early returns and the logs
console.log('plan() early returns and logs');
const T = fs.mkdtempSync(path.join(os.tmpdir(), 'bridge-protocol-'));
const MEMBERS = { claude: { cli: 'claude', name: 'Claude' }, gpt: { cli: 'codex', name: 'ChatGPT' } };
const health = { claude: { found: true, login: true, subscriptionOk: true, skip: false, maxPct: 12 } };
const logs = [], recalls = [];
let release = null;
const fake = async (member, brief, turn) => {
  if (turn.includes('"trigger":"slow"')) await new Promise((r) => (release = r));
  return { ok: true, text: JSON.stringify({ decision: 'hold', why: 'w', say: 's' }), ms: 20, detail: '', usage: { input: 5, output: 1 }, model: 'fake-model', effort: 'low' };
};
const busy = new Set();
const plan = createPlan({ MEMBERS, cfg: { prefetchTtlMin: 30, aiPlayers: T, memoryIndex: true, recall: true, recallBudget: {} }, SEATS: {}, VIA_CLI: {}, FAKE_CHAT: fake, adapters: null, game: null,
  briefFor: () => 'BRIEF', busy, preflight: { check: async (id) => health[id] }, log: (o) => logs.push(o), recallLog: (o) => recalls.push(o),
  lat: { claude: [], gpt: [] }, p90Sec: () => 10, DEADLINE_SEC: 120 });
const req = (trigger, extra = {}) => ({ member: 'claude', world: 'not-a-world', report_id: 11, goal_rev: 2, report: { trigger }, ...extra });
const p400 = await plan(req('x', { member: 'nobody' }));
ok(p400.status === 400 && same(p400.out, { ok: false, error: 'unknown member', detail: 'members: claude, gpt' }), 'unknown member: 400');
const slow = plan(req('slow'));
await new Promise((r) => setTimeout(r, 20));
const p409 = await plan(req('second'));
ok(p409.status === 409 && same(p409.out, { ok: false, error: 'busy' }) && busy.has('claude'), 'same member while busy: 409');
release(); const pSlow = await slow;
ok(pSlow.status === 200 && !busy.has('claude'), 'busy released after the decision');
const p200 = await plan(req('goal_done'));
ok(p200.status === 200 && same(keys(p200.out), ENVELOPE_KEYS) && p200.out.ok && p200.out.answer.decision === 'hold' && p200.out.report_id === 11 && p200.out.goal_rev === 2, '200: envelope keys in order, answer, report_id/goal_rev echoed');
const asLine = (o) => JSON.parse(JSON.stringify(o));   // what log() writes: undefined fields dropped
const l200 = asLine(logs.at(-1));
ok(same(keys(l200), PLAN_LOG_KEYS.filter((k) => k !== 'prefetch')) && l200.mem_note === 'memory: world id not usable' && l200.quota_pct === 12 && l200.no_search === true && l200.trigger === 'goal_done',
  'plan.jsonl line: keys in order (mem_note set, no prefetch), quota_pct from preflight', keys(l200).join(','));
ok(recalls.length === 0, 'no recall.jsonl line without index, rounds or prefetch');
const cases = [
  ['spawn', { found: false }, 503, { ok: false, error: 'spawn', detail: 'claude CLI not found' }],
  ['login', { found: true, login: false, authNote: 'expired' }, 503, { ok: false, error: 'login', detail: 'expired' }],
  ['auth', { found: true, login: true, subscriptionOk: false, authMethod: 'apiKey' }, 503, { ok: false, error: 'auth', detail: 'claude: authMethod=apiKey (refusing: not the subscription)' }],
  ['limit', { found: true, login: true, subscriptionOk: true, skip: true, skipWhy: 'usage 90% > 85%' }, 429, { ok: false, error: 'limit', detail: 'usage 90% > 85%', retry_after_s: 1800 }],
];
for (const [name, h, status, out] of cases) {
  health.claude = h; const n = logs.length;
  const p = await plan(req('auto_on'));
  ok(p.status === status && same(p.out, out) && !busy.has('claude') && logs.length === n + (name === 'limit' ? 1 : 0), `preflight ${name}: ${status}, ${name === 'limit' ? 'one log line' : 'no log line'}, busy released`);
}
ok(same(keys(asLine(logs.at(-1))), LIMIT_LOG_KEYS) && logs.at(-1).trigger === 'auto_on' && logs.at(-1).error === 'limit', 'limit log line: keys in order', keys(logs.at(-1)).join(','));

// ---- preflight with mocked chatroom adapters (no CLI is run)
console.log('preflight with mocked CLIs');
const ad = { has: (cli) => cli !== 'grok', bins: { claude: 'claude.exe', codex: 'codex.exe' }, cwd: () => T, gate: { ready: async (cli) => cli !== 'agy' } };
const runMock = async (bin, args) => ({ stdout: JSON.stringify({ loggedIn: true, authMethod: 'claude.ai', subscriptionType: 'max' }) });
const CLI = { codex: { checkLogin: async () => ({ ok: true, note: 'Logged in using ChatGPT' }), fetchUsage: async () => ({ windows: [{ id: 'week', usedPct: 91, resetsAt: 0 }, { id: '5h', usedPct: 3 }] }) },
  claude: { fetchUsage: async () => ({ windows: [{ id: 'week', usedPct: 40 }, { id: 'x', usedPct: 99, minor: true }] }) } };
const PM = { claude: { cli: 'claude' }, gpt: { cli: 'codex' }, gemini: { cli: 'agy' }, grok: { cli: 'grok' } };
const pb = new Set(['gpt']);
const pre = createPreflight({ MEMBERS: PM, adapters: ad, CLI, run: runMock, HERE: T, QUOTA_SKIP_PCT: 85, busy: pb, FAKE_CHAT: null });
const mc = await pre.check('claude');
ok(mc.found && mc.login && mc.subscriptionOk && mc.subscription === 'max' && !mc.skip && mc.maxPct === 40 && mc.ok && pre.state.claude.authAt > 0, 'claude: subscription login, usage 40% (minor window ignored)');
const mg = await pre.check('gpt');
ok(mg.subscription === 'ChatGPT' && mg.skip && mg.skipWhy === 'usage 91% > 85%' && !mg.ok, 'codex: ChatGPT login, 91% -> skip');
const ma = await pre.check('gemini');
ok(ma.found && !ma.login && ma.authNote === 'not logged in (LoginGate)' && ma.skip && /usage unknown/.test(ma.skipWhy), 'agy behind the login gate: not logged in, usage unknown -> skip');
const mk = await pre.check('grok');
ok(!mk.found && mk.quotaNote === 'CLI not found', 'grok CLI missing: found false');
const hh = pre.health();
ok(hh.ok === true && same(hh.busy, ['gpt']) && same(keys(hh.members), ['claude', 'gpt', 'gemini', 'grok']), 'health: ok when one member is, busy listed');
const pf = createPreflight({ MEMBERS: PM, adapters: ad, CLI, run: runMock, HERE: T, QUOTA_SKIP_PCT: 85, busy: pb, FAKE_CHAT: () => {} });
ok(same(await pf.check('grok'), { found: true, login: true, subscriptionOk: true, skip: false, maxPct: null }) && !pf.state.grok.authAt, 'fake chat: no checks, the fixed health');
fs.rmSync(T, { recursive: true, force: true });

// ---- fixture envelopes (refactor S1, test/fixtures/plan/*.json): checked when present
console.log('fixtures');
const FIX = process.env.PLAN_FIXTURES ? path.resolve(process.env.PLAN_FIXTURES) : path.join(HERE, 'fixtures', 'plan');   // PLAN_FIXTURES: another tree's fixtures
if (!fs.existsSync(FIX)) console.log(`  [SKIP] ${path.relative(path.join(HERE, '..'), FIX)} not here (written by the C# worker in S1; checked after the merge)`);
else {
  const ERR_KEYS = Object.fromEntries(Object.values(ERRORS).map(([g]) => [g.out.error, keys(g.out)]));
  const files = fs.readdirSync(FIX).filter((f) => f.endsWith('.json') && !f.endsWith('.expect.json'));
  let checked = 0, skipped = 0;
  for (const f of files) {
    let j; try { j = JSON.parse(fs.readFileSync(path.join(FIX, f), 'utf8').replace(/^﻿/, '')); } catch { j = null; }
    const b = j && typeof j === 'object' && Number.isInteger(j.status) && j.body && typeof j.body === 'object' ? j.body : j;
    if (!b || typeof b !== 'object' || Array.isArray(b)) { skipped++; continue; }   // not an envelope (cut body, html, empty): the mod's side only
    const k = keys(b);
    const full = 'recall_summary' in b || 'answer' in b;
    const want = full ? ENVELOPE_KEYS : ERR_KEYS[b.error];
    if (!want) { ok(false, `${f}: an error envelope the bridge never sends (error ${JSON.stringify(b.error)})`); continue; }
    const extra = k.filter((x) => !want.includes(x));
    const order = k.every((x, i) => i === 0 || want.indexOf(k[i - 1]) < want.indexOf(x));
    const missing = want.filter((x) => !k.includes(x));
    ok(!extra.length && order, `${f}: keys in the bridge's order${missing.length ? ` (not in the fixture: ${missing.join(',')})` : ''}`, `extra ${extra.join(',')}; keys ${k.join(',')}`);
    checked++;
  }
  console.log(`  fixtures: ${checked} envelopes checked, ${skipped} not JSON objects (skipped)`);
}

console.log(fails ? `${fails}/${checks} FAILED` : `ALL ${checks} CHECKS OK`);
process.exit(fails ? 1 : 0);
