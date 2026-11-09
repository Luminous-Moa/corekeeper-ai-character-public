import fs from 'node:fs';
import path from 'node:path';
const root='C:/Dev/CoreKeeperModSDK-refactor-20261106',dir=path.join(root,'Assets/AIPlayerSpike/Scripts');
function read(n){return fs.readFileSync(path.join(dir,n),'utf8').replace(/\r\n/g,'\n');}
function write(n,s){fs.writeFileSync(path.join(dir,n),s);}
function rep(s,a,b){if(!s.includes(a))throw Error('missing '+a.slice(0,100));return s.replace(a,b);}
for(const file of fs.readdirSync(dir).filter(n=>n.endsWith('.cs'))) {
 let s=read(file);
 if(!/\b(eatId|eatVariation|digWhat|item2)\b|placeStandOnly/.test(s))continue;
 s=s.replace(/\beatId\b/g,'itemId').replace(/\bitem2\b/g,'secondItemId').replace(/\bdigWhat\b/g,'targetLabel').replace(/\beatVariation\b/g,'itemVariation');
 s=s.split('\n').map(line=>{
  if(/kind = "craft"|kind = "take"|kind = "cook"|\["craft"\]|\["take"\]|\["cook"\]/.test(line))line=line.replace(/\bitemVariation\b/g,'itemCount');
  if(/kind = "edgefill"|kind = "edgerepair"|\["edgefill"\]|\["edgerepair"\]/.test(line))line=line.replace(/\bitemVariation\b/g,'edgeGap');
  if(/kind = "cookwait"|\["cookwait"\]/.test(line))line=line.replace(/\bitemVariation\b/g,'waitSeconds');
  if(/kind = "farm"|kind = "cook"|\["farm"\]|\["cook"\]/.test(line))line=line.replace(/\btargetLabel\b/g,'actionMode');
  if(/kind = "explore"|c.kind == "explore"/.test(line))line=line.replace(/\btargetLabel\b/g,'exploreDirection');
  if(/kind = "retreat"|\["retreat"\]/.test(line))line=line.replace(/\bplaceStandOnly\b/g,'retreatInside');
  if(/kind = "edgepickup"|\["edgepickup"\]/.test(line))line=line.replace(/\bplaceStandOnly\b/g,'pickupStandOnly').replace(/\bplaceStand\b/g,'pickupStand');
  return line;
 }).join('\n');
 if(file==='AIDecisions.cs') {
  s=rep(s,'    public string targetLabel = "";','    public string targetLabel = "";   // dig / craft / follow / retreat / step / combo: target description\n    public string actionMode = "";    // farm / cook: execution mode\n    public string exploreDirection = ""; // explore: skill Tag direction');
  s=rep(s,'    public int itemVariation;','    public int itemVariation; // eat: item variant\n    public int itemCount;     // craft / take / cook: amount\n    public int edgeGap;       // edgefill / edgerepair: gap index\n    public int waitSeconds;   // cookwait: duration\n    public EdgeCandidateRole edgeRole; // meaning independent of desc');
  s=rep(s,'    public int2 placeStand;','    public int2 pickupStand; // edgepickup: required collection stand\n    public bool pickupStandOnly, retreatInside; // separate from placing constraints\n    public int2 placeStand;');
 }
 write(file,s);
}
let s=read('AIEdge.cs');
s=s.replaceAll('kind = "place", key = $"place:{doorItem}', 'kind = "place", edgeRole = EdgeCandidateRole.DoorPlace, key = $"place:{doorItem}')
 .replaceAll('kind = "place", key = $"place:{torch}', 'kind = "place", edgeRole = EdgeCandidateRole.TorchPlace, key = $"place:{torch}')
 .replaceAll('x.desc.StartsWith("기지 문")','x.edgeRole == EdgeCandidateRole.DoorPlace')
 .replaceAll('x.desc.StartsWith("기지 문 다시 달기")','x.edgeRole == EdgeCandidateRole.DoorRepair')
 .replaceAll('x.desc.StartsWith("기지 안 횃불")','x.edgeRole == EdgeCandidateRole.TorchPlace');
