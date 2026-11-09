# 생활 선택 트리 첫 단위 구현 명세

## 1. 입력과 구현 경계

입력은 [합의](00-agreement.md), [개발 방향](01-direction.md), [HEAD 판단](02-direction-decision.md)이다. 이 명세는 **공통 탐색 + 제작·요리·농사·보관/꺼내기의 실제 선택 연결**을 구현한다. 수리·자동 가공·강화·거래·낚시의 새 몸 실행과 정식 설치/실게임은 이번 단위에 넣지 않는다. 이 단위를 전체 생활 기능 완료로 보고하지 않는다.

기반 커밋은 HEAD가 고정한 `1fc89b93edbee31a9001a174581e32568119efc1`이다. 공식 `685dde4`에 가방장비 후보를 통합한 후 상자 실제 용량 수정까지 포함한다. 구현 직전에 `git show --stat <고정 SHA>`와 조상 관계를 확인한다. 다른 담당 worktree의 움직이는 HEAD나 미커밋을 가져오지 않는다. 기존 경로/브랜치가 없으면 `C:/Dev/CoreKeeperModSDK-life-tree`, Git `life-tree-20261107`을 이 커밋으로 만든다. 이미 있으면 실제 HEAD·status를 먼저 확인하고 동일 작업의 계속인지 HEAD에 보고한다. 공식 저장소·다른 담당 worktree를 수정하지 않는다. Unity 환경용 파일은 기존 compile 절차의 사본만 격리 worktree에 준비하고 제품 커밋에 섞지 않는다.

## 2. 변경 파일과 책임

| 파일 | 변경 책임 |
|---|---|
| 새 `Scripts/Core/AILifeTree.cs` | Unity 없는 탐색 커서, 이동 토큰 검증, 경로/페이지 이동, selector/수량 형식과 진척 계산. 게임을 읽거나 실행하지 않는다 |
| 새 `Scripts/AILife.cs` | 캐릭터별 임시 상태, 현재 깊이의 목록을 관련 모듈에서 가져오기, nav/실행 후보 구성, 직접 실행 생성, 실행 전 재검증, 결과 관측 연결 |
| `AIAgents.cs` | AISlot.LifeTree와 Count 증가. 기존 슬롯 번호 보존 |
| `AIDecisions.cs` | AIGoal의 mode/second 선택값, AICandidate의 nav 및 생활 실행 binding. 기존 필드 재사용 |
| `AIBrain.Candidates.cs`, `AIBrain.Loop.cs` | 페이지의 손실 없는 후보 조립, 읽기전용 응답 선처리, 실행 직전 재검증, 입력에 경로·부족 사실 표시 |
| `AIActions.cs` | farm/cook/craft의 선택 payload를 실제 skill에 전달하고 해당 stale 검사를 호출 |
| `AIFarm.cs`, `AICook.cs`, `AICraft.cs`, `AIBrain.Prep.cs` | 관측·구체 실행 후보 생산을 명시 선택값으로 호출 가능하게 분리. 현재 Goal 임시 교체 금지. 기존 몸 입력·이동·예측 재사용 |
| `AIInventory.cs` | 자기 상자/정확 슬롯 후보를 명시 질의로 생산하는 입구. 기존 정상 이동·관측 receipt 재사용 |
| `Core/AIPlanProtocol.cs`, `Core/AIPlanRules.cs`, `AIPlanner.cs`, `AIGoals.cs`, `AIGoalText.cs` | 생활 selector와 상위 count/where를 손실 없이 전달·검증·표시. 현재 목표와 탐색 상태를 구별 |
| `Tools/ai-bridge/protocol.mjs`, `brief.md`와 해당 테스트 | 같은 selector의 shape와 실제 의미를 문서화. Decisions 외부 API/요금/모델은 변경하지 않음 |
| `Tools/core-tests`, 새 `Tools/life-tests`, 기존 compile/execution/adaptor test 구성 | 아래 검증만 추가·실행. 생산 정책 로직을 테스트에 복사하지 않음 |

