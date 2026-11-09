# 게임 메인 스레드 경로 독립 조사

2026-11-08. 읽기 전용 조사. 빌드·게임 조작·설치·설정 변경 없음. 소스 HEAD `7af8f91e5d26e4fd9ce5996809ae41e213340118`. C:/Dev/AGENTS.md의 과거 모델·추론강도 철회를 적용했다. 상위 지시와 HEAD start-job을 읽었으며 기존 `2026-11-06-stutter-diagnosis.md`를 재사용했다.

## 핵심 판단

사용자가 말한 화면 전체와 본인 캐릭터의 순간 멈춤을 설명할 우선 경로는 **게임 Update에서 수행하는 로컬 관측·후보 준비·길찾기·상위 보고서 작성**이다. 현행 코드는 HTTP 응답 자체를 동기 대기하지 않는다. 인터넷/모델 응답 지연과 프레임을 막는 준비 작업을 구분해야 한다.

이 문서는 코드 구조를 확인한 결과이며 사용자 현재 장면의 각 스톨 원인을 새로 재현한 것은 아니다. 과거 실측과 다른 작업의 로그를 현재 설치판·현재 사용자 장면의 실측으로 혼용하지 않는다. 담당 runtime 조사자가 보존한 `buildMs=434.5` 등 최근 로그와 본 문서의 동기 보고서 경로를 함께 판단할 수 있다.

Unity는 **6000.0.59f2 (ef281c76c3c1)**다. SDK `ProjectSettings/ProjectVersion.txt`와 처음 읽은 게임 Player.log 7행이 일치했다. 2022.3로 가정하면 안 된다.

## 관측 사실: 호출 경로와 측정 범위

아래 소스 경로의 공통 루트는 `C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/`이다.

| 위치 | 사실 / 빈도 | 프레임 비용과 로그 해석 |
|---|---|---|
| AIPlayerSpikeMod.cs:226–260 | Update가 모든 AI를 foreach로 순차 Tick. 각 Tick 전체 Stopwatch, 그 뒤 정보 UI/채팅/성능 집계 | 여러 AI의 긴 작업이 같은 프레임에 합산될 수 있음. 개별 AI Tick 통계에는 foreach 뒤 정보 UI 비용이 빠짐 |
| AIPlayerSpikeMod.cs:971–990 | Walking 단계가 60초 주기 캐릭터 저장 후 AIBrain.Tick과 AIBody.Tick 동기 실행 | 프레임 갱신과 같은 호출 흐름 |
| AIBrain.Loop.cs:663–718 | Send가 AIMemory.Observe(force=true), AIObservation.Take(true), BuildCandidates, BuildInput, BuildJson을 모두 마친 후 prepMs 읽음 | prep에 원격 응답 대기 없음. 실패/후보부족/goal상태로 688–711에서 조기 반환하면 SEND/prep 로그가 남지 않음 |
| AIBrain.Loop.cs:744–766 | prep 측정 종료 뒤 FULL/시험/SEND 문자열과 로그 생성, 그 뒤 HTTP 전송 | 로그 비용·UTF8 업로드 생성은 prepMs 바깥. prepMs는 전체 해당 프레임 비용의 상한이 아님 |
| AIDecisionsApi.cs:33–42, AIBrain.Loop.cs:133–151 | SendWebRequest 후 반환, 다음 프레임에서 isDone 확인. 미완료이면 return | `.Wait`, `.Result`, 응답까지 busy-loop하는 경로가 아님. SendWebRequest 시작 자체와 UTF8 복사 비용까지 0이라고 단정하지 않음 |
| AIBrain.Loop.cs:776–804, AIDecisionsApi.cs:47–74,167–179 | 완료된 응답 텍스트 복사/JSON 파싱 후 매 성공 응답마다 usage JSON 전체 동기 File.WriteAllText | 응답 시점의 별도 짧은 스톨 후보. 파일 크기는 작을 수 있으나 현재 지연 ms·GC 실측 없음 |
| AIBody.cs:274–276 → AIPlanner.cs:408–442 | 메모리/상위 플래너도 body Tick 안. ReportJson을 동기 작성한 뒤 request 생성, UTF8 변환, SendWebRequest | `buildMs`는 ReportJson만 포함하고 이후 body wrapping/업로드·전체 보고서 로그 문자열 쓰기는 제외 |
| AIPlanner.cs:447–489 | 상위 요청도 isDone을 확인하고 미완료이면 return | 브리지/상위 LLM이 생각하는 시간 자체는 게임 스레드 동기 대기 아님 |

