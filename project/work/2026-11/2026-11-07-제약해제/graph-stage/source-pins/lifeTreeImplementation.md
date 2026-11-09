# 생활 선택 트리 첫 단위 구현 결과

## 고정판과 반영 상태

- 격리 작업트리: `C:/Dev/CoreKeeperModSDK-life-tree`, 브랜치 `life-tree-20261107`.
- 고정 기반: `1fc89b93edbee31a9001a174581e32568119efc1`. 공식 `685dde4d27dd91a782a14d8ae33fb576160fa578`가 조상인 것을 확인했다.
- 최종 생산 후보: `c4a0f5e38016c79567f880bbaea5e56e0eeae347`. 중간 생산 후보 `24c61c246053237784c057cf97282b9ca0c5d564`에 독립 지적 R1~R3와 직접 영향 회귀를 더한 커밋이다.
- `git show --stat`로 기반의 상자 실제 여유 공간 수정과 조상 관계를 확인했다. 작업 시작 때 지정 경로/브랜치가 없어서 고정 SHA로 새 worktree를 만들었다. 공식 코드와 다른 담당 worktree를 수정하지 않았다.
- 입력 문서 다섯 개의 SHA256은 [input-hashes.json](logs/input-hashes.json)에 기록했다. 별도 작업 기록 도구에 쓰지 않았다.

## 구현 책임과 실제 연결

`Core/AILifeTree.cs`가 Unity 없는 커서·불변 nav 토큰·selector 형식·run 중복 방지 진척을 제공한다. `AILife.cs`는 `AISlot.LifeTree`의 캐릭터별 임시 상태를 사용한다. 목표 교체/종료/입장 초기화에서 경로와 진척이 초기화되며, 탐색을 몸 실행이나 새 목표로 취급하지 않는다.

`AIBrain.Candidates.AssembleLifePage`는 기존 Finish가 부여한 값과 직접 지정 후보를 보존하면서 한 깊이의 ordinal 정렬 목록을 5개씩 조립한다. back/root/close/prev/next와 기존 공통 stop_action/finish_goal/request_plan은 일반 14개 cut 밖에 둔다. 전투 중에는 생활 목록과 실행을 숨기고 커서를 보존한다. `HandleResponse`는 일반 request revision과 nav 토큰을 검사한 뒤 prefetch queue/ApplyOrHold보다 먼저 조회 응답을 소비한다. 조회는 몸·held/queued·목표와 실행 카운터를 유지한다.

각 모듈의 `LifeEntries`/`ExactCandidate`/`Make`/`Stale`가 명시 선택과 요청 시점의 실제 실행 payload를 연결한다. AIActions와 skill.Start가 선택 시설·슬롯·재료 variation·칸 상태를 다시 확인하며 다른 대상으로 대체하지 않는다.

- 농사: mode→종자/작물→자기 영역/연결구역/특정 칸→실행. 설치·작물 연결이 확인된 종자, 정확 variation과 고정 source 스택, 선택 도구와 실제 칸 복사본을 쓴다. 한 실행은 최대 6칸이고 전체 요청량의 상한이 아니다. 실제 바뀐 칸과 main 가방 증가를 별도로 보고한다.
- 요리: mode→순서와 variation이 있는 재료→자기 솥→투입/수령. BestPair 외 유효한 짝과 둘째 솥을 선택할 수 있다. 둘째 재료 예측은 현재 페이지 항목에서 지연 평가한다. 같은 재료의 분리 스택을 고정할 수 있고 전체 재고 가능량과 이번 고정 source 묶음량을 구별한다. 한 투입 묶음은 최대 5짝이다. 입력별 실제 투입·자동 생산·main 수령·솥/커서 잔류를 구별한다.
- 제작: 알려진 품목→손/실제·기억한 제작대→실행 또는 가서 확인. 실제 runtime recipe 출력량으로 횟수와 초과량을 계산하고 소비·station·재료 stamp를 고정한다. 자동 가공은 일반 Craft에서 제외한다. 커서에 출력이 남으면 가방 확보 완료로 보고하지 않는다. Prep도 같은 실제 생산 입구를 사용한다.
- 보관/꺼내기: store/take→자기 상자→inspect→전체 실제 source 슬롯→정확 이동. 첫 12개 밖 슬롯도 페이지로 접근한다. 기존 InventorySkill의 정상 MoveAmount, 정확 슬롯 검증과 실제 receipt를 재사용한다. 실제 선택 슬롯 품목을 해석한 뒤 원래 goal selector와 재비교한다.