파일을 더 나누는 것은 위 책임을 표현하는 코드 배치만 허용한다. 새 공통 플러그인 체계, 동적 그래프 엔진, 영구 저장 파일은 만들지 않는다.

## 3. 상위 계획의 입력 계약

기존 goal/plan의 do/item/count/where/keep/slot은 유지하고 `mode`, `second` 두 선택 문자열을 추가한다. PlanStep 생성자는 기존 호출이 그대로 컴파일되도록 두 선택값을 끝의 optional 인수로 받는다. PlanAnswer/PlanGoalJ/AIPlanner의 변환·SetGoalFrom·AIGoals.SetGoal과 목표 보고에 함께 전달한다. 문자열 누락/JSON null/빈 문자열은 미지정이다. 새로운 필드는 생활 외 목표에 있으면 거절한다. 기존 목표의 유효 형식과 fixture는 유지한다.

| do | mode | item / second | where | count 의미 |
|---|---|---|---|---|
| farm | hoe, plant, water, fill, harvest 또는 미지정 | plant의 item은 씨앗. harvest/water의 item은 작물 필터(미지정이면 종류 무관). second 불허 | farm 또는 미지정, tile:x,y, rect:x1,y1,x2,y2 | 해당 mode의 처리할 칸 수. fill은1만. 미지정은 유지관리 질의이며 목표 총량이 아님 |
| cook | put, fill, take, wait 또는 미지정 | put은 item/second가 입력0/1 재료. fill은 item이 비어 있는 입력1에 넣을 재료. take의 item은 출력 필터. 미지정이면 트리에서 선택 | 미지정 또는 보고에 나온 자기 pot:<ID>@x,y | put/fill은 접시를 만들 투입 짝 수, take는 수령할 접시 수, wait은 수량을 받지 않음 |
| craft | 미지정 | 기존 item 필수, second 불허 | 미지정, hand, 알려진 station:<ID>@x,y | 기존 품목 목표 수량. 실제 레시피 1회 출력과 요청 회수를 구별하여 표시 |
| store/take | 미지정 | 가방장비의 기존 계약 | 기존 chest:x,y / base 계약 | 기존 이동 개수·keep·slot 의미 그대로 |

농사/요리에서 count를 지정하면 정수1..9999이며 mode도 지정한다. count 미지정은0으로 내부 정규화해 유지관리 동작의 가용 범위에서 작은 실행묶음을 제시한다. farm mode 미지정 + item 지정은 종자 여부에 따라 자동 mode를 추측하지 않는다. 상위에 구체 mode 필요를 반환한다. mode만 있고 item/where가 없으면 현장이 트리에서 좁힐 수 있다. put의 재료 둘 중 하나만 상위에서 정해도 나머지는 현장이 선택할 수 있다. bridge shape는 타입·범위만 확인하고 실제 종자·권한·시설·게임 규칙은 모드가 확인한다.

수량을 입력마다1..N 선택으로 펼치지 않는다. 현장은 위 count를 사용한다. 바꾸고 싶으면 기존 request_plan을 선택하고 입력에 현재 선택·요청량·가능량·부족 사실을 포함한다. 요청량이 없는 별도 생활 행동은 해당 실행묶음의 실제 시도량을 명시하며 원래 목표의 수량을 가져다 쓰지 않는다.

where는 형식과 대상 범위를 분리한다. rect 좌표는 정수로 읽고 양끝을 정규화하며, 전체 사각형을 무조건 생성하지 않고 현재 관측한 유효 칸을 필터링한다. farm/tile/rect는 자기 밭 작업에만 쓰고, hoe는 기존 기지 근처의 관측된 일굴 수 있는 땅만 후보로 삼는다. 다른 캐릭터의 밭은 rect에 포함돼도 제외한다. 제작의 알려진 station은 기존 캐릭터 지식 범위, 요리 pot은 자기 솥만 허용한다.

## 4. 커서와 토큰

