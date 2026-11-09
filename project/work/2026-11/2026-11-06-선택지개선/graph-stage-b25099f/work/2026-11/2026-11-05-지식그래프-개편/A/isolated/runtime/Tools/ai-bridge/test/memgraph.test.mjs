// Offline tests of memgraph.mjs (design 7.1-1, -2, -4): a hand-made event set with known answers, the leak cases,
// rotation and a half-written line, and a 100-hour synthetic log for size and speed.
// Run: node test/memgraph.test.mjs [--kg <Tools/knowledge/data>] (the game graph is needed for names; default: the
// -kg worktree's data, read only). Writes only under the OS temp folder.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { performance } from 'node:perf_hooks';
import * as M from '../memgraph.mjs';

const arg = (k, d) => { const i = process.argv.indexOf(`--${k}`); return i > 0 ? process.argv[i + 1] : d; };
const KG = arg('kg', process.env.KG_DATA || 'C:/Dev/CoreKeeperModSDK-kg/Tools/knowledge/data');
const game = M.loadGame(KG);
let fails = 0, checks = 0;
const ok = (c, what, extra = '') => { checks++; if (!c) { fails++; console.log(`  [FAIL] ${what}${extra ? `\n         ${extra}` : ''}`); } else console.log(`  [OK] ${what}`); };
const has = (text, s, what) => ok(text.includes(s), what, text.includes(s) ? '' : `missing '${s}' in:\n${text.slice(0, 700)}`);
const hasNot = (text, s, what) => ok(!text.includes(s), what, text.includes(s) ? `found '${s}' in:\n${text.slice(0, 700)}` : '');

const ROOT = fs.mkdtempSync(path.join(os.tmpdir(), 'memgraph-test-'));
const W = 'a'.repeat(32);
function charDir(member) { const d = path.join(ROOT, W, member); fs.mkdirSync(d, { recursive: true }); return d; }
const C = (o = {}) => ({ hp: 100, hand: '', tool: 'WoodMiningPick', tlv: 1, wpn: 'WoodSword', wlv: 1, alv: 0, area: 'Slime', cell: '0,0', core: 10, mates: 0, hunger: 50, seen: 100, st: 2, ore: -1, ...o });
let nid = 0;
const E = (kind, play, o = {}) => ({ id: ++nid, sess: 's1', t: 1793770000 + play, play, world: W, char: 'claude', kind, run: o.run ?? null, ents: o.ents || [], src: o.src || 'game', d: o.d || {} });

console.log(`game graph: ${game ? `${Object.keys(game.nodes).length} nodes` : 'NONE'} (${KG})`);
if (!game) { console.log('the tests need the game graph'); process.exit(1); }

