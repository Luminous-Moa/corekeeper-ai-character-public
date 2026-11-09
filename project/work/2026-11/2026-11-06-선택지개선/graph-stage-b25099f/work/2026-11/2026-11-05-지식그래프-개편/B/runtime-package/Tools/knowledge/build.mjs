// build.mjs — builds the game knowledge graph (graph.json) from the game's own data only.
// Design: work/2026-11/2026-11-04-게임-지식그래프/2-설계.md (session 코어키퍼-AI-캐릭터).
//
// Sources:
//   - Conf JSON in the install (names/IDs, loot tables, spawns) — readable without the game.
//   - dump.json from the mod's test command "/ai kgdump" (recipes, stations, stats, effects, tiles, Korean names).
//     Without a dump the graph has only the Conf part and says so in "unresolved".
//   - rules.json next to this file (in git): hand-written game rules with their level and sources.
// Data: Tools/knowledge/data/<steam buildid>/{conf/, dump.json, graph.json}, data/current.txt. Not in git.
//
// Usage: node build.mjs [--dump <knowledge-dump.json>] [--game <install dir>] [--data <data root>]
//   --dump copies the file into data/<buildid>/dump.json first (refused if older than the installed build).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const args = parseArgs(process.argv.slice(2));
const GAME = args.game || 'C:/Program Files (x86)/Steam/steamapps/common/Core Keeper';
const MANIFEST = path.join(GAME, '..', '..', 'appmanifest_1621690.acf');
const CONF = path.join(GAME, 'CoreKeeper_Data/StreamingAssets/Conf');
const DATA = args.data || path.join(HERE, 'data');

const TT_WALL = 35, TT_ORE = 129;
const PLAYER = 6000;               // ObjectID.Player: its recipe list is the hand-craft list
const COND_MINING = 9;             // ConditionID.MiningIncrease (equipped pick bonus) — to confirm after the real dump
const BASE_MINING = 20;            // Constants.playerBaseMiningDamage (same estimate as the mod's AIWallData)

function parseArgs(a) {
  const o = {};
  for (let i = 0; i < a.length; i++) if (a[i].startsWith('--')) o[a[i].slice(2)] = a[i + 1] && !a[i + 1].startsWith('--') ? a[++i] : true;
  return o;
}
const readText = (f) => fs.readFileSync(f, 'utf8').replace(/^\uFEFF/, '');
// The dump wrote float.PositiveInfinity as bare Infinity (e.g. a condition with no time limit) before the writer
// emitted null; read such dumps too: bare Infinity / -Infinity / NaN outside strings become null.
const readJson = (f) => JSON.parse(readText(f).replace(/(?<=[\[,:]\s*)-?(?:Infinity|NaN)(?=\s*[\],}])/g, 'null'));
// Conf/ID/*.json are not standard JSON (no commas): one "Name": number per line.
function readIdFile(f) {
  const byName = {}, byId = {};
  for (const line of readText(f).split(/\r?\n/)) {
    const m = line.match(/^\s*"([^"]+)"\s*:\s*(-?\d+)/);
    if (m) { byName[m[1]] = +m[2]; if (!(m[2] in byId)) byId[m[2]] = m[1]; }
  }
  return { byName, byId };
}
function manifest() {
  const t = readText(MANIFEST);
  const get = (k) => (t.match(new RegExp(`"${k}"\\s+"([^"]*)"`)) || [])[1];
  return { buildid: get('buildid'), lastUpdated: +get('LastUpdated') };
}
function copyDir(from, to) {
  fs.mkdirSync(to, { recursive: true });
  for (const e of fs.readdirSync(from, { withFileTypes: true })) {
    const a = path.join(from, e.name), b = path.join(to, e.name);
    if (e.isDirectory()) copyDir(a, b); else fs.copyFileSync(a, b);
  }
}
function listJson(dir) {
  const out = [];
  if (!fs.existsSync(dir)) return out;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) out.push(...listJson(p)); else if (e.name.endsWith('.json')) out.push(p);
  }
  return out;
}

const { buildid, lastUpdated } = manifest();
const dir = path.join(DATA, buildid);
fs.mkdirSync(dir, { recursive: true });
const confDir = path.join(dir, 'conf');
if (!fs.existsSync(confDir)) { for (const d of ['ID', 'Loot', 'Spawn']) copyDir(path.join(CONF, d), path.join(confDir, d)); console.log(`conf copied for build ${buildid}`); }
const prev = fs.existsSync(path.join(DATA, 'current.txt')) ? readText(path.join(DATA, 'current.txt')).trim() : null;
if (prev && prev !== buildid) console.log(`game build changed ${prev} -> ${buildid}: new folder, old one kept`);

