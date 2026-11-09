// replay.mjs — the real-CLI replay comparison (design 7.1-7): the same logged reports sent to real member CLIs through
// the real bridge, (a) recall only (memoryIndex off, the brief without the index) and (b) memory_index + recall, each
// against the character's records cut at the moment the report was sent.
//
//   node replay.mjs pick --from claude:8,gemini:8 [--log <Player.log>]... [--world <id>] [--as claude,gemini] [--out <dir>]
//       Reads the mod log's "PLANNER: report #n sent" lines (copies, read only) and matches each report to its own
//       plan_result event (report number, trigger, play time). The snapshot is cut when the report was sent
//       (plan_result play − its secs): no event after that, and not the answer's own goal_start/goal_continue
//       (source llm:<member>#<n>). Writes <out>/set.json and <out>/AIPlayers/<fake world>/<member>/… for every
//       member in --as (so any CLI can play it). known.json and memory.json are today's (a little more than then).
//   node replay.mjs check --set <dir>      every snapshot: nothing after the send time, no goal_start of the answer
//   node replay.mjs run --set <dir> --mode a|b --member claude [--model <id>] [--effort <e>] [--label <name>] [--port 8398]
//       Starts bridge.mjs with its own config (snapshots, memoryIndex for b, the model override) and its own logs
//       folder <dir>/logs-<label>-<mode>, posts every report, writes <dir>/result-<label>-<mode>.jsonl.
//   node replay.mjs table --set <dir>      one line per label × mode
//   node replay.mjs judge --set <dir> [--out <file>]   per report: the report, the logged answer, every label × mode
//       answer, its queries and the start of their results (for the reviewer who judges the goals)
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import crypto from 'node:crypto';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { AIPLAYERS } from './memgraph.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const NAMES = { Claude: 'claude', ChatGPT: 'gpt', Gemini: 'gemini', Grok: 'grok' };
const LOGDIR = path.join(process.env.USERPROFILE || '', 'AppData/LocalLow/Pugstorm/Core Keeper');
const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf(`--${k}`); return i >= 0 ? argv[i + 1] : d; };
const opts = (k) => argv.flatMap((a, i) => (a === `--${k}` ? [argv[i + 1]] : []));
const readLines = (f) => (fs.existsSync(f) ? fs.readFileSync(f, 'utf8').split('\n').filter((l) => l.trim()) : []);
const readJsonl = (f) => readLines(f).map((l) => { try { return JSON.parse(l); } catch { return null; } }).filter(Boolean);

function eventsOf(dir) {
  const rot = fs.readdirSync(dir).filter((f) => /^events-.*\.jsonl$/.test(f)).sort();
  return [...rot, 'events.jsonl'].flatMap((f) => readJsonl(path.join(dir, f)));
}

// The events of a snapshot: before the report was sent, and not the answer's own goal start.
// Report ids start again at 1 in every game session, so "llm:<member>#<rid>" also names goal starts of earlier
// sessions: only one at or after the send time is this answer's own (play time never goes back, 1단계).
const ownAfter = (e, member, rid, sendPlay) => (e.kind === 'goal_start' || e.kind === 'goal_continue')
  && String(e.d?.source || '') === `llm:${member}#${rid}` && e.play >= sendPlay - 0.5;
function cutFor(events, member, rid, sendPlay) {
  return events.filter((e) => e.play <= sendPlay && !ownAfter(e, member, rid, sendPlay));
}

