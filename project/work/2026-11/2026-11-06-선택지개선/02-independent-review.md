# 선택지 개선 독립 검수

2026-11-06. 검수판 `choice-fixes-20261106/85f48145b68d02be434dbef9a6c76247b9b91277`, 기준 `902721e`. 저장소 `C:/Dev/CoreKeeperModSDK-choice-fixes`. 검수 시작 시 HEAD/브랜치/clean 상태를 확인했다. 작성자가 아닌 원래 사실조사 검토자가 합의 `00-agreement.md`의 세 항목을 독립 대조했다. 제품 코드 수정 없음.

## 초기 검증판 판정(85f48145)

**현재 검증판은 수정 필요 — 차단 발견 1개.** 목표/음식 줍기 보존과 걷기 ETA 통일에서 추가 차단 사항은 찾지 못했다. 폭발 위험 설명의 본 경로/대안 경로 선택이 잘못되어, 일부 주요 후보에 경고가 누락되거나 다른 경로의 위험이 붙는다. 아래 R1 수정 후 해당 차이와 회귀를 재검수하면 된다. 4a 및 전체 후보 상한은 범위 밖이다.

## R1 — 높음: digEdgeOk를 대안 경로 표시로 잘못 사용

검증판 `Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:1191~1193`:

```csharp
var plan = PlanC(server, player, c.tile, out _, c.digEdgeOk != null);
if (c.digEdgeOk != null) plan = plan?.alt;
if (plan != null) c.desc += plan.RiskText(server);
```

`digEdgeOk`는 모델에게 이미 알린 둘레 벽 목록이다. 우회 후보만의 표시가 아니다. 일반 본 경로에도 빈 목록을 포함해 non-null로 넣는다.

| 본 경로 생산자 | 근거 |
|---|---|
| 적에게 파 들어가기 | `AIBrain.Candidates.cs:224~232` — 본 plan의 edgeW를 일반 `dig:` 후보에 할당 |
| 기지로 돌아가기 | `AIBase.cs:458~462` — homeWalk의 digEdgeOk 할당 |
| goto/place 목적지 이동 | `AIPlaces.cs:544~548` — 본 walk의 digEdgeOk 할당 |
| 실제 대안 경로 | `AIPlaces.cs:111~117` — `digedge:`이고 `digDetour=true`, `digAllowEdge=true` |

`AIBrain.Loop.cs:1005~1042`의 PlanC는 detour=true에서도 **본 경로를 반환**하고 대안은 그 객체의 alt에 둔다. 따라서 현재 후처리의 결과는 다음과 같다.

- 본 경로에 폭발성 벽이 있고 alt=null: 계획을 null로 덮어 위험 경고가 **없음**.
- 본 경로에 폭발성 벽이 있고 alt는 안전: 본 경로의 경고가 **없음**.
- 본 경로는 안전하고 alt에 폭발성 벽: 본 경로에 **다른 경로의 위험**이 붙음.
- 실제 digDetour 대안: alt 조회가 맞음.

이 경로는 기지 계획이 없는 적 대상 파기에서도 발생한다. 둘레 벽이 없어 edgeW가 비어 있어도 ConvertAll 결과는 non-null이기 때문이다. 따라서 4a 문제로 미룰 수 없고 이번 위험 설명 수정의 결함이다.

**수정 요구:** 실제 대안 여부(`digDetour` 등 의미가 맞는 기존 값)와 해당 후보가 서술한 경로를 기준으로 위험을 읽을 것. 기존 본 경로/대안 선택 정책 및 캐시 계약은 유지할 것. 본/대안의 위험이 서로 다르고 alt가 없는 경우를 각각 검증해, 설명이 고른 경로와 일치한다는 증거를 남길 것.

현재 `ChoiceFactsTests.cs:58~61`은 RiskText 관련 문자열이 소스에 존재하는지 검사한다. 이 검사는 위 오연결을 잡지 못하며 CHOICE 17 통과를 본/대안 경고의 의미 검증으로 해석하면 안 된다.

## 나머지 변경의 독립 확인

### 위험 사실과 계속하기

