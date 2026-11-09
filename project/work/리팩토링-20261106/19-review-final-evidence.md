# 최종 추가 실행 근거 적재 독립 검수

2026-11-06. 16 그래프 한정검수 이후 staged L/C/refactor-20261106의 추가 관측과 D/integrate.mjs의 supplementalRuntimeObservations 적재를 검토한다. 코드 기준은 902721e. canonical·staged·제품·설치본은 수정하지 않았으며 게임 조작도 하지 않았다.

## 준비본 검토

최종 재생성 전 읽은 준비본은 status=awaiting-final-live-report다. 이 단계에서 최종 해시·정합성 통과를 선언하지 않는다. 최종본 확인은 아래 후속절에 남긴다.

- 추가 관측은 자연어 place/craft/다음 목표, 복수 캐릭터 목표교체/all-stop, 자기 문 통과, 정상 저장/완전 종료/재입장, 원인 미확정 DOWN을 각각 독립 항목으로 둔다. developmentCapabilities는 관련 능력의 참조이며 개발 확인한 선을 갱신하는 기능이 아니다.
- 실제 모델을 사용한 제작/놓기와 시험명령으로 재료·문 장면을 준비했다는 경계를 함께 적는다. arena의 체력/무기 지급 부작용을 숨기지 않으며, 최종 가방에서 CopperSword 대신 WoodSword인 이유를 저장 유실로 판단하지 않는다.
- 저장/재입장은 15필드×2명 30개 비교 및 추가 지급 없이 복원 문을 사용한 관측으로 한정한다. 모든 영속 필드·모든 재입장 상황을 검증했다고 확대하지 않는다.
- DOWN 항목은 observed-cause-unresolved-no-new-refactor-blocker-identified이며 relatedProductFeatures=[]다. 18의 한정 회귀 검토를 연결하고, 폭발벽은 원인 후보일 뿐 확정/정상 결과/안전 정책 성공이 아니라고 명시한다.
- 준비본의 재입장 증거 및 삭제/복원 진행 상태는 최종15가 확정되면 다시 동기화해야 한다. FoldRemote 입력 장애에 따른 시험월드 삭제 미완료를 성공으로 쓰면 안 된다. 환경 복원 결과와 게임 내부 삭제/Cloud 정리 성공도 별도로 다룬다.

## 적재 변경 범위

integrate.mjs를 canonical과 직접 diff했다. 추가 적재 관련 변경은 입력 schema/commit/hash 검증, 개발 ID 존재 검사, 기존 제품 기능 ID의 supplementalObservationIds, 별도 g.supplementalRuntimeObservations, 별도 validation.supplementalRuntime 및 역사적 미관측 문구의 범위 설명이다. 앞서 수용한 Chat partial의 staticEntry 경로 수정은 별도 기존 변경이다.

추가 적재는 새 함수 trace·제품 정책/기능 노드·간선을 만들지 않는다. 각 제품 ID가 실제 기능인지 검증하고 기존 slash:ai에 관측 항목 ID를 붙인다. baseline 그래프는 structuredClone으로 복제한 뒤 메모리에서 보충하며 원래 생성물을 덮어쓰지 않는다.

과거 liveScreen/D1–D5·76563bf 설치 관측, e82 코드 추론은 그대로 남고, 새 데이터는 902721e의 별도 추가 관측 층이다. sourceCommit 일치와 입력 snapshot hash 불일치를 검사하므로 다른 코드판 또는 변한 입력이 조용히 적재되는 경로는 없다. 개발 confirmed-line이나 기존 역사적 observation status를 일괄 변경하지 않는다.

현 준비본의 정책 범위 확대나 DOWN 성공 승격은 발견하지 못했다. 최종 재생성 통보 뒤 입력 동기화·최종 hash·읽기 전용 정합 검사로 마무리한다.

## 최종 재생성 후 판정

