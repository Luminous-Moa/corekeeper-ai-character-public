// Stage-3 (non-combat) replay variants on top of the stage-2 request bodies (v/<set>-S2.jsonl + v/<set>-meta.json S2).
//   node stage3.mjs <set> <variant> <outdir>      variants: E X N T EXNT SHUF
// E    base-edge materials ("edgemat") only while the bag's wall blocks are fewer than the open repair/fill tiles
// X    explore/goto/dig-explore choices that another explore choice dominates (no less new area, no more time) go;
//      "wait" comes back while the step's goal item is flying in (game auto pickup started)
// N    non-combat continues lose the switching-cost sentence (a neutral fact stays); the instruction's
//      "지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다" becomes neutral. Combat continues and I3 stay.
// T    input trimming: closed menu lines, empty expedition fields, duplicate observation/reason text
// SHUF option order shuffled (seeded) and renumbered — order-bias measurement only
// Writes <outdir>/<set>-<variant>.jsonl and merges meta into <outdir>/<set>-meta.json (map: new value -> {kind,key,orig}).
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const [setName, variant, outDir] = process.argv.slice(2);
const R = path.dirname(fileURLToPath(import.meta.url));
const bodies = fs.readFileSync(path.join(R, 'v', `${setName}-S2.jsonl`), 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const metaS2 = JSON.parse(fs.readFileSync(path.join(R, 'v', `${setName}-meta.json`), 'utf8')).S2;
const orig = fs.readFileSync(path.join(R, 'in', `${setName}.jsonl`), 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const COMBAT = new Set(['attack', 'attacknow', 'retreat', 'away', 'step', 'hit', 'combo', 'approach', 'hold']);
const has = v => variant.includes(v) || variant === 'EXNT' && 'EXNT'.includes(v);

function rng(seed) { let s = seed >>> 0; return () => ((s = (s * 1664525 + 1013904223) >>> 0) / 4294967296); }

const COST = /\s*바꾸는 비용: 다른 행동을 고르면 지금 동작이 끝나는 대로 이 일은 멈추고, 지금까지의 진행은 이어지지 않으며 새 행동은 처음부터\([^)]*\) 시작함\./;
const INSTR_OLD = '현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다.';
const INSTR_NEW = '현재 상황과 위험을 함께 본다. 계속하기는 지금 하는 행동을 이어 가고, 다른 행동을 고르면 지금 행동은 멈춘다.';
const EMPTY_FIELDS = ['마지막 결과', '귀환 맥락', '작업 장소', '명시 수량 조건', '실행 경계', '조건 소유 확인'];

function trimInput(input) {
  let lines = input.split('\n');
  lines = lines.filter(l => !(/^현재 경로 생활.*\(닫힘/.test(l)) && !/^가방 선택 메뉴: 닫힘/.test(l) && !/^장비 정비: 자기 pending 없음/.test(l));
  const obs = (lines.find(l => l.startsWith('최근 실행 관측: ')) || '').slice('최근 실행 관측: '.length).replace(/\.+$/, '');
  lines = lines.map(l => {
    if (l.startsWith('원정: ')) for (const f of EMPTY_FIELDS) l = l.replace(new RegExp(`; ${f} (?=;)`, 'g'), '').replace(new RegExp(`; ${f} $`), '');
    if (l.startsWith('작업 관측: ')) {
      let rest = l.slice('작업 관측: '.length).replace(/^(\s*\/\s*)+/, '').replace(/^\.\s*/, '');
      const core = obs.replace(/^\[[^\]]*\]\s*/, '');
      if (core && rest.startsWith(core)) rest = rest.slice(core.length).replace(/^\.+\s*/, '');
      l = rest.trim() ? '작업 관측: ' + rest.trim() : null;
    }
    if (l && l.startsWith('지금 다시 판단하는 이유: ') && obs) {
      const why = l.slice('지금 다시 판단하는 이유: '.length).replace(/\.+$/, '');
      if (why === obs) l = '지금 다시 판단하는 이유: 위 최근 실행 관측.';
    }
    return l;
  }).filter(l => l != null);
  return lines.join('\n');
}

const outBodies = [], outMeta = [];
const rand = rng(20261108);
bodies.forEach((b0, i) => {
  const b = JSON.parse(JSON.stringify(b0)); const m = metaS2[i]; const q = b.questions[0];
  let ch = q.choices.map(c => ({ ...c, m: m.map[c.value] }));
  const kindsAll = new Set(ch.map(c => c.m?.kind));
  const combat = [...kindsAll].some(k => COMBAT.has(k));
  if (has('E')) {
    const all = ch.map(c => c.description).join('\n') + '\n' + b.input;
    const hv = all.match(/가진 벽 블록 (\d+)개/); const have = hv ? +hv[1] : null;
    let need = 0;
    for (const c of ch) if ((c.m?.kind === 'edgerepair' || c.m?.kind === 'edgefill') && !/_out$/.test(c.m.orig || c.m.key || '')) { const n = c.description.match(/블록 (\d+)개를/); if (n) need += +n[1]; }
    if (have != null && have >= need) ch = ch.filter(c => c.m?.kind !== 'edgemat');
  }
  if (has('X')) {
    const ex = ch.filter(c => /^(탐험 이동|탐험 출발|통로 탐색)/.test(c.description)).map(c => ({ c, n: +((c.description.match(/새로 보일 구역 (\d+)칸/) || [])[1] ?? NaN), t: +((c.description.match(/예상 약 (\d+)초/) || [])[1] ?? NaN) })).filter(x => !isNaN(x.n) && !isNaN(x.t));
    const drop = new Set(ex.filter(a => ex.some(o => o !== a && o.n >= a.n && o.t <= a.t && (o.n > a.n || o.t < a.t))).map(a => a.c));
    ch = ch.filter(c => !drop.has(c));
    const incoming = ch.some(c => /날아오는 중\(게임 자동 줍기/.test(c.description) && /목표 품목/.test(c.description));
    if (incoming && !ch.some(c => c.m?.kind === 'wait')) {
      const o = orig[i].questions[0].choices.find(c => /^기다리기 —/.test(c.description));
      if (o) ch.push({ value: 'x', description: o.description, m: { kind: 'wait', key: 'wait', orig: o.value } });
    }
  }
  if (has('N')) {
    for (const c of ch) if (c.m?.kind === 'continue' && !COMBAT.has((c.m.key || '').split(':')[0]) && !/^(attack|attacknow|combo|hit|step|away|retreat|approach|hold)/.test(c.m.key || ''))
      c.description = c.description.replace(COST, ' 다른 행동을 고르면 이 일은 멈춤.');
    q.instructions = q.instructions.replace(INSTR_OLD, INSTR_NEW);
  }
  if (has('T')) b.input = trimInput(b.input);
  // V (review 작은 것 4): one direct remembered goal ore per vein — a synthesized direct ore next to an earlier one goes
  if (variant.includes('V')) {
    const kept = [];
    ch = ch.filter(c => {
      if (c.m?.kind !== 'dig' || c.m.orig) return true;
      const mm = (c.m.key || '').match(/dig:(-?\d+),(-?\d+)/); if (!mm) return true;
      const x = +mm[1], y = +mm[2];
      if (kept.some(([a, b2]) => Math.abs(a - x) <= 1 && Math.abs(b2 - y) <= 1)) return false;
      kept.push([x, y]); return true;
    });
  }
  if (variant === 'SHUF') { for (let k = ch.length - 1; k > 0; k--) { const j = Math.floor(rand() * (k + 1)); [ch[k], ch[j]] = [ch[j], ch[k]]; } }
  const map = {};
  q.choices = ch.map((c, k) => { const v = 'c' + (k + 1); map[v] = c.m || { kind: '?' }; return { value: v, description: c.description }; });
  outBodies.push(b); outMeta.push({ id: m.id, map, combat, after: q.choices.length });
});
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, `${setName}-${variant}.jsonl`), outBodies.filter(b => b.questions[0].choices.length > 1).map(b => JSON.stringify(b)).join('\n') + '\n');
// single-option requests are not sent (the replay tool needs 2+); keep their index mapping
const sentIdx = []; outBodies.forEach((b, k) => { if (b.questions[0].choices.length > 1) sentIdx.push(k); });
const mf = path.join(outDir, `${setName}-meta.json`);
const all = fs.existsSync(mf) ? JSON.parse(fs.readFileSync(mf, 'utf8')) : {};
all[variant] = { meta: outMeta, sentIdx };
fs.writeFileSync(mf, JSON.stringify(all));
const chars = a => a.reduce((s, b) => s + b.input.length + b.questions[0].instructions.length + b.questions[0].choices.reduce((t, c) => t + c.description.length, 0), 0);
console.log(`${setName}-${variant}: ${outBodies.length} requests (${sentIdx.length} sent), options ${(outBodies.reduce((s, b) => s + b.questions[0].choices.length, 0) / outBodies.length).toFixed(1)}/req, chars ${chars(bodies)} -> ${chars(outBodies)}`);
