# 판단 선택지 세 항목 구현

2026-11-06. 기준 `ai-character/902721e` → 최초 후보 `85f48145b68d02be434dbef9a6c76247b9b91277` → R1 수정 후보 **`b25099f6fd31e4c4c3618357eb9e8101e0e17124`**. 작업 트리 `C:/Dev/CoreKeeperModSDK-choice-fixes`, Git 브랜치 `choice-fixes-20261106`. 별도 트리를 만들었고 원본 SDK 설정/운영 데이터와 `CoreKeeperModSDK-base`는 변경하지 않았다. 최초 커밋 뒤 작업 트리는 clean이었다. 후속 Unity 담당의 SDK7파일 준비 변경은 미커밋 보존하며 구현 커밋에 넣지 않았다. 이 구현 담당은 병합·push·게임/Unity 실행·공통 설치·네트워크 모델 호출을 하지 않았다.

## 독립 검수 R1 반영

최초 후보의 위험 후처리는 `digEdgeOk != null`을 대안 경로 표시로 잘못 사용했다. 일반 home/goto/place에도 허용 둘레 칸 목록이 붙으므로 이 값은 경로 선택자가 아니다. 현재 후보는 **`digDetour`**를 사용한다. `AIWallRisk.RouteText`가 실제 선택한 본/대안 경로에서만 문구를 얻으며, 선택한 경로가 없으면 다른 경로의 사실을 빌려오지 않는다.

수정 뒤 [CHOICE 회귀](02-fix-choice-tests.txt) **22개 통과**, [compile-check](02-fix-compile.txt) **오류0**. 대안 없음의 본 경로, 본 위험/대안 안전, 본 안전/대안 위험, 선택한 대안 없음, production의 digDetour 연결을 추가 확인했다. 기존 전체 관문은 최초 후보에서 실패0이며 이번 국소 수정 후에는 관련 시험/컴파일만 재실행했다. 아래 전체 관문 1079개는 최초 후보의 수치로 구분한다.

## 구현 결과

### 폭발성 벽의 위험 설명

- `AIWallData.RiskText`가 `AIGameData.PrimaryPrefab`으로 게임 프리팹을 읽고 `IsExplosiveCD`가 있으며 `ignoreExploding=false`인지 검사한다. 이름에 Explosive가 들어가는지를 추측하거나 특정 자연 벽 하나를 하드코딩하지 않는다.
- 공통 문장 `Core/AIWallRisk.cs`: “폭발성 벽: 채굴로 폭발할 수 있어 가까이 있으면 피해 위험이 있음(피해량·범위 미확인). 이 캐기·파기 행동에는 폭발을 기다리며 물러나는 동작이 없음.”
- 직접 캐기 후보, 최종 목록의 모든 dig 후보(탐험·기억한 장소·상자·농사·기지 등의 이동), 둘레를 여는 별도 경로에 적용한다. 둘레 경로 후보는 일반 경로가 아닌 `plan.alt`의 벽을 검사한다. `TunnelPlan.RiskText`는 같은 경로의 여러 폭발성 벽에도 문장을 한 번만 반환한다.
- `MineSkill.Progress`와 `TunnelSkill.Progress`에도 같은 정보를 붙여 계속하기에서 사라지지 않게 했다. 캐기/파기 실행 동작이나 위험 행동 허용 정책은 바꾸지 않았다.
- 기지 둘레 사실과 폭발 사실은 독립적이다. 기존 EdgeWallNote/WithEdgeFact 및 4a의 예정 AIEdgeFacts를 대체하지 않는다.

로컬 게임 코드 근거:

- `C:/Dev/_ref/corekeeper-decomp/Pug.Other/TileDamageSystem.cs:517` 이후: 타일 피해를 HealthChange로 적용한다.
- `C:/Dev/_ref/corekeeper-decomp/Pug.Other/ExplosiveSystem.cs:229` 이후: `IsExplosiveCD`와 `ignoreExploding`, 파괴된 폭발체의 실제 폭발 생성 경로(`CreateExplosion`)를 확인했다. 이 근거에서 폭발 가능성만 설명하며 피해 수치/반경/카운트다운 시간은 주장하지 않는다.
- `C:/Dev/_ref/corekeeper-decomp/Pug.Objects/WallNaturalExplosiveBlock.cs`: 자연 폭발 벽의 `WallExplosiveBlock` 상속. 후자의 Wobble은 체력 감소 시 점멸/폭발 카운트다운 연출을 수행한다.
- 기존 knowledge `rules.json`의 `wall-break-ways`는 적의 벽 파괴 경로에 관한 규칙이다. 이 기능과 동일한 문장이 아니므로 새 판정에 복제하지 않았다.

