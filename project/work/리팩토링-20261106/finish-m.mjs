import fs from 'node:fs';
const root='C:/Dev/CoreKeeperModSDK-refactor-20261106/Assets/AIPlayerSpike/Scripts/';
const names=['AISkill','AIVision','AITileReader','AIObservation','AINavigator','AIInspectSkills','AIGotoSkill','AIMineSkill','AIAttackSkill','AIMoveTrace','AIPlace','AIBreakSkills','AIDrops','AIObjects','AITunnel','AIPlayerSpikePatches','AIWorldSettings','AICharacterStore','AIEdgeSkills','AIPickupSkill','AIBody.Commands','AIPlayerSpikeMod.Chat','AIEdge.Reach'];
for(const n of names){let s=fs.readFileSync(root+n+'.cs','utf8');s='// '+n+' — extracted from the existing implementation; behavior and state ownership are unchanged.\n'+s.slice(s.indexOf('using '));fs.writeFileSync(root+n+'.cs',s);}
for(const n of ['AIBody','AIPlayerSpikeMod']){let s=fs.readFileSync(root+n+'.cs','utf8').replaceAll('AIBodySkills.cs','AIBody.cs').replace('// AIBodySkills —','// AIBody —');fs.writeFileSync(root+n+'.cs',s);}
const files=fs.readdirSync(root,{recursive:true}).filter(p=>p.endsWith('.cs'));
const missing=files.filter(p=>!fs.existsSync(root+p+'.meta'));
const guids=files.map(p=>fs.readFileSync(root+p+'.meta','utf8').match(/guid: (\w+)/)[1]);
console.log('source/meta missing',missing.length,'duplicate GUID',guids.length-new Set(guids).size);
if(missing.length||guids.length!==new Set(guids).size)throw Error('meta check');
