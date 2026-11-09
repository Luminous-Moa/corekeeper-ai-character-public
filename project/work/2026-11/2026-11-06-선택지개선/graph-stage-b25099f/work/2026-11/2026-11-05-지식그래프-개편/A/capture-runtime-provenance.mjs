import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

// Metadata-only provenance comparison. Product text is never printed or interpreted.
const root=path.dirname(fileURLToPath(import.meta.url));
const repo='C:/Dev/CoreKeeperModSDK';
const deployed=path.join(root,'isolated/runtime');
const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
function git(args,cwd=repo){
  const result=spawnSync('git',args,{cwd,encoding:null,maxBuffer:64*1024*1024});
  if(result.status!==0)throw new Error(`Git metadata/blob operation failed: ${args[0]}, exit ${result.status}`);
  return result.stdout;
}
function normalizeCrlf(bytes){
  const out=Buffer.allocUnsafe(bytes.length);let n=0,crlf=0,lf=0;
  for(let i=0;i<bytes.length;i++){
    if(bytes[i]===13 && bytes[i+1]===10){crlf++;continue;}
    out[n++]=bytes[i];if(bytes[i]===10)lf++;
  }
  return {bytes:out.subarray(0,n),crlf,lf};
}
const commit=git(['rev-parse','e82eb87^{commit}']).toString('utf8').trim();
const paths=git(['ls-tree','-r','--name-only','-z',commit,'--','Tools/ai-bridge','Tools/knowledge']).toString('utf8').split('\0').filter(Boolean).sort();
const files=[],notInDeployment=[];
for(const relative of paths){
  const file=path.join(deployed,relative);
  if(!fs.existsSync(file)){notInDeployment.push(relative);continue;}
  const actual=fs.readFileSync(file),reference=git(['cat-file','blob',`${commit}:${relative}`]);
  const actualLf=normalizeCrlf(actual),referenceLf=normalizeCrlf(reference);
  files.push({path:relative,deploymentBytes:actual.length,deploymentSha256:sha(actual),gitBlobBytes:reference.length,gitBlobSha256:sha(reference),byteEqual:actual.equals(reference),deploymentLfSha256:sha(actualLf.bytes),gitBlobLfSha256:sha(referenceLf.bytes),lfNormalizedEqual:actualLf.bytes.equals(referenceLf.bytes),deploymentCrlfPairs:actualLf.crlf,gitBlobCrlfPairs:referenceLf.crlf,deploymentLfCount:actualLf.lf,gitBlobLfCount:referenceLf.lf,lineBreakCountEqual:actualLf.lf===referenceLf.lf});
}
const baselineSource=path.join(root,'../B/external-runtime.manifest.json');
const baselineBytes=fs.readFileSync(baselineSource);
const baseline=JSON.parse(baselineBytes.toString('utf8').replace(/^\uFEFF/,''));
const externalFiles=baseline.files.map(file=>{
  const actual=fs.readFileSync(path.join(baseline.root,file.path));
  return {path:file.path,baselineSha256:file.sha256,baselineBytes:file.bytes,currentSha256:sha(actual),currentBytes:actual.length,matchesBaseline:sha(actual)===file.sha256};
});
const outDir=path.join(root,'provenance');fs.mkdirSync(outDir,{recursive:true});
fs.writeFileSync(path.join(outDir,'external-runtime.baseline.json'),baselineBytes);
const startup=JSON.parse(fs.readFileSync(path.join(root,'raw/bridge-isolated.meta.json'),'utf8').replace(/^\uFEFF/,''));
const result={
  capturedUtc:new Date().toISOString(),executionStartedUtc:startup.startedUtc,pid:startup.pid,program:startup.program,
  scope:'Post-execution hash capture of the retained A deployment used by the running isolated service; not a contemporaneous process-loaded-code attestation.',
  deploymentOrigin:'HEAD supplied initial git archive deployment of e82eb87; Windows archive EOL conversion reported after execution. A deployment retained unchanged; B later corrected its package using raw git cat-file blobs.',
  referenceRepository:repo,referenceCommit:commit,normalization:'Binary-only replacement of CRLF with LF. No source text emitted; no other bytes transformed.',
  summary:{compared:files.length,byteEqual:files.filter(f=>f.byteEqual).length,eolOnlyDifference:files.filter(f=>!f.byteEqual&&f.lfNormalizedEqual).length,normalizedMismatch:files.filter(f=>!f.lfNormalizedEqual).length,lineBreakCountMismatch:files.filter(f=>!f.lineBreakCountEqual).length,trackedNotInDeployment:notInDeployment.length},
  files,notInDeployment,
  externalRuntime:{sourceManifest:'../B/external-runtime.manifest.json',sourceManifestSha256:sha(baselineBytes),preservedBaseline:'provenance/external-runtime.baseline.json',authority:baseline.authority,root:baseline.root,baselineGitHead:baseline.gitHead,baselineWorkingTreeStatus:baseline.workingTreeStatus,currentGitHead:git(['rev-parse','HEAD'],baseline.root).toString('utf8').trim(),outsidePinnedSdkCommit:true,notRuntimeAttestation:true,files:externalFiles},
  graphUntouched:{file:'graph-A.json',sha256:sha(fs.readFileSync(path.join(root,'graph-A.json')))}
};
fs.writeFileSync(path.join(outDir,'runtime-provenance.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({summary:result.summary,externalFiles:externalFiles.length,externalBaselineMatches:externalFiles.filter(f=>f.matchesBaseline).length,graphSha256:result.graphUntouched.sha256,manifest:'provenance/runtime-provenance.json'}));
if(result.summary.normalizedMismatch || result.summary.lineBreakCountMismatch)process.exitCode=1;
