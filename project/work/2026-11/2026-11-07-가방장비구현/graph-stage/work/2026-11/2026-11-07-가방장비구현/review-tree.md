# 가방·장비 선택 트리 독립 검수

2026-11-07. 최신 고정 검수판은 `741532fcc57ffc549ac155a8261f5e25c966902f`다. 최초 checkpoint는 `b1facaaef2afd66af321db66fc4b5d00725af5ce`다. 기준은 실행 부품판 `1fc89b9`이며, 지배 합의는 `tree-agreement.md`, `tree-spec.md`, `inventory-category-cap-audit.md`8절 HEAD 판정이다. 이전 평면판의 수용·Unity 근거를 새 트리의 완료 증거로 대신하지 않았다.

**현재 판정: 741532f의 소스·오프라인 선택/실행 흐름 독립 검수 통과.** b1facaa에서 발견한 활성 착용 프리셋 변경 P2는 후속 고정판에서 해소됐다. 연결 trace도 최신 고정판에서 독립 재실행해 통과했다. 741 Unity 컴파일 및 설치 없는 SDK source 후보 산출물도 완료 로그·exit·입력/출력 해시를 독립 읽기 대조해 확인했다. 이 판정을 실게임 QA나 공식 최종 반영 승인으로 확대하지 않는다.

## 독립 실행과 범위

생산자 상태를 읽은 뒤 존재하지 않던 `C:/Dev/CoreKeeperModSDK-bag-review-tree`에 별도 detached worktree를 만들었다. 생산 소스·미커밋 환경 변경을 수정하지 않았다. 검수판 제품 소스도 바꾸지 않고 `review-repro/`에만 재현을 작성했다. HEAD start-job·AGENTS 지침과 초기 설계 검토를 이어 적용했다.

| 검증 | 독립 결과 | 근거(`C:/Dev/CoreKeeperModSDK-bag-review-tree/` 기준) |
|---|---|---|
| 최신 fast compile-check | 경고0/오류0 | `review-repro/compile-741.log` |
| Core | 1204 통과 | `review-repro/core.log` |
| bridge protocol | 118 통과 | `review-repro/protocol.log` |
| 실제 Loop Apply/Surface/JSON/공통 입구 | 55/55 통과 | `review-repro/execution.log` |
| 실제 Menu+Inventory 소스 링크 기존 사례 | 326/0 | `review-repro/menu-base.log` |
| 독립 stale 경계 추가 | **최신394/0** (b1:384중1실패 보존) | `review-repro/menu-741.log`, `menu-extended.log`, `review-repro/menu/IndependentCases.cs` |
| 실제 메뉴→Surface/JSON→Apply/등록→InventorySkill 연결 | **102/102** | `review-repro/connected.log`, `review-repro/connected/connected-trace.json` |
| 기존 개별 물품·이동·수령·최대 스택 경계 | 67/0 | `review-repro/primitive.log`, `review-repro/primitive/` |

Core/protocol/Loop55/primitive67은 b1 checkpoint에서 독립 실행한 근거다. b1→741 제품 diff가 AIInventoryMenu.cs의 위치 역할 검사 하나에 한정되고 해당 Core/protocol/Loop 추출·primitive 입력은 바뀌지 않았음을 확인했다. 최신 메뉴394/연결102/fast compile은741에서 직접 실행했다.

메뉴 시험은 구현 산출물 `tree-menu-adapter`의 최소 World/시험을 복사하고 csproj Link를 검수 고정판으로 바꾼 뒤 독립 실행했다. `AIInventoryMenu.cs`, `AIInventory.cs`, `Core/AIInventoryRules.cs`는 실제 소스다. 추가 시험도 같은 실제 소스를 사용한다. 상태 전후는 대역 버퍼에 수동 공급한다. Loop 시험은 실제 Apply/Surface/BuildJson 등 추출과 실제 AIGoals/AISkill/공통 규칙을 사용하되 메뉴·몸·월드 경계는 대역이다. 이 두 시험의 개별 통과를 연결 흐름 통과로 합산하지 않았다. 이후 실제 메뉴까지 연결한 별도102검사를 최신판에서 실행했다.

