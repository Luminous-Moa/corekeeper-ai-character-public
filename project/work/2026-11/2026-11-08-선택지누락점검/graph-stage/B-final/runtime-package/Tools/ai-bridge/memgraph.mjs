// memgraph.mjs — one AI character's memory graph (an index rebuilt from its own event log), the low-resolution index
// ("memory_index") the upper LLM always sees, and the seven queries it can send through the bridge (recall).
// Design: work/2026-11/2026-11-04-기억-그래프-검색/1-설계.md (session 코어키퍼-AI-캐릭터), revision 2 + re-check R2/R3.
//
// - The source is the character's events*.jsonl only (written by the mod, AIExpCore.cs). Nothing here writes there.
// - Object nodes use the game knowledge graph's keys (o:<ObjectID number>); names not in the game data stay n:<name>.
// - What the character may see of the game graph is its own known.json (kg.mjs openView). The view is never null here
//   (null would be the developer's full view): missing/unreadable known.json -> the last view, else an empty one.
// - Coordinates shown to and taken from the LLM are game tiles (the report's). 16-tile cells stay inside.
//
// Developer command line:
//   node memgraph.mjs index|about|goal|near|when|follow|game|find [args] --member claude [--world <id>] [--root <AIPlayers>]
//   e.g. node memgraph.mjs about o:3050 --member claude      node memgraph.mjs near here --tile -70,120 --member claude
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import * as kg from '../knowledge/kg.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const AIPLAYERS = path.join(os.homedir(), 'AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers');
const readJson = (f) => JSON.parse(fs.readFileSync(f, 'utf8').replace(/^\uFEFF/, ''));

// Korean names for the biomes (the game graph's b: nodes have no ko). Written here, not from the game's text data.
export const BIOME_KO = {
  Slime: '지하(슬라임 지대)', Larva: '점토 동굴(애벌레 지대)', Stone: '잊힌 유적(돌 지대)', Obsidian: '흑요석 지대', Nature: '야생(자연 지대)',
  GreatWall: '큰 벽', Sea: '잠긴 바다', Desert: '사막', Crystal: '수정 지대', Passage: '통로', Excavation: '발굴지',
};

// Importance of an event kind for the index and the order of results (design 4.1: downs first).
const IMP = { down: 10, boss_kill: 9, boss_seen: 7, fight: 4, goal_end: 3, area_enter: 2, base_set: 5, item_first: 2, plan_result: 1, goal_start: 1, goal_continue: 0.5, back_up: 0.3, session_start: 0 };
const HALF_LIFE = 3 * 3600;   // play seconds
// Compatibility exports for callers. Information is not silently cut to a character/item budget.
export const LIMITS = { index: Infinity, prefetch: Infinity, query: Infinity, search: Infinity };
export const CAPS = { enemies: Infinity, places: Infinity, mates: Infinity };

// the mod's tile: Unity.Mathematics math.round (to even on .5), AIBody.TileOf
const roundEven = (v) => { const f = Math.floor(v), d = v - f; return d > 0.5 ? f + 1 : d < 0.5 ? f : f % 2 === 0 ? f : f + 1; };
const cellOf = (x, y) => [Math.floor(x / 16), Math.floor(y / 16)];
const cellCenter = (cx, cy) => [cx * 16 + 8, cy * 16 + 8];
// distance from a tile to the nearest tile of a 16-tile cell (0 inside)
const distToCell = (x, y, cx, cy) => {
  const dx = Math.max(cx * 16 - x, 0, x - (cx * 16 + 15)), dy = Math.max(cy * 16 - y, 0, y - (cy * 16 + 15));
  return Math.hypot(dx, dy);
};

export function ago(now, play) {
  const s = Math.max(0, now - play);
  if (s < 90) return '방금';
  if (s < 5400) return `${Math.round(s / 60)}분 전`;
  return `${(s / 3600).toFixed(1)}시간 전`;
}

// ---------------------------------------------------------------- game data (optional)

// The graph built in this checkout (node Tools/knowledge/build.mjs --dump <knowledge-dump.json>; git-ignored).
export const DEFAULT_KG = path.resolve(HERE, '..', 'knowledge', 'data');
export function kgCandidates(extra) {
  return [extra, process.env.KG_DATA, DEFAULT_KG].filter(Boolean);
}
// The first candidate with a graph: {game, dir} (game null when none, dir = the places looked at).
export function findGame(extra) {
  const c = kgCandidates(extra);
  for (const d of c) { const g = fs.existsSync(path.join(d, 'current.txt')) ? loadGame(d) : null; if (g) return { game: g, dir: d, looked: c }; }
  return { game: null, dir: null, looked: c };
}

export function loadGame(kgData) {
  try {
    const g = kg.loadGraph(kgData);
    g.byName = new Map();
    for (const [k, n] of Object.entries(g.nodes)) if (k.startsWith('o:') && n.name && !g.byName.has(n.name)) g.byName.set(n.name, k);
    g.biome = new Map();
    for (const [k, n] of Object.entries(g.nodes)) if (k.startsWith('b:') && n.name && n.name !== 'None' && !n.name.startsWith('__')) g.biome.set(n.name, k);
    return g;
  } catch { return null; }
}

// ---------------------------------------------------------------- the index

// One character in one world: events*.jsonl under dir. refresh() reads only what was appended to events.jsonl since
// the last call; a rotation (the list of events-*.jsonl changed), a shorter file or a missing one rebuilds everything.
export class MemIndex {
  constructor(dir, game) { this.dir = dir; this.game = game; this.reset(); }

  reset() {
    this.ev = []; this.ids = new Set(); this.byEnt = new Map(); this.byRun = new Map(); this.runs = new Map();
    this.byKey = new Map(); this.lastCond = null; this.lastCondPlay = -1; this.offset = 0; this.rotSig = ''; this.partial = Buffer.alloc(0);
    this.maxPlay = 0; this.rebuilds = (this.rebuilds || 0) + 1;
  }

  objKey(name) {
    if (!name) return null;
    return this.game?.byName.get(name) || `n:${name}`;
  }

  refresh() {
    let rot = [];
    try { rot = fs.readdirSync(this.dir).filter((f) => /^events-.*\.jsonl$/.test(f)).sort(); } catch { return this; }
    const sig = rot.join('|');
    const main = path.join(this.dir, 'events.jsonl');
    let size = -1;
    try { size = fs.statSync(main).size; } catch { size = -1; }
    if (size < 0 && rot.length && sig !== this.rotSig) { /* rotating right now: the next call sees the new file */ }
    if (sig !== this.rotSig || (size >= 0 && size < this.offset)) {
      this.reset();
      this.rotSig = sig;
      for (const f of rot) this.addText(fs.readFileSync(path.join(this.dir, f), 'utf8'), true);
    }
    if (size > this.offset) {
      const fd = fs.openSync(main, 'r');
      try {
        const buf = Buffer.alloc(size - this.offset);
        fs.readSync(fd, buf, 0, buf.length, this.offset);
        this.offset = size;
        const all = Buffer.concat([this.partial, buf]);
        const nl = all.lastIndexOf(0x0a);
        this.partial = nl < 0 ? all : all.subarray(nl + 1);
        if (nl >= 0) this.addText(all.subarray(0, nl + 1).toString('utf8'), false);
      } finally { fs.closeSync(fd); }
    }
    return this;
  }