`mode`/`second`와 기존 count/where는 실제 Protocol→PlanStep→AIPlanner→AIGoals 경로로 전달된다. 미지정 selector를 현장에서 좁혀도 하나의 상위 count를 공유하며, 명시 selector와 충돌하는 별도 행동은 1개/1칸/1회이고 원래 목표 진척에 합산하지 않는다. 결과는 `ILifeResult`의 RequestedWork/ConfirmedWork/ConfirmedReceived/ResultStage를 생산 OnSkillEnded에서 읽는다. 같은 run은 한 번만 집계한다. BuildInput/goal_now/생활 보고와 bridge brief도 갱신했다.

## 실제 검증

| 검증 | 결과 | 로그 |
|---|---|---|
| Core 전체 회귀·공유 selector fixture·커서/진척 | 1,295 통과 | [core-tests.log](core-tests.log) |
| bridge protocol | 154 통과 | [bridge-protocol.log](bridge-protocol.log) |
| 기존 공통 실행 실제 입구 | 36/36 통과 | [execution-tests.log](execution-tests.log) |
| 생활 대표 생산 파이프라인·교차 경계 | 96/0 통과 | [life-tests.log](life-tests.log) |
| 생산 요리 모듈 | 51/0 통과 | [life-cook-tests.log](life-cook-tests.log) |
| 생산 제작·실제 Prep | 19/0 통과 | [life-craft-tests.log](life-craft-tests.log) |
| 생산 상자·기존 가방 어댑터 | 91/0 통과 | [life-inventory-tests.log](life-inventory-tests.log) |
| fast compile | 최종판 경고 0 / 오류 0 | [compile-check.log](compile-check.log) |
| Unity 6000.0.59f2 CompileCheck | 최종 SHA 입력 exit 0 / compiler error 0 / CompileCheck:ok 1건(1814행) | [unity.log](unity.log), [unity-exit.json](logs/unity-exit.json), [unity-summary.json](logs/unity-summary.json) |

명령은 저장소 루트에서 실행한다.

```powershell
dotnet build Tools/compile-check -c Release
dotnet run --project Tools/core-tests -c Release
pwsh -NoProfile -File Tools/execution-tests/run.ps1
node Tools/ai-bridge/test/protocol.test.mjs
powershell -ExecutionPolicy Bypass -File Tools/life-tests/run.ps1
dotnet run --project Tools/life-cook-tests -c Release
& Tools/life-craft-tests/ExtractLifeBinding.ps1
dotnet run --project Tools/life-craft-tests -c Release
& Tools/life-inventory-tests/ExtractBase.ps1
& Tools/life-inventory-tests/ExtractLifeBinding.ps1
dotnet run --project Tools/life-inventory-tests -c Release
```

대표 생활 어댑터는 실제 AIPlanProtocol→PlanStep→생산 SetGoalFrom/AIGoals→생산 후보 조립 입구/HandleResponse/ApplyOrHold/Apply→AIActions.Make→FarmSkill.Start/Tick→정상 BodyInput→ECS 작물 변화→생산 OnSkillEnded를 실행했다. 요청6/관측 유효4에서 실제 입력4와 확인 작업량4/잔량2, 요청12에서6+3/미충족3을 확인했다. probe/held/queued 보존, prefetch nav, 페이지/특정 칸/back/root/close, 늦은 답/캐릭터·목표 교체/전투 진입·복귀/슬롯 교체, API 실패와 월 비용 한도에서 현재 실행 보존을 확인했다. item 미지정 plant6은 A4+B2가 같은 수량을 공유하며 명시 SeedA 목표와 다른 B1은 별도 행동으로 남는 회귀를 포함한다.

독립 검토 중 지적한 세 경로를 최종 후보에서 수정하고 직접 영향 회귀를 실행했다.

