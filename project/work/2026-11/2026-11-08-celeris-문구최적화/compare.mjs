// node compare.mjs <set> <variant...> : per-case first choice and grade, first variant is the reference
import fs from "node:fs";
import { SPLIT, GRADES, REG_REF } from "./lib.mjs";
import { JUDGE_BETTER } from "./judge-better.mjs";
const [set, ...vs] = process.argv.slice(2);
const R = vs.map(v => JSON.parse(fs.readFileSync(`results/${v}.json`, "utf8")));
const ab = { "최선": "B", "알맞음": "o", "부적절": "x" };
for (const id of SPLIT[set]) {
  const cells = R.map(r => { const ch = r.choices[id]; if (!ch) return "-"; const c = ch[0].choice;
    const g = set === "reg" ? (c === REG_REF[id] ? "=" : "!") : (ab[GRADES[id].grades[c]] || (JUDGE_BETTER[GRADES[id].label]?.includes(c) ? "?j" : "?"));
    const p = ch[0].probs?.[c]; return `${c}(${g}${p != null ? " " + p.toFixed(2) : ""})${ch.length > 1 && ch[1]?.choice !== c ? "~" + ch[1]?.choice : ""}`; });
  const same = cells.every(x => x.split("(")[0] === cells[0].split("(")[0]);
  if (!same || process.env.ALL) console.log((GRADES[id]?.label || id.replace("decisions-", "")).padEnd(6), cells.join(" | "));
}
