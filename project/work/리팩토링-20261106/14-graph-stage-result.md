# 리팩토링 그래프 격리 갱신 결과

2026-11-06. 코드 기준 `902721ec505df42d74d0b2d245ed34a049faadad` (`refactor-maint-20261106`). **최종 실게임·복원 증거를 포함한 격리 후보 갱신을 완료했다. 공식 G/P/L과 본 저장소는 변경하지 않았다.** 코드·그래프 검사와 이번 능력별 실게임 관측은 아래에서 구별한다. 시험 월드의 게임 내 삭제/Steam Cloud 삭제 1건은 미완료다.

## 위치와 보존

작업 루트는 `C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/리팩토링-20261106/graph-stage`다.

| 산출물 | 루트 기준 경로 |
|---|---|
| 개발 그래프와 기능 앵커 | `work/지식그래프/graph/graph.json`, `work/지식그래프/기획/기능.md` |
| 고정 코드 snapshot·추출 그래프 | `work/2026-11/2026-11-05-지식그래프-개편/B/` |
| 제품 통합 그래프 | `work/2026-11/2026-11-05-지식그래프-실화면/D/graph.json` |
| canonical 원본 해시 | `canonical-before.json` |
| canonical 불변·변경 파일·검증 집계 | `stage-audit.json` |
| 실제 최종 명령과 전체 출력 | `final-command-results.json` |
| 제품 check 전체 결과 | `product-check.json` |
| 868개 이동 심볼 대응 | `symbol-moves.json` |
| 개발 strict 출력·내 문 선 조회 | `dev-build.txt`, `dev-my-door.txt` |

상대경로를 유지한 G/P/L 434개 파일을 복사했으며 원본 해시 434개가 모두 그대로다. canonical HEAD도 `a07166f38086e0241c37c601c16452664236813e`다. node_modules 전체·기존 snapshot/runtime-package·빌드 bin/obj는 복사하지 않았다. 추출 의존성 acorn만 복사하고, snapshot/runtime-package는 빈 단계 경로에 최종 커밋의 Git blob으로 새로 만들었다. 삭제된 AIBodySkills/AIExplore의 옛 파일이 새 snapshot에 섞이지 않는다. 코드나 게임 실행·설치·모델 호출은 이 그래프 작업에서 하지 않았다.

## 검사 결과

| 검사 | 결과 |
|---|---|
| 개발 codeFiles | 기존 54 → 85(모드 84 + HeadlessSdk 1), 새 파일 누락 없음 |
| 개발 build --strict / query check | 통과. 원본 해시·인용 일치, 앵커·ID 경고 없음 |
| 개발 기능 / 코드 노드 | 기능 64 유지, 코드 1809 → 1884 |
| 개발 노드 / 간선 | 2524/21089 → 2605/21380 |
| 선 밖 코드 | 기존 51 유지. 신규 책임의 선을 연결하고 문자열 호환 EdgeWallNote를 시험용으로 분류 |
| 확인 뒤 코드 바뀜 | 42 → 48. 옛 확인 서명을 최신 확인으로 바꾸지 않아 경고를 보존 |
| B 고정 snapshot | snapshot와 runtime-package 합계 287개 Git blob 일치 |
| B 질문 경로 | 상태/목표 출처/기억검색 3개 통과, health가 plan으로 도달하지 않음 |
| B 결정성 | Roslyn+Acorn 새 추출 2회, 7개 생성물 byte-identical |
| B 추론 포함 최종 결과 | 5680노드/22105간선, 기능22 유지, executable 미도달41(실행 여부 판정 아님) |
| B 최종 병합 그래프 결정성 | 별도 build 2회 B.graph 동일; 기계 추출 해시가 결정성 근거와 일치 |
| B 수동 소스 표본 | 고정 seed의 8개 선언·등록 위치를 새 파일에서 다시 읽음 |
| P/C 및 L/C 참조 검증 | 통과 |
| L/D 질문 검증 | 채팅 partial → StatusAll → AIWorldSettings.Members → 설정 데이터 경로 통과 |
| 제품 통합 | 5807노드/22303간선; structuralErrors 0, stale 0, ok true |
| 제품 진입점 | overview/check/user/source/feature/legacy 및 build --check 총7개 통과 |
| 최종 재생성 | byte-identical |

