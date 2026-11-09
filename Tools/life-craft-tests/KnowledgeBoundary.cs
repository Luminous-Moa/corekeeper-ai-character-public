using Unity.Entities;
// The real knowledge service is an ECS/disk observation boundary, not craft policy under test.
public static class AIKnowledge
{
    public static System.Collections.Generic.List<ObjectID> KnownItems(World world, Entity player)
        => new() { ObjectID.Wood, ObjectID.Torch };
}
