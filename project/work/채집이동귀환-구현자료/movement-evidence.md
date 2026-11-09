# 이동 구현과 근거

격리 작업본: `C:/Dev/worktrees/corekeeper-expedition`, 시작 `5cc5a43`.

## 구현

- `AITravel.Compose`는 보이는 통로 방향을 직접 후보로 만들며 기존 임의 목적점 탐험 후보를 대체한다. 상위/원정 분류 트리와 후보 등록은 부모 통합 담당이다.
- `TravelSkill`은 선택한 통로 의도를 유지한다. 외길에서는 모퉁이마다 새 모델 응답 없이 다음 보이는 칸을 이동하고, 다음 갈림길에는 방향 후보를 미리 제시한다. `travel_turn`은 같은 skill/run에 방향을 예약하며 몸을 교체하지 않는다.
- 선행 범위는 실제 호출측에서 전달하는 응답 지연과 측정 이동속도의 곱이다. 선행 읽기는 다음 갈림길의 방향만 반환하며 통행 지도·자원 기억·일반 관측에는 저장하지 않는다.
- 갈림길 도착 때 현재 가시 출구 집합과 예약 토큰을 다시 비교한다. 변경되면 예약을 버리고 현재 선택을 다시 요청한다. 응답이 늦으면 공통 구간 끝에서 같은 의도를 유지한다.
- 막힘/실패는 현재 통로의 관측으로 보고한다. 임의 벽파기·다리 생성·대체 출구 선택을 실행하지 않는다. 재출발/되돌아가기와 다른 기존 행동 후보를 모델이 선택할 수 있다.
- `AITravel`의 캐릭터 slot 20은 월드별 정확한 가시 통행 지도를 저장한다. `travel-memory.json`은 그 캐릭터/월드 폴더에 저장된다. 현재 시야에서만 재관측하며 밖의 변경은 마지막 관측값으로 유지한다. 예측 분기 조회는 이 지도에 기록하지 않는다.
- `AINavigator`의 실행 경로는 위 관측/기억 지도의 유한 BFS를 사용한다. 알려진 목적지의 갈림길은 재질문하지 않는다. 오래된 기억은 접근 시 재관측하고 경로를 다시 구한다. 전역 game pathfinder의 미지 우회 선택을 사용하지 않는다. 실제 문 통과는 기존 open/pass/close 실행을 재사용한다.

## 실제 행동 그래프 조회

`work/게임행동-그래프/graph.py`의 게시된 행동판단 기본뷰에서 다음을 직접 조회했다.

- `find WoodDoor` → `show door`: 문 variation별 `toggle_door`/입력/조건 관계 확인. 전체 응답은 [movement-door-query.json](movement-door-query.json). 따라서 문은 단순한 영구 벽으로 단정하지 않고 기존 문 사용 실행의 조건·실제 결과를 유지한다.
- `find WoodBridge` → `show completion:B:place-tile:CAB-2ebcba97367158c9b08f790c25d642c9:-1592303294285069928`: ObjectID 4703, variation 0, needed pit/water와 invalid ground, 배치 검사·재고 소비와 실제 타일 결과가 별개임을 확인했다. 전체 응답은 [movement-bridge-query.json](movement-bridge-query.json). 이 근거는 막힘만으로 다리 성공을 가정하거나 자동 생성하지 않는 실행 경계를 지지한다. 다리 후보의 실제 실행 가능성은 해당 배치 기능이 검증해야 한다.

## 검증

- `dotnet run --project Tools/travel-tests -c Release`: 실제 AITravel/TravelSkill/GotoSkill/AISkill/Core 규칙을 연결한 오프라인 43개 검증 통과. 폭 2 외길·열린 방의 매칸 분기 제거, 폭 3 코너 조향, 넓은 십자교차 3방향 유지, 고리 동일방향 재진입 정지 및 원래 목적 보존, Gather 목표의 새 탐험 progress 제외를 추가 검증했다. 방향 선요청과 조기 예약, 코너 연속 이동, 늦은 응답 경계 정지, 바뀐 출구 대체 선택 금지, 이전 run 거절, 막다른 길 되돌아가기, 가시·마지막 관측·미지 구별, 캐릭터/월드 격리·저장 복원·월드 전환 직전 최근 관측 저장, 목적지의 미지/마지막 관측 구별, 재관측 막힘과 채굴 후 개방, 599칸 알려진 경로, 응답 지연에 비례하는 선행 범위.
- `dotnet build Tools/compile-check -c Release`: 전체 생산 C#을 설치 게임 어셈블리에 대해 컴파일, 0 warning / 0 error.
- 오프라인 fixture는 실제 Unity 물리/네트워크 입력과 외부 판단모델을 실행하지 않는다. 스크립트의 물리 navigator는 통제된 경계다. 별도 navigator fixture는 실제 AINavigator.Start/Tick/경로준비/조향/도착까지 연결한다. 실제 Unity 물리와 문 입력은 후속 실게임 통합검증 대상이다.

