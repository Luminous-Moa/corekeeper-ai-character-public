// node analyze.mjs <dir> -> a table of every decision (results-r*.jsonl) with the recall queries (logs/recall.jsonl)
import fs from 'node:fs';
import path from 'node:path';
const dir = process.argv[2];
const rd = (f) => (fs.existsSync(f) ? fs.readFileSync(f, 'utf8').trim().split('\n').filter(Boolean).map((l) => JSON.parse(l)) : []);
const res = fs.readdirSync(dir).filter((f) => /^results-r\d+\.jsonl$/.test(f)).flatMap((f) => rd(path.join(dir, f)));
const recall = rd(path.join(dir, 'logs', 'recall.jsonl'));
const plan = rd(path.join(dir, 'logs', 'plan.jsonl')).filter((x) => x.member);
const short = (g) => !g ? '' : [g.do, g.item, g.count, g.option, g.hold && 'hold=' + JSON.stringify(g.hold), g.door != null && 'door' + g.door, g.where].filter((x) => x != null && x !== false && x !== '').join(' ');
const rows = [];
for (const r of res) {
  const a = r.reply.answer || {};
  const world = null;
  const rec = recall.filter((x) => x.member === r.member && x.report_id === r.reply.report_id && x.world?.endsWith(String(r.round)) && x.world.startsWith(r.scene.replace(/\D/g, '').padStart(2, '0')));
  const pl = plan.filter((x) => x.member === r.member && x.report_id === r.reply.report_id && x.world?.endsWith(String(r.round)) && x.world.startsWith(r.scene.replace(/\D/g, '').padStart(2, '0'))).pop();
  const queries = rec.flatMap((x) => (x.rounds || []).flatMap((y) => (y.request || []).map((q) => JSON.stringify(q))));
  rows.push({
    k: `${r.scene} ${r.member} r${r.round}`, ok: r.reply.ok, err: r.reply.error, detail: r.reply.error ? r.reply.detail : undefined,
    dec: a.decision, act: a.plan ? a.plan.map(short).join(' → ') : short(a.goal), order: a.order, ep: a.edge_proposal && `${a.edge_proposal.id} ${a.edge_proposal.answer}: ${a.edge_proposal.why || ''}`, ba: a.build_after,
    say: a.say, why: a.why, problem: a.problem, prefetch: a.prefetch && JSON.stringify(a.prefetch),
    ms: r.reply.ms, calls: r.reply.calls, searches: r.reply.searches, prompt: r.reply.prompt_chars, usage: JSON.stringify(r.reply.usage), summary: r.reply.recall_summary, map_asked: r.reply.map_asked,
    queries, raw: r.reply.ok ? undefined : r.reply.raw?.slice(0, 400), model: pl?.model,
  });
}
for (const x of rows) console.log(JSON.stringify(x, null, 0));
