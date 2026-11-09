// 100-case Korean comparison: gpt-6-luna vs celeris-1.1-decision on real logged decision requests (<= MAX_TOKENS).
// Usage: node run100.mjs <logDir> <outDir> [maxTokens=10000] [total=100]
// Keys from env OPENAI_API_KEY / CELERIS_API_KEY (never printed).
import fs from "node:fs";
import path from "node:path";
import readline from "node:readline";
import https from "node:https";

const [LOGDIR, OUTDIR, MAXT = "10000", TOTAL = "100"] = process.argv.slice(2);
const MAX_TOKENS = Number(MAXT), N_TOTAL = Number(TOTAL);
const EXCLUDE = new Set([33, 36, 39, 78, 81, 87, 92, 95, 99, 103].map(n => `decisions-113324.log#${n}`));

// Instruction text by build period (git: 28bc573 11-04 09:32 → b1facaa 11-07 14:01 → current).
const INSTR_V2 = n => `목표와 지금 상황에서 ${n}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).`;
const INSTR_CUR = n => `목표와 지금 상황에서 ${n}가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.`;
const instrFor = (log, name) => (fs.statSync(path.join(LOGDIR, log)).mtime < new Date("2026-11-07T05:01:00Z") ? INSTR_V2 : INSTR_CUR)(name);

// Seeded RNG for reproducible sampling and A/B assignment.
let seed = 20261108;
const rnd = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };

const reIn = /^t=([\d.]+) @(\S+) DECISION #(\d+) INPUT rev=\S+ trigger='([^']*)'(.*?) input=\[(.*)\] options\((\d+)\): \|\| (.*)$/;
const reRes = /DECISION #(\d+) RESULT choice=(\S+) .*?tokens=(\d+)/;

function classify(trigger, extra, text, cands) {
  const goal = (/(?:^|\n)목표: ([^\n]*)/.exec(text) || [, ""])[1];
  const t = trigger, vals = cands.map(c => c.value + " " + c.description.slice(0, 40)).join(" ");
  // combat first (any enemy-driven re-decision, with or without a goal), then menu, then the decision right after a goal change
  if (/자기 방어|긴급|새 적|피격|적 .*칸|위협|싸움|전투|체력 감소/.test(t) || /close-fight/.test(extra) || /자기 방어 중/.test(text)) return { cat: "전투", goal };
  if (/가방 선택 단계|건네기|장비/.test(t) || /건네기|가방|장비|입기/.test(goal)) return { cat: "가방·장비·건네기", goal };
  if (/새 목표|목표 바뀜/.test(t) || /따라가기|따라와/.test(goal)) return { cat: "따라가기·목표 전환", goal };
  if (/짓기|지키기|둘레|건축|막기/.test(goal) || /edgerepair|edgemat|build_/.test(vals)) return { cat: "기지·건축", goal };
  if (/모으기|캐기|채집|확보/.test(goal)) return { cat: "채집·캐기", goal };
  if (/탐험|돌아가기|가기|이동|원정/.test(goal)) return { cat: "탐험·이동", goal };
  if (/만들기|제작|준비/.test(goal)) return { cat: "제작·준비", goal };
  return { cat: "생활·기타", goal };
}

async function parseLog(log) {
  const rl = readline.createInterface({ input: fs.createReadStream(path.join(LOGDIR, log), "utf8"), crlfDelay: Infinity });
  const pending = new Map(), out = [];
  for await (const line of rl) {
    let m = reIn.exec(line);
    if (m) { pending.set(Number(m[3]), m); continue; }
    m = reRes.exec(line);
    if (!m) continue;
    const n = Number(m[1]), tokens = Number(m[3]), im = pending.get(n);
    pending.delete(n);
    if (!im || tokens > MAX_TOKENS || EXCLUDE.has(`${log}#${n}`)) continue;
    const [, t, name, , trigger, extra, rawText, cnt, rest] = im;
    const cands = [];
    for (const part of rest.split(" || ")) {
      const mm = /^(\S+) \[[^\]]*\] ([\s\S]*)$/.exec(part);
      if (mm) cands.push({ value: mm[1], description: mm[2].replaceAll(" ⏎ ", "\n") });
    }
    if (cands.length !== Number(cnt) || cands.length < 2 || new Set(cands.map(c => c.value)).size !== cands.length) continue;
    const text = rawText.replaceAll(" ⏎ ", "\n");
    const { cat, goal } = classify(trigger, extra, text, cands);
    out.push({ id: `${log}#${n}`, log, n, t: Number(t), name, trigger, goal, cat, tokens, loggedChoice: m[2], text, cands });
  }
  return out;
}

