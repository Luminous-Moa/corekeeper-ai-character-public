# 격리 그래프 한정 독립 검수

2026-11-06. 입력 `14-graph-stage-result.md`, `04-graph-handoff.md`와 `graph-stage/`의 개발·제품 그래프를 검수했다. 제품 소스 기준은 `902721ec505df42d74d0b2d245ed34a049faadad`다. 코드의 역할 분리 검수와 별개로, 이번에는 파일 포함·대표 앵커·역사 근거 경계·읽기 전용 검사에 한정했다. 전체 그래프의 의미를 전수 재검수하거나 새 실게임 증거를 평가한 보고가 아니다.

## 판정

**격리 후보 그래프 수용 가능. Must-fix 발견 없음.** 대표 8개 함수의 문서/개발 노드/제품 Roslyn 노드가 실제 고정 소스에 연결된다. 제품 기능 ID와 정책을 유지했으며, 코드의 이동을 새 런타임 관측으로 승격하지 않는다. 아래 역사 문서 표지 권고는 HEAD가 수용하여 작성자에게 반영 요청했다.

canonical과 staged 그래프를 직접 수정하지 않았다. 외부 보고 폴더에 이 보고와 읽기 전용 확인 도구 `review-graph-check.mjs`만 작성했다. 제품·설치·게임·Unity도 변경하거나 실행하지 않았다.

## 기준과 파일 포함 직접 확인

- 본 저장소 `C:/Dev/CoreKeeperModSDK`의 실제 HEAD는 `a07166f38086e0241c37c601c16452664236813e`다. 후보 그래프의 B/D sourceCommit과 overlay baseCommit은 902721e다. 개발 config의 codeRoot는 후보 worktree, stagedDocumentRoot는 격리 문서 위치다. 공식 HEAD를 후보로 변경하지 않았다.
- `canonical-before.json`의 434개 파일을 실제 파일 SHA-256으로 다시 계산했다. **434개 모두 불변**이다. 작성자의 stage-audit 주장만 인용한 결과가 아니다.
- Git 902721e의 Scripts/**/*.cs와 HeadlessSdk.cs 목록을 config.codeFiles에 직접 대조했다. **85/85 일치, 누락 0, 불필요한 경로 0**이다.
- snapshot 및 runtime-package 전체 경로와 파일 바이트를 해당 Git tree/blob ID에 대조했다. **snapshot 232/232, runtime-package 55/55 일치**, 없는 파일·변경 파일·추가 잔존 파일 모두 0이다. 삭제된 AIBodySkills/AIExplore의 옛 .cs가 현재 snapshot에 남아 있지 않다. 역사 근거의 이전 파일명은 이 잔존 검사와 다른 대상이다.
- 개발 기능 ID 64개와 정책 ID 87개의 집합이 canonical과 같다. 개발 정책.md 파일 자체의 SHA-256도 같다. 제품 B의 기능 ID 22개 집합도 canonical과 일치한다. 함수/코드 노드 증가를 새 제품 기능이나 정책 추가로 해석하지 않았다.

## 대표 앵커 8개

`S/`는 `Assets/AIPlayerSpike/Scripts/`, 문서 행은 staged `work/지식그래프/기획/기능.md` 기준이다. 아래 소스 위치는 모두 git show 902721e에서 직접 확인했다. 각 함수의 개발 loc와 제품 B의 file/lineStart/lineEnd가 일치한다. 원문 인용은 아래 query check의 실제 원문 확인을 함께 사용했다.

