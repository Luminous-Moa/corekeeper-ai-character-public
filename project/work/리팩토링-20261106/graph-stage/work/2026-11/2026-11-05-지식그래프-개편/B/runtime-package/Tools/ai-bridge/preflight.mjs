// preflight.mjs — per member: is the CLI there, logged in with the subscription, and under the usage limit.
// The login is read once, the usage at most every 10 minutes (quotaCheck). health() is GET /v1/health.
// Refactor S8 (work/2026-11/2026-11-04-리팩토링/2-계획.md): moved out of bridge.mjs unchanged. The chatroom's adapters,
// CLI table and run() come in from bridge.mjs (which loads them), so this module has no side effects of its own.

export function createPreflight({ MEMBERS, adapters, CLI, run, HERE, QUOTA_SKIP_PCT, busy, FAKE_CHAT }) {
  const state = Object.fromEntries(Object.keys(MEMBERS).map((id) => [id, {
    cli: MEMBERS[id].cli, found: false, login: false, subscription: null, authMethod: null, authNote: '',
    weekPct: null, maxPct: null, windows: null, weekResets: null, quotaReadAt: 0, quotaNote: '',
  }]));

  async function authCheck(id) {
    const s = state[id];
    const cli = MEMBERS[id].cli;
    s.found = adapters.has(cli);
    if (!s.found) return;
    if (cli === 'claude') {
      const r = await run(adapters.bins.claude, ['auth', 'status'], { cwd: adapters.cwd('login', 'check'), timeoutMs: 20000 });
      try {
        const j = JSON.parse(r.stdout);
        s.login = j.loggedIn === true;
        s.authMethod = j.authMethod || null;
        s.subscription = j.authMethod === 'claude.ai' ? (j.subscriptionType || 'yes') : null;
      } catch { s.login = false; s.authMethod = `unreadable: ${r.stdout.slice(0, 80)}`; }
      return;
    }
    // grok / agy: the chatroom's gate (never runs a logged-out CLI that would open a browser)
    if (!(await adapters.gate.ready(cli))) { s.login = false; s.authNote = 'not logged in (LoginGate)'; return; }
    let r = { ok: null };
    try { r = await CLI[cli].checkLogin(adapters.bins[cli], { cwd: adapters.cwd('login', 'check') }); } catch (e) { r = { ok: null, why: String(e.message || e).slice(0, 80) }; }
    s.login = r.ok === true;
    s.authNote = r.note || r.why || '';
    if (cli === 'codex') { s.authMethod = r.note || 'unknown'; s.subscription = /chatgpt/i.test(r.note || '') ? 'ChatGPT' : null; }
    else { s.authMethod = 'account login'; s.subscription = s.login ? 'account' : null; }
  }

  async function quotaCheck(id, force = false) {
    const s = state[id];
    const cli = MEMBERS[id].cli;
    if (!force && Date.now() - s.quotaReadAt < 10 * 60 * 1000) return;
    s.quotaReadAt = Date.now();
    if (!s.found) { s.quotaNote = 'CLI not found'; return; }
    try {
      if (!(await adapters.gate.ready(cli))) throw new Error('not logged in (LoginGate)');
      const u = await CLI[cli].fetchUsage.call({ bins: adapters.bins, cwd: adapters.cwd('usage', 'check'), root: HERE });
      const main = u.windows.filter((w) => !w.minor && typeof w.usedPct === 'number');
      const week = u.windows.find((w) => w.id === 'week');
      s.windows = Object.fromEntries(u.windows.map((w) => [w.id, w.usedPct]));
      s.weekPct = week ? week.usedPct : null;
      s.maxPct = main.length ? Math.max(...main.map((w) => w.usedPct)) : null;
      s.weekResets = week?.resetsAt ? new Date(week.resetsAt).toISOString() : null;
      s.quotaNote = main.length ? '' : 'no usage window';
    } catch (e) {
      s.weekPct = null; s.maxPct = null;
      s.quotaNote = `could not read usage: ${String(e.message || e).slice(0, 120)}`;
    }
  }

  function memberHealth(id) {
    const s = state[id];
    const subOk = s.cli === 'claude' ? s.authMethod === 'claude.ai' : !!s.subscription;   // by the CLI (a test seat may move a member to another CLI)
    const over = s.maxPct != null && s.maxPct > QUOTA_SKIP_PCT;
    const unknown = s.maxPct == null && s.cli !== 'claude';
    const skip = over || unknown;
    return { ...s, subscriptionOk: subOk, skip, skipWhy: over ? `usage ${s.maxPct}% > ${QUOTA_SKIP_PCT}%` : unknown ? `usage unknown (${s.quotaNote})` : null, skipAbovePct: QUOTA_SKIP_PCT,
      ok: s.found && s.login && subOk && !skip };
  }

  function health() {
    const members = Object.fromEntries(Object.keys(MEMBERS).map((id) => [id, memberHealth(id)]));
    return { ok: Object.values(members).some((m) => m.ok), members, busy: [...busy] };
  }

  // Before each decision: the login once, the usage when due (none with the test's fake CLI), then the member's health.
  async function check(member) {
    if (!FAKE_CHAT) {
      if (!state[member].authAt) { await authCheck(member); state[member].authAt = Date.now(); }
      await quotaCheck(member);
    }
    const m = FAKE_CHAT ? { found: true, login: true, subscriptionOk: true, skip: false, maxPct: null } : memberHealth(member);
    return m;
  }

  return { state, authCheck, quotaCheck, memberHealth, health, check };
}