const dumpFile = path.join(dir, 'dump.json');
if (args.dump) {
  const d = readJson(args.dump);
  if (!(d.time >= lastUpdated)) { console.error(`dump ${args.dump} is older than the installed build (dump ${d.time} < build ${lastUpdated}): run /ai kgdump again`); process.exit(2); }
  fs.copyFileSync(args.dump, dumpFile);
  console.log(`dump copied: ${args.dump} -> ${dumpFile}`);
}

// ---------------------------------------------------------------- graph
const ids = Object.fromEntries(['ObjectID', 'LootTableID', 'Biome', 'ConditionID', 'PugTilemap.Tileset', 'PugTilemap.TileType']
  .map((n) => [n, readIdFile(path.join(confDir, 'ID', `${n}.json`))]));
const nodes = {}, edges = [], unresolved = [];
const node = (k, v) => (nodes[k] = Object.assign(nodes[k] || {}, v));
const edge = (t, a, b, attrs) => edges.push(attrs ? [t, a, b, attrs] : [t, a, b]);

for (const [name, id] of Object.entries(ids.ObjectID.byName)) if (id > 0 && ids.ObjectID.byId[id] === name) node(`o:${id}`, { id, name });
for (const [name, id] of Object.entries(ids.LootTableID.byName)) node(`l:${id}`, { id, name });
for (const [name, id] of Object.entries(ids.Biome.byName)) node(`b:${id}`, { id, name });
for (const [name, id] of Object.entries(ids.ConditionID.byName)) if (id > 0) node(`c:${id}`, { id, name });

// loot tables (file name is not the ID; the lootTableID field is)
let lootFiles = 0;
for (const f of listJson(path.join(confDir, 'Loot'))) {
  const t = readJson(f); lootFiles++;
  const l = `l:${t.lootTableID}`;
  node(l, { uniqueDrops: t.uniqueDrops, file: path.basename(f) });
  for (const x of t.loots || []) {
    const a = { w: +x.weight.toFixed(4), min: x.amount?.min, max: x.amount?.max };
    if (x.isOneOfGuaranteedToDrop) a.guaranteed = true;
    if (x.onlyDropsInBiome) a.biome = x.onlyDropsInBiome;
    edge('loot_item', l, `o:${x.objectID}`, a);
  }
}
// spawns (initial per biome folder, respawn)
let spawnFiles = 0;
for (const kind of ['Initial', 'Respawn']) {
  for (const f of listJson(path.join(confDir, 'Spawn', kind))) {
    const s = readJson(f); spawnFiles++;
    const c = s.spawnCheck || {};
    for (const x of s.spawns || []) {
      if (!x.objectID) continue;
      edge('spawns_in', `o:${x.objectID}`, `b:${c.biome ?? 0}`, { kind: kind.toLowerCase(), chance: +(c.spawnChance ?? 0).toPrecision(3), tilesets: c.tilesets || [] });
    }
  }
}

