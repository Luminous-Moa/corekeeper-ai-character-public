import fs from 'node:fs';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
const repo='C:/Dev/CoreKeeperModSDK';
const out='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/리팩토링-20261106/';
const start='a07166f38086e0241c37c601c16452664236813e', target='902721ec505df42d74d0b2d245ed34a049faadad';
const expected=['Assets/ModSDK/GameVersionRegistry.asset','Assets/Resources/ConditionsTable.asset','Assets/Resources/ConditionsTable.asset.meta','Packages/manifest.json','Packages/packages-lock.json','ProjectSettings/ProjectSettings.asset','ProjectSettings/ShaderGraphSettings.asset'].sort();
const git=(...args)=>execFileSync('git',args,{cwd:repo,encoding:'utf8',maxBuffer:8*1024*1024}).trim();
const paths=(...args)=>git(...args).split('\0').filter(Boolean).sort();
const hash=p=>createHash('sha256').update(fs.readFileSync(repo+'/'+p)).digest('hex');
function snapshot() {
 const dirty=execFileSync('git',['status','--porcelain=v1','-z','--untracked-files=no'],{cwd:repo,encoding:'utf8'}).split('\0').filter(Boolean).map(row=>row.slice(3)).sort();
 const untracked=paths('ls-files','--others','--exclude-standard','-z');
 return {head:git('rev-parse','HEAD'),branch:git('branch','--show-current'),dirty:dirty.map(p=>({path:p,sha256:hash(p)})),untracked:untracked.map(p=>({path:p,sha256:hash(p)}))};
}
if(process.argv[2]==='after-gates') {
 const original=JSON.parse(fs.readFileSync(out+'20-before-merge.json','utf8'));
 const current=snapshot();fs.writeFileSync(out+'20-after-gates.json',JSON.stringify(current,null,2));
 const matches=JSON.stringify(original.dirty)===JSON.stringify(current.dirty)&&JSON.stringify(original.untracked)===JSON.stringify(current.untracked);
 console.log(JSON.stringify({head:current.head,trackedLocalFiles:current.dirty.length,untrackedFiles:current.untracked.length,localBytesAndPathsPreserved:matches}));
 if(!matches||current.head!==target||current.branch!=='ai-character')throw Error('Final tree differs; return evidence to HEAD');
} else {
 const before=snapshot();
 if(before.head!==start||before.branch!=='ai-character'||git('rev-parse','refactor-maint-20261106')!==target)throw Error('HEAD/branch changed');
 if(JSON.stringify(before.dirty.map(x=>x.path))!==JSON.stringify(expected)||git('diff','--cached','--name-only'))throw Error('Local changes differ');
 git('merge-base','--is-ancestor',start,target);
 const changed=paths('diff','--name-only','-z',start,target);
 if(changed.some(p=>expected.includes(p)||before.untracked.some(x=>x.path===p)))throw Error('Merge intersects local files');
 fs.writeFileSync(out+'20-before-merge.json',JSON.stringify(before,null,2));
 const merge=git('merge','--ff-only',target);fs.writeFileSync(out+'20-merge-output.txt',merge+'\n');
 const after=snapshot();fs.writeFileSync(out+'20-after-merge.json',JSON.stringify(after,null,2));
 const matches=JSON.stringify(before.dirty)===JSON.stringify(after.dirty)&&JSON.stringify(before.untracked)===JSON.stringify(after.untracked);
 console.log(JSON.stringify({head:after.head,trackedLocalFiles:after.dirty.length,untrackedFiles:after.untracked.length,localBytesAndPathsPreserved:matches}));
 if(!matches||after.head!==target||after.branch!=='ai-character')throw Error('Merge verification differs; return evidence to HEAD');
}
