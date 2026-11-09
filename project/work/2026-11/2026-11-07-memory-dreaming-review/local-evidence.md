# 현행 캐릭터 기억: 구현 근거와 Dreaming 비교의 경계

2026-11-07 읽기 전용 조사. 대상은 `C:/Dev/CoreKeeperModSDK`, 브랜치 `ai-character`, HEAD `5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef`. 별도 base-4a 개발 작업의 효과를 포함하지 않았다. start-job과 `C:/Dev/AGENTS.md`를 읽었으며 저장소 아래 별도 AGENTS.md는 발견하지 못했다. 기존 변경 파일을 수정하지 않았다. 게임·브리지·외부 LLM·새 시험군을 실행하지 않았다. 현행 memgraph의 파일 읽기·색인·질의 함수만 실제 저장 기록에 호출했다. 이 문서는 현행 구현 조사이며 Anthropic 기능의 외부 출처 검증은 부모 조사와 합쳐야 한다.

## 주요 판정

현재 시스템은 **자동 관측 보존 + 목표 결과 집계 + 요청 시 사건 검색 + 제한된 게임 지식 조회**가 구현된 상태다. 저장된 과거가 아무 경로로도 쓰이지 않는 구조는 아니다. 공간 기억은 행동 후보·제작 조건에 직접 쓰이고, `experience`와 `memory_index`는 상위 LLM에 자동 전달된다. 그러나 **여러 경험에서 LLM이 새 교훈을 합성하고, 근거와 적용 조건을 붙여 별도 기억으로 축적·수정하는 오프라인 정리**는 현행 경로에서 확인되지 않았다.

사용자가 합의한 **LLM 요청에 따른 신뢰도 감점 → 복구 가능한 비활성화 → 재구성 뒤에도 유지**도 현행 기억 스키마·조회·후보 경로에는 없다. 11월 7일 방향 합의와 제품 구현을 구분해야 한다. 기존의 관측에 따른 광석/제작대 삭제, 시간에 따른 검색 중요도 감소, 실패 조건 재평가는 이 기능의 일부를 닮았지만 같은 기능이 아니다.

검색 횟수 증가보다, 필요한 과거가 판단에 도달하고 실제 작업 완료를 바꾸는지가 현재의 핵심 평가 기준이다. Dreaming식 정리는 그 위에 선택적으로 놓을 수 있는 계층이지, 현행 자동 기록·공간 기억을 대체해야 한다는 근거가 아니다.

## 혼동하면 안 되는 세 층

| 층 | 현행 위치·역할 | 경계 |
|---|---|---|
| 개발 작업 기록·지식 | `.head/sessions/코어키퍼-AI-캐릭터/...`의 합의·설계·평가 | 개발자가 일을 이어가는 기록. 게임 캐릭터가 이 작업 문서를 읽는다는 경로를 확인한 것이 아니다. |
| 게임 지식 그래프 | `Tools/knowledge/kg.mjs`, `data/<build>/graph.json`, `rules.json` | 제작법·물체·게임 규칙. 캐릭터 경험에서 자동 합성한 교훈이 아니다. `known.json`으로 캐릭터별 가시 범위를 계산하며 일반 규칙은 모두에게 열린다. |
| 캐릭터 런타임 기억 | `AIPlayers/<world>/<member>/memory.json`, `events*.jsonl`, `goals.jsonl`, `exp_state.json`, `known.json`; `memgraph.mjs` | 월드/캐릭터 단위 공간 상태와 경험. memgraph는 사건에서 재구성하는 인메모리 색인이다. 독립적인 LLM 사실 저장소나 그래프 DB가 아니다. |

근거: [AIMemory.cs:133](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIMemory.cs:133), [AIExpCore.cs:4](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:4), [memgraph.mjs:5](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:5), [kg.mjs:78](C:/Dev/CoreKeeperModSDK/Tools/knowledge/kg.mjs:78), [memgraph.mjs:548](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:548). `rules.json:2`의 version·checkedFor·level·src는 수작업 게임 규칙의 버전/출처이며 캐릭터 교훈의 신뢰도 관리가 아니다.