// dump part
let dump = null;
if (fs.existsSync(dumpFile)) {
  dump = readJson(dumpFile);
  if (!(dump.time >= lastUpdated)) { console.warn(`dump.json is older than the installed build; using Conf only (run /ai kgdump again)`); dump = null; }
}
const roleOf = (o) => {
  const r = new Set(), t = o.type || '';
  if (/^(MeleeWeapon|RangeWeapon|SummoningWeapon|ThrowingWeapon|BeamWeapon)$/.test(t)) r.add('weapon');
  if (/^(Shovel|Hoe|MiningPick|FishingRod|BugNet|Sledge|RoofingTool|DrillTool|WaterCan|Bucket|Seeder|PaintTool)$/.test(t)) r.add('tool');
  if (/^(Helm|BreastArmor|PantsArmor|Necklace|Ring|Offhand|Bag|Lantern|Pouch)$/.test(t)) r.add('armor');
  if (t === 'Eatable') r.add('food');
  if (t === 'PlaceablePrefab') r.add('placeable');
  if (/^(Creature|Critter|Pet)$/.test(t)) r.add('creature');
  if (t === 'NonUsable' || t === 'UniqueCraftingComponent') r.add('material');
  if (o.id !== PLAYER && o.canCraft?.length) r.add('station');
  if (o.tt === TT_WALL) r.add('wall');
  if (o.tt === TT_ORE) r.add('ore');
  return [...r];
};
if (dump) {
  for (const o of dump.objects || []) {
    const k = `o:${o.id}`;
    if (o.var && o.var !== 0) { const n = node(k, {}); (n.variations ||= []).push(o.var); continue; }   // v1: variation 0 is the node
    node(k, {
      id: o.id, name: nodes[k]?.name || ids.ObjectID.byId[o.id] || String(o.id), type: o.type, role: roleOf(o),
      ko: o.ko || undefined, en: o.en || undefined, rarity: o.rarity, sell: o.sell, stack: o.stack,
      hp: o.hp, red: o.red, wdmg: o.wdmg, dur: o.dur, craftTime: o.craftTime,
      // weapon (damage from the level entity, like the item tooltip): default-level damage, level index, damage per
      // level, ranged/magic, cooldown (null = the tooltip default: 0.6 s for a ranged weapon, else 0.4 s)
      lv: o.lv, wdmgLv: o.wdmgLv, wRange: o.wRange, wMagic: o.wMagic, rangeWeapon: o.rangeWeapon, cd: o.cd,
      // creature attacks and movement speed
      atk: o.atk, speed: o.speed,
    });
    if ((o.wRange !== undefined || o.wMagic !== undefined) && !(o.wdmg > 0)) unresolved.push([k, 'weapon without damage']);
    if (!o.ko) unresolved.push([k, 'no Korean name']);
    if (o.noPrefab) unresolved.push([k, 'prefab not found']);
    for (const [m, n] of o.req || []) edge('ingredient', `o:${m}`, k, { n });
    for (const [r, n] of o.canCraft || []) {
      if (o.id === PLAYER) edge('hand_craft', `o:${r}`, 'o:6000', { n });
      else edge('crafted_at', `o:${r}`, k, { n });
    }
    const loot = o.dropLoot || o.loot;
    if (loot) edge('uses_loot', k, `l:${loot}`);
    if (o.tt) { const t = `t:${o.ts}:${o.tt}`; node(t, { ts: o.ts, tt: o.tt, name: `${ids['PugTilemap.Tileset'].byId[o.ts] || o.ts}:${ids['PugTilemap.TileType'].byId[o.tt] || o.tt}` }); edge('tile_is', t, k); }
    for (const [c, v, d] of o.eat || []) edge('gives', k, `c:${c}`, { when: 'eat', v, d });
    for (const [c, v] of o.equip || []) edge('gives', k, `c:${c}`, { when: 'equip', v });
  }
  // derived: which pick can mine which wall (estimate, like the mod's tunnel planner)
  const tools = edges.filter((e) => e[0] === 'gives' && e[3].when === 'equip' && e[2] === `c:${COND_MINING}` && e[3].v > 0).map((e) => [e[1], e[3].v]);
  for (const [wk, w] of Object.entries(nodes)) {
    if (!wk.startsWith('o:') || !(w.role || []).includes('wall') || !w.hp) continue;
    for (const [tk, bonus] of tools) {
      const dmg = BASE_MINING + bonus - (w.red || 0);
      if (dmg > 0) edge('mineable_by', wk, tk, { hits: Math.ceil(w.hp / dmg), derived: 'hp/(20+MiningIncrease-red)' });
    }
  }
  for (const e of dump.errors || []) unresolved.push(['dump', e]);
} else {
  unresolved.push(['dump', 'not dumped yet: recipes, stations, stats, effects, tiles and Korean names are missing (/ai kgdump after the in-game tests)']);
}

// hand-written rules (rules.json, in git): base safety, spawning, enemy paths, wall breaking. Not item data, so each
// rule carries its own level (how it is known) and sources. Node r:<id>; rule_about r -> the objects it names.
const RULES = path.join(HERE, 'rules.json');
let rules = null;
if (fs.existsSync(RULES)) {
  rules = readJson(RULES);
  for (const r of rules.rules || []) {
    node(`r:${r.id}`, { id: r.id, topics: r.topics || [], text: r.text, level: r.level, src: r.src || [] });
    for (const n of r.objects || []) {
      const id = ids.ObjectID.byName[n];
      if (id > 0) edge('rule_about', `r:${r.id}`, `o:${id}`); else unresolved.push([`r:${r.id}`, `rule object not found: ${n}`]);
    }
  }
}

const graph = {
  version: 1,
  game: { steamBuildId: +buildid, steamLastUpdated: lastUpdated, appVersion: dump?.appVersion || null },
  built: new Date().toISOString(),
  sources: { conf: { dir: confDir, lootFiles, spawnFiles }, dump: dump ? { time: dump.time, objects: dump.objects?.length, fake: !!dump.fake } : null,
    rules: rules ? { count: rules.rules?.length || 0, checkedFor: rules.checkedFor, levels: rules.levels } : null },
  nodes, edges, unresolved,
};
fs.writeFileSync(path.join(dir, 'graph.json'), JSON.stringify(graph));
fs.writeFileSync(path.join(DATA, 'current.txt'), buildid);
const count = {}; for (const e of edges) count[e[0]] = (count[e[0]] || 0) + 1;
console.log(`graph built for build ${buildid}: nodes ${Object.keys(nodes).length}, edges ${edges.length} ${JSON.stringify(count)}, unresolved ${unresolved.length}${dump ? '' : ' (no dump yet)'} -> ${path.join(dir, 'graph.json')}`);