function pick() {
  const from = (opt('from', 'claude:16')).split(',').map((x) => x.split(':')).map(([m, n]) => ({ member: m, n: +(n || 16) }));
  const as = opt('as', [...new Set(from.map((f) => f.member))].join(',')).split(',');
  const logs = opts('log').length ? opts('log') : [path.join(LOGDIR, 'Player.log')];
  const out = opt('out', path.join(HERE, 'data', 'replay', new Date().toISOString().slice(0, 10)));
  const root = opt('root', AIPLAYERS);
  fs.mkdirSync(out, { recursive: true });
  const set = [];
  for (const { member, n } of from) {
    const world = opt('world') || fs.readdirSync(root).find((w) => fs.existsSync(path.join(root, w, member, 'events.jsonl')));
    const src = path.join(root, world, member);
    const events = eventsOf(src);
    const plans = events.filter((e) => e.kind === 'plan_result');
    const reports = [];
    for (const f of logs) {
      for (const l of fs.readFileSync(f, 'utf8').split('\n')) {
        const m = l.match(/@(\w+) PLANNER: report #(\d+) sent trigger=(\w+) rev=(\d+) reportChars=\d+[^:{]*: (\{.*\})\s*$/);   // later builds add " buildMs=…"
        if (!m || NAMES[m[1]] !== member) continue;
        let report; try { report = JSON.parse(m[5]); } catch { continue; }
        const ph = report.experience?.play_hours;
        const cand = plans.filter((p) => p.d?.report === +m[2] && p.d?.trigger === m[3] && (ph == null || Math.abs(p.play - ph * 3600) <= 0.06 * 3600));
        if (cand.length !== 1) continue;   // not matched to exactly one decision: left out
        const p = cand[0];
        reports.push({ rid: +m[2], rev: +m[4], trigger: m[3], report, plan: p, sendPlay: Math.max(0, p.play - (+p.d.secs || 0)) });
      }
    }
    const uniq = [...new Map(reports.map((r) => [r.plan.id, r])).values()].sort((a, b) => a.plan.id - b.plan.id);
    const step = Math.max(1, uniq.length / n);
    const chosen = []; for (let i = 0; i < uniq.length && chosen.length < n; i += step) chosen.push(uniq[Math.floor(i)]);
    for (const r of chosen) {
      const fake = crypto.createHash('md5').update(`${world}/${member}/${r.plan.id}`).digest('hex');
      const kept = cutFor(events, member, r.rid, r.sendPlay);
      for (const who of as) {
        const d = path.join(out, 'AIPlayers', fake, who);
        fs.mkdirSync(d, { recursive: true });
        fs.writeFileSync(path.join(d, 'events.jsonl'), kept.map((e) => JSON.stringify(e)).join('\n') + '\n');
        fs.writeFileSync(path.join(d, 'exp_state.json'), JSON.stringify({ v: 1, play: r.sendPlay }));
        for (const f of ['known.json', 'memory.json']) if (fs.existsSync(path.join(src, f))) fs.copyFileSync(path.join(src, f), path.join(d, f));
      }
      set.push({ char: member, world: fake, from_world: world, report_id: r.rid, goal_rev: r.rev, trigger: r.trigger, plan_event: r.plan.id, send_play: r.sendPlay,
        last_kept_event: kept.at(-1)?.id ?? null, logged: { result: r.plan.d.result, decision: r.plan.d.decision, goal: r.plan.d.goal, why: r.plan.d.why, say: r.plan.d.say, reason: r.plan.d.reason, secs: r.plan.d.secs }, report: r.report });
    }
    console.log(`${member}: ${reports.length} reports matched to one decision each, ${chosen.length} chosen`);
  }
  fs.writeFileSync(path.join(out, 'set.json'), JSON.stringify(set, null, 1));
  console.log(`-> ${out} (${set.length} reports, snapshots for ${as.join(', ')})`);
  check(out);
}

// Every snapshot against the original records: nothing after the send time, not the answer's own goal start.
function check(dir = opt('set'), root = opt('root', AIPLAYERS)) {
  const set = JSON.parse(fs.readFileSync(path.join(dir, 'set.json'), 'utf8'));
  let bad = 0;
  for (const s of set) {
    const snapDirs = fs.readdirSync(path.join(dir, 'AIPlayers', s.world));
    for (const who of snapDirs) {
      const ev = readJsonl(path.join(dir, 'AIPlayers', s.world, who, 'events.jsonl'));
      const late = ev.filter((e) => e.play > s.send_play);
      const own = ev.filter((e) => ownAfter(e, s.char, s.report_id, s.send_play));
      const plan = ev.filter((e) => e.id >= s.plan_event);
      // nothing that was there before the send time may be missing (an over-cut snapshot hides real memory)
      const srcDir = path.join(root, s.from_world, s.char);
      const have = new Set(ev.map((e) => e.id));
      const missing = fs.existsSync(srcDir) ? eventsOf(srcDir).filter((e) => e.play <= s.send_play && !ownAfter(e, s.char, s.report_id, s.send_play) && !have.has(e.id)).length : null;
      if (late.length || own.length || plan.length || missing) { bad++; console.log(`  BAD ${s.char} #${s.report_id} (${who}): after send ${late.length}, own goal ${own.length}, at/after plan_result ${plan.length}, missing ${missing}`); }
    }
  }
  console.log(`check: ${set.length} reports, ${bad ? `${bad} snapshot(s) BAD` : 'all clean (nothing after the send time, no goal start of the answer, nothing before it missing)'}`);
  return bad;
}

function post(port, body) {
  return new Promise((resolve) => {
    const req = http.request({ host: '127.0.0.1', port, path: '/v1/plan', method: 'POST', headers: { 'Content-Type': 'application/json' } }, (res) => {
      let t = ''; res.on('data', (d) => (t += d)); res.on('end', () => { try { resolve(JSON.parse(t)); } catch { resolve({ ok: false, error: 'bad reply', raw: t.slice(0, 200) }); } });
    });
    req.setTimeout(160000, () => req.destroy());
    req.on('error', (e) => resolve({ ok: false, error: String(e.message) }));
    req.end(JSON.stringify(body));
  });
}

async function runMode() {
  const dir = opt('set'), mode = opt('mode'), member = opt('member', 'claude');
  if (!dir || !['a', 'b'].includes(mode)) throw new Error('run needs --set <dir> --mode a|b');
  const label = opt('label', opt('model') || member);
  const set = JSON.parse(fs.readFileSync(path.join(dir, 'set.json'), 'utf8'));
  const port = +opt('port', 8398);
  const base = fs.existsSync(path.join(HERE, 'config.json')) ? JSON.parse(fs.readFileSync(path.join(HERE, 'config.json'), 'utf8')) : {};
  const cfg = { ...base, aiPlayers: path.join(dir, 'AIPlayers'), memoryIndex: mode === 'b', recall: true, ...(opt('kg') ? { kgData: opt('kg') } : {}) };
  if (opt('model')) {
    const def = { claude: { model: 'claude-opus-5-5', effort: 'low' }, gpt: { model: 'gpt-6.1-sol', effort: 'low' }, gemini: { model: 'gemini-3.8-flash-low' }, grok: { model: 'grok-4.7', effort: 'low' } };
    cfg.agents = { ...def, ...(base.agents || {}), [member]: { model: opt('model'), ...(opt('effort') ? { effort: opt('effort') } : {}) } };
  }
  const cfgFile = path.join(dir, `config-${label}-${mode}.json`);
  fs.writeFileSync(cfgFile, JSON.stringify(cfg));
  const logs = path.join(dir, `logs-${label}-${mode}`);
  const child = spawn(process.execPath, [path.join(HERE, 'bridge.mjs')], { env: { ...process.env, AI_BRIDGE_PORT: String(port), AI_BRIDGE_CONFIG: cfgFile, AI_BRIDGE_MEMBERS: member, AI_BRIDGE_LOGS: logs }, stdio: ['ignore', 'pipe', 'pipe'] });
  let log = ''; child.stdout.on('data', (d) => { log += d; process.stdout.write(d); }); child.stderr.on('data', (d) => (log += d));
  await new Promise((r) => { const t = setInterval(() => { if (log.includes('listening') || child.exitCode != null) { clearInterval(t); r(); } }, 100); setTimeout(() => { clearInterval(t); r(); }, 120000); });
  if (!log.includes('listening')) { child.kill(); throw new Error(`bridge did not start (port ${port} in use?): ${log.slice(-600)}`); }
  const outFile = path.join(dir, `result-${label}-${mode}.jsonl`);
  fs.writeFileSync(outFile, '');
  for (const s of set) {
    const o = await post(port, { member, world: s.world, report_id: s.report_id, goal_rev: s.goal_rev, report: s.report });
    const row = { label, mode, member, model: o.model, char: s.char, report_id: s.report_id, world: s.world, ok: o.ok, error: o.error, detail: o.detail, ms: o.ms, calls: o.calls, searches: o.searches,
      index_chars: o.index_chars, prompt_chars: o.prompt_chars, usage: o.usage, recall_summary: o.recall_summary, answer: o.answer, raw: o.ok ? undefined : o.raw };
    fs.appendFileSync(outFile, JSON.stringify(row) + '\n');
    console.log(`[${label} ${mode}] ${s.char}#${s.report_id}: ${o.ok ? `${o.answer?.decision} ${JSON.stringify(o.answer?.goal || '')}` : `${o.error} ${String(o.detail || '').slice(0, 80)}`} searches ${o.searches} calls ${o.calls} ${o.ms} ms ${o.recall_summary || ''}`);
  }
  child.kill();
  await new Promise((r) => (child.exitCode != null ? r() : child.once('exit', r)));   // the port is free for the next run
}

const FORMAT_ERR = new Set(['shape', 'q', 'arg', 'empty', 'place']);   // a query that is not a well-formed query
function runs(dir) {
  return fs.readdirSync(dir).filter((f) => /^result-.*-[ab]\.jsonl$/.test(f)).map((f) => {
    const [, label, mode] = f.match(/^result-(.*)-([ab])\.jsonl$/);
    const rows = readJsonl(path.join(dir, f));
    const plans = readJsonl(path.join(dir, `logs-${label}-${mode}`, 'plan.jsonl')).filter((p) => p.report_id != null);
    const recs = readJsonl(path.join(dir, `logs-${label}-${mode}`, 'recall.jsonl'));
    for (const r of rows) { r.plan = plans.find((p) => p.world === r.world && p.report_id === r.report_id); r.rec = recs.find((x) => x.world === r.world && x.report_id === r.report_id); }
    return { label, mode, rows };
  }).sort((a, b) => (a.label + a.mode).localeCompare(b.label + b.mode));
}

function table(dir = opt('set')) {
  const L = [];
  L.push('| 모델 | 모드 | 판단 | 판단당 호출 | 검색한 판단 | recall 질의 맞음/틀림 | 빈 결과 | 시간 중앙값/p90 | 판단당 입력/출력 토큰 | 답(set/hold/ask/오류) |');
  L.push('|---|---|---|---|---|---|---|---|---|---|');
  for (const { label, mode, rows } of runs(dir)) {
    const n = rows.length, calls = rows.reduce((a, r) => a + (r.calls || 0), 0);
    let qOk = 0, qBad = 0, empty = 0, q = 0;
    for (const r of rows) for (const rd of r.plan?.recall || []) for (const x of rd.queries || []) { q++; if (FORMAT_ERR.has(x.err)) qBad++; else qOk++; if (!x.hits || x.err) empty++; }
    const ms = rows.map((r) => r.ms).filter((x) => x != null).sort((a, b) => a - b);
    const pc = (p) => (ms.length ? (ms[Math.min(ms.length - 1, Math.ceil(ms.length * p) - 1)] / 1000).toFixed(1) : '-');
    const inT = rows.reduce((a, r) => a + (r.usage?.input || 0) + (r.usage?.cached || 0), 0), outT = rows.reduce((a, r) => a + (r.usage?.output || 0), 0);
    const dec = { set: 0, hold: 0, ask: 0, err: 0 }; for (const r of rows) dec[r.ok ? r.answer?.decision || 'err' : 'err'] = (dec[r.ok ? r.answer?.decision || 'err' : 'err'] || 0) + 1;
    L.push(`| ${label} | ${mode === 'a' ? '(가) 검색만' : '(나) 목차+검색'} | ${n} | ${n ? (calls / n).toFixed(2) : '-'} | ${rows.filter((r) => r.searches > 0).length} | ${qOk}/${qBad} | ${q ? `${empty}/${q}` : '-'} | ${pc(0.5)}/${pc(0.9)}초 | ${n ? Math.round(inT / n) : '-'}/${n ? Math.round(outT / n) : '-'} | ${dec.set}/${dec.hold}/${dec.ask}/${dec.err} |`);
  }
  const t = L.join('\n'); console.log(t); return t;
}

function judge() {
  const dir = opt('set'), outFile = opt('out', path.join(dir, 'judge.md'));
  const set = JSON.parse(fs.readFileSync(path.join(dir, 'set.json'), 'utf8'));
  const all = runs(dir);
  const L = ['# 재생 비교 판정용: 보고별 답', '', '목표 적합성 판정은 하지 않았다. 각 보고의 요약, 실제 게임에서 그때 나온 답(logged), 모델·모드별 답과 쓴 질의·결과 앞부분이다.', ''];
  for (const s of set) {
    const r = s.report;
    const gather = r.can_do?.gather || [], craft = (r.can_do?.craft || []).map((x) => String(x).split(':')[0]);
    L.push(`## ${s.char} #${s.report_id} (${s.trigger}, 플레이 ${(s.send_play / 3600).toFixed(2)}시간)`);
    L.push(`- 직전 목표: ${r.goal?.text || '-'} (${r.goal?.source || '-'}) · 끝난 까닭: ${r.why_ended || '-'}`);
    L.push(`- 나: 체력 ${r.me?.hp}, 배고픔 ${r.me?.hunger}, 손 ${r.me?.hand}, 코어 ${r.me?.core_dist}칸, 보이는 적 ${r.me?.enemies_in_view} · 최근 쓰러짐: ${(r.recent_downs || []).slice(0, 2).join(' / ') || '-'}`);
    L.push(`- 지난 일: ${(r.events || []).join('; ') || '-'}`);
    L.push(`- can_do: gather ${gather.join(',')} · craft ${craft.slice(0, 10).join(',')}${craft.length > 10 ? '…' : ''} · store ${r.can_do?.store} home ${r.can_do?.home} farm ${r.can_do?.farm} cook ${r.can_do?.cook}`);
    L.push(`- 거절된 직전 답: ${r.last_answer_rejected || '-'}`);
    const idx = all.map(({ rows }) => rows.find((y) => y.world === s.world && y.report_id === s.report_id)?.rec?.index).find(Boolean);
    if (idx) L.push(`- (나)에 붙은 목차:\n\n\`\`\`\n${idx}\n\`\`\`\n`);
    L.push(`- **그때 실제 답(logged ${s.logged.result}):** ${s.logged.decision} ${s.logged.goal || ''} — "${s.logged.why || ''}"${s.logged.reason ? ` (버린 까닭: ${s.logged.reason})` : ''}`);
    for (const { label, mode, rows } of all) {
      const x = rows.find((y) => y.world === s.world && y.report_id === s.report_id);
      if (!x) continue;
      const a = x.answer;
      L.push(`- **${label} ${mode === 'a' ? '(가)' : '(나)'}:** ${x.ok ? `${a?.decision} ${a?.goal ? JSON.stringify(a.goal) : ''} — "${a?.why || ''}"` : `오류 ${x.error}: ${String(x.detail || '').slice(0, 120)}`} · 호출 ${x.calls}, 검색 ${x.searches}, ${((x.ms || 0) / 1000).toFixed(1)}초`);
      for (const [i, rd] of (x.rec?.rounds || []).entries()) {
        L.push(`  - 검색 ${i + 1}: ${JSON.stringify(rd.request)}${rd.why ? ` (why: ${rd.why})` : ''}`);
        const head = String(rd.result || '').split('\n').slice(1, 6).map((l) => l.slice(0, 160)).join(' / ');
        L.push(`    - 결과 앞부분: ${head}`);
      }
    }
    L.push('');
  }
  fs.writeFileSync(outFile, L.join('\n'));
  console.log(`-> ${outFile}`);
}

const cmd = argv[0];
if (cmd === 'pick') pick();
else if (cmd === 'check') process.exit(check() ? 1 : 0);
else if (cmd === 'run') await runMode();
else if (cmd === 'table') table();
else if (cmd === 'judge') judge();
else console.log('usage: node replay.mjs pick|check|run|table|judge … (see the head of this file)');
