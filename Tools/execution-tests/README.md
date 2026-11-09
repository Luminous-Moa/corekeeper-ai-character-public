# Common execution regression

Run from the repository root with PowerShell.

```powershell
./Tools/execution-tests/run.ps1 -Baseline
./Tools/execution-tests/run.ps1
```

The baseline reads the unchanged `11eb461` goal and host-order source from Git and reproduces the ore-quota cancellation and premature completion of the copper-armour instruction. The changed run checks that quota and blocked-state observations keep the chosen execution alive, then exercises explicit replan, stop, new action and step completion.

The project links the actual `AIGoals`, `AISkill`, goal display and Core host-order/rule/run-gate sources. The runner extracts the actual `Apply`, `ObserveExecution`, `AIActions.Stale`, `AIBody.Stop`, `AIBody.EndSkill`, tail-stop decision, skill-ended callback, pending-choice cancellation helpers and data classes verbatim from their production files before compiling them. Source changes therefore reach the harness on the next run. It does not implement a second copy of those decisions.

`Stubs.cs` supplies Unity/world, inventory, physical input, planner/network and event boundaries. Its planner adapter calls the actual host-order rules. Its probe skill makes a single primary-use input so the test can observe whether the selected run survives. The production stop/end methods and skill-ended callback run against a controlled tail and pending-choice state. The late-stop cases use the production stale method and run gate. This verifies the common execution policy and explicit-control dispatch offline. It does not run the complete scheduling loop, pathfinding, game inventory transactions, remote model responses or a real mining swing. Full game compilation and live play are separate checks.
