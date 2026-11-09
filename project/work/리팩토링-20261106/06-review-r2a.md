# R2a 독립 코드 검수

2026-11-06. 고정 diff `33f477b..bcdec88`, 저장소 `C:/Dev/CoreKeeperModSDK-refactor-20261106`. R2b가 같은 트리에서 진행 중이므로 제품 코드·시험은 고정 커밋의 `git show/diff/grep`으로 조사했다. 제품 소스·브랜치·작업트리 변경 없음.

## 판정

**R2a 독립 코드 검수 통과. Must-fix 발견 없음.** 승인 명세 `01-spec-goals-candidates.md`의 R2a와 대조해, 후보 필드의 의미별 분리와 둘레 역할 태그가 생산부터 실행·조립 소비까지 연결됨을 확인했다. 변경된 15파일을 직접 검토했으며 기존 값·생성자 인자 순서·후보 key·설명·배정 규칙이 달라지는 근거를 발견하지 못했다.

R1에서 읽은 공통 AGENTS, HEAD start-job 및 05-dev-planning 검수 기준, 승인 합의와 계획을 이어 적용했다. 모델·추론 강도에 관한 과거 지시는 승계하지 않았다.

## 생산자 → 소비자 매핑

위치는 `bcdec88` 기준. `S/`는 `Assets/AIPlayerSpike/Scripts/`다. 이전 이름의 전체 참조를 `33f477b`에서 검색하고 변경 후 참조와 대조했다. `eatId/eatVariation/item2/digWhat`는 `bcdec88`의 모드 소스와 core-tests에서 남은 참조가 없었다.

| 의미와 새 필드 | 생산자 및 소비 대조 |
|---|---|
| 품목 `itemId` | AIBase의 store/take/eat, AIBrain.Candidates의 eat, Prep의 craft, AICook, AIPlaceSpot, AIEdge/Guard, AIFarm, AIPlaceGoal이 기존 eatId와 같은 표현식을 대입한다. `S/AIActions.cs:28` 이후 Eat/Craft/Place/Store/PlaceTiles/Take/Farm/Cook 생성자와 `:86` PlaceStale, `:117` EatStale는 같은 품목을 읽는다. edgepickup 품목은 예전과 마찬가지로 보관하되 PickupSkill 생성자에 새로 전달하지 않았다. |
| 음식 variation `itemVariation` | `S/AIBase.cs:768`의 f.variation과 `S/AIBrain.Candidates.cs:290`의 hi.variation → `AIActions:28` EatSkill 두 번째 인자. 품목 수량과 섞이지 않는다. |
| 수량 `itemCount` | `S/AIBrain.Prep.cs:146` tg.n, `S/AIBase.cs:540` take, `S/AICook.cs:417/425/450` own.outN/best.n/fill.n → CraftSkill 두 번째, TakeSkill 세 번째, CookSkill 마지막 인자. 기존 값·순서 동일. |
| 둘레 틈 번호 `edgeGap` | `S/AIEdge.cs:540/558`, `S/AIEdge.Guard.cs:733/742`의 b.gap → EdgeFillSkill 첫 번째 인자. repair의 group ?? tiles와 두 행동의 :out 판단을 그대로 유지한다. |
| 대기 초 `waitSeconds` | `S/AICook.cs:431`의 math.clamp(left,1,5) → `AIActions:45` WaitSkill. 일반 wait는 여전히 Settings().waitSeconds를 읽으며 후보의 cookwait 필드로 바뀌지 않았다. |
| 요리 둘째 품목 `secondItemId` | `S/AICook.cs:425/450` best.b/ObjectID.None → `AIActions:44` CookSkill 네 번째 인자. take 경로의 미설정 기본 None도 동일하다. |
| 표시 대상 `targetLabel` | dig 생산자의 적·광석·물체·아이템·호스트·기억한 탐색 끝·기지·상자·제작대·솥·밭·장소·우회 경로 label을 모두 대조했다. follow/retreat/step/세 combo 및 craft도 기존 digWhat 값을 그대로 옮긴다. AIActions의 해당 생성자와 MakeCombo가 같은 인자 위치에서 읽고, follow의 '호스트'와 retreat의 '코어 스폰 자리' 빈 값 fallback도 같다. |
| 실행 모드 `actionMode` | `S/AIFarm.cs:639` act 및 `S/AICook.cs:417/425/450` take/put/fill → FarmSkill 첫 번째·CookSkill 두 번째 인자. 문장 label과 분리됐다. |
| 탐험 방향 `exploreDirection` | `S/AIBrain.Candidates.cs:643` e.dir → `S/AIBrain.Loop.cs:910`의 실제 기술 Tag `explore:` 접미부. Make의 GotoSkill label은 기존 '탐험' 그대로다. |
| 회수 설 칸 `pickupStand/pickupStandOnly` | `S/AIEdge.Guard.cs:817` stand ?? default 및 standOnly → `AIActions:38` PickupSkill의 nullable stand와 bool 인자. dest.x의 NaN 검사와 재배치 목적지 변환은 그대로다. |
| 후퇴 목적지 안쪽 여부 `retreatInside` | `S/AIBrain.Candidates.cs:274` baseIn → `AIActions:48` RetreatSkill 세 번째 인자. placeStandOnly를 후퇴 의미로 읽는 참조가 남지 않았다. |
| 놓기 제약 유지 | `placeStand/placeStandOnly`는 Place 생산자·Make·PlaceStale에 그대로 남았다. `AIActions:31` placeAt 분기와 :out key 보존, 일반 place key 계산이 동일하다. 시험 명령의 PlaceSkill 생성자 인자도 품목 이름만 바뀌었다. |

