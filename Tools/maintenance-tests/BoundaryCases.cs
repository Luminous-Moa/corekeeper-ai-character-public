using Inventory;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

public static class BoundaryCases
{
    public static void Queue()
    {
        var f=Fixture.Create();var data=Create.RepairOrReinforce(f.Player,12,ObjectID.CookingPotHelm,1,f.Repair,f.Player,false,false);
        f.UI.Add(new(){tick=new(101)});
        bool first=AICraft.QueueCraftAction(f.World,f.Player,data,out var a,out _);
        bool second=AICraft.QueueCraftAction(f.World,f.Player,data,out var b,out _);
        Program.Check(first&&second&&a.Value==102&&b.Value==103,"strict normal UI queue avoids same-tick collisions with all not-yet-applied entries");
        f.World.EntityManager.Put(f.Player,new UIActionsCD{lastActionTick=new(200)});f.UI.Add(new(){tick=new(201)});
        AICraft.QueueCraftAction(f.World,f.Player,data,out var c,out _);
        Program.Check(c.Value==202,"strict queue posts after both current and last applied ticks while ignoring retained old UI history");
        int before=f.UI.Length;f.World.EntityManager.RemoveComponent<UIActionsCD>(f.Player);
        Program.Check(!AICraft.QueueCraftAction(f.World,f.Player,data,out _,out _)&&f.UI.Length==before&&f.Crafts.Length==0,"maintenance never falls back to an unobservable CraftBuffer queue when UI receipt is unavailable");
    }
    public static void Exact()
    {
        var f=Fixture.Create();f.Plan("repair");var candidate=f.Exact();var changed=f.Bag[12];changed.auxDataIndex++;f.Bag[12]=changed;
        Program.Check(AIMaintenance.Stale(f.World,f.Player,candidate)!=null&&!AIActions.Make(candidate).Start(f.World,f.Player,0),"same item ID with a replaced aux identity is stale in both action and skill start");
        f=Fixture.Create();f.Plan("repair");candidate=f.Exact();changed=f.Bag[12];var d=changed.objectData;d.variation=1;changed.objectData=d;f.Bag[12]=changed;
        Program.Check(AIMaintenance.Stale(f.World,f.Player,candidate)!=null,"changed exact variation does not substitute another level of the same item");
        f=Fixture.Create();f.Plan("repair");candidate=f.Exact();Boundary.RepairCosts=new(){(ObjectID.Wood,3)};
        Program.Check(AIMaintenance.Stale(f.World,f.Player,candidate)!=null&&f.UI.Length==0,"game helper cost change invalidates the frozen request before posting");
        f=Fixture.Create();f.Plan("repair");f.Bag[0]=Fixture.Item(ObjectID.Wood,1);
        Program.Check(f.Exact()==null&&AIMaintenance.Report(f.World,f.Player).Contains("부족"),"insufficient main materials produce a preparation fact rather than a free request");
        f=Fixture.Create();f.Plan("repair");f.Bag[12]=Fixture.Item(ObjectID.CookingPotHelm,90,0,17);
        Program.Check(f.Exact()==null,"already maximum durability is not a newly successful repair");
        f=Fixture.Create();f.Plan("upgrade");f.Bag[12]=Fixture.Item(ObjectID.CookingPotHelm,30,20,17);
        Program.Check(f.Exact()==null,"maximum level cannot produce another upgrade leaf");
        f=Fixture.Create();f.Plan("upgrade");f.ForgeItems[5]=Fixture.Item(ObjectID.CookingPotHelm,50,3,999);
        Program.Check(f.Exact()==null&&!AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance"}).Any(x=>x.Op=="mode"&&x.Id=="recover"),"preexisting foreign forge input is neither overwritten nor adopted as own pending");
        f=Fixture.Create();f.Plan("upgrade");for(int i=0;i<40;i++)if(i!=12)f.Bag[i]=Fixture.Item(ObjectID.Wood,20);
        Program.Check(f.Exact()==null,"full main bag prevents upgrade before a normal recovery destination can be guaranteed");
        f=Fixture.Create();f.Plan("repair");candidate=f.Exact();f.World.EntityManager.Remove(f.Repair);
        Program.Check(AIMaintenance.Stale(f.World,f.Player,candidate)!=null,"selected station entity disappearance rejects the candidate");
        f=Fixture.Create();f.Plan("repair");candidate=f.Exact();AIVision.Visible=false;
        Program.Check(AIMaintenance.Stale(f.World,f.Player,candidate)!=null,"loss of current station observation prevents execution");AIVision.Visible=true;
        f=Fixture.Create();f.Plan("repair");Boundary.QuerySystem=null;
        Program.Check(f.Exact()==null&&f.UI.Length==0,"unknown public lookup/DB state is not shown as executable");
        f=Fixture.Create();f.Plan("repair");candidate=f.Exact();var skill=f.Start(candidate);f.World.EntityManager.Put(f.Player,new LocalTransform{Position=new(100,0,0)});f.Tick(skill);
        Program.Check(skill.Status==SkillStatus.Failed&&f.UI.Length==0,"distance lost after selection cannot send a remote repair");
    }
    public static void PartialRepair()
    {
        var f=Fixture.Create();f.Plan("repair");var skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext(changeMaterials:false);f.Tick(skill);
        Program.Check(((ILifeResult)skill).ConfirmedWork==1&&f.Bag[0].amount==20&&skill.Facts.Any(x=>x.Contains("실제 감소 0")),"actual repaired equipment can count work while missing material decrease remains a separate explicit fact");
        f=Fixture.Create();f.Plan("repair");skill=f.Start(f.Exact());f.Tick(skill);f.ConsumeNext(changeTarget:false);f.Tick(skill);f.Tick(skill);
        Program.Check(((ILifeResult)skill).ConfirmedWork==0&&f.Bag[0].amount==18&&f.UI.Length==1,"materials-only change stays zero work and does not trigger another repair request");
        f=Fixture.Create();f.Plan("repair");skill=f.Start(f.Exact());f.Tick(skill);var posted=f.UI[0];f.ApplyTick(posted.tick);var changed=f.Bag[12];var d=changed.objectData;d.amount=90;changed.objectData=d;f.Bag[12]=changed;f.Tick(skill);
        Program.Check(((ILifeResult)skill).ConfirmedWork==0&&skill.Status==SkillStatus.Running,"equipment change at the posted tick cannot bypass actual next-tick processing proof");
    }
    public static void Capacity()
    {
        var f=Fixture.Create();f.Bag[41]=f.Bag[12];f.Bag[12]=default;
        var presets=Fixture.Buffer<EquipmentPresetsBuffer>();presets.Add(new(){equipment=Fixture.Gear(41)});presets.Add(new(){equipment=Fixture.Gear(55)});f.World.EntityManager.PutBuffer(f.Player,presets);f.World.EntityManager.Put(f.Player,new ActiveEquipmentPresetCD{Value=0});f.Plan("upgrade",41);var candidate=f.Exact();f.World.EntityManager.Put(f.Player,new ActiveEquipmentPresetCD{Value=1});
        Program.Check(candidate!=null&&AIMaintenance.Stale(f.World,f.Player,candidate)!=null,"active equipment preset change rejects the old gear source slot");
        f=Fixture.Create();var em=f.World.EntityManager;var primary=new Entity{Index=20};PugDatabase.Prefabs[(ObjectID.Bag,0)]=primary;em.Put(primary,new LevelCD{level=6});em.Put(primary,new DurabilityCD{maxDurability=90});em.Put(primary,new ExtraInventoryCD{size=5});var levels=Fixture.Buffer<LevelEntitiesBuffer>();for(int i=0;i<=20;i++)levels.Add(new(){entity=primary});em.PutBuffer(primary,levels);f.Bag[55]=Fixture.Item(ObjectID.Bag,60,0,33);f.Bag[44]=Fixture.Item(ObjectID.Wood,1);var equipment=Fixture.Gear();equipment.bagIndex=55;em.Put(f.Player,equipment);var inventories=em.GetBuffer<InventoryBuffer>(f.Player);var main=inventories[0];main.size=45;main.maxSize=45;main.extraInventorySizeSlot=55;inventories[0]=main;f.Plan("upgrade",55,item:"Bag");
        Program.Check(f.Exact()==null&&AIInventory.CapacityAfterRemoval(f.World,f.Player,55)!=null,"removing an equipped capacity provider is rejected when an extra slot would be lost with an item");
    }
    public static void Isolation()
    {
        var f=Fixture.Create();f.Plan("repair");var candidate=f.Exact();var owner=AIAgent.Cur;AIAgent.Cur=new();
        Program.Check(AIMaintenance.Stale(f.World,f.Player,candidate)!=null,"frozen maintenance request cannot cross character ownership");AIAgent.Cur=owner;
        Program.Check(AIMaintenance.Stale(new World(),f.Player,candidate)!=null&&AIMaintenance.Stale(f.World,new Entity{Index=f.Player.Index,Version=f.Player.Version+1},candidate)!=null,"frozen maintenance request cannot cross world reference or player entity version");
        f=Fixture.Create();f.Plan("upgrade");var skill=f.Start(f.Exact());f.Tick(skill);AIBody.Stop(f.World,f.Player,0,"test stop",true);f.ConsumeNext();owner=AIAgent.Cur;AIAgent.Cur=new();
        Program.Check(!AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance"}).Any(x=>x.Op=="mode"&&x.Id=="recover"),"a second character never inherits another character's normal put pending");AIAgent.Cur=owner;
        Program.Check(AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance"}).Any(x=>x.Op=="mode"&&x.Id=="recover"),"original character still owns its observed pending after another character inspects options");
        AIMaintenance.Reset();Program.Check(!AIMaintenance.LifeEntries(f.World,f.Player,f.Observation,"",new LifeSelection{Branch="maintenance"}).Any(x=>x.Op=="mode"&&x.Id=="recover"),"world/character reset drops temporary evidence and does not adopt existing forge contents");
    }
}
