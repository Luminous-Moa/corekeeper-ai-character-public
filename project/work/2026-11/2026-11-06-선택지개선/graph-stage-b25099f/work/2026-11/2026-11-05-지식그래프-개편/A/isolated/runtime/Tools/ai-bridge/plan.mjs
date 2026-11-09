// plan.mjs — one POST /v1/plan decision, in stages: reserve the member (busy) → preflight → memory context (index,
// prefetch, may it search) → recallLoop (the CLI calls) → after the loop (error word, prefetch kept) → envelope → logs.
// Refactor S8 (work/2026-11/2026-11-04-리팩토링/2-계획.md): moved out of bridge.mjs with the same order and values. The
// reply's keys live in protocol.mjs; the member checks in preflight.mjs; config, CLIs and the server stay in bridge.mjs.
// The memory state of the decisions (index per world/member, prefetch per world/member, last search cap) lives here.
import path from 'node:path';
import { MemIndex, context, memoryIndex, runQueries, freezeQueries, LIMITS } from './memgraph.mjs';
import { recallLoop, reportTurn, PrefetchStore, usageSum, CAP_WORDS } from './recall-loop.mjs';
import { shape, cliErr, envelope, errUnknownMember, errBusy, errSpawn, errLogin, errAuth, errLimit } from './protocol.mjs';

const WORLD_RE = /^[0-9a-f]{32}$/;

