// English rerun of the 100 Korean cases: gpt-6-luna vs celeris-1.1-decision, 2 runs each after warm-up.
// Inputs: ../100-ko/cases.json, ../100-ko/results.json, tr/en-pre.json (template translation).
// Outputs: cases-en.json, results-en.json, summary-en.json, label-*.json (4-combination blind labels), label-key.json
// Keys from env OPENAI_API_KEY / CELERIS_API_KEY (never printed).
import fs from "node:fs";
import path from "node:path";
import https from "node:https";
import { fileURLToPath } from "node:url";

const DIR = path.dirname(fileURLToPath(import.meta.url));
const rd = p => JSON.parse(fs.readFileSync(path.join(DIR, p), "utf8"));
const KO = rd("../100-ko/cases.json"), KOR = rd("../100-ko/results.json"), ENP = rd("tr/en-pre.json");

const INSTR_V2_KO = n => `목표와 지금 상황에서 ${n}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).`;
const INSTR_V2_EN = n => `Given the goal and the current situation, choose the one action ${n} should take next. Every choice is an action that can actually be started now. If the current action (continue) still fits, keep it; switch when a new fact has appeared (a new action starts from the beginning).`;
const INSTR_CUR_EN = n => `Given the goal and the current situation, choose the one next choice for ${n}. Looking up the life list keeps the current body action, pending choice and goal. Body actions and bag-menu choices are different. Entering the inventory, choosing a category, item or argument, going back, and cancelling the whole selection change only the selection step and keep the current body action and the original request. An item operation happens only when the final execute entry is chosen after the required arguments. Consider the current situation and risks together; if the current action (continue) still fits, keep it. A newly chosen body action starts from the beginning.`;

const cases = KO.map(k => {
  const e = ENP.find(x => x.id === k.id);
  const v2 = k.instructions === INSTR_V2_KO(k.name);
  return { id: k.id, cat: k.cat, name: k.name, instrVersion: v2 ? "v2" : "current",
    instructions: v2 ? INSTR_V2_EN(k.name) : INSTR_CUR_EN(k.name), input: e.input, choices: e.choices };
});
fs.writeFileSync(path.join(DIR, "cases-en.json"), JSON.stringify(cases, null, 1));

const agent = new https.Agent({ keepAlive: true, maxSockets: 1 });
const PROVIDERS = {
  openai: { host: "api.openai.com", path: "/v1/decisions", model: "gpt-6-luna", key: process.env.OPENAI_API_KEY },
  celeris: { host: "inference.celeris.ai", path: "/celeris-1-decision/v1/decisions", model: "celeris-1.1-decision", key: process.env.CELERIS_API_KEY },
};
for (const [p, v] of Object.entries(PROVIDERS)) if (!v.key) { console.error(`missing key for ${p}`); process.exit(1); }
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

for (const prov of Object.keys(PROVIDERS)) await call(prov, cases[0]);
const results = [];
for (const [i, c] of cases.entries()) {
  const ko = KOR.find(r => r.id === c.id);
  const row = { id: c.id, cat: c.cat, instrVersion: c.instrVersion,
    ko: { openai: ko.runs.openai.map(r => r.choice), celeris: ko.runs.celeris.map(r => r.choice), openai_tokens: ko.runs.openai[0].input_tokens, celeris_tokens: ko.runs.celeris[0].input_tokens },
    en: {} };
  for (const prov of Object.keys(PROVIDERS)) row.en[prov] = [await call(prov, c), await call(prov, c)];
  results.push(row);
  if (i % 10 === 9) console.log(`done ${i + 1}/${cases.length}`);
}
fs.writeFileSync(path.join(DIR, "results-en.json"), JSON.stringify(results, null, 1));

// --- summary ---
const CATS = ["전투", "채집·캐기", "탐험·이동", "제작·준비", "기지·건축", "가방·장비·건네기", "따라가기·목표 전환", "생활·기타"];
const q = (v, p) => { if (!v.length) return null; const s = [...v].sort((a, b) => a - b); return s[Math.min(s.length - 1, Math.floor(p * s.length))]; };
function stats(rows) {
  const o = { n: rows.length,
    enAgree: rows.filter(r => r.en.openai[0].choice && r.en.openai[0].choice === r.en.celeris[0].choice).length,
    koAgree: rows.filter(r => r.ko.openai[0] === r.ko.celeris[0]).length };
  for (const prov of Object.keys(PROVIDERS)) {
    const ms = rows.flatMap(r => r.en[prov]).filter(x => !x.error).map(x => x.ms);
    const enTok = rows.reduce((s, r) => s + (r.en[prov][0].input_tokens || 0), 0), koTok = rows.reduce((s, r) => s + r.ko[prov + "_tokens"], 0);
    o[prov] = { koToEnSame: rows.filter(r => r.en[prov][0].choice === r.ko[prov][0]).length,
      p50: q(ms, 0.5), p95: q(ms, 0.95), max: ms.length ? Math.max(...ms) : null,
      tokenRatioEnKo: koTok ? +(enTok / koTok).toFixed(3) : null,
      unstableEn: rows.filter(r => r.en[prov][0].choice !== r.en[prov][1].choice).length,
      errors: rows.flatMap(r => r.en[prov]).filter(x => x.error).length };
  }
  return o;
}
const summary = { all: stats(results), byCat: Object.fromEntries(CATS.map(c => [c, stats(results.filter(r => r.cat === c))])) };

// --- 4-combination blind labels (Korean input for the judge) ---
let seed = 20261109;
const rnd = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };
const label = [], key = {};
for (const r of results) {
  const combos = { ko_openai: r.ko.openai[0], ko_celeris: r.ko.celeris[0], en_openai: r.en.openai[0].choice, en_celeris: r.en.celeris[0].choice };
  const vals = [...new Set(Object.values(combos).filter(Boolean))];
  if (vals.length < 2) continue;
  for (let i = vals.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [vals[i], vals[j]] = [vals[j], vals[i]]; }
  const k = KO.find(x => x.id === r.id);
  const caseId = `L${String(label.length + 1).padStart(3, "0")}`;
  const X = Object.fromEntries(vals.map((v, i) => [`X${i + 1}`, v]));
  key[caseId] = { id: r.id, X, combos };
  label.push({ case: caseId, category: r.cat, trigger: k.trigger, instructions: k.instructions, input: k.text, choices: k.cands,
    options: vals.map((v, i) => ({ label: `X${i + 1}`, choice: v })) });
}
fs.writeFileSync(path.join(DIR, "label-key.json"), JSON.stringify(key, null, 1));
const files = [];
if (label.length > 30) {
  const nFiles = Math.round(label.length / 25);
  for (let f = 0; f < nFiles; f++) { const part = label.filter((_, i) => i % nFiles === f); const fn = `label-${f + 1}.json`; fs.writeFileSync(path.join(DIR, fn), JSON.stringify(part, null, 1)); files.push([fn, part.length]); }
} else { fs.writeFileSync(path.join(DIR, "label.json"), JSON.stringify(label, null, 1)); files.push(["label.json", label.length]); }
summary.labelCases = label.length; summary.labelFiles = files;
summary.optionsPerCase = label.reduce((m, l) => (m[l.options.length] = (m[l.options.length] || 0) + 1, m), {});
fs.writeFileSync(path.join(DIR, "summary-en.json"), JSON.stringify(summary, null, 1));
console.log(JSON.stringify(summary, null, 1));
