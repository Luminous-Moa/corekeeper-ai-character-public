# 생활·인벤토리 통합판 독립 검수

2026-11-07. 최신 고정 검수판은 `39b22df96dffe869ab0ccd474b18d31462b71d70`이다. 통합 시작판은 merge `7dc9c7e781f1b50a95cce1c89acaed260334d934`이며 부모는 생활판 `c4a0f5e`와 가방 트리판 `741532f`다. 기준은 `integration-agreement.md`, 가방 `tree-agreement.md`/`tree-spec.md`/cap 소비자 판정, 생활 `00-agreement.md`/`03-spec.md`와 c4 독립 검수 범위다. 741의 검수·패키지는 유효한 격리 중간 근거이며 통합판의 성공으로 자동 승계하지 않았다.

**현재 판정: 최신 39b22df의 소스·오프라인 통합 흐름·빌드/설치 없는 SDK 후보 대응 독립 검수 통과.** 7dc 통합판에서 발견한 전투 중 인벤토리 진입 P2는 수정 고정판에서 해소됐고, 정확 반례와 인접 stale/실제 Apply/취소 경계도 통과했다. 최신 Unity 컴파일·설치 없는 SDK source/ZIP 대응도 완료 증거를 독립 읽기 대조했다. 실제 모델/게임 소비자·설치/실게임 완료 판정은 아니다. 최종 그래프 staging의 한정 출처 검수도 통과했으며, 공식 반영 후 읽기 확인은 별도 기록한다.

## 고정·독립 실행

생산 작업트리의 HEAD·환경 미커밋 상태와 검수 경로 존재 여부를 읽고, 별도 `C:/Dev/CoreKeeperModSDK-bag-review-integrated` detached worktree를 만들었다. 제품·공식 main/graph·설치·게임·프로세스를 변경하지 않았다. 시험 추출 스크립트가 이 검수판의 Tools 아래 generated binding/Base 메서드 slice3개를 재생성했으며, 이는 제품 소스 수정과 구별한다. 독립 추가 재현은 `review-repro/`에 있다. 전체 제품 C#103개 해시는 `review-repro/7dc9c7e-source-pin.json`과 중간 `review-repro/b1e05a8-source-pin.json` 및 최종 `review-repro/39b22df-source-pin.json`에 구별해 고정했다.

다음은 검수자가 실제 실행한 결과다. 넓은 회귀는7dc에서, 공유 guard 수정 후 b1에서 compile/Loop55/생활96/연결195를 재실행했고, 최종39에서는 compile/생활96/연결199를 다시 실행했다. 7dc→b1 제품 변경은 AIActions/AIBrain.Loop/AILife의 좁은 guard6줄 추가/3줄 변경이고 해당 Core·protocol·메뉴/요리/제작/인벤토리 입력은 불변임을 확인했다. 로그는 위 독립 worktree의 `review-repro/` 기준이며 각 검사 수는 서로 다른 경계를 포함하므로 합산하지 않는다.

| 실행 | 결과 | 로그 |
|---|---|---|
| fast compile | 경고0/오류0 | `compile.log` |
| Core / bridge protocol | 1298 / 154 통과 | `core.log`, `protocol.log` |
| 실제 공통 Loop 입구·Surface·JSON | 55/55 | `execution.log` |
| 실제 Menu/Inventory 소스 링크 | 348/0 | `menu.log` |
| 생활 실제 응답→Apply→농사·진척 | 96/0 | `life.log` |
| 요리 / 제작 / 인벤토리 실제 소스 어댑터 | 51/0 / 19/0 / 91/0 | `cook.log`, `craft.log`, `inventory.log` |
| 실제 Menu+Life+Surface/JSON+Apply+InventorySkill 연결 | 181/181 | `connected.log` |
| 독립 전투 전환 경계 | 7dc:181/182 실패 보존 → **최종199/199** | `7dc9c7e-connected-failure.log`, `connected-fixed.log`, `connected-final39.log`, `connected/IndependentMergeCases.cs` |
| 최종 관련 재실행 | compile0/0 / 생활96 / 연결199 | `compile-final39.log`, `life-final39.log`, `connected-final39.log` (Loop55는앞선동일입구재실행) |

각 실행 명령은 Tools 해당 README를 따르되 PowerShell 추출은 UTF-8 기본인 pwsh로 실행했다. `git diff --check c4a0f5e HEAD`도 통과했다. 연결·메뉴 산출물은 구현 artifact의 사본을 만든 뒤 Link와 추출 root를 이 독립 고정판으로 바꿔 재생성했다. producer의 움직이는 파일을 링크하지 않았다.

