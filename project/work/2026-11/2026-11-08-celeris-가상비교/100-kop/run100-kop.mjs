// KO' (Korean rewording) rerun of the 100 Korean cases: gpt-6-luna vs celeris-1.1-decision, 2 runs each after warm-up.
// Inputs: ../100-ko/cases.json, ../100-ko/results.json, ../100-en/results-en.json, ../100-en/label-key.json, tr/kop-pre.json
// Outputs: cases-kop.json, results-kop.json, summary-kop.json, label-p1.json, label-p2.json, label-p-key.json
// Keys from env OPENAI_API_KEY / CELERIS_API_KEY (never printed). Pass --no-send to rebuild labels from results-kop.json.
import fs from "node:fs";
import path from "node:path";
import https from "node:https";
import { fileURLToPath } from "node:url";

const DIR = path.dirname(fileURLToPath(import.meta.url));
const rd = p => JSON.parse(fs.readFileSync(path.join(DIR, p), "utf8"));
const KO = rd("../100-ko/cases.json"), KOR = rd("../100-ko/results.json"), ENR = rd("../100-en/results-en.json"), LKEY = rd("../100-en/label-key.json"), KPP = rd("tr/kop-pre.json");
const NOSEND = process.argv.includes("--no-send");

const INSTR_V2_KO = n => `목표와 지금 상황에서 ${n}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).`;
const INSTR_V2_KOP = n => `목표와 현재 상황을 보고 ${n}가 다음에 할 행동을 하나 선택하시오. 선택지는 모두 지금 바로 시작할 수 있는 행동이다. 진행 중인 행동(계속 진행)이 아직 알맞다면 그대로 이어 가고, 새로운 사실이 생겼을 때 바꾼다(바꾸면 새 행동은 처음부터 시작된다).`;
const INSTR_CUR_KO = n => `목표와 지금 상황에서 ${n}가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.`;
const INSTR_CUR_KOP = n => `목표와 현재 상황을 보고 ${n}가 다음에 할 선택을 하나 고르시오. 생활 목록을 보는 것은 지금의 몸 행동·대기 중인 선택·목표를 그대로 둔다. 가방 메뉴 선택은 몸 동작과 별개다. 인벤토리 들어가기·분류·물품·인자 선택·뒤로·선택 전부 취소는 선택 단계만 바꿀 뿐, 지금 몸 동작과 원래 부탁은 그대로 둔다. 물품 조작은 필요한 인자를 다 고르고 마지막 실행 항목을 선택했을 때에만 일어난다. 현재 상황과 위험을 같이 살펴서, 진행 중인 행동(계속 진행)이 아직 알맞다면 계속 진행한다. 새 몸 동작을 선택하면 그 동작은 처음부터 시작된다.`;

const cases = KO.map(k => {
  const e = KPP.find(x => x.id === k.id);
  let ver;
  if (k.instructions === INSTR_V2_KO(k.name)) ver = "v2"; else if (k.instructions === INSTR_CUR_KO(k.name)) ver = "current"; else throw new Error("unknown instruction " + k.id);
  return { id: k.id, cat: k.cat, name: k.name, instrVersion: ver, instructions: ver === "v2" ? INSTR_V2_KOP(k.name) : INSTR_CUR_KOP(k.name), input: e.input, choices: e.choices };
});
fs.writeFileSync(path.join(DIR, "cases-kop.json"), JSON.stringify(cases, null, 1));

