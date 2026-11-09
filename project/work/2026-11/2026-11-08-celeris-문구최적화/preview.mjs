// node preview.mjs <rules,comma> <label|id> : print transformed choices (and changed instructions) for one case
import { CASES, GRADES } from "./lib.mjs";
import { apply, goalInfo } from "./transform.mjs";
const [rules, who] = process.argv.slice(2);
const c = CASES.find(x => x.id === who || GRADES[x.id]?.label === who);
const r = apply(c, rules ? rules.split(",") : []);
console.log(goalInfo(c.text)); console.log("INSTR:", r.instructions);
for (const ch of r.choices) { const o = c.cands.find(x => x.value === ch.value).description; console.log((o === ch.description ? "  " : "* ") + ch.value + " :: " + ch.description.slice(0, 260).replace(/\n/g, " / ")); }