`LifeCursor`는 Owner, GoalRevision, Revision, Open, Branch, Mode, Item, Second, Where, Slot, Page를 갖는다. 문자열은 selector/식별자이며 Unity Entity를 커서에 저장하지 않는다. 부모로 돌아갈 때 마지막 선택값 하나를 비우고 Page=0으로 한다. 루트는 선택값 전부와 Page를 비운 열린 커서, 닫기는 선택값 전부를 비운 닫힌 커서다. Branch/Mode 뒤로 가기·루트·닫기·페이지 이동은 모두 Revision을 증가시킨다. 동일 깊이 목록을 다시 그렸다는 이유만으로 Revision을 증가시키지 않는다.

`LifeNav`는 Owner, GoalRevision, CursorRevision, Op, Value를 담은 요청 시점의 변경 불가능 토큰이다. Op는 open/branch/mode/item/second/where/slot/back/root/close/page 중 하나다. 적용 전 세 번호와 현재 owner를 비교한다. 다르면 커서/몸/목표/대기 선택을 바꾸지 않고 새 목록을 볼 이유만 반환한다. 알 수 없는 Op/값도 같은 방식이다. 후보의 nav value/key는 `life_nav:<cursorRevision>:<op>:<identifier>` 형태의 안정된 신원이며 같은 질문 안에서 유일해야 한다.

상위 목표가 실제로 바뀌면 커서를 닫고 선택값/페이지/해당 목표의 임시 진척을 초기화한다. 다른 캐릭터, 월드 종료/재입장에는 커서를 공유하지 않는다. AIAgent.Get<State>(AISlot.LifeTree)를 쓰고 기존 agent reset 경로에서 상태가 초기화되게 한다.

실행 후보에는 `LifeSelection` binding을 붙인다. 이는 Branch/Mode/Item/Second/Where/Slot, RequestedCount, OfferedCount, GoalRevision, 현재 목표 참조, 현재 목표와 일치하는가를 가진다. entity/버전, 정확한 재료/출력 variation과 슬롯 stamp, 실제 tile 목록은 AICandidate와 관련 실행 payload에 요청 시점 복사본으로 둔다. 말단을 골랐다고 커서의 나중 값으로 payload를 다시 만들지 않는다.

## 5. 목록 생성과 조립

### 한 깊이만 생성

루트 갈래는 제작·요리·농사·보관/꺼내기 4개다. 아직 새 몸 실행이 없는 수리/강화/거래/낚시는 실행 말단이나 지원 가능 갈래로 추가하지 않는다. 현재 목표 None(직접 중지/자기방어만)이면 생활 탐색을 새로 시작하지 않는다. real goal 또는 Free에서는 탐색 가능하다. 전투 중에는 생활 페이지/실행을 제시하지 않고 기존 전투 후보를 유지한다. 커서는 보존되며 전투 뒤 목록을 새 관측으로 만든다.

각 깊이에서 현재 항목의 식별자를 ordinal 정렬하고 페이지당5개만 후보로 만든다. 전체 재료 짝, 물품×목적지, tile의 부분집합, 수량 조합을 먼저 만들지 않는다. 품목/자기 시설/칸 목록의 단일 축 스캔은 허용한다. 해당 페이지/선택의 조건을 판단할 때만 상대 축의 예측/경로를 계산한다.

닫혀 있을 때 기존 실제 후보 조립을 유지하고 마지막에 고정 value `life_open` 하나를 더한다. 열려 있을 때는 비전투에서 기존 결과 중 continue/위험 대응 eat/doorclose와 선택과 무관한 기존 공통 제어를 보존하고, 생활 페이지5개 및 back/root/close/prev/next를 별도 슬롯으로 조립한다. 기존14개 cut에 nav를 넣어 잘라버리지 않는다. 페이지에 prev/next가 없으면 만들지 않는다. root에서는 back을 생략한다. 해당 생활 말단과 같은 직접 후보는 신원으로 중복 제거한다. 직접 지정 실행 후보가 있으면 최대 하나를 page와 별도 보존해 바로 실행 가능하게 한다. 원래의 Finish가 부여한 일반 value는 유지하며 새 항목은 안정 key를 기반으로 유일 value를 부여한다. 기존 공통 stop_action/finish_goal/request_plan 추가는 마지막에 그대로 수행한다. 탐색이라는 이유로 그 제어를 없애지 않는다.

