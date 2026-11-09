# 가방·장비 및 생활 통합 그래프 최종 staging

2026-11-07. **최종39b22df의 staging 생성·검증 완료. 한정 독립 출처 검수까지 완료했다. 공식 그래프 적용은 ROOT의 명시 작업 대기다.** 코드와 승인된 main HEAD는 `39b22df96dffe869ab0ccd474b18d31462b71d70`이며 기획/공식 G/P/L 입력 기준은 c4a0f5e다. 이 그래프 작업자는 공식 G/P/L·main·설치·게임·외부 모델을 쓰거나 실행하지 않았다. main 소스의 ff-only는 별도 HEAD 승인 작업으로 이미 완료됐다.

## 보존과 실제 연결

- 최신 공식c4의 Psource 앞부분을 바이트 그대로 보존했다(SHA256 36bcf3936ecd9cdf4bbbb7887161a326dd69459e45e0dda1ec89f4321f3e5696). 모든 기존 기획 노드 의미와 관계, 최상위 P.release-goal, common8·life12 코드 대응을 실제 통합39 코드에서 확인했다. 구741/685 사본을 공식 위에 덮지 않았다.
- 가방은 분류→정확한 보유 물품→조작→인자→leaf, 뒤로/선택 전체 취소, 무페이지·무상한이다. 생활의 별도 승인된 lossless paging·읽기전용 held/queued·known-target 직접실행·작업 수량/진척/Runs는 보존한다. 두 소비자와 후보 개수 cut·물리/탐색/보고/기록/호출 예산을 R.bag-life-consumers에서 구별한다. HEAD 통합 판단은 사용자 직접 원문으로 승격하지 않았다.
- P.bag-item-first/navigation/no-cut → 실제 메뉴·공개surface/BuildJson·Apply·MenuMoves/InventorySkill, 기존 F.life-tree/F.action-select/F.plan-queue와 여섯 F.bag primitive·F.bag-navigation을 연결한다. 생활 storage의 Candidates/CandidatesFor는 현재 직접실행 binding에 쓰이므로741의 dead 분류를 철회했다.
- R.bag-validation → 최신 통합 검수·자체 Unity/sourceSDK·HEAD 한정 수용/소스 반영 원문으로 연결한다. c4의 기존 생활 사실과 과거 실행을 보존하면서 현재 code/sourceCommit과 역사 evidence를 구별했다. 생성 graph JSON은 직접 편집하지 않았다.

## 실제 검증

| 항목 | 결과 |
|---|---|
| 고정 소스 | Git blob327파일 일치, C#104(제품103+기존Editor1) 등록 |
| 추출 | Roslyn5.9/Acorn8.15, 새 추출2회의7출력 바이트 동일 |
| 생성/check | 기존13단계 모두 exit0. 개발 strict/check, 기획·screen 참조, 제품 source/hash/check·재생성 바이트 동일 |
| 교차 조회 | 56단언·53조회 통과; c4 전 기획/관계·release/common/life·두 소비자·코드/근거 경로 |
| 개발 graph | 3079노드/27887연결. 선밖107→95, 신규선밖0. 새 기록미결은 U.bag-live를 유지 |
| 제품 graph | 6714노드/26029연결 |
| 근거 | 원문/로그/fixture 58개 raw SHA256, 연결 소스14개의 raw 입력과 Git blob 텍스트 대응 |

생산자192/192와 독립199/199를 합산하지 않았다. 최신 compile0/0·Loop55·생활96과 broad7dc Core1298/protocol154/menu348/cook51/craft19/inventory91의 실제 불변 대조를 구별한다. 로컬 요청32/탐색30은 실제 외부모델·게임consumer·완전한Tick이 아니다. [단독 메뉴121표본](graph-stage/work/2026-11/2026-11-07-가방장비구현/menu-examples.md)과 [통합 요청/결과 표본](graph-stage/work/2026-11/2026-11-07-가방장비구현/connected-menu-examples.md), fixture/source pin을 보존했다.

자체39 Unity exit0/CompileCheck:ok/CS오류0와 설치없는SDK export를 원본 해시로 고정했다. payload는103 C#와manifest(104파일)이며 compilerDLL/바이너리릴리스/런타임설치와 다르다. producer/export raw103일치, 독립 raw102+EOL-only1이다. main 반영도 raw102+Loop 줄끝만1로 기록하며 원시 해시를 정규화해 덮지 않았다. ZIP707242bytes/SHA256 D8D8E891BFD2CAAEDBA34671958DBFFA21C125BC38246D5E8EA346381D3E107A. 실제 근거는 [통합 보고](integration-report.md), [독립 검수](review-integrated.md), [수용 판단](source-acceptance.md), [main 반영](source-integration.md)에 있다.

## 최소 적용물과 경계

[적용 계획](graph-stage/패치-적용경계.md)과 [활성 원본 패치 인덱스](graph-stage/원본-패치목록.md)의 23개 항목/baseSha256/stagedSha256만 검토한다. base 입력 939개 중 복사 후 원문 변화는 history.md이며 공식 저작 입력의 의미 드리프트는 없다. 승인된 main c4→39 이동은 별도 수용 기록으로 처리했다. history/context를 역복사하지 않는다.

공식 적용 때 repo/codeRoot를 공식 main39 및 공식 B/snapshot으로 바꾸고 격리 README 머리말을 적용 현재 상태로 고친 뒤 원본에서 재생성해야 한다. 생성 JSON 통째 복사로 검증을 대신하지 않는다. 단계·명령·대표 조회는 command-results/query-results/evidence와 적용 계획에 있다. source-patches의 이전 번호 잔여물을 통째 적용하지 않는다.

741의 코드/graph/package와 불변 review-tree-741,774·1fc·b1·7dc/b1e 추출은 revisions/ 및 원래 산출물에서 역사적으로 보존했다. UI11eb461/리팩토링902721e/화면76563bf의 실행 관측은 원래 sourceCommit이며 현재39의 runtime 관측으로 승격하지 않는다. 이전 official-drift는741 보존본에 남겼고 현재 판단은 rebase-baseline.json과 stage-audit.json을 따른다.

마지막 순서는 검수자 graph 부록1회 추가→그 원문 한 번 재핀/재생성/check→검수자 읽기 확인이다. 설치·bridge 재시작·외부 모델·실게임은 기존 보류를 유지한다. U.bag-live와 [별도 실제 소비자 계획](integration-runtime-plan.md)을 남겼으며 전체 사용자 플레이 성공을 완료로 선언하지 않는다.
