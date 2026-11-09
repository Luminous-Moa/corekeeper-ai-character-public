# D — 실제 게임 화면 후속 통합

2026-11-05. A/B/C를 작성하지 않은 새 통합 컨텍스트에서 제작 지침 02a/02b와 이번 agreement를 적용했다. 별도 UI 입력·제품 코드·설치본·게임 저장·세션 context 변경은 하지 않았다. 실제 화면은 A1/A3/C3 원본 PNG와 조작 기록으로 대조했다. 이 보고서는 범위를 한정한 후속 통합 결과이며 전체 제품 실행 검증 완료를 뜻하지 않는다.

## 결과와 진입점

기존 `work/지식그래프/tools/product.mjs`는 이번 `D/query.mjs`, `product-build.mjs`는 `D/integrate.mjs`를 호출한다. README와 제품그래프-사용법의 현재 안내를 갱신했다. 이전 개편의 D 생성물·보고서, B 코드 층, A HTTP 관측, C 정책·결정·인용은 변경하지 않았다.

- 총 3,585 노드 / 13,315 간선. 이전 노드의 모든 필드와 모든 간선 보존 검사를 통과했다.
- 추가: 실제 화면 5개, 화면요소 57개, 관측 표시 규칙 후보 6개, 활동 CKAI-ACT-01 하나. 제품코어/저니/IA는 기존 한 개씩 유지하고 C 보충 메타데이터만 추가했다.
- 기존 기능 22개를 그대로 재사용했다. `/ai status/help/inv/mem`을 별도 가짜 기능으로 만들지 않았다.
- A2 증거는 14장이다. C3의 별도 원본 5장은 동일 화면의 독립 재현 증거이며 A2 증거 수에 섞지 않았다.
- 표시 규칙은 원본의 후보 status와 `사용자 확정 정책 아님` authority를 유지한다. 기존 43개 정책의 권위·확정도를 바꾸지 않는다.
- 원본·추출 규칙·스크립트·현재 진입 안내 356개를 해시로 등록했다. 최종 그래프 SHA-256은 `b830cdb73180fc8f39e81a3bd00003ca206eae96cfe056f1ae7d60bd6d2ad764`.

기준 입력: C/overlay-source.json `347be6e21726eb61d8664fc322dc353784dbe69cfb76a3af060dee2a6db3b166`; 최종 A2/manifest.json `efcca234a7b0bf89a363450911af4891a0916b40bf8a4362470d65434f1a3293`. 독립 시각검수에서 요청한 채팅 부모 상자 포함 여백 수정 후 재생성·재검증했다.

## D-1·D-2 — 조작과 기존 입구 매칭

`A1 03-ai-draft → 04-ai-help`의 실제 `/ai` 제출과 `C3 03-ai-typed → 04-ai-response`의 독립 재현을 `CKAI-02#3 → slash:ai`로 매칭했다. 결과는 `slash:ai → CKAI-02#2-7`이다. 결과를 조회할 때는 생산 기능을 역방향으로 찾으며 결과 클릭 트리거로 뒤집지 않았다.

근거는 명령 문자열, 화면 목록 문구, B의 기존 TryHandleChatCommand/StatusAll 본문 대응이다. A1의 `local help response`라는 서술은 처리 위치 증거로 쓰지 않았다. `/ai`는 기본 status 분기이며 이름 `04-ai-help`를 전용 help 명령으로 해석하지 않는다. HTTP/IPC 요청 캡처·상태 코드·stream·실제 함수 trace는 없다.

`/ai Claude`는 입력 후 취소했다. 빈 입력, 취소 초안, 미실행 `/ai inv`, `/ai mem`, `/ai goal`은 실행 증거에서 제외했다. 기존 hook ChatSendPrefix와 slash 처리 함수의 정적 연결을 재사용했으며 게임 본체 프론트 코드를 새로 만들지 않았다. 호출되지 않은 나머지 입구를 죽은 코드로 판정하지 않는다.

