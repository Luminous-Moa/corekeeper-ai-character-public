// kg.mjs — query the game knowledge graph (library + developer command line).
// Design: work/2026-11/2026-11-04-게임-지식그래프/2-설계.md (session 코어키퍼-AI-캐릭터).
//
// Without --as: the whole graph (developer view). With --as <member>: only what is open to that character:
//   - objects it has seen or held (known.json; wall/ore tiles resolve through tile_is)
//   - hand recipes, and the recipes of every station it has seen, with their materials (as on the game's crafting
//     screen); those results and materials are open too
//   - drops it has experienced (known.json drops); loot tables, spawn biomes stay developer-only
//   - a relation shows only when both ends are open (recipes: when the recipe is visible)
//
// Rules (rules.json, r:<id> nodes) are open to every character; how/uses of an object add the rules about it.
//
// Usage:
//   node kg.mjs uses|how|station|find <name|id|text> [--as <member> [--world <id>] | --known <known.json>] [--json]
//   node kg.mjs rule [words|rule id]
//   node kg.mjs prefill --as <member> [--world <id>] [--out <file>]   memory.json -> known.json (same rule as the mod)
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
export const AIPLAYERS = path.join(os.homedir(), 'AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers');
const BROKEN_KEY_OFFSET = 1 << 24;   // AIVitals.BrokenKeyOffset
const readJson = (f) => JSON.parse(fs.readFileSync(f, 'utf8').replace(/^﻿/, ''));

// ---------------------------------------------------------------- load

export function loadGraph(dataRoot = path.join(HERE, 'data')) {
  const build = fs.readFileSync(path.join(dataRoot, 'current.txt'), 'utf8').trim();
  const g = readJson(path.join(dataRoot, build, 'graph.json'));
  return indexGraph(g);
}

export function indexGraph(g) {
  g.out = {}; g.in = {};
  for (const e of g.edges) {
    ((g.out[e[1]] ||= {})[e[0]] ||= []).push(e);
    ((g.in[e[2]] ||= {})[e[0]] ||= []).push(e);
  }
  return g;
}
const outE = (g, k, t) => g.out[k]?.[t] || [];
const inE = (g, k, t) => g.in[k]?.[t] || [];

// ---------------------------------------------------------------- known / open

export function knownPath(member, world) {
  if (!world) {
    const ws = fs.existsSync(AIPLAYERS) ? fs.readdirSync(AIPLAYERS).filter((w) => fs.existsSync(path.join(AIPLAYERS, w, member))) : [];
    if (ws.length !== 1) throw new Error(`--world needed (${member} found in ${ws.length} worlds: ${ws.join(', ')})`);
    world = ws[0];
  }
  return { world, file: path.join(AIPLAYERS, world, member, 'known.json'), memory: path.join(AIPLAYERS, world, member, 'memory.json') };
}

// Same rule as AIKnowledge.Prefill in the mod.
export function prefillFromMemory(mem) {
  const now = Math.floor(Date.now() / 1000);
  const seen = new Map(), held = new Map(), drops = [];
  const S = (id, t = now) => { if (id > 0 && !seen.has(id)) seen.set(id, t); };
  const H = (id, t = now) => { if (id > 0 && !held.has(id)) held.set(id, t); };
  (mem.oreId || []).forEach((id, i) => S(id, mem.oreSeen?.[i] ?? now));
  (mem.stationId || []).forEach((id, i) => S(id, mem.stationSeen?.[i] ?? now));
  (mem.toolId || []).forEach((id) => H(id));
  (mem.noDmgId || []).forEach((id) => S(id));
  const c = mem.combatLearned || [];
  for (let i = 0; i + 4 < c.length; i += 5) { const id = c[i + 1]; if (c[i] === 1) H(id >= BROKEN_KEY_OFFSET ? id - BROKEN_KEY_OFFSET : id); else S(id); }
  const d = mem.drops || [];
  for (let i = 0; i + 4 < d.length; i += 5) { S(d[i]); if (d[i + 1] > 0) { H(d[i + 1]); drops.push([d[i], d[i + 1]]); } }
  return {
    version: 1, seenId: [...seen.keys()], seenAt: [...seen.values()], heldId: [...held.keys()], heldAt: [...held.values()],
    tiles: [], tilesAt: [], dropSrc: drops.map((x) => x[0]), dropItem: drops.map((x) => x[1]), prefilled: 'memory.json', updated: now,
  };
}