제품 check의 `baselineDrift`는 **후보 902721e와 공식 HEAD a07166f가 다른 의도된 경계**다. 구조 오류로 숨기거나 공식 HEAD를 후보로 돌리지 않았다. 외부 ai-chatroom 정적 import 11개는 해시만 확인했으며 외부 런타임 실행 증거로 취급하지 않는다.

## 갱신의 의미와 한계

기존 9개 기능의 앵커 묶음에 목표 메타데이터·기능 진입, 위협 관측/기록, 벽 의미 필드, 문/물체 사실, 접근 탐색을 연결했다. 제품 기능 ID와 제품 정책은 추가하지 않았다. `AIBaseGuard.EdgeWallNote`는 제품이 WallFact를 사용한 뒤 core-tests에 남은 문자열 호환 함수로 분류했다.

알려진 개발 파서 한계 두 가지는 별도 유지보수 대상으로 남긴다. expression-body의 객체 초기화 반환 `AIApproach.WalkCandidate`를 함수로 잡지 못하므로 가짜 앵커를 넣지 않았다. 또한 기존 `AIBody.HandleCommand`의 `string[]` 인자를 개발 그래프 서명에서 `string`으로 표시하는 한계가 있다. 제품 Roslyn 그래프는 실제 WalkCandidate 함수와 배열 인자 서명을 추출한다. 이번 작업에서 개발 파서를 수정하거나 이 정적 표시 한계를 실행 검증 결과로 해석하지 않는다.

제품 코드 ID는 경로에 종속되어 있어 동일한 qualified symbol과 kind를 기준으로 868개 이동 대응을 만들었다. 기존 코드 추론의 끊긴 연결을 새 ID로 옮겼고, 파일 단위 상태/도움말 출력은 Chat partial, 시험명령은 Body.Commands로 연결했다. 참가 설정 동작은 본체에 남아 있다. 과거 추론의 줄번호는 `historicalEvidence`와 원래 추론 커밋 `e82eb87`에 남겨 현재 snapshot의 줄로 오해하지 않게 했다. 이 과거 해석을 새로운 화면 관측으로 승격하지 않았다.

과거 실화면 설치 기준은 `76563bf`를 유지한다. 새 파일에서 이동 전 파일로 거슬러 올라가 정확한 소스 구간을 비교한 결과 51개 중39개 동일,12개 변경이다. `runFile`과 이전 줄 범위를 명시했다. 같은 본문이라는 사실은 새 설치·게임 실행·함수 trace의 증거가 아니다. 새 실게임 증거는 관측 능력별 별도 추가 기록에 붙였다(아래 최종 보충 절). 기존 `L/D/verify.mjs`의 “이전 생성물 불변” 검사는 재추출과 충돌하므로 실행하지 않았고, 전체 검증 통과로 포괄하지 않는다.

## 이후 개발의 입구

아래 경로는 `Assets/AIPlayerSpike/Scripts/` 기준이다. 형식별 구현과 상태 소유를 유지하고, 등록 표의 행 추가만으로 모든 처리 경로가 자동 구현된다고 가정하지 않는다.