- `AITileReader.cs:92~97`: 이름 추측이 아니라 PrimaryPrefab의 `IsExplosiveCD`와 `ignoreExploding`으로 위험 사실을 읽는다. `Core/AIWallRisk.cs:5`는 가능성/가까이 있을 때 피해 위험을 말하고 피해량·범위는 미확인으로 둔다. 앞선 실게임 사망의 원인을 확정하는 문장이 없다.
- 로컬 `C:/Dev/_ref/corekeeper-decomp/Pug.Other/ExplosiveSystem.cs:232~294`를 직접 읽었다. ignoreExploding 등 조건을 거쳐 CreateExplosion이 호출된다. `Pug.Objects/WallExplosiveBlock.cs`는 체력 감소 시 폭발 관련 점멸/카운트다운 연출을 하며 `WallNaturalExplosiveBlock`이 상속한다. 따라서 위험 가능성 표현의 방향은 자료와 맞는다.
- 직접 캐기 후보 `AIBrain.Candidates.cs:346~348`, 직접 캐기 진행 `AIMineSkill.cs:206~209`, 파기 진행 `AITunnel.cs:575`에 위험 문장이 연결된다. 캐기/파기의 실제 입력과 자동 회피 정책은 변경되지 않는다. 위험 후보를 자동 금지하지도 않는다.
- 단, 실행 중 게임 프리팹에 실제로 해당 component가 있으며 variation 0 조회로 대상 벽을 잡는지는 오프라인 코드만으로 확정하지 않았다. 최종 전체 INPUT에서 자연 폭발 벽/평범한 벽 대비 확인이 필요하다. `MineSkill.Progress`의 벽 파괴 후 줍기 단계는 기존처럼 완료 문구만 반환한다(`:205`). 모든 폭발 이벤트/잔여 위험을 추적하는 구현이라고 평가하지 않는다.

### 필요한 줍기 후보

- `AIBrain.Candidates.cs:508`은 모든 관측 아이템을 읽는다. 가방 여유 검사(:514), 도달/자동 줍기 거리 분기(:519), 현재 실행 key 제외(:522)는 남는다. 최종 두 개를 고를 때 `Doable`을 먼저 확인한다(:546).
- `Core/AIPickupOptions.cs`: 목표 하나와 필요한 음식 하나를 먼저 확보하고 같은 key는 중복하지 않는다. 같은 후보가 목표+음식이면 두 자리를 낭비하지 않는다. 자유 시간에는 음식만 남는다. 자동 유입은 같은 필요 범주 안에서 뒤로 가며, 목표 자동 유입은 일반 물건보다 앞선다. 모델이 고르는 권한은 그대로다.
- 실패/몸 조건을 먼저 필터하므로 실패 후보가 보존 자리를 차지하지 않는다. 첫 실패 설명과 최종 assembler의 실패 처리는 유지된다. 반복 실패로 일찍 빠진 직접 줍기가 최종 Mark의 skipped 문구에는 잡히지 않는 점은 있으나, 뒤의 유효 후보를 숨기지 않는 목적과 충돌하는 차단 결함으로 보지 않았다.
- 벽 너머 아이템의 별도 dig 1개 제한은 그대로다. 전체 관측을 읽으므로 이전에 가까운 두 직접 줍기 뒤에서 멈추던 dig 검사까지 더 진행될 수 있다. 임의 자동 실행이나 탐색 알고리즘 변경은 없다. 다만 밀집된 드롭에서 추가 경로 조회 비용은 실게임 미측정이다.

### 걷기 ETA와 경로 정책

- `Core/AIMoveEstimate.cs`의 5.7칸/s를 `AIBody.WalkSpeed`가 참조하고, 기존 값 자체는 같다. 탐험·기지·상자·제작대·요리·농사·목적지·우회 문구의 `/2.4f`를 제거했다. 기존 걷기 continue·retreat도 같은 상수 경로다.
- 실제 #30의 순수 걷기 14칸/13칸은 2.46초/2.28초가 된다. 반올림 F0에서는 모두 2초이며 정상이다. BFS 길과 continue 직선 잔여 거리가 다를 수 있는 기존 차이는 남는다.
- 채굴은 기존 0.5초/타격 항을 따로 유지한다. 문 대기나 지형 버프 시간을 새로 발명하지 않았다. 정확한 실측 ETA 전체 개선이 아니라 열린 길 속도 기준 통일이다.
- `AITunnelSearch.cs` diff는 주석만이며 HitTiles=1.2, EstTiles 및 우회 제공 문턱은 그대로다. `AIBaseGuard.DetourFact`는 표시 초를 받도록 바뀌고 호출부도 EstimatedSeconds를 준다. 기존 경로 정책을 표시 변화에 따라 몰래 바꾸지 않았다.
- PlaceGoalTests의 시간 기대값 변경은 의도된 표시 계약 변경과 맞고 경로 선택 기대값을 바꾸지 않았다.

