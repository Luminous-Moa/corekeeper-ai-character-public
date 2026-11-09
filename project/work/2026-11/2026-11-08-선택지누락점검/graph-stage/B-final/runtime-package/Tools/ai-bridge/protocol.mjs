// protocol.mjs — what the bridge sends back to the mod for POST /v1/plan (and the server's own errors): the answer's
// shape check, the CLI error words, the 200 envelope and every error envelope, in one place.
// Refactor S8 (work/2026-11/2026-11-04-리팩토링/2-계획.md): moved out of bridge.mjs unchanged; key order and values are
// the contract with the mod (AIPlanner.cs reads the reply; its old by-hand reader took the first "answer": / "error":
// key, so answer and error come before recall_summary, which stays last). test/protocol.test.mjs pins all of it.
import { recallSummary } from './recall-loop.mjs';

// Shape check only (the mod checks the facts). An "order" outside the five values is removed from the answer, and so is
// an "orders" entry.
export const ORDER_VALUES = ['follow', 'defer', 'drop', 'done', 'answered'];
export const PLAN_MAX = Infinity;   // the bridge's own bound; the mod keeps the first upperPlanMaxSteps (default 5)
const GOAL_DO = ['gather', 'explore', 'follow', 'craft', 'store', 'home', 'farm', 'cook', 'goto', 'place', 'equip', 'unequip', 'hold', 'take', 'drop', 'give', 'maintain'];
const INVENTORY_DO = ['equip', 'unequip', 'hold', 'take', 'drop', 'give'];
function goalShape(g) {
  if (!g || !GOAL_DO.includes(g.do)) return 'bad goal.do';
  if (g.max_count != null) {
    if (g.do !== 'gather') return 'max_count is only for an explicit gather ceiling';
    if (!Number.isInteger(g.max_count) || g.max_count < 1 || g.max_count > 2147483647) return 'max_count must be a positive integer';
    if (Number.isInteger(g.count) && g.count > g.max_count) return 'gather target exceeds explicit max_count';
  }
  if (g.max_order != null) {
    if (!Number.isInteger(g.max_order) || g.max_order < 1 || g.max_order > 2147483647) return 'max_order must be a positive HostOrder integer id';
    if (g.max_count == null) return 'max_order requires max_count';
  }
  for (const k of ['mode', 'second']) {
    if (g[k] === null || g[k] === '') delete g[k];
    else if (g[k] != null && typeof g[k] !== 'string') return k + ' must be a string';
  }
  if (!['farm', 'cook', 'craft', 'store', 'take', 'maintain'].includes(g.do) && (g.mode != null || g.second != null)) return 'mode/second are only for life goals';
  if (['craft', 'store', 'take'].includes(g.do) && (g.mode != null || g.second != null)) return g.do + ' does not accept mode/second';
  if (g.do === 'craft' && g.where != null && typeof g.where !== 'string') return 'craft where must be a string';
  if (g.do === 'maintain') {
    if (!['repair', 'upgrade', 'recover'].includes(g.mode)) return 'maintain mode must be repair/upgrade/recover';
    if (Object.hasOwn(g, 'count') && (!Number.isInteger(g.count) || g.count !== 1)) return 'maintain count must be 1';
    if (g.keep != null || g.second != null) return 'maintain does not accept keep/second';
    for (const k of ['item', 'where']) if (g[k] != null && typeof g[k] !== 'string') return 'maintain ' + k + ' must be a string';
    if (g.slot != null && (!Number.isInteger(g.slot) || g.slot < 0 || g.slot > 2147483647)) return 'maintain slot must be a nonnegative integer';
    if (g.where && g.where !== 'null') {
      const station = /^station:[^@]+@([+-]?\d+),([+-]?\d+)$/.exec(g.where);
      if (!station || station.slice(1).some(x => Number(x) < -2147483648 || Number(x) > 2147483647)) return 'maintain where must be station:<ObjectID>@x,y';
    }
  }
  if (g.do === 'farm' || g.do === 'cook') {
    for (const k of ['item', 'where']) if (g[k] != null && typeof g[k] !== 'string') return 'life ' + k + ' must be a string';
    if (g.count != null && (!Number.isInteger(g.count) || g.count < 1 || g.count > 2147483647)) return 'life count must be an integer in 1..2147483647';
    if (g.count != null && !g.mode) return 'life count needs mode';
    if (g.keep != null || g.slot != null) return 'farm/cook do not accept keep/slot';
    if (g.do === 'farm') {
      if (g.mode != null && !['hoe', 'plant', 'water', 'fill', 'harvest'].includes(g.mode)) return 'bad farm mode';
      if (g.second != null) return 'farm does not accept second';
      if (!g.mode && g.item) return 'farm item needs mode';
      if (['hoe', 'fill'].includes(g.mode) && g.item) return 'farm hoe/fill do not accept item';
      if (g.mode === 'fill' && g.count != null && g.count !== 1) return 'farm fill count must be 1';
    } else {
      if (g.mode != null && !['put', 'fill', 'take', 'wait'].includes(g.mode)) return 'bad cook mode';
      if (g.second != null && g.mode !== 'put') return 'cook second is only for put';
      if (g.mode === 'wait' && (g.count != null || g.item)) return 'cook wait does not accept count/item';
    }
  }
  if (g.do === 'store' && g.item != null && typeof g.item !== 'string') return 'store item must be a string';
  if ((g.do === 'gather' || g.do === 'craft') && (typeof g.item !== 'string' || !Number.isInteger(g.count))) return g.do + ' needs item and integer count';
  // 제작대-놓기: goto needs a where; place an item, where optional (base), count optional (1). The mod checks the values.
  if (g.do === 'goto' && typeof g.where !== 'string') return 'goto needs where (a place)';
  if (g.do === 'place' && (typeof g.item !== 'string' || (g.where != null && typeof g.where !== 'string') || (g.count != null && !Number.isInteger(g.count)))) return 'place needs item (where a string, count an integer when given)';
  if (INVENTORY_DO.includes(g.do) || g.do === 'store') {
    if (g.where != null && typeof g.where !== 'string') return 'inventory where must be a string';
    for (const k of ['count', 'keep', 'slot']) if (g[k] != null && (!Number.isInteger(g[k]) || g[k] < 0 || g[k] > 2147483647)) return 'inventory ' + k + ' must be an integer in 0..2147483647';
    if (g.do !== 'store' && g.keep != null) return 'keep is only for store';
    if (!['store', 'unequip'].includes(g.do) && typeof g.item !== 'string') return g.do + ' needs item';
    if (g.do === 'unequip' && typeof g.item !== 'string' && typeof g.where !== 'string' && g.slot == null) return 'unequip needs item or equipment slot';
    if (g.do === 'give' && (typeof g.where !== 'string' || !g.where.trim())) return 'give needs recipient in where';
    if (['equip', 'unequip', 'hold'].includes(g.do) && g.count != null && g.count !== 1) return 'equipment action count must be 1';
    if (['take', 'drop', 'give'].includes(g.do) && g.count === 0) return 'transfer count must be positive';
  }
  return null;
}

