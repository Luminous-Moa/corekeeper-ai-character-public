# Life inventory production adapter

Run from the repository root:

```powershell
& Tools/life-inventory-tests/ExtractBase.ps1
& Tools/life-inventory-tests/ExtractLifeBinding.ps1
dotnet run --project Tools/life-inventory-tests -c Release
```

The existing bag final-review adapter cases were copied from the fixed `1fc89b9` evidence into this isolated test directory. The project links the current production `AIInventory` / `InventorySkill`, inventory rules and hand lease. `ExtractBase.ps1` extracts the current production AIBase preparation helpers verbatim; do not edit those slices to change policy.

`LifeCases.cs` exercises selected chest inspect, the complete source-slot axis beyond twelve slots, exact variation/quantity MoveAmount, actual two-sided main inventory receipt, unchanged original progress for a separate action and replacement stale rejection.

ECS, DB information, route availability, surrounding modules and the UI request queue are adapters. State changes are supplied by the fixture. This does not run the game consumer, model API, full AILife navigation or Unity. ExtractLifeBinding.ps1 extracts the production AILife prefix verbatim, including Sync, Matches, Bind, Resolve, ForGoal and Remaining. The adapter does not duplicate matching/count policy. Full navigation/Compose and the game consumer remain outside this module adapter.