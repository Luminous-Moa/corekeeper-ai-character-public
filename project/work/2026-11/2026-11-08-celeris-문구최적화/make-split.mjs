// Writes split.json once (seed fixed). Holdout ids are not scored until the final evaluation.
import fs from "node:fs";
import { makeSplit, SPLIT_FILE, CASES, GRADES } from "./lib.mjs";
if (fs.existsSync(SPLIT_FILE)) { console.log("split.json exists; not overwriting"); process.exit(0); }
const s = makeSplit();
fs.writeFileSync(SPLIT_FILE, JSON.stringify(s, null, 1));
const cat = id => CASES.find(c => c.id === id).cat;
const tally = ids => ids.reduce((o, id) => (o[cat(id)] = (o[cat(id)] || 0) + 1, o), {});
console.log("dev", s.dev.length, tally(s.dev)); console.log("hold", s.hold.length, tally(s.hold)); console.log("reg", s.reg.length);
console.log("dev labels", s.dev.map(id => GRADES[id].label).sort().join(" "));