graph_update의 최종 통보 뒤 동기화된 관측/15/18과 D 그래프·validation을 다시 확인했다. **추가 증거 적재 검수 통과, must-fix 0.** 이 판정은 제품의 모든 상황 통과, 원인 미확정 DOWN의 성공, 시험월드 정리 완료 또는 공식 그래프 반영 승인을 뜻하지 않는다.

- observations.status는 `final-report-synced-cloud-trial-world-deletion-outstanding`이다. 4개 능력 항목은 observed이며 DOWN 한 항목은 `observed-cause-unresolved-no-new-refactor-blocker-identified`다. DOWN은 제품 기능의 성공 관측 ID에 포함되지 않는다.
- 최종15의 §3.2 재입장/문 재통과, §4 복원 및 남은 정리 1건이 동기화되었다. movetrace CSV가 없었음을 정정하고 문 관측 근거를 실제 Player 로그로 한정했다. 준비본의 원본 로그 대조/재입장 시각 미반영 상태는 최종 결과로 갱신됐다.
- 입력 14개의 staged 바이트 SHA-256과 기록 hash, originalPath의 실제 원문 바이트를 각각 직접 비교했다. **14개 모두 일치**하며 누락/변경 0이다. 15/18 역시 작성자 통보 SHA와 일치한다.
- D 그래프의 supplementalRuntimeObservations는 sourceDocument 필드 외에 observations.json과 깊은 구조 비교로 같다. JSON key 정렬은 내용 변경으로 세지 않았다.
- D.meta.sourceCommit은 902721e, installedSourceCommit은 역사적 76563bf다. validation.supplementalRuntime.functionTraceObserved=false이며 기존 e82 추론/765 화면층을 현재 실행 전체 검증으로 바꾸지 않았다.
- 새 RF902 ID를 포함하는 실행/기능 간선은 0이다. 기존 slash:ai의 supplementalObservationIds에 4개 관측만 추가되고, DOWN은 그 배열에서 빠져 있다. 새로운 기능/정책 ID나 자동 “확인한 선” 승격은 없다.

## 최종 증거 표본 직접 확인

`Player-first.log`의 t659.4 all-stop에서 두 멤버의 목표 해제와 t659.5 Gemini 터널 CANCELLED를 직접 확인했다. t659.4 이후 첫 프로세스 종료까지 `DECISION #n SEND`를 시각 조건으로 검색한 결과 0건이다. 모든 늦은 응답/취소 조합을 검증했다는 주장은 하지 않는다.

`Player-second.log`의 t238.6 재입장 후 걷기 시작, t240.0 자기 WoodDoor(0,-5) 감지, t241.9 열림/닫힘 각각 누름1회, t242.4 목적지 도착을 직접 확인했다. 추가 설치 없이 복원된 문을 사용했다는 최종15의 관측과 일치한다. 기록된 3.8초와 replan1도 그대로 유지한다.

첨부 persistence-comparison.json은 Gemini/GPT 각각 15개 항목, 총30개 모두 same=true다. 입력 파일의 원문 동일성을 확인했으며 이 보고가 실제 저장 필드 전체를 새로 대조하거나 모든 필드의 영속성을 독립 재시험한 것은 아니다. 30필드 비교의 환경 실행·snapshot 대조는 15의 별도 증거 범위다.

환경 정리는 **17:23 기록 시점**의 로컬/registry 복원 verify 0 difference groups와 미완료 게임 내/Steam Cloud 삭제를 분리한다. mod54/AIPlayers43/localsteam248/remote40 복원, 실제 사용량 보존은 환경 보고와 첨부 restore 증거의 주장이다. Cloud에는 시험월드 파일 Upload OK가 남아 로컬 파일 부재를 서버 삭제 증거로 쓰지 않는다. 후속 삭제 재시도 착수는 현재 frozen 관측이 기록하는 당시 프로세스 종료/복원 사실을 무효화하지 않으며, 그 재시도의 성공 여부는 이 판정에 포함하지 않는다.

## 최종 정합과 해시