## 수행 검증과 경계

직접 실행: `dotnet run --no-restore -c Release --project Tools/core-tests -- CHOICE` → **17 checks 통과**. 픽업 선택/ETA의 순수 결과와 생산 연결 문자열 검사를 각각 구분해 읽었다. R1은 이 통과에도 존재하는 정적 의미 연결 결함이다. 추가 전체 시험·컴파일 반복은 하지 않았다.

작성자 `01-gates.txt`를 읽어 compile-check 오류0, core1079, 전체 비Unity gates 실패0 기록을 대조했다. 이는 작성자 실행 결과이며 독립 재실행이라고 쓰지 않는다. 실게임/유료 판단/설치/프로세스 변경 없음.

검수 중 Unity 담당이 SDK 설정7개·게임 참조를 복사한다는 HEAD 통지를 받았고 끝 status에 해당 수정/Assets/Plugins가 나타났다. 검수 대상 제품 HEAD는 여전히 85f48145이며, 이 운영 환경 변경을 작성자 코드 diff로 혼동하지 않았다.

재검수 조건: R1 수정 커밋과 본/대안 위험 의미 회귀 확인. 최종 INPUT의 게임 프리팹 연결 확인은 별도 실게임 검증 담당에게 남긴다.

## 최종 재검수 — b25099f6

**최종 판단: R1 해소. 세 합의 항목의 코드·오프라인 검수에서 남은 차단 사항 없음.** 이 판단은 실게임 프리팹 연결·최종 INPUT 확인까지 통과했다는 뜻은 아니다. 그 검증은 별도 담당 범위로 남는다.

재검수 HEAD `b25099f6fd31e4c4c3618357eb9e8101e0e17124`와 SDK 환경준비 미커밋 7개/게임 참조 상태를 확인했다. 직전 검증판 대비 제품 변경은 `AIBrain.Candidates.cs`, `Core/AIWallRisk.cs`, `ChoiceFactsTests.cs` 세 파일이다.

- 생산 경계 `AIBrain.Candidates.cs:1191~1192`가 `PlanC(..., c.digDetour)`와 `AIWallRisk.RouteText(plan,c.digDetour,...)`를 사용한다. 기존 본 경로의 digEdgeOk 목록이 있어도 본 plan을 읽는다. 진짜 digedge 후보만 alt를 읽는다.
- `Core/AIWallRisk.cs:12~16`은 선택한 경로만 설명하고, primary/선택한 alt가 없으면 다른 경로의 위험을 빌려 오지 않는다. 이미 detour 검사를 한 캐시는 일반 조회도 본 plan을 반환한다는 PlanC 계약과 맞는다.
- 새로운 순수 회귀는 alt 없는 위험 본 경로, 위험 본 경로+안전 alt, 안전 본 경로+위험 alt, 선택 경로 없음의 결과를 실제 RouteText 호출로 확인한다. 단순 문자열 존재 검사와 달리 R1의 의미 오류를 직접 검증한다. 생산 flag 연결은 별도 소스 검사이며, 검수자가 실제 생산 호출/기존 후보 생성의 digDetour 할당을 대조했다.
- 직접 최소 재실행 `dotnet run --no-restore -c Release --project Tools/core-tests -- CHOICE` → **22 checks 통과**. 앞서 통과한 줍기·ETA 사례도 포함된다. 작성자의 compile-check 오류0은 보고 결과이며 이 재검수에서 독립 재실행하지 않았다.

재검수 중 제품/설정 수정·게임 조작·유료 모델 호출 없음. 추가 전체 시험을 요구할 새 결함은 찾지 못했다. 이후 실게임에서는 일반 본 경로와 가능한 대안 경로의 전체 INPUT이 각자의 위험을 표시하는지, 대상 벽의 런타임 prefab 사실이 실제 조회되는지를 확인하면 된다.
