# 제약 해제 독립 검수

**제품 소스 5cc5a43ff8df91634c04b44bac9f15f849bdb611을 지정된 감사 범위의 정적·대표 오프라인 검수로 수용한다. 남는 blocker는 확인하지 않았다.** 미구현이라는 이유로 지정된 손/무기·적별 제어를 제외하지 않았으며 실제 생산 경로의 확장을 확인했다. 이는 실게임 성공이나 모든 게임 기능의 구현 완료 판정이 아니다.

## 기준과 범위

- 출발 소스: 5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef.
- 검수 작업트리: C:/Dev/CoreKeeperModSDK-unrestricted, ai-remove-restrictions-20261107.
- 최종 제품 소스/최종 검증 HEAD: 5cc5a43ff8df91634c04b44bac9f15f849bdb611.
- 앞선 전체 독립 재실행 HEAD는 6637e8e7027b3e4c9d73ab683706fd538c4b8212이며 그 시점의 제품 소스는 84ba9f22fafa87b930b4e4c23901f8cd064408a5와 같았다. 마지막 후속은 아래 고정 목록의 생성 gate와 진행 표시를 바꿨다. 그 diff와 새 생산 경로 검사 47개, Core·전체 컴파일을 최종 SHA에서 직접 확인했다.
- 실행 기준은 [00-agreement.md](00-agreement.md), [01-acceptance.md](01-acceptance.md), 감사 상세 [01](../2026-11-07-제약감사/01-choices.md)·[02](../2026-11-07-제약감사/02-plans-info.md)다. [coverage.md](coverage.md)의 항목별 판정도 대조했다.

상위 입력·폼·계획 큐·열린 지시, 현장 후보 생성/조립/생활 트리/최종 선택면, AIActions의 생성·stale, 실제 손 선택·스킬·정상 입력/요청·결과 관측, 경로 검색, 보고·채팅·기억 검색을 추적했다. 수정한 제품 경로와 관련 실행기를 읽고 대표 오프라인 어댑터를 직접 재실행했다. 검수자는 제품 코드를 직접 수정하지 않았다.

## 발견하여 수정된 중요 항목

|발견|수정 확인과 판정|
|---|---|
|KnownItems를 곧바로 채집 가능성으로 써서 CopperBar 같은 중간재의 하위 제작 분기가 막힘|Prep에서 지식 허용과 알려진 제작 레시피를 구분한다. 기존 하위 제작 분기가 다시 도달 가능함을 소스로 확인했다. 지식 서비스 자체를 대체한 fixture는 실제 지식 공개의 전수 실행 근거로 세지 않는다.|
|shape 상한만 int.MaxValue로 바꾸어 전체 List 할당·정수 끝 좌표 반복이 무한 실행/OOM 가능|lazy ShapeTileView, long 좌표 산술, 표현할 수 없는 총량 거절, 128칸 평가 묶음으로 바뀌었다. 100M칸 뷰의 끝 좌표와 int.MaxValue 한 칸, 좌표 wrap 방지를 직접 검증했다.|
|Placed 기록 64개 삭제가 장기 배치 총량 계산에도 사용됨|기록 축출을 제거했다. 200칸이 128+72 평가 묶음으로 유지되며 전부 확인되기 전 전체 완료가 되지 않는다. 기존/건너뛴 첫 묶음도 뒤 묶음으로 진행한다.|
|큰 제작 목표를 단일 커서 요청으로 보내 9999에서 전체 거절·ceil 산술 넘침|총 요청과 정상 커서 묶음을 분리했다. 실제 AICraft/CraftSkill에서 20000 요청이 9998→9998→4의 확인 수령으로 이어지며 원래 목표/잔량이 유지된다.|
|생활 메뉴를 전투에 열자 기존 공격/회피 후보가 사라짐|Compose가 실제 후보를 함께 보존한다. 생활 화면의 독점 탐색 소유권과 지원 행동의 금지는 구별한다.|
|생활 후보가 전투에도 표시되지만 AILife.Stale가 전투라는 이유만으로 전부 거절|84ba9f2에서 전투 blanket stale를 제거했다. maintenance115는 실제 Apply·정상 수리 요청·확인 수령까지 실행한다. 목표 revision·요청 잔량·정확 대상·시설/슬롯 stale는 유지한다.|
|새 secondary 후보가 Make에서 기본 공격으로 바뀜|AIActions가 actionMode를 실제 AttackSkill에 전달한다. 실제 후보→생성식→스킬→정상 입력을 실행하는 검사가 누락 시 실패한다.|
|소환 최대 충전을 개별 소환으로 설명했으나 게임은 같은 종류 해산/전체 교체로 처리|로컬 게임 원문의 SummoningWeaponSlot과 MinionSummonAndDismissSystem을 대조했다. 개별 미최대 해제와 최대 충전 해산/교체를 구분했다. 해제 시점을 놓친 경우와 시야 이탈도 개별 소환 성공으로 보고하지 않는다.|
|일반 이동 후보를 모두 제공하자 remembered frontier 생성 조건이 영구 닫힘|근거리 후보 유무에 의한 생성 gate를 제거했다. 방향별 대표·120칸·시작/목표 ±40 및 260칸 박스를 제거했다.|
|검색 예산 소진을 도구 부족/길 없음으로 확정, frontier 40000에서 부분 검색을 완료로 처리|AStar/KnownPath와 터널 검색의 미확정 상태를 구별한다. Frontier는 400/frame만 유지하고 heap이 남으면 다음 프레임에 계속한다.|
|반경 제거를 true/0으로 치환해 explicit base에서 100칸 떨어진 현장에 배치하거나 원격 상자로 목표 완료|place는 명시 anchor 도착 후 실행한다. Inventory는 생략한 목적지와 explicit base를 구분하며 후자는 도착 후 현재 관측·조작 가능한 자기 상자만 선택/진척으로 인정한다.|
|Inventory가 anchor 중심 0.7칸만 인정해 막힌 코어 주변 BaseStand에 도착한 뒤 Home을 무한 반복|Home과 Inventory가 같은 AIBase.ArrivedAtBase를 사용한다. 실제 공유 predicate 원문을 추출해 stand 3칸 도착/100칸 밖 거절을 검증했고 Inventory의 inspect→정상 transfer→확인 receipt도 별도로 실행했다.|
|사건 접두어 병합·정보/목차/보고 수량 및 문자열의 잘림|서로 다른 사건 본문, 전체 장소/상자/제작/가방·대화·경험·전투 및 기억 검색 결과를 보존한다. 지식 공개와 캐릭터 격리는 유지한다.|