| 표본 | 문서/기능 입구 | 실제 소스와 대응 |
|---|---|---|
| 목표 선언 조회 | 221행, F.goal-cmd의 AIGoalKinds.Of | `S/Core/AIGoalKinds.cs:50` Of, 50–54행. All에서 같은 Kind를 찾고 없으면 null을 반환하는 조회다. 등록 표가 모든 실행 정책을 자동 구현한다는 의미로 연결하지 않는다. |
| 목표별 후보 진입 | 239행, F.candidates의 AIGoalFeatures.AddCandidates | `S/AIGoalFeatures.cs:8` AddCandidates, 8–20행. 목표별 후보 생산 진입을 옛 CandBuild 흐름에서 찾을 수 있다. |
| 후보 역할 소비 | 239행, F.candidates의 AICandidateAssembler.Select | `S/Core/AICandidateAssembler.cs:58` Select, 58–161행. 같은 파일 14행의 EdgeCandidateRole과 연결되는 실제 조립기다. AICandidate 자체는 제품 그래프에서 `S/AIDecisions.cs:94–130`으로 추출된다. 14 결과표의 위치 정정도 확인했다. |
| Body 명령 | 개발 code:AIBody.HandleCommand, F.ai-toggle/F.multi-ai/F.test-switch 연결 | `S/AIBody.Commands.cs:19` HandleCommand, 19–369행. 이름만 남은 옛 AIBodySkills 경로로 연결하지 않는다. 제품 Roslyn 서명은 string[] parts까지 정확하다. 개발 signature 표시의 배열 누락은 아래 기존 한계다. |
| 탐색 기억 저장 | 310행, F.explore-memory의 AIMemory.Save | `S/AIMemory.cs:203` Save, 203–239행. AIKnowledge.Save(force) 이후 기억 저장을 수행하는 원본 함수다. 경험 기억이나 캐릭터 저장 함수와 혼합하지 않았다. |
| 캐릭터 저장 | 168행, F.ai-char-save의 SaveFromServer | `S/AICharacterStore.cs:300` SaveFromServer, 300–424행. _current/_path guard를 포함한 서버 캐릭터 저장 진입이다. 탐색 기억과 구별된다. |
| 문 사실 조회 | 872행, F.my-door의 AIDoorFacts.FirstAt | `S/AIDoorFacts.cs:40` FirstAt, 40–54행. 전체 query의 첫 문 선택이며 InWindowLastByTile/OpenTilesExcept와 다른 함수로 유지된다. |
| 위협 관측/기록 | 756행, F.base-reach의 ObserveThreat 및 AIThreatHistory | `S/AIEdge.Reach.cs:204` ObserveThreat, 204–210행. Assess의 nullable 결과가 있을 때만 history에 Record한다. 평가·기록·마지막 결과 조회를 구분하는 새 입구다. |

이전 검수에서 확인한 함수 의미와 이번 그래프 위치가 대응한다. 개발 노드의 features 배열에는 call graph를 통한 전이 연결도 있으므로, 그 목록 전체를 직접 실행 관측으로 읽지 않는다.

## 현재 코드와 역사 근거의 경계

- `B/inference.graph.json`은 meta.commit=e82eb87, confidence=code-only-inference, runtimeObserved=false를 보존한다. currentCodeAssociations만 902721e이며, 이전 범위는 historicalEvidence/historicalEvidenceCommit에 있다. BA-01 및 화면→요소 edge 표본에서 이 구분을 직접 확인했다.
- `L/C/overlay-source.json`과 D graph는 source/base=902721e, installedSourceCommit=76563bf를 구별한다. overlay boundaries는 같은 구간이 새 설치·함수 trace 증거가 아니며 신규 실게임 근거를 아직 연결하지 않는다고 명시한다.
- `L/B/existing-node-mapping.json`은 51개 중 sameSourceSegment true 39개/false 12개다. AIBody.HandleCommand/IsSkillCommand 표본은 현재 Commands.cs와 과거 AIBodySkills.cs의 runFile을 구별하고, 다른 본문은 runLines=null/false로 남긴다. runtime edge를 임의 추가하지 않는다.
- `L/B/version-evidence.json`의 옛 e82 기준은 원래 수집 기록으로 유지된다. 설치 디스크 일치가 메모리 내 assembly나 요청별 호출을 증명하지 않는다는 한계도 남아 있다.
- 개발 기능의 옛 확인한 선을 이번 실행 확인으로 바꾸지 않았다. 확인 뒤 코드 바뀜 42→48 경고가 보존된다. `미결.md:944`는 후보 필드 혼용의 902721e 해결 범위만 덧붙이고, 나머지 전투 전환 규칙이나 새 실게임 확인까지 해결됐다고 하지 않는다.

## 비차단 권고와 알려진 한계