### 목표 재료·필요한 음식의 줍기 후보 보존

- `CandBuild.Items`가 거리순 2개에서 관측 순회를 중단하던 조건을 없앴다. 가방에 들어갈 수 있는지와 현재 실행 중인 동일 key의 제외는 그대로 검사한다.
- `Core/AIPickupOptions.Select`가 전체 직접 줍기 후보에서 최대 두 개를 남긴다. 목표 품목 하나, 가방에 음식이 없고 음식이 필요한 경우 음식 하나를 먼저 남기고, 남은 칸은 기존 거리순 후보로 채운다. 하나의 후보가 두 필요를 충족하면 같은 행동을 중복해 넣지 않는다. 동일 key도 한 칸만 사용한다.
- 실패/몸 조건 `Doable`을 최종 선별 전에 검사하므로 가까운 실패 후보가 뒤의 유효 후보를 가리지 않는다. BagHasRoomFor는 후보 생성 전 기존 경계에서 검사한다.
- 자동 유입 비교는 **동일 필요 안에서만** 한다. 목표 자동 유입 물건은 일반 접근 필요 물건보다 여전히 먼저 보존한다. 같은 목표 물건 중 자동 유입과 접근 필요가 함께 있으면 후자를 먼저 남긴다. 자동 유입 자체를 금지하지 않으며 여유 슬롯이 있으면 계속 선택 가능하다.
- 자유 시간은 기존 정책대로 음식 줍기만 남긴다. 일반 물건이 먼저 두 개 있었어도 뒤의 음식이 검색되어 후보가 된다.
- 벽 너머 아이템의 별도 dig 후보는 기존 1개 제한을 유지한다. 이번 수정은 직접 줍기 후보를 먼저 두 개 채웠다는 이유로 나머지 관측을 검색하지 않던 결함을 해결한다. 전체 후보 14개 상한이나 모든 종류의 경로별 개수 제한을 재설계하지 않았다.

### 걷기 예상 시간 기준 통일

- 기존 실측 상수 5.7칸/초를 `Core/AIMoveEstimate.WalkSpeed` 한 곳에 두고, `AIBody.WalkSpeed`는 그 상수를 참조한다. 몸 제어·속도 변경이 아니다.
- `TunnelPlan.EstimatedSeconds` = 길 칸 수/5.7 + 예상 타격 수×0.5. 탐험, 호스트 쪽, 기지/상자, 제작대, 요리, 농사, 놓기 장소 및 둘레 별도 경로 설명이 같은 기준을 사용한다.
- 기존 물러나기·걷기 진행 중 ETA도 같은 WalkSpeed를 참조한다. 열린 길 14칸은 약 2.46초, 13칸은 약 2.28초이다(현재 F0 표시에서는 모두 약 2초).
- `AIBaseGuard.DetourFact`는 이제 걷기 칸 단위의 점수가 아니라 실제 표시용 초를 받는다. 호출부는 원래/별도 경로의 EstimatedSeconds를 준다.
- **경로 선택 알고리즘·탐색 점수는 보존했다.** `AITunnelSearch.HitTiles=1.2`, EstTiles, OfferDetour의 10칸 문턱, 경로 탐색 순서/계산 결과는 바꾸지 않았다. 표시용 예상 시간과 기존 경로 선택 점수의 의미를 주석으로 분리했다.
- 타격 시간은 기존 추정값을 유지한다. 문 대기·장애물/버프 등의 실제 지연을 새 수치로 발명하지 않았고, 경로의 문 사실 표시는 기존대로 유지한다.

## 검증

[전체 비Unity 관문 출력](01-gates.txt): **failed gates: 0**.

- compile-check: C#8, 게임 어셈블리 참조 빌드, 오류 0.
- core-tests: 1,079개 통과. 새 `CHOICE` 17개 포함.
- exp-sim, recall-loop 41, bridge-wiring 23, memgraph 80, protocol 106, fake-bridge, logshape, movecheck 통과.
- S6b의 기존 854개 후보 조립 재생 기대값은 수정하지 않았다.
- `git diff --check` 통과.