## 저장 → 검색 → 주입 → 행동

1. **공간 관측과 저장.** `AIBody.cs:274`가 `AIMemory.Observe`를 부른다. 약 1초마다 시야 안 광석의 종류·타일·마지막 관측 시간을 갱신하고, 재관측 때 사라진 광석을 삭제한다. 제작대는 로딩 지연을 고려해 연속 두 번 부재를 관측하면 삭제한다. 더티 상태는 약 30초 간격으로 저장한다. 저장 스키마에는 광석·제작대뿐 아니라 상자/기지·전투 학습값·드롭 통계·텃밭/물·조리솥 등이 포함된다. 광석 등은 원본 사건마다 적재하는 것이 아니라 현재 기억 상태를 갱신한다. [AIMemory.cs:47](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIMemory.cs:47), [AIMemory.cs:242](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIMemory.cs:242), [AIMemory.cs:299](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIMemory.cs:299).
2. **사건과 목표 결과.** `AIExperience.Tick`이 쓰러짐·전투·지역·첫 소지 등을 기록하고, 목표 시작/끝 훅이 목표 시도를 연결한다. `ExpRecorder.Event`는 `{id,sess,t,play,world,char,kind,run,ents,src,d}`를 append한다. `goal_end`에는 목표 시도 전체 결과·당시 조건·시작/끝 사건 ID가 들어가며 `goals.jsonl`에도 파생 행을 저장한다. [AIExperience.cs:388](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExperience.cs:388), [AIExpCore.cs:694](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:694), [AIExpCore.cs:776](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:776).
3. **자동 요약과 검색.** `ExpReport`는 최근 6개 목표·최대 6개 목표 키·키별 최근 12회 결과를 목표 2,200자 범위로 조립한다. 원인별 조건 변화와 현재 막힘 여부도 계산한다. `MemIndex`는 사건 원본을 읽어 대상·목표·시도 색인을 만들고 추가된 바이트만 갱신하며, 회전 목록 변경·축소 시 재구성한다. `about/goal/near/when/follow/game/find` 7개 질의를 지원한다. `near`는 공간 기억도 읽는다. [AIExpCore.cs:389](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:389), [memgraph.mjs:82](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:82), [memgraph.mjs:479](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:479), [memgraph.mjs:583](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:583).
4. **상위 판단 입력.** 게임 보고는 `memory`, 장소·상자 등과 `experience`를 조립한다. 브리지 `plan.mjs`가 캐릭터 목차와 이전 답의 prefetch 결과를 붙이고 `recallLoop`에 보낸다. 추가 검색은 모델이 `decision:"recall"`을 내야 일어난다. 기본 한 판단 2회, 회당 질의 3개, 30초 검색 창·전체 시간 예산이 있다. 검색 결과는 다음 호출의 전체 입력에 붙는다. [AIPlanReport.cs:64](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanReport.cs:64), [AIPlanReport.cs:100](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanReport.cs:100), [plan.mjs:37](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/plan.mjs:37), [recall-loop.mjs:33](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/recall-loop.mjs:33), [recall-loop.mjs:58](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/recall-loop.mjs:58).
5. **실행 연결과 결과 회수.** 상위 `set`은 검증 뒤 계획 첫 단계를 `SetGoalFrom`으로 시작하고 `PlanResult`를 남긴다. 하위 후보 생성은 화면 밖 목표 광석을 `RememberedOres`에서 읽고 경로를 평가하여 `dig` 후보로 제공한다. 따라서 recall 없이도 기억이 행동 후보를 바꿀 수 있다. 제작대 기억은 `RememberedStationFor`와 `KnownRecipe`에 쓰인다. 실제 종료 결과는 다시 `goal_end`로 들어간다. [AIPlanner.cs:673](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs:673), [AIPlanner.cs:732](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs:732), [AIBrain.Candidates.cs:490](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:490), [AICraft.cs:414](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AICraft.cs:414), [AICraft.cs:448](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AICraft.cs:448).

