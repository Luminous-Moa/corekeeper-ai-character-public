# 선택지 개선 격리 그래프 준비

2026-11-06. 코드 후보 **b25099f6fd31e4c4c3618357eb9e8101e0e17124**의 개발·제품 그래프를 `graph-stage-b25099f/`에 준비했다. 공식 그래프와 `ai-character/902721e`는 보존했다. 이 단계는 코드 연결과 과거 근거의 경계를 갱신한 것으로, 현재판의 실게임 성공을 새로 주장하지 않는다.

## 결과와 검사

| 항목 | 결과 |
|---|---|
| 개발 G strict build / query check | 통과 |
| 개발 노드 / 간선 | 2,617 / 21,426 (독립 검수 앵커 정정 뒤) |
| 코드 snapshot / runtime-package | 후보 SHA의 Git blob 원바이트로 생성, verify 통과 |
| Roslyn / Acorn 추출 | 통과, 새 helper 포함 |
| 코드 추출 결정성 | 독립 2회, 추출 파일 7개 모두 byte-identical |
| P/C 및 L/C 참조 | 통과 |
| 제품 통합 / 질문 / check | 통과 |
| 제품 노드 / 간선 | 5,831 / 22,379 |
| 제품 재생성 --check | byte-identical |
| 공식 원본 보존 | 복사한 canonical 파일 454개 SHA256 변화 0 |

기존 `stage-graphs.mjs`, 리팩토링 14/16/21 기록과 현행 canonical extractor를 재사용했다. 새 helper의 연결은 `F.candidates`, `F.skill-mine`, `F.skill-dig`, `F.tunnel-detour`에 한정했다. 기능 ID·제품 정책·과거 확인한 선을 새 검증으로 바꾸지 않았다. 경로 선택 점수나 실행 동작 수정으로 기술하지 않았다.

독립 검수 정정: `F.skill-goto → AIMoveEstimate.Seconds` 함수 앵커는 제거했다. GotoSkill은 이 함수를 호출하지 않고 AIBody.WalkSpeed 상수를 참조한다. 공유 상수 관계를 함수 실행/처리 관계로 표시하지 않는다. `prepare-graphs.mjs`와 staged `기획/기능.md`를 함께 고쳤고, G strict build/query check 및 제품 통합/check/동일재생성을 다시 통과했다. 코드·Git snapshot·Roslyn 원자료는 바뀌지 않았다. 정정 로그는 stage `anchor-fix-build.txt`, `anchor-fix-check.txt`, 최신 `finish-commands.json`이다.

새 연결 대상은 `AIPickupOptions.Select`, `AIMoveEstimate.Seconds`, `AIWallData.RiskText`, `AIWallRisk.Text/RouteText`, `TunnelPlan.RiskText`다. 속성 EstimatedSeconds도 고정 코드 snapshot과 Roslyn 추출의 원본에서 확인 가능하다. 개발 G는 후보 worktree를, 제품 P/B는 고정 Git blob을 읽는다.

## 과거 실게임 증거와 현재판의 구분 — 독립 검수 대상

기존 L/D 통합기는 `C/refactor-20261106/observations.json`의 sourceCommit이 코드 그래프와 다르면 무조건 실패했다. 이번 후보는 새 코드 b25099f이고 해당 실게임 관측은 과거902721e이므로, **관측의 SHA를 바꾸지 않고** staged 통합기에 역사 근거를 명시하는 분기를 추가했다.

변경 파일과 정확한 차이는 다음과 같다.

