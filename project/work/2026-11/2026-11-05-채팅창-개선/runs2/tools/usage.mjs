// usage.mjs — read each subscription's usage windows right now (no model call), the way the bridge preflight does.
import path from 'node:path';
import { pathToFileURL } from 'node:url';
const CHATROOM = 'C:/Dev/ai-chatroom';
const HERE = 'C:/Dev/CoreKeeperModSDK-nl/Tools/ai-bridge';
const { Adapters } = await import(pathToFileURL(path.join(CHATROOM, 'lib/agents/adapters.mjs')).href);
const { CLI } = await import(pathToFileURL(path.join(CHATROOM, 'lib/agents/cli/index.mjs')).href);
const cfg = { agents: { claude: { model: 'claude-opus-5-5' }, gpt: { model: 'gpt-6.1-sol' }, gemini: { model: 'gemini-3.8-flash-low' }, grok: { model: 'grok-4.7' } }, turnTimeoutSec: 60, bins: {} };
const ad = new Adapters(HERE, cfg);
const out = { at: new Date().toISOString(), members: {} };
for (const [id, cli] of [['claude', 'claude'], ['gpt', 'codex'], ['gemini', 'agy'], ['grok', 'grok']]) {
  try {
    ad.refreshBins?.();
    if (!(await ad.gate.ready(cli))) throw new Error('not logged in');
    const u = await CLI[cli].fetchUsage.call({ bins: ad.bins, cwd: ad.cwd('usage', 'check'), root: HERE });
    out.members[id] = u.windows.map((w) => ({ id: w.id, pct: w.usedPct, resets: w.resetsAt ? new Date(w.resetsAt).toISOString() : null }));
  } catch (e) { out.members[id] = { error: String(e.message || e).slice(0, 160) }; }
}
console.log(JSON.stringify(out, null, 1));
