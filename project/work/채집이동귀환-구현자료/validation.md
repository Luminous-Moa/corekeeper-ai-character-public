# 원정 구현 후보 검증

2026-11-08 최신 상태: [사용자 범위 결정](../2026-11/2026-11-08-채집이동귀환-조명후속분리.md)에 따라 조명 시야 R1은 별도 후속이다. HEAD가 본 소스 C:/Dev/CoreKeeperModSDK의 ai-character를 7af8f91로 통합했고 기존 dirty 파일 해시 보존을 [integration.json](integration.json)에 기록했다. 개발 그래프 정본 반영은 별도 담당 진행 중이며 완료 대기다. 아래 검증 기록은 그대로 재사용하며 제품코드 수정이나 재검증은 하지 않았다.


최종 제품 후보는 **7af8f91e5d26e4fd9ce5996809ae41e213340118**이다. 기준 `5cc5a43`, 격리 작업본 `C:/Dev/worktrees/corekeeper-expedition`, 브랜치 `feature/expedition-20261107`. 2026-11-08 HEAD가 본 소스 ai-character에 같은 코드를 fast-forward 통합했다(integration.json). 설치·게임 실행·외부 모델 호출은 하지 않았다. 조명 시야 R1은 사용자 결정으로 별도 후속에 분리됐다.

|검사|결과|로그|
|---|---|---|
|원정/공통 실행 생산 연결|92/92 PASS|execution-final-v2.log|
|고정 채집 묶음·직접후보·상한 소비|61 PASS|gathering-final-v2.log|
|실제 Mine/Root/Break/Tunnel 입력 경계|14 PASS|native-final.log|
|통로·기억·갈림길 생산 경로|43/43 PASS|movement-tests.log|
|실제 AINavigator 물체 접근/조향/도착|9/9 PASS|movement-navigator-tests.log|
|실제 후보 생성→HasProgress→고갈보고|11/11 PASS|candidate-progress-final.log|
|순수 core 전체 회귀|1418 PASS|core-final-v2.log|
|생활 생산 경계·실제 FarmSkill 수확입력|105 PASS|life-final-v2.log|
|정비 생산 통합 회귀|115 PASS|maintenance-final-v2.log|
|bridge 기존 protocol + 명시상한/소유|188 + 25 PASS|protocol-final-v2.log|
|기존 임의제약 해제 생산 회귀|47 PASS|restriction-final.log|
|설치 게임 DLL 대상 전체 C# compile|경고0/오류0|compile-final-v2.log|
|Unity 전체 스크립트 compile|CompileCheck: ok, exit0|unity-final-v2.log:1814, unity-final-v2-exit.txt|

수정에 영향을 받는 연결을 대상으로 검사했다. 변경되지 않은 이동/Navigator 검사 결과를 재사용했고, 최종 조건 소유/프로토콜 및 공통 Apply 변경 뒤에는 관련 실행/core/생활/정비/bridge와 전체 컴파일을 확인했다. 모든 과거 게임행동을 새로 실게임 실행했다는 뜻은 아니다.

## 대표 생산 경로

`execution-tests/run.ps1`은 현재 생산 Apply/Stale/몸종료/goal lifecycle과 AIExpedition 전체를 사용한다. Planner의 OnGoalEnded/StartNextStep/SetGoalFrom, 실제 다음tick 분기, accepted plan의 carried owner→Bind→Plan.Set→Goal 소비, RequestReplan/Report/AddEvent/pending dispatch를 원본에서 추출한다. AIPlanReport의 events/expedition_observation JSON 구성도 실제 줄을 추출한다. 네트워크 boundary는 `offline://`만 허용하며 실제 만들어진 HTTP body를 검사한다. 외부 호출은 없다.

- 부분 채집120→곁일Wood2→기존 고정원작업 재개→누적220/목표200. 목표달성 관측만으로 body나 원부탁을 자동 종료하지 않는다.
- 실제 finish_goal→생산 OnGoalEnded→다음tick StartNextStep→SetGoalFrom이 Home→Store→Craft를 소비한다. 수동 PlanQueue.Advance로 통합을 대체하지 않는다.
- Gather50/획득20→조기 Home→Store→상위가 지정한 Gather30/기존 tile:20,30. 원목표·획득·부족·장소 기록은 다음 단계 보고에서도 남는다.
- 이탈기회 중복은 pending도 추가하지 않는다. 첫 report가 진행 중일 때 새 기회가 관측되면 동일 body 재선택도 Choosing을 통과하고, pending 후속보고의 실제 JSON/HTTP payload에 새 events와 expedition 사실이 남는다.
- 미래 gather 단계의 사용자 max도 plan 수락 시 즉시 원부탁ID에 등록한다. 앞선 Home/보관, source 변경, 미룸/재개에도 유지한다. 별도 원부탁의 행동/획득에는 전파하지 않는다. 모호한 소유·같은원부탁의 상위계획만으로 max변경은 기존 goal/몸을 유지하며 retry 사유로 반환한다. 원부탁 완료시 해제한다.

