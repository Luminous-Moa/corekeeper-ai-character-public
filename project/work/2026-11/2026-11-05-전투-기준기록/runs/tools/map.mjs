// map.mjs <probe.txt>... : draws the probed tiles (# wall, . ground, ? no answer), north (bigger z) on top
import fs from 'fs';
const g = new Map();
for (const f of process.argv.slice(2)) for (const l of fs.readFileSync(f, 'utf8').split(/\r?\n/)) {
  const m = l.match(/^(-?\d+),(-?\d+) (\S+)/); if (!m) continue;
  g.set(m[1] + ',' + m[2], m[3].includes('wall') ? '#' : (m[3] === 'none' ? '?' : '.'));
}
const xs = [...g.keys()].map(k => +k.split(',')[0]), zs = [...g.keys()].map(k => +k.split(',')[1]);
const x0 = Math.min(...xs), x1 = Math.max(...xs), z0 = Math.min(...zs), z1 = Math.max(...zs);
let hdr = '      '; for (let x = x0; x <= x1; x++) hdr += (Math.abs(x) % 10); console.log(hdr + `   (x ${x0}..${x1}, last digit)`);
for (let z = z1; z >= z0; z--) { let s = String(z).padStart(5) + ' '; for (let x = x0; x <= x1; x++) s += g.get(x + ',' + z) ?? ' '; console.log(s); }