// The part of the graph open to one character.
export function openView(g, known) {
  const open = new Set(['o:6000']), stations = new Set(), recipes = new Set();   // the player itself (hand recipes) is always open
  for (const id of [...(known.seenId || []), ...(known.heldId || [])]) open.add(`o:${id}`);
  for (const t of known.tiles || []) for (const e of outE(g, `t:${t}`, 'tile_is')) open.add(e[2]);
  for (const k of open) if (inE(g, k, 'crafted_at').length) stations.add(k);
  for (const e of inE(g, 'o:6000', 'hand_craft')) recipes.add(e[1]);
  for (const s of stations) for (const e of inE(g, s, 'crafted_at')) recipes.add(e[1]);
  for (const r of recipes) { open.add(r); for (const e of inE(g, r, 'ingredient')) open.add(e[1]); }
  const drops = [];
  for (let i = 0; i < (known.dropSrc || []).length; i++) drops.push([`o:${known.dropSrc[i]}`, `o:${known.dropItem[i]}`]);
  return { open, stations, recipes, drops };
}

function visible(view, e) {
  if (!view) return true;
  const [t, a, b] = e;
  switch (t) {
    case 'ingredient': return view.recipes.has(b);
    case 'crafted_at': return view.stations.has(b);
    case 'hand_craft': return true;
    case 'tile_is': return view.open.has(b);
    case 'gives': return view.open.has(a);
    case 'mineable_by': return view.open.has(a) && view.open.has(b);
    case 'rule_about': return true;   // rules are general game knowledge: open to every character (user decision 2026-11-05)
    default: return false;   // uses_loot, loot_item, spawns_in: developer view only
  }
}

// ---------------------------------------------------------------- queries

export function nameOf(g, k) {
  const n = g.nodes[k];
  if (!n) return k;
  return n.ko ? `${n.ko}(${n.name})` : n.name || k;
}

export function resolve(g, q, view) {
  if (/^[olbct]:/.test(q) && g.nodes[q]) return q;
  if (/^\d+$/.test(q) && g.nodes[`o:${q}`]) return `o:${q}`;
  const ql = q.toLowerCase();
  const all = Object.entries(g.nodes).filter(([k]) => k.startsWith('o:') && (!view || view.open.has(k)));
  const exact = all.find(([, n]) => [n.name, n.ko, n.en].some((x) => x && x.toLowerCase() === ql));
  if (exact) return exact[0];
  const part = all.filter(([, n]) => [n.name, n.ko, n.en].some((x) => x && x.toLowerCase().includes(ql)));
  return part.length === 1 ? part[0][0] : null;
}

export function find(g, text, view) {
  const t = text.toLowerCase();
  return Object.entries(g.nodes)
    .filter(([k, n]) => k.startsWith('o:') && (!view || view.open.has(k)) && [n.name, n.ko, n.en].some((x) => x && x.toLowerCase().includes(t)))
    .map(([k]) => k);
}

const where = (g, r, view) => {
  const at = outE(g, r, 'crafted_at').filter((e) => visible(view, e)).map((e) => nameOf(g, e[2]));
  if (outE(g, r, 'hand_craft').length) at.unshift('손');
  return at;
};
const mats = (g, r, view) => inE(g, r, 'ingredient').filter((e) => visible(view, e)).map((e) => `${nameOf(g, e[1])} ×${e[3].n}`);

// The game's damage range around a base damage (EntityUtility.GetDamageMinMax: ±10%, at least 1).
const dmgRange = (d) => { const y = Math.max(1, Math.trunc(0.1 * Math.abs(d))); return [Math.max(1, d - y), d + y]; };

