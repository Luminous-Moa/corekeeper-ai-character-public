// Replay variants for the finish_goal loop (decisions-202006.log) and a control of accepted finishes.
//   node build.mjs <requests.jsonl> <decisions.log> <outdir> <prefix> <mode: stuck|accepted> [sampleEvery]
// F0 unchanged · F1 finish text rewritten · F3 "goal search move" wording on the list-open and explore options · F13 both
import fs from 'fs';
import path from 'path';
const [reqFile, logFile, out, prefix, mode, everyArg] = process.argv.slice(2);
const every = +(everyArg || 1);
const reqs = fs.readFileSync(reqFile, 'utf8').split('\n').filter(Boolean).map(JSON.parse);
const kinds = new Map(), triggers = new Map();
for (const line of fs.readFileSync(logFile, 'utf8').split(/\r?\n/)) {
  const m = line.match(/@(\w+) DECISION #(\d+) INPUT rev=\d+ trigger='(.*?)' running=.*options\(\d+\): (.*)$/);
  if (!m) continue;
  const id = m[1] + '#' + m[2];
  const map = {};
  for (const x of m[4].matchAll(/(?:^|\|\| |\n| )(c\d+) \[([a-z_]+) ([^\]]*)\]/g)) map[x[1]] = { kind: x[2], key: x[3] };
  kinds.set(id, map); triggers.set(id, m[3]);
}
const REJECT = '단계 완료 선택을 현재 사실로 재검사함';
function finishText(goal, have, need) {
  const prog = need ? ` 지금 확인된 수량 ${have}/${need}${have < need ? ' — 아직 부족해서 이걸 고르면 완료되지 않음' : ''}.` : '';
  return `단계 완료 처리 — '${goal}'이(가) 이미 끝났을 때 고름.${prog} 프로그램이 수량·수령·기능 결과를 확인해 맞으면 다음 계획 단계로 넘어가고, 아니면 완료되지 않은 채 몸은 움직이지 않고 같은 단계에서 다시 판단함. ` +
    `상태를 확인하는 행동이 아님(지금 수량은 위 목표 줄에 있음). 측정 완료 조건이 없는 탐색·따르기는 이걸로 직접 끝냄. 원래 부탁 완료는 위쪽 LLM이 판단.`;
}
const sets = { F0: [], F1: [], F3: [], F13: [] }, meta = { F0: [], F1: [], F3: [], F13: [] };
let i = 0;
for (let k = 0; k < reqs.length; k++) {
  const r = reqs[k];
  const id = r.id.split(':').pop();
  const km = kinds.get(id) || {};
  if (km[r.logged]?.kind !== 'finish_goal') continue;
  const next = reqs[k + 1] && triggers.get(reqs[k + 1].id.split(':').pop()) || '';
  const accepted = !next.includes(REJECT);
  if ((mode === 'accepted') !== accepted) continue;
  if (i++ % every) continue;
  const input = r.body.input;
  const g = input.match(/목표: (.+?) — 지금 (\d+)\/(\d+)\./) || input.match(/목표: (.+?)\./);
  const goal = g ? g[1] : '?'; const have = g && g[2] != null ? +g[2] : null; const need = g && g[3] != null ? +g[3] : null;
  const item = (goal.match(/^(\w+)\(/) || [])[1] || goal;
  const mem = +((input.match(/그중 목표 품목 광석 (\d+)곳/) || [])[1] || 0);
  const choices = r.body.questions[0].choices;
  const f1 = c => km[c.value]?.kind === 'finish_goal' ? { value: c.value, description: finishText(goal, have, need) } : c;
  const f3 = c => {
    const kd = km[c.value]?.kind;
    if (kd === 'expedition_nav' && c.description.startsWith('채집·이동 목록 펼치기'))
      return { value: c.value, description: c.description + (mem ? ` · 목표 품목 ${item} 탐색을 위한 이동 대상 포함: 기억한 ${item} ${mem}곳(지금은 화면 밖). 펼친 뒤 장소를 골라야 이동함.` : '') };
    if (kd === 'explore' && /^(통로 탐색|탐험 출발|탐험 이동)/.test(c.description))
      return { value: c.value, description: c.description + ` 화면에 ${item}이(가) 없을 때 ${item} 탐색을 위한 이동(새 구역을 봄).` };
    return c;
  };
  const add = (v, chs) => { const b = JSON.parse(JSON.stringify(r.body)); b.questions[0].choices = chs; sets[v].push(b); meta[v].push({ id: r.id, map: km }); };
  add('F0', choices); add('F1', choices.map(f1)); add('F3', choices.map(f3)); add('F13', choices.map(c => f3(f1(c))));
}
for (const v of Object.keys(sets)) { fs.writeFileSync(path.join(out, `${prefix}-${v}.jsonl`), sets[v].map(b => JSON.stringify(b)).join('\n') + '\n'); console.log(prefix, v, sets[v].length); }
fs.writeFileSync(path.join(out, `${prefix}-meta.json`), JSON.stringify(meta));