67개 실행 부품 시험에서는 `AIInventoryMenu.Reset`만 대역으로 두고 실제 Inventory/HandLease/Core 규칙 및 실제 Base 메서드 추출을 사용했다. 이는 이전 실행 부품 회귀 보존 확인이며 메뉴 선택 전이의 추가 증거가 아니다. 수동 Swap/수량/바닥 소비 상태 공급은 실제 게임 consumer 성공이 아니다.

## P2 — 활성 착용 프리셋 변경: b1facaa 발견, 741532f 해소

발견 당시 b1 위치: `AIInventoryMenu.cs`의 `Items`, `ValidateFrame`, `Recover` 및 물품 선택 frame. b1의 `ValidateFrame`은 선택 슬롯 물품의 EqualsExact를 검사하지만 그 슬롯이 현재 활성 착용 위치인지를 보존·재검증하지 않았다.

재현:

1. 현재 head40에 CopperHelm 상태200을 두고 인벤토리→착용 장비→물품40→조작 노드로 간다.
2. head40 버퍼는 그대로 두고 활성 프리셋을 head60(다른 CopperHelm)인 프리셋으로 바꾼다.
3. 이전 `operation:unequip` 토큰의 Stale은 null이다. 메뉴는 이전 slot40 경로를 계속 보여주고 자식은 back/cancel뿐이다. 변경 사유와 현재 물품 단계 복구가 없다.

```text
PRESET MENU BOUNDARY: old gear slot40 unchanged, active head60; stale=null, prompt=가방 선택 메뉴: 인벤토리 → 착용 장비 → 슬롯 40 CopperHelm. ... actions=back,cancel
FAIL independent: active preset change invalidates old worn-item selection and recovers to current owned worn items
```

이 재현은 잘못된 물품을 실제 교환한 결과가 아니다. `MenuMoves`의 뒤쪽 실행 가능성 검사로 해당 실행은 막힌다. 문제는 명세가 요구한 프리셋 변경 사실·토큰 무효화·유효 부모 단계 복구가 빠진 것이다. owner와 HEAD에 즉시 전달했다.

수정판은 선택 당시 sourceRole(주 가방/핫바/현재 gear name/관측 상자)을 저장하고 현재 역할과 대조한다. 동일한 실패 fixture를741532f에서 재실행하면 `현재 착용 head → 현재 주 가방/착용 범위 밖` 사유로 stale이고 items 부모로 돌아가 item60/back/cancel이 나온다. 독립 추가 정상 경계로 가방12의 검을 선택한 상태에서 관계없는 head 프리셋을 바꿔도 선택이 유효한 것을 확인했다. blanket 메뉴 취소 대신 선택 위치 역할의 실제 변경만 다루는 좁은 수정이다.

## 소스와 독립 실행에서 확인한 경계

