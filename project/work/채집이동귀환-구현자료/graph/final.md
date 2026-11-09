# 채집·이동·귀환의 개발 지식그래프 반영

공식 ai-character, 고정 코드층, 제품 그래프를 **7af8f91e5d26e4fd9ce5996809ae41e213340118**로 맞췄다. [사용자의 조명 후속 분리 결정](../../2026-11/2026-11-08-채집이동귀환-조명후속분리.md)을 현재 수용 범위로 연결했다. R1은 해결된 결함이 아니라 후속 과제이며, 이번 구현의 일반 관측은 기존 화면범위다.

이 문서는 그래프 작성자의 반영·생성 확인이다. 그래프 변경분의 독립 검수는 HEAD가 별도로 수행한다. 완료된 구현 검수와 Unity 검사를 반복하지 않았다.

## 반영한 내용

- 최상위 모드 제작·배포 목적에서 확정 채집·이동·귀환 합의로 도달한다. 직접/분류 선택, 곁일 판단 위임, 고정 묶음과 실제 확보, 개인 통행기억, 방향 선행 질문, 막힘, 자동 상위보고, 귀환·후속 작업을 연결했다.
- 사용자 조명후속 결정을 별도 현재 기획·정책으로 연결했다. 과거 검수의 R1 선택대기 문구는 당시 이력으로 남기고 최신 결정이 현재 범위를 설명하게 했다. CPU 근사/오프스크린 렌더 방식을 새로 정하지 않았다.
- 구현과 검증은 참고 노드로 분리했다. `max_count/max_order`는 명시 사용자 조건의 원부탁 소유·수명이며 일반 count 상한이 아니다. 실제 입력·미래 단계 등록·소유 격리의 오프라인 근거를 연결했다.
- 실제 게임행동 그래프에서 조회한 곡괭이·문·다리의 원문 근거는 구현 인계와 연결했다. 당시126판 안내를 고정 사본으로 보존하고, 병행 갱신된 최신 행동 그래프 전체를 이번 구현의 완료 증거로 승계하지 않는다. 게임행동 그래프와 런타임 known 정책은 수정하지 않았다.
- 신규 기획/참고 **20노드·31관계**, 정책2개·기능4개 및 **30개 정적 코드 대응**을 추가했다. 모든 코드 대응은 `runtimeTrace=false`다.

## 변경한 정본

기획연결 `source.mjs`와 생성물을 갱신했다. 새 원문은 [고정 근거 목록](final-source-pins.json)의 정확 사본과 원본 위치를 함께 등록했다. 이후 완료 문서 수정이 이미 수용된 근거를 조용히 바꾸지 않는다.

기존 지식그래프 `기획/정책.md`, `기획/기능.md`, `tools/config.json`에 현재 정책·능력·새 C# 파일을 등록했다. B의 snapshot/runtime-package와 Roslyn/Acorn 코드층, C, 실화면 대응/overlay, 제품 그래프를 기존 생성기로 재생성했다. 생성 JSON을 직접 편집하지 않았다.

실화면 `D/planning.mjs`에는 정확 함수 signature를 찾는 정적 대응을 추가했다. `D/bag-source.mjs`는 [후계 투영](successor-projection.json)으로 동일한 과거 가방 메뉴 blob만 현재판에 대응한다. 과거39 실행증거의 `sameRevision=false`, `evidenceAppliesToCurrentRevision=false`를 유지한다.

적용 전 입력은 준비 시점과 같았고 병행 변경은 없었다. [적용 기록](applied-canonical.json)과 [적용 전 사본](before-final-graph/)을 보존했다. 제품 코드와 사용자 dirty 환경 파일, context.md, 게임/설치본/세이브를 변경하지 않았다.

## 검증 결과

| 확인 | 결과 |
|---|---|
| 공식 소스·B·제품 sourceCommit | 모두7af8f91, baselineDrift.warning=null |
| 고정 snapshot/runtime-package | 355파일 Git blob 일치 |
| Roslyn/Acorn 코드층 | 6,914노드·27,940관계, Roslyn 실제 추출1회 |
| 코드층 재생성 | 동일 raw/JS 입력으로 7개 생성물 바이트 동일 |
| 기획 | 131노드·206관계·80원문, 근거/참조 검사 통과 |
| 기존 근거·C·화면 | strict build/check 및 참조 검증 통과 |
| 제품 | 7,281노드·28,725관계, structuralErrors=[]·stale=[] |
| 제품 재생성 | 바이트 동일 |
| 새 코드 대응 | 30개 signature 유일·Git/snapshot 일치·runtimeTrace=false |
| 대표 실제 조회 | 목적·합의·조명후속·정책·게임근거·구현검수·오프라인·check 13개 exit0 |
| 과거 근거 보호 | 옛 가방 실행/연결 증거를 현재판 성공으로 승격하지 않음 |

원문 명령 출력은 [final-logs](final-logs/), [대표 조회](final-queries/), [최종 확인](final-verification.json), [코드 결정성](final-code-generation.json)에 있다. 최상위 목적에서 신규 노드 전부로 도달하며 근거 사본 해시 불일치는 없다.

## 조회 진입점

`work/지식그래프`에서:

```powershell
node tools/product.mjs intent P.expedition
node tools/product.mjs intent P.expedition-light-followup
node tools/product.mjs source P.expedition-light-followup
node tools/product.mjs show P.expedition-bundle
node tools/product.mjs show P.expedition-report
node tools/product.mjs show R.expedition-implementation
node tools/product.mjs show R.expedition-final-review
node tools/product.mjs show R.expedition-offline
node tools/product.mjs check
```

그래프는 실제 코드·합의·오프라인 검증의 색인이다. 새 게임 실행, 모델 선택 품질, 실제 네트워크·충돌·회수·상자 소비, 조명까지 사람과 같은 시야를 입증하지 않는다. 이 작업의 실게임/외부 모델/설치는 기존 후속 검증 범위다.
