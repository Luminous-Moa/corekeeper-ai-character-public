// node run.mjs <dir> <round> [members=claude,gpt,gemini,grok] [scenes=all]
// Starts the real bridge (base worktree, own port/config/logs), posts every scene body of <dir>/scenes per member (members in
// parallel, scenes in order), writes <dir>/results-r<round>.jsonl.
import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import { spawn } from 'node:child_process';
const BR = 'C:/Dev/CoreKeeperModSDK-base/Tools/ai-bridge';
const [dir, round = '1', mem = 'claude,gpt,gemini,grok', sc = 'all'] = process.argv.slice(2);
const members = mem.split(',');
const PORT = 8397;
const cfgFile = path.join(dir, 'config.json');
fs.writeFileSync(cfgFile, JSON.stringify({ kgData: 'C:/Dev/CoreKeeperModSDK/Tools/knowledge/data' }, null, 1));
const logs = path.join(dir, 'logs');
fs.mkdirSync(logs, { recursive: true });
const child = spawn(process.execPath, [path.join(BR, 'bridge.mjs')], { cwd: BR, env: { ...process.env, AI_BRIDGE_PORT: String(PORT), AI_BRIDGE_CONFIG: cfgFile, AI_BRIDGE_MEMBERS: members.join(','), AI_BRIDGE_LOGS: logs }, stdio: ['ignore', 'pipe', 'pipe'] });
let up = false;
child.stdout.on('data', (d) => { process.stdout.write('[bridge] ' + d); if (/listening/.test(d)) up = true; });
child.stderr.on('data', (d) => process.stdout.write('[bridge!] ' + d));
for (let i = 0; i < 600 && !up; i++) await new Promise((r) => setTimeout(r, 500));
if (!up) { console.log('bridge did not start'); child.kill(); process.exit(1); }
const health = await new Promise((res) => http.get(`http://127.0.0.1:${PORT}/v1/health`, (r) => { let t = ''; r.on('data', (d) => (t += d)); r.on('end', () => res(JSON.parse(t))); }));
fs.writeFileSync(path.join(dir, `health-r${round}.json`), JSON.stringify(health, null, 1));
for (const [id, m] of Object.entries(health.members)) if (members.includes(id)) console.log(`health ${id}: skip=${m.skip} ${m.skipWhy || ''} usage=${JSON.stringify(m.windows)}`);
function post(body) {
  return new Promise((resolve) => {
    const req = http.request({ host: '127.0.0.1', port: PORT, path: '/v1/plan', method: 'POST', headers: { 'Content-Type': 'application/json' } }, (res) => {
      let t = ''; res.on('data', (d) => (t += d)); res.on('end', () => { try { resolve({ status: res.statusCode, ...JSON.parse(t) }); } catch { resolve({ ok: false, error: 'bad reply', raw: t.slice(0, 200) }); } });
    });
    req.setTimeout(170000, () => req.destroy());
    req.on('error', (e) => resolve({ ok: false, error: String(e.message) }));
    req.end(JSON.stringify(body));
  });
}
const files = fs.readdirSync(path.join(dir, 'scenes')).filter((f) => f.endsWith('.json')).sort((a, b) => a.localeCompare(b, 'en', { numeric: true }));
const outFile = path.join(dir, `results-r${round}.jsonl`);
await Promise.all(members.map(async (m) => {
  for (const f of files.filter((f) => f.endsWith(`.${m}.json`) && (sc === 'all' || sc.split(',').some((s) => f.startsWith(s + '.'))))) {
    const b = JSON.parse(fs.readFileSync(path.join(dir, 'scenes', f), 'utf8'));
    const world = b.world.slice(0, 31) + String(round).slice(-1);
    const t0 = Date.now();
    const r = await post({ member: m, world, report_id: b.report_id, goal_rev: b.goal_rev, report: b.report, geo: { ...b.geo, world } });
    const line = { scene: b.scene, member: m, round: +round, wall_ms: Date.now() - t0, reply: r };
    fs.appendFileSync(outFile, JSON.stringify(line) + '\n');
    console.log(`${m} ${b.scene}: ok=${r.ok} err=${r.error || ''} ${r.ms}ms calls=${r.calls} searches=${r.searches} ${JSON.stringify(r.answer)?.slice(0, 300)}`);
  }
}));
child.kill();
console.log('done');