마지막 단계는 코드 연결 확인이다. 이번 조사에서 모델을 호출하거나 실제 이동·제작 완료를 관측한 것은 아니다. 상위 LLM의 계획 주도권 확대, 철광석 등 행동 연결의 공백은 기존 11월 7일 평가가 남긴 별도 문제이며, 검색 정리만으로 해결됐다고 보지 않는다.

## 보존·추적성의 강점과 제한

- 사건 원본은 append-only이고 기본 8MB 이후 회전하며 삭제 로직은 없다. 손상/누락된 목표 집계는 `goal_end`에서 복구한다. `exp_state.json`은 이전 `.bak`을 남긴다. 이는 요약만 남기고 원본을 버리는 구조보다 재해석·감사에 유리하다. [AIExpCore.cs:573](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:573), [AIExpCore.cs:694](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:694), [AIExpCore.cs:805](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:805).
- 검색 결과의 `[e:id]`, `[r:run]`은 원본 사건·시도를 추적할 수 있게 한다. `src`로 LLM 발언과 게임 관측을 구분하며, `plan_result`의 말에는 “관찰 아님”을 표시한다. 과거 장소를 같은 시도의 가까운 사건에서 추정하면 “장소 추정”을 표시한다. [memgraph.mjs:186](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:186), [memgraph.mjs:304](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:304), [memgraph.mjs:574](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:574).
- 다만 **원본 = 모든 감각·대화의 무손실 로그**는 아니다. 원본 자체가 선택된 사건이며, `PlanResult` 저장 시 why 160자·say 100자·recall 요약 200자로 잘린다. 공간 상태도 마지막 관측을 갱신하므로 전체 변경 이력이 자동 보존되는 구조가 아니다. [AIExperience.cs:280](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExperience.cs:280), [AIMemory.cs:203](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIMemory.cs:203).
- memgraph는 원본의 `d` 전체를 유지하지 않고 선택된 필드만 축약해 들고 있다. `follow e:id`도 원본 줄을 다시 여는 기능이 아니라 색인에서 만든 문장을 돌려준다. 설계의 “필요하면 원본 세부를 다시 읽기”까지 구현됐다고 확대하면 안 된다. [memgraph.mjs:135](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:135), [memgraph.mjs:156](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:156), [memgraph.mjs:524](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:524).
- 저장 형식의 `version=1`/`v=1`은 있으나, 이 경로에서 기억별 해석 버전·정정 이력·폐기 사유·복구 상태는 확인되지 않았다. `goal_rev`는 실행 목표 수정 번호이지 기억 버전이 아니다. [AIMemory.cs:49](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIMemory.cs:49), [AIExpCore.cs:808](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:808), [recall-loop.mjs:35](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/recall-loop.mjs:35).

## 이미 있는 기능과 아직 없는 기능

| 비교 축 | 이미 구현된 것 | 합의/후속 설계 또는 확인되지 않은 것 |
|---|---|---|
| 경험 정리 | 목표별 통계·최근 결과, 대상/장소 색인, 중요도×최근성 순위 | 여러 사건을 종합해 LLM이 새 교훈·행동 원칙을 지속 저장하는 경로 |
| 조건에 따른 재평가 | `ExpResolve.Measured/Judge`: 도구 향상·추가 광석 발견·제작대/재료 변화로 막힘 재평가, 반복 시 대기 증가, 오래됨 표시 | 일반 자연어 교훈의 적용 조건·반증·폐기 기준 |
| 최신 상태 갱신 | 시야 안 광석 부재 확인 후 삭제, 제작대 두 번 부재 후 삭제, 새 관측으로 추가/갱신 | LLM 요청으로 신뢰도 감점, 기준 미달 비활성화, 재접속/재구성 지속, 복구 API |
| 출처 | 사건/시도 ID·src·당시 조건·장소 추정 표시 | LLM 파생 교훈별 근거 사건 집합, 해석 버전과 원본 변경의 독립 관리 |
| 오프라인 작업 | 순수 C# 평가 도구·파일 읽기 검색·손상 복구·재색인 가능 | 게임 휴식/종료 등에 자동 실행되는 LLM 정리 작업·스케줄러·consolidation queue |
| 다음 턴 준비 | 모델 답의 `prefetch` 질의 최대 3개를 메모리에 보관하고 다음 요청 때 실행, 기본 30분 TTL | 백그라운드로 경험을 분석하여 기억을 바꾸는 Dreaming과는 다름 |

