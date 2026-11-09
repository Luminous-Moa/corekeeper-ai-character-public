# Travel production checks

`dotnet run --project Tools/travel-tests -c Release`

Links the production AITravel, TravelSkill, GotoSkill, AISkill and AITravelRules (AIAreaRules, AreaTracker, ObservedGrid). The fixture supplies visible world geometry, actors, persistence serialization and a physical navigator boundary that walks along open cells; it runs the actual area/exit analysis, area questions, exit answers, crossing, connector following, walking into an area while an answer is on the way, knowledge updates and known-map path search.

Checks the area-exit design cases (plain room, jagged cave behind a neck, width-1 T junction, exits appearing while crossing, dead-end room, re-entry of the same area) with their area-question counts, a width-2 corridor followed as one way, dead ends, the one-cell loop stop, exit experience across runs and reloads (travel-memory v1/v2/v3 read, v4 written; records keyed by gate cell and outward direction), final-review cases (area split/merge/asked-ahead relabel without a second question, a chosen unseen side keeping its bearing, the area line made at preparation, analysis rate on a blocked cell), no off-screen reads, observed closure/reopening, per-character/world persistence, the 97x97 analysis time and a 599-tile remembered path. This is offline production logic coverage, not a remote model/game/server-input or full AINavigator physics test. Full installed-assembly compile is `dotnet build Tools/compile-check -c Release`.

Exact geometry starts accumulating from real view observations. The old 4x4-cell `seen` flags do not reconstruct unrecorded walkability; old saves need to observe such routes again.

`dotnet run --project Tools/travel-tests/navigator -c Release`

Additionally links the actual production AINavigator and AITravel/Core. Uses the installed game's Unity.Mathematics assembly, with controlled world/input/door boundaries. Exercises Start/Tick/path preparation/steering/arrival for Store, Take and Break tolerances approaching a nonwalkable object footprint; checks intervening-wall, already-near-wall and diagonal-corner rejection. This proves production navigation logic offline, not real game physics, door input or rendering.

`powershell -NoProfile -File Tools/travel-tests/candidate-progress/run.ps1`

Runs the existing execution harness, then compiles source slices from the actual walking/frontier/remembered-ore candidate initializers and the production HasProgress predicate. The slices retain all initializer fields before descriptive prose. They run through production AIGoals.CheckAfterBuild and AIExpedition.AfterBuild from the execution harness assembly. Checks that optional exploration remains available under Gather/Home/Goto without claiming task progress, Explore counts it, actual remembered target ore prevents a false exhaustion report, and unrelated remembered ore does not. This tests purpose/report classification, not frontier discovery or tunnel execution.
