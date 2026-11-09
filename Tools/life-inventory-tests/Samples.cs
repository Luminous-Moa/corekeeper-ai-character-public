using Unity.Entities;
static partial class Program {
static void Samples(){
 var(w,p,b)=Setup();b[12]=Item(ObjectID.CopperBreastArmor,1,300);b[41]=Item(ObjectID.CopperBreastArmor,0,150);b[15]=Item(ObjectID.MiningPick);b[16]=Item(ObjectID.CopperSword);var(chest,box)=Chest(w);box[0]=Item(ObjectID.Wood,0,20);
 Goal(w,p,new(){kind=GoalKind.Equip,item=ObjectID.CopperBreastArmor,slot=12,where="chest",count=1});
 foreach(var c in AIInventory.Candidates(w,p,Observe(w,p),""))Console.WriteLine("SAMPLE EQUIP ["+c.kind+" "+c.key+"] "+c.desc);
 Goal(w,p,new(){kind=GoalKind.Other});var list=AIInventory.Candidates(w,p,Observe(w,p),"");Console.WriteLine("SAMPLE GENERAL INVENTORY COUNT="+list.Count);
 foreach(var c in list.Take(4))Console.WriteLine("SAMPLE GENERAL ["+c.kind+" "+c.key+"] "+c.desc);
 Goal(w,p,new(){kind=GoalKind.Take,item=ObjectID.Wood,count=5,where="chest:1,0"});
 foreach(var c in AIInventory.Candidates(w,p,Observe(w,p),""))Console.WriteLine("SAMPLE TAKE CLOSED ["+c.kind+" "+c.key+"] "+c.desc);
 AIInventory.Opened(chest);foreach(var c in AIInventory.Candidates(w,p,Observe(w,p),""))Console.WriteLine("SAMPLE TAKE OPEN ["+c.kind+" "+c.key+"] "+c.desc);
}
}