| 변경하려는 것 | 주된 위치 / 지켜야 할 경계 |
|---|---|
| 목표 추가·명칭·입력 계약 | `Core/AIGoalKinds.cs`의 All/Of/TryWire/IsHostWord, `AIGoalText.cs`; 프로토콜·호스트 입력·기억 키를 함께 대조 |
| 목표별 후보/완료/진행 불가 | `AIGoalFeatures.cs` AddCandidates/TryDone/StallNote; 생명주기/종료 순서는 `AIGoals.cs` |
| 후보 역할·벽 의미 | `AIDecisions.cs:94`의 AICandidate 의미 필드, `Core/AICandidateAssembler.cs:14`의 EdgeCandidateRole와 조립 규칙, `Core/AIBaseGuard.cs` EdgeWallFact; 설명 문자열에서 정책을 역추론하지 않음 |
| 후보 생성 인자 소유 | `AIBrain.Loop.cs` Send → BuildCandidates → `AIBrain.Candidates.cs` CandBuild; prefetch/breachCall은 전달 인자이며 전역 상태로 복구하지 않음 |
| 문·물체 공간 조회 | `AIDoorFacts.cs`, `AIWorldFacts.cs`; 칸별 마지막/첫 선택/하나라도 열린 문이라는 다른 선택 계약 보존 |
| 접근 탐색·보행 후보 | `Core/AIApproachSearch.cs`, `AIApproach.cs`; 세 설 칸까지만 첫 성공, cached/immediate·Home/Place detour 차이는 호출부에 남음 |
| 몸·명령·관측·길찾기 | `AIBody.cs` 상태, `AIBody.Commands.cs` 명령, `AISkill.cs`, `AIObservation.cs`, `AIVision.cs`, `AITileReader.cs`, `AINavigator.cs`, 각 Skill 파일 |
| 탐색 기억·떨굼·물체·굴길 | `AIMemory.cs`, `AIDrops.cs`, `AIObjects.cs`, `AITunnel.cs`; 경험 기억 `AIExperience.cs`/`AIExpCore.cs`와 구별 |
| 저장·월드 참여 | `AICharacterStore.cs`, `AIWorldSettings.cs`; 몸/모드 partial은 정적 필드를 원래 본체에 유지 |
| 위협 평가·기록·마지막 결과 읽기 | `AIEdge.Reach.cs` AssessThreatWithinRange/ObserveThreat/ObserveAnyThreat/LastAssessedCannotCome, `Core/AIThreatHistory.cs`; 기록은 캐릭터 State 소유, 범위 밖 미기록·열거 시점 유지 |
| 모드 진입·패치·채팅 | `AIPlayerSpikeMod.cs`, `AIPlayerSpikePatches.cs`, `AIPlayerSpikeMod.Chat.cs` |

## 승인 이후 canonical 반영 순서

1. 부모의 독립 검수·실게임 보고와 사용자의 ai-character 병합 승인을 확인한다. 대상 SHA가 달라지면 이 snapshot/검사 결과를 그대로 사용하지 않고 새 SHA로 재추출한다.
2. 기존 canonical G/P/L과 사용자 로컬 변경을 별도 백업하고 `canonical-before.json` 대비 차이를 먼저 확인한다. 이 보고의 사본은 리뷰용이며 canonical에 덮어쓰지 않았다.
3. 소스 병합 후 본 저장소 HEAD가 승인된 SHA인지 확인한다. G config의 85개 codeFiles와 기능 앵커/설명을 반영하되 codeRoot는 본 저장소, sessionRoot는 원래 `../..`로 둔다. **stagedDocumentRoot는 제거**한다. staged lib/query의 경로 override는 격리 검사를 위한 것으로 필수 제품 변경이 아니다. canonical 기존 lib/query를 유지해도 된다.
4. `stage-audit.json.stagedChanges`의 원본·도구 변경을 검토해 반영한다. README의 “격리 후보” 문구는 실제 반영 상태에 맞게 갱신한다. P/B 추출기의 고정 SHA, StatusText/Members 새 파일, 질문 근거 줄·심볼, inference의 이동 연결과 역사적 근거 구분, L/B 이동 전 runFile 대응, L/D 동적 qualified symbol 조회와 staticEntry, overlay baseline을 포함한다.
5. 옛 snapshot/runtime-package는 백업한 뒤 승인된 정리로 치우거나 새 빈 출력 위치에서 추출한다. 기존 폴더 위 pin-snapshot만 실행하면 삭제된 파일이 남는다. 추출 순서는 04-graph-handoff를 따른다. P/C 통합 뒤 **먼저 product-build로 현재 D graph를 만들고** L/D/questions를 실행해야 옛 D graph가 새 snapshot을 읽는 오류를 피한다.
6. 개발 strict/query, B verify/determinism/8개 표본, P/C·L/C, 제품 질문·진입점·build --check를 다시 실행한다. 실제 승인된 실행 증거만 별도 추가하고 옛 76563bf 관측은 보존한다. 생성물은 canonical 경로에서 재생성한다(격리 산출물의 절대경로/mtime을 그대로 공식 생성물로 복사하지 않음).
7. 공식 HEAD와 제품 baseline 일치, structuralErrors/stale 0, canonical 단계에서 source 경로가 정상임을 확인한 후 부모가 세션을 저장한다.

