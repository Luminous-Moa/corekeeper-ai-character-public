// node run.mjs <variant> <rules|-> <sets: dev,reg,hold> [reps=2]
// Runs Celeris on the transformed requests, writes results/<variant>.json and prints metrics.
import fs from "node:fs";
import path from "node:path";
import { CASES, SPLIT, HERE, decide, pool, scoreSet, regSet, callCount } from "./lib.mjs";
import { apply } from "./transform.mjs";
const [variant, rulesArg, setsArg = "dev,reg", repsArg = "2"] = process.argv.slice(2);
const rules = rulesArg && rulesArg !== "-" ? rulesArg.split(",") : [];
const sets = setsArg.split(","), reps = Number(repsArg);
const ids = sets.flatMap(s => SPLIT[s]);
const jobs = [];
for (const id of ids) { const c = CASES.find(x => x.id === id), req = apply(c, rules); for (let r = 0; r < reps; r++) jobs.push({ id, req, r }); }
const before = callCount();
const res = await pool(jobs, 4, j => decide(j.req, { variant, id: j.id }, j.r));
const choices = {};
jobs.forEach((j, k) => { if (!res[k].error) (choices[j.id] ||= [])[j.r] = { choice: res[k].choice, probs: res[k].probs }; });
const out = { variant, rules, sets, reps, at: new Date().toISOString(), choices, metrics: {} };
for (const s of sets) out.metrics[s] = s === "reg" ? regSet(SPLIT.reg, choices) : scoreSet(SPLIT[s], choices);
fs.mkdirSync(path.join(HERE, "results"), { recursive: true });
const f = path.join(HERE, "results", variant + ".json");
fs.writeFileSync(f, JSON.stringify(out, null, 1));
const errs = res.filter(r => r.error).length;
console.log(`${variant} [${rules.join(",") || "baseline"}] new calls ${callCount() - before} (total ${callCount()}), errors ${errs}`);
for (const s of sets) {
  const m = out.metrics[s];
  if (s === "reg") console.log(`  reg: changed ${m.changed.length}/${m.n}, repAgree ${m.repAgree}`, m.changed.map(c => `${c.id.replace("decisions-", "")}:${c.ref}->${c.now}`).join(" "));
  else console.log(`  ${s}: score ${m.score}/${m.max} (graded ${m.graded}, dist ${JSON.stringify(m.dist)}), unlabeled ${m.unlabeled.length}, repAgree ${m.repAgree}/${m.n}, pBest ${m.pBest}`, m.unlabeled.map(u => `${u.label}:${u.choice}`).join(" "));
}