## 마지막 일괄 gate 점검 목록

감사 C02/03/04/05/15의 생성 입구→조립→stale를 모아서 점검한 다음 고정 목록은 최종 5cc5a43에서 모두 해제됐고, 생산 원문 추출과 실제 Core 조립을 실행하는 47개 검사로 확인했다.

1. Food는 None/전투 여부와 무관하게 실제 AIFood.Candidates를 호출한다. 허기 90에서도 먹기 후보가 조립까지 보존된다.
2. 승인된 활성 Edge/Guard는 None에서도 생성한다. 새 기지 계획을 자동으로 정하지 않는다.
3. 보이는 플레이어 Follow의 목표 종류 gate를 제거했다. 원래 Follow 목표와 맞을 때만 진척으로 표시한다.
4. 캐릭터가 기억한 광석 전체를 조회한다. wantedFirst 밖 광석도 선택할 수 있고 실제 wanted 일치만 진척이다.
5. 목표 수확물과 일치하는 야생 미성숙 식물도 선택 가능하다. 지금 수확물이 없다는 설명과 진척 false를 유지하며 자기 성장 밭·타인 작물 보호는 보존한다.
6. 가방에 음식이 있어도 보급 후보를 만들고 첫 best 하나에서 종료하지 않는다. 별도 가방 메뉴의 정상 꺼내기 실행/소유권·정확 슬롯 검사는 유지한다.

이미 해제된 것으로 확인한 경로는 None/Free/combat 조립의 최종 합집합, AILife.Compose/Stale, 보이는 Walls/Roots/Items다. Help의 None 조건은 일반 Enemies 경로에서 같은 attack 대상이 이미 제공되어 별도 몸 동작 손실로 세지 않는다. UI 탐색 소유권, 같은 실행 key의 중복 방지, 실제 몸/슬롯/재료/소유권 조건은 이 묶음에서 제거할 blanket gate가 아니다. Free에 새로운 장기 목표를 자동으로 만드는 정책도 추가하지 않는다.

## 직접 재실행한 근거

아래는 검수자가 직접 실행한 결과다. 전체 검사는 제품 84와 같은 검증 HEAD663에서, 마지막 생성 gate·Core·전체 컴파일은 최종 제품 5cc에서 재실행했다. 마지막 후속이 바꾸지 않은 실행기/정보 경로는 앞선 실행 증거를 유지하며 최종 변경 입구는 새 47개 검증으로 보강했다. 생성 스냅샷을 사용하는 검사는 생산 소스에서 다시 추출했다.