  addText(text, whole) {
    const lines = text.split('\n');
    const last = lines.pop();   // after the final \n: "" or a line not fully written yet
    if (whole && last.trim()) lines.push(last);   // appended text always ends at a \n (the rest waits as bytes)
    for (const l of lines) {
      if (!l.trim()) continue;
      let e; try { e = JSON.parse(l); } catch { continue; }
      this.add(e);
    }
  }

  add(e) {
    if (e == null || typeof e.id !== 'number' || this.ids.has(e.id)) return;
    this.ids.add(e.id);
    const d = e.d || {};
    const r = { id: e.id, kind: e.kind, play: +e.play || 0, run: e.run || null, src: e.src || 'game', keys: [], cell: null, tile: null, area: '', c: null, est: false };
    for (const x of e.ents || []) {
      const i = x.indexOf(':'); const t = x.slice(0, i), v = x.slice(i + 1);
      if (t === 'enemy' || t === 'item') r.keys.push(this.objKey(v));
      else if (t === 'area') { r.area = v; r.keys.push(`area:${v}`); }
      else if (t === 'cell') { const [a, b] = v.split(',').map(Number); if (Number.isFinite(a) && Number.isFinite(b)) r.cell = [a, b]; }
      else if (t === 'player') r.keys.push(`p:${v}`);
    }
    const c = e.kind === 'goal_end' ? (d.c1 && (d.c1.tlv >= 0 || d.c1.hp > 0) ? d.c1 : d.c0) : d.c;
    if (c && typeof c === 'object') r.c = { hp: c.hp, t: c.tlv, w: c.wlv, a: c.alv, wpn: c.wpn || '', tool: c.tool || '' };
    if (c?.cell && !r.cell) { const [a, b] = String(c.cell).split(',').map(Number); if (Number.isFinite(a)) r.cell = [a, b]; }
    if (c?.area && !r.area) r.area = c.area;
    // exact tile: a down's cause "(x,z)" or a base's tile
    const m = typeof d.cause === 'string' && d.cause.match(/\((-?[\d.]+),(-?[\d.]+)\)/);
    if (e.kind === 'down' && m) r.tile = [roundEven(+m[1]), roundEven(+m[2])];
    if (Array.isArray(d.tile)) r.tile = [d.tile[0], d.tile[1]];
    if (r.tile && !r.cell) r.cell = cellOf(r.tile[0], r.tile[1]);
    // what it was, kept small (the whole d is not kept: 100 h of four characters)
    switch (e.kind) {
      case 'down': r.enemies = (d.enemies || []).map((n) => this.objKey(n)); r.cause = String(d.cause || ''); break;
      case 'fight': r.enemies = Object.entries(d.enemies || {}).map(([n, k]) => [this.objKey(n), k]); r.with = d.with || []; r.downs = d.downs | 0; r.secs = +d.secs || 0; r.boss = d.boss || ''; r.damage = d.damage; r.endHp = d.end_hp; r.maxHp = d.max_hp; r.fell = d.fell || null; break;
      case 'problem': r.text = String(d.text || ''); break;   // the upper LLM's problem report (자연어 명령)
      case 'order': r.text = String(d.text || ''); r.was = d.was || ''; r.now = d.now || ''; r.why = String(d.why || ''); break;   // the host instruction changed (검수 작은 지적 4)
      case 'boss_seen': case 'boss_kill': r.boss = this.objKey(d.boss); r.with = d.with || []; break;
      case 'area_enter': r.first = !!d.first; r.with = d.with || []; break;
      case 'item_first': r.item = this.objKey(d.item); break;
      case 'base_set': r.by = d.by || ''; r.with = d.with || []; break;
      case 'plan_result': r.result = d.result; r.decision = d.decision; r.goal = d.goal; r.why = d.why || ''; r.say = d.say || ''; r.reason = d.reason || ''; break;
      case 'goal_start': case 'goal_continue': r.key = d.key; r.count = d.count; r.source = d.source; break;
      case 'goal_end': {
        r.key = d.key;
        this.runs.set(d.run, { run: d.run, key: d.key, item: d.item, outcome: d.outcome, cls: d.cls || '', cause: String(d.cause || ''), got: d.got, count: d.count, secs: +d.secs || 0, downs: d.downs | 0,
          downBy: (d.down_by || []).map((n) => this.objKey(n)), area: d.area || '', source: d.source || '', c: r.c, c0: d.c0 ? { t: d.c0.tlv, w: d.c0.wlv, a: d.c0.alv, hp: d.c0.hp } : null, play0: +d.play0 || 0, play1: +d.play1 || r.play, ev: e.id });
        if (!this.byKey.has(d.key)) this.byKey.set(d.key, []);
        this.byKey.get(d.key).push(d.run);
        break;
      }
      default: break;
    }
    if (r.c && r.play >= this.lastCondPlay) { this.lastCond = r.c; this.lastCondPlay = r.play; }
    if (r.play > this.maxPlay) this.maxPlay = r.play;
    const idx = this.ev.length;
    this.ev.push(r);
    for (const k of new Set(r.keys)) { if (!this.byEnt.has(k)) this.byEnt.set(k, []); this.byEnt.get(k).push(idx); }
    if (r.run) { if (!this.byRun.has(r.run)) this.byRun.set(r.run, []); this.byRun.get(r.run).push(idx); }
  }

  // A place for events without one (old fights, boss_seen, item_first): the nearest event of the same run that has a
  // cell. Marked as estimated.
  placeOf(r) {
    if (r.cell) return { cell: r.cell, tile: r.tile, area: r.area, est: false };
    if (!r.run) return null;
    const l = this.byRun.get(r.run) || [];
    let best = null, bd = Infinity;
    for (const i of l) { const o = this.ev[i]; const dd = Math.abs(o.id - r.id) - (o.tile ? 0.5 : 0); if (o.cell && o !== r && dd < bd) { bd = dd; best = o; } }   // nearest by id, an exact tile wins a tie
    return best ? { cell: best.cell, tile: best.tile, area: best.area, est: true } : null;
  }

  areaOf(r) {
    if (r.area) return { area: r.area, est: false };
    const p = this.placeOf(r);
    return p?.area ? { area: p.area, est: true } : null;
  }