## P2 — 전투로 생활 메뉴가 숨겨질 때 인벤토리 진입 누락: 7dc 발견, b1/743 보완 후39 최종 해소

실제 경로는 `AILife.Compose(combat:true)`와 `AIBrain.InventoryChoiceSurface`다. 생활의 승인 계약대로 전투에서는 생활 nav/leaf를 숨기고 `Cursor.Open`을 보존한다. 그런데 public surface는 `Cursor.Open`만 보고 즉시 반환하므로 inventory entry도 붙이지 않는다. `life close` 역시 전투 후보에 없어 모델은 메뉴 닫기로 우회할 수도 없다.

독립 재현은 현재 goal Farm/plant6, 실제 가방 검 보유, 생활 open을 실제 Apply로 선택한 뒤 근접 적을 관측 입력으로 공급했다. 실제 AILife Compose와 실제 InventoryChoiceSurface의 결과:

```text
LIFE COMBAT INVENTORY BOUNDARY: lifeCursorOpen=True, inventoryActive=False, kinds=attack, inventoryEntry=False, lifeClose=False
FAIL independent merge: combat interrupting a life view must retain the approved possessed-weapon inventory entry
```

이것은 잘못된 인벤토리 요청을 보냈다는 P1이 아니다. 생활 탐색의 기존 커서 보존과 공유 surface의 소유권 조건을 연결하면서, 전투 중 유효 무기로 바꾸는 승인된 가방 흐름에 접근할 수 없어진 P2다. 기존 메뉴가 닫혀 있을 때의 전투 inventory entry는 살아 있으므로 그 정상 사례만으로 이 전환을 증명할 수 없다.

단순 entry 후첨만으로도 해결되지 않는다. `AIActions.Stale`와 `Loop.Apply`의 inventory navigation 분기에도 `Cursor.Open` 일괄 거절이 있다. 실제 표시→stale 검사→navigation 적용→필요한 소유권 전환까지, body/goal/생활 quota·Runs를 보존하는 연결을 확인해야 한다. 비전투의 열린 생활 노드에 비관련 inventory root를 상시 섞거나 새로운 전투 계획 정책을 추가하라는 요구는 아니다. owner와 HEAD에 즉시 전달했다.

b1 수정은 생활 커서가 열려 있다는 이유만으로 inventory 진입을 막지 않고, 현재 적 관측으로 생활 탐색이 보류되는 경계에서 entry 생성과 shared stale/Apply 허용을 연결한다. 실제 AIActions.Stale와 Apply가 같은 검사 입구를 사용한다. 독립 반례를 다시 실행해 attack+inventory_menu/entryTrue를 확인하고, 실제 Stale 통과→Apply navigation으로 life cursor만 닫기→inventory 전체 취소까지 body/goal/잔량/요청0 보존을 추가 확인했다. 수정 전181의 기존 cross-view/실행/진척 검사도 그대로 유지된 생산자191에 독립4경계를 더한 결과가195/195다.

## 병합·소비자 대조에서 확인한 사항

