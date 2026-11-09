# 게임 지식 해금과 채집 목표 허용은 별개

사용자 확인 요청에 따라 AIKnowledge.Note → known.json → kg.openView → 브리지 검색 경로와 AIPlanReport → AIPlanner.CheckGoal 경로를 대조했다. 지식그래프는 미관측 전체를 숨기고 본 것·보유한 것·본 제작대의 레시피와 재료를 연다. ‘실제로 획득한 것만’이 해금 기준의 전부라는 앞선 요약은 부정확했다. 이 지식 공개 제한은 사용자가 의도한 정책으로 유지한다.

실제 생산 함수와 현행 그래프를 [probe-unlock.mjs](probe-unlock.mjs)로 실행했다. IronOre(o:1510)는 미관측이면 closed, seenId에 있으면 open, heldId에 있으면 open이다. Furnace만 seenId에 넣어도 IronBar 레시피와 재료 IronOre가 모두 열린다. 이는 함수 수준 가상 known 입력 시험이며 실게임 신규 해금 시험은 아니다.

근거: AIKnowledge.cs:144~168은 보인 물체·적·드롭, 보유품과 광석 타일을 기록한다. Tools/knowledge/kg.mjs:78~87은 본 제작대의 레시피와 재료를 연다. Tools/ai-bridge/memgraph.mjs:226은 캐릭터의 known.json으로 조회 범위를 만든다.

반면 AIPlanReport.cs:32~35는 흙·구리·주석·나무와 GatherableFood만 can_do.gather에 넣는다. AIPlanner.cs:848은 같은 품목 및 _gatherFood만 받으며 AIKnowledge/known/openView를 이 검사에 사용하지 않는다. 따라서 철 지식이 열렸거나 보유 이력이 생겨도 이 검사 자체는 철 gather를 거절한다. 제작대에서 본 품목이기 때문에 채집이 잠긴 것이 아니라 지식 공개와 별도의 실행 지원 목록이다. AIBrain.Prep.cs:21의 Gatherable4종도 별도 연결 범위다.

정책 결론: 지식 해금을 풀라는 권고가 아니다. 캐릭터가 알게 된 품목과 게임 도구 조건을 실제 채집 목표/실행 지원에 연결할지 판단해야 한다. 코드 변경은 수행하지 않았다.
