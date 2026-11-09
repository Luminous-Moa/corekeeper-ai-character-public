# 검색 코어 벤치마크

`Tools/path-search-benchmark`는 생산 `AITunnelSearch`와 변경 전 구현의 독립 참조 함수를 같은 합성 지도에 실행한다. 707 요청은357고유 목적지와350재방문으로 구성하고,2179목적지는별도의고유지점검사다. 이 벤치마크는 코어 검색을 매 요청마다 실행해 분할 동일성을 검증한다. build 캐시 재사용 검증은 `Tools/path-preparation-tests`에서 생산 `PreparePlan`과 `PlanC`를 직접 링크하여 별도로 수행한다.

예산32로 모든 요청이 완료됐고 경로·실패·cap·detour 결과가 이전 구현과 같았다. 각Step의작업단위는32이하였다. 원시실행메트릭과시간은 [벤치마크 로그](core-benchmark.log)에 있다. 이것은 Unity 전체프레임 성능 측정이 아니다.