// ---------------------------------------------------------------- 1. hand-made set
console.log('1. hand-made events');
const d1 = charDir('claude');
const ev = [
  E('session_start', 0),
  E('goal_start', 100, { run: 's1.2', ents: ['item:CopperOre', 'cell:0,0', 'area:Slime'], src: 'llm', d: { key: 'gather:CopperOre', count: 5, source: 'llm:claude#1', c: C() } }),
  E('fight', 160, { run: 's1.2', ents: ['enemy:Caveling', 'player:Luminous'], d: { secs: 12, enemies: { Caveling: 2 }, with: ['Luminous(호스트)'], together: true, downs: 1, boss: '' } }),   // old fight: no cell, no c
  E('down', 170, { run: 's1.2', ents: ['enemy:Caveling', 'enemy:MushroomBrute', 'cell:-5,7', 'area:Slime'], d: { cause: '2026-11-04 15:18:04 (-73.5,127.1) 적 근처(Caveling 1.4)', enemies: ['Caveling', 'MushroomBrute'], c: C({ cell: '-5,7', core: 140 }) } }),
  E('goal_end', 300, { run: 's1.2', ents: ['item:CopperOre', 'cell:-5,7', 'area:Slime'], d: { run: 's1.2', key: 'gather:CopperOre', item: 'CopperOre', count: 5, outcome: 'blocked', cls: 'tool_weak', cause: '도구 부족', got: 0, secs: 200, downs: 1, down_by: ['Caveling'], area: 'Slime', source: 'llm', c0: C(), c1: C({ cell: '-5,7' }), play0: 100, play1: 300 } }),
  E('area_enter', 400, { ents: ['area:Larva', 'cell:-9,3'], d: { area: 'Larva', from: 'Slime', first: true, with: [], c: C({ area: 'Larva', cell: '-9,3' }) } }),
  E('fight', 500, { ents: ['enemy:Larva', 'cell:-9,3', 'area:Larva'], d: { secs: 8, enemies: { Larva: 3 }, with: [], together: false, downs: 0, boss: '', c: C({ wlv: 2, area: 'Larva', cell: '-9,3' }) } }),
  E('item_first', 510, { ents: ['item:Slime'], d: { item: 'Slime', count: 2 } }),
  E('plan_result', 600, { run: null, src: 'llm', d: { result: 'accepted', report: 3, trigger: 'goal_done', decision: 'set', goal: 'gather CopperOre 5', why: '동굴인 근처 구리는 피하고 서쪽으로 간다', say: '가요', reason: '', secs: 5 } }),
  E('plan_result', 650, { src: 'game', d: { result: 'dropped', report: 4, trigger: 'goal_done', decision: 'set', goal: 'gather TinOre 3', why: '동굴인 피해서 주석', say: '', reason: 'can_do 밖', secs: 4 } }),
  E('base_set', 700, { ents: ['cell:-2,2', 'area:Slime'], src: 'host', d: { tile: [-32, 32], by: '호스트', with: [], c: C({ wlv: 2 }) } }),
  E('boss_seen', 800, { run: null, ents: ['enemy:SlimeBoss'], d: { boss: 'SlimeBoss', first: true, hp: 6000, max: 6000, c: C({ wlv: 2, cell: '-5,-2' }) } }),
];
fs.writeFileSync(path.join(d1, 'events.jsonl'), ev.map((e) => JSON.stringify(e)).join('\n') + '\n');
fs.writeFileSync(path.join(d1, 'exp_state.json'), JSON.stringify({ v: 1, next: nid + 1, play: 900 }));
fs.writeFileSync(path.join(d1, 'known.json'), JSON.stringify({ version: 1, seenId: [3050, 1500, 4004], seenAt: [], heldId: [10], heldAt: [], tiles: [], tilesAt: [], dropSrc: [], dropItem: [] }));
fs.writeFileSync(path.join(d1, 'memory.json'), JSON.stringify({ baseSet: 1, baseXY: [-32, 32], oreXY: [-70, 120, -72, 118, 200, 200], oreId: [1500, 1500, 1505], stationId: [4004], stationXY: [-30, 30] }));
let ctx = M.context({ root: ROOT, world: W, member: 'claude', game, tile: [-70, 122] });
ok(ctx.idx.ev.length === ev.length, `index has ${ev.length} events (got ${ctx.idx.ev.length})`);
ok(ctx.play === 900, 'now = exp_state play (900)');
ok(ctx.cond && ctx.cond.w === 2, 'now conditions = latest event with conditions (weapon Lv2)');

const idxText = M.memoryIndex(ctx);
console.log(idxText.split('\n').map((l) => '    | ' + l).join('\n'));
ok(idxText.length <= M.LIMITS.index, `index within ${M.LIMITS.index} chars (${idxText.length})`);
has(idxText, '점수 상위만: 적', 'index header says top-n of all (R3-가)');
has(idxText, 'o:3050 동굴인', 'enemy row keyed o:3050 with the Korean name');
has(idxText, 'tile:-74,127', 'down spot keyed by the cause tile (R3-나)');
has(idxText, 'p:Luminous (호스트)', 'teammate keyed p:<name>, the role outside the key (R3-다, review 5)');
const mateKey = (idxText.match(/\n (p:\S+)/) || [])[1];
ok(mateKey === 'p:Luminous' && M.runQuery(ctx, { q: 'about', target: mateKey }).text.includes('함께 싸움 1'), `the index's teammate key as is finds the fights (${mateKey})`);
has(M.runQuery(ctx, { q: 'about', target: 'p:Luminous(호스트)' }).text, '함께 싸움 1', 'a key with the role glued on still resolves');
has(idxText, '싸움 1(그중 장소 추정 1)', 'biome fights count the estimated ones, marked (review 7)');
has(idxText, '게임 지식', 'game knowledge line');
// R3-나: near on the index's tile key finds that down
const nearKey = M.runQuery(ctx, { q: 'near', place: 'tile:-74,127' });
has(nearKey.text, '쓰러짐', 'near tile:<index key> finds the down there');

