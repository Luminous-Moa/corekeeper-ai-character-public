// ai-bridge — local bridge between the Core Keeper AI mod and each member's subscription CLI
// (claude → claude, gpt → codex, gemini → agy, grok → grok; the chatroom's member table).
// Design: work/2026-11/2026-11-04-위쪽LLM-구독CLI-설계.md; reports: work/2026-11/2026-11-04-위쪽LLM-첫단계.md,
// work/2026-11/2026-11-04-멤버넷-캐릭터.md (session 코어키퍼-AI-캐릭터).
//
// - Uses the chatroom's CLI adapters read-only (C:\Dev\ai-chatroom\lib\agents\adapters.mjs): finding the CLI,
//   login check, every tool off, the same flags. Nothing in the chatroom is written; this bridge keeps its own
//   working folder (data/cwd/<member>/chat under this folder), one per member, so members never share prompt files.
// - Subscription, not API: API-key variables (ANTHROPIC_API_KEY, OPENAI_API_KEY, GEMINI_API_KEY, GOOGLE_API_KEY,
//   XAI_API_KEY) are removed from this process (children inherit the cleaned env). Per member the login must show
//   the subscription: claude `auth status` authMethod "claude.ai"; codex `login status` "using ChatGPT"; grok and agy
//   logged in (their CLIs have only the account login). Otherwise the bridge refuses plans for that member ("auth").
// - Quota: each CLI's account usage without a model call (claude `-p /usage`, codex app-server rateLimits, grok
//   ACP billing, agy `-p /usage`), read at start and again every 10 minutes per member. Above 85% in any main window
//   the member is skipped (error "limit"). Grok and agy are run only after the chatroom's LoginGate (logged out they
//   open a browser login page by themselves). When usage cannot be read, plans for that member are refused too
//   ("limit", unknown) unless it is claude (first slice behaviour kept).
// - One plan per member at a time (409 busy); different members run at the same time.
// - Memory graph search (기억-그래프-검색, work/2026-11/2026-11-04-기억-그래프-검색/1-설계.md): every report turn gets the
//   character's memory_index (memgraph.mjs); the CLI may answer {"decision":"recall","recall":[...]} and is asked again
//   with the results (recall-loop.mjs), at most recallBudget searches per decision, or carry "prefetch" queries in its
//   answer for the next decision. recall/prefetch never reach the mod. One decision ends within DEADLINE_SEC of the
//   request (the checks before the first call count); each CLI call gets the time left as its own limit.
//
// API (127.0.0.1:8331):
//   GET  /v1/health            -> {ok, members: {<id>: {...}}, busy}
//   POST /v1/plan  {member, world, report_id, goal_rev, report} -> {ok, ms, answer|null, raw, error, ..., recall_summary}
// Log: logs/plan.jsonl (one line per decision), logs/recall.jsonl (queries and results in full, for the developer).
// Modules (refactor S8): preflight.mjs (login, subscription, usage, health), plan.mjs (one decision, in stages),
// protocol.mjs (the answer shape, the reply envelope and every error envelope). This file: config, the chatroom
// CLIs, the server (a closed connection stops the decision) and the checks at start.

import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { AIPLAYERS, findGame } from './memgraph.mjs';
import { childrenOf } from './recall-loop.mjs';
import { createPreflight } from './preflight.mjs';
import { createPlan } from './plan.mjs';
import { errNotFound, errBridge } from './protocol.mjs';

for (const k of ['ANTHROPIC_API_KEY', 'OPENAI_API_KEY', 'GEMINI_API_KEY', 'GOOGLE_API_KEY', 'XAI_API_KEY']) delete process.env[k]; // before anything spawns a CLI

const HERE = path.dirname(fileURLToPath(import.meta.url));
const CHATROOM = 'C:/Dev/ai-chatroom';
const PORT = Number(process.env.AI_BRIDGE_PORT || 8331);
const QUOTA_SKIP_PCT = 85;
const PLAN_TIMEOUT_SEC = 90;     // one CLI call at most (each call's own limit is the time left, below)
const DEADLINE_SEC = 120;        // one decision, from when the request came in (the mod waits 150 s)

// member id -> CLI and in-game name (the chatroom's lib/members.mjs ids and CLIs; names as in the mod's AIAgents.cs)
const MEMBERS = {
  claude: { cli: 'claude', name: 'Claude' },
  gpt: { cli: 'codex', name: 'ChatGPT' },
  gemini: { cli: 'agy', name: 'Gemini' },
  grok: { cli: 'grok', name: 'Grok' },
};

