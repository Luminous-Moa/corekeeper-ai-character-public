// node table.mjs <set> <variant...> : per-rep score, 부적절, unlabeled (judge-named better in brackets), rep agreement, reg changes
import fs from "node:fs";
import { SPLIT, GRADES, REG_REF } from "./lib.mjs";
import { JUDGE_BETTER } from "./judge-better.mjs";
const S = { "최선": 2, "알맞음": 1, "부적절": 0 };
const [set, ...vs] = process.argv.slice(2);
console.log("| variant | rules | score r0 | score r1 | 부적절 r0/r1 | 미판정 r0/r1 (판정자 언급) | 2회 일치 | 회귀 변화 r0/r1 |");
console.log("|---|---|---|---|---|---|---|---|");
for (const v of vs) {
  const R = JSON.parse(fs.readFileSync(`results/${v}.json`, "utf8"));
  const cell = rep => { let s = 0, bad = 0, un = 0, uj = 0;
    for (const id of SPLIT[set]) { const c = R.choices[id]?.[rep]?.choice; if (!c) return null; const g = GRADES[id].grades[c];
      if (g) { s += S[g]; if (g === "부적절") bad++; } else { un++; if (JUDGE_BETTER[GRADES[id].label]?.includes(c)) uj++; } }
    return { s, bad, un, uj }; };
  const a = cell(0), b = R.reps > 1 ? cell(1) : null;
  const agree = SPLIT[set].filter(id => R.choices[id]?.length > 1 && R.choices[id][0].choice === R.choices[id][1].choice).length;
  const rc = rep => R.choices[SPLIT.reg[0]]?.[rep] ? SPLIT.reg.filter(id => R.choices[id][rep].choice !== REG_REF[id]).length : "-";
  console.log(`| ${v} | ${R.rules.join("+") || "(지금 문구)"} | ${a.s}/${2 * SPLIT[set].length} | ${b ? b.s : "-"} | ${a.bad}/${b ? b.bad : "-"} | ${a.un}(${a.uj})/${b ? `${b.un}(${b.uj})` : "-"} | ${R.reps > 1 ? agree + "/" + SPLIT[set].length : "-"} | ${R.sets.includes("reg") ? rc(0) + "/" + rc(1) : "-"} |`);
}
