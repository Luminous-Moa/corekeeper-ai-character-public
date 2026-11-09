// The bridge's wiring with a fake CLI (no real CLI, no game): memory_index in the turn, a recall answered with the
// results, the answer to the mod without recall/prefetch, recall_summary last, a prefetch attached once to the next
// request, the retry after a cap parse with 0 searches, a closed connection stopping the decision.
// Run: node test/bridge-wiring.test.mjs   (starts bridge.mjs on port 8399 with a temp config and AI_BRIDGE_LOGS in a temp
// folder: nothing is written to the real logs/)
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { spawn } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const KG = process.env.KG_DATA || 'C:/Dev/CoreKeeperModSDK-kg/Tools/knowledge/data';
let fails = 0, checks = 0;
const ok = (c, what, extra = '') => { checks++; if (!c) { fails++; console.log(`  [FAIL] ${what}${extra ? `\n         ${extra}` : ''}`); } else console.log(`  [OK] ${what}`); };

const T = fs.mkdtempSync(path.join(os.tmpdir(), 'bridge-wiring-'));
const W = 'b'.repeat(32), dir = path.join(T, 'AIPlayers', W, 'claude');
fs.mkdirSync(dir, { recursive: true });
const ev = (id, kind, play, ents, d, run = null, src = 'game') => JSON.stringify({ id, sess: 's', t: 1, play, world: W, char: 'claude', kind, run, ents, src, d });
fs.writeFileSync(path.join(dir, 'events.jsonl'), [
  ev(1, 'down', 100, ['enemy:Caveling', 'cell:-5,7', 'area:Slime'], { cause: 'x (-70.2,120.4) y', enemies: ['Caveling'], c: { hp: 100, tlv: 1, wlv: 1, alv: 0 } }),
  ev(2, 'fight', 110, ['enemy:Caveling', 'cell:-5,7', 'area:Slime'], { secs: 9, enemies: { Caveling: 1 }, with: [], downs: 1, c: { hp: 100, tlv: 1, wlv: 1, alv: 0 } }),
].join('\n') + '\n');
fs.writeFileSync(path.join(dir, 'exp_state.json'), JSON.stringify({ play: 600 }));
fs.writeFileSync(path.join(dir, 'known.json'), JSON.stringify({ seenId: [3050], heldId: [] }));
fs.writeFileSync(path.join(T, 'config.json'), JSON.stringify({ aiPlayers: path.join(T, 'AIPlayers'), kgData: KG }));
// the fake CLI: answers from what the turn contains
fs.writeFileSync(path.join(T, 'fake.mjs'), `
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
export default async function (member, brief, turn, sec) {
  const rid = +(turn.match(/보고 번호 (\\d+)/) || [])[1];
  const FOR = { report: rid, goal_rev: 1 };
  const j = (o) => ({ ok: true, text: JSON.stringify(o), ms: 20, detail: '', usage: { input: brief.length + turn.length, output: 7 } });
  if (rid === 50) return j({ decision: 'hold', for: FOR, why: (brief.includes('[[') ? 'markers ' : '') + (brief.includes('목차') ? 'index-brief' : 'plain-brief') + (turn.includes('memory_index') ? ' index-turn' : ' no-index-turn') });
  if (rid === 9) { await sleep(3000); return j({ decision: 'hold', for: FOR }); }       // slow: the test closes the connection
  if (rid === 3 || rid === 4) return j({ decision: 'recall', for: FOR, recall: [{ q: 'about', target: 'o:3050' }] });   // always recall
  if (turn.includes('검색 결과:')) return j({ decision: 'set', for: FOR, goal: { do: 'explore' }, why: 'error "answer" test', prefetch: [{ q: 'near', place: 'here' }] });
  if (rid === 1) return j({ decision: 'recall', for: FOR, recall: [{ q: 'about', target: 'o:3050' }], why: '동굴인 확인' });
  return j({ decision: 'hold', for: FOR, why: turn.includes('prefetched (') ? 'saw prefetch' : 'no prefetch' });
}`);