실제 묶음 sequence는 gather61, 실제 몸 입력은 native14/FarmSkill 수확경계, 실제 길찾기/조향/도착은 navigator9로 별도 생산 연결했다. 원정 통합 suite의 개별 물리 skill은 통제 fixture이므로 실제 네트워크/충돌/회수/상자 소비 성공을 주장하지 않는다.

## 재현 명령

저장소 루트에서 다음을 사용한다.

```powershell
powershell -File Tools/execution-tests/run.ps1
powershell -File Tools/gather-tests/run.ps1
powershell -File Tools/gather-tests/native/run.ps1
dotnet run --project Tools/travel-tests -c Release
dotnet run --project Tools/travel-tests/navigator -c Release
powershell -File Tools/travel-tests/candidate-progress/run.ps1
dotnet run --project Tools/core-tests -c Release
powershell -File Tools/life-tests/run.ps1
powershell -File Tools/maintenance-tests/run.ps1
node --test Tools/ai-bridge/test/protocol.test.mjs Tools/ai-bridge/test/gather-limit.test.mjs
node Tools/restriction-tests/prepare.mjs
dotnet run --project Tools/restriction-tests -c Release
dotnet build Tools/compile-check -c Release --nologo
```

native `-Baseline`은 e1cedcf의 실제 Mine/Tunnel primary 우회를 재현하는 음성시험이며 게임 런타임 성공시험이 아니다. 수정판은 동일 입력경계에서 추가 파괴입력이 나오지 않는 것을 확인한다.

## Unity와 소스 동일성

실행 명령은 `Unity.exe -batchmode -quit -projectPath C:/Dev/worktrees/corekeeper-expedition -executeMethod HeadlessSdk.CompileCheck -logFile .../unity-final-v2.log`이다. BuildAndInstall은 호출하지 않았다. Unity6000.0.59f2, SDK DLL 읽기복사와 임시 USE_PUG_OTHER define을 기존 승인된 compile-only 절차대로 사용했다.

7af8f91 동결 뒤 제품코드 변경 없이 실행했고 `CompileCheck: ok`, 프로세스 exit0, 정상shutdown을 확인했다. AIPlayerSpike.dll은 2026-11-07 20:55:22, 1,638,912바이트, SHA256 `6908E608063F995AD4DD3D733BD451F7C952E831F2EB25CBDD6C236D135CFD7D`이다.

컴파일한 제품 소스 blob: AIExpedition=`9eeafcbbcc107c0fad068da0d8cd9bc8ab99140e`, AIGathering=`6594acfd363a4f6f67d96dfe9f51b03825d6987c`, AIBrain.Loop=`22c95d2f8d1f36fd483c8faf6893182b793d96bf`. 최종 제품 tracked diff 없음.

Unity가 만든 Packages/ProjectSettings/ConditionsTable 및 .vsconfig 변경은 격리 작업본에서만 복구했다. 미추적 Assets/Plugins/CoreKeeper/는 재현용 SDK 읽기복사이며 제품커밋에 포함하지 않았다. 공식 main의 기존 dirty 설정/자산은 건드리지 않았다.

첫 unity-final.log도 CompileCheck ok였지만 외부 Get-Process 모니터가 exit code를 보존하지 못했고 후속 제품 수정이 있었으므로 최종 성공근거로 사용하지 않는다. 최종은 v2 로그와 exit0만 사용한다.

## 미해결과 인계

R1: AIVision의 기존 카메라 사각형은 빛·어둠/벽 인접 조명 판정을 포함하지 않는다. 현재 게임의 GPU irradiance/호스트카메라 의존과 AI offscreen 렌더 경계를 vision-authority-investigation.md에 조사했다. 2026-11-08 사용자는 조명 시야를 별도 후속으로 분리했다. 이번 행동 구현과 이동지도/묶음채집은 기존 화면 범위 관측으로 수용하며, 조명까지 사람과 동일한 시야를 구현했다고 주장하지 않는다.

독립 재검수 수용은 HEAD/검수자 담당이다. coverage.md 및 graph-handoff.md는 구현 근거 인계이며 독립수용 또는 공식 통합을 대신하지 않는다.