export function shape(a) {
  if (!a || typeof a !== 'object') return 'no-json';
  if (!['set', 'keep', 'hold', 'ask'].includes(a.decision)) return 'bad decision';   // keep: go on (자연어 명령)
  if (a.decision === 'set') {
    // a plan (합의 3e): "plan" is a list of goal steps (the mod takes up to its upperPlanMaxSteps); else the one "goal"
    if (a.plan != null) {
      if (!Array.isArray(a.plan) || a.plan.length === 0) return `plan must be a nonempty list of goals`;
      for (let i = 0; i < a.plan.length; i++) { const e = goalShape(a.plan[i]); if (e) return `plan[${i}]: ${e}`; }
    } else {
      const e = goalShape(a.goal);
      if (e) return e;
    }
  }
  // a JSON null text field is no field (재검수 작은 지적 5: the mod read "say": null as the text "null")
  for (const k of ['say', 'why', 'problem', 'order', 'orders']) if (a[k] === null) delete a[k];
  if (a.say != null && typeof a.say !== 'string') return 'bad say';
  if (a.problem != null && typeof a.problem !== 'string') return 'bad problem';   // a problem report (자연어 명령)
  // the host instruction (자연어 명령); a value outside the five is dropped, not a reason to ask again (검수 작은 지적 2)
  if (a.order != null && !ORDER_VALUES.includes(a.order)) delete a.order;
  // valuable kinds (2026-11-08): {"<ObjectID>": true|false}; entries that are not booleans are dropped, a non-object too
  if (a.valuable != null) {
    if (typeof a.valuable !== 'object' || Array.isArray(a.valuable)) delete a.valuable;
    else {
      for (const [k, v] of Object.entries(a.valuable)) if (!/^[A-Za-z][A-Za-z0-9_]{0,63}$/.test(k) || typeof v !== 'boolean') delete a.valuable[k];
      if (Object.keys(a.valuable).length === 0) delete a.valuable;
    }
  }
  // earlier open host instructions (재검수 R3): {"<id>": value}; entries with a bad id or value are dropped, a non-object too
  if (a.orders != null) {
    if (typeof a.orders !== 'object' || Array.isArray(a.orders)) delete a.orders;
    else {
      for (const [id, v] of Object.entries(a.orders)) if (!/^[1-9]\d{0,8}$/.test(id) || !ORDER_VALUES.includes(v)) delete a.orders[id];
      if (Object.keys(a.orders).length === 0) delete a.orders;
    }
  }
  return null;
}