`stage-graphs.mjs`, `fix-mappings.mjs`, `finalize-sources.mjs`는 이번 사본 준비 기록이며 반복 실행용 배포 도구가 아니다. `verify-final.mjs`는 격리 사본의 정적 재검증 도구, `verify-live-final.mjs`는 추가 실게임 증거 적재 후 관련 검사 도구다. 공식 반영·세션 저장과 시험 월드 Cloud 삭제 인계는 부모 담당으로 남아 있다.

## HEAD 위치표 검토 반영

AICandidate의 선언을 고정 902721e Git blob과 두 staged 그래프에서 대조했다. 정확한 위치는 `AIDecisions.cs:94-130`이며 위 유지보수 표의 `AIBrain.cs` 오기를 수정했다. 개발 노드 `code:AICandidate`와 제품 노드 `code:Assets/AIPlayerSpike/Scripts/AIDecisions.cs#AICandidate`는 처음부터 정확한 위치였다. staged 원문·앵커에는 같은 위치 오기가 없었다.

추가로 미결 문서에 2026-11-05 당시 “digWhat/eatVariation 필드 돌려쓰기는 그대로”라는 역사적 메모가 남아 있어 원문을 보존하고 2026-11-06 후보에서 의미 필드로 나뉘었다는 후속 메모를 두 해당 항목에 붙였다. 다른 전투 전환 규칙이나 실게임 확인까지 해결된 것으로 닫지 않았다. 개발 strict/query 재검증 통과, 제품 structuralErrors/stale 0 유지, canonical 434파일 변경 0을 다시 확인했다. staged 변경 파일은 40개다.

독립 검수의 비차단 문서 권고도 반영했다. staged `L/B/code-version-mapping.md` 최상단에 현재 격리 기준902721e, 과거 화면 관측76563bf 및 각 이력의 “지금 설치본” 표현이 당시 기록임을 명시했다. 역사 본문과 관측 지위는 유지한다. 보고서의 개발 파서 한계에는 WalkCandidate 누락과 HandleCommand 배열 인자 서명 표시를 함께 적었다. 문서 해시 반영 뒤 제품 재생성/`--check` byte-identical, structuralErrors/stale0, canonical434파일 변경0을 확인했으며 현재 staged 변경 파일은41개다.

## 최종 실게임·복원 증거 보충

최종 `15-unity-and-live.md`와 한정 검토 `18-review-live-observation.md`를 동기화했다. 추가 관측 원본은 staged `L/C/refactor-20261106/observations.json`이며 보고서·Player 로그·설치 소스 해시·저장 비교·복원/Cloud 기록·관련 화면 총14개 파일의 사본과 원본 경로/해시를 가진다. 과거 화면 관측층은 유지하고, 제품 `D/graph.json.supplementalRuntimeObservations` 및 `D/validation.json.supplementalRuntime`에 별도 적재했다. 기능·코드 실행 간선을 새로 추정하지 않았다. 개발 기능5개의 메모에도 해당 능력의 관측과 한계를 연결했고 확인한 선 서명은 유지했다.

| 능력 | 이번902721e 근거와 경계 |
|---|---|
| 실제 자연어 계획·제작·놓기·다음 목표 | Gemini의 place CopperAnvil→craft CopperSword, 두 목표 완료/경험, plan_done 후 gather Wood 선택 관측. 재료·시설은 testcmd 지급, 모든 제작법·목표 검증 아님 |
| 다중 멤버 목표 교체/allstop | GPT/Gemini 교체·취소 관측. t659.4 이후 첫 process 종료까지 DECISION SEND0 최종 대조. 상위 bridge 의도 종료 뒤 연결 실패는 환경 상태 |
| 자기 문 | 설치/기억, 열기1회→통과→닫기1회→도착. Player 로그 근거, movetrace CSV 생성되지 않음 |
| 저장·완전 종료·동일 월드 재입장 | 기존 GUID 자동참여,15필드×2명30/30 및 참여 설정 일치. 추가 지급/설치 없이 복원된 문 열기→통과→닫기→goto 완료 |
| DOWN 관측 | 피해원 미확정.18 한정 검토는 관련 동작 보존·새 회귀blocker 식별근거 없음을 확인했으며 정상 결과/폭발원인/안전정책 통과를 뜻하지 않음 |