읽기 전용 product check를 직접 실행해 **exit 0, ok=true, structuralErrors=0, stale=0**을 확인했다. canonical-before의 434개 원본 SHA도 다시 대조하여 변경0을 확인했다. live-final-command-results의 strict/query/C 참조/진입점/product check 및 byte-identical은 저장된 실행 출력으로 확인했다. 불필요한 전체 재추출·게임·시험은 반복하지 않았다.

| 최종 산출물 | SHA-256 |
|---|---|
| observations.json | `c162308e0caf53f8780684d8a6860e4659db538e98fb849fc1cd84cb3c419751` |
| staged 15-unity-and-live.md | `dc2e03715a5a26fbc25c8c127b1316994a90d67b3565efd3decf7627ffa9043b` |
| staged 18-review-live-observation.md | `fc1f958fe57a2ef4ae42a0b525250c220df549f04a80dbb080edd24540efffe5` |
| L/D/graph.json | `8bb20f6c1bc2f20d88e604ccc10a29f56d81a58b7b288493ecfdb0827262f3a2` |
| L/D/validation.json | `bc656a593e62a60a3530cff3cd484345da57517c92b3e88536d6245d94f0a4f3` |

## HEAD 반환

최종 제품 관측과 추가 그래프 적재는 위 고정 증거 범위에서 수용 가능하다. 남은 시험월드 UI/Cloud 삭제의 후속 결과가 생기면 그 추가 기록만 대조하면 된다. 지금의 삭제 미완료와 원인 미확정 DOWN을 삭제하거나 성공으로 바꾸는 승인으로 사용하지 않는다.

## 사용자 중단 뒤 환경 후속 최종 확인

15 §5.1 및 graph_update의 최소 동기화만 확인했다. **후속 적재 수용, must-fix 0.** 제품/정적 전체 검수나 게임 실행은 반복하지 않았다.

입력이 19개로 늘었으며, 원문/staged/hash를 직접 대조해 모두 일치했다. D의 추가 관측 payload도 원본 observations와 같다. 제품 관측의 validation 해시는 이전과 동일하고, 원인 미확정 DOWN의 지위는 유지된다.

17:36:28 재복원 로그의 mod54/AIPlayers43/localsteam248/remote40/registry 동일 및 verify0을 직접 읽었다. MouseKeys 전후 JSON은 Flags=62, MaximumSpeed=80, TimeToMaximumSpeed=3000, CtrlSpeed=null로 같다. 이 4개 설정 동일성과 NumLock의 사전 실제 토글 상태 미관측을 구별하며, NumLock까지 원상태라고 주장하지 않는다. 버튼 DOWN/놓기 미실행, 재시도 중 제품 테스트/월드 입장 없음, FoldRemote 정상 끊음이라는 15의 기록과 supplemental finalFollowup 표현이 일치한다.

시험월드 삭제는 여전히 성공하지 않았고 Cloud 재다운로드가 관측됐다. 사용자가 삭제를 별도 작업으로 넘기고 마무리하도록 결정한 사실을 추가했을 뿐, 삭제 성공/전면 환경 상태 동일로 바꾸지 않았다. 이 기록을 최종 상태로 인계한다.

최종 후속 SHA-256 직접 확인:

- observations: `b958c27b8f4428665882c952aaee4345f3047b75f44dfb5a0ca2a87bab55b40d`
- staged 15: `5b09a387725610760ae97d344ce193c82c35270c5e54b5e5e2fa0f5ec2fa2409`
- D graph: `3e31724bd4df16681b6302524a73737ed977695cb6efc496a68114f7176bcef0`
- D validation: `bc656a593e62a60a3530cff3cd484345da57517c92b3e88536d6245d94f0a4f3`(이전 동일)

cleanup-followup-checks.json의 product check 오류0/stale0, byte-identical, canonical434 변경0은 작성자의 저장된 최종 결과로 확인했다. 이 짧은 후속에서는 그 전체 검사를 다시 실행하지 않았다.
