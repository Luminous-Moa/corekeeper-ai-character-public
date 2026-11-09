// cframe-lag.mjs <bruteprobe.log> : how far the brute's ghost on the host client world (CFRAME cpos) is behind the server
// (TICK/EVT pos), while it moves fast (charging). For each CFRAME during a charge: the server time at which the server
// position was where the client draws it now (linear between server samples on the same charge) -> lag = t_client - t_server.
// Also: first client frame that shows the charge moving vs first server sample that moved (onset lag).
import fs from 'fs';
const L = fs.readFileSync(process.argv[2], 'utf8').split(/\r?\n/);
const S = new Map(), C = new Map();
for (const l of L) {
  const m = l.match(/^wall=(\S+) t=([\d.]+) stk=(\d+) .*BRUTE-PROBE (TICK|EVT|CFRAME) e=(\S+) (.*)$/); if (!m) continue;
  const [, wall, t, stk, kind, e, body] = m;
  if (kind === 'CFRAME') {
    const c = body.match(/cpos=\((-?[\d.]+),(-?[\d.]+)\) spos=\((-?[\d.]+),(-?[\d.]+)\) sphase=(\w+)/); if (!c) continue;
    if (!C.has(e)) C.set(e, []); C.get(e).push({ wall, t: +t, stk: +stk, cx: +c[1], cz: +c[2], sx: +c[3], sz: +c[4], ph: c[5] });
  } else {
    const b = kind === 'EVT' ? (body.split(' | ')[1] || '') : body; const p = b.match(/pos=\((-?[\d.]+),(-?[\d.]+)\)/); const ph = (b.match(/phase=(\w+)/) || [])[1];
    if (!p) continue; if (!S.has(e)) S.set(e, []); S.get(e).push({ wall, t: +t, stk: +stk, x: +p[1], z: +p[2], ph });
  }
}
const q = (a, p) => { const s = [...a].sort((x, y) => x - y); return s[Math.min(s.length - 1, Math.max(0, Math.ceil(p * s.length) - 1))]; };
const lags = [], onsets = [];
for (const [e, cs] of C) {
  const ss = (S.get(e) || []).filter(s => s.ph === 'Charging');
  // group server Charging samples into charges (gap > 2 ticks)
  const groups = []; for (const s of ss) { const g = groups[groups.length - 1]; if (g && s.stk - g[g.length - 1].stk <= 2) g.push(s); else groups.push([s]); }
  for (const g of groups) {
    if (g.length < 4) continue;
    const t0 = g[0].t, t1 = g[g.length - 1].t;
    // server onset: first sample whose position differs from the first by > 0.05
    const sOn = g.find(s => Math.hypot(s.x - g[0].x, s.z - g[0].z) > 0.05);
    const p0 = g[0];
    const cf = cs.filter(c => c.t >= t0 - 0.5 && c.t <= t1 + 1.0);
    const cOn = cf.find(c => c.t >= t0 && Math.hypot(c.cx - p0.x, c.cz - p0.z) > 0.05);
    if (sOn && cOn) onsets.push({ e, wall: g[0].wall, server: sOn.t, client: cOn.t, lag: cOn.t - sOn.t });
    // path matching: for client frames in the middle of the charge (both moving)
    const dir = [g[g.length - 1].x - g[0].x, g[g.length - 1].z - g[0].z]; const len = Math.hypot(...dir); if (len < 1) continue;
    const u = [dir[0] / len, dir[1] / len];
    const proj = (x, z) => (x - g[0].x) * u[0] + (z - g[0].z) * u[1];
    const sp = g.map(s => ({ t: s.t, d: proj(s.x, s.z) }));
    for (const c of cf) {
      const d = proj(c.cx, c.cz); if (d < 0.5 || d > sp[sp.length - 1].d - 0.5) continue;
      for (let i = 1; i < sp.length; i++) if (sp[i - 1].d <= d && sp[i].d >= d && sp[i].d > sp[i - 1].d) {
        const ts = sp[i - 1].t + (sp[i].t - sp[i - 1].t) * (d - sp[i - 1].d) / (sp[i].d - sp[i - 1].d);
        lags.push(c.t - ts); break;
      }
    }
  }
}
const f = x => x.toFixed(3);
console.log('onsets (client first moved frame - server first moved sample), s:'); for (const o of onsets) console.log(`  ${o.wall} ${o.e} ${f(o.lag)}`);
if (onsets.length) console.log(`onset lag n=${onsets.length} p50=${f(q(onsets.map(o => o.lag), .5))} min=${f(Math.min(...onsets.map(o => o.lag)))} max=${f(Math.max(...onsets.map(o => o.lag)))}`);
if (lags.length) console.log(`path lag (client frame time - server time at the same position) n=${lags.length} p50=${f(q(lags, .5))} p10=${f(q(lags, .1))} p90=${f(q(lags, .9))}`);
