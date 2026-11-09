# 길 계산 최적화의 공식 지식그래프 반영

2026-11-08. 공식 ai-character, 고정 코드층과 제품 그래프를 `43c3817fc1a36142c8591fb087ed867665633fe1`로 맞췄다. HEAD의 대표 실기 수용·공식 fast-forward 판정과 최종 실기·설치 보고를 고정 근거로 반영했다.

## 반영한 내용

- 기존 임의 제약 해제와 후보 선택권 보존 합의에서 길 계산 최적화로 연결한다. 후보 준비의 프레임 분산, 결과 재사용, 공유 검색과 전송 전 관측 검증을 실제 코드에 연결했다.
- 기존 생활·가방·채집이동 트리의 존재와 감사 결과를 기록했다. 이번 수정은 기존 원정 트리의 진입·leaf 선택면과 상세 경로 평가를 연결한다. 눈앞 채집 묶음, 현재 행동과 명시 목적지의 직접 실행, 다른 대상·장소의 얕은 탐색, 전체 대상 접근 가능성을 유지한다.
- 1차2a0112d의147.9초 준비 취소와 별도258개 요청의 HTTP400을 실패 근거로 보존했다. 해당 후보의 오프라인 수용을 실기 성공으로 승격하지 않았다.
- 2차43c3817은 갑옷 계획 수신→29개 선택지 SEND→정상 APPLY→실제 이동이라는 대표 흐름을 확인했다. SEND31회·응답 파싱31회·RESULT29개, HTTP 실패0, 후보19~56개, START18회·DONE12회를 정확히 기록했다.
- 장애물 변경 취소26회와 약9~18초 재준비 지연,118.4~120fps 구간, 초기 작업 조각45.65ms를 함께 보존했다. 3ms는 공유 목표 예산이다. 실제 게임의 모든 프레임 상한, 원자 세계 snapshot, 동률 최단경로 타일 순서의 동일성을 보장하는 표현을 사용하지 않았다.
- 7,526개 대상의 전체 메뉴 탐색은 실게임에서 선택되지 않았다. 전수 leaf 접근은 오프라인 근거다. 갑옷 최종 제작·전달과 채집 확보도 이번 실기에서 확인되지 않았다. 직접 대응만255개를 넘는 선택면은 진단 후 전송을 중단하는 경계가 남는다.

## 실제 변경한 정본

기획연결 `source.mjs`에 신규8노드·11관계를 반영하고 기존 생성기로 다시 만들었다. 원문 위치와 고정 사본 해시는 [근거 목록](graph-stage/final-source-pins.json)에 있다. 새 근거는 구현 최종본, 독립 검수, 기존 트리 감사, 두 차례 실기, HEAD 수용·통합 전달과 설치·보존 검증이다.

기존 지식그래프 `기획/기능.md`에서 변경된 `CandBuild.*Steps` 앵커를 보정하고 경로 준비 기능을 추가했다. 기존 선택권·제약 해제·원정 정책을 재사용했다. `tools/config.json`에 새 생산 파일을 등록했다.

B의 Git blob snapshot/runtime-package와 Roslyn/Acorn 생성물을43c3817로 갱신했다. 동일 SHA 격리 추출에서 검증한 결과를 재사용했고 공식 위치에서도 verify를 통과했다. C와 화면 대응·overlay를 다시 만들었다. 실화면 D에는11개 실제 함수 signature의 정적 대응을 추가하고 기존 가방 후보 대응의 바뀐 함수명을 보정했다.

가방 메뉴의 과거39 소스 blob이 현재판과 같음을 확인해 후계 투영을 갱신했다. 과거 실행 증거의 `sameRevision=false`, `evidenceAppliesToCurrentRevision=false`와 모든 새 코드 대응의 `runtimeTrace=false`를 유지했다. README와 제품 사용법의 현재 기준도43c3817로 맞췄다.

정본은 Git 저장소 밖에 있으므로 적용 직전15개 입력의 SHA를 준비본과 대조했다. 병행 변경은0이었다. [적용 전 사본](graph-stage/before-final-graph/)과 [적용 기록](graph-stage/applied-canonical.json)을 보존했다. 별도 활성 그래프 대화의 작업 영역과 제품 코드는 편집하지 않았다. HEAD가 갱신한 최신 todo 원문도 최종 생성에 반영됐다.

## 검증 결과

| 확인 | 결과 |
|---|---|
| 공식 Git HEAD·B·제품 sourceCommit | 모두43c3817 |
| 고정 snapshot/runtime-package |357파일 Git blob 일치 |
| B 통합 코드층 |7,529노드·29,490관계 |
| 순수 코드층 |7,438노드·29,293관계 |
| 동일 raw 생성 |7개 생성물 바이트 동일 |
| 기획 |139노드·217관계·94원문 |
| legacy strict·check |원본·인용·앵커·ID 검사 통과 |
| C·화면 대응 |통합·참조 검사 통과 |
| 제품 |7,813노드·30,100관계 |
| 제품 check |구조오류0·stale0·baseline drift 없음 |
| 제품 재생성 |바이트 동일 |
| 핵심 조회 |목적·합의·1차 실패·기존 트리·구현·검수·실기·통합·정책 등14개 exit0 |
| 실제 함수 대응 |11개 signature 유일·Git blob/snapshot 일치·정적 대응 |
| 신규 근거 연결 |신규8노드 모두 최상위 목적에서 도달·고정 근거 해시 불일치0 |

[최종 검사](graph-stage/final-verification.json), [생성 로그](graph-stage/final-logs/), [실제 조회 출력](graph-stage/final-queries/), [격리 코드 결정성](graph-stage/shared-verification.json)에 원문 결과를 보존했다.

## 설치와 보존 상태

실기 담당이 게임을 정상 종료하고 시험 전 설치본·AI자료·세이브·remote·레지스트리·시험스위치를 먼저 복원해 차이0을 확인했다. 그 뒤 HEAD 지시에 따라 모드만43c3817로 최종 설치했다. BuildAndInstall exit0이며116개 소스 SHA가 설치본과 일치한다. AI자료53개·localsteam268개·remote49개·레지스트리·시험스위치는 시험 전과 같다. 모드만 승인된 최종판으로 바뀌었다. 최종 재설치 후 게임은 다시 실행하지 않았다. 이 그래프 담당은 게임이나 설치본을 조작하지 않았다. [최종 실기·설치 보고](live-validation.md)

## 조회

`work/지식그래프`에서 다음 명령으로 원문과 실제 구현 위치를 확인한다.

```powershell
node tools/product.mjs intent P.path-optimization
node tools/product.mjs source P.path-optimization
node tools/product.mjs show R.path-tree-audit
node tools/product.mjs show R.path-implementation
node tools/product.mjs source R.path-live
node tools/product.mjs show R.path-integration
node tools/product.mjs check
```

이 문서는 정본 작성자의 반영·생성 검증 기록이다. 코드·오프라인 독립 검수와 HEAD의 대표 실기 수용은 각각 고정한 별도 원문에 귀속된다. 그래프 delta의 최종 확인은 호출한 HEAD가 수행한다.