|검사|독립 결과|실행 입구|
|---|---|---|
|전체 게임 DLL 참조 컴파일|0 경고·0 오류|dotnet build Tools/compile-check -c Release --no-restore|
|Core|1396 PASS|dotnet run --project Tools/core-tests -c Release|
|프로토콜|188 PASS|node Tools/ai-bridge/test/protocol.test.mjs|
|배치/경로/공유 도착·최종 생성 gate|47 PASS|최종 5cc에서 node Tools/restriction-tests/prepare.mjs 후 dotnet run --project Tools/restriction-tests -c Release|
|생활 생산 경로|98 PASS|Tools/life-tests/run.ps1|
|정비 생산/생활 통합|115 PASS|Tools/maintenance-tests/run.ps1|
|가방 생산 경로|152 PASS|ExtractBase/ExtractLifeBinding 후 Tools/life-inventory-tests|
|큰 제작 생산 경로|29 PASS|ExtractLifeBinding 후 Tools/life-craft-tests|
|신규 손/전투 입력|43 PASS|node Tools/action-input-tests/run.mjs|
|요리 생산 경로|51 PASS|dotnet run --project Tools/life-cook-tests -c Release|
|공통 실행|55 PASS|Tools/execution-tests/run.ps1|
|기억 검색|84 PASS|memgraph.test.mjs --kg C:/Dev/CoreKeeperModSDK/Tools/knowledge/data|
|검색 호출/시간 보호|41 PASS|node Tools/ai-bridge/test/recall-loop.test.mjs|
|정보 보존|7 PASS|dotnet run --project Tools/info-tests -c Release|
|경험/100시간 합성|ALL CHECKS OK|dotnet run --project Tools/exp-sim -c Release|

life99→98은 전투 응답이 즉시 farm 분기로 들어가므로 불필요해진 추가 Nav 호출 하나와 그 helper 내부 검사 하나가 줄어든 결과다. 핵심 기대를 삭제한 것이 아니다. 정비의 중간 100 PASS/4 FAIL은 옛 전투 화면 중단 기대와 실제 잔여 stale gate를 함께 드러냈고, 테스트를 skip하지 않고 새 계약과 실제 실행을 검증하도록 고쳤다.

대표 검증은 순수 상수 검사에 그치지 않는다. 신규 입력 검사는 생산 AICombatOptions·AIActions 생성식·StepSkill/AttackSkill을, 가방 검사는 생산 Candidates·InventorySkill·정상 Swap·양 슬롯/실제 손 관측을, 제작은 생산 요청·커서 잔류·main 수령·잔량을 실행한다. 배치는 실제 ShapeList/Remaining/GoalDone/PlaceTilesSkill을 추출하지만 개별 PlaceSkill과 게임 타일 소비자는 fixture다. 모든 검사의 경계를 동일한 실게임 성공으로 합치지 않는다.

Unity는 담당자가 실행한 최종 unity-producers-final.log:1874의 HeadlessSdk CompileCheck: ok와 unity-producers-final-exit.txt의 0을 독립적으로 읽었다. 최종 DLL은 2026-11-07 17:27:20, 1580544바이트다. 최종 후보 생성 소스 blob은 a23b2e95d15b2f72a2c9948dbee64904ea952c20, AIBase blob은 6eab05db3cc654c71c999673b7ecdc6aa81bed4d로 확인했다. 앞선 unity-combat-final은 제품84의 별도 gate이며 최종 결과 대신 사용하지 않았다. 검수자가 Unity를 새로 실행한 것은 아니다. 자세한 재현 기록은 [validation.md](validation.md), [actions.md](actions.md), [craft.md](craft.md), [info.md](info.md)에 있다.

## 수용 경계

최종 고정 목록에 남는 미해제 blocker는 확인하지 않았다. 이 판정은 임의 목표량·계획/지시 수·후보/정보 잘림과 감사에서 지정한 지원 행동 경로에 대한 것이다. 실제 게임의 모든 행동을 새로 지원한다는 판정이 아니다. 등록 do/mode, 실제 장비/레시피·재료·슬롯·충돌, 소유권, 지식 공개, 사용자 비용·구독·동시 요청·중복 실행 보호는 유지한다.

실게임·설치·새 모델 호출은 수행하지 않았다. 실제 투사체 명중·소환수 생성/해산·마나 소비·네트워크 왕복·모든 물품/모드 조합은 미확인이다. 오프라인 fixture의 결과 주입을 그 성공의 증거로 세지 않는다. 확대한 전체 후보/보고의 실게임 대규모 프레임 성능과 외부 모델의 비공개 context 한계도 이번 검수로 보장하지 않는다. 새 원정/갈림길 제품 설계 전체의 완성이나 공식 통합·배포 승인으로 확대하지 않는다.

## 최종 고정란

- 최종 제품 소스 SHA: 5cc5a43ff8df91634c04b44bac9f15f849bdb611.
- 최종 47/Core/전체 컴파일 검증 HEAD: 위 제품 소스와 동일.
- 앞선 전체 독립 검증: HEAD663 / 제품84. 차이와 후속 보강 범위를 위에 명시했다.
- 남는 blocker: 없음.
- 최종 정적/대표 오프라인 수용: 통과.
- 실게임·설치·새 모델 호출: 미수행. 이 상태를 그대로 보존한다.