- R1: 상위에서 미지정한 item/second/시설·구역·슬롯은 goal의 wildcard로 정규화한다. A4+B2는 하나의 plant6을 공유하며 종료 run 중복은 세지 않는다. 실제 AILife에 넣은 구조화 결과로 요리의 미지정 second/출력 종류도 공유 수량을 확인했다. 후자의 표본은 요리 물리 입력 실행과 구별하며, 실제 CookSkill 입출력은 별도 51개 표본이 검증한다.
- R2: 실제 source 슬롯의 ID/variation/slot을 읽은 뒤 생산 Resolve로 goal 일치와 잔량을 다시 판단한다. Wood3 목표 중 Ore 슬롯은 별도1/goal null이고 실제 Ore receipt는 Wood 진척0, 이후 Wood3 receipt만 진척3이다. 다른 품목을 숨기지 않는다. 원래 base 범위 밖 자기 상자도 별도 행동으로 구분한다. 이 검증은 생산 AILife의 lifecycle/Matches/Bind/Resolve/Remaining prefix를 그대로 추출하여 사용한다.
- R3: 실제 Prep 전체를 링크해 출력2/품목3 요청/Wood2에서 tree/direct가 동일한2회·비용2의 payload를 만들고, Wood1일 때 실제 부족1을 보고하는 것을 확인했다. 실행 불가인 재료 부족도 확인된 runtime recipe 메타데이터를 보존해 준비 사실에 재사용한다. 규칙 미확인 조건을 실행 가능으로 승격하지 않는다.

추가 자기 검증에서 같은 요리 재료1+1 스택을 두 정확한 출발 슬롯으로 고정하는 표본, 현재 variant 전체 재고 가능량과 이번 고정 source 묶음량을 구별하는 표본을 추가했다. Craft의 시작/현재 수량 관측은 main 가방으로 맞췄고, 소문자/별칭으로 유효 검사된 요리 second는 실제 ObjectID 이름으로 정규화한다. 월 비용 상한의 기존 분기는 동등하게 `CostAllows`로 분리해 원래 retry 지연과 몸·held/queued·목표 보존을 직접 실행했다. 이 분리는 새 비용 정책을 추가하지 않는다.

Unity는 다른 배치가 없는 것을 확인한 뒤 이번 작업이 연 PID의 command line을 기록하고 `-batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK-life-tree -executeMethod HeadlessSdk.CompileCheck`만 실행했다. 최종 배치 PID41772는 sourceCommit `c4a0f5e38016c79567f880bbaea5e56e0eeae347`로 시작해 자연 종료했으며, 종료코드0과 compiler error0, CompileCheck:ok를 함께 확인했다. 시작 인수/시각은 [unity-start.json](logs/unity-start.json)에 있다. 기존 성공 환경의 SDK assembly/USE_PUG_OTHER 및 Unity 환경 설정 사본을 격리 경로에 준비했다. 환경 파일은 제품 커밋에서 제외했다. 첫 성공 배치 PID44156의 로그·exit는 logs/unity-initial*에 보존했다. headless entitlement 진단은 이전 가방 성공 로그에도 존재하며, 실제 성공 종료/CompileCheck:ok/컴파일 오류 없음과 구별했다. BuildAndInstall은 실행하지 않았다.

## 검증의 경계와 남은 단위

어댑터의 World/ECS·DB·가방·UI 요청 큐·입력 소비·주변 기능은 통제된 경계다. 생산 정책과 응답 분기를 테스트에 복제하지 않고 현재 소스를 직접 링크하거나 본문을 추출한다. 대표 생활 harness는 전체 CandBuild의 경로 탐색/후보 선별 및 전체 AIBrain.Tick 스케줄러를 실행하지 않는다. 요리·제작·상자 표본의 결과 buffer 변화는 fixture가 공급한다. 비용 상한은 기존 Tick 분기를 동등하게 분리한 생산 CostAllows 본문을 실행한 증거다.

이 결과는 실제 모델/API 호출이나 실제 Unity 게임 소비자의 성공, 설치·실게임·배포 완료를 뜻하지 않는다. 실게임 보류를 유지한다. 정식 설치, 기존 게임·기존 Unity 프로세스 종료를 하지 않았다. 공식 코드·정식 그래프에 반영하지 않았으며 generated graph.json도 수정하지 않았다. 후보·검증 결과의 저장과 독립 검수/정식 반영 판단은 HEAD가 수행한다.

수리·자동 가공/제련·강화·거래·낚시의 새 실행은 이번 단위에 포함하지 않았고 지원 가능한 말단으로 노출하지 않는다. 첫 네 갈래 통합을 전체 시설 생활 완료로 보고하지 않는다. 실게임 보류 해제 뒤 실제 모델의 탐색→실제 입력/소비→가방 결과→다음 판단은 별도 확인이 필요하다.