- `git show --remerge-diff`로 실제 충돌5파일 AIActions/AIBrain.Candidates/AIBrain.Loop/AIDecisions/AIInventory를 대조했다. 추가 공유 연결인 AILife/AIGoalFeatures도 확인했다. `lifeNav/life` 및 farm/cook/craft binding과 별도 `inventoryMenu` payload가 공존하고 nav kind는 `life_nav`/`inventory_menu`로 구별된다.
- 생활 nav는 기존 HandleResponse/Apply에서 prefetch·몸 전환보다 먼저 처리되며 held/queued·현재 몸을 보존한다. 성공 시 inventory 메뉴 토큰만 Reset한다. 반대로 가방 navigation은 AILife.LeaveNavigation으로 생활 커서/Offered만 닫고 기존 CancelPendingChoices로 이전 미적용 선택 권한을 폐기한다. LeaveNavigation은 Progress·Runs·goal을 지우지 않는다.
- 통합 연결181은 스냅샷이 아직 유효한 inventory leaf도 cancel→life entry 뒤 적용 권한을 잃고 실제 Apply에서 시작되지 않는 것을 확인했다. life close나 inventory cancel 뒤 예전 open 토큰이 부활하지 않는 것도 확인했다.
- 생활의 기록된 작업2/6 뒤 inventory enter/cancel을 거쳐도 잔량4이고, 진행 중 run binding을 같은 전환 뒤 Ended해도 잔량2다. 이 결과는 실제 AILife/Progress/Runs 코드에 구조화 WorkResult를 공급한 검사이며 게임 농사 consumer를 이 통합 표본에서 실행했다는 뜻은 아니다. 별도 생활96은 실제 FarmSkill의 입력을 fixture가 소비하는 대표 경로를 확인한다.
- AILife.Resolve/ProgressKey/Remaining과 기존 수량·wildcard 공유 로직은 c4를 보존했다. inventory branch의 실제 이동 진척과 다른 물품 진척 제외는 merged91 검사에서 확인했다. 제작 output2/요청3의 회수·재료 gate, 요리 분리 스택·수령 경계도 merged19/51에서 유지됐다.
- public surface는 legacy flat inventory/store/take를 숨기되 `life` binding이 있는 완전 지정 실행은 보존한다. AIGoalFeatures의 정확 slot+자기 chest 지정 입구→실제 life-bound store5→MoveAmount 한 번→양 슬롯 수동 관측→실제 moved5/receipt와 원래 부탁 보존을 연결181에서 실행했다.
- 가방 분류·물품 무상한/무페이지 및 Decisions countcut 제거 코드는741과 동일하다. 생활의 페이지5는 별도 승인된 lossless cursor로 c4와 같은 방식이며 이를 영구 누락 cap으로 바꾸지 않았다. 작업 count/RequestedCount/OfferedCount는 후보 개수 제한이 아니다. 실제 sensor/search/소유·물리·보고 경계와 기존 예산을 넓히는 병합은 확인되지 않았다.

## 증거 한계와 대기 자료

메뉴와 연결 trace의 World/DB/UI 큐·물리 dispatcher·일부 planner/goal 경계는 대역이다. 양 슬롯·EquippedObject·상자/솥·작업 결과는 fixture가 공급한다. 실제 production 전체 CandBuild/전체 Tick 스케줄러·실제 모델/API·게임 consumer·사용자 월드의 전체 흐름을 실행한 근거가 아니다. 원래 부탁의 HostOrder 자료형/상태와 실제 선택/바인딩/수량 코드의 실행은 그 한정된 범위에서 확인했다.

기존741 또는7dc Unity/export 성공을 최신판에 승계하지 않았다. 다음의 b1 고정판 새 완료 증거를 읽기 대조했다. 검수자는 Unity/게임/설치/프로세스 제어·제품/main/공식 graph 변경을 하지 않는다. 실제 모델의 두 메뉴 탐색→정상 게임 입력→물품/작업 결과→다음 판단은 기존 보류로 미검증이다.


## 중간 b1 Unity·SDK 산출물 대조(최종39 성공의 대체 근거 아님)

자료는 integration/final-b1e05a8/{unity-input,unity-settings,unity-exit,artifact-manifest,package}.json과 unity-export.log다. sourceCommit은b1e05a8, Unity exit0/완료UTC2026-11-07 05:40:43.2580029이며로그의CompileCheck:ok·SDK built·Export complete,no install을확인했다. 원C#컴파일오류패턴은0이다. 검수자가Unity/export를실행한것은아니다.

103개제품소스를독립고정checkout과대조해102개rawhash동일,1개줄끝만차이(CRLF/LF정규화외다른byte내용차이0)를확인했다. producer의현재103rawhash와SDK로export된103C#rawhash는모두입력pin에일치한다. 상세독립기록은 review-repro/export-source-verification.json이다. 전체메뉴·생활소스를포함하며과거741입력을혼합하지않았다.

SDK후보는103C#와ModManifest.json의104파일sourcepayload이며Unity compilerDLL은검증용으로구별한다. AIPlayerSpike-integrated-b1e05a8.zip의707220바이트/SHA256 C7D8D4AC51A8E1DE78076F9D0F0C6D0028470B552531542E4AF81E647149FE55가package.json과일치한다. ZIP을설치/추출하지않고104payloadstream의hash를후보디스크파일과대조해불일치0을확인했다. 임시Editorhelper의hash도입력pin과같다.

공식main은읽기확인시여전히c4a0f5e이고공식소스102개와기존설치96파일의현재해시는Unity전입력pin에전부일치한다. owner의전후unchanged기록과함께이번후보검증이공식/설치변경을수행하지않았다는범위로확인했다. 이결과를설치본게임에최신코드가로드됐다는주장으로쓰지않는다.



## 최종39 표시 소유권 정합성과 lifecycle 재검수