`Measured/Judge`의 현재 클래스명은 **ExpResolve**다(기존 평가의 ExpRules 표기와 다름). [AIExpCore.cs:279](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:279), [AIExpCore.cs:341](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIExpCore.cs:341). 최근성 점수는 노출 순위이며 사실 신뢰도 점수가 아니다([memgraph.mjs:340](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:340)). 하위 결정의 `DAnswer.confidence`도 기억 항목 신뢰도와 관계없는 선택 응답 필드다([AIDecisions.cs:71](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIDecisions.cs:71)).

게임 수치의 좁은 경험 학습도 이미 있다. 예를 들어 `AIDrops.Note`는 파괴 전후 인벤토리 차이로 드롭 횟수·합계를 축적한다. 이는 LLM 교훈 합성은 아니지만 “아무 학습/정리도 없음”이라는 평가는 틀리게 만든다. [AIDrops.cs:32](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIDrops.cs:32), [AIDrops.cs:73](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIDrops.cs:73).

## 대표 실제 기록의 읽기 전용 추적

입력: `C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers/b39072f4c49d28bf6ab43b7b6a702d7f/claude/events.jsonl`. SHA-256 `12c9c61f9bdc3e822aaeda30c06b6a983d25d855aba0f466259921fe646cdfdd`. 호출 전후 파일 문자열 동일을 확인했다. 게임 지식은 현행 main tree의 `Tools/knowledge/data`에서 읽었다.

1. 원본 599행: 사건 `id=599`, `kind=down`, `src=game`, `run=b5426ebb.596`, 플레이 10958.4초. `ents`는 `enemy:CavelingSkirmisher`, `cell:-4,3`, `area:Slime`. `d.cause`는 2026-11-04 21:38:30 `(-53.4,50.0)`의 쓰러짐이고, 당시 무기 레벨 0·방어구 3·최대 체력 100·곡괭이 2를 포함한다. 원본은 가장 가까이 보인 적을 기록하며 실제 가해자 확정과 구별해야 한다.
2. `MemIndex.add`: 같은 `id/run/src`를 유지하고 적을 `o:3061`로 연결, 타일을 `[-53,50]`로 변환했다. 당시 조건 중 검색에 쓰는 필드만 남겼다.
3. `follow e:599`: “3분 전 쓰러짐(가장 가까움: 동굴인 척후병)… 타일 -53,50 Slime · cook 중”과 `r:b5426ebb.596`의 “다른 목표로 바뀜, 쓰러짐 1”을 반환했다.
4. `near tile:-53,50 radius=32`: 원본 사건 599를 재노출하고, memory.json의 요리 솥 `(-32,33)`을 함께 반환했다. 싸움 37개 중 장소 추정 33개라고 명시했다.
5. 같은 실제 기록의 `goal g:gather:CopperOre`: 시도 6개 중 완료 5·다른 목표로 바뀜 1을 반환했다. 11월 4일 질문 시험의 기록 근거와 일치한다.
6. 생성한 목차는 총 607개 사건에서 적 3/11종·장소 2/18곳·동료 2/4명을 보여줬다. 장소를 모르는 옛 싸움 18개가 빠진다는 문구가 있었다. 목차 밖 기록이 존재한다는 제한도 표시됐다. 이 텍스트는 코드상 `reportTurn`에 주입되는 바로 그 출력이다. 이번에 실제 모델 입력 전송이나 행동 실행은 하지 않았다.

