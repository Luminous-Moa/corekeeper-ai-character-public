# world 후보 개수 상한 제거 결과

기준 `1fc89b9`의 격리 `C:/Dev/CoreKeeperModSDK-bag-common`에서 부모가 지정한 후보 생성·Core 조립 파일만 변경했다. 아직 독립된 최종 커밋은 없으며 가방 메뉴/Loop 작업과 함께 부모가 고정한다. [트리 합의](tree-agreement.md), [조사 지도](inventory-category-cap-audit.md), [삭제 명세](cap-removal-spec.md)를 따른다.

## 변경한 경로

- `AIBrain.Candidates.cs`: 관측된 적3/4, 벽 목표3·기타2, 벽 뒤 적1/드롭1, 광석 파기 성공2·타깃시도4, 부술 물체2, 뿌리2, 기억 광석 조회12·후보2·타깃시도4, 동료2, 화면끝 탐험2, 기억 frontier결과2의 숫자 quota를 제거했다. 현재 시야/물품·도구·도달 가능성/클러스터·action-key 조건과 정렬은 유지했다.
- `Core/AICandidateAssembler.cs`: 비전투14와 양수 전투 후보 설정을 포함한 마지막 숫자 cut을 없앴다. 기존 public 호출 형식은 유지하되 `LimitFor`는0을 반환하고 `Finish(..., limit)`의 숫자로 목록을 자르지 않는다. wait·고유 선택값·진행 중 동작 유지·실행 불가 제거를 보존했다. close-fight는 기존 목표측 자격을 만족하는 모든 후보를 전투 후보 뒤에 유지하며 현재 목표 동작을 먼저 둔다. 실패 과거는 기존처럼 사실 주석으로 남는다.
- `Core/AIPickupOptions.cs`: 목표/배고픔 우선·자동 줍기 구분·실행 가능성과 같은 키 중복 제거는 유지하며 모든 적격 드롭을 반환한다.
- `Core/AIPlaceGrid.cs`: 결과별 최저 score 대표 및 clean 우선 정렬은 유지하고, 결과 대표를3개와 edge추가2개로 자르던 단계를 제거했다.
- `AIMemory.cs`: `RememberedOres(..., max<=0)`를 제한 없는 조회로 지원하며 현장 호출은0으로 바꿨다. 명시 max를 사용하는 요약 호출 형식은 유지한다.
- `AIBase.cs`: 상자 보관2·준비 꺼내기6·음식 후보3의 cut을 제거했다. 상자 소유권과 준비/배치·실제 용량 판단, 일반 보관 품목 범위는 유지했다.
- `AIFarm.cs`, `AIEdge.cs`: 야생 씨앗 대상2·실제 횃불 자리2 cut을 제거했다.

가방 자체 8/12/20 cut과 메뉴 트리는 메뉴 엔진 담당, Loop의 방향대표4→3 cut과 실제 BuildJson 전달은 부모 담당이므로 여기서 수정하지 않았다.

## 보존한 다른 의미의 제한

개별 BFS/Dijkstra/frontier search 내부 guard, 실제 시야와 화면 범위, 농사 행동 batch/요리 회당 수량, 이동 stand 탐색 solver, 자동 주움/같은 광맥 묶음과 결과별 대표, 보고·지도·로그·기억 보관량, 호출·비용 상한은 보존했다. Core `AIBaseEdgeCore.Options`의 MaxOptions4는 상위/명령 둘레 설계안 소비자로 HEAD 판단에 따라 남겼다. 이것을 현장 후보 상한 제거 완료의 범위에 조용히 포함하지 않는다.

## 검증

`dotnet run --project Tools/core-tests -c Release`를 실제 변경판에서 실행해 **1,204 검사 통과, exit0**을 확인했다. [최종 로그](tree-cap-core.log). 대상 코드의 `git diff --check`도 exit0이다.

- 25개 조립 후보가 legacy positive14 인자를 주어도 wait와 함께26개 모두 남고 실제 선택값이 모두 고유한지 확인했다.
- 24개 서로 다른 적격 드롭이 선택 함수에서 모두 같은 순서로 남는지 확인했다.
- close-fight의 여러 적격 목표측 후보가 모두 남고, 실제 body 불가·목표 진척 아님·이미 자동으로 들어오는 줍기는 여전히 제외되는지 확인했다.
- 오래된 edge/share/cap 기대 검사는 새 계약의 전체 보존으로 갱신했다. 원시 L1 fixture는 수정하지 않았다. 과거 기록은 당시 one-goal/14cut의 기록된 부분으로 **역사 projection**해 비교하고, 새로 남게 된 후보의 물리적 가능성을 그 과거 로그가 증명한다고 주장하지 않는다. 현재 정책은 위 별도 실제 Core 함수 검사로 확인한다.

이 검증은 Unity-free Core의 후보 선택·조립을 실행한 것이다. World 생성기·모델 요청·몸 동작 전체의 실행 성공이나 실게임 성공은 아니다. 부모가 새 메뉴/Loop와 통합한 판에서 실제 14초과 항목의 최종 모델 choices 전달 및 전체 회귀·독립검수를 수행해야 한다. 이 작업에서 Unity·설치·게임·외부 모델 호출·공식 그래프 변경·커밋은 수행하지 않았다.

수정 테스트 파일: `Tools/core-tests/AssemblerReplayTests.cs`, `ChoiceFactsTests.cs`, `FixMergeReviewTests.cs`, `PlaceGoalTests.cs`.
