# Life craft production adapter

```powershell
& Tools/life-craft-tests/ExtractLifeBinding.ps1
dotnet run --project Tools/life-craft-tests -c Release
```

Links production AICraft/CraftSkill and AIInventory helpers. Exercises two current manual stations, materials supplied by any chest within 10 tiles of the station (2026-11-08 shared-chest decision; far chest excluded, chest slot change stale, bag+chest consumption corroborated), remembered auto-process stations excluded from craft items and places, a second-station exact selection shared by direct and tree leaves, an output-two recipe requested for three items (two calls, expected four), normal Craft UI queue payload, production awaiting cursor stow without false bag receipt or timeout completion, actual main receipt with extra output, material/recipe replacement rejection, and auto-process exclusion.

ECS/DB/native containers, route availability, surrounding modules and normal UI request queue representation are adapters; result buffer changes are supplied by the fixture. This does not execute the game consumer or model API. ExtractLifeBinding.ps1 extracts the production AILife binding/lifecycle prefix verbatim. The project also links the complete production AIBrain.Prep.cs and exercises WantedItems through a thin wrapper: output two, goal three, Wood two executes the identical tree/direct payload, and Wood one reports shortage one using the same runtime cost metadata. No binding or Prep decision policy is copied into fixtures.