// One line of combat numbers. Weapons: what the item tooltip shows, so open to a character that sees the item
// (e.g. on a seen station's recipe list). Creatures: the game screen shows no attack numbers, so developer view only.
export function stats(g, k, view) {
  const n = g.nodes[k];
  if (!n) return null;
  if (n.wdmg > 0 || n.wRange !== undefined) {
    if (view && !view.open.has(k)) return null;
    const p = [];
    if (n.wdmg > 0) { const [a, b] = dmgRange(n.wdmg); p.push(`피해 ${a}~${b}(기본 ${n.wdmg}, ±10%${n.lv >= 0 ? `, 레벨 ${n.lv}` : ''})`); } else p.push('피해 모름');
    p.push(`${n.wRange ? '원거리' : '근접'}/${n.wMagic ? '마법' : '물리'}`);
    const cd = n.cd ?? (n.rangeWeapon ? 0.6 : 0.4);
    p.push(`공속 ${cd}초${n.cd == null ? '(게임 기본값)' : ''}`);
    return `무기: ${p.join(', ')}`;
  }
  if ((n.role || []).includes('creature') && (n.hp || n.atk || n.speed)) {
    if (view) return null;
    const p = [], a = n.atk || {}, at = [], iv = [];
    if (n.hp) p.push(`체력 ${n.hp}`);
    if (n.red) p.push(`방어 ${n.red}`);
    if (a.melee) at.push(`근접 ${a.melee}`);
    if (a.touch) at.push(`접촉 ${a.touch}`);
    if (a.jump) at.push(`점프 ${a.jump}`);
    if (a.rangeProj) at.push(`원거리 탄 ${nameOf(g, `o:${a.rangeProj}`)}`);
    p.push(at.length ? `공격력 ${at.join('·')}` : '공격력 모름');
    if (a.touchCd) iv.push(`접촉 ${a.touchCd}초`);
    if (a.rangeCdMin != null) iv.push(`원거리 ${a.rangeCdMin}~${a.rangeCdMax}초`);
    if (iv.length) p.push(`공격 간격 ${iv.join('·')}`);
    if (n.speed) p.push(`이동 속도 ${n.speed}`);
    return `몬스터: ${p.join(', ')}`;
  }
  return null;
}

// ---------------------------------------------------------------- rules (rules.json: r:<id> nodes)
// Hand-written game rules (spawning, base safety, enemy paths, wall breaking). Open to every character: they are
// general game knowledge the user asked the upper LLMs to be able to look up. Each line carries how it is known.

const RULE_MAX = 3;   // rules shown with how/uses of one object; the rest through the rule query
export function ruleLevel(g, level) {
  const names = g.sources?.rules?.levels || {};
  return String(level || 'estimate').split('+').map((x) => names[x] || x).join('+');
}
export const ruleLine = (g, k) => { const r = g.nodes[k]; return `[${ruleLevel(g, r.level)}] ${r.text}`; };
export const ruleKeys = (g) => Object.keys(g.nodes).filter((k) => k.startsWith('r:'));

function rulesOf(g, k) {
  const rs = inE(g, k, 'rule_about').map((e) => e[1]);
  if (!rs.length) return [];
  const lines = rs.slice(0, RULE_MAX).map((r) => `규칙 ${r}: ${ruleLine(g, r)}`);
  if (rs.length > RULE_MAX) lines.push(`규칙 ${rs.length - RULE_MAX}개 더: ${rs.slice(RULE_MAX).join(', ')} (rule 질의로 봄)`);
  return lines;
}

// Rules matching the words of text (any word, in topics, text or id), most matched words first. No text or no hit:
// the list of rules (id and first topics) so the asker can pick.
export function rule(g, text) {
  const ks = ruleKeys(g);
  if (!ks.length) return ['(규칙 자료 없음 — rules.json으로 그래프를 다시 만들어야 함)'];
  const index = () => [`규칙 ${ks.length}개(낱말로 찾기, 예: 스폰·기지·벽·문·코어): ${ks.map((k) => `${k.slice(2)}(${(g.nodes[k].topics || []).slice(0, 3).join('·')})`).join(', ')}`];
  const r = g.nodes[`r:${String(text || '').trim()}`] ? [`r:${String(text).trim()}`] : null;
  if (r) return [`${r[0]} ${ruleLine(g, r[0])}`];
  const words = String(text || '').toLowerCase().split(/[\s,·/]+/).filter((w) => w.length >= 1);
  if (!words.length) return index();
  const scored = ks.map((k) => {
    const n = g.nodes[k], topics = (n.topics || []).map((t) => t.toLowerCase()), body = `${n.id} ${n.text}`.toLowerCase();
    let s = 0;
    for (const w of words) { if (topics.some((t) => t === w)) s += 3; else if (topics.some((t) => t.includes(w) || w.includes(t))) s += 2; else if (body.includes(w)) s += 1; }
    return [k, s];
  }).filter(([, s]) => s > 0).sort((a, b) => b[1] - a[1]);
  if (!scored.length) return [`'${text}': 맞는 규칙 없음`, ...index()];
  return scored.slice(0, 6).map(([k]) => `${k} ${ruleLine(g, k)}`).concat(scored.length > 6 ? [`(맞는 규칙 ${scored.length - 6}개 더: ${scored.slice(6).map(([k]) => k).join(', ')})`] : []);
}

