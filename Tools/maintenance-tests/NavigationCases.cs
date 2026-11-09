using Unity.Entities;

public static class NavigationCases
{
    static List<AICandidate> Page(Fixture f)
    {
        var prior=new List<AICandidate>{new(){kind="continue",key="probe",value="c1",run=AIBody.Current?.Run??0},new(){kind="eat",key="food",value="c2"},new(){kind="doorclose",key="door",value="c3"}};
        var page=AIBrain.AssembleLifePage(f.World,f.Player,f.Observation,"probe",false,prior);
        AIBrain.AddCommonControls(page,false,AIBody.Current);return page;
    }
    static void Nav(Fixture f,string op,string value=null,bool prefetch=false)
    {
        var page=Page(f);var chosen=page.First(x=>x.lifeNav?.Op==op&&(value==null||x.lifeNav.Value==value));var body=AIBody.Current;var held=AIBrain.HeldChoice;var queued=AIBrain.QueuedChoice;var goal=AIGoals.Goal;int applied=AIBrain.Applied,started=AIBrain.StartedAnswers;
        AIBrain.Response(f.World,f.Player,page,chosen,prefetch);
        Program.Check(ReferenceEquals(body,AIBody.Current)&&ReferenceEquals(held,AIBrain.HeldChoice)&&ReferenceEquals(queued,AIBrain.QueuedChoice)&&ReferenceEquals(goal,AIGoals.Goal)&&applied==AIBrain.Applied&&started==AIBrain.StartedAnswers,"production maintenance nav "+op+" preserves body, held/queued, goal and execution counters"+(prefetch?" during prefetch":""));
    }
    public static void Run()
    {
        var f=Fixture.Create();f.Plan("repair");AIBody.StartSkill(f.World,f.Player,new ProbeSkill(),0);AIBrain.SeedPending(AIBody.Current);
        Nav(f,"open",prefetch:true);var root=Page(f);Program.Check(root.Count(x=>x.lifeNav?.Op=="branch")==6&&root.Any(x=>x.lifeNav?.Value=="maintenance")&&root.Any(x=>x.lifeNav?.Value=="rest"),"actual shared root exposes maintenance beside the other branches (rest added 2026-11-08) without losing prior branches");
        Nav(f,"branch","maintenance");Nav(f,"mode","repair");Nav(f,"slot","12");Nav(f,"where",f.RepairId);
        var leaf=Page(f).Single(x=>x.kind=="maintain");var direct=f.Exact();Program.Check(leaf.key==direct.key&&leaf.maintenance.BeforeItem.EqualsExact(direct.maintenance.BeforeItem),"actual navigated maintenance leaf is identical to directly specified production request");
        var page=Page(f);AIBrain.Response(f.World,f.Player,page,leaf);var skill=AIBody.Current;f.Tick(skill);f.ConsumeNext();f.Tick(skill);f.End(skill);
        Program.Check(((ILifeResult)skill).ConfirmedWork==1&&AILife.RequestSatisfied(AILife.ForGoal(AIGoals.Goal)),"production response selection of navigated repair reaches actual structured result and one shared goal progress");
        var full=Fixture.Create();full.Plan("repair");full.Bag[55]=Fixture.Item(ObjectID.CookingPotHelm,30,1,42);var presets=Fixture.Buffer<EquipmentPresetsBuffer>();presets.Add(new(){equipment=Fixture.Gear(41)});presets.Add(new(){equipment=Fixture.Gear(55)});full.World.EntityManager.PutBuffer(full.Player,presets);full.World.EntityManager.Put(full.Player,new ActiveEquipmentPresetCD{Value=0});
        var slots=AIMaintenance.LifeEntries(full.World,full.Player,full.Observation,"",new LifeSelection{Branch="maintenance",Mode="repair"});
        Program.Check(!slots.Any(x=>x.Op=="slot"&&(x.Id=="50"||x.Id=="55")),"cursor output and inactive-preset equipment are excluded from the maintenance source axis");
    }
}