// deps from bridge.mjs: the member table and config, the CLI adapters (FAKE_CHAT in the wiring test), the game graph,
// the brief, the busy set (shared with health), preflight.check, the logs, the CLI latency (lat, p90Sec), the deadline.
export function createPlan({ MEMBERS, cfg, SEATS, VIA_CLI, FAKE_CHAT, adapters, game, briefFor, busy, preflight, log, recallLog, lat, p90Sec, DEADLINE_SEC }) {
  const indexes = new Map();
  const prefetches = new PrefetchStore(cfg.prefetchTtlMin * 60000);   // (world/member) -> {queries (frozen), atMs}
  const capHitLast = new Set();          // (world/member) whose last decision ended on the search cap (the next one: no searches)

  return async function plan(body, recvAt = Date.now(), aborted = () => false) {
    const member = body.member || 'claude';
    if (!MEMBERS[member]) return errUnknownMember(MEMBERS);
    if (busy.has(member)) return errBusy();
    busy.add(member);   // reserved before the checks, so a second report of the same member waits (409)
    try {
      // ---- preflight (preflight.mjs): login, subscription, usage
      const m = await preflight.check(member);
      const cli = MEMBERS[member].cli;
      if (!m.found) return errSpawn(cli);
      if (!m.login) return errLogin(m);
      if (!m.subscriptionOk) return errAuth(cli, m);
      if (m.skip) {
        log({ member, ok: false, error: 'limit', detail: m.skipWhy, report_id: body.report_id, trigger: body.report?.trigger });
        return errLimit(m);
      }
      const brief = briefFor(member);
      // ---- memory context: this character's index and context (the world id is checked before it goes into a path)
      const report = body.report || {};
      const tileM = String(report.me?.tile || '').match(/^(-?\d+),(-?\d+)$/);
      const tile = tileM ? [+tileM[1], +tileM[2]] : null;
      let ctx = null, index = '', prefetched = '', pfUsed = null, memNote = '';
      const pk = `${body.world}/${member}`;
      if (WORLD_RE.test(String(body.world || ''))) {
        try {
          if (!indexes.has(pk)) indexes.set(pk, new MemIndex(path.join(cfg.aiPlayers, body.world, member), game));
          ctx = context({ root: cfg.aiPlayers, world: body.world, member, game, index: indexes.get(pk), tile });
          if (cfg.memoryIndex) index = memoryIndex(ctx);
        } catch (e) { ctx = null; memNote = `memory: ${String(e.message || e).slice(0, 120)}`; }
      } else memNote = 'memory: world id not usable';
      // what the last decision asked to look up for this one: used once, then dropped (re-check R2)
      const pf = ctx && cfg.recall ? prefetches.take(pk) : null;
      if (pf) {
        const r = runQueries(ctx, pf.queries, { limit: LIMITS.prefetch });
        prefetched = `prefetched (지난 판단 ${Math.round((Date.now() - pf.atMs) / 60000)}분 전에 미리 찾음: ${pf.queries.map((q) => q?.q).join(', ')}, 그 답은 적용 여부 모름)\n${r.text}`;
        pfUsed = { queries: pf.queries, log: r.log };
      }
      // ---- may this decision search (recall), and how often
      const lastRejected = String(report.last_answer_rejected || '');
      const capRetry = capHitLast.has(pk) || (report.trigger === 'retry' && lastRejected.includes(CAP_WORDS));
      const noSearch = !cfg.recall || !ctx || capRetry;
      const noSearchWhy = !cfg.recall ? '기억 찾아보기가 꺼져 있다' : !ctx ? '이번에는 기억을 읽을 수 없다' : '직전 판단이 검색 상한에서 끝나 이번 다시 묻기에는 검색이 없다';
      capHitLast.delete(pk);
      const budget = cfg.recall ? (cfg.recallBudget?.[member] ?? 2) : 0;
      const searchNote = noSearch ? `이번 판단에는 기억 찾아보기(recall)를 쓸 수 없다(${noSearchWhy}).` : `이번 판단의 기억 찾아보기(recall): 최대 ${budget}번.`;
      const baseTurn = reportTurn(body, { index, prefetched, searchNote });
      // ---- the CLI calls (recall-loop.mjs)
      const loop = await recallLoop({
        // each call's own time limit without touching the shared cfg (other members run at the same time)
        chat: FAKE_CHAT ? (turn, sec) => FAKE_CHAT(member, brief, turn, sec)
          : (turn, sec) => {
            const st = SEATS[member], via = st && VIA_CLI[st.via] ? st.via : member;
            const ad = Object.create(adapters);
            ad.cfg = { ...adapters.cfg, turnTimeoutSec: sec, agents: !st ? adapters.cfg.agents : { ...adapters.cfg.agents, [via]: { model: st.model, ...(st.effort ? { effort: st.effort } : {}) } } };   // a seat on its own CLI changes only the model/effort
            if (via !== member) ad.cwd = (id, sub) => adapters.cwd(member, sub);   // the seat's own folder (no shared prompt files)
            return ad.chat(via, brief, turn);
          },
        runQueries: (qs, o) => runQueries(ctx, qs, o),
        baseTurn, recvAt, deadlineMs: DEADLINE_SEC * 1000, budget, p90Sec: p90Sec(member), agy: cli === 'agy', aborted, noSearch, noSearchWhy,
      });
      // ---- after the loop: latency, prefetch, the error word (protocol.mjs shape/cliErr)
      for (const c of loop.calls) if (c.ok && c.ms) lat[member].push(c.ms);
      if (pf && !loop.calls.length) prefetches.giveBack(pk, pf);   // ended before any CLI call: kept for the next one
      const r = loop.last || { ok: false, text: '', ms: 0, detail: loop.detail || '' };
      const answer = loop.answer || null;
      const bad = loop.error ? null : shape(answer);
      const error = loop.error === 'cli' ? cliErr(r.detail || '') : loop.error ? loop.error : bad ? 'parse' : null;
      if (loop.capHit && error === 'parse') capHitLast.add(pk);
      const usage = usageSum(loop.calls) || r.usage;   // all calls of this decision (each sends the whole prompt again)
      // a prefetch carried by an answer that goes to the mod: frozen to this moment's tile and play time (R2)
      if (!error && loop.prefetch?.length && ctx && cfg.recall && !aborted()) prefetches.put(pk, freezeQueries(loop.prefetch, { tile, play: ctx.play }));
      const totalMs = Date.now() - recvAt;
      const lastTurnChars = loop.calls.length ? loop.calls[loop.calls.length - 1].chars : baseTurn.length;
      // ---- the reply (protocol.mjs) and the logs
      const out = envelope({ error, member, cli, r, totalMs, body, answer, bad, loop, brief, lastTurnChars, usage, index, pfUsed });
      log({ member, cli, model: r.model, ms: totalMs, ok: out.ok, error, world: body.world, report_id: body.report_id, goal_rev: body.goal_rev, trigger: report.trigger,
        turn_chars: out.turn_chars, prompt_chars: out.prompt_chars, answer_chars: out.answer_chars, usage, quota_pct: m.maxPct,
        rounds: loop.rounds.length, recall: loop.rounds.map((x, i) => ({ round: i + 1, queries: x.log, ms: x.ms })), calls: loop.calls, prefetch_used: !!pfUsed,
        index_chars: index.length, total_ms: totalMs, deadline_hit: loop.deadlineHit, cap_hit: loop.capHit, no_search: noSearch, mem_note: memNote || undefined,
        answer: answer && { decision: answer.decision, goal: answer.goal, say: answer.say }, prefetch: loop.prefetch || undefined });
      if (loop.rounds.length || pfUsed || index) recallLog({ member, world: body.world, report_id: body.report_id, index, prefetched: pfUsed ? prefetched : undefined,
        rounds: loop.rounds.map((x) => ({ request: x.request, why: x.why, result: x.result })), prefetch: loop.prefetch || undefined });
      return { status: 200, out };
    } finally {
      busy.delete(member);
    }
  };
}
