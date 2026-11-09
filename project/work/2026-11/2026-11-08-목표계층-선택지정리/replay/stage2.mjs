// Offline approximation of the stage-2 field choice surface (AIFieldChoices + no-effect continue removal + remembered goal
// ores as direct choices + I3) applied to logged requests. Writes request bodies for Tools/decisions-compare/compare.mjs.
//   node stage2.mjs <set name> <in.jsonl of bodies> <meta.json: [{id, map:{value:{kind,key}}}]> <outdir> [memory.json]
// Variants: S1 = stage 1 only (finish_goal removed), S2 = stage 2 (this file's rules). Choice values are renumbered c1..cN
// like the game; the meta keeps new value -> original value.
import fs from 'fs';
import path from 'path';

const [setName, inFile, metaFile, outDir, memFile] = process.argv.slice(2);
const bodies = fs.readFileSync(inFile, 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const metas = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
const mem = memFile ? JSON.parse(fs.readFileSync(memFile, 'utf8')) : null;

const I3 = ' 몇 번 맞으면 쓰러지는지와 가는 쪽이 적 쪽인지를 보고, 쓰러질 위험이 크면 적에게서 멀어지는 행동을 앞에 둔다.';
const GATHER = new Set(['mine', 'break', 'root', 'pickup', 'gather_group']);
const FOOD = /^(FoodRation|Mushroom|HeartBerry|BombPepper|Carrock|GlowingTulip|Pewpaya|Cooked\w*|Larva\w*Meat|\w*Meat|\w*Fish)$/;
const MENUFREE = new Set(['gather', 'explore', 'follow', 'goto', 'home']);

function goalOf(input) {
  const m = input.match(/\n목표: (.+?)(?:\.\n| — 지금)/) || input.match(/목표: (.+?)\./);
  const t = m ? m[1] : '없음';
  let r;
  if ((r = t.match(/^\S*\((\w+)\) \d+개 모으기/))) return { kind: 'gather', item: r[1], text: t };
  if ((r = t.match(/^\S*\((\w+)\) \d+개 만들기/))) return { kind: 'craft', item: r[1], text: t };
  if (/^건네기 /.test(t)) return { kind: 'give', item: (t.match(/^건네기 (\w+)/) || [])[1] || '', text: t };
  if (/따라가기/.test(t)) return { kind: 'follow', item: '', text: t };
  if (/탐험/.test(t)) return { kind: 'explore', item: '', text: t };
  if (/^없음/.test(t)) return { kind: 'none', item: '', text: t };
  if (/기지로|돌아가기/.test(t)) return { kind: 'home', item: '', text: t };
  return { kind: 'other', item: '', text: t };
}
function wantedOf(input, g) {
  const w = new Set(); if (g.item && (g.kind === 'gather' || g.kind === 'craft')) w.add(g.item);
  const prep = (input.match(/준비 길\(제작\): ([^\n]*)/) || [])[1] || '';
  for (const m of prep.matchAll(/(\w+) \d+개 더 필요/g)) w.add(m[1]);
  for (const m of prep.matchAll(/(\w+) (\d+)\/(\d+)/g)) if (+m[2] < +m[3]) w.add(m[1]);
  return w;
}
// the build's own wanted list is printed on root options ("필요한 재료: Wood, CopperOre")
function wantedFromChoices(choices, w) {
  for (const c of choices) { const m = c.description.match(/필요한 재료: ([\w, ]+)/); if (m) for (const x of m[1].split(', ')) w.add(x.trim()); }
}
function productsOf(kind, desc) {
  let m;
  if (kind === 'dig' && (m = desc.match(/^기억한 광석 쪽으로 — (\w+)/))) return [m[1]];
  if (kind === 'root') return ['Wood'];
  if ((m = desc.match(/부수면 (\w+) \+ 광석 (\w+)/))) return [m[2]];   // an ore wall gives its ore (AIGathering.ProductOf)
  if ((m = desc.match(/부수면 (\w+)/))) return [m[1]];
  if ((m = desc.match(/캐기 — 벽 (\w+)/))) return [m[1]];
  if ((m = desc.match(/떨어진 (\w+)/))) return [m[1]];
  if ((m = desc.match(/줍기 — (\w+)/))) return [m[1]];
  return [];
}
function noEffectContinue(c) {
  return /이어 하기 \(멈춰서 다음 행동 선택을 기다림\)/.test(c.description) || c.description.startsWith('제자리에 서 있기 — ')
    || /계속하기 — 답이 오기 전에 끝남/.test(c.description) || /출구 선택을 기다림\. 계속하기를 골라도/.test(c.description);
}
function navOp(key) { const p = (key || '').split(':'); return p.length > 2 ? p[2] : ''; }

function remembered(input, item) {
  if (!mem || item !== 'CopperOre') return [];
  const pos = input.match(/위치 \((-?\d+),(-?\d+)\)/); if (!pos) return [];
  const x0 = +pos[1], y0 = +pos[2], out = [];
  for (let i = 0; i < mem.oreId.length; i++) {
    if (mem.oreId[i] !== 1500) continue;
    const x = mem.oreXY[2 * i], y = mem.oreXY[2 * i + 1];
    if (Math.abs(x - x0) <= 15 && Math.abs(y - y0) <= 12) continue;   // on screen: not a remembered choice
    out.push({ x, y, d: Math.hypot(x - x0, y - y0) });
  }
  out.sort((a, b) => a.d - b.d);
  return out.slice(0, 3).map(o => ({ kind: 'dig', key: `dig:${o.x},${o.y}`,
    description: `기억한 광석 쪽으로 — CopperOre (${o.x},${o.y}) ${o.d.toFixed(1)}칸, 최근에 봄(지금은 화면 밖이라 지금 상태는 모름). 길이 있으면 걷고 막히면 벽을 뚫으며 감.` }));
}

function stage2(body, map) {
  const input = body.input, g = goalOf(input), wanted = wantedOf(input, g);
  const hunger = +((input.match(/배고픔 (\d+)/) || [])[1] ?? 100);
  const bag = (input.match(/\n가방: ([^\n]*)/) || [])[1] || '';
  const foodNeed = hunger < 50 && !bag.split(', ').some(s => FOOD.test(s.split(' ')[0]));
  wantedFromChoices(body.questions[0].choices, wanted);
  let ch = body.questions[0].choices.map(c => ({ ...c, kind: map[c.value]?.kind || '?', key: map[c.value]?.key || '', orig: c.value }));
  const removed = {};
  const drop = (c, why) => { c.removed = true; removed[why] = (removed[why] || 0) + 1; };
  for (const c of ch) {
    if (c.kind === 'finish_goal') drop(c, 'finish_goal');
    else if (c.kind === 'continue' && noEffectContinue(c)) drop(c, 'no-effect continue');
  }
  const listOpened = ch.some(c => c.kind === 'expedition_nav' && navOp(c.key) !== 'open');
  const lifeOpen = ch.some(c => c.kind === 'life_nav' && navOp(c.key) !== 'open');
  const bagActive = ch.some(c => c.kind === 'inventory_menu' && !/:root:enter:/.test(c.key));
  if (g.kind !== 'none' && !listOpened && !lifeOpen && !bagActive) {
    // remembered goal ores as direct choices (before the first explore choice)
    const rem = remembered(input, g.item);
    if (rem.length) { const at = ch.findIndex(c => c.kind === 'explore'); ch.splice(at < 0 ? ch.length : at, 0, ...rem.map(r => ({ ...r, value: '', orig: null }))); }
    for (const c of ch) {
      if (c.removed) continue;
      const isG = GATHER.has(c.kind) || c.kind === 'dig' && /^기억한 광석 쪽으로/.test(c.description);
      if (isG) {
        const pr = productsOf(c.kind, c.description);
        const serve = pr.filter(p => wanted.has(p));
        if (serve.length) { c.description += ' (' + serve.map(p => p === g.item ? `주요 목표 ${p} 나옴` : `주요 목표 재료 ${p} 나옴`).join('; ') + ')'; c.serves = true; c.visible = c.kind !== 'dig'; continue; }
        if (foodNeed && pr.some(p => FOOD.test(p))) continue;
        drop(c, '무관 채집 ' + c.kind);
      } else if ((c.kind === 'life_nav' || c.kind === 'inventory_menu') && MENUFREE.has(g.kind)) drop(c, c.kind);
    }
    const NOTWORK = new Set(['wait', 'eat', 'stop_action', 'request_plan', 'finish_goal', 'life_nav', 'inventory_menu', 'expedition_nav']);
    if (g.kind !== 'follow' && ch.some(c => !c.removed && !NOTWORK.has(c.kind))) for (const c of ch) if (!c.removed && c.kind === 'wait') drop(c, 'wait');
    const directGoal = ch.some(c => !c.removed && c.serves);
    for (const c of ch) if (!c.removed && c.kind === 'expedition_nav' && navOp(c.key) === 'open' && (directGoal || g.kind === 'follow')) drop(c, 'expedition_nav open');
    const inView = ch.some(c => !c.removed && c.serves && c.visible);
    if ((g.kind === 'gather' || g.kind === 'craft') && wanted.size && !inView) {
      const names = [...wanted].sort(); if (g.item && names.includes(g.item)) { names.splice(names.indexOf(g.item), 1); names.unshift(g.item); }
      const what = names.slice(0, 3).join('·');
      for (const c of ch) if (!c.removed && (c.kind === 'explore' || c.kind === 'travel_turn')) c.description += ` 화면에 목표 ${what} 없음 — 목표 ${what} 탐색을 위한 이동.`;
    }
  }
  ch = ch.filter(c => !c.removed);
  const out = JSON.parse(JSON.stringify(body));
  out.questions[0].instructions = body.questions[0].instructions.endsWith(I3.trim()) ? body.questions[0].instructions : body.questions[0].instructions + I3;
  const newMap = {};
  out.questions[0].choices = ch.map((c, i) => { const v = 'c' + (i + 1); newMap[v] = { kind: c.kind, key: c.key, orig: c.orig }; return { value: v, description: c.description }; });
  return { out, newMap, removed, goal: g };
}
function stage1(body, map) {
  const out = JSON.parse(JSON.stringify(body)); const newMap = {}; let i = 0;
  out.questions[0].choices = body.questions[0].choices.filter(c => map[c.value]?.kind !== 'finish_goal').map(c => { const v = 'c' + (++i); newMap[v] = { kind: map[c.value]?.kind || '?', key: map[c.value]?.key || '', orig: c.value }; return { value: v, description: c.description }; });
  return { out, newMap };
}

const S1 = [], S2 = [], meta = { S1: [], S2: [] }, removedAll = {};
bodies.forEach((b, i) => {
  const m = metas[i];
  const a = stage1(b, m.map), z = stage2(b, m.map);
  S1.push(a.out); S2.push(z.out);
  meta.S1.push({ id: m.id, map: a.newMap, before: b.questions[0].choices.length, after: a.out.questions[0].choices.length });
  meta.S2.push({ id: m.id, map: z.newMap, before: b.questions[0].choices.length, after: z.out.questions[0].choices.length, removed: z.removed, goal: z.goal });
  for (const [k, n] of Object.entries(z.removed)) removedAll[k] = (removedAll[k] || 0) + n;
});
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, `${setName}-S1.jsonl`), S1.map(x => JSON.stringify(x)).join('\n') + '\n');
fs.writeFileSync(path.join(outDir, `${setName}-S2.jsonl`), S2.map(x => JSON.stringify(x)).join('\n') + '\n');
fs.writeFileSync(path.join(outDir, `${setName}-meta.json`), JSON.stringify(meta));
const avg = k => (meta[k].reduce((s, x) => s + x.after, 0) / meta[k].length).toFixed(1);
console.log(`${setName}: ${bodies.length} requests; options before ${(meta.S2.reduce((s, x) => s + x.before, 0) / bodies.length).toFixed(1)} -> S1 ${avg('S1')} -> S2 ${avg('S2')}; removed ${JSON.stringify(removedAll)}`);