const cfgFile = path.join(process.env.AI_BRIDGE_CONFIG || path.join(HERE, 'config.json'));
// TEST ONLY: AI_BRIDGE_FAKE_CHAT=<module> replaces the CLI call (default export: async (member, brief, turn, sec) ->
// {ok, text, ms, detail}) and skips the login/usage checks. For the offline wiring test and nothing else.
const FAKE_CHAT = process.env.AI_BRIDGE_FAKE_CHAT ? (await import(pathToFileURL(path.resolve(process.env.AI_BRIDGE_FAKE_CHAT)).href)).default : null;
const cfg = {
  // the chatroom's usual models and efforts (C:\Dev\ai-chatroom\config.json "agents", 2026-11-04), boost not used
  agents: {
    claude: { model: 'claude-opus-5-5', effort: 'low' },
    gpt: { model: 'gpt-6.1-sol', effort: 'low' },
    gemini: { model: 'gemini-3.8-flash-low' },
    grok: { model: 'grok-4.7', effort: 'low' },
  },
  turnTimeoutSec: PLAN_TIMEOUT_SEC,
  bins: {},
  memoryIndex: true,                                        // the memory_index in every report turn (replay compares off/on)
  recall: true,                                             // the CLI may search its memory (recall) and carry prefetch
  recallBudget: { claude: 2, gpt: 2, gemini: 2, grok: 2 },  // searches per decision, per member (e.g. gemini: 1)
  prefetchTtlMin: 30,
  kgData: null,                                             // the game knowledge graph; null: KG_DATA, then this checkout's Tools/knowledge/data (memgraph findGame)
  aiPlayers: AIPLAYERS,
  ...(fs.existsSync(cfgFile) ? JSON.parse(fs.readFileSync(cfgFile, 'utf8')) : {}),
};

// TEST ONLY (config "seats"): a member's seat runs on another CLI and model for a model comparison in the game, e.g.
// "seats": {"gpt": {"via": "claude", "model": "claude-haiku-5-5", "effort": "low"}}. via is the chatroom member whose CLI
// is used (claude → claude CLI, gemini → agy); the seat keeps its own name, working folder, login/usage checks and
// logs. Two seats on one CLI share its bin, so a closed connection kills both seats' calls on that CLI (rare: the mod
// gives up only after 150 s). Without "seats" nothing changes.
const SEATS = cfg.seats || {};
const VIA_CLI = { claude: 'claude', gemini: 'agy', gpt: 'codex', grok: 'grok' };
for (const [id, st] of Object.entries(SEATS)) if (MEMBERS[id] && VIA_CLI[st.via]) MEMBERS[id].cli = VIA_CLI[st.via];

const { Adapters } = await import(pathToFileURL(path.join(CHATROOM, 'lib/agents/adapters.mjs')).href);
const { CLI } = await import(pathToFileURL(path.join(CHATROOM, 'lib/agents/cli/index.mjs')).href);
const { run, running, killTree } = await import(pathToFileURL(path.join(CHATROOM, 'lib/agents/run.mjs')).href);

const adapters = new Adapters(HERE, cfg);
// brief.md is shared; {{name}} is the member's in-game name
const briefTpl = fs.readFileSync(path.join(HERE, 'brief.md'), 'utf8');
const briefMode = (t, withIndex) => t.replace(/\[\[index\]\]([\s\S]*?)\[\[\/index\]\]/g, withIndex ? '$1' : '').replace(/\[\[noindex\]\]([\s\S]*?)\[\[\/noindex\]\]/g, withIndex ? '' : '$1');
const briefFor = (id) => briefMode(briefTpl, !!cfg.memoryIndex).split('{{name}}').join(MEMBERS[id].name);
const LOGS = process.env.AI_BRIDGE_LOGS ? path.resolve(process.env.AI_BRIDGE_LOGS) : path.join(HERE, 'logs');
fs.mkdirSync(LOGS, { recursive: true });
const logFile = path.join(LOGS, 'plan.jsonl');
const log = (o) => fs.appendFileSync(logFile, JSON.stringify({ t: new Date().toISOString(), ...o }) + '\n');
const recallLog = (o) => fs.appendFileSync(path.join(LOGS, 'recall.jsonl'), JSON.stringify({ t: new Date().toISOString(), ...o }) + '\n');

