// Replay logged decision requests with <= MAX_TOKENS input tokens to gpt-6-luna and celeris-1.1-decision.
// Writes: <outDir>/results.json (with model names), <outDir>/judge-cases.json (blind A/B), <outDir>/ab-key.json
// Usage: node replay_compare10k.mjs <decisions.log> <outDir> [maxTokens=10000] [count=10]
import fs from "node:fs";
import https from "node:https";
import path from "node:path";

const [LOG, OUTDIR, MAXT = "10000", COUNT = "10"] = process.argv.slice(2);
const MAX_TOKENS = Number(MAXT), N = Number(COUNT);
const NAME = "Gemini";
const INSTR = `목표와 지금 상황에서 ${NAME}가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.`;

const agent = new https.Agent({ keepAlive: true, maxSockets: 1 });
const PROVIDERS = {
  openai: { host: "api.openai.com", path: "/v1/decisions", model: "gpt-6-luna", key: process.env.OPENAI_API_KEY },
  celeris: { host: "inference.celeris.ai", path: "/celeris-1-decision/v1/decisions", model: "celeris-1.1-decision", key: process.env.CELERIS_API_KEY },
};
for (const [p, v] of Object.entries(PROVIDERS)) if (!v.key) { console.error(`missing key for ${p}`); process.exit(1); }

const reIn = /DECISION #(\d+) INPUT rev=\S+ trigger='([^']*)'.*? input=\[(.*)\] options\((\d+)\): \|\| (.*)$/;
const reRes = /DECISION #(\d+) RESULT choice=(\S+) .*?latency=([\d.]+)s tokens=(\d+)/;
const inputs = new Map(), results = new Map();
for (const line of fs.readFileSync(LOG, "utf8").split(/\r?\n/)) {
  let m = reIn.exec(line);
  if (m) {
    const [, n, trigger, text, cnt, rest] = m;
    const cands = [];
    for (const part of rest.split(" || ")) {
      const mm = /^(c\d+) \[[^\]]*\] ([\s\S]*)$/.exec(part);
      if (mm) cands.push({ value: mm[1], description: mm[2].replaceAll(" ⏎ ", "\n") });
    }
    if (cands.length === Number(cnt)) inputs.set(Number(n), { trigger, text: text.replaceAll(" ⏎ ", "\n"), cands });
    continue;
  }
  m = reRes.exec(line);
  if (m) results.set(Number(m[1]), { choice: m[2], latency: Number(m[3]), tokens: Number(m[4]) });
}
const ids = [...inputs.keys()].filter(n => results.has(n) && results.get(n).tokens <= MAX_TOKENS).sort((a, b) => a - b);
const picks = Array.from({ length: Math.min(N, ids.length) }, (_, i) => ids[Math.floor(i * ids.length / Math.min(N, ids.length))]);
console.log(`parsed ${inputs.size}; <=${MAX_TOKENS} tokens: ${ids.length}; picks ${picks}`);

function call(prov, body) {
  const P = PROVIDERS[prov];
  const payload = Buffer.from(JSON.stringify({ ...body, model: P.model }), "utf8");
  return new Promise(resolve => {
    const t = performance.now();
    const req = https.request({ host: P.host, path: P.path, method: "POST", agent, timeout: 30000,
      headers: { Authorization: `Bearer ${P.key}`, "Content-Type": "application/json", "Content-Length": payload.length } }, res => {
      const chunks = [];
      res.on("data", c => chunks.push(c));
      res.on("end", () => {
        const ms = Math.round(performance.now() - t);
        const data = Buffer.concat(chunks).toString("utf8");
        if (res.statusCode !== 200) return resolve({ error: res.statusCode, body: data.slice(0, 300), ms });
        const j = JSON.parse(data), a = j.answers[0];
        const top = [...a.probabilities].sort((x, y) => y.probability - x.probability).slice(0, 3).map(p => [p.value, p.probability]);
        resolve({ ms, choice: a.choice, confidence: a.confidence, top, input_tokens: j.usage.input_tokens });
      });
    });
    req.on("timeout", () => req.destroy(new Error("timeout")));
    req.on("error", e => resolve({ error: String(e.message) }));
    req.end(payload);
  });
}
const bodyOf = n => { const c = inputs.get(n); return { input: c.text, questions: [{ type: "choice", name: "action", instructions: INSTR, choices: c.cands }] }; };

for (const p of Object.keys(PROVIDERS)) await call(p, bodyOf(picks[0])); // warm connections

const out = [], judge = [], key = {};
for (const n of picks) {
  const row = { decision: n, trigger: inputs.get(n).trigger, candidates: inputs.get(n).cands.length, logged: results.get(n), runs: {} };
  for (const p of Object.keys(PROVIDERS)) row.runs[p] = [await call(p, bodyOf(n)), await call(p, bodyOf(n))];
  out.push(row);
  console.log(JSON.stringify(row));
  const swap = Math.random() < 0.5;
  const [pa, pb] = swap ? ["celeris", "openai"] : ["openai", "celeris"];
  key[n] = { A: pa, B: pb };
  const c = inputs.get(n);
  judge.push({ case: n, trigger: c.trigger, instructions: INSTR, input: c.text, choices: c.cands,
    A_choice: row.runs[pa][0].choice, B_choice: row.runs[pb][0].choice });
}
fs.mkdirSync(OUTDIR, { recursive: true });
fs.writeFileSync(path.join(OUTDIR, "results.json"), JSON.stringify(out, null, 1));
fs.writeFileSync(path.join(OUTDIR, "judge-cases.json"), JSON.stringify(judge, null, 1));
fs.writeFileSync(path.join(OUTDIR, "ab-key.json"), JSON.stringify(key, null, 1));
const med = v => { const s = [...v].sort((a, b) => a - b); return s[Math.floor(s.length / 2)]; };
for (const p of Object.keys(PROVIDERS)) {
  const v = out.flatMap(r => r.runs[p]).filter(r => r.ms && !r.error).map(r => r.ms);
  console.log(p, "median", med(v), "max", Math.max(...v), "n", v.length);
}
const agree = out.filter(r => r.runs.openai[0].choice === r.runs.celeris[0].choice).length;
console.log(`agree ${agree}/${out.length}`);