  objectsInMemory() {
    const s = new Set();
    for (const k of this.byEnt.keys()) if (k.startsWith('o:') || k.startsWith('n:')) s.add(k);
    return s;
  }
}

// ---------------------------------------------------------------- one character's context

// Everything one request needs: the index, the open view, the play time and tile now, memory.json (base, ores,
// stations). viewCache keeps the last good view per character (never null).
const viewCache = new Map();
const EMPTY_KNOWN = { seenId: [], heldId: [], tiles: [], dropSrc: [], dropItem: [] };

export function context({ root = AIPLAYERS, world, member, game, index, tile = null }) {
  const dir = path.join(root, world, member);
  const idx = index || new MemIndex(dir, game);
  idx.game = game; idx.refresh();
  let play = idx.maxPlay;
  try { const st = readJson(path.join(dir, 'exp_state.json')); if (typeof st.play === 'number') play = Math.max(play, st.play); } catch { /* keep the log's */ }
  const ck = `${world}/${member}`;
  let view = viewCache.get(ck) || null, viewNote = '';
  if (game) {
    try { view = kg.openView(game, readJson(path.join(dir, 'known.json'))); viewCache.set(ck, view); }
    catch { if (!view) { view = kg.openView(game, EMPTY_KNOWN); viewNote = 'known.json 없음: 열린 게임 지식 없음'; } else viewNote = 'known.json을 못 읽어 직전 것을 씀'; }
  }
  let mem = null;
  try { mem = readJson(path.join(dir, 'memory.json')); } catch { mem = null; }
  return { dir, world, member, idx, game, view, viewNote, play, tile, mem, cond: idx.lastCond };
}

// ---------------------------------------------------------------- names

const lc = (s) => String(s || '').toLowerCase();

// Allowed objects for names and suggestions: open in the view ∪ in this character's own memory.
function allowed(ctx) {
  const s = ctx.idx.objectsInMemory();
  if (ctx.view) for (const k of ctx.view.open) s.add(k);
  return s;
}

export function objName(ctx, k) {
  if (k.startsWith('n:')) return k.slice(2);
  const n = ctx.game?.nodes[k];
  if (!n) return k;
  return n.ko ? `${n.ko}(${n.name})` : n.name;
}
const areaName = (a) => (BIOME_KO[a] ? `${BIOME_KO[a]}·${a}` : a);
const keyName = (ctx, k) => (k.startsWith('o:') || k.startsWith('n:') ? objName(ctx, k) : k.startsWith('area:') ? areaName(k.slice(5)) : k.startsWith('p:') ? k.slice(2) : k);

// A target string -> one or more keys. Keys pass through (o:, n:, area:, b:, tile:, p:/player:, g:, e:, r:). Names are
// looked up in the allowed objects and the biomes; a name that is both (Slime, Larva) gives both.
export function resolveTarget(ctx, q) {
  q = String(q ?? '').trim();
  if (!q) return { keys: [], err: '대상이 비었음' };
  if (/^o:\d+$/.test(q)) return allowed(ctx).has(q) ? { keys: [q] } : { keys: [], err: `${q}는 이 캐릭터가 본 적 없거나 기록에 없음` };
  if (/^n:/.test(q)) return { keys: [q] };
  if (/^area:/.test(q)) return { keys: [`area:${q.slice(5)}`] };
  if (/^b:\d+$/.test(q) && ctx.game?.nodes[q]) return { keys: [`area:${ctx.game.nodes[q].name}`] };
  if (/^(p|player):/.test(q)) return { keys: [`p:${q.replace(/^(p|player):/, '').replace(/\s*\([^)]*\)\s*$/, '').trim()}`] };
  if (/^tile:-?\d+,-?\d+$/.test(q) || /^(g|e|r):/.test(q)) return { keys: [q] };
  if (/^(tile|cell):/.test(q)) return { keys: [], err: `좌표는 tile:x,y(정수 타일 좌표) 꼴로` };
  const ql = lc(q);
  const keys = [];
  for (const [a, ko] of Object.entries(BIOME_KO)) if (lc(a) === ql || lc(ko) === ql || ko.split(/[()·]/).some((x) => x && lc(x) === ql)) keys.push(`area:${a}`);
  if (ctx.idx.byEnt.has(`area:${q}`) && !keys.includes(`area:${q}`)) keys.push(`area:${q}`);
  const al = allowed(ctx);
  const names = (k) => (k.startsWith('n:') ? [k.slice(2)] : (() => { const n = ctx.game?.nodes[k]; return n ? [n.name, n.ko, n.en] : []; })());
  const exact = [...al].filter((k) => names(k).some((x) => x && lc(x) === ql));
  if (exact.length) keys.push(...exact);
  for (const p of ctx.idx.byEnt.keys()) if (p.startsWith('p:') && lc(p.slice(2)) === ql) keys.push(p);
  if (keys.length) return { keys: [...new Set(keys)] };
  const part = [...al].filter((k) => names(k).some((x) => x && lc(x).includes(ql)));
  if (part.length === 1) return { keys: part };
  const sug = (part.length ? part : [...al].filter((k) => names(k).some((x) => x && lc(x).includes(ql))));
  return { keys: [], err: `'${q}'를 이 캐릭터의 기억·본 것에서 찾지 못함${sug.length ? `. 비슷한 것: ${sug.map((k) => `${k} ${keyName(ctx, k)}`).join(', ')}` : ''}` };
}

// ---------------------------------------------------------------- lines

const lv = (c) => (c ? `무기 Lv${c.w ?? '?'}·방어구 Lv${c.a ?? '?'}·최대 체력 ${c.hp ?? '?'}` : '모름');
function diff(then, now) {
  if (!then) return '그때 조건 모름';
  const r = [];
  const p = (lab, a, b) => (a != null && a >= 0 && b != null && b >= 0 && a !== b ? `${lab} ${a}(지금 ${b})` : a != null && a >= 0 ? `${lab} ${a}` : null);
  const w = p('무기 Lv', then.w, now?.w), a = p('방어구 Lv', then.a, now?.a);
  if (w) r.push(w.replace('Lv ', 'Lv')); if (a) r.push(a.replace('Lv ', 'Lv'));
  if (then.hp > 0) r.push(now?.hp > 0 && Math.abs(now.hp - then.hp) >= Math.max(10, then.hp / 10) ? `최대 체력 ${then.hp}(지금 ${now.hp})` : `최대 체력 ${then.hp}`);
  return r.length ? `그때 ${r.join('·')}` : '그때 조건 모름';
}
function diffShort(then, now) {
  if (!then) return '그때 조건 모름';
  const r = [];
  if (then.w >= 0 && now?.w >= 0 && then.w !== now.w) r.push(`무기 Lv${then.w}(지금 ${now.w})`);
  if (then.a >= 0 && now?.a >= 0 && then.a !== now.a) r.push(`방어구 Lv${then.a}(지금 ${now.a})`);
  if (then.hp > 0 && now?.hp > 0 && Math.abs(now.hp - then.hp) >= Math.max(10, then.hp / 10)) r.push(`최대 체력 ${then.hp}(지금 ${now.hp})`);
  return r.length ? `그때 ${r.join('·')}` : '그때 장비 지금과 같음';
}
const tileText = (pl) => (pl ? `${pl.est ? '(장소 추정) ' : ''}타일 ${pl.tile ? pl.tile.join(',') : cellCenter(...pl.cell).join(',') + '(16칸 묶음 가운데)'}` : '장소 모름');

