# ProcessResources 가공 사슬 판단 보완

HEAD 요청(2026-11-08, 목표계층-선택지정리 작업의 craft-diagnosis 후속): 행동 판단 그래프에 용광로 자동 가공 규칙을 추가한다.

- 사슬: 레시피 활성 → 재료 이동 → 타이머 → 출력. 각 규칙에 조건·결과·실패 분기·원문 위치를 붙인다.
- 대상: CraftingAuthoring.craftingType=ProcessResources인 원본 전체(정적 확인 7개). 원본마다 sourceKey 신원을 합치지 않는다. 레시피 칸이 숨겨진 원본(1200)은 활성·이동 규칙에서 빼고 타이머·출력 규칙에만 연결한다.
- 기존 260판의 노드·간선·근거를 보존한다. 기존 행(inventory_transfer의 '활성·타이머·출력은 넣기에서 추론하지 않음', process_recipe_selection, automatic_recipe_result, full-d production 완결)은 고치지 않고 새 기록과의 관계로 잇는다.
- 근거는 고정 디컴파일 루트(게임원본-상호작용/code, code-inputs의 Pug.Other.dll·Pug.Automation.dll이 설치본 11-03과 같은 해시)와 raw components.jsonl이다.
- 정적 근거와 실게임 관측을 구분한다. 실게임 성공은 관측하지 않았다(runtimeTested=false).
- 게임·모드 코드와 세이브는 바꾸지 않는다. 원격 저장소 push는 하지 않는다.
- 원문·생성 검수는 별도 컨텍스트가 맡는다. 게시 뒤 프로젝트 제작·가공 주제(R.game-topic.craft)에서 새 기록에 도달하게 하고 정상 조회 진입점에서 원문까지 확인한다.
