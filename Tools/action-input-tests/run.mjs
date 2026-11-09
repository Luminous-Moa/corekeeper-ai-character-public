import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';import {spawnSync} from 'node:child_process';
const here=path.dirname(fileURLToPath(import.meta.url));const scripts=path.resolve(here,'../../Assets/AIPlayerSpike/Scripts');
const source=fs.readFileSync(path.join(scripts,'AIShortSkills.cs'),'utf8');const start=source.indexOf('public class StepSkill');const next=source.indexOf('public class HitOnceSkill',start);const step=source.slice(start,source.lastIndexOf('}',next)+1);
const actions=fs.readFileSync(path.join(scripts,'AIActions.cs'),'utf8');const expression=actions.match(/\["step"\].*?Make = c => (new StepSkill\([^\n]+?\)) \}/)[1];
const attackExpression=actions.split('\n').find(l=>l.includes('["attack"]')).split('Make = c => ')[1].split(', Stale')[0];
fs.mkdirSync(path.join(here,'.generated'),{recursive:true});fs.writeFileSync(path.join(here,'.generated/Step.cs'),'using Unity.Entities;using Unity.Mathematics;using Unity.Physics;\n'+step+'\npublic static class ProductionStepFactory{public static AISkill Make(AICandidate c)=>'+expression+';public static AttackSkill Attack(AICandidate c)=>'+attackExpression+';}');
const result=spawnSync('dotnet',['run','--project',here,'-c','Release'],{stdio:'inherit',windowsHide:true});process.exitCode=result.status;
