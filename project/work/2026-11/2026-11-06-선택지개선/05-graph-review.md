# 선택지 개선 그래프 stage 증거 정합성 독립 검토

2026-11-06. 대상 `graph-stage-b25099f`, `04-graph-stage.md`, `prepare-graphs.mjs`, `finish-graphs.mjs`. 제품 코드 검수의 재실행이나 그래프 재설계는 하지 않았다. 공식 그래프·stage·제품 코드 수정 없음. 확인한 것은 변경 경계와 대표 증거/코드 연결이다.

## 판정

**과거 실행 증거를 b25099f의 실행 확인으로 승격한 정황은 없다. 역사 관측 허용 변경은 이 준비 절차 안에서 명시적으로 제한되어 있다.** snapshot SHA와 새 helper 원본은 맞는다.

단, **공식 반영 전 작은 연결 정정 1개가 필요하다(G1)**. 실제 걷기 구현이 호출하지 않는 `AIMoveEstimate.Seconds`를 걷기 기능의 함수 앵커로 넣었다. 새 검증을 했다는 과장은 아니지만 기능→코드 선의 의미가 부정확하다. 제품 코드 수정 사안은 아니다.

## G1 — F.skill-goto의 함수 앵커가 실제 의존과 다름

`prepare-graphs.mjs`의 anchors가 `F.skill-goto: ['AIMoveEstimate.Seconds']`를 추가한다. 결과 개발 `graph/graph.json`에 다음 두 간선이 실제 생겼다.

- `F.skill-goto --처리(해석)--> code:AIMoveEstimate.Seconds`
- `F.skill-goto --지남(투영)--> code:AIMoveEstimate.Seconds`

그러나 b25099f `Assets/AIPlayerSpike/Scripts/AIGotoSkill.cs:69,73`은 `d / AIBody.WalkSpeed`이며 `AIMoveEstimate.Seconds`를 호출하지 않는다. `AIBody.cs:55`를 통해 공유 **상수** `AIMoveEstimate.WalkSpeed`를 쓰는 관계다. 순수 함수 Seconds는 다른 후보/경로 설명 계산에서 쓰인다.

**정정:** 걷기 기능에 추가한 Seconds 함수 앵커를 제거하거나, 현재 그래프가 표현할 수 있는 방식으로 공유 속도 상수 의존임을 명시한다. 실제 호출처럼 보이는 처리/지남 선으로 남기지 않는다. stage의 문서 원본과 재생성 경로를 고치고, 공식 graph.json을 직접 편집하지 않는다.

## 대표 대조 결과

### 1. 역사 관측 허용 guard가 검사 전체를 끄지 않음

canonical과 staged `2026-11-05-지식그래프-실화면/D/integrate.mjs`의 diff는 세 지점이다.

- `:22`: 현재 코드 SHA와 일치하거나 `overlay.historicalRefactorObservationCommit`과 정확히 일치하는 관측만 허용. schemaVersion 검사는 유지한다.
- `:29`, `:64`: 현재 SHA와 동일 판인지/현재판에 증거가 적용되는지 표시를 추가한다.
- `:23`의 개별 실행 증거 SHA 검사, `:26~27`의 개발 capability/제품 feature 존재 검사는 그대로다. sourceCommit을 덮어쓰거나 functionTraceObserved를 true로 만들지 않는다.

stage 통합기 자체가 '902721e만' 하드코딩하는 것은 아니다. **prepare가 호출하는 finish-graphs.mjs가** 허용 관측 SHA를 `902721ec505df42d74d0b2d245ed34a049faadad`로 고정하고 canonical 관측 JSON과 원바이트 동일성까지 확인한 뒤 overlay를 쓴다. 따라서 이번 준비 경로는 임의 mismatch 전체 허용이 아니다. 향후 공식 적용 시 이 제한과 표시 필드를 함께 유지해야 하며, guard 일부만 별도로 복사해서는 같은 보장이 성립하지 않는다.

### 2. 실제 생성 결과와 과거 자료의 지위

staged `D/graph.json`과 `D/validation.json`에서 직접 확인:

| 필드 | 값 |
|---|---|
| 현재 graph sourceCommit | b25099f6fd31e4c4c3618357eb9e8101e0e17124 |
| installedSourceCommit(과거 화면) | 76563bf3ac3af56241bb796c6510ad7e96c3ccd7 |
| supplemental sourceCommit | 902721ec505df42d74d0b2d245ed34a049faadad |
| supplemental currentSourceCommit | b25099f6fd31e4c4c3618357eb9e8101e0e17124 |
| sameRevision / evidenceAppliesToCurrentRevision | false / false |
| validation functionTraceObserved | false |