// A failed CLI call (recallLoop error "cli"): the error word from the CLI's detail text.
export const cliErr = (d) => (/timeout/.test(d) ? 'timeout' : /not logged in/.test(d) ? 'login' : /usage limit|rate[ _-]?limit|\b402\b|\b429\b|exhausted|limit reached|quota exceeded|hit your (usage )?limit/i.test(d) ? 'limit' : 'spawn');

// The 200 envelope of one decision (also when the decision failed: error parse/timeout/aborted/limit/login/spawn).
export function envelope({ error, member, cli, r, totalMs, body, answer, bad, loop, brief, lastTurnChars, usage, index, pfUsed }) {
  return {
    ok: !error, member, cli, model: r.model, effort: r.effort, ms: totalMs, report_id: body.report_id, goal_rev: body.goal_rev,
    answer: error ? null : answer, raw: String(r.text || '').slice(0, 1500), error,
    detail: error ? (loop.error && loop.error !== 'cli' ? loop.detail : bad || String(r.detail || '').slice(0, 300)) : null,
    prompt_chars: brief.length + lastTurnChars, turn_chars: lastTurnChars, answer_chars: String(r.text || '').length, usage, usage_last: r.usage,
    stderr_api_override: /api key|API_KEY/i.test(r.detail || ''),
    calls: loop.calls.length, searches: loop.searches, index_chars: index.length, prefetch_used: !!pfUsed,
    recall_summary: recallSummary(loop.rounds, !!pfUsed),   // last: the mod reads it with its by-hand Field()
  };
}

// Error envelopes: {status, out}. Before a decision (plan.mjs) and in the server (bridge.mjs).
export const errUnknownMember = (MEMBERS) => ({ status: 400, out: { ok: false, error: 'unknown member', detail: `members: ${Object.keys(MEMBERS).join(', ')}` } });
export const errBusy = () => ({ status: 409, out: { ok: false, error: 'busy' } });
export const errSpawn = (cli) => ({ status: 503, out: { ok: false, error: 'spawn', detail: `${cli} CLI not found` } });
export const errLogin = (m) => ({ status: 503, out: { ok: false, error: 'login', detail: m.authNote } });
export const errAuth = (cli, m) => ({ status: 503, out: { ok: false, error: 'auth', detail: `${cli}: authMethod=${m.authMethod} (refusing: not the subscription)` } });
export const errLimit = (m) => ({ status: 429, out: { ok: false, error: 'limit', detail: m.skipWhy, retry_after_s: 1800 } });
export const errNotFound = () => ({ status: 404, out: { ok: false, error: 'not found' } });
export const errBridge = (e) => ({ status: 500, out: { ok: false, error: 'bridge', detail: String(e.message || e).slice(0, 300) } });