로컬 복원은 mod54/AIPlayers43/localsteam248/remote40/registry 동일, verify0이다. 게임·Unity·시험 bridge 종료, 로컬 시험 파일 없음, 실제 사용량 보존, FoldRemote 보기전용 원복·연결 종료를 기록했다. **시험 월드 rfmaint1106(슬롯7/파일인덱스6/GUID97b88eb024da6d5794f65879c2592dcc)의 게임 내 삭제와 Steam Cloud 삭제는 미완료**다. 원격 앱이 입력을 차단했고 정상 재연결1회 뒤에도 같아 추가 디버깅을 중단했다. Cloud에는 해당3파일 Upload OK가 있으므로 로컬 복원을 서버 삭제로 해석하지 않는다. 사용자 월드1–6은 정리 대상에서 제외한다.

관련 최종 검증은 `live-final-command-results.json`에 있다. 개발 strict/query, L/C 참조, 제품 재생성/진입점/check/byte-identical 모두 통과. 최종 수치는 개발2605노드/21380간선·기능64, 제품5807노드/22303간선이다. 제품 structuralErrors0/stale0, canonical434파일 변경0, 공식 HEAD a07166f 유지. 기존 사본 대비 수정파일41개이며 신규 보충 증거14개와 observations 원본은 별도 추가물이다. 코드 추출 자체가 바뀌지 않아 B 전체 추출 검사를 불필요하게 반복하지 않았다.

최종 환경 후속 동기화 뒤 독립 검토 전달 SHA-256:

- 보충 observations: `b958c27b8f4428665882c952aaee4345f3047b75f44dfb5a0ca2a87bab55b40d`
- 최종15 사본: `5b09a387725610760ae97d344ce193c82c35270c5e54b5e5e2fa0f5ec2fa2409`
- 검토18 사본: `fc1f958fe57a2ef4ae42a0b525250c220df549f04a80dbb080edd24540efffe5`
- 제품 graph: `3e31724bd4df16681b6302524a73737ed977695cb6efc496a68114f7176bcef0`
- 제품 validation: `bc656a593e62a60a3530cff3cd484345da57517c92b3e88536d6245d94f0a4f3`

## 사용자 중단 결정 이후 환경 상태 동기화

최종15 §5.1을 반영했다. 사용자가 시험 월드 삭제는 별도로 요청하겠다고 하여 추가 시도를 중단했다.17:36:28 재복원도 mod54/AIPlayers43/localsteam248/remote40/registry 동일·verify0이다. MouseKeys 수집 설정4개(Flags62 포함)는 같고 활성 변화가 관측되지 않았으며 버튼 DOWN/놓기 입력은 실행하지 않았다. **NumLock 사전 실제 토글 상태는 수집하지 않아 전후 동일을 주장하지 않는다.** FoldRemote는 입력모드 변경 없이 화면 확보에만 쓰고 정상 연결 종료했다. 재시도 중 정확한 시험 월드3파일의 실제 Cloud 재다운로드가 관측됐으므로 Cloud 삭제는 사용자 별도 작업으로 유지한다.

제품 관측5기록은 byte-equivalent payload로 유지했다.15사본 교체와 환경 후속근거5파일을 추가하여 입력사본은 총19개다. 환경 상태만 `environmentRestoration.finalFollowup`에 추가했으며 개발 그래프·코드 추출은 다시 실행하지 않았다. `cleanup-followup-checks.json`에 입력해시 검증을 포함한 제품 재생성,check 오류0/stale0,byte-identical 및 canonical434파일 변경0을 기록했다. 제품 validation 해시는 이전과 같다.