## 명시한 한계

기존 `AIMemory`의 4×4 seen 표시는 정확한 통행 상태를 보존하지 않았으므로 새 지도에 없는 과거 길을 사실로 복원하지 않는다. 구세이브는 해당 길을 실제로 다시 관측해야 하며 목적지가 기억돼 있어도 통행 경로가 미지이면 이를 보고한다. 통로 분기는 진행 방향의 횡단 폭과 새로 열리는 측면 통로로 판정한다. 열린 방은 선택한 방향을 유지하며, 폭이 같은 길의 옆 칸은 별도 분기로 만들지 않는다. 일반 동굴 형상의 최종 체감 검증은 실게임 보류 중이다. 빛·어둠 미반영은 합의상 미해결 상태이며 완료에서 제외한 것이 아니다. vision-authority-investigation.md에 게임 GPU irradiance·벽 인접 광도 판정과 3개 아키텍처 대안을 기록하고 HEAD 결정 대기 중이다.

## 독립 검수 후 이동 보완

- `PassageExits`는 통로 폭 안의 옆 칸과 새로운 측면 통로를 구별한다. 고유 코너는 계속 조향하고, 넓은 교차에서는 진짜 선택 가능한 방향들을 유지한다.
- 같은 실행에서 실제로 지나온 유향 간선을 다시 밟으려 하면 멈추고 원래 목적을 유지한 채 이미 돈 경로 사실을 현장 판단에 보낸다. 자동 목적 변경은 없다.
- `progress`는 새 탐험/재출발/되돌아가기에서 상위 Explore 목적일 때만 true다. 현재 실행의 예약 방향인 travel_turn은 원실행 이어짐으로 유지한다.
- R7: 물체 목표칸은 기억맵에서 계속 false다. `KnownPath`는 실제 호출 tolerance 안의 관측된 열린 접근칸을 종점으로 삼으며, 목표 전 중간 벽/대각 모서리를 통과하는 접근은 허용하지 않는다. 도착 판정도 같은 접근면 조건을 확인한다.
- `dotnet run --project Tools/travel-tests/navigator -c Release`: 실제 AITravel와 AINavigator를 함께 연결해 Store/Take/Break 접근 및 벽/대각모서리 회귀 9/9 통과. 대상물까지 직접 걸을 수 있다고 위조하지 않는다.

## R5 재검수: 일반 탐험과 실제 목표 대안 구별

- `AIBrain.Candidates.Explore`: lExplore의 `progress=wantExplore`와 lDigExplore의 무조건 true를 모두 `Goal.kind == GoalKind.Explore`로 수정. wantExplore는 후보 가용성=true를 그대로 유지하므로 Gather/Home/Goto에서도 탐험 후보는 삭제되지 않는다.
- 대조한 실제 목표 대안: 보이는 벽 `isMatch && est > red`, 보이는 파기 대상 `gatherOre && wanted.Contains`, 물체 접근/파괴 `b.prog`, 뿌리와 기억 광석 `gatherOre && wanted.Contains`, 아이템/아이템 접근 `isMatch`는 유지한다. 기억한 목표 광석은 이미 알려진 채집 대안이며 일반 탐험과 섞지 않는다.
- follow는 실제 Follow 목적+호스트 일치. 호스트 기억위치의 dig true는 `free == GoalKind.Free` 블록 전용이므로 Gather/Home/Goto의 고갈보고 경로에 들어오지 않는다. continue는 선택된 기존 실행의 진척 맥락을 사용한다. 이번 수정에서 이들을 변경하지 않는다.
- `candidate-progress/run.ps1`는 현 소스의 일반걷기/기억탐색끝/기억광석 initializer(설명문 이전 실제 필드)와 Loop.HasProgress 판정을 추출해 컴파일한다. 실제 AIGoals.CheckAfterBuild→AIExpedition.AfterBuild까지 연결한 11/11 검사 통과. 일반 탐험이 보고를 막지 않음, 보고 뒤 후보/목적 유지, 목표 광석 기억이 있으면 거짓 고갈보고 없음, 다른 재료 기억만 있으면 보고됨을 확인했다.
- 전체 생산 C# compile 0 warning / 0 error, Travel 회귀 43/43 통과. 참조 execution harness의 신규 보고 2개 테스트는 부모가 별도 수정 중인 78/80 상태였고 이 검사 결과를 전체 통과로 바꾸어 기록하지 않았다.