중간b1의단순근접적판정을최종판은쓰지않는다. 실제CandBuild가생활Compose에넘기는combat결과로actor별State.ViewSuspended를기록하고,Surface·Stale·Apply는공유BlocksInventoryView입구로'현재실제로생활목록이표시되는가'를판정한다.39는기존goal/actor동기화와LeaveNavigation에서ViewSuspended=false를명시한다.메뉴소유권을닫을때진척/수량/Runs를지우는동작은추가하지않았다.새combat정책/거리·예산확대가아니라기존Compose결과의동일판정을세소비자에연결한delta다.

독립추가경계는근처적이있어도Compose(false)이면life노드전용/인벤토리entry없음,Compose(true)이면entry표시,그후다시life노드가표시되면이전entry답이Stale/실제Apply에서거절됨을확인했다.다시현재숨김상태에서새entry를받아실제Apply→life커서만닫기→전체취소까지몸/goal/잔량/요청0을유지했다.기존생산자192와독립7경계를포함한최종결과는199/199이며실제외부모델/게임consumer는0이다.개별fixture단계수를전체게임스케줄러증거로확대하지않는다.

## 최종39 Unity·SDK·공식/설치 대응

최종근거는 integration/final-39b22df/{unity-input,unity-settings,settings-after,unity-exit,artifact-manifest,package,package-verification,bridge-candidate}.json 및 unity-export.log다.sourceCommit39b22df/exit0/종료UTC2026-11-07 05:45:44.9543465,CompileCheck:ok·SDKbuilt·Exportcomplete,noinstall을독립읽기확인했다.C#컴파일오류패턴0이며검수자가Unity/export를실행한것은아니다.

제품C#103개를검수고정checkout과대조해102개rawhash일치,1개CRLF/LF만차이(다른byte내용차이0)를확인했다.producer현재103개와export된103개rawhash는모두입력pin에일치한다.독립기록은 review-repro/export-final39-verification.json이다.manifest103파일과실제104파일sourcepayload(C#103+manifest)를확인했으며UnitycompilerDLL을설치payload로혼동하지않는다.

최종ZIP AIPlayerSpike-integrated-39b22df.zip은707242바이트/SHA256 D8D8E891BFD2CAAEDBA34671958DBFFA21C125BC38246D5E8EA346381D3E107A로package.json과일치한다.설치/압축해제없이ZIP104개payloadstream을후보디스크파일과해시대조해불일치0이다.별도bridge후보2개도해시일치하며배포·재시작근거가아니다.

공식main은확인시c4a0f5e이며,그공식제품소스102개·기존설치96파일의현재hash가이번Unity전pin과전부같다.격리SDK/프로젝트설정8개도시작pin과현재hash가같고owner의settings-after표시는모두unchanged=true다.과거741/7dc/b1의빌드성공을39에승계하지않고이새입력·완료·source/ZIP대응으로판정했다.실게임·실모델·정식설치·공식반영은계속별도이며미검증/미수행상태다.


## 최종 그래프 staging 한정 독립 검수

최종39의 `graph-stage-result.md`, `graph-stage/패치-적용경계.md`와 활성 `원본-패치목록.md`를 기준으로 새 의도→코드→검수·빌드·sourceSDK 대응만 읽기 검수했다. 중대한 출처·경로 모순은 발견하지 않았다. 대규모 그래프 전체의 의미를 재검수하거나 생성 검사를 독립 재실행한 결과로 확대하지 않는다.

- c4 공식 기획 source의 원본 바이트와 staged Psource의 동일 길이 앞부분 SHA256이 `36BCF3936ECD9CDF4BBBB7887161A326DD69459E45E0DDA1EC89F4321F3E5696`로 일치한다. 기존 release-goal/common/생활 기획·관계 보존 단언과 실제 가방 추가 블록을 확인했다. 가방 무상한·무페이지와 생활 lossless paging/읽기전용 held·queued/명시 인자 직접실행/작업수량은 분리돼 있다. 통합 소비자 결정은 새 사용자 원문이 아닌 HEAD 참고 판단으로 표시된다.
- D overlay의 실제 Menu/Surface/Apply/Stale/FinalMove와 AILife Compose/BlocksInventoryView/LeaveNavigation/Remaining 앵커 및 기능·정책·미결 연결을 확인했다. 메뉴 흐름과 수량 보존은 정적 구현 위치로 설명되며, 실제 게임 성공으로 승격하지 않는다. 내부 storage는 생활 명시 binding 소비자로 남고, 과거 flat/UI 근거는 역사 자료로 구별된다.
- 최종 source39의 Git blob snapshot271개와 별도 runtime-package56개(총327개)의 디스크 해시를 각각 manifest와 독립 대조해 불일치0이었다. C#104는 제품103과 기존 Editor1을 구별한다. 연결 fixture14파일은 독립 고정39 worktree와 EOL 정규화 후 차이0이었다.
- 원문58개의 현재 원본 해시와 staged 증거 사본 해시가 각각 metadata와 일치했다. 활성 저작 패치23개의 staged 해시도 일치하며 history/context 역복사 항목은 없다. 원문 review가 이 부록으로 바뀌므로 마지막 재핀·생성/check 뒤 읽기 확인을 요구한다.
- 작업자 산출물의 13단계/56단언/53조회는 모두 성공으로 기록돼 있으며 source39/c4 기준을 확인했다. 이는 작업자 실행 로그를 읽은 근거다. 현재 canonical drift 중 history는 보호 대상이며, 과거 source-patches 잔여 파일이나 staging JSON 통째 복사가 활성 반영 범위가 아니다.

