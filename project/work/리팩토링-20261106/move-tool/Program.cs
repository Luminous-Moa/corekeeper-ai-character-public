using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;

string root = "C:/Dev/CoreKeeperModSDK-refactor-20261106/Assets/AIPlayerSpike/Scripts/";
string output = "C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/리팩토링-20261106/";
var manifest = new List<string>();
CompilationUnitSyntax Parse(string text) => CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
string Tokens(SyntaxNode n) => string.Join(" ", n.DescendantTokens().Select(t => t.Text));
string Hash(string s) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s)));
Dictionary<string, string> Bodies() {
 var d = new Dictionary<string,string>();
 foreach(var file in Directory.GetFiles(root,"*.cs",SearchOption.AllDirectories))
 foreach(var type in Parse(File.ReadAllText(file)).Members.OfType<BaseTypeDeclarationSyntax>()) {
  string name=type.Identifier.Text;
  var bodies = type is TypeDeclarationSyntax c ? c.Members.Select(m=>Hash(Tokens(m))).ToArray() : new[]{Hash(Tokens(type))};
  foreach(var h in bodies) { var k=name+":"+h; d[k] = d.TryGetValue(k,out var n) ? (int.Parse(n)+1).ToString() : "1"; }
 }
 return d;
}
if(args.Contains("--walk")) {
 var oldDescriptions=new List<string>();var newDescriptions=new List<string>();
 foreach(var file in new[]{"AIBase.cs","AICook.cs","AIBrain.Prep.cs","AIFarm.cs","AIPlaces.cs"}) {
  var p=new System.Diagnostics.Process();p.StartInfo=new System.Diagnostics.ProcessStartInfo("git"){WorkingDirectory="C:/Dev/CoreKeeperModSDK-refactor-20261106",RedirectStandardOutput=true,UseShellExecute=false};p.StartInfo.ArgumentList.Add("show");p.StartInfo.ArgumentList.Add("3c31536:Assets/AIPlayerSpike/Scripts/"+file);p.Start();var old=Parse(p.StandardOutput.ReadToEnd());p.WaitForExit();
  foreach(var obj in old.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Where(n=>n.Type.ToString()=="AICandidate")) {
   var f=obj.Initializer.Expressions.OfType<AssignmentExpressionSyntax>().ToDictionary(a=>a.Left.ToString(),a=>a.Right);
   if(f.GetValueOrDefault("kind")?.ToString()!="\"dig\"" || f.GetValueOrDefault("key")?.ToString().Contains("digedge:")==true)continue;
   oldDescriptions.Add(Hash(Tokens(f["desc"])));
  }
  foreach(var call in Parse(File.ReadAllText(root+file)).DescendantNodes().OfType<InvocationExpressionSyntax>().Where(n=>n.Expression.ToString()=="AIApproach.WalkCandidate"))newDescriptions.Add(Hash(Tokens(call.ArgumentList.Arguments.Last().Expression)));
 }
 bool same=oldDescriptions.Order().SequenceEqual(newDescriptions.Order());if(!same)throw new Exception("walk descriptions differ");File.WriteAllText(output+"r-description-tokens.txt",oldDescriptions.Count+" original candidate description expressions exactly match the new call argument tokens\n");Console.WriteLine("Walk description expressions match: "+oldDescriptions.Count);return;
}
if(args.Contains("--fields")) {
 string Git(string path) {
  var p=new System.Diagnostics.Process(); p.StartInfo=new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory="C:/Dev/CoreKeeperModSDK-refactor-20261106",RedirectStandardOutput=true,UseShellExecute=false };
  p.StartInfo.ArgumentList.Add("show");p.StartInfo.ArgumentList.Add("f512924:Assets/AIPlayerSpike/Scripts/"+path);p.Start();string s=p.StandardOutput.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Exception(path);return s;
 }
 var oldTypes=new[]{"AIBodySkills.cs","AICraft.cs","AIExplore.cs","AIPlayerSpikeMod.cs","AIEdge.cs","AIEdge.Guard.cs"}.SelectMany(f=>Parse(Git(f)).Members.OfType<TypeDeclarationSyntax>()).ToArray();
 var newTypes=Directory.GetFiles(root,"*.cs").SelectMany(f=>Parse(File.ReadAllText(f)).Members.OfType<TypeDeclarationSyntax>()).ToArray();
 var lines=new List<string>();
 foreach(var group in oldTypes.GroupBy(t=>t.Identifier.Text)) {
  IEnumerable<MemberDeclarationSyntax> Initializers(TypeDeclarationSyntax t)=>t.Members.Where(m=>m is FieldDeclarationSyntax || m is PropertyDeclarationSyntax p && p.Initializer!=null);
  var a=group.SelectMany(Initializers).Select(Tokens).ToArray();
  var b=newTypes.Where(t=>t.Identifier.Text==group.Key).SelectMany(Initializers).Select(Tokens).ToArray();
  if(!a.SequenceEqual(b))throw new Exception("field order changed "+group.Key);
  lines.Add(group.Key+": "+a.Length+" field/property initializer declarations in identical order");
 }
 File.WriteAllText(output+"m-field-order.txt",string.Join("\n",lines)+"\n");Console.WriteLine("Field declaration/initializer sequence identical for "+lines.Count+" types");return;
}
var before = Bodies();
void Save(string name,string text) {
 File.WriteAllText(root+name,text);
 if(!File.Exists(root+name+".meta")) File.WriteAllText(root+name+".meta","fileFormatVersion: 2\nguid: "+Guid.NewGuid().ToString("N")+"\n");
}
void Split(string source, params (string dest,string types)[] groups) {
 var text=File.ReadAllText(root+source); var u=Parse(text);
 var header=string.Concat(u.Usings.Select(x=>x.ToFullString()));
 var all=u.Members.OfType<BaseTypeDeclarationSyntax>().ToDictionary(t=>t.Identifier.Text);
 if(groups.SelectMany(g=>g.types.Split(',')).Count()!=all.Count) throw new Exception("type coverage "+source);
 foreach(var (dest,names) in groups) {
  var parts=names.Split(',').Select(n=>all[n]).ToList();
  Save(dest,header+string.Concat(parts.Select(p=>p.ToFullString())));
  manifest.Add(source+" | "+names+" | "+dest);
 }
 if(!groups.Any(g=>g.dest==source)) {
  string keep=groups[0].dest;
  // Only these explicitly mapped source files are removed; the old GUID follows the owner.
  if(!Path.GetFullPath(root+source).StartsWith(Path.GetFullPath(root))) throw new Exception("scope");
  File.Delete(root+source);
  File.Copy(root+source+".meta",root+keep+".meta",true);
  File.Delete(root+source+".meta");
 }
}
Split("AIBodySkills.cs",("AIBody.cs","AIBody"),("AISkill.cs","BodyInput,SkillStatus,AISkill"),("AIVision.cs","AIVision"),("AITileReader.cs","AITileReader,AIWallData"),("AIObservation.cs","SeenEnemy,AIObservation"),("AINavigator.cs","NavStatus,AINavigator"),("AIInspectSkills.cs","LookSkill,InvSkill"),("AIGotoSkill.cs","GotoSkill"),("AIMineSkill.cs","MineSkill"),("AIAttackSkill.cs","AttackSkill"),("AIMoveTrace.cs","WalkTestSkill,AIMoveTrace"));
Split("AICraft.cs",("AICraft.cs","AICraft,CraftSkill"),("AIPlace.cs","AIPlaceSpot,PlaceSkill"),("AIBreakSkills.cs","RootSkill,BreakSkill"));
Split("AIExplore.cs",("AIMemory.cs","AIMemory"),("AIDrops.cs","AIDrops"),("AIObjects.cs","AIObjects"),("AITunnel.cs","TunnelPlan,AITunnel,TunnelSkill"));
Split("AIPlayerSpikeMod.cs",("AIPlayerSpikeMod.cs","AIPlayerSpikeMod"),("AIPlayerSpikePatches.cs","ThinClientDriverConstructor,AIPlayerSpikePatches"),("AIWorldSettings.cs","AIWorldSettings"),("AICharacterStore.cs","AICharacterStore"));
Split("AIEdge.cs",("AIEdge.cs","AIEdge"),("AIEdgeSkills.cs","EdgeFillSkill,DoorPassSkill,DoorCloseSkill"));
Split("AIEdge.Guard.cs",("AIEdge.Guard.cs","AIEdge"),("AIPickupSkill.cs","PickupSkill"));
string MemberName(MemberDeclarationSyntax m) => m switch { MethodDeclarationSyntax n=>n.Identifier.Text, PropertyDeclarationSyntax p=>p.Identifier.Text, FieldDeclarationSyntax f=>f.Declaration.Variables[0].Identifier.Text, _=>"" };
void Partial(string source,string dest,string typeName,Func<MemberDeclarationSyntax,bool> pick,string declaration) {
 var text=File.ReadAllText(root+source); var u=Parse(text); var type=u.Members.OfType<ClassDeclarationSyntax>().Single();
 var selected=type.Members.Where(pick).ToArray();
 if(selected.Length==0) throw new Exception("empty partial");
 var header=string.Concat(u.Usings.Select(x=>x.ToFullString()));
 var body=string.Concat(selected.Select(m=>m.ToFullString()));
 foreach(var m in selected.Reverse()) text=text.Remove(m.FullSpan.Start,m.FullSpan.Length);
 if(!type.Modifiers.Any(t=>t.IsKind(SyntaxKind.PartialKeyword))) text=text.Replace("class "+typeName,"partial class "+typeName);
 Save(source,text); Save(dest,header+"\n"+declaration+"\n{\n"+body+"}\n");
 manifest.Add(source+" | "+string.Join(",",selected.Select(MemberName))+" | "+dest);
}
var bodyNames="HandleCommand,TestOnlyWhere,TestOnlyTiles,IsSkillCommand,IsTestCommand,TestSwitchPath,TestCommandsOn".Split(',');
Partial("AIBody.cs","AIBody.Commands.cs","AIBody",m=>bodyNames.Contains(MemberName(m)),"public static partial class AIBody");
var chatNames="TryHandleChatCommand,TryHandleMention,IsAll,Without,DefaultAgent,Dispatch,IsAiCommand,StageText,StatusText,StatusAll,Reply,FlushReplies,MentionNames".Split(',');
Partial("AIPlayerSpikeMod.cs","AIPlayerSpikeMod.Chat.cs","AIPlayerSpikeMod",m=>chatNames.Contains(MemberName(m)),"public partial class AIPlayerSpikeMod");
bool reach=false;
Partial("AIEdge.Guard.cs","AIEdge.Reach.cs","AIEdge",m=>{if(MemberName(m)=="ReachRange")reach=true; var take=reach;if(MemberName(m)=="CannotCome")reach=false;return take;},"public static partial class AIEdge");
var after=Bodies();
var changes=before.Keys.Union(after.Keys).Where(k=>before.GetValueOrDefault(k)!=after.GetValueOrDefault(k)).ToArray();
File.WriteAllText(output+"m-token-check.txt","Direct type member token multiset changes: "+changes.Length+"\n"+string.Join("\n",changes));
File.WriteAllText(output+"09-move-manifest.md","# M 이동 manifest\n\n기준 f512924. 각 형식/멤버 본문은 동일 토큰으로 이동했다. 원본/대상 경로는 Assets/AIPlayerSpike/Scripts 아래다. AIBodySkills→AIBody, AIExplore→AIMemory는 기존 GUID를 유지했다.\n\n| 원본 | 형식/멤버 | 대상 |\n|---|---|---|\n"+string.Join("\n",manifest.Select(x=>"| "+x+" |"))+"\n");
Console.WriteLine("Token body differences: "+changes.Length);
if(changes.Length>0) throw new Exception("token check");