- 보유 분류는 실제 main/hotbar/active GearSlots에서 만든다. goal item을 목록 필터로 쓰지 않는다. 같은 ID의 다른 슬롯/변형/aux/상태, amount0 파손 장비, info 없는 기타 항목을 유지한다. 빈 분류나 DB 전수 catalogue를 노출하지 않는다.
- 현재 노드 자식과 뒤로/전체 취소만 메뉴로 생성한다. 확인된 최종 leaf만 InventorySkill을 만든다. 메뉴 함수는 자체적으로 body/goal을 바꾸거나 요청 큐를 쓰지 않는다. categories/items/operations/destinations/confirm/quantity/number/chests/access/recipients 단계의 취소와 뒤로/다른 물품 전이는 소스 링크 시험에서 확인했다. 접근 실행 중 await의 메뉴 취소가 선택된 inspect 몸 동작을 멈추지 않는 것도 연결 trace에서 별도로 통과했다. inspect는 별도 명시 실행 leaf이고, 그 관측 뒤 실제 자기 상자 내용만 선택한다.
- 실제 Loop의 navigation 분기는 ApplyOrHold의 몸 전환 대기보다 먼저 처리하고 `CancelPendingChoices`로 이전 일반 in-flight/queued/held/next-hit 권한을 폐기한다. 기존 몸·목표·원래 부탁은 유지한다. 선택 메뉴가 활성화된 동안 추가 prefetch를 만들지 않는다. 실제 Loop55는 이 권한 폐기와 기존 몸의 다음 입력·목표 보존, 기존 rate/monthly budget 거절 경계를 확인했다.
- `InventoryChoiceSurface`는 assembler 뒤에 기본 인벤토리 진입을 후첨하므로 GoalNone/closeFight의 의미 필터로 무기 선택 진입이 사라지지 않는다. 활성 메뉴에서는 기존 continue/stop_action/finish_goal/request_plan/eat 및 실제 위협 관련 전투/접근 경로를 보존하고 비관련 일반 후보를 제거한 뒤 메뉴 자식을 붙인다. 옛 flat inventory/store/take는 공개 surface에서 제거한다. 내부 준비·실행 skill 등록 자체는 보존한다.
- MenuGoal은 조작 종류·물품·출발 슬롯·목적 슬롯/상자/상대 조건이 실제 현재 목표와 일치할 때만 receipt를 연결한다. 다른 물품의 유효 준비·drop이 기존 갑옷 목표를 완료하지 않는 실제 소스 링크 사례를 확인했다.
- 목적 슬롯을 first-fit 하나로 고정하지 않고 모든 유효 슬롯을 열거한다. 25개 반지+현재 착용 장비, 36개 상자 목적 슬롯과 8/14 초과 JSON 전송을 확인했다. 수량1/요청 잔량/가능 전량 외에도 숫자 자리·지우기·확정으로 유효 정수에 도달한다. 입력·확정 단계에서는 요청하지 않는다.
- 독립 추가 시험에서 목적 슬롯이 수량 입력 중 바뀌면 이전 digit 토큰은 stale이고 현재 물품 선택으로 복구됐다. 최종 leaf 직전 출발 수량 변경도 자동 clamp/실행 없이 차단됐다. 활성 프리셋 경계도 위 P2 수정 후 통과했다.

## 후보 상한 제거와 보존한 경계

`1fc89b9 → b1facaa` 실제 diff와 호출부를 대조했다. Inventory8/12/20, 최종 assembler14 및 combatMax 수치 cut, closeFight goalSide ONE 할당량, 이미 관측한 적/벽/광석/물체/뿌리/기억광석/줍기/따르기/탐색 후보·상자/음식/야생씨앗/횃불 자리·PlaceGrid consequence 대표의 수치 slice가 제거됐다. 정렬·동일 action key/광맥 집단/행동 consequence 대표화·자유시간/몸 가능성 같은 의미 조건은 숫자 cut과 구별한다.

`Core/AIBaseEdge.cs`의 상위 둘레 설계 제안4, `AIPlaces` 보고 요약량, 센서 시야, 경로 solver guard, 물리 슬롯/최대 스택, 행동 묶음 크기, 기억·로그 보관량·기존 호출/비용 상한을 제거한 diff는 없다. 실제 호출에서 `AIPlaceGrid.Pick`은 현장 배치 결과용이고 별도 상위 CoreEdge.Options4 소비자와 구별했다. 분류·물품·목적지 목록에는 페이지나 임의 countcut이 없다.

## 모델 입력과 남은 증거

독립 실행에서 출력된 메뉴 단계별 원문은 `review-repro/menu/payloads.json`에 보존된다. 실제 Loop55의 `MODEL ROOT PAYLOAD`, `MODEL NODE PAYLOAD`는 `execution.log`에 있다. 앞 자료는 실제 메뉴 출력+대역 World, 뒤 자료는 실제 Surface/BuildJson+대역 메뉴 경계다. 외부 모델 응답이나 실게임 trace로 표시하지 않는다. 최신 연결 trace는 아래 범위로 별도 확인했다.

검수자는 Unity·SDK export·설치·게임·기존 프로세스 종료·외부 모델 호출·공식 그래프 쓰기를 수행하지 않았다. producer의 b1 Unity/export는 임시 Editor helper의 직접 SDK 타입 참조 때문에 실패했다는 통지를 받았다. 이를 제품 성공 또는 제품 source 실패로 바꾸어 기록하지 않았다. helper만 reflection으로 고친 뒤741에서 재시도했고 아래 완료 증거를 확인했다. 과거774 성공을 승계한 것이 아니다. 실게임 대표 사용자 흐름은 계속 미관측이다.



## 연결 trace의 독립 확인

