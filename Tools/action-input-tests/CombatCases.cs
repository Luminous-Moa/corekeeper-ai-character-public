using Unity.Entities;
using Unity.Mathematics;
partial class Program
{
 static void CombatChecks()
 {
  var w=new World();var p=new Entity{Index=1};AIBody.Position=new float3(0,0,0);AIBody.Weapon=ObjectID.None;
  var o=new AIObservation{pos=AIBody.Position,time=0};o.enemies.Add(new SeenEnemy{entity=new Entity{Index=9},id=ObjectID.Slime,pos=new float3(1,0,0),dist=1,health=20,maxHealth=20});
  var choices=AICombatOptions.Candidates(w,p,o,"");
  Check(choices.Exists(c=>c.kind=="hit"&&c.entity.Index==9),"non-Brute has actual hit candidate");
  var step=choices.Find(c=>c.kind=="step"&&c.targetLabel.Contains("뒤 한 칸"));
  Check(step!=null,"non-Brute has actual short retreat candidate");
  AISkill skill=ProductionStepFactory.Make(step);Check(skill.Start(w,p,0),"actual AIActions step expression creates actual StepSkill");
  var input=skill.Tick(w,p,0.1f);Check(input.move.x<0&&input.slot==-1&&!input.primary,"actual StepSkill outputs selected backward movement without attack");
  AIBody.Position=new float3(step.dest.x,0,step.dest.y);skill.Tick(w,p,0.3f);
  Check(skill.Status==SkillStatus.Done&&skill.Reason.Contains("도착"),"actual StepSkill completes only after observed position arrives");
  AIBody.Fight=false;choices=AICombatOptions.Candidates(w,p,o,"");Check(!choices.Exists(c=>c.kind=="hit"),"unusable selected hand cannot expose fake hit capability");
  AIBody.Fight=true;AIBody.Weapon=ObjectID.Bow;choices=AICombatOptions.Candidates(w,p,o,"");Check(choices.Exists(c=>c.kind=="attack")&&!choices.Exists(c=>c.kind=="hit"),"ranged hand exposes aim/use rather than melee hit");
  AIInventory.UseSelected=true;AIObservation.Current=o;o.enemies[0].pos=new float3(8,0,0);o.enemies[0].dist=8;
  w.EntityManager.Put(new Entity{Index=99},new SecondaryUseCD{hasSecondaryUse=true,hasWindup=true,windupTiers=1,summonsMinion=true});
  w.EntityManager.Put(o.enemies[0].entity,new HealthCD{health=20,maxHealth=20});
  w.EntityManager.Put(o.enemies[0].entity,new Unity.Transforms.LocalTransform{Position=o.enemies[0].pos});
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{currentWindup=0.5f});
  choices=AICombatOptions.Candidates(w,p,o,"");var secondary=choices.Find(c=>c.actionMode=="secondary");
  Check(secondary!=null,"production candidate exposes actual secondary capability");
  Check(secondary.desc.Contains("전부 해산")&&secondary.desc.Contains("새로 소환하지 않음"),"max summon use truthfully describes dismissal rather than individual summon");
  var single=choices.Find(c=>c.actionMode=="summon");Check(single!=null&&single.desc.Contains("최대 충전 전에 해제"),"individual summon has its own pre-max-release choice");
  var summonSkill=ProductionStepFactory.Attack(single);Check(summonSkill.SecondaryUse&&summonSkill.IndividualSummon&&summonSkill.Start(w,p,0),"actual Make preserves individual summon mode into actual skill");
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{currentWindup=0});
  var summonInput=summonSkill.Tick(w,p,0.01f);Check(summonInput.secondary&&!summonInput.primary,"individual summon starts normal secondary hold");
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{currentWindup=0.2f,currentWindupTier=0,atMaxWindup=false});
  summonInput=summonSkill.Tick(w,p,0.1f);
  Check(!summonInput.secondary&&!summonInput.primary,"individual summon releases before maximum rather than toggling existing summons off");
  var individualResult=GameSummonBoundary.Release(false,true);
  Check(individualResult=="spawn"&&(summonSkill.Reason == null || !summonSkill.Reason.Contains("소환 성공")),"non-max release chooses source-grounded SpawnMinion request with same-kind minion present, not dismissal");
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{currentWindup=0});summonSkill.Tick(w,p,0.2f);
  Check(summonSkill.Status==SkillStatus.Done,"individual summon input completes after game windup reset without inventing spawn observation");
  var missed=ProductionStepFactory.Attack(single);Check(missed.Start(w,p,0),"late individual summon accepted target");
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{currentWindup=1,atMaxWindup=true});
  summonInput=missed.Tick(w,p,0.1f);Check(summonInput.secondary&&missed.Status==SkillStatus.Running&&missed.Facts.Exists(f=>f.Contains("時点")||f.Contains("시점을 놓침")),"missed pre-max window never silently substitutes destructive max release");
  AIVision.Visible=false;summonInput=missed.Tick(w,p,0.2f);
  Check(!summonInput.secondary&&missed.Status==SkillStatus.Failed&&missed.Reason.Contains("해산/전체 교체")&&GameSummonBoundary.Release(true,true)=="dismiss-matching","missed window plus visibility-loss release reports possible dismissal and failure, never individual summon success");
  AIVision.Visible=true;
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{currentWindup=0.5f});
  var attack=ProductionStepFactory.Attack(secondary);Check(attack.SecondaryUse,"actual attack Make expression preserves secondary selection");
  Check(attack.Start(w,p,0),"actual AttackSkill accepts selected observed target");
  input=attack.Tick(w,p,0.1f);Check(input.secondary&&!input.primary&&input.aim&&input.point.x==8&&input.slot==3,"actual secondary AttackSkill aims and holds secondary at distant target");
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{atMaxWindup=true,currentWindup=1});
  input=attack.Tick(w,p,0.3f);Check(!input.secondary&&!input.primary&&attack.Status==SkillStatus.Running,"actual AttackSkill releases after actual max windup observation");
  Check(GameSummonBoundary.Release(true,true)=="dismiss-matching"&&GameSummonBoundary.Release(true,false)=="replace-all","max-release consumer boundary toggles matching minions off or replaces all, distinctly from individual spawn");
  w.EntityManager.Put(p,new PlayerEquipment.EquipmentSlotCD{currentWindup=0});attack.Tick(w,p,0.4f);
  Check(attack.Status==SkillStatus.Done&&attack.Reason.Contains("20→20"),"actual secondary completes input cycle without inventing damage");
  attack=ProductionStepFactory.Attack(choices.Find(c=>c.kind=="attack"&&c.actionMode!="secondary"));Check(!attack.SecondaryUse&&attack.Start(w,p,1),"actual normal Make preserves primary use");
  input=attack.Tick(w,p,1.1f);Check(input.primary&&!input.secondary&&input.point.x==8&&input.move.x==0,"actual primary AttackSkill shoots at observed distance instead of walking to melee range");
  AIVision.Visible=false;input=attack.Tick(w,p,1.2f);Check(!input.primary&&!input.secondary,"actual skill stops firing after target leaves vision");
  AIVision.Visible=true;AIInventory.UseSelected=false;
 }
}
// Fixture for the external game consumer, grounded in SummoningWeaponSlot.SummonWithItem and
// MinionSummonAndDismissSystem SpawnMinion/ChargeUpMinionAction. This records request semantics, not live entities.
static class GameSummonBoundary
{
 public static string Release(bool atMax,bool sameKindPresent)=>!atMax?"spawn":sameKindPresent?"dismiss-matching":"replace-all";
}