const logs = fs.readdirSync(LOGDIR).filter(f => /^decisions-\d+\.log$/.test(f) && fs.statSync(path.join(LOGDIR, f)).size > 0);
let pool = [];
for (const log of logs) { const r = await parseLog(log); pool.push(...r); console.log(`parsed ${log}: ${r.length} usable`); }

// Stratified sampling: equal share per category, round-robin over logs, >= 30 game-seconds apart within a log.
const CATS = ["전투", "채집·캐기", "탐험·이동", "제작·준비", "기지·건축", "가방·장비·건네기", "따라가기·목표 전환", "생활·기타"];
const byCat = Object.fromEntries(CATS.map(c => [c, pool.filter(p => p.cat === c)]));
console.log("pool by category:", Object.fromEntries(CATS.map(c => [c, byCat[c].length])));
const picked = [], usedT = {};
const farEnough = p => !(usedT[p.log] || []).some(t => Math.abs(t - p.t) < 30);
function take(cat, k) {
  const byLog = {};
  for (const p of byCat[cat]) (byLog[p.log] ||= []).push(p);
  for (const l in byLog) byLog[l].sort(() => rnd() - 0.5);
  const order = Object.keys(byLog).sort(() => rnd() - 0.5);
  let got = 0, progress = true;
  while (got < k && progress) {
    progress = false;
    for (const l of order) {
      if (got >= k) break;
      const arr = byLog[l];
      while (arr.length) { const p = arr.shift(); if (farEnough(p)) { picked.push(p); (usedT[p.log] ||= []).push(p.t); got++; progress = true; break; } }
    }
  }
  return got;
}
let remaining = N_TOTAL, catsLeft = [...CATS].sort((a, b) => byCat[a].length - byCat[b].length);
while (catsLeft.length) { const c = catsLeft.shift(); remaining -= take(c, Math.floor(remaining / (catsLeft.length + 1))); }
console.log(`picked ${picked.length}`);

// --- calls ---
const agent = new https.Agent({ keepAlive: true, maxSockets: 1 });
const PROVIDERS = {
  openai: { host: "api.openai.com", path: "/v1/decisions", model: "gpt-6-luna", key: process.env.OPENAI_API_KEY },
  celeris: { host: "inference.celeris.ai", path: "/celeris-1-decision/v1/decisions", model: "celeris-1.1-decision", key: process.env.CELERIS_API_KEY },
};
for (const [p, v] of Object.entries(PROVIDERS)) if (!v.key) { console.error(`missing key for ${p}`); process.exit(1); }
function call(prov, body) {
  const P = PROVIDERS[prov];
  const payload = Buffer.from(JSON.stringify({ ...body, model: P.model }), "utf8");
  return new Promise(resolve => {
    const t0 = performance.now();
    const req = https.request({ host: P.host, path: P.path, method: "POST", agent, timeout: 30000,
      headers: { Authorization: `Bearer ${P.key}`, "Content-Type": "application/json", "Content-Length": payload.length } }, res => {
      const chunks = [];
      res.on("data", c => chunks.push(c));
      res.on("end", () => {
        const ms = Math.round(performance.now() - t0), data = Buffer.concat(chunks).toString("utf8");
        if (res.statusCode !== 200) return resolve({ error: res.statusCode, body: data.slice(0, 300), ms });
        try {
          const j = JSON.parse(data), a = j.answers[0];
          const top = [...a.probabilities].sort((x, y) => y.probability - x.probability).slice(0, 3).map(p => [p.value, p.probability]);
          resolve({ ms, choice: a.choice, confidence: a.confidence, top, input_tokens: j.usage.input_tokens });
        } catch (e) { resolve({ error: "parse", body: data.slice(0, 300), ms }); }
      });
    });
    req.on("timeout", () => req.destroy(new Error("timeout")));
    req.on("error", e => resolve({ error: String(e.message) }));
    req.end(payload);
  });
}
const bodyOf = p => ({ input: p.text, questions: [{ type: "choice", name: "action", instructions: instrFor(p.log, p.name), choices: p.cands }] });
for (const prov of Object.keys(PROVIDERS)) await call(prov, bodyOf(picked[0]));