구현 산출물 tree-connected의 .cs/.csproj/run.ps1을 별도 review-repro/connected로 복사했다. source Link와 메서드 추출 root를 모두 독립 검수 worktree741532f로 고정하고 재생성·실행했다. 실제 Menu/Inventory, 실제 public Surface·BuildJson·Loop.Apply·inventory action registry/Make, 실제 HostOrder 규칙을 연결한다. World/큐/물리 dispatcher·일부 planner/goal 경계는 대역이다. 입력 상황 문자열과 일반 후보 목록은 fixture가 구성하고, 모델 응답은 반환된 실제 value를 고르는 로컬 선택으로 대신한다. production BuildCandidates 전체 또는 외부 모델을 실행한 trace는 아니다.

대표 8선택은 enter→category→item12→back→item13→hold→hotbar5→execute다. 앞7선택은 큐0/기존 몸·목표·원래 부탁 유지, 마지막 선택은 실제 등록/팩터리로 InventorySkill 하나 시작, 첫 Tick에 선택한13↔5 Swap 요청 하나, 버퍼 미변경 때 재전송/성공 없음, 수동 양 슬롯 변경과 실제 EquippedObject 관측 공급 후 Done/receipt를 확인했다. 목표와 실제 HostOrder record는 계속 열려 있다. 일반 취소와 inspect-await 실행 중 메뉴 취소도 같은 연결에서 몸/goal/order/큐 보존을 확인했다.

독립 결과는102/102, 로컬 Decisions 요청 구성19회/메뉴 navigation17회, 외부 모델0/실제 게임 consumer0이다. 이19회는 대표 행위 경로들의 fixture 합계이지 한 번의 물품 조작에 필요한 모델 호출 수라는 뜻이 아니다. source pin은 review-repro/741532f-input-pin.json의101개 C# 해시다. 각 requestJson과 선택 value·실행/큐/목표·부탁 상태는 connected-trace.json에 보존했다. 이 로컬 구성/가정과 실제 모델 요청 문구를 구별해 사용자 설명에 쓸 수 있다.




## 741 Unity·SDK 후보 산출물 읽기 대조 완료

검수자가 Unity/export를 실행한 것이 아니다. tree-final-unity-exit.json은 source741532f/exitCode0/종료UTC2026-11-07 05:10:14.8626270을 기록한다. tree-final-unity-export.log에서 CompileCheck:ok, Successfully built mod, Export complete,no install을 확인했고 CS컴파일오류 패턴은0이었다. tree-final-unity-input.json은101개 제품 C#·SDK입력220·설정5·임시helper해시를 고정한다. 검수 checkout과92개 C# rawhash가 같고9개는CRLF/LF만 달랐다(BOM을 보존한 UTF-8 byte decoding 후 줄끝만 정규화하여 차이0). producer의101개 rawhash와후보source101개 rawhash는 모두입력pin에일치한다. 독립상세결과는 review-repro/export-source-verification.json이다. 다른내용불일치를줄끝차이로뭉개지않았다.

SDK 후보는 tree-741532f-candidate/AIPlayerSpike의 C#101개와ModManifest.json,총102파일 source패키지다. manifest files101과실제101개source누락0/입력해시불일치0을확인했다. Unity compiler의Library/ScriptAssemblies/AIPlayerSpike.dll은설치payload와구별한다. Editorhelper도입력manifest해시와일치하며격리산출폴더만사용하는임시helper다. buildBundles=false·buildLinux=false인이번source후보의성공을모든플랫폼/assetbundle빌드성공으로확대하지않는다.

AIPlayerSpike-tree-741532f.zip은675666바이트, SHA256 5FE44B67281E10778ACFE95639F2D40611C8E6B0E62D675E341D7503903FC512로tree-package.json과독립일치했다. ZIP을풀거나설치하지않고102개payload파일stream을해시대조해tree-artifact-manifest.json과불일치0을확인했다. ownermanifest의source전후101/설정5/기존설치96파일unchanged표시는모두true이며,이는owner가수집한전후관측이다. 검수자가과거설치상태를독립재현한것으로표시하지않는다.

최종소스·오프라인선택/실행흐름·Unity컴파일·설치없는SDKsource후보범위에서미해소blocking발견은없다. 실제모델선택·서버/게임consumer·사용자월드의장착/보관/전달대표흐름은미검증이며기존실게임보류를유지한다.

