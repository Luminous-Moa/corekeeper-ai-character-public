// Shared data loading, split, Celeris calls (cached, logged) and scoring for the phrasing experiment.
// Key comes from env CELERIS_API_KEY and is never printed or written.
import fs from "node:fs";
import path from "node:path";
import https from "node:https";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

export const HERE = path.dirname(fileURLToPath(import.meta.url));
const SRC = path.join(HERE, "..", "2026-11-08-celeris-가상비교");
export const CASES = JSON.parse(fs.readFileSync(path.join(SRC, "100-ko", "cases.json"), "utf8"));
const OLD = JSON.parse(fs.readFileSync(path.join(SRC, "100-ko", "results.json"), "utf8"));
const KEY = JSON.parse(fs.readFileSync(path.join(SRC, "100-en", "label-key.json"), "utf8"));
const VER = { ...JSON.parse(fs.readFileSync(path.join(SRC, "100-en", "label-1-verdicts.json"), "utf8")),
  ...JSON.parse(fs.readFileSync(path.join(SRC, "100-en", "label-2-verdicts.json"), "utf8")) };
const SCORE = { "최선": 2, "알맞음": 1, "부적절": 0 };

// grades: case id -> { label, grades: {value: grade} }
export const GRADES = {};
for (const [L, k] of Object.entries(KEY)) {
  const g = {};
  for (const [x, v] of Object.entries(k.X)) g[v] = VER[L][x].grade;
  GRADES[k.id] = { label: L, grades: g, combos: k.combos };
}
// regression reference: the choice all four earlier runs agreed on (ko openai/celeris run 0 checked here)
export const REG_REF = {};
for (const r of OLD) if (!GRADES[r.id]) {
  if (r.runs.openai[0].choice !== r.runs.celeris[0].choice) throw new Error("regression case without agreement " + r.id);
  REG_REF[r.id] = r.runs.celeris[0].choice;
}

// ---- split (seeded, stratified by category, largest remainder to exactly 30 dev) ----
export const SPLIT_FILE = path.join(HERE, "split.json");
export function makeSplit() {
  let seed = 20261109;
  const rnd = () => { seed = (seed * 1664525 + 1013904223) >>> 0; return seed / 4294967296; };
  const graded = CASES.filter(c => GRADES[c.id]);
  const cats = [...new Set(graded.map(c => c.cat))].sort();
  const want = 30, N = graded.length;
  const quota = cats.map(c => { const n = graded.filter(g => g.cat === c).length, q = n * want / N; return { c, n, f: Math.floor(q), r: q - Math.floor(q) }; });
  let left = want - quota.reduce((s, q) => s + q.f, 0);
  [...quota].sort((a, b) => b.r - a.r).slice(0, left).forEach(q => q.f++);
  const dev = [], hold = [];
  for (const q of quota) {
    const ids = graded.filter(g => g.cat === q.c).map(g => g.id).sort();
    for (let i = ids.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [ids[i], ids[j]] = [ids[j], ids[i]]; }
    dev.push(...ids.slice(0, q.f)); hold.push(...ids.slice(q.f));
  }
  return { seed: 20261109, dev, hold, reg: Object.keys(REG_REF), quota };
}
export const SPLIT = fs.existsSync(SPLIT_FILE) ? JSON.parse(fs.readFileSync(SPLIT_FILE, "utf8")) : null;