HEAD의 별도 승인으로 제품 main이39에 반영된 사실은 `source-acceptance.md`/`source-integration.md`와 구별한다. 앞선 빌드 대조의 공식c4 불변은 당시 시점의 관측이다. 그래프 공식 반영은 HEAD가 조건부 승인한 활성 저작 입력만 대상으로 최신 main/canonical 재확인·백업·공식 경로 재설정·재생성 뒤 확인해야 한다. `U.bag-live`는 열려 있으며 설치·외부 모델·실게임 소비자 성공을 주장하지 않는다. 검수자는 공식 그래프·제품·설치에 쓰지 않았다.

## 공식 그래프 반영 후 한정 읽기 확인

HEAD가 승인한 staged endorsement→마지막 핀/check→독립 읽기 gate를 통과한 뒤 그래프 작업자가 공식 저작 입력을 반영·재생성했다. 검수자는 `official-graph-apply.md`와 `official-graph-apply-20261107T060758/{before,after,commands}.json`, 실제 공식 G/P/L 출력을 읽어 확인했다. 이 부록은 제품 재검수 확대나 실게임 수용이 아니다.

공식 main HEAD와 제품 그래프 source는 모두 `39b22df96dffe869ab0ccd474b18d31462b71d70`이다. 공식 개발 config는 공식 B/snapshot을 가리키고 pin/remap은 공식 `C:/Dev/CoreKeeperModSDK`를 사용한다. 제품 그래프 노드6714/간선26029, entry `P.release-goal`, 기존 common8·생활12 연결을 확인했다. 가방 탐색/무상한, 생활 명시 대상 직접실행/작업수량, 별도 소비자 참고판단에서 실제39 Menu/Apply/Surface/Life 코드와 독립검수·연결·sourceSDK/HEAD수용 원문으로 이어지는 대표 경로가 남아 있다.

작업자의 공식 재생성14단계와 대표조회28건(42명령)은 모두 exit0이며, 보호 상태·소스·이전관측 비승계 단언도 성공으로 기록돼 있다. 검수자는 현재 공식 저작 입력23개와 근거 사본58개의 해시를 after 기록과 독립 대조해 차이0을 확인했다. 백업497개도 before 기록과 차이0이다. 반영 전 백업의 c4 기획 source와 현재 공식 Psource 동일 길이 prefix는 모두 SHA256 `36BCF3936ECD9CDF4BBBB7887161A326DD69459E45E0DDA1EC89F4321F3E5696`로 일치한다.

before/after의 보호 상태는 동일하며, 그 안의 환경7·미추적450·bridge2·설치96·소스359 파일은 현재 해시도 차이0이다. history/context는 활성 반영 입력에 없고, staging 시작 이후 달라진 root history는 기존 관측과 같은 별도 진행기록으로 유지됐다. 다른 history/context15개는 시작핀과 일치한다. 검수자가 공식 문서나 제품을 수정하지 않았다.

공식 `U.bag-live`는 여전히 실제 게임 흐름 미확인 상태다. 현재 metadata는 liveGameObserved/gameConsumerExecuted=false이며 과거 UI·보조 런타임 관측도 현재 source의 실행 근거로 취급하지 않는다. 설치·외부 모델·실게임 보류는 그대로다. 공식 반영 후 한정 출처·경로 검수에서 중대한 결함은 발견하지 않았다. 이 최종 부록 원문을 한 번 재핀·재생성/check한 뒤 해시와 source를 읽기 확인하며, 이후 보고서 본문을 다시 바꾸지 않는다.