미매칭 요소 55개는 `validation.json/D1/unmatched`와 각 요소 `mappingBoundary`에 이유를 적었다. 플레이어 관리·접속자 목록·이름표·HUD·채팅 에코는 외부 게임 코드 경계다. 전투 메시지는 화면에 보인 사실까지만 연결하며 e82eb87 밖 전투판의 특정 함수 실행으로 매칭하지 않는다. 활동의 20개 요소 중 직접 기능 대응이 없는 18개는 `user CKAI-ACT-01`의 gaps에서 그 이유를 보여 준다.

## D-3 — 실제 물음에서 원문까지

[questions.json](questions.json)에 4개 질문의 요소 원문, 코드 원문, 제한 깊이 16의 경로를 저장했다. A1/04-ai-help.png와 C3/04-ai-response.png를 직접 보고, 고정 snapshot의 TryHandleChatCommand·StatusAll·StageText·Members·PathFor·Reply·FlushReplies 원문을 읽었다.

| 물음 | 그래프 경로·답 | 관측 한계 |
|---|---|---|
| Claude 켜짐/들어와 있음은 어디서 오는가? | CKAI-ACT-01 → CKAI-02#3 → slash:ai → TryHandleChatCommand → StatusAll → AIWorldSettings.Members → 데이터 `AIPlayerSpikeMod.cs:p`. PathFor 원문은 `AIPlayers/<world>/settings.json`. StageText는 참여 단계 문구 | A1·C3의 출력과 코드 분기는 맞는다. 현재 설정 파일 내용·실제 함수 trace는 읽지 않음 |
| 상태 뒤 `/없음`은 아무 행동도 안 한다는 뜻인가? | StatusAll 원문에서 Walking 때 붙는 Goal.Text 자리로 대응 | 현재 기술/자기 방어/최근 행동 전체를 뜻하지 않음. C3 사용자는 현재 목표·필드 의미를 확인하지 못함 |
| 결과와 에코는 어떻게 표시되는가? | Reply의 queue와 다음 Update → FlushReplies → 외부 ChatWindow.RenderText 경계 | 비동기를 직접 call 사슬로 만들지 않음. `Luminous: /ai` 에코는 게임 출력으로 별도 미매칭 |
| 플레이어 관리/공격 로그로 목표·행동 주체를 확정할 수 있나? | CKAI-03#7 → CKAI-04 관측은 관리자·차단 관리. 공격 로그는 요소 원문까지만 제공 | A3는 AI 목표를 찾지 못함. AI 접두어만으로 특정 동료 귀속·현재 목표를 확정하지 않음 |

사용 예:

```powershell
node tools/product.mjs user CKAI-ACT-01
node tools/product.mjs show "CKAI-02#3"
node tools/product.mjs source "CKAI-02#2-7"
node tools/product.mjs feature "slash:ai"
node tools/product.mjs source "code:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs#AIWorldSettings.Members(string)"
```

설치 소스 대응판은 76563bf, 코드 그래프 원본은 e82eb87이다. B의 30개 설치 C# 정규화 해시 일치는 디스크 동일성 증거다. 선택 코드 51개 중 50개 본문 대응을 재사용하며, AIBody.StartSkill 변경 및 실행판 추가 4파일·기존 10파일 변경을 숨기지 않는다. 메모리 내 로드 코드 동일성·전체 전투 coverage는 주장하지 않는다.

## D-4 — 정합성과 비대표 표본

- 중복 ID·끊긴 참조 0, 원본 해시 변경/없음 0.
- 기능과 연결됐지만 활동이 없는 요소 0. 새 화면 5개는 모두 활동에서 사용한다.
- 활동 없는 화면 후보는 기존 BRG-01(격리 시작), BRG-04(unknown-member), BRG-05(malformed) 3개 그대로다. 이전 D 보고의 실제 개발 진단 출력이며 정상 사용자 활동으로 확인되지 않았다는 처리를 유지한다. 목적 없는 제품 화면이라고 단정하지 않는다.
- A2 manifest의 6개 표시 규칙은 원문 문자열로 적재하고 설계서 `화면 정책` 절을 연결했다. 관측 후보의 일반화·기존 사용자 정책과 충돌하는 신설은 없다.
- Return/Escape와 A3 플레이어 관리 진입은 관측했지만 게임 navigate 코드가 범위 밖이므로 코드 간선 없음만으로 막힌 동선이라 판정하지 않는다. 새 활동의 다음 활동 ID는 없고 기존 저니 순서를 바꾸지 않았다.
- C3는 `/ai`를 안내받은 뒤 참여 확인 성공, 행동 종류 부분 이해였다. C4는 메시지가 보이는 순서와 전송 후 입력 자동 닫힘을 반영했다. 명령 발견 가능성과 목표/발화자 식별의 미결은 유지했다.