const results = [];
for (const [i, p] of picked.entries()) {
  const row = { id: p.id, cat: p.cat, trigger: p.trigger, goal: p.goal, candidates: p.cands.length, loggedTokens: p.tokens, loggedChoice: p.loggedChoice,
    instr: fs.statSync(path.join(LOGDIR, p.log)).mtime < new Date("2026-11-07T05:01:00Z") ? "v2(11-04~11-07)" : "current", runs: {} };
  for (const prov of Object.keys(PROVIDERS)) row.runs[prov] = [await call(prov, bodyOf(p)), await call(prov, bodyOf(p))];
  results.push(row);
  if (i % 10 === 9) console.log(`done ${i + 1}/${picked.length}`);
}

// --- outputs ---
fs.mkdirSync(OUTDIR, { recursive: true });
fs.writeFileSync(path.join(OUTDIR, "cases.json"), JSON.stringify(picked.map(p => ({ ...p, instructions: instrFor(p.log, p.name) })), null, 1));
fs.writeFileSync(path.join(OUTDIR, "results.json"), JSON.stringify(results, null, 1));
const diff = results.filter(r => r.runs.openai[0].choice && r.runs.celeris[0].choice && r.runs.openai[0].choice !== r.runs.celeris[0].choice);
const key = {}, blind = [];
diff.forEach((r, i) => {
  const p = picked.find(x => x.id === r.id), swap = rnd() < 0.5;
  const [pa, pb] = swap ? ["celeris", "openai"] : ["openai", "celeris"];
  const caseId = `K${String(i + 1).padStart(3, "0")}`;
  key[caseId] = { id: r.id, A: pa, B: pb };
  blind.push({ case: caseId, category: r.cat, trigger: r.trigger, instructions: instrFor(p.log, p.name), input: p.text, choices: p.cands,
    A_choice: r.runs[pa][0].choice, B_choice: r.runs[pb][0].choice });
});
fs.writeFileSync(path.join(OUTDIR, "ab-key.json"), JSON.stringify(key, null, 1));
const nFiles = Math.max(1, Math.round(blind.length / 25));
for (let f = 0; f < nFiles; f++) {
  const part = blind.filter((_, i) => i % nFiles === f);
  fs.writeFileSync(path.join(OUTDIR, `blind-${f + 1}.json`), JSON.stringify(part, null, 1));
}

// --- summary ---
const q = (v, p) => { if (!v.length) return null; const s = [...v].sort((a, b) => a - b); return s[Math.min(s.length - 1, Math.floor(p * s.length))]; };
function stats(rows) {
  const o = { n: rows.length, agree: rows.filter(r => r.runs.openai[0].choice && r.runs.openai[0].choice === r.runs.celeris[0].choice).length };
  for (const prov of Object.keys(PROVIDERS)) {
    const ms = rows.flatMap(r => r.runs[prov]).filter(x => !x.error).map(x => x.ms);
    o[prov] = { p50: q(ms, 0.5), p95: q(ms, 0.95), max: ms.length ? Math.max(...ms) : null,
      unstable: rows.filter(r => r.runs[prov][0].choice !== r.runs[prov][1].choice).length,
      errors: rows.flatMap(r => r.runs[prov]).filter(x => x.error).length };
  }
  o.openaiTokenMatch = rows.filter(r => r.runs.openai[0].input_tokens === r.loggedTokens).length;
  return o;
}
const summary = { all: stats(results), byCat: Object.fromEntries(CATS.map(c => [c, stats(results.filter(r => r.cat === c))])), disagreements: diff.length, blindFiles: nFiles };
fs.writeFileSync(path.join(OUTDIR, "summary.json"), JSON.stringify(summary, null, 1));
console.log(JSON.stringify(summary, null, 1));
