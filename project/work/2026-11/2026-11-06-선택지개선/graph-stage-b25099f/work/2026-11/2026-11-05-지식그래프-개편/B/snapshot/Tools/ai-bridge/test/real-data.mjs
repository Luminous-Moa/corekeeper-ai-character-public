// Design 7.1-3: the memory index and a few queries on the real AIPlayers records (read only), for a person to read.
// Run: node test/real-data.mjs [--root <AIPlayers>] [--world <id>] [--kg <data>] > out.txt
import fs from 'node:fs';
import path from 'node:path';
import { performance } from 'node:perf_hooks';
import * as M from '../memgraph.mjs';

const arg = (k, d) => { const i = process.argv.indexOf(`--${k}`); return i > 0 ? process.argv[i + 1] : d; };
const root = arg('root', M.AIPLAYERS);
const game = M.loadGame(arg('kg', process.env.KG_DATA || M.DEFAULT_KG));
const worlds = arg('world') ? [arg('world')] : fs.readdirSync(root).filter((w) => /^[0-9a-f]{32}$/.test(w));
for (const world of worlds) {
  for (const member of fs.readdirSync(path.join(root, world)).filter((m) => fs.existsSync(path.join(root, world, m, 'events.jsonl')))) {
    const t0 = performance.now();
    const ctx = M.context({ root, world, member, game });
    const ms = performance.now() - t0;
    console.log(`\n==================== ${world} / ${member}: ${ctx.idx.ev.length} events, build ${ms.toFixed(1)} ms, play ${(ctx.play / 3600).toFixed(2)} h`);
    const idx = M.memoryIndex(ctx);
    console.log(idx);
    console.log(`(index ${idx.length} chars)`);
    // the index's own keys: the first enemy, the first place, plus goal and when
    const ek = (idx.match(/\n (o:\d+)/) || [])[1];
    const pk = (idx.match(/\n (tile:-?\d+,-?\d+|area:\w+)/) || [])[1];
    const lastGoal = [...ctx.idx.byKey.keys()].at(-1);
    const qs = [ek && { q: 'about', target: ek }, pk && { q: 'near', place: pk }, lastGoal && { q: 'goal', key: `g:${lastGoal}` }, { q: 'when', ago_min: [30, 0], kind: 'down|fight|goal_end' }, { q: 'game', op: 'how', target: 'CopperBar' }].filter(Boolean);
    const r = M.runQueries(ctx, qs.slice(0, 3));
    console.log(`--- recall ${JSON.stringify(qs.slice(0, 3))} (${r.text.length} chars)\n${r.text}`);
    const r2 = M.runQueries(ctx, qs.slice(3));
    console.log(`--- recall ${JSON.stringify(qs.slice(3))} (${r2.text.length} chars)\n${r2.text}`);
  }
}