새 회귀는 가까운 일반 물건 뒤의 목표·음식, 자유 시간 음식, 실패/불가능 우선 후보 뒤 유효 후보, 자동 유입과 목표 우선순위, 한 물건의 두 역할, 중복 key, 빈 결과, #30의 걷기 14/13칸 비교, 타격 비용 분리, 경로 선택 점수 불변, 위험 정보의 후보/계속하기 연결을 확인한다. 순수 시험과 소스 연결 검사를 구분하며, 게임 프리팹이 실제로 IsExplosiveCD를 갖는지는 짧은 실게임의 최종 입력에서 확인할 항목이다.

기존 `PlaceGoalTests`의 DetourFact 시험 한 곳은 의도된 표시 계약 변경에 맞춰 고쳤다. 입력을 30칸+2타격, 12칸+4타격의 표시 초로 주고 “약 6초/4초”를 기대한다. 같은 파일의 경로 선택·우회/문 통과 결과 시험과 `AITunnelSearch.EstTiles(10,5)==16`/OfferDetour 시험은 원래 기대값 그대로 통과했다.

SDK 로컬 변경이나 Library 복사 없이 compile-check를 수행했다. `KG_DATA`는 기존 원본의 게임 지식 데이터 경로를 읽기 참조했다. Unity/게임 준비는 HEAD가 별도 담당에게 배정한다.

## 독립 검수·실게임에 넘길 사항

1. 테스트 월드에서 폭발성 벽이 포함된 파기 후보와 직접 캐기 후보의 **전체 INPUT**에 공통 위험 문장이 있고, 가능한 경우 실행 중 계속하기에도 남는지 확인한다. 위험이 표시됐다는 사실과 실제 생존/모델 선택의 우수성을 구분한다. 피해량을 얻으려고 의도적으로 희생시키는 시험은 필요 없다.
2. 가까운 일반 물건 두 개 뒤에 목표 Wood 또는 필요한 음식이 있는 장면에서 최종 INPUT에 해당 줍기 후보가 남는지 확인한다. 음식은 가방에 음식이 없고 foodNeed가 참이어야 한다. 목표 품목과 음식 둘 다 있으면 두 역할이 보존되는지 확인한다. 기존 `/ai give`는 가방 지급이므로 땅 드롭 장면은 게임의 드롭 조작 등 실제 상태를 만들어야 한다.
3. 열린 길 물러나기와 탐험이 함께 있을 때 길 수 대비 시간이 같은 5.7칸/초 기준인지 확인한다. 표시 반올림으로 13/14칸 모두 2초인 것은 정상이다.
4. 대표 정상 흐름 1회에서 선택→기술 실행→가방/위치 결과를 확인한다. 새 동작을 추가하지 않았으므로 기존 모루 놓기→검 제작 또는 짧은 목표 재료 줍기가 적절하다.
5. 성능: 위험 설명은 최종 dig 목록에서 이미 사용한 PlanC를 재사용한다. 직접 캐기는 기존 벽 데이터 조회 뒤 프리팹 사실을 추가로 읽는다. 큰 화면에서 비용이 문제로 보이면 프리팹 종류별 범위 캐시를 검토할 수 있으나 이번 오프라인 시험에서 실게임 비용을 측정했다고 주장하지 않는다.

## 지식그래프·타 작업 경계

기존 개발/제품 그래프는 세션의 `work/지식그래프/기획/기능.md`, `work/2026-11/2026-11-05-지식그래프-개편/B/`, `work/2026-11/2026-11-05-지식그래프-실화면/D/` 절차를 사용한다. 공식 그래프는 아직 ai-character 902721e를 가리키므로 이 격리 후보로 덮지 않았다. 병합/검증 시 후보 만들기 기능, 걷기/터널/직접 캐기 기능에 새 `AIPickupOptions.Select`, `AIMoveEstimate`, `AIWallData.RiskText`, `TunnelPlan.RiskText/EstimatedSeconds`, `AIWallRisk` 앵커를 연결하고 코드 snapshot·추출 그래프를 갱신해야 한다. 과거 실게임 서명을 새 검증으로 바꾸지 않는다.

4a 교차 파일은 `AIBrain.Candidates.cs`의 직접 캐기 설명과 최종 Assemble, `Core/AIBaseGuard.cs`의 DetourFact **표시 API**이다. CountsAsGoalProgress/AICandidateAssembler/EdgeWallFact 및 기지 공유 설계는 건드리지 않았다. 작은 수정판이 먼저 병합되면 4a 담당이 그 위에서 진행할 수 있다.