`S/AIDecisions.cs:94`의 새 문자열 필드는 모두 기존과 같은 빈 문자열, 품목은 ObjectID.None, 정수·bool·좌표는 기존의 0/false/default로 초기화된다. payload 계층이나 새 실행 추상화는 추가하지 않았다. Make/Stale 호출 위치·기술 구현·비동기 후보 적용 경로는 이 diff에서 변경하지 않았다.

## 역할 태그와 조립

- `S/AIEdge.cs:590/599`: 문을 안쪽/바깥쪽에서 새로 다는 두 생산자 모두 DoorPlace.
- `S/AIEdge.Guard.cs:775/783`: 문을 다시 다는 두 생산자 모두 DoorRepair.
- `S/AIEdge.cs:621`: 둘레 안 횃불 생산자는 TorchPlace.
- 일반 `S/AICraft.cs:640` AIPlaceSpot 후보와 이를 이용하는 AIBase/Prep/AICook/AIPlaceGoal은 기본 None. 일반 놓기 설명이 기존 둘레 역할 prefix를 생성하는 숨은 호출도 발견하지 못했다.
- `S/AIBrain.Candidates.cs:1075` AICandidate→AsmCand 어댑터가 edgeRole을 실제 복사한다. 조립 후에는 원래 후보 객체로 되돌리므로 새 태그나 실행 필드가 유실되지 않는다.
- `S/Core/AICandidateAssembler.cs:249` IsRepair는 기존 edgerepair 또는 place+DoorRepair를 읽는다. `:252` EdgeKindWord는 place만 role에 따라 분류하며 다른 kind의 문구와 기본 반환은 그대로다. None/알 수 없는 role의 place는 '놓기'가 된다.
- Select의 breach 우선순위, EdgeShare=4, Cut의 progress/keep/continue 보호, detour의 자리 되찾기 금지, Finish의 순서·value 생성 코드는 바뀌지 않았다. 현재 생산자가 만드는 태그가 기존 문장 분류와 일치하므로 기존 후보 순서·몫은 보존된다.
- `S/AIEdge.cs:696/697/704` door/torch 시험 명령도 role을 읽는다. Candidates → GuardCandidates fallback과 :out 조건, StartSkill 인자 및 출력은 유지된다.

새 enum은 분류 의미만 전달하며 문장 파싱 fallback을 production에 남기지 않았다. 이미 kind로 구별되는 edgefill/edgerepair/edgemat/edgepickup에 중복 역할을 붙이지 않았다. 새 중앙 실행 허브·정책·저장 상태는 없다.

## 출력·replay·시험

후보의 key/desc/progress와 생성 순서 표현식은 변경되지 않았다. 모델 요청 `S/AIBrain.Loop.cs:1337` BuildJson은 기존 value/description만 명시적으로 내보내므로 새 후보 필드가 wire에 추가되지 않는다. ASM 기록 작성 부분도 변경하지 않았다.

`Tools/core-tests/AssemblerReplayTests.cs:35`의 LegacyEdgeRole은 과거 fixture 입력의 place와 prefix만 보고 role을 복원한다. 문 재설치 → 새 문 → 횃불 우선순위와 null→빈 문자열 처리가 옛 분류와 같다. 이 함수는 시험 코드에만 있고, 기존 ASM-OUT 기대 결과·최종 id/kind/key/value/desc 비교·Doable/BodyCan 조회 비교를 새 결과로 생성하거나 대체하지 않는다. 기존 fixture 파일 수정도 없다. 과거 입력 adapter를 정답 생성기로 사용하는 순환 검증은 발견하지 못했다.

`Tools/core-tests/FixMergeReviewTests.cs:362` 추가 사례는 같은 역할 후보들의 desc를 전부 바꾸어도 최종 key 순서와 cut 분류 문장이 같음을 확인한다. 이어 문장만 '기지 문 다시 달기'인 무태그 후보는 repair가 아니고, desc 없이 DoorRepair를 지정한 후보는 repair임을 직접 검사한다. 기존 EdgeShare·보수 우선·cut 문장 기대값을 유지한다. 과거 L1 replay가 현재 둘레 문 재설치 실장면 전체를 실행해 준다고 확대 해석하지 않는다.

## 검증 범위와 HEAD 반환

- 직접 실행한 `git diff --check 33f477b..bcdec88`는 통과했다. 고정 소스의 diff·참조·실제 소비 경계를 정적으로 확인했다.
- 작업자가 보고한 core 1045 / protocol 106 / 전체 gate 실패 0은 보조 근거다. R2b가 같은 트리를 변경 중이므로 이 검수에서 시험·컴파일·replay를 재실행하지 않았다.
- 보조적인 텍스트 역치환 비교를 시도했으나 기본 python 명령이 없어 실행되지 않았다. 이 시도를 검증 성공 근거에 포함하지 않는다. 위 판정은 직접 diff 대조와 고정 커밋 검색에 근거한다.
- 실게임에서 둘레 보수·문 재설치·횃불 후보가 잘림 전후 올바르게 분류되고 실행되는 장면은 최종 대표 경로 확인에 남는다. 이 정적 검수 결과를 실게임 통과로 올리지 않는다.

R2a 수정 요청 없음. 고정 R2b 커밋의 부서짐 계기·벽 사실 검수를 이어갈 수 있다.