이 읽기 전용 확인은 **원본 → 색인 → 선택적 질의 결과**의 추적성을 보여준다. **질문을 받은 모델이 스스로 조회했는지, 조회로 목표/행동이 바뀌었는지, 실게임에서 완수했는지**는 보여주지 않는다. 현행 헤더의 “지금” 조건은 `idx.lastCond`(마지막 조건 기록)와 저장된 플레이 시간을 사용하므로 게임을 켜지 않은 이번 출력의 “3분 전/지금”도 마지막 저장 시점 기준이다([memgraph.mjs:178](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:178), [memgraph.mjs:217](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:217)).

## 실제 부족분과 평가 범위

- **필요한 조회를 선택하는 능력과 검색기 자체는 별도다.** 과거 실게임 42판단 recall 0, 질문 시험 35판단 recall 0이 기록돼 있다. 질문 시험에는 일부 부정확한 답과 보고/기록 시점 불일치도 있었다. 이를 현재 모든 모델의 동일 성능으로 일반화하지 않는다. [7-실게임.md:39](../2026-11-04-기억-그래프-검색/7-실게임.md:39), [8-질문시험.md](../2026-11-04-기억-그래프-검색/8-질문시험.md).
- **원본 보존과 원본 세부 접근은 별도다.** 사건 ID는 좋은 기반이지만, 현재 조회는 축약된 필드를 읽는다. 실제 정보 공백이 발생하면 원본 세부를 볼 수 있는 좁은 질의가 필요한지 먼저 평가할 수 있다. 전체 기록을 매번 주입할 근거는 아니다.
- **검색 가능한 범위에도 작은 공백이 있다.** `problem/order`의 본문은 `r.text`에 저장하지만 현재 `qFind`의 텍스트 검색 필드 목록에는 `r.text`가 없다. `near`가 공간 기억의 제작대 좌표/광석 수를 반환할 때 저장된 `stationSeen/oreSeen`을 출력하지 않는다. 각각 코드상 확인한 범위이며 이번에 결함 수정이나 추가 시험은 하지 않았다. [memgraph.mjs:160](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:160), [memgraph.mjs:493](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:493), [memgraph.mjs:577](C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/memgraph.mjs:577).
- **합의된 비활성화의 지속성과 전체 경로 적용은 새 설계가 필요하다.** 사건을 지우거나 검색 색인에만 active 표시를 넣으면 재구성으로 돌아오거나 `experience`·공간 후보 경로에서 계속 쓰일 수 있다. 같은 근거의 반복 감점 방지, 관측 실패/실제 반증 구별, 현재 상태 무효화/과거 사건 진위 구별, 새 관측으로 복구는 기존 사용자 합의의 요구다. 현행 기능으로 충족됐다고 보지 않는다. [동료형AI-계획주도권.md](../2026-11-07-동료형AI-계획주도권.md).
- **오프라인 교훈 생성은 현 요구에 자동 포함되지 않는다.** 11월 4일 설계가 `lesson:`·`chapter:`를 “나중”으로 명시했고, 11월 7일 합의도 자유로운 교훈 작성·관계 편집을 범위 밖에 뒀다. Dreaming을 참고하더라도 지금 승인된 방향을 확대 구현할 근거는 없다. [1-설계.md:80](../2026-11-04-기억-그래프-검색/1-설계.md:80).

기존 평가에 적힌 memgraph 80개·recall-loop 41개 검사 통과와 합성 100시간 시험은 과거 검증 기록으로 인용할 수 있으나 이번 조사에서 재실행하지 않았다. 부모가 확인한 600회 시험 역시 동일 합성 3장면의 반복·첫 행동/모의 수락 검증이며 실플레이 효과로 쓰지 않는다. 후속 실게임 평가는 원본 저장 → 보고/검색 → 목적지 선택 → 실제 확보 → 고갈 재관측 후 다음 선택을 연결하는 기존 시험 초안을 유지할 만하다([기억-실게임시험계획.md](../2026-11-07-기억-실게임시험계획.md)).