const about = M.runQuery(ctx, { q: 'about', target: 'o:3050' });
console.log(about.text.split('\n').map((l) => '    | ' + l).join('\n'));
has(about.text, '가장 가까운 적 1', 'about: nearest-enemy downs counted apart from near ones');
has(about.text, '(장소 추정)', 'old fight without a place is marked estimated');
has(about.text, '그때 조건 모름', 'old fight without conditions says unknown (R3-라)');
has(about.text, '그때 무기 Lv1(지금 2)', 'down line shows then vs now');
const aboutBrute = M.runQuery(ctx, { q: 'about', target: 'o:3180' });
has(aboutBrute.text, '가장 가까운 적 0', 'MushroomBrute was near, not nearest');

const goal = M.runQuery(ctx, { q: 'goal', key: 'g:gather:CopperOre' });
has(goal.text, '막힘(도구 부족)', 'goal: the blocked run with its cause');
has(goal.text, '그때 곡괭이 Lv1', 'goal: tool level then');
has(M.runQuery(ctx, { q: 'goal', key: 'gather:TinOre' }).text, '기록 없음', 'goal without runs: no record');
const emp = M.runQuery(ctx, { q: 'about', target: 'Slime' }).text;
ok(/area:Slime [^:]*: /.test(emp) && emp.includes('o:1630 슬라임'), 'an empty result keeps the key and the name (review 4)', emp);
has(M.runQuery(ctx, { q: 'about', target: 'o:4004' }).text, 'o:4004 용광로', 'open but not met: key and name with 기록 없음');

const near = M.runQuery(ctx, { q: 'near', place: 'here', radius: 32 });
has(near.text, '쓰러짐', 'near here: the down 5 tiles away');
has(near.text, '기억한 광석: 구리 광석(CopperOre) 2곳', 'near here: remembered ores inside the radius only');
const nearBase = M.runQuery(ctx, { q: 'near', place: 'base' });
has(nearBase.text, '기지를 정함', 'near base: the base event');
has(nearBase.text, '용광로', 'near base: the remembered furnace');
has(M.runQuery(ctx, { q: 'near', place: '-74,127' }).text, 'place는', 'near with a bare coordinate: usage');

const when = M.runQuery(ctx, { q: 'when', ago_min: [14, 6] });   // play 60..540
has(when.text, '쓰러짐', 'when [14,6] min ago includes the down at play 170');
hasNot(when.text, '기지를 정함', 'when [14,6] leaves out the base at play 700');
has(M.runQuery(ctx, { q: 'when', ago_min: '−90' }).text, '숫자 둘', 'when with a bad ago_min: usage');

const fol = M.runQuery(ctx, { q: 'follow', id: 'e:4' });
has(fol.text, 'r:s1.2', 'follow e:4 (the down) leads to its run');
const folRun = M.runQuery(ctx, { q: 'follow', id: 'r:s1.2', rel: 'down' });
has(folRun.text, '쓰러짐', 'follow r:s1.2 rel down');
has(M.runQuery(ctx, { q: 'follow', id: 'e:999' }).text, '사건 번호', 'follow a missing id: the id range');

const fnd = M.runQuery(ctx, { q: 'find', text: '동굴인' });
has(fnd.text, '관찰 아님', 'find: an LLM line is marked not an observation');
ok(/관찰 \d+, 전에 한 말 2/.test(fnd.text), `find: a dropped answer's why counts as said, not observed (review 6): ${fnd.text.split('\n')[0]}`);
ok(fnd.text.indexOf('관찰 아님') > fnd.text.indexOf('쓰러짐'), 'find: observations first, the LLM line after');

// names: Slime is a biome and an item; Larva a biome and an enemy (both shown)
const sl = M.resolveTarget(ctx, 'Slime');
ok(sl.keys.includes('area:Slime') && sl.keys.includes('o:1630'), `Slime -> biome and item (${sl.keys.join(', ')})`);
const la = M.resolveTarget(ctx, 'Larva');
ok(la.keys.includes('area:Larva') && la.keys.includes('o:3001'), `Larva -> biome and enemy (${la.keys.join(', ')})`);
ok(M.resolveTarget(ctx, '잠긴 바다').keys[0] === 'area:Sea', 'a biome by its Korean name');
ok(M.resolveTarget(ctx, 'tile:-1.5,3').err, 'a non-integer tile key is refused');