const agent = new https.Agent({ keepAlive: true, maxSockets: 1 });
const PROVIDERS = {
  openai: { host: "api.openai.com", path: "/v1/decisions", model: "gpt-6-luna", key: process.env.OPENAI_API_KEY },
  celeris: { host: "inference.celeris.ai", path: "/celeris-1-decision/v1/decisions", model: "celeris-1.1-decision", key: process.env.CELERIS_API_KEY },
};
function call(prov, c) {
  const P = PROVIDERS[prov];
  const payload = Buffer.from(JSON.stringify({ model: P.model, input: c.input, questions: [{ type: "choice", name: "action", instructions: c.instructions, choices: c.choices }] }), "utf8");
  return new Promise(resolve => {
    const t0 = performance.now();
    const req = https.request({ host: P.host, path: P.path, method: "POST", agent, timeout: 30000,
      headers: { Authorization: `Bearer ${P.key}`, "Content-Type": "application/json", "Content-Length": payload.length } }, res => {
      const chunks = [];
      res.on("data", d => chunks.push(d));
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

let results;
if (NOSEND) results = rd("results-kop.json");
else {
  for (const [p, v] of Object.entries(PROVIDERS)) if (!v.key) { console.error(`missing key for ${p}`); process.exit(1); }
  for (const prov of Object.keys(PROVIDERS)) await call(prov, cases[0]);
  results = [];
  for (const [i, c] of cases.entries()) {
    const ko = KOR.find(r => r.id === c.id);
    const row = { id: c.id, cat: c.cat, instrVersion: c.instrVersion,
      ko: { openai: ko.runs.openai.map(r => r.choice), celeris: ko.runs.celeris.map(r => r.choice), openai_tokens: ko.runs.openai[0].input_tokens, celeris_tokens: ko.runs.celeris[0].input_tokens },
      kop: {} };
    for (const prov of Object.keys(PROVIDERS)) row.kop[prov] = [await call(prov, c), await call(prov, c)];
    results.push(row);
    if (i % 10 === 9) console.log(`done ${i + 1}/${cases.length}`);
  }
  fs.writeFileSync(path.join(DIR, "results-kop.json"), JSON.stringify(results, null, 1));
}

// --- summary ---
const CATS = ["전투", "채집·캐기", "탐험·이동", "제작·준비", "기지·건축", "가방·장비·건네기", "따라가기·목표 전환", "생활·기타"];
const q = (v, p) => { if (!v.length) return null; const s = [...v].sort((a, b) => a - b); return s[Math.min(s.length - 1, Math.floor(p * s.length))]; };
function stats(rows) {
  const o = { n: rows.length,
    kopAgree: rows.filter(r => r.kop.openai[0].choice && r.kop.openai[0].choice === r.kop.celeris[0].choice).length,
    koAgree: rows.filter(r => r.ko.openai[0] === r.ko.celeris[0]).length };
  for (const prov of Object.keys(PROVIDERS)) {
    const ms = rows.flatMap(r => r.kop[prov]).filter(x => !x.error).map(x => x.ms);
    const kTok = rows.reduce((s, r) => s + (r.kop[prov][0].input_tokens || 0), 0), koTok = rows.reduce((s, r) => s + r.ko[prov + "_tokens"], 0);
    o[prov] = { koToKopSame: rows.filter(r => r.kop[prov][0].choice === r.ko[prov][0]).length,
      p50: q(ms, 0.5), p95: q(ms, 0.95), max: ms.length ? Math.max(...ms) : null,
      tokenRatioKopKo: koTok ? +(kTok / koTok).toFixed(3) : null,
      unstableKop: rows.filter(r => r.kop[prov][0].choice !== r.kop[prov][1].choice).length,
      errors: rows.flatMap(r => r.kop[prov]).filter(x => x.error).length };
  }
  return o;
}
const summary = { all: stats(results), byCat: Object.fromEntries(CATS.map(c => [c, stats(results.filter(r => r.cat === c))])) };

// --- incremental blind labels: only (case, choice) pairs without a grade ---
const byId = Object.fromEntries(Object.entries(LKEY).map(([L, k]) => [k.id, L]));
let seed = 20261110;
const rnd = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };
const shuffle = a => { for (let i = a.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [a[i], a[j]] = [a[j], a[i]]; } return a; };
const p1 = [], p2 = [], pkey = {};
let nNewItems = 0, pCount = 0;
for (const r of results) {
  const k = KO.find(x => x.id === r.id), en = ENR.find(x => x.id === r.id);
  const combos = { ko_openai: r.ko.openai[0], ko_celeris: r.ko.celeris[0], en_openai: en.en.openai[0].choice, en_celeris: en.en.celeris[0].choice,
    kop_openai: r.kop.openai[0].choice, kop_celeris: r.kop.celeris[0].choice };
  const L = byId[r.id];
  const base = { category: r.cat, trigger: k.trigger, instructions: k.instructions, input: k.text, choices: k.cands };
  if (L) {
    const X = { ...LKEY[L].X };
    const graded = new Set(Object.values(X));
    const fresh = [...new Set([combos.kop_openai, combos.kop_celeris].filter(v => v && !graded.has(v)))];
    if (!fresh.length) { pkey[L] = { id: r.id, X, newX: [], combos }; continue; }
    let n = Object.keys(X).length; const newX = [];
    for (const v of fresh) { n++; X[`X${n}`] = v; newX.push(`X${n}`); }
    nNewItems += newX.length;
    pkey[L] = { id: r.id, X, newX, combos };
    const item = { case: L, ...base, note: `이 사례의 ${Object.keys(LKEY[L].X).join('·')}는 이미 등급을 매겼다(참고용). 새로 매길 것: ${newX.join('·')}.`,
      options: Object.entries(X).map(([x, v]) => ({ label: x, choice: v, grade_this: newX.includes(x) })) };
    (Number(L.slice(1)) % 2 === 1 ? p1 : p2).push(item);
  } else {
    const common = combos.ko_openai; // all four earlier combos were equal here
    const fresh = [...new Set([combos.kop_openai, combos.kop_celeris].filter(v => v && v !== common))];
    if (!fresh.length) continue;
    pCount++;
    const P = `P${String(pCount).padStart(3, "0")}`;
    const vals = shuffle([common, ...fresh]);
    const X = Object.fromEntries(vals.map((v, i) => [`X${i + 1}`, v]));
    nNewItems += vals.length;
    pkey[P] = { id: r.id, X, newX: Object.keys(X), combos };
    const item = { case: P, ...base, note: "새 사례: 표시된 선택 모두에 등급을 매긴다.", options: Object.entries(X).map(([x, v]) => ({ label: x, choice: v, grade_this: true })) };
    (pCount % 2 === 1 ? p1 : p2).push(item);
  }
}
fs.writeFileSync(path.join(DIR, "label-p1.json"), JSON.stringify(p1, null, 1));
fs.writeFileSync(path.join(DIR, "label-p2.json"), JSON.stringify(p2, null, 1));
fs.writeFileSync(path.join(DIR, "label-p-key.json"), JSON.stringify(pkey, null, 1));
summary.labels = { p1: p1.length, p2: p2.length, newItems: nNewItems, newCasesP: pCount, priorCasesWithNew: p1.length + p2.length - pCount };
fs.writeFileSync(path.join(DIR, "summary-kop.json"), JSON.stringify(summary, null, 1));
console.log(JSON.stringify(summary, null, 1));
