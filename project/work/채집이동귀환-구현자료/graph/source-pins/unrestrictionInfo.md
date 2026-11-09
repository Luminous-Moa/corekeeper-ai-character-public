# 정보·기억·채팅 제약 해제

작업트리 C:/Dev/CoreKeeperModSDK-unrestricted의 생산 코드를 변경했다. 게임·설치·모델 호출은 하지 않았다. 다른 구현 담당과 같은 worktree이므로 정보 파일의 최종 통합 커밋은 주 담당이 만든다.

## 변경

- Tools/ai-bridge/memgraph.mjs: 목차 적4/장소4/동료3와 지역2, 제작대3/5, 검색 모호후보3/find15/관련목표5/관련시도3의 임의 선택 잘림을 제거했다. 점수·최신순은 정렬에만 쓴다.
- 목차1000·선조회1500·질의1200·결과2500자와 질의별 몫 잘림을 제거했다. 과거 호출자가 작은 limit 인자를 전달해도 정보가 사라지지 않는다. LIMITS/CAPS의 Infinity는 기존 import 호환용이며 배열 크기 사전할당에 쓰지 않는다.
- near 반경은16/32/48 강제 선택 대신 유한한0이상 입력값을 적용한다. 빈 입력의 기본32는 기본 검색 의도이지 최대 반경이 아니다.
- 사건의 원문·사유 문자열을 인덱스 생성 중160/200/120자로 자르지 않는다. find가 host order/problem의 실제 text도 검색하게 연결했다. recall-loop의 why160·요약200자 잘림도 제거했다.
- AIExpCore.cs: recent6/goalkeys6/window12/report2200/blocks24 및 적3종·사유320자의 정보 잘림을 제거했다. 현재 후보가 아닌 과거 목표 키도 전부 보고한다. ExpRun.Trim의 기존 호출과 호환하되 실제 원문을 보존한다.
- Core/AIChatLog.cs: 대화40개 보관·보고10개 제한을 제거하고 원래 순서와 new/to_me를 유지한다.
- Core/AIFightLog.cs: 싸움5개 보관·10분 보고 필터를 제거했다. AIExperience.cs의 쓰러짐 순간 보이는 적 거리6/4종 제한 대신 보이는 적 전체를 거리순 기록한다. 가장 가까운 적이라는 기존 의미를 보존한다.

지식 공개는 known.json/openView를 그대로 사용한다. 캐릭터 자신의 기록 범위와 미관측 게임 지식 숨김을 유지한다. 검색 호출 횟수·한번 질의3개·전체 deadline·서비스 비용/구독 보호는 결과/증거의 잘림과 다른 별도 운영 보호이므로 이번 정보 수정에서 해제하지 않았다. 파일 복구 때 tail 일부를 읽는 최적화·로그 회전·biome 재계산 캐시는 보고 항목 삭제가 아니므로 유지한다.

## 검증

- `node Tools/ai-bridge/test/memgraph.test.mjs --kg C:/Dev/CoreKeeperModSDK/Tools/knowledge/data`: **84 checks 통과**. 원정 작업트리는 ignored 게임 그래프를 자동 복사하지 않으므로 공식 data를 읽기 전용으로 지정해야 한다. [로그](info-memgraph.log). 기존 지식누출 차단·회전·불완전JSON·100시간 인덱스 성능 회귀를 포함한다. 새 사례는 20개 긴 지시 원문/2500자초과 결과/8종 적·8지역·8동료/임의300반경을 실제 생산 함수로 검증한다.
- `node Tools/ai-bridge/test/recall-loop.test.mjs`: **41 checks 통과**, deadline·검색 횟수 보호 회귀. [로그](info-recall.log).
- `dotnet run --project Tools/info-tests -c Release`: **7 checks 통과**. 실제 ExpReport·AIChatLog·AIFightLog를 직접 링크해70개 대화,30개 목표 이력과 키,2200자 초과 보고,5000초 뒤20회 싸움 보존을 검증. [로그](info-retention.log).
- `dotnet run --project Tools/exp-sim -c Release`: **ALL CHECKS OK**. 기존 경험 시나리오·복구·100시간 합성1339회 보고 회귀. [로그](info-experience.log).
- 중간 통합 core-tests는 컴파일됐고1396 중34개 구정책 기대가 실패했다. 정보·채팅 신규 기대는 통과했으며 실패는 다른 담당의 수량/후보/계획/장소 변경 기대값 갱신 중이므로 주 담당에게 넘겼다. [중간 로그](info-core.log). 이 중간 결과를 최종 통합 통과로 세지 않는다.
- 소유 파일 `git diff --check` 통과.