const gm = M.runQuery(ctx, { q: 'game', op: 'how', target: '구리 광석' });
has(gm.text, 'o:1500', 'game how on an open item');
has(M.runQuery(ctx, { q: 'game', op: 'bad', target: 'x' }).text, 'op는', 'game with a bad op: usage');
has(M.runQuery(ctx, { q: 'nope' }).text, '모르는 q', 'unknown q: the list');

const noGame = M.context({ root: ROOT, world: W, member: 'claude', game: null, tile: [-70, 122] });
has(M.memoryIndex(noGame), '게임 자료 없음', 'no game graph: the index head says so (review 8)');
// ---------------------------------------------------------------- 2. leaks
console.log('2. leaks');
const never = M.resolveTarget(ctx, 'OctarineOre');
ok(!never.keys.length, 'a never-seen object name does not resolve');
hasNot(never.err || '', '1522', 'its id is not shown');
const sug = M.resolveTarget(ctx, '구리');
ok(sug.keys.length === 1 || /비슷한 것/.test(sug.err || ''), 'a partial name resolves inside the allowed set');
const wrong = M.resolveTarget(ctx, 'Octarin');
hasNot(wrong.err || '', '팔색석', 'suggestions come only from open/own objects');
has(M.runQuery(ctx, { q: 'game', op: 'how', target: 'o:1522' }).text, '열리지 않음', 'game how on a closed o: key: not open, no name');
hasNot(M.runQuery(ctx, { q: 'game', op: 'how', target: 'o:1522' }).text, '팔색석', 'no name of the closed object');
hasNot(M.runQuery(ctx, { q: 'about', target: 'o:1522' }).text, '팔색석', 'about on a closed key does not leak');
const fo = M.runQuery(ctx, { q: 'follow', id: 'o:1500' });
hasNot(fo.text, '드롭 표', 'follow o: shows memory relations only');
// known.json missing: an empty view (never the developer's full view)
const d2 = charDir('gemini');
fs.writeFileSync(path.join(d2, 'events.jsonl'), ev.slice(0, 4).map((e) => JSON.stringify({ ...e, char: 'gemini' })).join('\n') + '\n');
const c2 = M.context({ root: ROOT, world: W, member: 'gemini', game });
ok(c2.view && c2.view.stations.size === 0 && !c2.view.open.has('o:3050'), `known.json missing -> empty view: no station, nothing seen (open ${c2.view?.open.size}: the hand recipes only)`);
has(M.runQuery(c2, { q: 'game', op: 'how', target: 'o:4004' }).text, '열리지 않음', 'empty view: furnace not open');
// known.json unreadable after a good read: the last view stays
fs.writeFileSync(path.join(d2, 'known.json'), JSON.stringify({ seenId: [4004], heldId: [] }));
const c3 = M.context({ root: ROOT, world: W, member: 'gemini', game });
fs.writeFileSync(path.join(d2, 'known.json'), '{"seenId":[40');   // being rewritten
const c4 = M.context({ root: ROOT, world: W, member: 'gemini', game, index: c3.idx });
ok(c4.view === c3.view && c4.view.open.has('o:4004'), 'known.json half-written -> the last view');
// another character's memory is not in this one's
hasNot(M.runQuery(c2, { q: 'find', text: '서쪽' }).text, '서쪽으로', "gemini does not see claude's records");

