# 제약 해제의 공식 지식그래프 반영

공식 소스와 현재 코드층·제품 그래프 기준을 **5cc5a43ff8df91634c04b44bac9f15f849bdb611**로 맞췄다. HEAD의 공식 fast-forward와 [독립 구현 검수](review.md)의 정적·대표 오프라인 수용을 확인한 뒤 최종 원본을 pin했다. 설치·게임·새 모델 호출은 수행하지 않았다. 이번 그래프 delta는 [별도 독립 검수](graph-review.md)에서도 수용됐으며 아래 작성자의 자체 생성검증과 구별한다.

## 반영 내용

[사용자 합의](00-agreement.md)에서 임의 작업량·계획길이·열린 부탁·후보/생활 페이지·정보잘림·고정거리/기지·농장·지원행동 제한 해제를 연결했다. 지식 공개·소유권·사용자 비용·실제 게임조건과 정상 요청은 유지 조건으로 분리했다. 새 기획/참고13노드·20관계가 최상위 제작/배포 목표에서 도달한다. 상위 목적·현장 선택·실제 입력/결과, 원래 부탁과 중간 단계의 완료를 구분하는 기존 합의를 유지한다.

과거 R.bag-life-consumers·R.maintenance-consumers·R.bag-cap-audit·R.life-tree-spec는 당시 생활페이지/정보제한을 유지한 역사적 통합판정·명세로 표시하고 현재 해제 지시에 연결했다. 이전 구현 상한 메모가 있는 pol.nl-plan-steps·pol.place-where-anywhere·pol.place-craft-list는 원규칙/메모를 보존하면서 후속 대체 범위를 표시했다. pol.action-constraints-open을 현재 사용자 지시로 조회할 수 있다.

[actions](actions.md), [craft](craft.md), [정보 결과](info.md), [최종 검증](validation.md), [독립 구현 검수](review.md)의 지위를 구별했다. 최종 생성입구 gate의47검사·Core/전체컴파일 및 영역별 이전검증을 같은 종류로 합치거나, 실제 명중·소환·마나소비·게임소비자 성공으로 승격하지 않는다.

## 실제 변경한 정본과 원본

- 기획연결 source.mjs와 기존 compile/graph 생성물: 새 해제 지시·영역별 오프라인 증거·최종 독립수용을 연결했다.
- 지식그래프 기획/정책.md·기능.md·tools/config.json: 새 정책, F.action-constraints-open·F.weapon-use-expanded와 실제 앵커, 새 C# 원본을 등록했다.
- 기존 B 고정 snapshot/runtime-package와 Roslyn/Acorn 코드층, C·실화면 판 대응/overlay, 제품 생성물을 재생성했다. 생성 JSON을 직접 편집하지 않았다.
- 실화면 D/planning.mjs에는 최종 코드14정적대응과 현재 정책/과거상한 해석을 추가했다. 모든 새 코드 대응은 runtimeTrace=false다.
- 실화면 D/bag-source.mjs의 과거39→5a 투영을 최종판으로 자동 확대하지 않았다. [새 successor](graph-stage/successor-projection.json)에서 같은 menu blob을 확인하고 옛 실행증거의 sameRevision=false/evidenceAppliesToCurrentRevision=false를 유지했다. 새 구현의 결과는 새 보고를 따른다.
- 지식그래프 README·제품그래프-사용법 및 B README의 현재 기준을5cc로 맞추고 과거 코드/화면 증거를 역사적 근거로 남겼다.

적용 전 정본 source SHA가 사전 stage와 같음을 확인했고 [적용 전 사본](graph-stage/before-final-graph/)을 보존했다. context.md·할일·게임행동/런타임 지식그래프 데이터와 과거 실게임 원문은 변경하지 않았다.

## 검증

|확인|결과·근거|
|---|---|
|고정 Git 원본|snapshot/runtime-package341파일 Git blob 일치. [pin](graph-stage/logs/code-pin.log), [검증](graph-stage/logs/code-verify.log)|
|최종 코드층|B6685노드·26659관계, 기능24. [생성](graph-stage/logs/code-build.log). Unity/게임metadata 미해결 경계는 기존 방식으로 보존|
|동일 고정 raw 재생성|새 Roslyn을 불필요하게 재실행하지 않고 동일 raw/JS입력에서 생성물7개 바이트 동일. [결정성](graph-stage/code-generation.json)|
|개발 근거|strict 및 원본·인용·앵커 검사 통과. [build](graph-stage/logs/legacy-build.log), [check](graph-stage/logs/legacy-check.log)|
|기획|111노드·175관계·67등록원문. 새 방향/참고와 과거 정책 지위를 구별|
|C·화면 대응|고정코드와 과거 화면의 동일구간만 다시 매칭. C/참조 검사 통과, 과거실게임증거는 새판 성공으로 미승격|
|제품|6939노드·27184관계, 구조오류0·stale0. [검사](graph-stage/logs/product-check.log)|
|공식 동일성|제품 sourceCommit=코드층=공식 HEAD5cc, baselineDrift.warning=null|
|제품 재생성|바이트 동일. [확인](graph-stage/logs/product-determinism.log)|
|새 delta 대표조회|intent/source/정책/과거정책/새지원행동/큰제작10조회 exit0,14정적앵커 각각 유일·실제Git본문과snapshot일치. [최종 확인](graph-stage/final-verification.json)|

사전 stage는108노드/172관계/63고정근거와11조회로 검증했다. 작업 중 변경되는 문서는 source-pins로 고정해 결정성을 확인했다. [사전 검증](graph-stage/verification.json)은 당시의 정본 미변경 확인이며 현재 정본이 아직 미변경이라는 뜻이 아니다.

## 추출 이력과 남은 범위

502·f222를 후보 freeze 통지 뒤 추출했으나 독립검수의 후속 수정으로 최종판이 바뀌었다. 이후60c를 추출했고, 테스트원문만 바뀐663은 [전체 추출입력 동일성](graph-stage/test-head-source-proof.json)을 확인해 raw를 재사용했다. 마지막 검수수용5cc에서 다시 원본을 pin/추출했다. 따라서 Roslyn 실제 실행은502/f222/60c/5cc의4회이며 중간 추출을 최종증거로 쓰지 않는다. [중간 digest 이력](graph-stage/extraction-history.json)에 남겼다.

이번 결과는 사용자 방향·현재 소스·정적 관계와 실제 오프라인 검증의 연결이다. 설치·게임·외부 모델 성공, 모든 게임물품/모드조합, 전체 실게임 프레임 성능, 새 원정/갈림길 설계 전체 구현까지의 완료를 뜻하지 않는다. 현재 게임에서 새 정책이 안정적으로 작동하는지는 기존 치트환경 구축 후 묶음 실게임 검증으로 남는다.

최종 독립 그래프 검수는 graph-review.md를 따른다. 이 작성자의 자체 생성검증은 그 검수를 대신하지 않는다.