하위 AI는 실행 중 0.5초 전제 관측, idle시 0.25초 적 검사, 전투시 설정된 최소 간격·호출한도·응답완료에 따라 연속 재판단한다(AIBrain.Loop.cs:178–242). 캐릭터당 한 요청만 전송 중이지만 **서로 다른 캐릭터의 CPU 준비를 나누는 전역 스케줄러는 이 흐름에 없다**. frontier `_fJob.Step(FrontierTilesPerFrame)`는 여러 프레임에 나누지만 메인 Tick에서 직접 호출하므로 주석의 “background”는 worker thread를 뜻하지 않는다(42–64).

## 관측 사실: 길찾기는 현행판에도 한 프레임 동기 실행

`AIBrain.Loop.cs:1092–1127 PlanC`는 결과/실패 캐시를 조회하고 miss면 1121에서 `AITunnel.Plan`을 직접 실행한다. 평화 성공 캐시 5초, 전투 2초, 실패 10초 및 이동거리 조건이 이미 있다. 과거 기각된 “피해 변화가 계속 캐시를 비움” 가설을 재제시하지 않는다.

`AITunnel.cs:243–349`는 장애물 스캔, 도구 피해·타일값 조회와 Dictionary/HashSet 생성 후 `FindWithDetour`를 호출한다(307). 검색 cost 콜백은 AITileReader/EntityManager, 현재 시야, 기억, 문·기지 경계 상태에 직접 접근한다. 순수 숫자 배열만 읽는 worker 함수 상태가 아니다.

`Core/AITunnelSearch.cs:26–63`는 heap+g/came Dictionary를 쓰며 최대 **120,000 heap pop** guard 동안 동기 while한다. 타일마다 4방향 비용 콜백을 평가하므로 120,000은 ms 제한도 방문타일 수 보장도 아니다. 경계 회피/우회는 첫 검색 뒤 두 번째 검색을 실행할 수 있다(95–119). 현행 검색의 이전 902721e 대비 차이는 정수 좌표 overflow 경계와 주석 정도이며 프레임 분할/worker화는 도입되지 않았다.

후보 준비는 한 요청 안에서 여러 PlanC를 부른다: 적240, 귀환285, 보이는 광석379, 부술 것444, 기억 광석497, 아이템539, 동료572, 탐색693, 최종 위험 설명1162(AIBrain.Candidates.cs). 새로 선택한 dig 스킬의 시작·재계획도 별도로 AITunnel.Plan을 직접 호출한다(AITunnel.cs:393,429). 따라서 후보 생성만 나눠도 실행 시작의 길찾기를 놓치면 잔여 스파이크가 남을 수 있다.

과거 실측의 준비 100–320ms/먼 길 20–30ms는 `2026-11-06-stutter-diagnosis.md`에 출처가 정리되어 있다. 현행 소스에도 같은 동기 구조가 있지만 그 숫자를 현행판의 재현값으로 재사용하지 않는다.

## 관측 사실: ECS/할당/직렬화 부하

`AIObservation.Take`는 매 호출마다 enemy / pickup / collider object 쿼리를 새로 만들고 ToEntityArray(Allocator.Temp) 후 월드의 일치 엔티티 전체를 순회하며 **그 안에서** 시야를 필터링한다(AIObservation.cs:78–129). 결과가 시야 안으로 제한되어도 입력 스캔량은 월드 쿼리 매칭수다. 각각 결과 sort와 managed List/SeenEnemy 할당이 있다. Native Temp 배열 자체를 managed GC 할당이라고 잘못 부르면 안 된다.

