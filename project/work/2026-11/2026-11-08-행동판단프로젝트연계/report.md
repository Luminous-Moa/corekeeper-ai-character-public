# 행동 판단 그래프의 프로젝트 연계

프로젝트 정상 조회에서 기획·구현 위치와 게임 규칙을 함께 찾도록 연결했다. 현재 행동 정본 251,417노드·350,275관계에서 정확한 ID를 유지한 19,122개 참조를 추가했다. 전체 정본은 기존 Python CLI로 조회한다. 프로젝트 결과는 27,015노드·49,591관계다.

22개 주제는 관련 규칙을 검토하는 색인이다. 각 주제와 간선에 개발 참고 지위를 표시하며, 실제 모드 지원·실행 관측은 기존 코드와 관측 기록으로 판단한다. sourceKey·variation을 합치거나 새 행동 성공 사실을 만들지 않았다.

## 사용 흐름

프로젝트의 `intent P.life-tree-first-scope` 또는 `behavior P.life-tree-first-scope` → `show R.game-topic.cook` → `show game:cooking:1` → `source game:cooking:1`로 조건과 정확 원문을 읽는다. `feature <기능 ID>`에는 그 기능의 코드 트리에서 확인되는 관련 규칙 주제도 표시한다.

`game target 8373 --variation 0`처럼 기존의 target/find/path/gaps/cooking/show/source/overview/check를 정상 프로젝트 진입점으로 조회할 수 있다. `source`는 원문 위치가 있는 노드에 사용한다. 원문 위치가 없는 행위 label은 show의 관계에서 주장 또는 완결 레코드로 이동한다.

## 연결 범위

| 주제 | 기획 입구 | 정확 참조 수 | 코드 입구 수 |
|---|---|---:|---:|
|제작·가공|P.life-tree-first-scope · P.unrestricted-work|670|1|
|요리|P.life-tree-first-scope|6646|1|
|농사·성장·물|P.life-tree-first-scope · P.unrestricted-space|91|2|
|가방·장비·보관·회수|P.bag-equipment|885|3|
|수리·강화|P.maintenance · P.maintenance-single|1257|2|
|채굴·채집|P.expedition|37|2|
|이동·문·다리·장소|P.expedition-navigation · P.expedition-blocked|37|3|
|설치·도색·기지|P.base-example · P.unrestricted-space|2690|2|
|전투·공격·피해|P.field-choice · P.unrestricted-support|505|3|
|섭취·휴식|P.life|562|1|
|동물·펫・번식|P.life|244|0|
|벌레·포획|P.life|160|0|
|낚시|P.life|18|0|
|상점·거래|P.life|36|0|
|차량·보트|P.expedition|12|0|
|포탈·지도|P.expedition-navigation · P.spatial|96|0|
|전기·자동화|P.base-example · P.life|571|0|
|월드 상호작용·꾸미기|P.life · P.base-example|198|0|
|사망·부활|P.result|3|0|
|환경·변형·스폰|P.observation|395|0|
|환경·적 공격의 위험|P.observation · P.field-choice|28|0|
|원본 디버그 동작|P.game-first|2|0|

참조 수는 서로 다른 레코드 단위의 합이며 고유 행동 수가 아니다. 한 규칙은 여러 검토 주제에서 참조할 수 있다. 행위 label 129개, 주장 5,880개, 행동 완결 2,539개와 요리 조합 6,561개를 전부 연결했다. 원본 source·조건 소비·설정 규칙·미결 등도 정본 입구에서 정확한 노드로 이동한다. 전체19,122참조는 각 원본 id/kind/origin과 비교한다.

기존 `R.expedition-game-evidence`에는 실제 조회 기록에서 사용한 곡괭이·문·다리의 정확한 세 레코드를 연결했다. 곡괭이는 채굴 주제, 다리는 이동 주제에도 대응했다. 코드와 게임 사실 사이의 새로운 실행 인과는 주장하지 않는다.

## 근거와 변경 관리

편집 원본은 [topics.mjs](topics.mjs)와 [link.mjs](link.mjs)이다. 기존 프로젝트 integrate.mjs가 이 입력을 합치고 기존 query.mjs가 현재 게임 CLI를 호출한다. 생성 JSON은 기존 product-build.mjs로 재생성했다. 게임 정본·게시본 생성기·legacy 코드·조회 진입점·설명 문서를 meta.sources에 등록해 변경을 감지한다.

대상 원본 ID 및 원문 해시를 보존한다. 다음 게시본으로 바뀌면 check의 stale로 드러나며 다시 빌드할 때 label 미분류와 코드 입구 누락을 검사한다. 범위 밖의 상세 증거·관계·후속 보완도 전체 정본의 show/path/source를 통해 이어서 조회한다.

게임 행동이 없는 모드 고유의 모델 통신·기억 유지·토큰 비용 등에는 게임 행동 구현 관계를 추가하지 않았다. 해당 프로젝트 노드와 합의는 기존 그래프에 남아 있다. 낚시·차량·동물·전기·디버그 등은 주제 관련 참고로 드러내고 구현 지원 여부를 별도로 확인한다.

## 검증 파일

[검증 스크립트](verify.mjs)는 프로젝트 CLI에서 22주제마다 정확 노드 show와 source를 실행하고 원문 해시를 확인한다. [evidence.json](evidence.json)은 결과 요약, 주제별 JSON은 실제 조회 결과다. [check.json](check.json)은 구조·입력 낡음·코드 기준 검사를 보관한다. 독립 검수는 부른 대화가 별도로 수행한다.

자체 검증은 22주제 전부의 show/source와 원문 해시, 19,122참조의 id/kind/origin 일치가 통과했다. 원문조회 진행 중 채굴·이동에 기존 정확근거 2간선을 추가했다. 원본 게시본과 노드가 유지되어 원문조회 결과를 보존하고, 최종 49,591간선 상태를 [final-check.json](final-check.json), [final-mining.json](final-mining.json), [final-move.json](final-move.json)으로 다시 확인했다. [최종 집계](finalize.mjs)는 두 시점과 최종 검증을 evidence.json에 명시한다. 최종 check는 ok:true, 구조 오류와 stale는 모두 빈 배열이다. product-build.mjs --check도 byte-identical로 통과했다.
