from pathlib import Path
r=Path('C:/Dev/CoreKeeperModSDK-unrestricted')
p=r/'Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs';s=p.read_text(encoding='utf-8');s=s.replace('// eat for hunger / fetch food from an own chest (AIFood, 생활-기지): only with a goal and no enemy within combat\n            // range; offered after the goal\'s own options in the usual set, never in the close-fight set','// Hunger food / own-chest supplies remain choices with no goal and during combat too.');s=s.replace('if (Goal.kind != GoalKind.None && !inCombat) { try { lLife = AIFood.Candidates(server, player, o, curKey, lCombat); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: food candidates failed", e); } }','try { lLife = AIFood.Candidates(server, player, o, curKey, lCombat); } catch (Exception e) { AIPlayerSpikeMod.LogError("brain: food candidates failed", e); }');p.write_text(s,encoding='utf-8')
p=r/'Tools/restriction-tests/prepare.mjs';s=p.read_text(encoding='utf-8');s=s.replace("fs.writeFileSync(new URL('Generated.cs',import.meta.url),code);",'''function balanced(source,signature){const a=source.indexOf(signature);if(a<0)throw Error(signature);const b=source.indexOf('{',a);let d=1,e=b+1;for(;e<source.length&&d;e++){if(source[e]==='{')d++;if(source[e]==='}')d--;}return source.slice(a,e);}
const brain=fs.readFileSync(new URL('Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs',root),'utf8');
code+='\\npublic partial class FoodProbe {\\n'+balanced(brain,'        public void Food()')+'\\n}';
code+='\\npublic static partial class AIFood {\\n'+balanced(base,'    public static List<AICandidate> Candidates(World server, Entity player, AIObservation o, string curKey, List<AICandidate> combat)')+'\\n}';
fs.writeFileSync(new URL('Generated.cs',import.meta.url),code);''');p.write_text(s,encoding='utf-8')
p=r/'Tools/restriction-tests/Program.cs';s=p.read_text(encoding='utf-8').replace('public enum GoalKind{Goto,Place}','public enum GoalKind{None,Gather,Follow,Goto,Place}');s=s.replace('Console.WriteLine("ALL PASS "+checks);','''foreach(var goal in new[]{GoalKind.None,GoalKind.Gather})foreach(bool danger in new[]{false,true}) {
 var f=new FoodProbe{Goal=new AIGoal{kind=goal},inCombat=danger,o=new AIObservation{hunger=90}};f.Food();
 Check(f.lLife.Exists(c=>c.kind=="eat"),$"actual Food producer calls real AIFood.Candidates at90hunger goal={goal} combat={danger}");
 var empty=new List<AsmCand>();var foods=f.lLife.ConvertAll(c=>new AsmCand{kind=c.kind,key=c.key,desc=c.desc});
 var input=new AsmInput{goal=goal,combat=danger,closeFight=danger,lLife=foods,lCont=new(),lFollow=new(),lEnemy=new(),lCombat=new(),lCraft=new(),lMine=new(),lDig=new(),lRemember=new(),lItem=new(),lExplore=new(),lDigExplore=new(),lOther=new(),foodPicks=new()};
 var offered=AICandidateAssembler.Select(input,_=>true,_=>true,out _,out _,out _);AICandidateAssembler.Finish(offered,new AsmCand{kind="wait",key="wait"});
 Check(offered.Exists(c=>c.kind=="eat"),$"actual final assembler preserves produced food goal={goal} combat={danger}");
}
Console.WriteLine("ALL PASS "+checks);''')
s+='''
public class AICandidate {public string kind,key,desc;public ObjectID itemId;public int itemVariation;}
public class AIObservation{public int hunger;}
public partial class FoodProbe {public World server=new();public Entity player;public AIObservation o;public string curKey="";public List<AICandidate> lCombat=new(),lLife;public bool foodNeed,inCombat;public AIGoal Goal;}
public static partial class AIFood {public class Food{public ObjectID id=ObjectID.Wood;public int variation,hunger=10,count=5;public string heal="buff";}public static List<Food> InBag(World w,Entity p)=>new(){new()};public static string Meaning(int h)=>"hunger facts";public const string Rule="observed food data";public static bool NeedsFood(World w,Entity p,AIObservation o)=>false;}
public static class AIBase{public static List<AICandidate> FoodTakeCandidates(World w,Entity p,AIObservation o,string k,string why)=>new();}
''';p.write_text(s,encoding='utf-8')
p=r/'Tools/restriction-tests/RestrictionTests.csproj';s=p.read_text(encoding='utf-8').replace('</ItemGroup>','<Compile Include="../../Assets/AIPlayerSpike/Scripts/Core/AICandidateAssembler.cs" Link="AICandidateAssembler.cs" /></ItemGroup>');p.write_text(s,encoding='utf-8')