function evLine(ctx, r) {
  const now = ctx.play, pl = ctx.idx.placeOf(r);
  const head = `[e:${r.id}] ${ago(now, r.play)}`;
  const inRun = r.run && ctx.idx.runs.get(r.run) ? ` · ${ctx.idx.runs.get(r.run).key} 중` : r.run ? ' · 목표 중' : '';
  switch (r.kind) {
    case 'down': {
      const [n0, ...rest] = r.enemies || [];
      return `${head} 쓰러짐(${n0 ? `가장 가까움: ${keyName(ctx, n0)} ${n0}` : '원인 모름'}${rest.length ? `, 근처 ${rest.map((k) => keyName(ctx, k)).join('·')}` : ''}) ${tileText(pl)} ${r.area}${inRun} · ${diff(r.c, ctx.cond)}`;
    }
    case 'fight':
      return `${head} 싸움 ${Math.round(r.secs)}초(${(r.enemies || []).map(([k, n]) => `${keyName(ctx, k)}×${n}`).join(', ')}), 쓰러짐 ${r.downs}${r.with?.length ? `, 함께 ${r.with.join('·')}` : ''}${r.boss ? `, 보스 ${r.boss}` : ''}${r.damage != null ? `, 받은 피해 ${r.damage}, 끝 체력 ${r.endHp}/${r.maxHp}` : ''}${r.fell && Object.keys(r.fell).length ? `, 쓰러진 적 ${Object.entries(r.fell).map(([k, n]) => `${k}×${n}`).join(', ')}(누가 쳤는지 모름)` : ''} ${tileText(pl)}${inRun} · ${diff(r.c, ctx.cond)}`;
    case 'problem': return `${head} 문제 보고: "${r.text}" (전에 ${ctx.member}가 한 말, 관찰 아님)`;
    case 'order': return `${head} 호스트 지시 "${r.text}" ${r.was}→${r.now}: "${r.why}" (전에 ${ctx.member}가 한 말, 관찰 아님)`;
    case 'boss_seen': return `${head} 보스를 봄 ${keyName(ctx, r.boss)} ${r.boss} ${tileText(pl)}`;
    case 'boss_kill': return `${head} 보스가 쓰러지는 것을 봄 ${keyName(ctx, r.boss)}${r.with?.length ? `(함께 ${r.with.join('·')})` : ''}`;
    case 'area_enter': return `${head} 지역 들어감 ${areaName(r.area)}${r.first ? '(처음)' : ''}${r.with?.length ? ` 함께 ${r.with.join('·')}` : ''}`;
    case 'item_first': return `${head} 처음 얻음 ${keyName(ctx, r.item)} ${r.item}`;
    case 'base_set': return `${head} 기지를 정함(${r.by}) ${tileText(pl)}`;
    case 'goal_start': return `${head} 목표 시작 ${r.key}×${r.count}(${r.source})`;
    case 'goal_continue': return `${head} 같은 목표를 다시 받음 ${r.key}`;
    case 'goal_end': { const u = ctx.idx.runs.get(r.run); return `${head} ${runLine(ctx, u)}`; }
    case 'plan_result': return `${head} 판단 ${r.result}: ${r.decision} ${r.goal}${r.why || r.say ? ` — "${r.why || r.say}" (전에 ${ctx.member}가 한 말, 관찰 아님)` : ''}${r.reason ? ` (버린 까닭: ${r.reason})` : ''}`;
    case 'back_up': return `${head} 코어에서 다시 일어남`;
    default: return `${head} ${r.kind}`;
  }
}

const OUT_KO = { completed: '완료', blocked: '막힘', exhausted: '탐험할 곳 없음', cancelled: '취소', replaced: '다른 목표로 바뀜', system: '실행 장애', unknown: '결과 모름' };
const CLS_KO = { tool_weak: '도구 부족', no_source: '모을 곳 못 찾음', cannot_craft: '만들 방법 없음', only_wait: '할 행동 없음', no_place: '갈 곳·넣을 곳 없음' };
function runLine(ctx, u) {
  if (!u) return '(목표 기록 없음)';
  const out = OUT_KO[u.outcome] || u.outcome;
  const tool = u.c && u.c.t != null ? ` · 그때 곡괭이 Lv${u.c.t}${ctx.cond?.t != null && ctx.cond.t !== u.c.t ? `(지금 ${ctx.cond.t})` : ''}` : '';
  return `[r:${u.run}] ${u.key}${u.count ? `×${u.count}` : ''} → ${out}${u.cls ? `(${CLS_KO[u.cls] || u.cls})` : ''}${u.got >= 0 ? ` ${u.got}/${u.count}` : ''}, ${Math.round(u.secs / 60)}분${u.downs ? `, 쓰러짐 ${u.downs}(${u.downBy.map((k) => keyName(ctx, k)).join('·')})` : ''}${u.area ? `, ${u.area}` : ''}${u.source ? `, ${u.source}` : ''}${tool}`;
}

const score = (ctx, r) => (IMP[r.kind] ?? 1) * (r.kind === 'fight' && r.downs ? 2 : 1) * Math.pow(0.5, Math.max(0, ctx.play - r.play) / HALF_LIFE);

// Keep legacy parameters for callers, but preserve every information line.
function cap(lines, limit, more) { return lines.join('\n'); }

export function header(ctx) {
  return `지금: ${lv(ctx.cond)}${ctx.cond?.t != null ? `·곡괭이 Lv${ctx.cond.t}` : ''}${ctx.tile ? `, 타일 ${ctx.tile.join(',')}` : ''}, 플레이 ${(ctx.play / 3600).toFixed(1)}시간 / 범위: 이 캐릭터의 전 기간 기록 ${ctx.idx.ev.length}개 / ${ctx.game ? '게임 자료 있음' : '게임 자료 없음(이름은 ObjectID 이름만)'}${ctx.viewNote ? ` / ${ctx.viewNote}` : ''}`;
}

// ---------------------------------------------------------------- the index the LLM always sees