1. **역사 문서 직접 진입 표지:** `L/B/code-version-mapping.md`의 기존 최신 머리말은 a07166f 갱신이며 아래에는 과거의 “지금 설치본” 표현도 있다. JSON mapping/overlay는 정확하지만, 문서 직접 진입자가 현재 후보와 혼동하지 않도록 상단에 현재 staged 기준 902721e와 아래 설치 문구의 역사성을 표시하도록 권고했다. HEAD는 권고를 수용하고 작성자에게 반영 요청했다. 새 live 결과를 추가하라는 권고가 아니다.
2. **개발 파서의 기존 배열 표시 한계:** 개발 code:AIBody.HandleCommand의 signature는 string[] parts를 string parts로 표시한다. canonical의 기존 노드도 동일한 오류이고, 함수 ID·현재 파일/행·실제 원문·제품 Roslyn 서명은 정확하다. 이번 이동이 만든 회귀나 잘못된 함수 연결은 아니다. 정확한 타입은 원문/제품 Roslyn을 따른다.
3. **개발 파서의 이미 보고된 WalkCandidate 누락:** expression-body 객체 initializer 함수가 개발 함수 색인에서 누락된다. 가짜 앵커로 strict를 통과시키지 않았다. 제품 B에는 `AIApproach.cs:13–14` WalkCandidate가 실제 MethodDeclaration으로 추출된다. 이번 검수에서 파서를 확장하지 않았다.

## 실행한 확인과 기록으로 확인한 검사

직접 실행한 읽기 전용 `node tools/query.mjs check`는 exit 0이다. 원본 해시 일치, 인용의 현재 원문 존재, 인용/앵커/ID 경고 없음의 실제 출력을 확인했다. 직접 실행한 `node tools/product.mjs check`도 exit 0, ok=true, structuralErrors=0, stale=0이었다. baselineDrift의 후보 902721e/공식 a07166f 차이는 의도된 격리 경계로 표시된다.

개발 build --strict 자체는 staged 생성물을 다시 쓰므로 이 검수에서 재실행하지 않았다. 저장된 dev-build 결과와 현재 query의 원본/인용/앵커 검사 결과를 대조했다. `final-command-results.json`에서 B verify의 snapshot287/external11/questions3/healthDoesNotReachPlan=true, 질문의 Chat partial→StatusAll→AIWorldSettings.Members 경로, 제품 진입점 7개 통과와 product-build --check의 byte-identical 출력을 확인했다. 이들을 독립 재실행했다고 주장하지 않는다. 결정성 추출·전체 테스트도 반복하지 않았다. L/D/verify의 이전 생성물 불변 검사를 미실행했다는 14의 한계는 유지한다.

## 검수 입력 식별과 HEAD 반환

한정검수 본체에서 읽은 SHA-256:

- stage-audit.json: `7d2693678b415ac7bc2f9f8f1ccae5814237f4eaf3dda512091ddc3ad77966d5`
- G graph.json: `dcf62c23b7b98348570147f64f30835b775a8e449da716531c5a874c7eb9e3f1`
- B B.graph.json: `c8b3f8b979c190dc82ee166152c8cc97c3839f052c29d87e015b05fe317d7893`
- D graph.json: `080148bc184893defb777074c7fd2f0dde49cc4a29b9cab703726dd097359818`

역사 문서 표지 반영으로 이후 감사 해시가 달라질 수 있으며, 아래 추가 확인에 구분한다. 이 판정은 canonical 반영/병합 승인이나 새로운 게임 실행 검증을 대신하지 않는다. 공식 반영 시 상대경로·codeRoot/stagedDocumentRoot 전환과 최종 승인 SHA에 대한 재생성은 14의 절차를 따른다.

### 역사 문서 표지 반영 후 추가 확인

같은 날 작성자가 `L/B/code-version-mapping.md:3`에 현재 격리 코드 902721e, 과거 화면 관측 설치 76563bf, “지금 설치본”의 역사적 의미, 동일 구간을 새 실측으로 승격하지 않음, 최신 대응 JSON 위치를 명시한 표지를 추가했다. 직접 읽어 확인했으며 위 권고 1은 처리 완료다.

이 문서 변경에 따른 갱신 뒤 product check만 다시 읽기 전용 실행했다. exit 0, ok=true, structuralErrors=0, stale=0이다. G graph 해시는 위와 같고, 추가 확인 시 stage-audit 해시는 `d1538fd102421f8d26ae756ef0c34343800e8382b680358ea255f660c5f3b976`, D graph 해시는 `6a9da25d560562b89539a4449f36d940d9756c8e7db1a90f41ec33459d2c6710`이다. 새 실게임 근거가 추가된 것으로 해석하지 않았다. 최종 판정은 수용 가능/must-fix 0을 유지한다.