// ---------------------------------------------------------------- rotation and a half-written line
console.log('3. rotation, appends, half-written line');
const d3 = charDir('grok');
const mk = (i) => JSON.stringify(E('fight', 1000 + i, { ents: ['enemy:Caveling', 'cell:1,1'], d: { secs: 3, enemies: { Caveling: 1 }, with: [], downs: 0, c: C() } }));
fs.writeFileSync(path.join(d3, 'events.jsonl'), [mk(1), mk(2)].join('\n') + '\n' + mk(3).slice(0, 40));
const g3 = M.context({ root: ROOT, world: W, member: 'grok', game });
ok(g3.idx.ev.length === 2, `half-written last line waits (2 events, got ${g3.idx.ev.length})`);
fs.appendFileSync(path.join(d3, 'events.jsonl'), mk(3).slice(40) + '\n' + mk(4) + '\n');
M.context({ root: ROOT, world: W, member: 'grok', game, index: g3.idx });
ok(g3.idx.ev.length === 4, `appended lines are read from the last offset (4, got ${g3.idx.ev.length})`);
const rb = g3.idx.rebuilds;
fs.renameSync(path.join(d3, 'events.jsonl'), path.join(d3, 'events-20261104-1.jsonl'));   // rotation: events.jsonl gone for a moment
M.context({ root: ROOT, world: W, member: 'grok', game, index: g3.idx });
ok(g3.idx.ev.length === 4 && g3.idx.rebuilds === rb + 1, `rotation rebuilds once, nothing lost (${g3.idx.ev.length})`);
fs.writeFileSync(path.join(d3, 'events.jsonl'), mk(5) + '\n');
M.context({ root: ROOT, world: W, member: 'grok', game, index: g3.idx });
ok(g3.idx.ev.length === 5, `new events.jsonl after the rotation (5, got ${g3.idx.ev.length})`);