export function memoryIndex(ctx, limit = LIMITS.index) {
  const idx = ctx.idx, now = ctx.play;
  const enemies = new Map(), places = new Map(), mates = new Map();
  let noAreaFights = 0;
  const bump = (m, k, r, f) => { let o = m.get(k); if (!o) m.set(k, o = { k, s: 0, last: -1, lastKind: '', fights: 0, downNear: 0, downNearest: 0, together: 0, cAt: -1, c: null, cKind: '', n: 0 }); o.s += score(ctx, r); o.n++; if (r.play >= o.last) { o.last = r.play; o.lastKind = r.kind; } if (r.c && r.play >= o.cAt) { o.cAt = r.play; o.c = r.c; o.cKind = r.kind; } f(o); };
  // conditions of an enemy row: the latest of its events that has them (old fights have none; nothing is borrowed silently)
  const condOf = (o) => (!o.c ? '그때 조건 모름' : o.cAt === o.last ? diffShort(o.c, ctx.cond) : `${ago(now, o.cAt)} ${o.cKind === 'down' ? '쓰러짐' : o.cKind} ${diffShort(o.c, ctx.cond)}`);
  for (const r of idx.ev) {
    if (r.kind === 'fight') {
      for (const [k] of r.enemies || []) bump(enemies, k, r, (o) => o.fights++);
      for (const w of r.with || []) bump(mates, `p:${w.split('(')[0]}`, r, (o) => { o.together++; o.role = w.includes('(AI)') ? 'AI' : w.includes('호스트') ? '호스트' : ''; });
    } else if (r.kind === 'down') {
      (r.enemies || []).forEach((k, i) => bump(enemies, k, r, (o) => { o.downNear++; if (i === 0) o.downNearest++; }));
      const pl = idx.placeOf(r);
      if (pl) {
        const ck = pl.cell.join(',');
        bump(places, `cell:${ck}`, r, (o) => { o.downs = (o.downs || 0) + 1; o.tile = pl.tile || o.tile || cellCenter(...pl.cell); o.by = (r.enemies || [])[0]; o.area = r.area; });
      }
      if (r.area) bump(places, `area:${r.area}`, r, (o) => (o.downs = (o.downs || 0) + 1));
    } else if (r.kind === 'boss_seen' || r.kind === 'boss_kill') {
      if (r.boss) bump(enemies, r.boss, r, (o) => { o.boss = true; });
    }
    if (r.kind === 'fight') { const a = idx.areaOf(r); if (a) bump(places, `area:${a.area}`, r, (o) => { o.fights = (o.fights || 0) + 1; if (a.est) o.fightsEst = (o.fightsEst || 0) + 1; }); else noAreaFights++; }
    else if (r.kind === 'area_enter' && r.area) bump(places, `area:${r.area}`, r, () => {});
  }
  const top = (m, n) => [...m.values()].sort((a, b) => b.s - a.s).slice(0, n);
  const te = top(enemies, CAPS.enemies), tm = top(mates, CAPS.mates);
  // Include both area and exact/down places; scores change ordering, not availability.
  const tp = top(places, places.size);
  const L = [];
  const head = (ne, np, nm) => `memory_index (${header(ctx).split(' / ')[0]}${ctx.game ? '' : '. 게임 자료 없음(이름은 ObjectID 이름만, game 질의 안 됨)'}${noAreaFights ? `. 장소를 모르는 옛 싸움 ${noAreaFights}개는 장소 칸에 없음` : ''}. 전체 항목(점수순): 적 ${ne}/전체 ${enemies.size}종, 장소 ${np}/전체 ${places.size}곳, 동료 ${nm}/전체 ${mates.size}명. 목표별 결과는 experience. 목차에 없어도 기록이 있을 수 있음)`;
  L.push(head(te.length, tp.length, tm.length));
  const sec = [];
  if (te.length) sec.push(['적', te.map((o) => ({ s: o.s, t: ` ${o.k} ${objName(ctx, o.k)}${o.boss ? '(보스)' : ''} · 싸움 ${o.fights}·쓰러질 때 근처 ${o.downNear}(가장 가까움 ${o.downNearest}) · 마지막 ${ago(now, o.last)} · ${condOf(o)}` }))]);
  if (tp.length) sec.push(['장소', tp.map((o) => o.k.startsWith('cell:')
    ? { s: o.s, t: ` tile:${o.tile.join(',')} · 쓰러짐 ${o.downs || 0}${o.by ? `(가장 가까움 ${keyName(ctx, o.by)})` : ''} ${o.area || ''} · 마지막 ${ago(now, o.last)}` }
    : { s: o.s, t: ` ${o.k} ${areaName(o.k.slice(5))} · 쓰러짐 ${o.downs || 0}·싸움 ${o.fights || 0}${o.fightsEst ? `(그중 장소 추정 ${o.fightsEst})` : ''} · 마지막 ${ago(now, o.last)}` })]);
  if (tm.length) sec.push(['동료', tm.map((o) => ({ s: o.s, t: ` ${o.k}${o.role ? ` (${o.role})` : ''} · 함께 싸움 ${o.together} · 마지막 ${ago(now, o.last)}` }))]);
  if (ctx.view && ctx.game) {
    const nRec = (k) => (ctx.game.in[k]?.crafted_at || []).length;
    const st = [...ctx.view.stations].filter((k) => k !== 'o:6000').sort((a, b) => nRec(b) - nRec(a));
    sec.push(['게임 지식', [{ s: 1e9, t: ` 본 적 있는 제작대 ${st.length}종${st.length ? `(${st.map((k) => `${k} ${objName(ctx, k)}`).join(', ')})` : ''} · 보이는 레시피 ${ctx.view.recipes.size} · 열린 물체 ${ctx.view.open.size} — 지식일 뿐 지금 쓸 수 있는 제작대가 아님(그건 places·화면 제작대) · game 질의로 봄` },
      ...(kg.ruleKeys(ctx.game).length ? [{ s: 1e9 - 1, t: ` 게임 규칙 ${kg.ruleKeys(ctx.game).length}개(적이 생기는 바닥·기지 안전·적의 길·벽 부수기·방어물 재료 등, 누구나 봄) · {"q":"game","op":"rule","target":"낱말"}로 봄` }] : [])]]);
  }
  // Preserve every section and row.
  const count = (name) => sec.find(([h]) => h === name)?.[1].length || 0;
  const flat = () => [head(count('적'), count('장소'), count('동료')), ...sec.flatMap(([h, ls]) => (ls.length ? [h, ...ls.map((x) => x.t)] : []))].join('\n');
  return flat();
}

// ---------------------------------------------------------------- queries

const evsOf = (ctx, k) => (ctx.idx.byEnt.get(k) || []).map((i) => ctx.idx.ev[i]);
const ranked = (ctx, list) => [...list].sort((a, b) => score(ctx, b) - score(ctx, a) || b.play - a.play);