const PORT = 8399;
const REAL = path.join(HERE, '..', 'logs', 'plan.jsonl');
const sizeOf = (f) => { try { return fs.statSync(f).size; } catch { return -1; } };
const before = { size: sizeOf(REAL), realChanged: () => sizeOf(REAL) !== before.size };
const child = spawn(process.execPath, [path.join(HERE, '..', 'bridge.mjs')], {
  env: { ...process.env, AI_BRIDGE_PORT: String(PORT), AI_BRIDGE_CONFIG: path.join(T, 'config.json'), AI_BRIDGE_FAKE_CHAT: path.join(T, 'fake.mjs'), AI_BRIDGE_MEMBERS: 'claude', AI_BRIDGE_LOGS: path.join(T, 'logs') },
  stdio: ['ignore', 'pipe', 'pipe'],
});
let outText = '';
child.stdout.on('data', (d) => (outText += d)); child.stderr.on('data', (d) => (outText += d));
await new Promise((r) => { const t = setInterval(() => { if (outText.includes('listening')) { clearInterval(t); r(); } }, 50); setTimeout(() => { clearInterval(t); r(); }, 15000); });
ok(outText.includes('listening'), 'bridge started with the fake CLI', outText.slice(0, 400));

function post(body, { abortAfterMs } = {}) {
  return new Promise((resolve) => {
    const data = JSON.stringify(body);
    const req = http.request({ host: '127.0.0.1', port: PORT, path: '/v1/plan', method: 'POST', headers: { 'Content-Type': 'application/json' } }, (res) => {
      let t = ''; res.on('data', (d) => (t += d)); res.on('end', () => resolve({ status: res.statusCode, text: t }));
    });
    req.on('error', () => resolve({ status: 0, text: '' }));
    if (abortAfterMs) setTimeout(() => req.destroy(), abortAfterMs);
    req.end(data);
  });
}
const rep = (rid, extra = {}) => ({ member: 'claude', world: W, report_id: rid, goal_rev: 1, report: { trigger: 'goal_done', me: { hp: '100/100', tile: '-70,121' }, last_answer_rejected: null, ...extra } });

