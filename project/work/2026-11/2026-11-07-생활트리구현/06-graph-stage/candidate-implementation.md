# 생활 트리 격리 후보의 코드 참고

## 고정 후보와 지위

후보 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`, 기반 `1fc89b93edbee31a9001a174581e32568119efc1`를 Git blob으로 고정했다. 현재 지위는 **격리 구현 후보·검증 진행중**이다. 공식 ai-character의685dde4 코드 그래프를 이 후보로 바꾸지 않았고, 설치·실게임·최종 독립 수용은 확인하지 않았다.

이 판은 HEAD가 고정한 최종 생산 후보이며 최종 독립 수용 지위는 pending이다. 이전24c61c2는 중간 참고로 보존한다. 제품 코드/Tools의 고정 상태와 자체검증 보고는 아래 별도 근거로 구분하고 공식 반영·설치·실게임으로 승격하지 않는다.

이 문서의 자체 확인은 Git blob 신원·소스 앵커의 존재와 정적 추출이다. 실행/컴파일 수치는 생산자가 남긴 고정판 자체검증 보고이며 이 그래프 컨텍스트가 재실행한 검증이 아니다.

## 대표 코드와 경계

기존 제품 코드층에서 사용하는 Roslyn Extract.dll을 재사용했다. 관련20개 생산소스 중 변경8개만 다시 추출하고 동일 blob의 미변경12개를 재사용해 실제 함수38개의 신원·범위를 확인했다. 선택 심볼 사이 정적 관계14개, 구문 진단0개다. delta 범위 밖 호출을 새로 추정하지 않아 부분 정적 관계 수는 이전판과 다를 수 있다. Unity/게임 metadata를 로드하지 않은 semantic 진단은 제품 컴파일/런타임 실패를 뜻하지 않는다. 호출 관계는 정적 후보이며 실행 trace가 아니다.

정식 프로젝트 그래프의 기존 상위 기획·공통실행 및 코드 기준은 유지한다. 별도 candidate-project-preview.json은 기획↔후보 코드의 부분 색인이지 정식 제품 그래프 전체가 아니다.

| 실제 심볼 | 후보 원본 |
|---|---|
| `AILife.Compose(World, Entity, AIObservation, string, bool, System.Collections.Generic.List<AICandidate>, AICandidate)` | [AILife.cs:136](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:136) |
| `AILife.Navigate(LifeNav)` | [AILife.cs:187](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:187) |
| `AILife.ExecuteStale(AICandidate)` | [AILife.cs:194](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:194) |
| `AILife.Starting(AICandidate, AISkill)` | [AILife.cs:211](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:211) |
| `AILife.Ended(AISkill)` | [AILife.cs:215](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:215) |
| `AILife.Report()` | [AILife.cs:226](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:226) |
| `AILife.ForGoal(AIGoal)` | [AILife.cs:77](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:77) |
| `AILife.Remaining(LifeSelection)` | [AILife.cs:103](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:103) |
| `LifeCursor.Check(LifeNav, string, int, System.Func<string, string, bool>)` | [AILifeTree.cs:32](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/Core/AILifeTree.cs:32) |
| `LifeCursor.Apply(LifeNav, string, int, System.Func<string, string, bool>)` | [AILifeTree.cs:51](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/Core/AILifeTree.cs:51) |
| `LifeProgress.Record(int, LifeSelection, ILifeResult)` | [AILifeTree.cs:101](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/Core/AILifeTree.cs:101) |
| `AILifeGoalForm.Check(PlanStep)` | [AILifeTree.cs:118](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/Core/AILifeTree.cs:118) |
| `AIBrain.HandleResponse(World, Entity, float)` | [AIBrain.Loop.cs:760](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:760) |
| `AIBrain.Apply(World, Entity, float, AICandidate)` | [AIBrain.Loop.cs:904](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:904) |
| `AIActions.Make(AICandidate)` | [AIActions.cs:74](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIActions.cs:74) |
| `AIActions.Stale(World, Entity, AICandidate)` | [AIActions.cs:77](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIActions.cs:77) |
| `AIFarm.LifeEntries(World, Entity, AIObservation, string, LifeSelection)` | [AIFarm.cs:649](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIFarm.cs:649) |
| `AIFarm.ExactCandidate(World, Entity, AIObservation, string, LifeSelection)` | [AIFarm.cs:690](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIFarm.cs:690) |
| `FarmSkill.Start(World, Entity, float)` | [AIFarm.cs:1050](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIFarm.cs:1050) |
| `FarmSkill.Tick(World, Entity, float)` | [AIFarm.cs:1125](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIFarm.cs:1125) |
| `AICook.LifeEntries(World, Entity, AIObservation, string, LifeSelection)` | [AICook.cs:552](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICook.cs:552) |
| `AICook.ExactCandidate(World, Entity, AIObservation, string, LifeSelection)` | [AICook.cs:613](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICook.cs:613) |
| `CookSkill.Start(World, Entity, float)` | [AICook.cs:822](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICook.cs:822) |
| `CookSkill.Tick(World, Entity, float)` | [AICook.cs:868](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICook.cs:868) |
| `AICraft.LifeEntries(World, Entity, AIObservation, string, LifeSelection)` | [AICraft.cs:51](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICraft.cs:51) |
| `AICraft.ExactCandidate(World, Entity, AIObservation, string, LifeSelection)` | [AICraft.cs:89](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICraft.cs:89) |
| `CraftSkill.Start(World, Entity, float)` | [AICraft.cs:605](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICraft.cs:605) |
| `CraftSkill.Tick(World, Entity, float)` | [AICraft.cs:622](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AICraft.cs:622) |
| `AIInventory.LifeEntries(World, Entity, AIObservation, string, LifeSelection)` | [AIInventory.cs:240](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIInventory.cs:240) |
| `AIInventory.CandidatesFor(World, Entity, AIObservation, string, AIGoal, AIGoal)` | [AIInventory.cs:217](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIInventory.cs:217) |
| `InventorySkill.Start(World, Entity, float)` | [AIInventory.cs:693](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIInventory.cs:693) |
| `InventorySkill.Tick(World, Entity, float)` | [AIInventory.cs:701](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIInventory.cs:701) |
| `AIPlanProtocol.Read(string)` | [AIPlanProtocol.cs:50](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/Core/AIPlanProtocol.cs:50) |
| `AIBrain.WantedItems(World, Entity, out System.Collections.Generic.List<AICandidate>)` | [AIBrain.Prep.cs:26](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIBrain.Prep.cs:26) |
| `AILife.Resolve(LifeSelection)` | [AILife.cs:76](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AILife.cs:76) |
| `AIBrain.AssembleLifePage(World, Entity, AIObservation, string, bool, System.Collections.Generic.List<AICandidate>, AICandidate)` | [AIBrain.Candidates.cs:19](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:19) |
| `AIBrain.AddCommonControls(System.Collections.Generic.List<AICandidate>, bool, AISkill)` | [AIBrain.Candidates.cs:22](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:22) |
| `AIBrain.OnSkillEnded(AISkill, float)` | [AIBrain.cs:373](candidate-code/c4a0f5e38016/snapshot/Assets/AIPlayerSpike/Scripts/AIBrain.cs:373) |

파일별 Git blob OID·SHA256는 [candidate-code/c4a0f5e38016/input-manifest.json](candidate-code/c4a0f5e38016/input-manifest.json), 정적 원자료는 [csharp.raw.json](candidate-code/c4a0f5e38016/csharp.raw.json)에 있다.