### 갈래별 깊이

| 갈래 | 펼치는 순서 |
|---|---|
| 농사 | mode → plant이면 씨앗, harvest/water이면 작물 종류(전체 포함), hoe/fill은 종류 생략 → 영역/특정 칸 → 실행 |
| 요리 | mode → put이면 입력0 재료 → 입력1 재료, fill이면 넣을 재료, take는 출력 종류 → 자기 솥 → 실행. wait은 자기 솥→기존 cookwait 실행 |
| 제작 | 캐릭터에게 열린 품목 → 손 또는 실제 제작대/기억한 제작대 → 실행/그 제작대로 이동 |
| 보관/꺼내기 | store/take → 자기 상자 → 실제 관측한 물품·정확 슬롯 → 실행. 내용 미관측 상자는 먼저 다가가 열기만 실제 선택으로 제공 |

영역은 전체 자기밭과 현재 보이는 자기밭의 연결구역(4방향 연결) 및 특정 칸을 선택할 수 있다. 연결구역 이름에는 anchor 좌표·칸 수·대상 상태를 표시하며 식별은 정렬된 실제 tile 집합이다. 상위 rect/tile가 있으면 그 필터를 적용한 뒤 묶는다. 개별 칸 목록은 별도 깊이로 페이지 처리한다. hoe는 기존 NewPlotTiles의 후보 수집을 선택 가능한 현재 영역으로 분리하고 기존3~9칸 기지 범위·hoeable·stand/충돌 확인을 유지한다. 새 기지 배치나 범위 밖 농장 건설을 추가하지 않는다.

기억한 화면 밖 솥/제작대/밭/상자는 마지막 관측·현재 모름을 표시하고 이동/가서 확인 선택만 만든다. 화면 밖 버퍼를 읽어 현재 입력·작물·상자 내용을 알아내지 않는다. 도착 뒤 다시 관측하고 다음 실행을 고른다.

## 6. 응답 처리와 직접 실행

`AIBrain.HandleResponse`의 chosen 탐색/기본 요청 revision 검증 뒤, `_reqPrefetch` queue 분기와 ApplyOrHold보다 앞에서 nav를 별도 소비한다. nav 자체 stale는 AIActions.Stale를 통해 생활 커서 토큰을 확인할 수 있게 연결한다. 성공이면 커서만 변경하고 `_needCall=true` 및 경로 변경 이유를 설정한다. 실패면 그 이유로 새 목록을 요청한다. 어떤 경우든 StartSkill, Stop, CancelPendingChoices, DropQueued, `_held`/`_queued` 대입, Goal 변경, receipt 성공, Applied/AnsStarted 증가를 하지 않는다. Prefetch 응답이어도 동일하며 실행 경계까지 기다리지 않는다. 기존 API 실패·비용한도 처리도 현재 몸·목표를 유지한다.

실행 말단과 완전 지정된 상위 목표의 직접 후보는 **같은 모듈의 `ExactCandidate` 입구**를 사용한다. AILife는 Goal을 임시 교체하지 않고 선택 요청을 명시 인수로 전달한다. 기존 AIGoalFeatures/Prep의 현재 목표 경로도 그 입구를 사용하도록 바꿔 같은 요청에서 서로 다른 선택/수량을 만들지 않는다.

실행 binding이 현재 목표의 do/mode/지정 item/second/where/slot과 일치하면 현재 목표 요청량의 잔량을 사용한다. 목표에서 미지정한 값은 현장의 선택으로 좁혀도 일치한다. 명시된 값과 다른 대안을 고르면 `원래 단계 밖의 별도 행동; 목표 진척에 합산하지 않음`을 설명에 표시하고 현재 목표 참조를 붙이지 않는다. 그런 별도 행동의 작업량은1회/1칸/1개이며 더 많은 양/중요한 계획 변경은 request_plan으로 요청한다. 목표를 몰래 덮어쓰거나 다른 품목을 원래 목표의 달성량에 넣지 않는다.

