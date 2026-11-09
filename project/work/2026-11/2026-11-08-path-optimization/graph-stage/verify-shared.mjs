import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const stage = path.dirname(fileURLToPath(import.meta.url));
const B = path.join(stage,'B-shared');
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const read = name => JSON.parse(fs.readFileSync(path.join(B,name),'utf8'));
const target = '43c3817fc1a36142c8591fb087ed867665633fe1';
const generated = ['code.graph.json','B.graph.json','javascript.raw.json','validation.json','boundaries.json','call-forms.json','snapshot.manifest.json'];
const before = Object.fromEntries(generated.map(name => [name,hash(fs.readFileSync(path.join(B,name)))]));
execFileSync(process.execPath,['extractor/build.mjs'],{cwd:B,stdio:'pipe'});
const changed = generated.filter(name => hash(fs.readFileSync(path.join(B,name))) !== before[name]);
const graph = read('code.graph.json');
const signatures = [
  'AIBrain.PreparePlan(', 'AIBrain.PreparationSteps(', 'AIBrain.BuildCandidateSteps(',
  'AIBrain.BuildJsonSteps(', 'AIBrain.CandBuild.ExploreSteps(', 'AIBrain.CandBuild.RememberedOresSteps(',
  'AITunnel.PlanBatch.StepSearches(', 'AITunnel.PlanBatch.BeginSinglePlan(',
  'AIExpedition.Compose(', 'AIExpedition.Navigate(', 'AITunnelSearch.SharedSearchJob.Step('
];
const anchors = signatures.map(signature => {
  const matches = graph.nodes.filter(node => node.signature?.startsWith(signature));
  if (matches.length !== 1) throw Error('Absent or ambiguous signature ' + signature);
  const node = matches[0];
  const blob = execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','cat-file','blob',target+':'+node.file]);
  return {signature:node.signature,id:node.id,file:node.file,lineStart:node.lineStart,lineEnd:node.lineEnd,
    blobMatch:blob.equals(fs.readFileSync(path.join(B,'snapshot',node.file))),runtimeTrace:false};
});
const prep = JSON.parse(fs.readFileSync(path.join(stage,'preparation.json'),'utf8'));
const canonicalChanges = prep.canonicalFiles.filter(file => hash(fs.readFileSync(file.path)) !== file.sha256).map(file=>file.path);
const result = {sourceCommit:target,stageOnly:true,canonicalApplied:false,liveAccepted:false,
  sameRawGeneration:{ok:changed.length===0,changed,files:before},
  nodes:graph.nodes.length,edges:graph.edges.length,anchors,canonicalChanges,
  officialHead:execFileSync('git',['-C','C:/Dev/CoreKeeperModSDK','rev-parse','HEAD'],{encoding:'utf8'}).trim()};
fs.writeFileSync(path.join(stage,'shared-verification.json'),JSON.stringify(result,null,2)+'\n');
if (changed.length || anchors.some(anchor=>!anchor.blobMatch)) throw Error('Generation verification failed');
console.log(JSON.stringify({sourceCommit:target,nodes:result.nodes,edges:result.edges,anchors:anchors.length,canonicalChanges,sameRawGeneration:true}));
