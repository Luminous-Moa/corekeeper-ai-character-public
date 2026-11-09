# 생활 생산 파이프라인 어댑터 검증

저장소 루트에서 실행합니다.

```powershell
powershell -ExecutionPolicy Bypass -File Tools/life-tests/run.ps1
```

`run.ps1`은 현재 생산 소스를 읽어 `.generated`를 매번 다시 만듭니다. 추출 위치가 바뀌면 실패하며 이전 생성본으로 검증하지 않습니다. 정책 로직을 테스트에 다시 작성하지 않습니다.

- `AIDecisions.cs`의 실제 설정·목표·후보·응답 데이터 클래스
- `AIPlanner.SetGoalFrom`, 전체 `AIGoals.cs`, 실제 `AIPlanProtocol`·`PlanStep`
- `AIBrain.Candidates.cs`의 `AssembleLifePage`·`AddCommonControls`
- `AIBrain.Loop.cs`의 실제 `HandleResponse`·`CallFailed`·`StaleReason`·`Apply`·`ApplyOrHold`·`Switch`·`ObserveExecution`·`CostAllows`
- `AIBrain.cs`의 실제 `SkillRule`부터 `OnSkillEnded`까지
- `AIActions.cs`의 실제 farm dispatch 행·`Of`·`Make`·`Stale`, 실제 `AIRunGate`
- 직접 링크한 전체 `AILife.cs`·`AIFarm.cs`·`FarmSkill`·`AISkill`과 Unity 없는 Core

Unity/ECS·타일·가방·prefab·시야와 정상 `BodyInput`을 소비하는 게임 입력 경계는 `FarmStubs.cs`·`FarmFixture` 어댑터입니다. `BrainStubs.cs`는 네트워크 응답, 움직임 경계, 몸 실행 시작/중단, 관련 없는 기능 모듈을 제공합니다. 몸 실행 시작은 실제 생산 `Apply`→`AIActions.Make`→`FarmSkill.Start`로 연결하며, 스킬의 실제 Tick이 낸 조준·선택 슬롯·secondary 입력을 fixture가 소비하여 ECS 작물 생성과 종자 감소를 일으킵니다. 생산 `OnSkillEnded`는 실제 구조화 결과를 생활 진척과 다음 판단 관측에 연결합니다.

대표 표본은 실제 Protocol→PlanStep→SetGoalFrom→AIGoals로 plant/SeedA/rect/count6을 설정하고, probe와 held/queued를 둔 채 농사→plant→SeedA→필터된 구역을 조회합니다. 일반 및 prefetch 탐색이 몸·목표·대기 선택·실행 카운터를 유지하는지 검사합니다. 말단 실행으로 네 칸의 실제 상태가 바뀌고 확인 작업량4/잔량2, 다음 후보의 부족 사실을 확인합니다. 별도 표본은 count12의 6칸+3칸 실행과 미충족3, 종자·타일 교체의 실행 거절, 작은 여러 종자 스택을 확인합니다.

교차 표본은 개별 칸 페이지·root/close·목록 재표시 revision, 늦은 nav·다른 캐릭터·목표 교체·전투 진입/복귀·API 429/500/401 실패와 직접/트리 payload 동일성을 실행합니다. 다른 생활 모듈의 본 실행은 각 모듈 어댑터가 검증하며 여기서는 농사 대표 경로에 집중합니다.

월 비용 상한은 실제 `CostAllows` 본문으로 상한 직전 허용·도달 시 호출 보류와 30초 재시도, probe·held/queued·목표·실행 카운터 불변을 검사합니다.

상위에서 품목을 생략한 plant/count6 표본은 SeedA 네 칸 뒤 SeedB 후보의 잔량이 두 칸이며 SeedB 두 칸 뒤 양쪽 실행이 닫히는 것을 실제 입력으로 확인합니다. cook put의 second 미지정과 cook take의 출력 미지정은 명시적인 `ILifeResult` 어댑터 결과를 실제 `AILife.Starting`→`AIBrain.OnSkillEnded`에 연결하여 서로 다른 선택이 하나의 목표 수량을 공유하는지 확인합니다. 이 두 요리 표본은 요리 물리 입력을 실행한 표본이 아닙니다. 실제 Protocol→SetGoalFrom→AIGoals에서 소문자 second 식별자의 정규화도 확인합니다.

이 검증은 전체 `CandBuild`의 경로 계산/후보 선별이나 전체 `AIBrain.Tick` 스케줄러를 실행하지 않습니다. 실제 모델/API 호출·실제 Unity 게임 소비자·설치·실게임 검증의 증거가 아닙니다. 실게임 보류가 풀리면 실제 모델 탐색→정상 게임 입력→가방 결과→다음 판단을 별도로 확인해야 합니다.

2026-11-08 선택지 공백 표본(`FarmCases.Gaps`): 작물 없는 판 땅 미리 물주기(ground 필터, 이미 젖은 칸·작물 칸 제외), 괭이 없을 때 공격 입력으로 거두기와 수확 목표 진척(괭이 준비 요구 제거), 씨앗 뿌리개 second 선택·게임이 쓸 첫 종자 확인·범위 심기의 대상 칸 작업 집계를 fixture의 정상 입력 경계로 실행합니다. 씨앗 뿌리개 범위(2x2)는 fixture 가정이며 실제 도구 크기·위치 계산은 실게임 확인 대상입니다.

침대 표본(`BedCases`, 2026-11-08): 생산 `AIBed`·`SleepSkill`을 링크해 생활 루트의 rest 갈래, 빈 침대 선택지와 남이 차지한 침대 사실, 잠자리 이동→게임 경계(ClaimBedSystem 모사)의 차지·Sleep 상태→체력 가득 종료를 확인합니다. 실제 침대 충돌·잠자리 오프셋·주변 적 판정은 실게임 확인 대상입니다.
