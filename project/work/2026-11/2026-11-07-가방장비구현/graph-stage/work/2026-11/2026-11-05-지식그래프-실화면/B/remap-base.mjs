// remap-base.mjs - regenerate existing-node-mapping.json when the code graph base commit moves.
// Same selection and exact-segment comparison as audit-code.py (its existing-node-mapping part only).
// It does not read the installed mod: version-evidence.json stays the record of the screen-observation run.
// Usage (from this folder or anywhere):
//   node remap-base.mjs [--base <commit>] [--run <commit>] [--graph <code.graph.json>] [--out <file>]
// Defaults: base = sourceCommit of ../../2026-11-05-지식그래프-개편/B/code.graph.json, run = 76563bf (the
// source installed while the screens were observed), out = existing-node-mapping.json next to this script.
// Added 2026-11-05 for the combat stage 2 re-extraction (fc8161b); checked to reproduce the e82eb87 file byte for byte.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const HERE = path.dirname(fileURLToPath(import.meta.url));
const arg = k => { const i = process.argv.indexOf(k); return i > 0 ? process.argv[i + 1] : null; };
const REPO = 'C:/Dev/CoreKeeperModSDK-bag-common';
const graphPath = arg('--graph') || path.resolve(HERE, '../../2026-11-05-지식그래프-개편/B/code.graph.json');
const g = JSON.parse(fs.readFileSync(graphPath, 'utf8'));
const git = (...a) => execFileSync('git', ['-C', REPO, ...a], {maxBuffer: 64 * 1024 * 1024});
const BASE = git('rev-parse', arg('--base') || g.meta.sourceCommit).toString().trim();
const RUN = git('rev-parse', arg('--run') || '76563bf').toString().trim();
if (BASE !== g.meta.sourceCommit) throw Error(`graph is ${g.meta.sourceCommit}, not ${BASE}`);
const sha = s => crypto.createHash('sha256').update(Buffer.from(s, 'utf8')).digest('hex');
const cache = new Map();
const blob = (c, p) => { const k = c + ':' + p; if (!cache.has(k)) cache.set(k, git('show', k).toString('utf8')); return cache.get(k); };
const lines = s => { const a = s.split(/\r\n|\n|\r/); if (a.length && a[a.length - 1] === '') a.pop(); return a; };   // Python str.splitlines
const selectedNames = new Set(['TryHandleChatCommand','Dispatch','IsAiCommand','DefaultAgent','IsSkillCommand','IsTestCommand','TestCommands','TestCommandsOn','TestSwitchPath','HandleCommand','StartSkill','StatusAll','StatusText','StageText','Reply','FlushReplies','ChatSendPrefix','HandleGoalCommand','InventoryCounts','Summary','Ensure','Load','Prepare','CreateNewCharacter','DistinctLook','CreateServerCopy','CharacterDataPostfix','Members','PathFor','FilePath','All','Find','Update','InstallPatches','PlayerSpawned','OnSpawned','ApplyPlayerName','InitPlayer','Settings']);
const segment = n => lines(blob(BASE, n.file)).slice(n.lineStart - 1, n.lineEnd).join('\n');
// Files that did not exist in the screen-observation run cannot match it; same-name methods there (AIHostOrders.Find,
// AIMention.All/Members after b2fa4cc) are not the slash-command code this selection means. Skipped (2026-11-05 b2fa4cc).
const runFiles = new Set(git('ls-tree', '-r', '--name-only', RUN).toString().split(/\r?\n/).map(s => s.trim()));
const rows = [];
const previousFile = n => {
 const file=n.file, name=path.basename(file);
 if(['AIBody.cs','AIBody.Commands.cs','AISkill.cs','AIVision.cs','AITileReader.cs','AIObservation.cs','AINavigator.cs','AIInspectSkills.cs','AIGotoSkill.cs','AIMineSkill.cs','AIAttackSkill.cs','AIMoveTrace.cs'].includes(name))return file.replace(name,'AIBodySkills.cs');
 if(['AIPlayerSpikeMod.Chat.cs','AIPlayerSpikePatches.cs','AIWorldSettings.cs','AICharacterStore.cs'].includes(name))return file.replace(name,'AIPlayerSpikeMod.cs');
 if(['AIMemory.cs','AIDrops.cs','AIObjects.cs','AITunnel.cs'].includes(name))return file.replace(name,'AIExplore.cs');
 return file;
};
for (const n of g.nodes) {
  if (n.type !== '코드' || !selectedNames.has(n.name)) continue;
  if (!['MethodDeclaration', 'VariableDeclarator', 'PropertyDeclaration'].includes(n.kind)) continue;
  if (!(n.file || '').endsWith('.cs')) continue;
  const runFile=previousFile(n); if (!runFiles.has(runFile)) continue;
  const part = segment(n), b = blob(RUN, previousFile(n)), hits = [];
  let pos = 0;
  for (;;) { const idx = b.indexOf(part, pos); if (idx < 0) break; hits.push(b.slice(0, idx).split('\n').length); pos = idx + part.length; }
  rows.push({id: n.id, file: n.file, runFile:previousFile(n), baseLines: [n.lineStart, n.lineEnd], sourceTextSha256: sha(part), exactSegmentMatchesInRun: hits, runLines: hits.length === 1 ? [hits[0], hits[0] + n.lineEnd - n.lineStart] : null, sameSourceSegment: hits.length === 1});
}
// Explicit inventory output and name-setting method (selected by existing AST range).
for (const n of g.nodes) {
  if ((n.signature || '').startsWith('InvSkill.Start(') || (n.signature || '').startsWith('AIPlayerSpikeMod.Step2_NameAndPrepareInput(')) {
    const part = segment(n), b = blob(RUN, previousFile(n)), idx = b.indexOf(part), line = idx >= 0 ? b.slice(0, idx).split('\n').length : null;
    rows.push({id: n.id, file: n.file, runFile:previousFile(n), baseLines: [n.lineStart, n.lineEnd], sourceTextSha256: sha(part), runLines: line ? [line, line + n.lineEnd - n.lineStart] : null, sameSourceSegment: idx >= 0});
  }
}
const ids = new Set(rows.map(r => r.id));
const features = g.nodes.filter(n => n.type === '기능' && (n.id === 'slash:ai' || n.id.includes('AIPlayerSpike')));
const edges = g.edges.filter(e => (ids.has(e.from) && (ids.has(e.to) || ['read', 'write'].includes(e.type))) || (e.type === '기능→코드' && ids.has(e.to)));
const dataIds = new Set(edges.filter(e => ['read', 'write'].includes(e.type)).map(e => e.to));
const out = {authority: 'Existing machine IDs and AST ranges reused; exact source segment comparison only. No new graph nodes or inferred runtime edges.', baseCommit: BASE, runCandidateCommit: RUN, nodes: rows, features, existingEdges: edges, dataNodes: g.nodes.filter(n => dataIds.has(n.id))};
fs.writeFileSync(arg('--out') || path.join(HERE, 'existing-node-mapping.json'), (JSON.stringify(out, null, 2) + '\n').replace(/\n/g, '\r\n'));   // CRLF like the Python text-mode original
console.log(JSON.stringify({baseCommit: BASE, runCandidateCommit: RUN, mappedNodes: rows.length, sameSegments: rows.filter(r => r.sameSourceSegment).length, changed: rows.filter(r => !r.sameSourceSegment).map(r => r.id)}));