후보→스킬 key는 씨앗/재료 순서·variation·시설 entity Index/Version·mode·타일 집합·실제 요청 수량을 포함한다. 긴 타일 집합은 Core의 결정적인 해시를 써도 되지만 hash 이전 원문도 payload로 보존하고 재검증은 원문을 쓴다. 스킬 생성 후 Key가 후보 key와 같게 한다. 현재와 같은 완전한 선택을 다시 고르면 기존 계속하기 처리를 재사용한다. 다른 씨앗이나 다른 솥인데 첫 tile/entity만 같아서 같은 행동으로 합쳐지지 않게 한다.

실행 직전 AIActions.Stale와 각 skill.Start에서 실제 대상/소유/관측 가능/슬롯 stamp·재료·타일 상태를 다시 확인한다. 요청 시점 이후 **선택한 항목의 신원이나 조건이 바뀌면** 후보를 폐기하고 차이를 보고한다. 다른 씨앗·솥·슬롯·타일로 대체 실행하지 않는다. 요청 시점부터 가능한 양이 요청량보다 적었던 경우는 OfferedCount와 부족량을 설명한 말단을 선택했으므로 그 OfferedCount만 실행할 수 있다.

## 7. 각 실행의 세부 변경

### 7.1 농사

`AIFarm.Candidates`를 관측 Snap과 명시 선택을 받는 목록/ExactCandidate로 분리한다. SeedsInBag의 첫 항목 고정을 제거하고 씨앗 목록 자체를 선택하게 한다. 식용 여부로 종자를 단정하지 말고 seed prefab의 실제 설치/작물 연결이 확인된 종자만 분류한다. 확인 안 된 종자는 실행 가능으로 노출하지 않고 부족한 규칙으로 보고한다. 직접 실행 검증에는 실제 선택 종자·목표 타일·도구를 넘긴다.

plant/hoe/water/harvest의 수량은 처리할 tile 수다. 명시 count 잔량에서 유효 영역/씨앗/물/도구가 허용하는 양을 계산하고 **한 실행 묶음은 최대6칸**으로 한다. 이6은 몸 실행 묶음 크기이며 사용자 요청이나 전체 밭 규모 상한이 아니다. 기존 MaxPlots=6은 과거 자동 밭 생성의 기본 크기인 코드 사실이다. count/where 미지정 유지관리에서만 그 기본을 재사용하고, 명시 count/where를6칸 전체상한으로 묵살하지 않는다. 명시 범위의 현재 적합한 땅/자기밭이 남아 있으면 다음 묶음을 AI가 선택할 수 있다. 요청12/가능9면 첫 묶음6, 확인 뒤 잔량6 중 가능3이며 미충족3을 보고한다. 명시 범위 밖 자동 확장은 하지 않는다.

FarmSkill은 선택 도구/종자와 타일 복사본을 고정해 실행한다. 기존 HeldItem이 매번 다른 괭이/물뿌리개로 바꿔 잡는 동작은 이번 명시 선택에서는 막고, 선택한 도구가 사라지거나 부서졌으면 사실을 반환한다. 이미 작업된 tile은 새 성공으로 세지 않는다. 동작 전후를 비교해 실제로 바뀐 tile만 작업 확인량에 더한다. `_done`과 가방 변화는 구별한다. 수확은 거둔 칸과 실제 가방 증가를 따로 보고하며, 바닥 드롭을 가방 확보로 세지 않는다.

### 7.2 요리

`BestPair`는 추천/설명에 유지하되 실행 허용의 필터로 사용하지 않는다. 첫 재료 페이지를 열 때는 가방의 유효 재료 목록만, 상대 재료 페이지를 열 때만 선택 첫 재료와 `Predict`를 계산한다. 같은 재료 둘이면 실제 두 개 소비 가능량을 반영한다. 결과가 생식 배고픔보다 낮아도 게임 규칙상 유효하면 효과·비용을 보여 주고 선택 가능하다. 직접/트리 양쪽에서 둘째 자기 솥도 선택 가능하다.

