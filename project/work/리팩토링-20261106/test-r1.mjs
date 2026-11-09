import fs from 'node:fs';
import path from 'node:path';
const root='C:/Dev/CoreKeeperModSDK-refactor-20261106';
const goals=['gather','explore','follow','craft','store','home','farm','cook','goto','place'];
const cases=[];
const add=(name,goal,bridgeError=null,max)=>cases.push({name,goal,bridgeError,...(max===undefined?{}:{max})});
for(const d of goals) add('valid '+d,{do:d,...(['gather','craft','place'].includes(d)?{item:'Wood'}:{}),...(['gather','craft'].includes(d)?{count:1}:{}),...(d==='goto'?{where:'base'}:{})});
for(const [d,max] of [['gather',30],['craft',10],['place',64]]) {
 for(const count of [-1,0,1,max,max+1]) add(d+' count '+count,{do:d,item:'Wood',count},null,max);
}
add('missing gather item',{do:'gather',count:1},'gather needs item and integer count');
add('missing craft count',{do:'craft',item:'Wood'},'craft needs item and integer count');
add('fraction gather count',{do:'gather',item:'Wood',count:1.5},'gather needs item and integer count');
add('missing goto where',{do:'goto'},'goto needs where (a place)');
add('bad goto where',{do:'goto',where:3},'goto needs where (a place)');
add('missing place item',{do:'place'},'place needs item (where a string, count an integer when given)');
add('bad place where',{do:'place',item:'Wood',where:3},'place needs item (where a string, count an integer when given)');
add('bad place count',{do:'place',item:'Wood',count:'1'},'place needs item (where a string, count an integer when given)');
add('store item',{do:'store',item:'Wood'});
add('bad store item',{do:'store',item:4},'store item must be a string');
add('unknown goal',{do:'build'},'bad goal.do');
add('internal free',{do:'free'},'bad goal.do');
const data={wireNames:goals,hostWords:{None:[],Gather:[],Explore:['탐험','explore'],Follow:['따라와','follow'],Craft:['만들기','craft'],Store:['보관','store'],Home:['기지로','home'],Farm:['농사','밭','farm'],Cook:['요리','cook'],Free:[],Goto:['가기','goto'],Place:['놓기','place']},cases,
 experience:[
 {kind:0,item:'Wood',hasItem:true,key:''},{kind:9,item:'Wood',hasItem:true,key:''},
 {kind:1,item:'None',hasItem:false,key:'gather:None'},{kind:4,item:'None',hasItem:false,key:'craft:None'},
 {kind:2,item:'Wood',hasItem:true,key:'explore'},{kind:3,item:'Wood',hasItem:true,key:'follow'},
 {kind:5,item:'None',hasItem:false,key:'store'},{kind:6,item:'Wood',hasItem:true,key:'home:Wood'},
 {kind:11,item:'Torch',hasItem:true,key:'place:Torch'},{kind:99,item:'Wood',hasItem:true,key:'99:Wood'},{kind:99,item:'None',hasItem:false,key:'99'}]};
// The game's shape bound is read from the existing canonical constant, not guessed.
const places=fs.readFileSync(path.join(root,'Assets/AIPlayerSpike/Scripts/Core/AIPlaces.cs'),'utf8');
const actual=Number(places.match(/MaxShapeTiles\s*=\s*(\d+)/)[1]);
for(const c of cases) if(c.goal.do==='place'&&c.max!==undefined) { if(c.goal.count===64)c.goal.count=actual;if(c.goal.count===65)c.goal.count=actual+1;c.max=actual;c.name='place count '+c.goal.count; }
fs.writeFileSync(path.join(root,'Tools/ai-bridge/test/fixtures/goal-contract.json'),JSON.stringify(data,null,2)+'\n');
let p=path.join(root,'Tools/core-tests/Program.cs'),s=fs.readFileSync(p,'utf8');s=s.replace('("S1", PlanProtocolTests.Run),','("S1", PlanProtocolTests.Run),\n            ("R1", GoalContractTests.Run),');fs.writeFileSync(p,s);
p=path.join(root,'Tools/ai-bridge/test/protocol.test.mjs');s=fs.readFileSync(p,'utf8');
const at=s.indexOf("console.log('cliErr()');");
const extra=[
"// The same goals are parsed by Core/AIPlanProtocol in GoalContractTests.",
"{",
"  const contract = JSON.parse(fs.readFileSync(path.join(HERE, 'fixtures/goal-contract.json'), 'utf8'));",
"  for (const c of contract.cases) {",
"    const got = shape({ decision: 'set', goal: structuredClone(c.goal) });",
"    ok(got === c.bridgeError, 'shared goal contract: ' + c.name, String(got));",
"  }",
"  const protocolSource = fs.readFileSync(path.join(HERE, '../protocol.mjs'), 'utf8');",
"  const allowed = protocolSource.match(/const GOAL_DO = (\\[[^;]+\\]);/)[1];",
"  ok(same(JSON.parse(allowed.replaceAll(\"'\", '\"')), contract.wireNames), 'bridge and C# share the exact external vocabulary');",
"}",
"",
].join('\n');
s=s.slice(0,at)+extra+s.slice(at);fs.writeFileSync(p,s);
p=path.join(root,'Assets/AIPlayerSpike/Scripts/AIGoals.cs');s=fs.readFileSync(p,'utf8').replaceAll('\n\n\n\n}', '\n}');fs.writeFileSync(p,s);
console.log('R1 tests written');