withTiles=true이면 N개 보이는 타일마다 Layers→HasWall→(비벽이면 Walkable)로 타일 층을 2~3회 읽는다(54–75). AITileReader.Layers는 매번 `new List<TileCD>(4)`를 만든다(AITileReader.cs:37–54). 따라서 관측 하나만으로 2N~3N List와 backing array 생성이 가능하다. fallback 시야 31×17=527 타일이 모두 조건을 통과하는 예에서는 1,054~1,581회의 Layers List 생성이며, **현재 실제 카메라 시야/실할당량을 측정한 값은 아니다**. 직전 force memory 관측도 같은 시야의 HasWall을 한 차례 더 읽는다(AIMemory.cs:242–293). 이는 반복 타일 접근과 GC 압력의 코드 근거이며 GC pause가 실제 원인이라는 측정 근거는 아직 없다.

현재 SDK가 포함한 Entities 내부 구현도 구분해서 읽었다.

- `Packages/com.unity.entities/Unity.Entities/Iterators/EntityQuery.cs:967–981`: ToEntityArray가 CalculateEntityCount와 Entity 쓰기 dependency 완료 후 배열 복사.
- `.../EntityDataAccess.cs:1232–1245`: GetComponentData가 해당 component의 CompleteWriteDependency.
- 같은 파일2605–2627: GetBuffer(readonly=true)는 writer 완료, 기본 readwrite는 reader/writer 완료.

즉 CPU 스캔 자체 외에 **Unity jobs 완료를 메인 스레드가 기다리는 시간**이 끼어들 수 있다. 쿼리 하나가 무조건 모든 jobs를 기다린다는 뜻은 아니며, 실제 jobs 대기시간은 Profiler marker 없이 확정할 수 없다. 게임 로드 Entities DLL과 SDK package 바이너리 동일성까지 이번 조사에서 입증하지 않았으므로 API 구현 근거의 적용 범위를 유지한다.

## 현행판에서 커진 추가 범위

이전 진단 HEAD 902721e와 현행 HEAD 사이 Scripts는 117파일/8,448삽입/1,088삭제로 차이가 크다. 과거 “이후 리팩토링은 동작 변화 없음”만으로 현행판을 설명할 수 없다.

- `AIPlanReport.cs:24–121`: 전체 inventory, 알려진 모든 gather 아이템, 모든 다운 이력, maintenance/expedition/실행/생활 상태, inventory_actions가 보고서에 포함된다.
- `AIPlanReport.cs:124–125`: 이전 CraftMax40/CraftChars1800/PlaceChars400가 모두 int.MaxValue로 바뀌었다. 판단 내용 보존 요구의 일부로 읽어야 하므로 cap 복원/정보 삭제를 성능 해법으로 임의 적용하지 않는다.
- `AIPlanReport.cs:71–81`: 손과 모든 보이는 제작대의 모든 레시피마다 Materials를 평가한다. Materials는 매번 InventoryCounts를 다시 만든다(AICraft.cs:247–258). InventoryCounts는 가방 전체 buffer를 순회하고 항목마다 ObjectInfo를 조회한다(AIBody.cs:784–799). R개 recipe와 I개 slot이면 이 부분에 **R×I 반복 가방 조회/Dictionary 생성**이 있다. 레시피별 재료 종류와 중복 제작대도 추가된다. 같은 report의 inv는 28행에서 이미 읽었다.
- `AIPlanReport.cs:92–112`: can_do/기지/가방/음식/농사/요리/제작대/경험 보고가 여러 feature의 관측을 연달아 수행한다. 구간별 타이머가 없어 434ms를 특정 한 줄로 귀속할 수 없다.
- 현행 정보패널은 Visible+선택된 캐릭터가 있을 때 0.5초마다 읽는다(AIInfoPanel.cs:49–64). AIInfoSnapshot은 inventory/equipment와 client player scan을 한다. 상위 모델 호출은 없고 전체 AI를 매번 스캔하지도 않는다. 대규모 보고서 경로보다 먼저 원인 확정할 근거 없음.

## 우선 해결책과 검증법 — 구현 제안이며 이번에 적용하지 않음