솥 empty/input/output/time 상태를 정확히 읽고 put/fill/take/wait의 현재 조건을 구분한다. put/fill의 OfferedCount는 상위 잔량, 선택 재료 실제 가능량, 같은 재료2배, 해당 입력/출력 적합 공간을 반영한다. MaxPairs=5도 기존 추천 묶음 크기이지 명시 요청 총량 상한이 아니다. 한 요청의 실제 투입량은5짝 이하 작은 묶음으로 처리하고 남은 양을 별도 보고한다. take는 `_n`을 실제 이동 요청에 적용해 현재 pot.outN 전체를 무조건 가져오지 않는다. 요청 시 선택한 출력 ID/variation과 일치할 때만 이동한다.

CookSkill은 입력0과 입력1의 **실제 투입 확인량**을 추적한다. 자동 생산이 시작돼 입력이 줄어들 수 있으므로 단순히 두 슬롯이 차 있거나 output>0이라는 이유로 요청량 전부를 투입했다고 선언하지 않는다. 자기 요청 전후의 입력·해당 출력 변화와 가방 감소를 함께 확인해 확실한 양만 보고하고, 불명확하면 미확인 상태를 유지해 재판단으로 넘긴다. 같은 입력1 보충 요청을 확인 없이 반복 발송하지 않는다. 일부만 들어갔으면 실제 상태와 빈 입력을 보고하고 다음 fill 선택으로 이어진다.

put/fill 성공은 투입 단계이고 음식 확보가 아니다. take 성공은 main inventory의 정확한 output ID/variation 증가량으로 확인한다. 커서/솥 잔류·가방 가득 참·일부 이동은 각각 facts에 남긴다. 시간 경과만으로 유효 행동을 강제 취소하지 않는 공통 계약을 유지한다.

### 7.3 제작

캐릭터가 지금 알 수 있는 손 제작/관측·기억한 제작대의 레시피만 목록에 넣는다. prefab 전체 레시피를 현재 해금으로 승격하지 않는다. 품목을 고른 다음 해당 레시피를 실제 제공하는 손/제작대별로 선택한다. 기억한 제작대는 이동 후 다시 확인하며 실제 레시피와 runtime CraftingCD/CanCraftObjectsBuffer를 읽는다. 자동 가공(CraftingCD.IsProcessAutoCrafter)은 일반 Craft 말단에서 제외하고 지원 경계로 설명한다.

CraftSkill은 선택한 station entity와 recipe entry의 출력량/소비 정보를 고정하고, 실행 직전 실제 레시피·재료·출력 공간을 확인한다. 기존 Materials의 기본값을 최종 비용 확정값으로 표현하지 않는다. 실제 게임이 정한 recipe amount와 요청 craft 횟수를 구별한다. 상위 목표 수량을 충족할 회수는 `ceil(남은 품목 수/한 회 기본 출력)`이며 예상 초과량을 선택 설명에 표시한다. dynamic bonus로 생긴 추가 출력은 실제 결과로 기록한다. 선택 레시피가 소비/결과를 확정할 수 없는 형태면 실행 가능하다고 추측하지 않고 해당 조건 미확인을 반환한다.

제작 결과는 main inventory와 커서를 따로 읽는다. 기존 `StowCursorItem`의 정상 UI 요청을 재사용하되 커서에 잔류하면 Done/가방 확보라고 끝내지 않고 실제 잔류 관측을 기록한다. 커서/바닥을 포함한 InventoryCounts 증가만으로 가방 수령 완료를 세지 않는다. 실제 main inventory가 늘고 커서 상태가 확인된 뒤 확보량을 보고한다. 선택 station/레시피의 key를 후보와 맞춘다. 기존 Prep에서 필요한 도구 제작도 명시 생산자에 연결하되 목표를 새로 세우지 않는다.

### 7.4 보관/꺼내기

