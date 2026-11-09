# 장비 정비 빈 선택지 구현 결과 (M1·M2·M3)

- worktree `C:\Dev\CoreKeeperModSDK-gap-maint`, 브랜치 `gap-maint-20261108`, 기준 20b1ace.
- 커밋 `bff88f9` "Repair beside a forge pending, walk back to an unseen own forge, give up a lost pending".
- 바뀐 파일: `Assets/AIPlayerSpike/Scripts/AIMaintenance.cs`(+214/−26), `Tools/maintenance-tests/`(GapCases.cs 신규, Program.cs, BrainStubs.cs, WorldStubs.cs).
- 공용 파일은 바꾸지 않았다. AIActions·AILife·AIPlanner·AIBrain 모두 그대로다. 포기 행동도 기존 `maintain` kind 안에서 `AIMaintenance.Make`가 나눠 처리한다.
- M4(파우치)는 가방 포크가 맡는다.

## 항목별 구현

### M3 업그레이드 pending 중 일반 수리 허용 — 완료
- **pending 분리:** 캐릭터 상태의 pending을 둘로 나눴다(`AIMaintenance.cs:63-64`).
  - `Pending`: 업그레이드·회수용. 대장간에 넣은 물품을 기록한다.
  - `Repair`: 게시한 수리 요청용.
- **수리 허용 조건:** 수리는 물품을 넣지 않으므로 대장간 pending이 있어도 후보로 나온다. 다만 그 pending이 게시한 요청이 아직 처리되지 않았으면 기다린다. 동시에 처리 중인 요청은 하나뿐이다(`RepairBlockedBy`, `:436`).
- **적용 위치:** ExactCandidate, Stale, Begin이 이 규칙을 따른다. 새 업그레이드 투입은 기존처럼 막힌다. 막혔다는 사실 문장에 "투입 없는 일반 수리는 가능"을 덧붙였다.
- **분리에 맞춘 처리:**
  - ObservePending이 두 pending을 각각 관측한다(`:582`).
  - SlotBarrier는 두 pending의 장벽을 모두 적용한다(`:635`).
  - Complete와 CleanupIntent는 두 칸을 모두 정리한다.
- **재료 사실 고정:** 레벨 변화를 확인한 순간의 재료 변화 사실을 고정한다(`MaterialFact`). 사이에 수리한 재료 소비가 업그레이드 재료 보고에 섞이지 않는다.

### M1 화면 밖 대장간에 남은 자기 장비 회수 — 완료
- **이동 후보:** `Recovery`에서 대장간 칸이 시야 밖이면 그 정확한 위치로 가는 이동 후보를 낸다(`:372-384`). 위치는 pending에 저장된 타일이므로 기억 목록이 없어도 된다. 사용하는 경로는 기존 기억 시설 이동과 같은 `AICraft.StandTilesFor` → `AIApproach.FirstCached` → `WalkCandidate`다.
- **원래 묶음 유지:** 이동 후보의 life에는 원래 묶음이 그대로 실린다. 같은 목표에서 이미 강화된 물품이면 원래 upgrade 묶음이다. progress는 false다.
- **도착 뒤:** 정상 회수 후보로 다시 판단한다.
- **경로를 못 찾을 때:** 경로가 없으면 사실 문장만 남긴다.
- **잃음 판정 안 함:** 시야 밖인 상태는 잃은 것으로 판정하지 않는다.

### M2 회수 불가 pending 포기 — 완료
- **판정** (`ObserveLoss`, `:455`): 대장간 칸을 지금 볼 수 있을 때만 판정한다.
  - 그 자리에 대장간이 없으면 "그 자리에 대장간이 없음"이다.
  - 투입은 확인됐고 회수는 확인되지 않았으며 자기 요청 처리가 끝났는데, 입력 칸이 비었거나 다른 물건이 있으면 잃음이다.
  - 처리가 끝난 자기 take가 main에 들어온 경우는 제외한다.
  - 물품이 다시 보이면 판정을 거둔다.
- **판단 모델에 주는 것** (`AbandonCandidate`, `:507`): 사실 문장과 '포기' 선택지를 준다. 선택지는 정비 루트와 recover 말단에 나온다. PendingText와 Report에도 같은 사실이 들어간다. 잃었더라도 포기하기 전에는 새 투입을 계속 막는다.
- **고르면** (`MaintenanceAbandonSkill`, `:805`; `Abandon`, `:517`):
  - 몸 행동 없이 pending을 지우고, LastResult에 "장비 미회수"를 남긴다.
  - 상위 LLM에는 기존 `AIPlanner.RequestReplan`(trigger `action_replan`)으로 상황 문장을 보낸다. 문장은 `정비 장비를 잃음 — 행동 판단이 회수를 포기함: …`이다.
  - 따지라는 지시는 넣지 않았다.
