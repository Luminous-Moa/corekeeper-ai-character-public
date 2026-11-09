# Gathering sequence checks

Run `./Tools/gather-tests/run.ps1` from the repository root. The project links the entire production `AIGathering.cs`, `AISkill` and Unity-free grouping/inventory rules. Controlled ECS, tile and physical skill boundaries supply observations. The actual candidate producer, immutable selection, stale gate, Make dispatch, runtime adapter and sequence execute in the end-to-end case.

The tests cover connected same-kind grouping, a 301-target patch without a quantity cap, immutable selection, approach/action/recovery sequencing, observed inventory versus target counts, no newly discovered target expansion, interruption, resume of only the selected remaining targets, missing targets, actual operation failure, uncollected drops, and gains surviving later inventory spending.

Explicit user ceilings are checked before each selected target: a 9/10 receipt with five known single drops starts only one more, while ordinary target count 10 continues all five to 14. Unknown native yields and indivisible oversized stacks pause with facts. Individual pickup validation reobserves changed stack size. New visible ore/root/drop/object facts wake judgment once while retaining the current body and fixed target selection; selected-target recovery drops do not create unrelated discoveries.

The max_count wire contract is separately checked by `Tools/core-tests` group `GATHER-LIMIT`, `node Tools/ai-bridge/test/gather-limit.test.mjs`, and actual Protocol → Planner.SetGoalFrom → Goals linkage in `Tools/life-tests`.

This is an offline production orchestration test. Physical mining, pathfinding and pickup are controlled boundaries and are compiled against installed game assemblies separately; those game actions and remote model choices are not run by this harness.

`./Tools/gather-tests/native/run.ps1` separately compiles actual MineSkill/RootSkill/BreakSkill and the unchanged extracted TunnelSkill class against controlled world/navigation boundaries. It checks actual primary inputs with no ceiling, then suppression when an explicit ceiling arrives, as well as a pure walk replan encountering the next wall. `-Baseline` uses pre-fix `e1cedcf` skill source and reproduces the unintended mining primary input despite the ceiling; this is defect reproduction, not a runtime-success claim. Farm harvest uses actual secondary input and is covered by the production-linked life suite, including real crop/PlantCD observation instead of hoe/progress candidate metadata.
