// node bycat.mjs : base vs final by category over dev+holdout (rep 0), score / 부적절 / 미판정
import fs from "node:fs";
import { CASES, SPLIT, GRADES } from "./lib.mjs";
const S = { "최선": 2, "알맞음": 1, "부적절": 0 };
const L = f => JSON.parse(fs.readFileSync(`results/${f}.json`, "utf8")).choices;
const base = { ...L("base"), ...L("hold-base") }, fin = { ...L("f-G1s+I3"), ...L("hold-G1s+I3") };
const cats = {};
for (const id of [...SPLIT.dev, ...SPLIT.hold]) { const c = CASES.find(x => x.id === id).cat; const o = (cats[c] ||= { n: 0, b: [0, 0, 0], f: [0, 0, 0] });
  o.n++; for (const [k, R] of [["b", base], ["f", fin]]) { const g = GRADES[id].grades[R[id][0].choice]; if (g) { o[k][0] += S[g]; if (g === "부적절") o[k][1]++; } else o[k][2]++; } }
console.log("| 분류 | 건수 | 지금 문구 점수(부적절/미판정) | 최종 점수(부적절/미판정) |\n|---|---|---|---|");
for (const [c, o] of Object.entries(cats)) console.log(`| ${c} | ${o.n} | ${o.b[0]}/${2 * o.n} (${o.b[1]}/${o.b[2]}) | ${o.f[0]}/${2 * o.n} (${o.f[1]}/${o.f[2]}) |`);
