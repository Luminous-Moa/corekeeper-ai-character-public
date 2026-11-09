# Production cooking adapter

Run `dotnet run --project Tools/life-cook-tests -c Release` from the repository root.

The project directly links the complete production `AICook.cs`, including `LifeEntries`, `ExactCandidate`, `Stale`, and `CookSkill`. `Boundary.cs` supplies only Unity/ECS data access, game recipe data and normal UI request/consumer boundaries. It does not copy the candidate or skill policies.

The scenarios check a valid non-recommended pair, second owned pot, exact ingredient order/variation, request batching, partial first/second input movement, automatic consumption with output growth, one-time fill, split identical-ingredient stacks and whole-stock versus exact-source batch capacity, requested-amount take, main inventory receipt versus cursor residue, stale slots/output, and approach-only remembered off-screen pots. It also checks the 2026-11-08 gap additions: an input1-only pot filled into input0, clearing an input slot back into the bag, and a top-up of the input that already holds the ingredient. These are offline adapter checks; they do not run a model/API or the actual game consumer.
