// Combat-entry replay (live 22:43, decisions-224158 #105-#131, MushroomEnemy attack:58196 behind a wall).
//   node build.mjs <base/requests.jsonl> <decisions log> <Player.log> <outdir>
// V0 as logged; V1 the new texts: attack options of enemies that have no known way (this build offered a "dig toward"
// option for the enemy's tile, i.e. no open walk) end with AIBaseGuard.NoKnownWay; the running attack's continue says
// "길 찾기 N번 실패(S초째), 대상 체력 변화 없음" (N, S from Player.log's path-failure lines at that time) and drops the
// hits-left estimate (AIBaseGuard.AttackStuckText).
import fs from 'fs';
import path from 'path';
const [reqFile, logFile, playerLog, out] = process.argv.slice(2);
const NO_WAY = '아는 길 없음 — 벽 너머, 본 적 없는 칸을 거쳐야만 이어짐. 고르면 다가가려다 막힘.';
const reqs = fs.readFileSync(reqFile, 'utf8').split('\n').filter(Boolean).map(JSON.parse).filter(r => { const n = +r.id.split('#').pop(); return n >= 105 && n <= 131; });
const kinds = new Map(), times = new Map();
for (const line of fs.readFileSync(logFile, 'utf8').split(/\r?\n/)) {
  const m = line.match(/^t=([\d.]+) @(\w+) DECISION #(\d+) INPUT .*options\(\d+\): (.*)$/);
  if (!m || +m[3] < 105 || +m[3] > 131) continue;
  const map = {};
  for (const x of m[4].matchAll(/(?:^|\|\| |\n| )(c\d+) \[([a-z_]+) ([^\]]*)\]/g)) map[x[1]] = { kind: x[2], key: x[3] };
  kinds.set(+m[3], map); times.set(+m[3], +m[1]);
}
// the attack's path-failure streaks: [{t, n}]
const fails = [];
for (const line of fs.readFileSync(playerLog, 'utf8').split(/\r?\n/)) {
  const m = line.match(/t=([\d.]+) @Gemini SKILL 공격: path to target failed #(\d+)/);
  if (m) fails.push({ t: +m[1], n: +m[2] });
}
function stuckAt(t) {
  let last = null; for (const f of fails) if (f.t <= t) last = f; else break;
  if (!last) return null;
  let i = fails.indexOf(last); while (i > 0 && fails[i].n > 1) i--;
  return { n: last.n, secs: t - fails[i].t };
}
const v0 = [], v1 = [], meta = [];
for (const r of reqs) {
  const n = +r.id.split('#').pop(); const km = kinds.get(n); const t = times.get(n);
  const b0 = r.body; v0.push(b0);
  const b1 = JSON.parse(JSON.stringify(b0)); const q = b1.questions[0];
  const digTiles = new Set(Object.values(km).filter(x => x.kind === 'dig' && /^dig:-?\d+,-?\d+$/.test(x.key)).map(x => x.key.slice(4)));
  for (const c of q.choices) {
    const k = km[c.value]; if (!k) continue;
    if (k.kind === 'attack') {
      const pos = (c.description.match(/적 \w+ \((-?\d+),(-?\d+)\)/) || []);
      if (pos.length && digTiles.has(`${pos[1]},${pos[2]}`)) c.description += ' ' + NO_WAY;
    }
    if (k.kind === 'continue' && k.key.startsWith('attack:')) {
      const st = stuckAt(t);
      c.description = c.description.replace(/\(대상 (\w+) 체력 (\d+)\/(\d+), ([\d.]+)칸, 관찰 기준 약 \d+타\([\d.]+초\) 남음\)/, (all, id, hp, max, d) =>
        st && st.n >= 2 ? `(대상 ${id} 체력 ${hp}/${max}, ${d}칸. 길 찾기 ${st.n}번 실패(${st.secs.toFixed(0)}초째), 대상 체력 변화 없음)` : all);
    }
  }
  v1.push(b1); meta.push({ id: r.id, n, t, map: km, logged: r.logged });
}
fs.mkdirSync(out, { recursive: true });
fs.writeFileSync(path.join(out, 'V0.jsonl'), v0.map(b => JSON.stringify(b)).join('\n') + '\n');
fs.writeFileSync(path.join(out, 'V1.jsonl'), v1.map(b => JSON.stringify(b)).join('\n') + '\n');
fs.writeFileSync(path.join(out, 'meta.json'), JSON.stringify(meta));
const ex = v1[5].questions[0].choices.filter(c => /길 찾기|아는 길 없음/.test(c.description)).map(c => c.value + ': ' + c.description.slice(-140));
console.log(`requests ${v1.length}; example #${meta[5].n}:\n` + ex.slice(0, 3).join('\n'));
