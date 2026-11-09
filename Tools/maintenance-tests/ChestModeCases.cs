using Inventory;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// 상자 열림 모드 (사용자 결정 2026-11-08): while a chest is open the decision offers only that chest's work and closing it;
// closing returns to the ordinary surface; a hit (the existing hp-drop trigger) and a vanished chest end the mode.
public static class ChestModeCases
{
    static Entity Chest(Fixture f,int index=40)
    {
        var em=f.World.EntityManager;var chest=new Entity{Index=index,Version=1};
        em.Put(chest,new InventoryAutoTransferEnabledCD());em.Put(chest,new LocalTransform{Position=new float3(1,0,1)});em.Put(chest,new ObjectDataCD{objectID=ObjectID.Chest});
        var items=Fixture.Buffer<ContainedObjectsBuffer>(4);items[0]=Fixture.Item(ObjectID.Wood,5);em.PutBuffer(chest,items);
        var inv=Fixture.Buffer<InventoryBuffer>();inv.Add(new(){startIndex=0,size=4,sizeX=4,sizeY=1,maxSize=4,extraInventorySizeSlot=-1});em.PutBuffer(chest,inv);
        AIBase.Memories.Add(new(){id=ObjectID.Chest,tile=new int2(1,1)});return chest;
    }
    static System.Func<int,int,int> Lost=(a,b)=>a-b;
    static List<AICandidate> Go(Fixture f,List<AICandidate> now,string action)
    {var c=now.First(x=>x.inventoryMenu?.action==action);AIInventoryMenu.ApplyNavigation(f.World,f.Player,c,out _);return AIInventoryMenu.Candidates(f.World,f.Player,f.Observation,"");}
    public static void Run()
    {
        var f=Fixture.Create();AIChestMode.Reset();var chest=Chest(f);AIInventory.Opened(chest);
        Program.Check(AIChestMode.Active(f.World,f.Player,100,10,Lost,out var open)&&open==chest,"chest mode: an open chest in reach holds the mode");
        var root=AIInventoryMenu.ChestModeCandidates(f.World,f.Player,f.Observation,"",chest);
        var actions=root.Select(x=>x.inventoryMenu?.action).ToList();
        Program.Check(root.All(x=>x.kind=="inventory_menu")&&actions.Contains("chest-take")&&actions.Contains("chest-store")&&actions.Contains("chest-close")&&!actions.Contains("own-chests")&&!actions.Contains("bulk")&&!actions.Contains("presets"),"chest mode: the surface is the chest's take, store and close only (no other lists): "+string.Join(",",actions));
        var takeCats=Go(f,root,"chest-take");
        Program.Check(takeCats.Any(x=>x.inventoryMenu?.action?.StartsWith("category:")==true)&&!takeCats.Any(x=>x.inventoryMenu?.action=="own-chests")&&takeCats.Any(x=>x.inventoryMenu?.action=="chest-close"),"chest mode: taking lists the chest's items by category, close stays");
        var items=Go(f,takeCats,takeCats.First(x=>x.inventoryMenu.action.StartsWith("category:")).inventoryMenu.action);
        var ops=Go(f,items,items.First(x=>x.inventoryMenu?.action?.StartsWith("item:")==true).inventoryMenu.action);
        var opNames=ops.Where(x=>x.inventoryMenu?.action?.StartsWith("operation:")==true).Select(x=>x.inventoryMenu.action).ToList();
        Program.Check(opNames.Count==1&&opNames[0]=="operation:take","chest mode: a chest item offers only taking it (no wear/hold): "+string.Join(",",opNames));
        AIInventoryMenu.Reset("test");root=AIInventoryMenu.ChestModeCandidates(f.World,f.Player,f.Observation,"",chest);
        var storeCats=Go(f,root,"chest-store");
        var bagItems=Go(f,storeCats,storeCats.First(x=>x.inventoryMenu?.action?.StartsWith("category:")==true).inventoryMenu.action);
        var bagOps=Go(f,bagItems,bagItems.First(x=>x.inventoryMenu?.action?.StartsWith("item:")==true).inventoryMenu.action);
        var bagOpNames=bagOps.Where(x=>x.inventoryMenu?.action?.StartsWith("operation:")==true).Select(x=>x.inventoryMenu.action).ToList();
        Program.Check(bagOpNames.All(x=>x=="operation:store"),"chest mode: an own item offers only storing it into this chest: "+string.Join(",",bagOpNames));
        // Merge review G-1: a final take/store clears the menu path, but the chest stays open; the no-goal idle gate sees the
        // open chest as an open choice view, so the decision goes on until close.
        AIInventoryMenu.Reset("final move chosen (path cleared)");
        Program.Check(!AIInventoryMenu.Active&&AIChestMode.Open&&!AIRunGate.IdleHold(false,true,false,false,false,false,false,AIInventoryMenu.Active||AIChestMode.Open),"merge review G-1: with no goal, an open chest after a final move keeps the decision going (idle gate not held)");
        Program.Check(AIRunGate.IdleHold(false,true,false,false,false,false,false,AIInventoryMenu.Active),"merge review G-1: without the chest term the official gate alone would have held the call");
        root=AIInventoryMenu.ChestModeCandidates(f.World,f.Player,f.Observation,"",chest);
        Program.Check(root.Any(x=>x.inventoryMenu?.action=="chest-close"),"merge review G-1: the next decision is the chest surface again, with close");
        // close returns to the ordinary surface
        var close=AIInventoryMenu.Candidates(f.World,f.Player,f.Observation,"").First(x=>x.inventoryMenu?.action=="chest-close");
        AIInventoryMenu.ApplyNavigation(f.World,f.Player,close,out var note);
        Program.Check(!AIChestMode.Active(f.World,f.Player,100,10,Lost,out _)&&!AIInventoryMenu.Active&&note.Contains("닫음"),"chest mode: close ends the mode and the menu; the ordinary surface comes back");
        // a hit ends the mode (existing hp-drop trigger); an enemy merely seen does not (no such input here)
        AIInventory.Opened(chest);AIChestMode.Active(f.World,f.Player,100,10,Lost,out _);
        Program.Check(AIChestMode.Active(f.World,f.Player,95,10,Lost,out _),"chest mode: a small health loss below the hit trigger keeps the mode");
        Program.Check(!AIChestMode.Active(f.World,f.Player,88,10,Lost,out _)&&AIChestMode.LastEnded.Contains("피격")&&!AIChestMode.Active(f.World,f.Player,88,10,Lost,out _),"chest mode: a hit (loss reaching the trigger) ends the mode and closes the chest");
        // a vanished chest releases the mode
        AIInventory.Opened(chest);AIChestMode.Active(f.World,f.Player,100,10,Lost,out _);AIInventoryMenu.ChestModeCandidates(f.World,f.Player,f.Observation,"",chest);f.World.EntityManager.Remove(chest);
        Program.Check(!AIChestMode.Active(f.World,f.Player,100,10,Lost,out _)&&AIChestMode.LastEnded.Contains("없어짐")&&!AIInventoryMenu.Active,"chest mode: a destroyed chest releases the mode and the menu rooted at it");
        // moving away releases it too
        chest=Chest(f,41);AIInventory.Opened(chest);f.World.EntityManager.Put(chest,new LocalTransform{Position=new float3(9,0,9)});
        bool still=AIChestMode.Active(f.World,f.Player,100,10,Lost,out _);
        Program.Check(!still&&AIChestMode.LastEnded.Contains("멀어짐"),"chest mode: moving away from the open chest releases the mode");
    }
}