AIInventory의 정상 InventorySkill/MoveAmount/슬롯 검사/receipt를 그대로 재사용한다. `CandidatesFor(world,player,observation,currentKey,requestGoal,progressGoal)` 입구를 추가한다. requestGoal은 임시 선택 질의이며 AIGoals.Goal에 대입하거나 GoalSet을 호출하지 않는다. 기존 `ChestCandidates`를 이 입구에서 사용할 수 있게 분리하고 S.moved/S.done은 progressGoal이 실제 현재 목표와 같은 때만 읽는다. 별도 행동에는 이동 진척0을 인수로 쓰고 move.goal=null로 둔다. 기존 Completed의 reference 검사로 원래 목표 오염을 막는다.

상자를 먼저 선택하고 미관측이면 inspect/이동만 제공한다. 실제 열어 본 뒤 현재 슬롯 목록을 page 처리하고 하나를 선택한 다음 정확한 목적 슬롯과 이동량을 만든다. 기존12개 result cap을 재사용해 첫12개 밖 물품을 영구히 숨기지 않는다. 슬롯 목록 수집 단계는 전체 유효 source 슬롯을 읽되 최종 이동 목적지 후보는 선택 슬롯 하나에 대해서만 계산한다. 동일 물품 여러 variation/상태/슬롯은 합치지 않는다. 최신 기반의 실제 stackMaxAmount/슬롯 조건을 보존한다. 다른 사람 상자는 새로 허용하지 않는다.

## 8. 진척과 보고

AILife의 임시 진척은 목표 참조/GoalRevision과 선택 identity(mode/item/second/where)에 묶인다. 명시 농사 mode는 실제 바뀐 tile 수, 요리 put/fill은 확실히 투입한 짝 수, 요리 take는 실제 받은 접시 수를 기록한다. 작업이 달라지거나 명시 selector가 충돌하면 다른 행동의 결과를 이 카운터에 더하지 않는다. 작업 종료 이벤트는 기존 OnSkillEnded에서 실제 생산 skill의 결과를 읽어 연결한다. 각 skill에 `ConfirmedWork`, `ConfirmedReceived`, `RequestedWork`, `ResultStage`의 읽기 전용 결과를 주거나 동등한 명확한 payload를 사용한다. 문자열을 파싱해 수량을 복원하지 않는다.

관측은 `요청량 / 이번 시도량 / 확인된 작업량 / 확보량 / 잔량·부족`을 구별한다. 동일 결과를 중복 집계하지 않게 실행 Run 번호로1회 처리한다. finish_goal은 AI가 직접 고르며 원래 부탁의 order=done은 상위만 결정한다. 농사 유지관리의 성장중 관측도 원래 수확 부탁 완료로 바꾸지 않는다.

BuildInput에는 현재 경로, 현재 목표의 수량 기준, 목록은 조회이고 몸 행동을 바꾸지 않는다는 설명, 현재 부족 사실을 넣는다. 상위 보고 goal_now에는 mode/second/where/count와 생활 진척을 짧게 추가한다. brief에는 지원하는 네 갈래·선택지 탐색/직접 실행·count/where 예시·중간 결과 구분을 고치고 아직 없는 강화/거래를 지원한다고 적지 않는다.

## 9. 검증 명세

아래 검증을 구현자가 실제 수행해 결과폴더의 로그와 `05-implementation.md`에 기록한다. 숫자 목표/커버리지 경쟁이 아니라 아래 조건의 통과 여부를 기록한다.