function qAbout(ctx, q) {
  const t = resolveTarget(ctx, q.target);
  if (!t.keys.length) return { text: t.err, hits: 0, err: 'target' };
  const parts = [];
  let hits = 0;
  for (const k of t.keys) {
    if (k.startsWith('tile:')) { const r = qNear(ctx, { place: k }); parts.push(r.text); hits += r.hits; continue; }
    if (k.startsWith('g:')) { const r = qGoal(ctx, { key: k }); parts.push(r.text); hits += r.hits; continue; }
    if (k.startsWith('e:') || k.startsWith('r:')) { const r = qFollow(ctx, { id: k }); parts.push(r.text); hits += r.hits; continue; }
    let evs = evsOf(ctx, k).filter((r) => r.kind !== 'goal_start' && r.kind !== 'goal_continue');
    let estFights = 0;
    if (k.startsWith('area:')) { const a = k.slice(5); const extra = ctx.idx.ev.filter((r) => r.kind === 'fight' && !r.area && ctx.idx.areaOf(r)?.area === a); estFights = extra.length; evs = [...evs, ...extra]; }
    const runsAbout = [...ctx.idx.runs.values()].filter((u) => k === ctx.idx.objKey(u.item));
    hits += evs.length + runsAbout.length;
    const fights = evs.filter((r) => r.kind === 'fight'), downs = evs.filter((r) => r.kind === 'down');
    const nearest = downs.filter((r) => (r.enemies || [])[0] === k).length;
    const hpNote = k.startsWith('o:') && ctx.game?.nodes[k]?.hp ? `(체력 ${ctx.game.nodes[k].hp})` : '';
    const last = evs.length ? ago(ctx.play, Math.max(...evs.map((r) => r.play))) : '-';
    let sum = `${k} ${keyName(ctx, k)}${hpNote}: `;
    if (k.startsWith('p:')) sum += `함께 싸움 ${fights.length} · 기록 ${evs.length} · 마지막 ${last}`;
    else if (k.startsWith('area:')) sum += `쓰러짐 ${downs.length} · 싸움 ${fights.length}${estFights ? `(그중 장소 추정 ${estFights})` : ''} · 기록 ${evs.length} · 마지막 ${last}`;
    else sum += `싸움 ${fights.length}(쓰러짐 있던 싸움 ${fights.filter((f) => f.downs).length}) · 쓰러질 때 근처 ${downs.length}, 가장 가까운 적 ${nearest}${runsAbout.length ? ` · 이것을 목표로 한 시도 ${runsAbout.length}` : ''} · 마지막 ${last}`;
    if (!evs.length && !runsAbout.length) { parts.push(`${k} ${keyName(ctx, k)}: 기록 없음(겪지 않았다는 뜻이지 안전하다는 뜻은 아님)`); continue; }
    const lines = [...ranked(ctx, evs).map((r) => evLine(ctx, r)), ...runsAbout.sort((a, b) => b.play1 - a.play1).map((u) => runLine(ctx, u))];
    parts.push(`${sum}\n${cap(lines, LIMITS.query - sum.length, `{"q":"follow","id":"${k}","rel":"fight|down"}`)}`);
  }
  return { text: parts.join('\n'), hits };
}

function qGoal(ctx, q) {
  let key = String(q.key || '').replace(/^g:/, '');
  if (!key) return { text: 'goal 질의에는 key가 필요(예: {"q":"goal","key":"g:gather:CopperOre"})', hits: 0, err: 'arg' };
  const runs = (ctx.idx.byKey.get(key) || []).map((r) => ctx.idx.runs.get(r)).filter(Boolean);
  let list = runs.sort((a, b) => b.play1 - a.play1);
  if (q.before != null && Number.isFinite(+q.before)) list = list.filter((u) => u.play1 <= ctx.play - +q.before * 60);
  if (!list.length) {
    const near = [...ctx.idx.byKey.keys()].filter((k) => k.split(':')[0] === key.split(':')[0]);
    return { text: `g:${key}: 기록 없음${near.length ? `. 있는 목표 키: ${near.map((k) => `g:${k}`).join(', ')}` : ''}`, hits: 0 };
  }
  const by = {}; for (const u of list) by[u.outcome] = (by[u.outcome] || 0) + 1;
  const sum = `g:${key}: 시도 ${list.length}(${Object.entries(by).map(([o, n]) => `${OUT_KO[o] || o} ${n}`).join(', ')}), 최신 먼저`;
  return { text: `${sum}\n${cap(list.map((u) => `- ${ago(ctx.play, u.play1)} ${runLine(ctx, u)}`), LIMITS.query - sum.length, `{"q":"goal","key":"g:${key}","before":<분>}`)}`, hits: list.length };
}

function placePoint(ctx, place) {
  const p = String(place ?? 'here').trim();
  if (p === 'here') return ctx.tile ? { xy: ctx.tile, name: `지금 위치 타일 ${ctx.tile.join(',')}` } : { err: '지금 위치를 모름(보고에 me.tile 없음)' };
  if (p === 'core') return { xy: [0, 0], name: '코어' };
  if (p === 'base') {
    const b = ctx.mem?.baseXY;
    return ctx.mem?.baseSet && Array.isArray(b) ? { xy: [b[0], b[1]], name: `기지 타일 ${b[0]},${b[1]}` } : { err: '기지가 정해지지 않음' };
  }
  const m = p.match(/^tile:(-?\d+),(-?\d+)$/);
  if (m) return { xy: [+m[1], +m[2]], name: `타일 ${m[1]},${m[2]}` };
  if (/^(area:|b:)/.test(p) || BIOME_KO[p]) { const t = resolveTarget(ctx, p); return t.keys.length ? { area: t.keys[0].slice(5), name: areaName(t.keys[0].slice(5)) } : { err: t.err }; }
  return { err: `place는 here·base·core·tile:x,y(타일 좌표)·area:이름 가운데 하나` };
}