export function how(g, k, view) {
  const lines = [], n = g.nodes[k];
  if (view && !view.open.has(k)) return ['(이 캐릭터에게 열리지 않음)'];
  const st = stats(g, k, view);
  if (st) lines.push(st);
  const at = where(g, k, view);
  if (at.length) lines.push(`만드는 곳: ${at.join(', ')} / 재료: ${mats(g, k, view).join(', ') || '(없음)'}`);
  else if (outE(g, k, 'crafted_at').length || outE(g, k, 'hand_craft').length) lines.push('만드는 곳: (아직 본 적 없는 제작대)');
  const mine = outE(g, k, 'mineable_by').filter((e) => visible(view, e)).map((e) => `${nameOf(g, e[2])} 약 ${e[3].hits}타`);
  if (mine.length) lines.push(`캐는 도구(계산): ${mine.join(', ')}`);
  for (const e of inE(g, k, 'tile_is').filter((e) => visible(view, e))) lines.push(`타일: ${g.nodes[e[1]]?.name || e[1]}`);
  if (!view) {
    const srcs = inE(g, k, 'loot_item').map((e) => {
      const users = inE(g, e[1], 'uses_loot').map((u) => nameOf(g, u[1]));
      return `${g.nodes[e[1]]?.name || e[1]}${users.length ? `(${users.slice(0, 4).join(', ')}${users.length > 4 ? ' …' : ''})` : ''} w${e[3].w} ×${e[3].min}~${e[3].max}`;
    });
    if (srcs.length) lines.push(`드롭 표: ${srcs.slice(0, 12).join(' / ')}${srcs.length > 12 ? ` … 외 ${srcs.length - 12}` : ''}`);
    const sp = outE(g, k, 'spawns_in').map((e) => `${g.nodes[e[2]]?.name}(${e[3].kind})`);
    if (sp.length) lines.push(`나오는 곳: ${[...new Set(sp)].join(', ')}`);
  } else {
    const got = view.drops.filter(([, i]) => i === k).map(([s]) => (s === 'o:-1' ? '큰 뿌리' : nameOf(g, s)));
    if (got.length) lines.push(`겪은 드롭: ${got.join(', ')}에서 나옴`);
  }
  if (!lines.length) lines.push(n ? '(아는 얻는 법 없음)' : '(그래프에 없음)');
  lines.push(...rulesOf(g, k));
  return lines;
}

export function uses(g, k, view) {
  const lines = [];
  if (view && !view.open.has(k)) return ['(이 캐릭터에게 열리지 않음)'];
  const st = stats(g, k, view);
  if (st) lines.push(st);
  const as = outE(g, k, 'ingredient').filter((e) => visible(view, e))
    .map((e) => `${nameOf(g, e[2])} ×${e[3].n} 필요 @${where(g, e[2], view).join('/') || '?'}`);
  if (as.length) lines.push(`재료로 쓰임(${as.length}): ${as.slice(0, 15).join(' / ')}${as.length > 15 ? ' …' : ''}`);
  const gv = outE(g, k, 'gives').filter((e) => visible(view, e)).map((e) => `${e[3].when === 'eat' ? '먹으면' : '장착하면'} ${g.nodes[e[2]]?.name || e[2]} ${e[3].v}${e[3].d ? ` ${e[3].d}초` : ''}`);
  if (gv.length) lines.push(`효과: ${gv.join(', ')}`);
  const mines = inE(g, k, 'mineable_by').filter((e) => visible(view, e)).map((e) => `${nameOf(g, e[1])} 약 ${e[3].hits}타`);
  if (mines.length) lines.push(`캘 수 있는 벽(계산): ${mines.slice(0, 12).join(', ')}${mines.length > 12 ? ' …' : ''}`);
  const rec = inE(g, k, 'crafted_at');
  if (rec.length) lines.push(`제작대: 레시피 ${rec.filter((e) => visible(view, e)).length}개 (station 명령으로 목록)`);
  if (!view) {
    for (const e of outE(g, k, 'uses_loot')) {
      const items = outE(g, e[2], 'loot_item').map((x) => `${nameOf(g, x[2])} w${x[3]?.w ?? '?'}`);
      lines.push(`떨구는 드롭 표 ${g.nodes[e[2]]?.name || e[2]}: ${items.slice(0, 10).join(', ') || '(비어 있음)'}${items.length > 10 ? ' …' : ''}`);
    }
  } else {
    const gave = view.drops.filter(([s]) => s === k).map(([, i]) => nameOf(g, i));
    if (gave.length) lines.push(`겪은 드롭: ${gave.join(', ')}`);
    else if (outE(g, k, 'uses_loot').length) lines.push('무언가를 떨굼(겪은 것 없음)');
  }
  if (!lines.length) lines.push(g.nodes[k] ? '(아는 쓰임 없음)' : '(그래프에 없음)');
  lines.push(...rulesOf(g, k));
  return lines;
}