// ---- the game graph (memgraph.mjs), once; the per-decision memory state is in plan.mjs
const { game, dir: gameDir, looked: gameLooked } = findGame(cfg.kgData || undefined);
console.log(game ? `ai-bridge: game graph ${gameDir} (${Object.keys(game.nodes).length} nodes, build ${game.game?.steamBuildId ?? '?'})`
  : `ai-bridge: game graph NOT FOUND (looked in ${gameLooked.join(' ; ')}): no game queries, object names only. Set kgData in config.json`);
// CLI latency p90 per member from the last 20 calls (plan.jsonl at start, then every call)
const lat = Object.fromEntries(Object.keys(MEMBERS).map((id) => [id, []]));
try {
  for (const l of fs.readFileSync(logFile, 'utf8').trim().split('\n').slice(-2000)) {
    let o; try { o = JSON.parse(l); } catch { continue; }
    if (!o.member || !lat[o.member] || !o.ok) continue;
    if (Array.isArray(o.calls)) { for (const c of o.calls) if (c.ok && c.ms) lat[o.member].push(c.ms); } else if (typeof o.ms === 'number') lat[o.member].push(o.ms);
  }
} catch { /* no log yet */ }
const P90_DEFAULT = { claude: 10, codex: 15, agy: 20, grok: 15 };
function p90Sec(id) {
  const l = lat[id].slice(-20).filter((x) => x > 0).sort((a, b) => a - b);
  return l.length >= 5 ? l[Math.min(l.length - 1, Math.ceil(l.length * 0.9) - 1)] / 1000 : P90_DEFAULT[MEMBERS[id].cli];
}

const busy = new Set();
const preflight = createPreflight({ MEMBERS, adapters, CLI, run, HERE, QUOTA_SKIP_PCT, busy, FAKE_CHAT });
const { state, authCheck, quotaCheck, health } = preflight;
const plan = createPlan({ MEMBERS, cfg, SEATS, VIA_CLI, FAKE_CHAT, adapters, game, briefFor, busy, preflight, log, recallLog, lat, p90Sec, DEADLINE_SEC });

const server = http.createServer(async (req, res) => {
  const send = (status, o) => { res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8' }); res.end(JSON.stringify(o)); };
  try {
    if (req.method === 'GET' && req.url === '/v1/health') return send(200, health());
    if (req.method === 'POST' && req.url === '/v1/plan') {
      const recvAt = Date.now();   // the decision's deadline counts from here
      let raw = '';
      for await (const c of req) raw += c;
      const body = JSON.parse(raw || '{}');
      let finished = false, gone = false;
      // The mod gave up (closed the connection): stop this decision and kill this member's CLI only (members and CLIs
      // are 1:1); the other members' calls go on.
      res.on('close', () => {
        if (finished) return;
        gone = true;
        const bin = MEMBERS[body.member]?.cli && adapters.bins[MEMBERS[body.member].cli];
        for (const c of childrenOf(running, bin)) killTree(c.pid);
      });
      const { status, out } = await plan(body, recvAt, () => gone);
      finished = true;
      return send(status, out);
    }
    const nf = errNotFound();
    send(nf.status, nf.out);
  } catch (e) {
    const be = errBridge(e);
    send(be.status, be.out);
  }
});

// Members to check at start: all four, or AI_BRIDGE_MEMBERS=claude,gemini
const startMembers = (process.env.AI_BRIDGE_MEMBERS || Object.keys(MEMBERS).join(',')).split(',').map((x) => x.trim()).filter((x) => MEMBERS[x]);
if (!FAKE_CHAT) for (const id of startMembers) { await authCheck(id); state[id].authAt = Date.now(); await quotaCheck(id, true); }
const h = health();
for (const id of startMembers) {
  const m = h.members[id];
  console.log(`ai-bridge: ${id} (${m.cli}) found=${m.found} login=${m.login} auth=${m.authMethod} subscription=${m.subscription} usage=${JSON.stringify(m.windows)} ${m.skip ? 'SKIP ' + m.skipWhy : 'ok'} ${m.quotaNote}`);
}
log({ event: 'start', members: startMembers, health: h, game_graph: gameDir, memory_index: !!cfg.memoryIndex, recall: !!cfg.recall, agents: cfg.agents, seats: SEATS });
server.listen(PORT, '127.0.0.1', () => console.log(`ai-bridge listening on http://127.0.0.1:${PORT}`));