1. **Core/프로토콜:** 실제 AIPlanProtocol→PlanStep→생활 selector 전달, 누락 호환, farm/cook mode별 형식, 잘못된 count/where, 원래 부탁과 작업 count 분리. bridge protocol 테스트에도 같은 계약 입력을 사용한다. core-tests에 생산 AILifeTree.cs를 링크하여 nav owner/revision/back/root/close/page·작업 진척을 시험한다.
2. **대표 실제 파이프라인:** 새 Tools/life-tests에서 생산 AIBrain의 후보 조립·응답 선택 처리, AILife, AIActions, AIFarm/FarmSkill을 직접 링크한다. 기존 execution-tests의 방법처럼 Unity/게임 ECS·입력 경계만 어댑터로 둔다. 위쪽 plan farm plant seed A/rect R/count6→현재 probe skill과 held/queued를 가진 채 농사→plant→seed A→R 탐색→nav가 probe/held/queued/목표를 유지→생산 Apply/AIActions.Make/FarmSkill.Start/Tick→정상 입력 경계→어댑터의 실제 상태 변화4칸→확인량4/미충족2와 다음 후보/관측을 확인한다. count12가6으로 전체 제한되지 않는 다음 묶음도 확인한다. 정책 로직/응답 분기를 테스트에 복사해 만든 모사로 대체하지 않는다.
3. **요리 표본:** 생산 AICook/CookSkill로 BestPair와 다른 유효 재료, 둘째 자기 솥, 재료 순서/variation·지정량, 부분 투입과 출력 잔류, 지정 개수만 take, main inventory 수령을 실행한다. 직접 지정 후보와 트리 말단의 payload/재검증 입구가 같음을 확인한다.
4. **제작/상자 표본:** 생산 CraftSkill에서 station 선택·출력2 레시피의 요청 회수·커서 잔류를 확인하고, 생산 AIInventory/InventorySkill에서 선택 상자→inspect→첫12개 뒤 슬롯→정확 양 이동·실제 receipt와 다른 목표 진척 불변을 확인한다. 기반의 가득 찬 상자 공간 수정 회귀도 유지한다.
5. **교차 경계:** 늦은 nav·다른 캐릭터·목표 변경·전투 진입/복귀, prefetch nav, 오래된 execute와 교체된 슬롯/솥/타일, 페이지 잘림·특정 칸 접근·close/root, API 실패/한도에서 현재 행동 유지. 선택 후 변경은 다른 대상으로 실행하지 않는지 확인한다.
6. **기존 회귀:** `dotnet build Tools/compile-check -c Release`, `dotnet run --project Tools/core-tests -c Release`, `Tools/execution-tests/run.ps1`, ai-bridge protocol 관련 테스트 및 가방장비 기존 어댑터 실행을 수행한다. 기존 bag 어댑터의 정확한 실행 입구는 기반 Tools README/04 의존보고서에서 가져오고 다른 담당 파일을 고치지 않는다. 새 life-tests는 실행 명령을 README에 남긴다.
7. **Unity:** 격리 Unity6000.0.59f2 `-batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK-life-tree -executeMethod HeadlessSdk.CompileCheck -logFile <결과폴더>/unity.log`. 실제 SDK assembly/USE_PUG_OTHER는 기존 검증 환경을 재사용하고 동시 Unity batch가 없는지 확인한다. BuildAndInstall/게임 실행/설치 변경은 하지 않는다. 종료코드와 compiler error 검사를 함께 기록한다.

독립 검수자는 합의·명세와 실제 diff/검증을 대조한다. 대표 파이프라인의 어댑터는 모델/API 또는 실제 게임 소비자를 실행한 증거가 아니므로 이 경계를 결과에 적는다. 실게임 보류가 해제된 뒤 실제 모델의 탐색→실제 입력→가방 결과→다음 판단은 별도 확인이 필요하다.

## 10. 구현 순서와 결과물

1. 기반 고정/격리 worktree와 입력 해시 기록.
2. Core cursor/형식과 상위 selector 전달.
3. 생활 생산자 관측/ExactCandidate 분리 및 첫 네 갈래 연결.
4. 후보 조립/nav 응답 선처리와 직접 실행 재검증.
5. 실제 결과·진척/상위 보고·brief 연결.
6. 위 검증·수정 후 독립 검수에 후보 커밋과 결과파일 전달.

결과는 구현한 범위·고정 기반·후보커밋·변경 책임·검증/로그·미확인과 후속 단위를 `05-implementation.md`에 정리한다. 공식 코드/설치 반영 여부를 명확히 구별한다. 그래프 갱신은 후보 구현과 실게임 미확인을 구별해 원본에서 반영하며 generated graph.json을 직접 수정하지 않는다. 명세가 실제 고정판과 달라 새로운 규칙 결정이 필요하면 추측 구현하지 않고 차이와 필요한 결정을 HEAD에 반환한다.