// ---- calls ----
export const LOG = path.join(HERE, "calls.jsonl");
const cache = new Map();
let nCalls = 0;
if (fs.existsSync(LOG)) for (const l of fs.readFileSync(LOG, "utf8").split("\n")) if (l.trim()) {
  const j = JSON.parse(l); nCalls++; if (!j.error) cache.set(j.hash + "#" + j.rep, j);
}
export const callCount = () => nCalls;
export const CAP = 3000;
const agent = new https.Agent({ keepAlive: true, maxSockets: 4 });
const KEYV = process.env.CELERIS_API_KEY;
function post(payload) {
  return new Promise(resolve => {
    const t0 = performance.now();
    const req = https.request({ host: "inference.celeris.ai", path: "/celeris-1-decision/v1/decisions", method: "POST", agent, timeout: 30000,
      headers: { Authorization: `Bearer ${KEYV}`, "Content-Type": "application/json", "Content-Length": payload.length } }, res => {
      const chunks = [];
      res.on("data", c => chunks.push(c));
      res.on("end", () => {
        const ms = Math.round(performance.now() - t0), data = Buffer.concat(chunks).toString("utf8");
        if (res.statusCode !== 200) return resolve({ error: res.statusCode, body: data.slice(0, 300), ms });
        try {
          const j = JSON.parse(data), a = j.answers[0];
          resolve({ ms, choice: a.choice, confidence: a.confidence, probs: Object.fromEntries(a.probabilities.map(p => [p.value, p.probability])), input_tokens: j.usage?.input_tokens });
        } catch (e) { resolve({ error: "parse", body: data.slice(0, 300), ms }); }
      });
    });
    req.on("timeout", () => req.destroy(new Error("timeout")));
    req.on("error", e => resolve({ error: String(e.message) }));
    req.end(payload);
  });
}
export async function decide(req, meta, rep) {
  const body = { model: "celeris-1.1-decision", input: req.input,
    questions: [{ type: "choice", name: "action", instructions: req.instructions, choices: req.choices }] };
  const s = JSON.stringify(body), hash = crypto.createHash("sha256").update(s).digest("hex").slice(0, 24);
  const ck = hash + "#" + rep;
  if (cache.has(ck)) return { ...cache.get(ck), cached: true };
  if (!KEYV) throw new Error("CELERIS_API_KEY missing");
  for (let attempt = 0; attempt < 3; attempt++) {
    if (nCalls >= CAP) throw new Error("call cap reached");
    nCalls++;
    const r = await post(Buffer.from(s, "utf8"));
    const row = { at: new Date().toISOString(), variant: meta.variant, id: meta.id, rep, hash, ...r };
    fs.appendFileSync(LOG, JSON.stringify(row) + "\n");
    if (!r.error) { cache.set(ck, row); return row; }
    await new Promise(z => setTimeout(z, 1000 * (attempt + 1)));
  }
  return { error: "failed" };
}
export async function pool(items, n, fn) {
  const out = new Array(items.length); let i = 0;
  await Promise.all(Array.from({ length: n }, async () => { while (i < items.length) { const k = i++; out[k] = await fn(items[k], k); } }));
  return out;
}

// ---- scoring ----
export function scoreSet(ids, choices) { // choices: id -> [rep0, rep1...]
  let score = 0, graded = 0, unl = [], agree = 0, pBest = 0, dist = { "최선": 0, "알맞음": 0, "부적절": 0 };
  for (const id of ids) {
    const ch = choices[id]; if (!ch) continue;
    const g = GRADES[id].grades[ch[0].choice];
    if (g) { score += SCORE[g]; graded++; dist[g]++; } else unl.push({ id, label: GRADES[id].label, choice: ch[0].choice });
    if (ch.length > 1 && ch.every(c => c.choice === ch[0].choice)) agree++;
    pBest += Object.entries(GRADES[id].grades).filter(([, gg]) => gg === "최선").reduce((s, [v]) => s + (ch[0].probs?.[v] || 0), 0);
  }
  return { n: ids.length, score, max: 2 * ids.length, graded, dist, unlabeled: unl, repAgree: agree, pBest: +(pBest).toFixed(2) };
}
export function regSet(ids, choices) {
  const changed = [], agree = ids.filter(id => choices[id]?.length > 1 && choices[id].every(c => c.choice === choices[id][0].choice)).length;
  for (const id of ids) if (choices[id] && choices[id][0].choice !== REG_REF[id]) changed.push({ id, ref: REG_REF[id], now: choices[id][0].choice });
  return { n: ids.length, changed, repAgree: agree };
}