비대표 추출 씨앗은 **2026110506**이다. 대표 입력·상태 결과·Claude 월드 이름표를 제외한 54개 요소에서 고정 정렬 후 LCG로 5개를 비복원 선택했다. [verification.json](verification.json)에 원문 줄·해시·좌표를 저장했다. 아래 판정은 원본 PNG를 직접 읽어 확인한 것이다.

| 표본 | 원문/PNG 대조 | 판정 |
|---|---|---|
| CKAI-03#10-2 | E12 우상단 접속한 플레이어의 Claude 행과 A2 해당 절 | 일치. AI 제어 기능이나 이름표 renderer로 연결하지 않음 |
| CKHUD-01#5-1 | E01 하단 첫 칸의 1·24, 선택 테두리 | 일치. 공통 HUD 위치는 공통 영역 정의를 증거별로 연결 |
| CKHUD-01#5-8 | E01 하단 8번 칸·수량 3 | 일치. 아이템 이름/사용 기능은 추정하지 않음 |
| CKAI-03#7 | E12 좌하단 `플레이어 관리`, A2의 A1 미실행/A3 진입 구분 | 일치. A1 화면에 있다고 A1이 클릭한 것으로 합치지 않음 |
| CKAI-02#2-2 | E02 `AI 공격 완료`, MushroomEnemy 처치, 3.9초; 두 70 사이 기호 | 일치하되 중간 기호 불확실 표지를 유지. 코드 문자열로 화면 문구를 복원하지 않음 |

## 재생성과 보존 검증

다음 검사는 최종 A2 수정 이후 모두 통과했다.

```powershell
node tools/product-build.mjs
node tools/product-build.mjs --check
node tools/product.mjs check
node ../2026-11/2026-11-05-지식그래프-실화면/D/verify.mjs
node ../2026-11/2026-11-05-지식그래프-실화면/D/questions.mjs
node ../2026-11/2026-11-05-지식그래프-실화면/D/check-entrypoints.mjs
```

[verification.json](verification.json): 같은 입력 두 번 생성과 디스크 결과의 바이트 동일성, 기존 모든 노드 필드·간선, 보존 사본 전체 해시, 57개 요소 원문 ID, 입력/결과 방향, 파일 데이터 경로, 제품코어/기능 중복 없음, 표시 후보 지위 보존.

[entrypoint-checks.json](entrypoint-checks.json): 실제 진입점 overview/check/user/source/feature/legacy 및 build --check 총 7개 호출 모두 종료 코드 0, 경고 없음. check의 structuralErrors/stale는 모두 0. 기존 F.action-select 조회도 유지된다. [preexisting-hashes.json](preexisting-hashes.json)은 이전 주요 생성·원본 7개 불변의 시작 해시다.

## 남은 범위와 판정

이번 요청의 실제 화면 관측 → 활동/요소/기능 연결 → 원문 조회 통합은 완료했다. 전체 상태 전수, 목표·가방·기억 명령, 저장·재입장, 외부 모델/기억검색 성공, 게임 본체 renderer와 전체 전투판 재추출은 완료로 승격하지 않는다. inv는 기술 중단, mem은 초기화/저장/마이그레이션 가능성이 있어 이번에 실행하지 않았다.

C4의 CKAI-C-U01~05(명령 발견, 동료/목표 구분, 메시지 가독성·보존, 휠 부수 효과, 조회 부수 효과)는 제품 판단 목록이다. 제품 변경 승인이나 새 사용자 정책으로 취급하지 않았다. 최종 독립 검수는 부모가 별도 컨텍스트에서 수행한다.