function qNear(ctx, q) {
  const pt = placePoint(ctx, q.place);
  if (pt.err) return { text: pt.err, hits: 0, err: 'place' };
  const rad = q.radius != null && Number.isFinite(+q.radius) && +q.radius >= 0 ? +q.radius : 32;
  const inside = [];
  for (const r of ctx.idx.ev) {
    if (!['down', 'fight', 'boss_seen', 'boss_kill', 'base_set', 'area_enter', 'goal_end'].includes(r.kind)) continue;
    if (pt.area) { const a = ctx.idx.areaOf(r); if (a?.area === pt.area) inside.push(r); continue; }
    const pl = ctx.idx.placeOf(r);
    if (!pl) continue;
    const d = pl.tile ? Math.hypot(pl.tile[0] - pt.xy[0], pl.tile[1] - pt.xy[1]) : distToCell(pt.xy[0], pt.xy[1], pl.cell[0], pl.cell[1]);
    if (d <= rad) inside.push(r);
  }
  const extra = [];
  if (ctx.mem && pt.xy) {
    const ores = new Map(), sts = [];
    const ox = ctx.mem.oreXY || [], oi = ctx.mem.oreId || [];
    for (let i = 0; i < oi.length; i++) if (Math.hypot(ox[2 * i] - pt.xy[0], ox[2 * i + 1] - pt.xy[1]) <= rad) ores.set(oi[i], (ores.get(oi[i]) || 0) + 1);
    const sx = ctx.mem.stationXY || [], si = ctx.mem.stationId || [];
    for (let i = 0; i < si.length; i++) if (Math.hypot(sx[2 * i] - pt.xy[0], sx[2 * i + 1] - pt.xy[1]) <= rad) sts.push(`${objName(ctx, `o:${si[i]}`)} 타일 ${sx[2 * i]},${sx[2 * i + 1]}`);
    if (ores.size) extra.push(`기억한 광석: ${[...ores].map(([id, n]) => `${objName(ctx, `o:${id}`)} ${n}곳`).join(', ')}`);
    if (sts.length) extra.push(`기억한 제작대: ${sts.join(', ')}`);
  }
  const by = {}, est = {};
  for (const r of inside) { by[r.kind] = (by[r.kind] || 0) + 1; if (ctx.idx.placeOf(r)?.est || (pt.area && !r.area)) est[r.kind] = (est[r.kind] || 0) + 1; }
  const sum = `${pt.name}${pt.area ? '' : ` 둘레 ${rad}타일`}: ${Object.entries(by).map(([k, n]) => `${k} ${n}${est[k] ? `(장소 추정 ${est[k]})` : ''}`).join(', ') || '기록 없음(겪지 않았다는 뜻이지 안전하다는 뜻은 아님)'}`;
  const lines = [...extra, ...ranked(ctx, inside).map((r) => evLine(ctx, r))];
  return { text: `${sum}\n${cap(lines, LIMITS.query - sum.length, `{"q":"near","place":"${q.place}","radius":${rad}}`)}`.trim(), hits: inside.length + extra.length };
}

function qWhen(ctx, q) {
  let from, to;
  if (Array.isArray(q.play)) [from, to] = q.play;   // frozen prefetch: absolute play seconds
  else {
    const a = q.ago_min;
    if (!Array.isArray(a) || a.length !== 2 || !a.every((x) => Number.isFinite(+x))) return { text: 'when의 ago_min은 [시작 분 전, 끝 분 전] 숫자 둘(예: [90, 30])', hits: 0, err: 'arg' };
    const [x, y] = a.map(Number);
    from = ctx.play - Math.max(x, y) * 60; to = ctx.play - Math.min(x, y) * 60;
  }
  const kinds = q.kind ? String(q.kind).split(/[|,]/) : null;
  const list = ctx.idx.ev.filter((r) => r.play >= from && r.play <= to && r.kind !== 'session_start' && (!kinds || kinds.includes(r.kind)));
  const sum = `${ago(ctx.play, from)}~${ago(ctx.play, to)}: 사건 ${list.length}개, 시간순`;
  return { text: `${sum}\n${cap(list.map((r) => evLine(ctx, r)), LIMITS.query - sum.length, '더 좁은 ago_min')}`, hits: list.length };
}

function qFollow(ctx, q) {
  const id = String(q.id || '');
  const rel = q.rel ? String(q.rel).split(/[|,]/) : null;
  let m;
  if ((m = id.match(/^e:(\d+)$/))) {
    const r = ctx.idx.ev.find((x) => x.id === +m[1]);
    if (!r) return { text: `${id} 없음(이 캐릭터의 사건 번호 ${ctx.idx.ev[0]?.id ?? '-'}~${ctx.idx.ev.at(-1)?.id ?? '-'})`, hits: 0, err: 'id' };
    const lines = [evLine(ctx, r), ...r.keys.map((k) => `이어짐: ${k} ${keyName(ctx, k)}`)];
    if (r.run) lines.push(`목표 시도: r:${r.run} ${ctx.idx.runs.get(r.run) ? runLine(ctx, ctx.idx.runs.get(r.run)) : '(진행 중이거나 끝 기록 없음)'}`);
    return { text: lines.join('\n'), hits: 1 };
  }
  if ((m = id.match(/^r:(.+)$/))) {
    const l = (ctx.idx.byRun.get(m[1]) || []).map((i) => ctx.idx.ev[i]).filter((r) => !rel || rel.includes(r.kind));
    if (!l.length) return { text: `${id} 없음`, hits: 0, err: 'id' };
    return { text: cap(l.map((r) => evLine(ctx, r)), LIMITS.query, `{"q":"follow","id":"${id}","rel":"down|fight"}`), hits: l.length };
  }
  const t = resolveTarget(ctx, id);
  if (!t.keys.length) return { text: t.err, hits: 0, err: 'target' };
  if (t.keys[0].startsWith('tile:')) return qNear(ctx, { place: t.keys[0] });
  // memory relations only: game relations of an o: node are shown by the game query
  const l = t.keys.flatMap((k) => evsOf(ctx, k)).filter((r) => !rel || rel.includes(r.kind) || (rel.includes('goal') && r.kind === 'goal_end'));
  return { text: `${t.keys.map((k) => `${k} ${keyName(ctx, k)}`).join(', ')}의 기억 관계 ${l.length}개(최신 먼저)\n${cap([...l].sort((a, b) => b.play - a.play).map((r) => evLine(ctx, r)), LIMITS.query, `{"q":"follow","id":"${id}","rel":"<kind>"}`)}`, hits: l.length };
}

function qGame(ctx, q) {
  if (!ctx.game) return { text: '게임 자료 없음: game 질의를 할 수 없음', hits: 0, err: 'nodata' };
  const op = q.op;
  if (!['how', 'uses', 'station', 'find', 'rule'].includes(op)) return { text: 'game의 op는 how(얻는 법)·uses(쓰임)·station(제작대 목록)·find(이름 찾기)·rule(게임 규칙: 스폰·기지 안전·적 길·벽 부수기, target은 낱말)', hits: 0, err: 'arg' };
  const view = ctx.view;   // never null here
  const target = String(q.target ?? '').trim();
  if (op === 'rule') {   // general game rules: open to every character, target optional (none: the list)
    const lines = kg.rule(ctx.game, target);
    const hits = lines.filter((l) => l.startsWith('r:')).length;
    return { text: cap([`게임 규칙${target ? ` '${target}'` : ''} (rules.json, 수준: 게임 데이터·웹·사용자·추정)`, ...lines.map((l) => `  ${l}`)], LIMITS.query, null), hits };
  }
  if (!target) return { text: 'game 질의에는 target이 필요', hits: 0, err: 'arg' };
  if (op === 'find') {
    const ks = kg.find(ctx.game, target, view);
    return { text: ks.length ? `찾음(열린 것만): ${ks.map((k) => `${k} ${objName(ctx, k)}`).join(', ')}` : `'${target}': 열린 것 가운데 없음`, hits: ks.length };
  }
  let k = /^o:\d+$/.test(target) ? target : /^\d+$/.test(target) ? `o:${target}` : kg.resolve(ctx.game, target, view);
  if (k && !view.open.has(k) && !(op === 'station' && k === 'o:6000')) return { text: `${target}: 이 캐릭터에게 열리지 않음`, hits: 0 };
  if (!k) {
    const c = kg.find(ctx.game, target, view);
    return { text: `'${target}'를 하나로 정하지 못함${c.length ? `. 비슷한 것: ${c.map((x) => `${x} ${objName(ctx, x)}`).join(', ')}` : ' (열린 것 가운데 없음)'}`, hits: 0, err: 'target' };
  }
  const lines = kg[op](ctx.game, k, view);
  return { text: cap([`${k} ${objName(ctx, k)} (${op}, 이 캐릭터가 본 범위)`, ...lines.map((l) => `  ${l}`)], LIMITS.query, null), hits: lines.length };
}