export function station(g, k, view) {
  const hand = k === 'o:6000';   // the player itself: hand recipes (always shown, like the inventory crafting screen)
  if (view && !hand && !view.stations.has(k)) return [view.open.has(k) ? '(본 적 없는 제작대라 목록을 모름)' : '(이 캐릭터에게 열리지 않음)'];
  const rec = inE(g, k, hand ? 'hand_craft' : 'crafted_at').filter((e) => visible(view, e));
  if (!rec.length) return ['(레시피 없음 — 뽑기 전이면 레시피 데이터가 아직 없음)'];
  return rec.map((e) => `${nameOf(g, e[1])}${e[3].n > 1 ? ` ×${e[3].n}` : ''}: ${mats(g, e[1], view).join(', ') || '(재료 없음)'}`);
}

// ---------------------------------------------------------------- command line

function parseArgs(a) {
  const o = { _: [] };
  for (let i = 0; i < a.length; i++) if (a[i].startsWith('--')) o[a[i].slice(2)] = a[i + 1] && !a[i + 1].startsWith('--') ? a[++i] : true; else o._.push(a[i]);
  return o;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));
  const [cmd, ...rest] = args._;
  const q = rest.join(' ');
  if (cmd === 'prefill') {
    if (!args.as) throw new Error('prefill needs --as <member>');
    const p = knownPath(args.as, args.world);
    const known = prefillFromMemory(readJson(p.memory));
    const out = args.out || path.join(HERE, 'data', 'known', p.world, args.as, 'known.json');   // never into the game folder
    fs.mkdirSync(path.dirname(out), { recursive: true });
    fs.writeFileSync(out, JSON.stringify(known));
    console.log(`prefill ${args.as} (${p.world}) from ${p.memory}: seen ${known.seenId.length}, held ${known.heldId.length}, drops ${known.dropSrc.length} -> ${out}`);
    return;
  }
  if (!['uses', 'how', 'station', 'find', 'rule'].includes(cmd) || (!q && cmd !== 'rule')) {
    console.log('usage: node kg.mjs uses|how|station|find <name|id|text> [--as <member> [--world <id>] | --known <file>] [--json]\n       node kg.mjs rule [words|rule id]   (game rules; open to every character)\n       node kg.mjs prefill --as <member> [--world <id>] [--out <file>]');
    process.exit(1);
  }
  const g = loadGraph(args.data);
  if (cmd === 'rule') { for (const l of rule(g, q)) console.log(l); return; }
  let view = null, who = '전체(개발자)';
  if (args.known || args.as) {
    let file = args.known;
    if (!file) {
      const p = knownPath(args.as, args.world);
      file = fs.existsSync(p.file) ? p.file : path.join(HERE, 'data', 'known', p.world, args.as, 'known.json');
    }
    if (!fs.existsSync(file)) throw new Error(`no known.json for ${args.as}: ${file} (run prefill first, or play with the mod)`);
    view = openView(g, readJson(file));
    who = `${args.as || 'known'} 시점 (열린 물체 ${view.open.size}, 본 제작대 ${view.stations.size}, 보이는 레시피 ${view.recipes.size}; ${file})`;
  }
  let out;
  if (cmd === 'find') out = find(g, q, view).slice(0, 40).map((k) => { const st = stats(g, k, view); return `${k} ${nameOf(g, k)}${st ? ` — ${st}` : ''}`; });
  else {
    const k = resolve(g, q, view);
    if (!k) { const c = find(g, q, view).slice(0, 10); out = [`'${q}'를 하나로 정하지 못함${c.length ? `: ${c.map((x) => nameOf(g, x)).join(', ')}` : ' (없거나 열리지 않음)'}`]; }
    else out = [`${nameOf(g, k)} [${k}]`, ...({ uses, how, station }[cmd])(g, k, view).map((l) => `  ${l}`)];
  }
  if (args.json) console.log(JSON.stringify({ who, cmd, q, lines: out }));
  else { console.log(`# ${who} · 게임 빌드 ${g.game.steamBuildId}${g.sources.dump ? (g.sources.dump.fake ? ' · 가짜 뽑기 데이터' : '') : ' · 뽑기 전(Conf만)'}`); for (const l of out) console.log(l); }
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) main().catch((e) => { console.error(e.message); process.exit(1); });