1. staged `C/overlay-source.json`: `baseCommit=b25099f...`와 별개로 `historicalRefactorObservationCommit=902721e...`를 명시했다. `installedSourceCommit=76563bf...` 등 기존 화면 설치 경계는 유지했다.
2. staged `D/integrate.mjs`: 기존 `관측.sourceCommit == 현재코드.sourceCommit`만 허용하던 guard를 `현재 SHA와 같거나, overlay가 명시한 역사 관측 SHA와 정확히 같음`으로 바꿨다. schema 검사, 근거 입력파일 SHA 검사, capability/feature 참조 검사는 그대로 수행한다.
3. 같은 통합기에서 `supplementalRuntimeObservations` 및 validation.supplementalRuntime에 `currentSourceCommit`, `sameRevision`, `evidenceAppliesToCurrentRevision`를 추가했다. 실제 출력은 **관측.sourceCommit=902721e, 현재=b25099f, sameRevision=false, evidenceAppliesToCurrentRevision=false**이다.
4. 준비 절차 `finish-graphs.mjs`는 허용할 역사 자료를 임의로 받지 않는다. `sourceCommit == 902721ec505df42d74d0b2d245ed34a049faadad`이며 관측 JSON 원바이트가 canonical의 해당 자료와 완전히 같아야 통합 준비를 진행한다. 기존 19개 실행 증거 입력의 SHA도 통합기에서 검사한다.

따라서 최초 product build 실패는 기록에 남아 있으며, 관측을 현재판으로 승격하거나 검사 전체를 끄지 않았다. 새로운 현재판 실게임 증거가 생기면 별도 범위의 관측으로 붙여야 한다. 이번 stage에는 아직 그 증거가 없다. 공식 적용 전에 이 역사 경계 변경은 독립 검수 대상이다.

추출기/통합기 소스 변경은 이 task의 stage 사본에만 존재한다. 공식 tools/lib.mjs·query.mjs·integrate.mjs 등을 덮어쓰지 않았다. 개발 G의 stagedDocumentRoot는 사본 문서 해시 조회용이며 공식 적용 때는 제거/공식 resolver 복원 절차가 필요하다.

## 산출물과 재생성

- [준비 스크립트](prepare-graphs.mjs): 현재 후보 커밋별 새 stage를 만든다. 기존 stage가 있으면 덮어쓰지 않고 중단한다. 공식 세 뿌리를 복사하고 canonical-before.json을 만들며, 앵커·소스 SHA를 맞춘 뒤 추출한다.
- [역사 경계 및 통합 마무리](finish-graphs.mjs): 지정된 task stage만 받는다. 위 역사 자료 동일성 검증과 최종 통합·check·동일재생성을 수행한다. prepare 스크립트가 현재 이 단계를 호출한다.
- stage `commands.json`: G/P/L 초기 명령과 전체 출력. 최초 product guard 실패도 보존했다.
- stage `finish-commands.json`: 역사 경계 명시 후 최종 검사 결과.
- stage `canonical-before.json`, `audit.json`: 원본 해시와 변화0.
- stage `code-determinism.txt`, `work/2026-11/2026-11-05-지식그래프-개편/B/determinism.json`: 코드 추출 2회 동일 결과.

후보 변경 시 prepare 스크립트는 새 SHA의 별도 stage를 만들도록 되어 있다. 새 helper가 더 생기거나 파일이 없어지면 codeFiles/앵커 목록도 그 후보에 맞춰 확인해야 한다. 기존 stage를 재귀 삭제하거나 공식 graph.json만 덮어쓰는 절차는 쓰지 않는다.

## 공식 적용에 남는 일

부모가 최종 코드·독립 검수·실게임 결과 및 병합 승인 경계를 확인한다. 공식 적용 시 현재 canonical의 해시가 준비 당시와 같은지 다시 확인하고, 승인된 소스만 백업 후 반영하여 공식 위치에서 재생성한다. 후보 코드가 아직 ai-character에 합쳐지지 않았으므로 제품 check의 baselineDrift는 의도된 경계다. 이를 숨기려고 본 저장소 HEAD를 바꾸지 않았다.

과거76563bf 화면 관측, e82eb87 코드 추론,902721e 추가 실행 관측은 각각 보존했다. 이 stage의 통과를 선택지 품질·실게임 생존·완전한 실행 coverage의 증거로 해석하지 않는다.