// ---------------------------------------------------------------- 4. 100 hours synthetic
console.log('4. 100-hour synthetic (size, speed)');
const d4 = charDir('big');
const enemies = ['Caveling', 'MushroomBrute', 'Larva', 'CavelingShaman', 'SlimeBlob', 'MushroomEnemy', 'CavelingSpearman', 'CavelingSkirmisher'];
const areas = ['Slime', 'Larva', 'Stone', 'Nature', 'Sea', 'Desert'];
const lines = [];
let id = 0, run = '';
const N = 21000;
for (let i = 0; i < N; i++) {
  const play = Math.round((i / N) * 100 * 3600);
  const cx = Math.floor(Math.sin(i / 300) * 40), cy = Math.floor(Math.cos(i / 300) * 40), area = areas[Math.floor(i / 3500) % areas.length];
  const c = C({ wlv: 1 + Math.floor(i / 3500), alv: Math.floor(i / 4000), hp: 100 + Math.floor(i / 300), cell: `${cx},${cy}`, area });
  const r = i % 20;
  let e;
  if (r === 0) { run = `s.${id + 1}`; e = { kind: 'goal_start', run, ents: [`item:CopperOre`, `cell:${cx},${cy}`, `area:${area}`], d: { key: 'gather:CopperOre', count: 5, source: 'llm', c } }; }
  else if (r === 19) e = { kind: 'goal_end', run, ents: [`cell:${cx},${cy}`], d: { run, key: ['gather:CopperOre', 'gather:TinOre', 'explore', 'craft:CopperBar'][i % 4], item: 'CopperOre', count: 5, outcome: i % 3 ? 'completed' : 'blocked', cls: i % 3 ? '' : 'no_source', cause: '', got: 3, secs: 300, downs: 0, down_by: [], area, source: 'llm', c0: c, c1: c, play0: play - 300, play1: play } };
  else if (r === 7) e = { kind: 'down', run, ents: [`enemy:${enemies[i % 8]}`, `cell:${cx},${cy}`, `area:${area}`], d: { cause: `x (${cx * 16 + 3}.5,${cy * 16 + 4}.1) y`, enemies: [enemies[i % 8]], c } };
  else if (r % 3 === 0) e = { kind: 'fight', run, ents: [`enemy:${enemies[i % 8]}`, `enemy:${enemies[(i + 3) % 8]}`, `cell:${cx},${cy}`, `area:${area}`, 'player:Luminous'], d: { secs: 10, enemies: { [enemies[i % 8]]: 2 }, with: ['Luminous(호스트)'], downs: 0, boss: '', c } };
  else if (r % 5 === 1) e = { kind: 'plan_result', run, ents: [], src: 'llm', d: { result: 'accepted', report: i, trigger: 'goal_done', decision: 'set', goal: 'gather CopperOre 5', why: '구리를 더 모은다', say: '', reason: '', secs: 5 } };
  else e = { kind: 'item_first', run, ents: [`item:${['Wood', 'Slime', 'CopperOre', 'TinOre'][i % 4]}`], d: { item: 'Wood', count: 1 } };
  lines.push(JSON.stringify({ id: ++id, sess: 's', t: 1793770000 + play, play, world: W, char: 'big', src: 'game', ...e }));
}
fs.writeFileSync(path.join(d4, 'events.jsonl'), lines.join('\n') + '\n');
fs.writeFileSync(path.join(d4, 'exp_state.json'), JSON.stringify({ play: 100 * 3600 }));
const mb = fs.statSync(path.join(d4, 'events.jsonl')).size / 1e6;
global.gc?.(); const heap0 = process.memoryUsage().heapUsed;
let t = performance.now();
const big = M.context({ root: ROOT, world: W, member: 'big', game, tile: [10, 10] });
const build = performance.now() - t;
global.gc?.(); const heap = (process.memoryUsage().heapUsed - heap0) / 1e6;
t = performance.now();
fs.appendFileSync(path.join(d4, 'events.jsonl'), JSON.stringify({ id: ++id, sess: 's', t: 1, play: 360000, world: W, char: 'big', kind: 'fight', run: null, ents: ['enemy:Caveling'], src: 'game', d: { secs: 3, enemies: { Caveling: 1 }, with: [], downs: 0 } }) + '\n');
M.context({ root: ROOT, world: W, member: 'big', game, index: big.idx, tile: [10, 10] });
const append = performance.now() - t;
const qs = [{ q: 'about', target: 'o:3050' }, { q: 'goal', key: 'g:gather:CopperOre' }, { q: 'near', place: 'here' }, { q: 'when', ago_min: [120, 60] }, { q: 'follow', id: 'o:3050', rel: 'down' }, { q: 'find', text: '구리' }, { q: 'game', op: 'uses', target: 'o:1500' }];
const times = [];
for (let k = 0; k < 20; k++) for (const q of qs) { t = performance.now(); M.runQuery(big, q); times.push(performance.now() - t); }
times.sort((a, b) => a - b);
t = performance.now();
const bigIdx = M.memoryIndex(big);
const idxMs = performance.now() - t;
const sizes = qs.map((q) => M.runQueries(big, [q]).text.length);
console.log(`    events ${N + 1} (${mb.toFixed(1)} MB): build ${build.toFixed(0)} ms, heap +${heap.toFixed(0)} MB, append ${append.toFixed(1)} ms, query p50 ${times[times.length >> 1].toFixed(2)} ms p95 ${times[Math.floor(times.length * 0.95)].toFixed(2)} ms max ${times.at(-1).toFixed(1)} ms, index ${idxMs.toFixed(1)} ms ${bigIdx.length} chars, one-query results ${Math.min(...sizes)}-${Math.max(...sizes)} chars`);
ok(build < 1500, 'build under 1.5 s');
ok(times[Math.floor(times.length * 0.95)] < 100, 'query p95 under 100 ms');
ok(bigIdx.length <= M.LIMITS.index, 'index stays within its limit at 100 h');
ok(Math.max(...sizes) <= M.LIMITS.search, 'one search result within the search limit');
const three = M.runQueries(big, qs.slice(0, 4));
ok(three.text.length <= M.LIMITS.search && three.log.length === 3 && /앞의 3개만/.test(three.text), `4 queries -> 3 run, ${three.text.length} chars`);

// ---------------------------------------------------------------- prefetch freezing (R2-2)
console.log('5. prefetch freezing');
const fz = M.freezeQueries([{ q: 'near', place: 'here' }, { q: 'when', ago_min: [30, 0] }, { q: 'about', target: 'o:3050' }, { q: 'x' }], { tile: [5, 6], play: 7200 });
ok(fz.length === 3 && fz[0].place === 'tile:5,6' && fz[1].play[0] === 5400 && fz[1].play[1] === 7200 && !fz[1].ago_min, 'here -> the tile then; ago_min -> absolute play range; 3 at most');
const later = { ...ctx, play: 99999 };
has(M.runQuery(later, fz[1]).text, '사건 0개', 'a frozen when keeps its range when run later');

fs.rmSync(ROOT, { recursive: true, force: true });
console.log(fails ? `${fails}/${checks} FAILED` : `ALL ${checks} CHECKS OK`);
process.exit(fails ? 1 : 0);
