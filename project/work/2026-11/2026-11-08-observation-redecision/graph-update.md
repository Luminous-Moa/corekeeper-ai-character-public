# 관측·재판단 흐름의 공식 지식그래프 반영

2026-11-08. 공식 ai-character, 고정 코드층과 제품 그래프를 `20b1aced7594a47eacb4be060405bcb53d38f48f`로 맞췄다. 현재 설치·실행판은 `09e418a8af85bd1578b2cfea09cdbe3c0180db77`이며 사용자 게임 종료·시험 자료 복원·최종20b1aced 설치 승인을 기다린다.

## 반영한 계약과 구현

사용자의 후속 완료 확인 요청을 기존 AI 계속·중단·전환 권한과 원래 부탁 보존 계약에 연결했다. 측정 조건이 있는 finish_goal은 적용 직전 사실을 다시 확인하고 부족·미확인이면 단계와 미완료 의무를 유지하며 판단을 다시 호출한다. 비계량 목표의 판단 종료와 stop_action/request_plan 권한을 유지한다.

관측의 목표 세대·이름·대상·실행 회차 소속, 실제 몸 Tick에서 갱신된 pending 재검토를 연결했다.5초는 같은 대기 사실의 구현 재검토 주기이며 기존 호출·비용 가드를 따른다. 새 사건·이미 진행 중인 요청·준비·미소비 관측의 수명도 보존한다.

Give는 sender 표식·물품 신원·출발 슬롯 감소·새 유일 floor를 귀속하고 같은 floor의 지정 recipient pickup·실제 소비·수령자 증가를 대조한다. 수령자 수량을 매 Tick 읽고 미측정을 표시하며 이미 송신한 drop을 자동 반복하지 않는다.

추가20b1aced는 새 set 계획 전체가 거절되고 현재 활성 계획이 운반하는 같은 미완료 원부탁에 done이 함께 온 경우를 적용 전에 보류한다. 목표·몸·계획·원부탁을 유지하며 실제 모순을 상위에 재보고한다. 무관한 done, defer/drop/answered, 유효 새 계획과 비계량 마지막 목표의 권한을 유지한다. 이 추가 경계는181개 생산 검사·독립 검수·Unity 수용 근거다.

## 실기 귀속과 남은 경계

09e418a 실기에서 투구·갑옷·바지 각각1개의 정확한 수령과 다음 단계 진행, 마지막 finish→plan_done→원래 부탁 done을 확인했다. 각 전달은 drop1회, 지정 floor와 pickup·소비·호스트 증가가 함께 기록됐다. 바지 미확인 finish의 반복은 의무를 보존하며 약5.3초 간격 재판단 뒤 실제 전달로 이어졌다.

조용한 통로에서는 방향 필요 관측→모델 방향 선택→이동을 확인했다. continue 유지 후5초 재호출 조합과 floorNull 빠른 pickup은 실기 미관측이며 오프라인 근거에 한정한다. 추가 상위 완료 경계를 포함한20b1aced 전체의 실기 성공으로 확대하지 않았다.

09e418a와20b1aced의 AIInventory·AISkill·AITravel Git blob 동일성을 확인했다. 제품의 새 실기 참고에는 `sourceCommit=09e418a`, `currentSourceCommit=20b1aced`, `sameRevision=false`, `evidenceAppliesToCurrentRevision=false`를 기록했다. 정적 대응은 모두 `runtimeTrace=false`다.

사용자의 이후 지연·왕복은 [별도 분석](latency-loops/report.md)을 따른다. 이번 관측 수정이 그 증상을 해결했다는 완료 기록을 추가하지 않았다. 이 작업에서 추가 이동 코드를 수정하지 않았다.

## 변경한 정본과 보존

- 기획연결 source.mjs에 신규7노드·11관계를 추가하고 R.finish-goal에 현재 후속 사실 검사와 기존 계약의 귀속을 보완했다.
- 기존 정책에 완료 관측 대조·재판단 정책을 추가했다. F.action-select, F.plan-queue, F.executor, F.common-execution, F.bag-give의 설명과 실제 함수 앵커를 갱신했다. 원래 공통 계약 문서는 당시 원문으로 보존했다.
- B snapshot/runtime-package를20b1aced Git blob으로 pin하고 Roslyn/Acorn을 추출했다. C·화면 대응·overlay와 제품 그래프를 기존 생성기로 다시 만들었다. 새 observer와 완료·재보고 소비자17개 signature를 정적 대응했다.
- ObserveExecution이 두 overload로 나뉜 데 맞춰 legacy 앵커를 /1·/2로 보정하고 제품의 기존 공통 입구는 정확한 string 단일 인자 signature에 연결했다.
- 과거 가방39 증거의 같은 menu blob을 확인하고 기존 `sameRevision=false`와 현재판 미적용 경계를 유지했다. README·제품 사용법의 현재 기준과 설치 대기 상태를 맞췄다.

적용 직전 정본15입력 SHA의 병행 변경은0이었다. [적용 전 사본](graph-stage/before-final-graph/)과 [적용 기록](graph-stage/applied-canonical.json)을 보존했다. 제품 코드·사용자 dirty 환경·게임·설치본·세이브는 이 담당이 변경하지 않았다. 최신 todo를 최종 생성에 포함했다. 원문과 고정 사본 해시는 [최종 근거 목록](graph-stage/final-source-pins.json)에 있다.

## 검증

| 항목 | 결과 |
|---|---|
| 공식 Git HEAD·B·제품 기준 |모두20b1aced |
| snapshot/runtime-package |357파일 Git blob 일치 |
| B 통합 코드층 |7,578노드·29,749관계 |
| 동일 raw 재생성 |7개 생성물 바이트 동일 |
| legacy strict/check·C·화면 |모두 통과 |
| 제품 |7,870노드·30,388관계 |
| 제품 check |구조오류0·stale0·baseline drift 없음 |
| 제품 재생성 |바이트 동일 |
| 실제 핵심 조회 |13개 exit0 |
| 정적 앵커 |17개 유일·Git blob/snapshot 일치 |
| 신규 근거 |7노드 모두 최상위 목적에서 도달·고정 사본 해시 불일치0 |

[최종 검사](graph-stage/final-verification.json), [생성 로그](graph-stage/final-logs/), [조회 출력](graph-stage/final-queries/), [09e418a 소스 대응](graph-stage/tested-source-correspondence.json)에 결과를 보존했다.

```powershell
node tools/product.mjs intent P.observation-completion-check
node tools/product.mjs show R.finish-goal
node tools/product.mjs show R.observation-upper-completion
node tools/product.mjs source R.observation-live
node tools/product.mjs show R.observation-integration
node tools/product.mjs check
```

현재 완료한 것은 공식 코드 정본 반영과 위 범위의 생성 검증이다.09e418a 게임은 사용자 조작 상태로 인계되어 있으며 정상 종료·복원·최종20b1aced 설치는 승인 대기로 남아 있다. 설치 상태가 확정되면 해당 담당의 새 증거로 갱신한다. 그래프 delta의 최종 확인은 호출한 HEAD가 수행한다.

사용자 마감 요청 뒤 HEAD가 추가한 후속2건의 최신 todo를 기획·제품 생성에 다시 반영했다.20b1aced 기준에서 구조오류0·stale0·기준판 일치와 제품 재생성 바이트 동일을 재확인했다. 신규 노드와 분석 범위를 추가하지 않았으며 설치 보류 상태를 유지했다.
