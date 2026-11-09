using Unity.Entities;
static partial class Program
{
    static void ExtendedHandChecks()
    {
        foreach(var id in new[]{ObjectID.Bow,ObjectID.SummonTome,ObjectID.BeamStaff,ObjectID.Bomb,ObjectID.Drill,ObjectID.Sledge,ObjectID.Wood})
        {
            var(w,p,b)=Setup(); b[14]=Item(id,0,100);
            Goal(w,p,new(){kind=GoalKind.Hold,item=id,slot=14,where="hotbar:3",count=1});
            var candidates=AIInventory.Candidates(w,p,Observe(w,p),"");
            Check(candidates.Count==1&&candidates[0].inventory.mode=="hold","non-melee main item receives exact hold choice: "+id);
            var move=candidates[0].inventory;var skill=Start(w,p,move);
            Check(AICraft.Requests.Count==1,"normal swap request rather than direct item mutation: "+id);
            Swap(w,move);w.EntityManager.Put(p,new EquippedObjectCD{equippedSlotIndex=3,containedObject=b[3]});skill.Tick(w,p,1);
            Check(skill.Status==SkillStatus.Done&&b[14].objectID==ObjectID.None&&b[3].objectID==id,"observed exact hand swap: "+id);
            bool mining=id==ObjectID.Drill||id==ObjectID.Sledge;
            if(id!=ObjectID.Wood)Check(AIInventory.TryChosenHand(w,p,mining,out int chosen,out _,out _)&&chosen==3,"selected hand lease persists for game type: "+id);
            else Check(AIInventory.TryChosenHand(w,p,false,out int chosen,out _,out string why)&&chosen<0&&why.Length>0,"held nonweapon does not become an attack weapon");
        }
    }
}
