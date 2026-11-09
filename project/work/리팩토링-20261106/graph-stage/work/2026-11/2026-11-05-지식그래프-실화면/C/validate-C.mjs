// validate-C.mjs - Node port of validate-C.py (same checks, same C/validation.json output).
// Added 2026-11-05 for the combat stage 2 re-extraction because this PC has no Python. Read-only except C/validation.json.
//   node validate-C.mjs
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const HERE = path.dirname(fileURLToPath(import.meta.url));
const BASE = path.dirname(HERE);
const OLD = path.join(path.dirname(BASE), '2026-11-05-지식그래프-개편');
const read = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^﻿/, ''));
const isFile = p => fs.existsSync(p) && fs.statSync(p).isFile();
const overlay = read(path.join(HERE, 'overlay-source.json'));
const a2 = read(path.join(BASE, 'A2/manifest.json'));
const b = read(path.join(BASE, 'B/existing-node-mapping.json'));
const oldC = read(path.join(OLD, 'C/graph-C.json'));
const screens = new Set(a2.screens.map(s => s.id));
const elements = new Set(a2.screens.flatMap(s => s.elements.map(e => e.id)));
const features = new Set(b.features.map(n => n.id));
const newIds = new Set(overlay.nodes.map(n => n.id));
const known = new Set([...screens, ...elements, ...features, ...newIds]);
const errors = [], seen = new Set();
const py = k => `('${k[0]}', '${k[1]}', '${k[2]}')`;   // Python tuple text, as validate-C.py prints it
for (const edge of overlay.edges) {
  const key = [edge.source, edge.type, edge.target], ks = JSON.stringify(key);
  if (seen.has(ks)) errors.push(`duplicate edge: ${py(key)}`);
  seen.add(ks);
  for (const end of ['source', 'target']) if (!known.has(edge[end])) errors.push(`unknown ${end}: ${edge[end]}`);
  if (!isFile(path.join(BASE, edge.evidence))) errors.push(`missing evidence: ${edge.evidence}`);
}
const oldIds = new Set(oldC.nodes.map(n => n.id));
for (const note of overlay.existingNodeAnnotations) if (!oldIds.has(note.id)) errors.push(`unknown old node: ${note.id}`);
for (const note of overlay.screenAnnotations) if (!screens.has(note.id)) errors.push(`unknown screen: ${note.id}`);
for (const p of overlay.evidenceReferences) if (!isFile(path.join(BASE, p))) errors.push(`missing evidence input: ${p}`);
const activityText = fs.readFileSync(path.join(HERE, '활동-동료확인.md'), 'utf8');
for (const edge of overlay.edges) if (edge.type === '유저 활동→화면요소' && !activityText.includes(edge.target)) errors.push(`activity element absent from prose: ${edge.target}`);
const report = {
  status: errors.length ? 'fail' : 'pass',
  errors,
  nodes: overlay.nodes.length,
  edges: overlay.edges.length,
  activityElements: overlay.edges.filter(e => e.type === '유저 활동→화면요소').length,
  functionMappings: overlay.edges.filter(e => e.type !== '유저 활동→화면요소').length,
  overlaySha256: crypto.createHash('sha256').update(fs.readFileSync(path.join(HERE, 'overlay-source.json'))).digest('hex'),
  limit: 'Reference validation only; not runtime trace. C3 actual result and B version boundaries remain separate.'
};
const prev = isFile(path.join(HERE, 'validation.json')) ? fs.readFileSync(path.join(HERE, 'validation.json'), 'utf8') : '';
const eol = prev.includes('\r\n') ? '\r\n' : '\n';   // keep the existing file's line ends
fs.writeFileSync(path.join(HERE, 'validation.json'), (JSON.stringify(report, null, 2) + '\n').replace(/\n/g, eol));
console.log(JSON.stringify(report));
process.exitCode = errors.length ? 1 : 0;
