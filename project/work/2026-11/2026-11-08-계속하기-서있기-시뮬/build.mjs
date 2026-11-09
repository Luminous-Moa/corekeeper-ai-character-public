// Build replay variants of the logged requests that offered "continue".
//   node build.mjs <requests.jsonl> <decisions.log> <outdir>
// H = continue of a halted travel run ("멈춰서 다음 행동 선택을 기다림"), O = any other continue.
// Variants: v0 unchanged, v1 continue removed (renumbered like the game), v2 (H only) continue reworded.
import fs from 'fs';
import path from 'path';
const [reqFile, logFile, out] = process.argv.slice(2);
const reqs = fs.readFileSync(reqFile, 'utf8').split('\n').filter(Boolean).map(JSON.parse);
// kinds per request number from the log: "cN [kind key]"
const kinds = new Map();
for (const line of fs.readFileSync(logFile, 'utf8').split(/\r?\n/)) {
  const m = line.match(/DECISION #(\d+) INPUT .*options\(\d+\): (.*)$/);
  if (!m) continue;
  const map = {};
  for (const x of m[2].matchAll(/(?:^|\|\| |\n| )(c\d+) \[([a-z_]+) ([^\]]*)\]/g)) map[x[1]] = { kind: x[2], key: x[3] };
  kinds.set(+m[1], map);
}
const HALT = '멈춰서 다음 행동 선택을 기다림';
const sets = { H: { v0: [], v1: [], v2: [] }, O: { v0: [], v1: [] } };
const meta = {};
for (const r of reqs) {
  const n = +r.id.split('#').pop();
  const choices = r.body.questions[0].choices;
  const km = kinds.get(n) || {};
  const ci = choices.findIndex(c => km[c.value]?.kind === 'continue');
  if (ci < 0) continue;
  const cont = choices[ci];
  const grp = cont.description.includes(HALT) ? 'H' : 'O';
  const base = { id: r.id, logged: r.logged, loggedKind: km[r.logged]?.kind, contKey: km[cont.value].key, grp };
  const add = (v, chs, map) => {
    const body = JSON.parse(JSON.stringify(r.body)); body.questions[0].choices = chs;
    sets[grp][v].push(body);
    (meta[grp + v] ||= []).push({ ...base, map });
  };
  // v0
  add('v0', choices, Object.fromEntries(choices.map(c => [c.value, km[c.value] || { kind: '?' }])));
  // v1: drop continue, renumber
  const rest = choices.filter((_, i) => i !== ci).map((c, i) => ({ old: c.value, value: 'c' + (i + 1), description: c.description }));
  add('v1', rest.map(c => ({ value: c.value, description: c.description })), Object.fromEntries(rest.map(c => [c.value, km[c.old] || { kind: '?' }])));
  if (grp === 'H') {
    const secs = (cont.description.match(/(\d+)초째|방금/) || [])[0] || '';
    const fight = cont.description.startsWith('싸움을 끊고');
    const desc = `제자리에 서 있기 — '통로 탐색'(${km[cont.value].key})은 ${secs.replace('초째', '초째')} 멈춰 있음. 이걸 고르면 움직이지도 싸우지도 않고 그 자리에 계속 서 있으며, 탐색은 다시 움직이지 않음. ` +
      `다시 움직이려면 '멈춘 곳에서 … 다시 출발'·출구·되돌아가기 같은 이동 선택지나 다른 행동을 골라야 함. 멈춰 있으므로 다른 행동으로 바꿔도 잃는 진행은 없음(원래 목적·탐색 기억은 유지).` +
      (fight ? ' 적과의 싸움에서 빠지는 선택이 아님.' : '');
    const chs = choices.map((c, i) => i === ci ? { value: c.value, description: desc } : c);
    add('v2', chs, Object.fromEntries(choices.map(c => [c.value, km[c.value] || { kind: '?' }])));
  }
}
for (const g of Object.keys(sets)) for (const v of Object.keys(sets[g])) {
  const f = path.join(out, `${g}-${v}.jsonl`);
  fs.writeFileSync(f, sets[g][v].map(b => JSON.stringify(b)).join('\n') + '\n');
  console.log(f, sets[g][v].length);
}
fs.writeFileSync(path.join(out, 'meta.json'), JSON.stringify(meta));