- **가져간 사람** (`PlayersNear`, NearRange 6칸):
  - 장비가 마지막으로 보였을 때와 사라진 것을 본 때 대장간 6칸 안에 있던 플레이어 이름을 합친다.
  - "추정 — 게임은 꺼낸 사람을 기록하지 않음"을 붙인다.
  - 아무도 없었으면 모른다고 쓴다.
  - 대장간 자체가 없어진 경우에는 이름을 대지 않는다. 누가 치웠는지, 장비가 어디로 갔는지 모르며 땅에 떨어졌을 수 있다고(미확인) 쓴다.
  - 이름은 `PlayerCustomizationCD.customization.name`, 없으면 `PlayerGhost.onlineName`이다. AIAgents의 기존 방식과 같다.
- **오래된 선택 거절:** 포기를 고른 뒤 물품이 다시 보이면 그 선택은 Stale로 거절된다.

## 시험
- `Tools/maintenance-tests/run.ps1`: **144 passed, 0 failed.** 기존 115개에 GapCases 29개를 더했다.
  - M3: 대장간 pending 옆 수리의 실제 요청과 내구도 확인. 수리 뒤에도 pending을 보존하고, 재강화 없이 정확히 회수. 업그레이드 요청 처리 중에는 수리 대기. 새 목표에서는 투입만 막힘.
  - M1: 시야 밖이면 이동 후보, 원래 upgrade 묶음 유지, 말단에도 이동 후보, 다시 보이면 정상 회수.
  - M2: 칸이 빔·다른 물건·대장간 제거·근처 없음·시야 밖·다시 보임. 포기 dispatch, `action_replan` 1회와 문장 내용(이름 포함, '따지' 없음), 포기 뒤 새 업그레이드 가능.
- `dotnet build Tools/compile-check -c Release`: 오류 0.
- `Tools/execution-tests/run.ps1`: 181/181. `Tools/core-tests`: ALL CHECKS OK (1571).
- **Unity 6000.0.59f2 `HeadlessSdk.CompileCheck`: exit 0, error CS 0, "CompileCheck: ok".** 로그는 worktree `Logs/compile-gap-maint.log`이다.
  - 실행 전에 CoreKeeper.exe·Unity.exe가 없음을 확인했다.
  - 환경은 20b1ace의 `CoreKeeperModSDK-observation-redecision`에서 복사했다. Plugins/CoreKeeper, manifest/lock, ProjectSettings 2개, Library 캐시다.
  - 환경 파일과 Unity가 다시 만든 ConditionsTable은 커밋하지 않았다.
- 실게임·실제 모델·설치·공식 병합은 하지 않았다.

## 추정·미구현·통합 주의
- **추정:**
  - 가져간 사람은 근처 플레이어로만 추정한다. 그 사이에 왔다 간 사람은 관측 시점에 따라 빠질 수 있다.
  - 대장간이 부서질 때 내용물이 떨어지는지는 확인하지 못했다.
  - 시야 안에 있는 대장간 엔티티는 로드돼 있다고 보고 "없음"을 판정한다.
- **미구현:**
  - 수리 pending 자체가 처리 확인을 끝내 못 하는 경우의 포기는 합의 범위(대장간 없음/칸 빔) 밖이라 넣지 않았다. 이 경우 막히는 것은 수리뿐이다.
  - 연결 리셋으로 pending을 잃은 뒤 대장간에 남은 장비는 기존 명세 §9대로 범위 밖이다.
- **통합 주의:**
  - M4(가방 포크)가 `SourceSlots`/`ActiveSlot`에 파우치를 넣으면, 이 커밋의 `MainHolds`(main 범위만 확인)와 `Begin`의 두 pending 구조와 같이 맞춰야 한다. 충돌 예상 위치는 `AIMaintenance.cs` 71-75, 235-241 부근이다.
  - 상위 알림은 기존 `RequestReplan`만 썼다. 다른 포크가 상자 공용 등으로 AIPlanner를 바꿔도 겹치지 않는다.
  - 이동전달-개선4건의 AIBrain.Loop·AIDecisions 변경과 겹치는 파일은 없다.