`C/refactor-20261106/observations.json`은 canonical과 **원바이트 동일**했다. 입력 중 `install-result.json`, `installed-source-sha256.json`, `down-t600-619.txt` 세 개의 SHA256을 직접 계산하여 관측 JSON 선언과 일치함을 확인했다. 사망 관측은 여전히 cause-unresolved이며, 폭발벽은 원인 후보이지 입증된 사망 원인·안전 정책 성공이 아니라는 boundary가 보존된다. 새 b25099f 실게임 관측은 이 stage에 없다.

### 3. 검증된 선/역사 추론의 지위를 덮어쓰지 않음

개발 기능 `F.candidates`, `F.skill-goto`, `F.skill-mine`의 기존 status는 '실게임 확인'으로 남지만, **verifiedLine은 canonical과 같고 현재 lineHash는 다르다**. staged query.mjs:183이 이를 '다름 — 다시 시험 필요'로 출력하도록 유지한다. 상태 문자열만 보고 현재판 확인으로 해석하면 안 된다. 새 helper에 맞춰 verifiedLine을 갱신하지 않았다.

`B/inference.graph.json`은 `meta.currentCodeAssociations`를 제외하면 canonical과 구조가 동일하다. 역사 추론 commit=e82eb87, confidence=code-only-inference, runtimeObserved=false가 유지된다. 현재 SHA 연결 문구에도 정적 추출이며 소급 실행 관측이 아니라고 명시한다.

### 4. 코드 snapshot과 새 helper

staged `B/snapshot`의 다음 네 파일을 b25099f의 Git blob과 직접 비교해 **모두 원바이트 동일**함을 확인했다.

- `Core/AIWallRisk.cs`
- `Core/AIPickupOptions.cs`
- `Core/AIMoveEstimate.cs`
- `AIBrain.Candidates.cs`

pin-snapshot.mjs는 candidate repository와 b25099f를 고정하고 `git cat-file blob`으로 저장한다. 후보 sourceCommit도 그 SHA다. F.candidates의 AIPickupOptions.Select/AIWallRisk.RouteText, F.skill-mine의 AIWallRisk.Text, F.skill-dig의 TunnelPlan.RiskText 등은 현행 코드 책임과 맞는다. G1의 걷기 함수 앵커만 제외해야 한다.

## 확인하지 않은 것과 적용 경계

- 작성자의 canonical454파일 변화0·strict/check·동일 재생성 통과는 `audit.json` 및 기록을 읽었으며, 이번에 전체 재검사로 반복하지 않았다. 독립 직접 검사는 위 관측 JSON 동일성, 입력 증거3개, 코드 원본4개, 대표 기능3개에 한정했다.
- stage 조회의 status '실게임 확인'은 오래된 기능 확인 기록과 선 서명을 함께 해석해야 한다. 최신 실행 증거가 없는 상태에서 완료/안전성/전체 coverage를 주장할 근거가 없다.
- `stagedDocumentRoot`와 stage 전용 resolver는 공식 적용 때 별도 복원/정리가 필요하다는 04 문서의 한계가 유효하다. 후보 baselineDrift도 코드가 아직 canonical HEAD에 병합되지 않은 경계이며 이를 숨기려고 공식 HEAD를 바꾸지 않았다.
- G1 수정 후 작은 간선 재대조면 충분하다. 새 제품 구현·전체 그래프 재설계·추가 실게임을 이 그래프 검토가 요구하지 않는다.

## G1 수정 재검토 — 최종 판단

**G1 해소. 이번 좁은 증거 정합성·신규 앵커 검토에서 남은 수정 요구 없음.** 과거 증거가 현재 b25099f 실게임 확인으로 승격되지 않는다는 앞 판정과 실게임 미검증 한계는 그대로다.

수정 뒤 직접 확인한 세 지점:

1. `prepare-graphs.mjs`에 F.skill-goto→AIMoveEstimate.Seconds 앵커 추가가 없다.
2. stage `기획/기능.md`의 실제 F.skill-goto 절 코드 앵커에서 Seconds가 제거되었다.
3. 재생성된 개발 `graph/graph.json`에서 F.skill-goto와 code:AIMoveEstimate.Seconds 사이 양방향 간선이 **0개**다. 노드 2,617/간선 21,426을 확인했다.

이 재검토에서는 해당 연결만 대조했다. 작성자의 G strict/query·제품 통합/check·동일재생성 및 canonical454변화0 기록을 독립 전체 재실행으로 주장하지 않는다. 제품 코드/공식 그래프 수정 없음.