1. **frame ID 기반 최소 계측을 먼저 붙여 병목을 분리한다.** 전체 Update, AI별 brain/body, 관측/후보/PlanC 검색, report의 각 feature, 업로드 시작, 응답 파싱/usage 저장, memory/character 저장을 같은 frame ID와 연결한다. GC.Alloc/GC.Collection 및 jobs wait와 main thread CPU를 Profiler로 분리한다. SEND 조기반환도 finally에서 기록해야 한다. 기존 PERF는 30초 집계라 frame max와 Tick max의 일치 시점을 입증하지 못한다(AIAgents.cs:186–211).
2. **전체 AI 합산 메인 스레드 예산을 둔 단계형 준비가 일차 수정 경계다.** ReportJson 전체 한 프레임 생성과 BuildCandidates/PlanC를 취소 가능한 state machine으로 나눈다. 후보/기억/보고내용을 유지하고 완성된 전체 결과만 전송한다. 개별 함수가 한 번에 20ms이면 그 함수를 호출하는 loop만 쪼개서는 부족하다. 순수 Find while도 continuation 가능한 검색 job으로 분할해야 한다. 초기 2~4ms/프레임 예산은 실험 시작 제안일 뿐 실제 성능 보장값이 아니다. 원래 body 입력/안전 반응은 계속 갱신하고, 준비 중 목표·월드·사망·주문 revision 변화를 검증해 오래된 작업을 버린다.
3. **중복 읽기를 같은 관측 단위로 통합한다.** report 안의 InventoryCounts 1회, 타일층 1회, station/현재 관측 1회를 각 feature가 공유하도록 하면 R×I를 R+I에 가깝게 줄일 수 있다(재료 처리비용은 별도). Layers의 per-tile managed List를 호출자 buffer/구조체 열거로 대체한다. 재료·소유권·문 상태·상자내용의 정확성이 달라지지 않아야 하며 실행 직전 stale 검증은 남긴다. 효과 ms는 계측 후 판단.
4. **worker는 스냅샷 경계를 만든 뒤 적용한다.** 현재 AITunnel.Plan 전체를 Task.Run으로 감싸면 Unity 접근과 per-agent 전역 상태가 worker로 넘어간다. 메인에서 관측/기억/장애물/문/경계/도구 비용을 immutable 숫자 스냅샷으로 준비하고 순수 FindWithDetour/후보 설명·직렬화만 worker에서 처리하는 방식이 적절하다. 여러 목적지에 하나의 snapshot을 공유할 수 있다. snapshot 복사량도 큰 월드에서 프레임 예산에 넣어야 하므로 worker만 도입해도 스톨이 자동 해결되지는 않는다.
5. **응답/저장 비용은 후순위로 분리한다.** 성공마다 동기 usage 쓰기를 순서 보장된 writer queue로 옮기거나 묶고, memory/경험/character 저장도 detached 데이터 후 비동기 파일 저장을 검토한다. 종료 flush/오류/월드별 분리/유실 방지 요구가 따른다. 현재 지연 원인으로 측정되지 않았으므로 보고서·검색보다 먼저 단정하지 않는다.

검증은 동일 실제 월드·카메라·대표 행동에서 AI on/off와 인원별 표본을 비교한다. 평균FPS 외 frame p95/p99/max, 16.7/25/33/50ms 초과 횟수, main thread 모드 max, 한 요청 총 준비 wall time(분할로 늘어날 수 있음), 응답까지 행동 지속을 함께 본다. 판단 내용/후보 key·설명·보고서 필드/정렬과 stale 반응을 동일 스냅샷 replay로 비교해 의미가 보존됐는지 확인한다. GC/디스크/네트워크를 각각 별도 marker로 측정해 성능 향상을 잘못 귀속하지 않는다.

## 증거 안정성 주의

조사 초기 Player.log는 660,372byte/11월7일09:59 파일이었고 Unity 버전 및 마지막 Gemini SEND를 읽었다. 조사 도중 외부 실행에 의해 9,918byte/11월8일08:03:16 파일로 교체되었다. 이 조사에서 게임을 켜거나 조작한 것은 아니다. 과거 report buildMs와 PERF 인용은 runtime 담당의 보존본을 사용해야 하며 현재 살아 있는 Player.log 경로만으로 같은 파일이라고 주장하면 안 된다.

남은 미확정: 사용자 장면의 프레임별 CPU/GC/jobs/I/O 분해, 현재 실행 로드 바이너리와 각 로그의 정확한 버전, 상위 report 세부 feature별 비중, 실제 월드 엔티티 수/가방·recipe 수. 이 문서는 병목 경계와 안전한 수정 순서를 구체화한 결과이며 수정·성능개선 완료 보고가 아니다.
