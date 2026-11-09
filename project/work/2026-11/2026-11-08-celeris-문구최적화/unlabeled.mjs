// Writes unlabeled-new-choices.json: final-rule choices that have no grade (dev/holdout) and regression changes,
// with original descriptions, for a separate judge. Not graded here.
import fs from "node:fs";
import { CASES, SPLIT, GRADES, REG_REF } from "./lib.mjs";
import { JUDGE_BETTER } from "./judge-better.mjs";
const out = [];
const add = (set, v) => { const R = JSON.parse(fs.readFileSync(`results/${v}.json`, "utf8"));
  for (const id of SPLIT[set]) { const ch = R.choices[id]; if (!ch) continue; const reps = ch.map(x => x.choice), c0 = CASES.find(x => x.id === id);
    for (const c of new Set(reps)) {
      const desc = c0.cands.find(x => x.value === c).description;
      if (set === "reg") { if (c === REG_REF[id]) continue;
        out.push({ set, variant: v, id, cat: c0.cat, reps: reps.join("/"), choice: c, previous_agreed_choice: REG_REF[id], choice_description: desc, previous_description: c0.cands.find(x => x.value === REG_REF[id]).description }); continue; }
      if (GRADES[id].grades[c]) continue;
      out.push({ set, variant: v, id, label: GRADES[id].label, cat: c0.cat, reps: reps.join("/"), choice: c, judge_named_better_in_report: !!JUDGE_BETTER[GRADES[id].label]?.includes(c), graded_values: GRADES[id].grades, choice_description: desc });
    } } };
add("dev", "f-G1s+I3"); add("hold", "hold-G1s+I3"); add("reg", "f-G1s+I3");
fs.writeFileSync("unlabeled-new-choices.json", JSON.stringify(out, null, 1));
for (const o of out) console.log(o.set, o.label || o.id, o.cat, o.choice, "reps", o.reps, o.set === "reg" ? "prev " + o.previous_agreed_choice : "judge-named " + o.judge_named_better_in_report, "::", o.choice_description.slice(0, 160).replace(/\n/g, " "));
