// Offline contract comparison (S8): drive one bridge.mjs with a fake CLI and a fixed request sequence, record every
// reply (status, raw text, key order at every depth), plan.jsonl and recall.jsonl lines, the health reply.
// Usage: node compare.mjs <bridge.mjs> <out.json> <port>
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { spawn } from 'node:child_process';

const [bridgeFile, outFile, portArg] = process.argv.slice(2);
const PORT = +portArg;
const KG = 'C:/Dev/CoreKeeperModSDK-refactor-bridge/Tools/knowledge/data';
const T = fs.mkdtempSync(path.join(os.tmpdir(), 's8-compare-'));
const W = 'b'.repeat(32), dir = path.join(T, 'AIPlayers', W, 'claude');
fs.mkdirSync(dir, { recursive: true });
const ev = (id, kind, play, ents, d, run = null, src = 'game') => JSON.stringify({ id, sess: 's', t: 1, play, world: W, char: 'claude', kind, run, ents, src, d });
fs.writeFileSync(path.join(dir, 'events.jsonl'), [
  ev(1, 'down', 100, ['enemy:Caveling', 'cell:-5,7', 'area:Slime'], { cause: 'x (-70.2,120.4) y', enemies: ['Caveling'], c: { hp: 100, tlv: 1, wlv: 1, alv: 0 } }),
  ev(2, 'fight', 110, ['enemy:Caveling', 'cell:-5,7', 'area:Slime'], { secs: 9, enemies: { Caveling: 1 }, with: [], downs: 1, c: { hp: 100, tlv: 1, wlv: 1, alv: 0 } }),
].join('\n') + '\n');
fs.writeFileSync(path.join(dir, 'exp_state.json'), JSON.stringify({ play: 600 }));
fs.writeFileSync(path.join(dir, 'known.json'), JSON.stringify({ seenId: [3050], heldId: [] }));
fs.writeFileSync(path.join(T, 'fake.mjs'), `
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
export default async function (member, brief, turn, sec) {
  const rid = +(turn.match(/보고 번호 (\\d+)/) || [])[1];
  const FOR = { report: rid, goal_rev: 1 };
  const j = (o, extra = {}) => ({ ok: true, text: JSON.stringify(o), ms: 20, detail: '', usage: { input: brief.length + turn.length, output: 7 }, ...extra });
  const fail = (detail) => ({ ok: false, text: '', ms: 5, detail, usage: { input: 3 } });
  if (rid === 50) return j({ decision: 'hold', for: FOR, why: (brief.includes('[[') ? 'markers ' : '') + (brief.includes('목차') ? 'index-brief' : 'plain-brief') + (turn.includes('memory_index') ? ' index-turn' : ' no-index-turn') });
  if (rid === 9) { await sleep(3000); return j({ decision: 'hold', for: FOR }); }
  if (rid === 20) { await sleep(1500); return j({ decision: 'hold', for: FOR }); }
  if (rid === 3 || rid === 4) return j({ decision: 'recall', for: FOR, recall: [{ q: 'about', target: 'o:3050' }] });
  if (rid === 7) return fail('timeout after 90 s');
  if (rid === 8) return fail('You hit your usage limit (ANTHROPIC_API_KEY ignored)');
  if (rid === 10) return fail('not logged in');
  if (rid === 11) return fail('spawn ENOENT');
  if (rid === 12) return { ok: true, text: 'sorry, no json here', ms: 20, detail: '', model: 'fake-m', effort: 'low' };
  if (rid === 13) return j({ decision: 'foo', for: FOR });
  if (rid === 14) return j({ decision: 'set', for: FOR, goal: { do: 'gather', item: 'Wood' } });
  if (rid === 15) return j({ memo: { why: 'x', goal: 1 }, decision: 'ask', for: FOR, say: 'he said "hi"\\nand \\\\ left', why: 'q' }, { model: 'fake-m', effort: 'low' });
  if (rid === 16) return j({ decision: 'set', for: FOR, goal: { do: 'craft', item: 'CopperBar', count: 1 }, prefetch: [{ q: 'about', target: 'o:3050' }] });
  if (turn.includes('검색 결과:')) return j({ decision: 'set', for: FOR, goal: { do: 'explore' }, why: 'error "answer" test', prefetch: [{ q: 'near', place: 'here' }] });
  if (rid === 1) return j({ decision: 'recall', for: FOR, recall: [{ q: 'about', target: 'o:3050' }], why: '동굴인 확인' });
  return j({ decision: 'hold', for: FOR, why: turn.includes('prefetched (') ? 'saw prefetch' : 'no prefetch' });
}`);