write('AIEdge.cs',s);
s=read('AIEdge.Guard.cs').replaceAll('kind = "place", key = $"place:{doorItem}','kind = "place", edgeRole = EdgeCandidateRole.DoorRepair, key = $"place:{doorItem}');
write('AIEdge.Guard.cs',s);
s=read('AIBrain.Candidates.cs');s=rep(s,'desc = c.desc, progress = c.progress','desc = c.desc, edgeRole = c.edgeRole, progress = c.progress');write('AIBrain.Candidates.cs',s);
s=read('Core/AICandidateAssembler.cs');
s=rep(s,'public sealed class AsmCand','public enum EdgeCandidateRole { None, DoorPlace, DoorRepair, TorchPlace }\n\npublic sealed class AsmCand');
s=rep(s,'    public string kind, key, desc, value;','    public string kind, key, desc, value;\n    public EdgeCandidateRole edgeRole;');
s=rep(s,'c.kind == "place" && c.desc != null && c.desc.StartsWith("기지 문 다시 달기")','c.kind == "place" && c.edgeRole == EdgeCandidateRole.DoorRepair');
s=rep(s,'        string d = c.desc ?? "";\n','');
s=rep(s,'d.StartsWith("기지 문 다시 달기") ? "문 다시 달기" : d.StartsWith("기지 문 달기") ? "문 달기" : d.StartsWith("기지 안 횃불") ? "횃불" : "놓기"','c.edgeRole == EdgeCandidateRole.DoorRepair ? "문 다시 달기" : c.edgeRole == EdgeCandidateRole.DoorPlace ? "문 달기" : c.edgeRole == EdgeCandidateRole.TorchPlace ? "횃불" : "놓기"');
write('Core/AICandidateAssembler.cs',s);
let p=path.join(root,'Tools/core-tests/AssemblerReplayTests.cs');s=fs.readFileSync(p,'utf8');
s=rep(s,'desc = ExpJson.GetS(j, "desc"), progress =','desc = ExpJson.GetS(j, "desc"), edgeRole = LegacyEdgeRole(ExpJson.GetS(j, "kind"), ExpJson.GetS(j, "desc")), progress =');
const ins=s.indexOf('    // Replays one IN/OUT pair');
s=s.slice(0,ins)+[
'    // Old ASM logs have no role field. Decode only historical fixtures, never production candidates.',
'    static EdgeCandidateRole LegacyEdgeRole(string kind, string desc)',
'    {',
'        if (kind != "place") return EdgeCandidateRole.None;',
'        string d = desc ?? "";',
'        return d.StartsWith("기지 문 다시 달기") ? EdgeCandidateRole.DoorRepair',
'            : d.StartsWith("기지 문 달기") ? EdgeCandidateRole.DoorPlace',
'            : d.StartsWith("기지 안 횃불") ? EdgeCandidateRole.TorchPlace : EdgeCandidateRole.None;',
'    }','',
].join('\n')+s.slice(ins);fs.writeFileSync(p,s);
p=path.join(root,'Tools/core-tests/FixMergeReviewTests.cs');s=fs.readFileSync(p,'utf8');
s=rep(s,'static AsmCand E(string kind, string key, string desc) { var c = C(kind, key); c.desc = desc; return c; }','static AsmCand E(string kind, string key, string desc, EdgeCandidateRole role = EdgeCandidateRole.None) { var c = C(kind, key); c.desc = desc; c.edgeRole = role; return c; }');
s=rep(s,'"기지 안 횃불 놓기"));','"기지 안 횃불 놓기", EdgeCandidateRole.TorchPlace));');
s=rep(s,'"기지 문 다시 달기 — (8,4) 달았던 문이 없어짐"));','"기지 문 다시 달기 — (8,4) 달았던 문이 없어짐", EdgeCandidateRole.DoorRepair));');
const testAt=s.indexOf('        string note = AICandidateAssembler.EdgeCutNote(cut);');
s=s.slice(0,testAt)+[
'        var renamed = Crowd(true);',
'        foreach (var c in renamed.lEdge) c.desc = "a changed display sentence";',
'        var renamedResult = RunCut(renamed, out var renamedCut);',
'        Program.Check(string.Join("|", renamedResult.ConvertAll(c => c.key)) == string.Join("|", br.ConvertAll(c => c.key))',
'            && AICandidateAssembler.EdgeCutNote(renamedCut) == AICandidateAssembler.EdgeCutNote(cut),',
'            "R2a: changing every edge description leaves repair priority, quota, order and cut categories unchanged");',
'        Program.Check(!AICandidateAssembler.IsRepair(new AsmCand { kind = "place", desc = "기지 문 다시 달기" })',
'            && AICandidateAssembler.IsRepair(new AsmCand { kind = "place", edgeRole = EdgeCandidateRole.DoorRepair }),',
'            "R2a: repair meaning comes from the role, never from the description");',
'',
].join('\n')+s.slice(testAt);fs.writeFileSync(p,s);
console.log('R2a written');