function qFind(ctx, q) {
  const t = lc(q.text);
  if (!t) return { text: 'find의 text는 비어 있을 수 없음', hits: 0, err: 'arg' };
  const hit = (r) => [r.text, r.cause, r.why, r.say, r.reason, r.key, r.goal, r.kind].some((x) => x && lc(x).includes(t)) || r.keys.some((k) => lc(keyName(ctx, k)).includes(t));
  const l = ctx.idx.ev.filter(hit);
  const said = l.filter((r) => r.src === 'llm' || r.kind === 'plan_result'), obs = l.filter((r) => !said.includes(r));
  return { text: `'${q.text}': 관찰 ${obs.length}, 전에 한 말 ${said.length}\n${cap([...ranked(ctx, obs), ...ranked(ctx, said)].map((r) => evLine(ctx, r)), LIMITS.query, '더 좁은 낱말이나 about')}`, hits: l.length };
}

const Q = { about: qAbout, goal: qGoal, near: qNear, when: qWhen, follow: qFollow, game: qGame, find: qFind };
export const QUERY_NAMES = Object.keys(Q);

// One query -> {text, hits, err}. Never throws.
export function runQuery(ctx, q) {
  try {
    if (!q || typeof q !== 'object' || Array.isArray(q)) return { text: '질의는 {"q": 이름, ...} 꼴의 객체', hits: 0, err: 'shape' };
    const f = Q[q.q];
    if (!f) return { text: `모르는 q '${q.q}'. 쓸 수 있는 것: ${QUERY_NAMES.join(', ')}`, hits: 0, err: 'q' };
    const r = f(ctx, q);
    return r;
  } catch (e) { return { text: `질의를 처리하지 못함: ${String(e.message || e)}`, hits: 0, err: 'error' }; }
}

// Query count is a call-budget policy; result text is never cut to a character budget.
export function runQueries(ctx, queries, { maxQ = 3, limit = LIMITS.search } = {}) {
  const list = Array.isArray(queries) ? queries : [];
  const log = [], parts = [];
  if (!list.length) return { text: 'recall은 질의 객체 1~3개의 배열(예: [{"q":"about","target":"o:3050"}])', log: [{ q: null, err: 'empty', hits: 0, chars: 0 }] };
  const head = header(ctx);
  list.slice(0, maxQ).forEach((q, i) => {
    const r = runQuery(ctx, q);
    const t = r.text;
    parts.push(`(${i + 1}) ${JSON.stringify(q)}\n${t}`);
    log.push({ q: q?.q ?? null, target: q?.target ?? q?.key ?? q?.place ?? q?.id ?? q?.text ?? null, hits: r.hits, chars: r.text.length, err: r.err || null });
  });
  if (list.length > maxQ) parts.push(`(질의 ${list.length}개 가운데 앞의 ${maxQ}개만 함. 한 번에 ${maxQ}개까지)`);
  let text = `${head}\n${parts.join('\n')}`;
  return { text, log };
}

// ---------------------------------------------------------------- prefetch: freeze "here" and relative times at the
// moment the answer carried them (re-check R2-2).
export function freezeQueries(queries, { tile, play }) {
  return (Array.isArray(queries) ? queries : []).slice(0, 3).map((q) => {
    if (!q || typeof q !== 'object') return q;
    const c = { ...q };
    if (c.q === 'near' && (c.place == null || c.place === 'here') && tile) c.place = `tile:${tile[0]},${tile[1]}`;
    if (c.q === 'when' && Array.isArray(c.ago_min) && c.ago_min.every((x) => Number.isFinite(+x))) {
      const [x, y] = c.ago_min.map(Number);
      c.play = [play - Math.max(x, y) * 60, play - Math.min(x, y) * 60]; delete c.ago_min;
    }
    return c;
  });
}

// ---------------------------------------------------------------- developer command line

function parseArgs(a) {
  const o = { _: [] };
  for (let i = 0; i < a.length; i++) if (a[i].startsWith('--')) o[a[i].slice(2)] = a[i + 1] && !a[i + 1].startsWith('--') ? a[++i] : true; else o._.push(a[i]);
  return o;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const [cmd, ...rest] = args._;
  const root = args.root || AIPLAYERS, member = args.member || 'claude';
  let world = args.world;
  if (!world) { const ws = fs.readdirSync(root).filter((w) => fs.existsSync(path.join(root, w, member, 'events.jsonl'))); if (ws.length !== 1) throw new Error(`--world needed (${ws.join(', ')})`); world = ws[0]; }
  const { game } = findGame(args.data);
  const tile = args.tile ? args.tile.split(',').map(Number) : null;
  const t0 = performance.now();
  const ctx = context({ root, world, member, game, tile });
  const t1 = performance.now();
  if (cmd === 'index' || !cmd) { console.log(memoryIndex(ctx)); console.error(`(${ctx.idx.ev.length} events, build ${(t1 - t0).toFixed(1)} ms)`); return; }
  const v = rest.join(' ');
  const q = cmd === 'about' ? { q: 'about', target: v } : cmd === 'goal' ? { q: 'goal', key: v } : cmd === 'near' ? { q: 'near', place: v || 'here', radius: args.radius } : cmd === 'when' ? { q: 'when', ago_min: v.split(',').map(Number) }
    : cmd === 'follow' ? { q: 'follow', id: v, rel: args.rel } : cmd === 'game' ? { q: 'game', op: rest[0], target: rest.slice(1).join(' ') } : { q: 'find', text: v };
  console.log(runQueries(ctx, [q]).text);
}

if (import.meta.url === pathToFileURL(process.argv[1] || '').href) main().catch((e) => { console.error(e.message); process.exit(1); });