// 1. recall -> set, prefetch carried
const r1 = await post(rep(1));
const j1 = JSON.parse(r1.text);
ok(j1.ok && j1.answer?.decision === 'set' && j1.searches === 1 && j1.calls === 2, `recall -> set (searches ${j1.searches}, calls ${j1.calls})`);
ok(!('recall' in j1.answer) && !('prefetch' in j1.answer), 'answer to the mod without recall/prefetch');
ok(j1.index_chars > 0 && j1.recall_summary.includes('about o:3050'), `memory_index in the turn (${j1.index_chars} chars), recall_summary "${j1.recall_summary}"`);
const iErr = r1.text.indexOf('"error":'), iAns = r1.text.indexOf('"answer":'), iSum = r1.text.indexOf('"recall_summary":');
ok(iAns < iErr && iErr < iSum && !/"/.test(j1.recall_summary), 'answer, then error, then recall_summary (the mod reads the first keys by hand)');
// 2. the next request gets the prefetch once
const j2 = JSON.parse((await post(rep(2))).text);
ok(j2.answer?.why === 'saw prefetch' && j2.prefetch_used, 'next request: prefetched attached');
const j2b = JSON.parse((await post(rep(5))).text);
ok(j2b.answer?.why === 'no prefetch' && !j2b.prefetch_used, 'the one after: not again (used once)');
// 3. always recall: cap -> parse; the mod's retry gets 0 searches
const j3 = JSON.parse((await post(rep(3))).text);
ok(j3.error === 'parse' && /검색은 한 판단에/.test(j3.detail) && j3.searches === 2 && j3.calls === 4, `cap: parse with a plain detail (calls ${j3.calls}, searches ${j3.searches})`);
// the mod's reader is checked against fixtures/plan/cases.expect.json (refactor S1): the real replies have the fixtures' keys, in order
{ const fx = JSON.parse(fs.readFileSync(path.join(HERE, 'fixtures', 'plan', 'cases.expect.json'), 'utf8')).cases;
  // model / effort are left out of the reply when the CLI does not say them (the fake CLI here never does)
  const ks = (o) => Object.keys(o).filter((k) => k !== 'model' && k !== 'effort').join();
  const keys = (name) => ks(JSON.parse(fx.find((c) => c.name === name).body));
  ok(ks(j1) === keys('ok-set-gather'), 'a set reply has the keys of fixture ok-set-gather, in order', ks(j1));
  ok(ks(j3) === keys('env-parse'), 'a parse reply has the keys of fixture env-parse, in order', ks(j3)); }
const j4 = JSON.parse((await post(rep(4, { trigger: 'retry', last_answer_rejected: '답 형식 오류: ' + j3.detail }))).text);
ok(j4.searches === 0 && j4.error === 'parse', `retry after the cap: 0 searches (searches ${j4.searches}, calls ${j4.calls})`);
// 4. bad world id: no memory, still an answer
const j5 = JSON.parse((await post({ ...rep(6), world: '../../etc' })).text);
ok(j5.ok && j5.index_chars === 0, 'a world id that is not 32 hex: no memory used, still answered');
// 5. the mod closes the connection: the decision stops
const t0 = Date.now();
const r9 = await post(rep(9), { abortAfterMs: 400 });
ok(r9.status === 0 && Date.now() - t0 < 2000, 'closed connection returns at once on the mod side');
await new Promise((r) => setTimeout(r, 3500));
const logs = fs.readFileSync(path.join(T, 'logs', 'plan.jsonl'), 'utf8').trim().split('\n').map((l) => JSON.parse(l));
const l9 = logs.filter((l) => l.report_id === 9 && l.member === 'claude').at(-1);
ok(l9 && l9.error === 'aborted', `the decision logged as aborted (${l9?.error})`);
const l1 = logs.filter((l) => l.report_id === 1).at(-1);
ok(l1 && l1.rounds === 1 && l1.recall?.[0]?.queries?.[0]?.q === 'about' && Array.isArray(l1.calls), 'plan.jsonl has rounds, queries and calls');
ok(fs.existsSync(path.join(T, 'logs', 'recall.jsonl')), 'recall.jsonl written in the given logs folder');
ok(!before.realChanged(), 'the real logs/plan.jsonl was not touched (review 9)');
ok(j1.usage?.input > 0 && j1.usage.input === (l1.calls || []).reduce((a, c) => a + (c.usage?.input || 0), 0) && l1.calls.every((c) => c.usage), `usage is the sum over the calls (${j1.usage?.input}), each call keeps its own (review 3)`);
ok(outText.includes('game graph'), 'the start line says where the game graph is (review 8)');
const jb = JSON.parse((await post(rep(50))).text);
ok(jb.answer?.why === 'index-brief index-turn', `memory index on: the brief and the turn have it (${jb.answer?.why})`);

child.kill();
// (a) recall only: the brief has neither the index nor its example keys (review 2)
fs.writeFileSync(path.join(T, 'config.json'), JSON.stringify({ aiPlayers: path.join(T, 'AIPlayers'), kgData: KG, memoryIndex: false }));
await new Promise((r) => setTimeout(r, 500));
const child2 = spawn(process.execPath, [path.join(HERE, '..', 'bridge.mjs')], { env: { ...process.env, AI_BRIDGE_PORT: String(PORT), AI_BRIDGE_CONFIG: path.join(T, 'config.json'), AI_BRIDGE_FAKE_CHAT: path.join(T, 'fake.mjs'), AI_BRIDGE_MEMBERS: 'claude', AI_BRIDGE_LOGS: path.join(T, 'logs2') }, stdio: ['ignore', 'pipe', 'pipe'] });
let out2 = ''; child2.stdout.on('data', (d) => (out2 += d)); child2.stderr.on('data', (d) => (out2 += d));
await new Promise((r) => { const t = setInterval(() => { if (out2.includes('listening')) { clearInterval(t); r(); } }, 50); setTimeout(() => { clearInterval(t); r(); }, 15000); });
const ja = JSON.parse((await post(rep(50))).text);
ok(ja.answer?.why === 'plain-brief no-index-turn' && ja.index_chars === 0, `memory index off: no index in the brief or the turn (${ja.answer?.why})`);
child2.kill();
fs.rmSync(T, { recursive: true, force: true });
console.log(fails ? `${fails}/${checks} FAILED` : `ALL ${checks} CHECKS OK`);
process.exit(fails ? 1 : 0);