function request(method, p, data, { abortAfterMs } = {}) {
  return new Promise((resolve) => {
    const req = http.request({ host: '127.0.0.1', port: PORT, path: p, method, headers: { 'Content-Type': 'application/json' } }, (res) => {
      let t = ''; res.on('data', (d) => (t += d)); res.on('end', () => resolve({ status: res.statusCode, text: t }));
    });
    req.on('error', () => resolve({ status: 0, text: '' }));
    if (abortAfterMs) setTimeout(() => req.destroy(), abortAfterMs);
    req.end(data);
  });
}
const post = (body, o) => request('POST', '/v1/plan', typeof body === 'string' ? body : JSON.stringify(body), o);
const rep = (rid, extra = {}) => ({ member: 'claude', world: W, report_id: rid, goal_rev: 1, report: { trigger: 'goal_done', me: { hp: '100/100', tile: '-70,121' }, last_answer_rejected: null, ...extra } });

async function start(cfg, logs) {
  fs.writeFileSync(path.join(T, 'config.json'), JSON.stringify({ aiPlayers: path.join(T, 'AIPlayers'), kgData: KG, ...cfg }));
  const child = spawn(process.execPath, [bridgeFile], { env: { ...process.env, AI_BRIDGE_PORT: String(PORT), AI_BRIDGE_CONFIG: path.join(T, 'config.json'), AI_BRIDGE_FAKE_CHAT: path.join(T, 'fake.mjs'), AI_BRIDGE_MEMBERS: 'claude', AI_BRIDGE_LOGS: path.join(T, logs) }, stdio: ['ignore', 'pipe', 'pipe'] });
  let out = ''; child.stdout.on('data', (d) => (out += d)); child.stderr.on('data', (d) => (out += d));
  await new Promise((r) => { const t = setInterval(() => { if (out.includes('listening')) { clearInterval(t); r(); } }, 50); setTimeout(() => { clearInterval(t); r(); }, 15000); });
  return { child, stdout: () => out };
}

const replies = [];
const rec = (name, r) => replies.push({ name, status: r.status, text: r.text });

// phase 1: memory index and recall on
let b = await start({}, 'logs');
rec('health', await request('GET', '/v1/health'));
for (const [name, body] of [
  ['1 recall->set+prefetch', rep(1)], ['2 prefetch used', rep(2)], ['5 not again', rep(5)],
  ['3 cap->parse', rep(3)], ['4 retry after cap', null], ['6 bad world', { ...rep(6), world: '../../etc' }],
  ['7 cli timeout', rep(7)], ['8 cli usage limit', rep(8)], ['10 cli not logged in', rep(10)], ['11 cli spawn', rep(11)],
  ['12 no json', rep(12)], ['13 bad decision', rep(13)], ['14 gather no count', rep(14)], ['15 ask say escapes, memo first', rep(15)],
  ['16 craft + prefetch', rep(16)], ['17 after prefetch', rep(17)], ['18 no report', { member: 'claude', world: W, report_id: 18, goal_rev: 1 }],
  ['19 member omitted', { world: W, report_id: 19, goal_rev: 1, report: { trigger: 'auto_on' } }],
  ['400 unknown member', { ...rep(30), member: 'nobody' }],
]) {
  if (name.startsWith('4 ')) { const j3 = JSON.parse(replies.at(-1).text); rec(name, await post(rep(4, { trigger: 'retry', last_answer_rejected: '답 형식 오류: ' + j3.detail }))); continue; }
  rec(name, await post(body));
}
// 409: a slow decision and a second report of the same member
const slow = post(rep(20));
await new Promise((r) => setTimeout(r, 300));
rec('409 busy', await post(rep(21)));
rec('20 slow', await slow);
rec('404', await request('GET', '/v1/nope'));
rec('404 post', await request('POST', '/v1/other', '{}'));
rec('500 bad json', await post('{"member": "claude", oops'));
rec('health busy-free', await request('GET', '/v1/health'));
const r9 = await post(rep(9), { abortAfterMs: 400 });
rec('9 aborted (mod side)', r9);
await new Promise((r) => setTimeout(r, 3500));
rec('50 index on', await post(rep(50)));
b.child.kill();
const start1 = b.stdout();
await new Promise((r) => setTimeout(r, 500));
// phase 2: memory index and recall off
b = await start({ memoryIndex: false, recall: false }, 'logs2');
rec('50 index off', await post(rep(50)));
rec('1 recall off', await post(rep(1)));
rec('16 prefetch with recall off', await post(rep(16)));
rec('17 after (recall off)', await post(rep(17)));
b.child.kill();
const start2 = b.stdout();

const read = (f) => { try { return fs.readFileSync(f, 'utf8').trim().split('\n').filter(Boolean); } catch { return []; } };
const result = {
  T, replies,
  plan1: read(path.join(T, 'logs', 'plan.jsonl')), recall1: read(path.join(T, 'logs', 'recall.jsonl')),
  plan2: read(path.join(T, 'logs2', 'plan.jsonl')), recall2: read(path.join(T, 'logs2', 'recall.jsonl')),
  stdout1: start1, stdout2: start2,
};
fs.writeFileSync(outFile, JSON.stringify(result, null, 1));
fs.rmSync(T, { recursive: true, force: true });
console.log(`${replies.length} replies, plan ${result.plan1.length}+${result.plan2.length}, recall ${result.recall1.length}+${result.recall2.length}`);
