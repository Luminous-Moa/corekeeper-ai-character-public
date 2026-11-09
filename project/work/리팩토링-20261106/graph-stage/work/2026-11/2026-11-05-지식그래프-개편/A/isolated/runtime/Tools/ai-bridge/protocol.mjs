// protocol.mjs — what the bridge sends back to the mod for POST /v1/plan (and the server's own errors): the answer's
// shape check, the CLI error words, the 200 envelope and every error envelope, in one place.
// Refactor S8 (work/2026-11/2026-11-04-리팩토링/2-계획.md): moved out of bridge.mjs unchanged; key order and values are
// the contract with the mod (AIPlanner.cs reads the reply; its old by-hand reader took the first "answer": / "error":
// key, so answer and error come before recall_summary, which stays last). test/protocol.test.mjs pins all of it.
import { recallSummary } from './recall-loop.mjs';

// Shape check only (the mod checks the facts).
export function shape(a) {
  if (!a || typeof a !== 'object') return 'no-json';
  if (!['set', 'hold', 'ask'].includes(a.decision)) return 'bad decision';
  if (a.decision === 'set') {
    const g = a.goal;
    if (!g || !['gather', 'explore', 'follow', 'craft', 'store', 'home', 'farm', 'cook'].includes(g.do)) return 'bad goal.do';
    if (g.do === 'store' && g.item != null && typeof g.item !== 'string') return 'store item must be a string';
    if ((g.do === 'gather' || g.do === 'craft') && (typeof g.item !== 'string' || !Number.isInteger(g.count))) return g.do + ' needs item and integer count';
  }
  if (a.say != null && typeof a.say !== 'string') return 'bad say';
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
