# 실화면 후속 최종 독립 검수

2026-11-05. 판정: **합격 — agreement에 한정한 실화면 후속 통합 완료.** 미해결 중대 결함은 발견하지 않았다. 전체 게임 상태·모든 목표/기억/전투 흐름의 완료 판정은 아니다.

검수자는 A/B/C/D 제작과 다른 컨텍스트다. 새 게임 조작, UI 소유권 취득, 제품·설치·저장 파일·그래프 수정, 작업 세션 변경은 하지 않았다. Review 폴더의 검수 실행기·명령 로그·이 보고서만 작성했다. 수정 후보는 부모와 D 담당에게 전달했고 A2 제작자가 자기 원본을 수정했다.

## 기준과 대상

- 제작 기준: `C:/Dev/_ref/head-agent-core/graph/build/02a-graph-definitions.md`, `02b-graph-workflow.md`, 이번 `agreement.md`.
- 실제 사용자 진입점: `work/지식그래프/tools/product.mjs`.
- 최종 통합 그래프 SHA-256: `b830cdb73180fc8f39e81a3bd00003ca206eae96cfe056f1ae7d60bd6d2ad764`.
- 최종 A2 manifest: `efcca234a7b0bf89a363450911af4891a0916b40bf8a4362470d65434f1a3293`.
- C overlay: `347be6e21726eb61d8664fc322dc353784dbe69cfb76a3af060dee2a6db3b166`.

## 실제 화면과 대표 흐름

요약 보고만 읽지 않고 다음 원본 PNG를 직접 열었다: A1의 01-world, 04-ai-help, 12-game-menu; A3의 new-user-05-player-management; C3의 01-start, 03-ai-typed, 04-ai-response, 05-end. A2 E01/E04/E12/E14 위치 매칭 PNG도 직접 확인했다. 이 검수는 전체 14장 각각의 전 픽셀 재검수가 아니라 5화면을 포함하는 대표·비대표 표본 검수다.

월드의 Claude 이름표, 일반 메뉴의 접속자 Luminous/Claude, 관리자 Luminous·차단된 플레이어 없음, 채팅 `/ai` 제출 에코와 상태 목록이 원문 문서와 맞았다. C3는 초기 월드 → 입력칸 `/ai` → 응답 → 입력 닫힌 월드의 순서가 PNG로 뒷받침된다. 슬롯 1도 시작과 끝에 유지된다. 진행 중 게임의 자연 변화는 있었으며 이를 정지된 상태라고 하지 않았다.

A3는 명령을 발견하지 못했고 메뉴·플레이어 관리 경로에서 AI 목표를 찾지 못했다. C3는 명령을 안내받아 조회·참여 확인에 성공했다. 두 결과를 합쳐 자연스러운 명령 발견 성공으로 바꾸지 않았다. C3의 현재 목표 미확인, 특정 동료의 행동 귀속 미확인, 빠른 출력 소멸·줄바꿈·포인터 가림은 C4/활동/최종 D 보고에 남아 있다. 코드의 Goal.Text 해석을 처음 온 사용자가 화면에서 이해한 사실로 승격하지 않았다.

실제 진입점에서 `user CKAI-ACT-01`, 입력/결과의 `show`, `feature slash:ai`, 해당 코드와 문서의 `source`를 실행했다. 결과는 다음의 좁은 사실을 지지한다.

`CKAI-ACT-01 → CKAI-02#3 → slash:ai → TryHandleChatCommand → StatusAll → AIWorldSettings.Members → data:…AIPlayerSpikeMod.cs:p`

- `TryHandleChatCommand` 444/454줄: 인자 없는 `/ai`는 status로 분기해 `Reply(StatusAll())`.
- `StatusAll` 666~669줄: 이름, 설정의 활성 여부, StageText, Walking일 때 Goal.Text, 짧은 명령 안내를 조합한다. A1/C3의 `Claude 켜짐/들어와 있음/없음`과 대응한다.
- `Members` 1505/1508/1509줄은 PathFor의 파일 p를 읽는다. `PathFor` 1549~1552줄은 `AIPlayers/<world>/settings.json`을 만든다. DB나 현재 게임 저장값을 직접 읽은 검사가 아니다.
- `Reply` 679줄의 큐와 `Update` 252줄의 FlushReplies 호출을 구분했다. `FlushReplies` 690/696줄의 게임 ChatWindow.RenderText reflection 경계를 확인했다. 큐의 다음 프레임 전달을 하나의 직접 call로 만들어 설명하지 않았다.
- 응답 생산 간선은 `slash:ai → CKAI-02#2-7`이다. 결과를 클릭해 명령을 실행하는 역방향 트리거가 아니다. `Luminous: /ai` 에코는 별도 게임 본체 경계로 남는다.

실제 함수 호출 trace나 HTTP/IPC 캡처가 없다는 한계가 `runtimeTrace:false`, source 대응 문서와 최종 보고에 유지된다. 일반 메뉴·HUD·이름표·플레이어 관리에는 가짜 renderer/control 기능이 붙지 않았다. user 조회의 20개 요소 중 18개 미매칭 이유도 출력된다. `/ai Claude` 취소 초안과 inv/mem/goal 미실행을 실제 실행으로 세지 않았다.

## 비대표 표본

씨앗 **20261105**. `x=(1664525*x+1013904223) mod 2^32` LCG. 활동 CKAI-ACT-01에서 직접 참조하지 않은 CK 화면요소를 ID의 `localeCompare(...,'en')` 순으로 정렬한 뒤 비복원 3개 선택했다. D 작성자의 seed 2026110506과 다른 독립 표본이다. 정확한 선택 실행은 `run-review.mjs`, 결과와 명령 전문은 `command-log.json`에 있다.

| 표본 | 진입점에서 연 원문 | PNG와 의미 대조 | 판정 |
|---|---|---|---|
| CKAI-03#8 | A2/CKAI-03.md:156~165 | A1/12 좌하단 `설정`, 매칭 E12 #8. 설정 실행은 미관측이고 가짜 기능 연결 없음 | 일치 |
| CKHUD-01#5-3 | A2/CKHUD-01.md:141~151 | A1/01 하단 세 번째 슬롯의 3·수량2, E01 H5-3. 아이템 이름은 확정하지 않음 | 일치 |
| CKAI-01#1-2 | A2/CKAI-01.md:56~63 | A1/01 코어 앞 이름표 없는 다른 캐릭터, E01 #1-2. PNG만으로 Luminous 동일성을 확정하지 않음 | 일치 |

## 위치 매칭 지적과 처리

처음 E04를 보고 부모 #2 채팅 상자가 아래 #2-7 목록을 감싸지 않는 것처럼 보여 수정 후보를 전달했다. 이후 manifest를 직접 확인하니 원래 부모는 `[28,313,487,531]`로 하단844까지 포함했고 자식과 경계가 겹친 것이었다. 따라서 최초 지적은 **좌표 누락 결함이 아니라 시각적 경계 식별 문제로 정정**한다.

A2 제작자가 E02~E10 부모 외곽 여백을 추가했다. 최종 E04 부모 `[24,309,495,539]`의 하단848이 자식 하단844를 감싼다. 수정 E04 PNG를 다시 직접 열어 확인했다. 최종 manifest의 증거 내 명시 부모·자식 64쌍을 독립 코드로 대조해 포함관계 실패0이었다. A2 작성자의 전체 정본+증거 94쌍 검사는 별도이며 이 독립 64쌍과 혼동하지 않는다. ID·문구·흐름은 바뀌지 않았고 최종 D 해시에 반영됐다.

## 실행·보존·버전 검사

실행 명령:

```powershell
node 'C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-05-지식그래프-실화면/Review/run-review.mjs'
```

실행기는 실제 product.mjs 진입점으로 overview/check/user/show/feature/source/legacy 23개 명령을 호출한다. 최종 실행 종료0, 모든 자식 명령 종료0, stderr 없음. `check`: ok=true, structuralErrors=0, stale=0. 실행기 최초 시도는 A2 manifest의 UTF-8 BOM을 처리하지 못해 검수기 자체가 종료1이었다. 검수기의 JSON 읽기만 BOM 허용으로 고친 뒤 재실행했다. 제품 진입점 오류가 아니며 제품 파일은 수정하지 않았다.

이전 개편의 D/graph.json·validation.json·report.md, B/code.graph.json, A/graph-A.json, C/graph-C.json, preserved/manifest.json 7개를 시작 해시와 독립 대조하여 모두 동일했다. 기존 결정 `pol.ai-own-character`의 legacy 조회에서 사용자 결정/확정 지위와 두 인용의 status=ok도 확인했다. D 작성자의 재생성·전체 기존 노드/간선·보존 사본 검사 보고도 읽었으며 그 검사를 내가 별도 재실행한 것으로 적지 않는다. 이전 HTTP 증거와 의도/인용을 새 실화면으로 덮지 않았다.

설치판은 76563bf, 그래프 코드는 e82eb87이다. B의 전체 30개 설치 소스 대조 결과와 추가4파일/변경10파일 경계를 읽었다. 독립 표본 AIPlayerSpikeMod.cs와 AIBodySkills.cs는 실제 설치 디스크 파일 및 `git show 76563bf:<path>`를 읽어 CRLF→LF 정규화 SHA-256을 다시 비교했으며 B 기록과 모두 일치했다. 이는 디스크 동일성 검사이고 메모리 내 로드 어셈블리·실제 함수 coverage 검사가 아니다.

현재 README와 제품그래프-사용법은 실화면 5개·요소57개·A2 증거14장·C3 조회 결과를 안내한다. 현재 상태를 ‘실게임 전부 유예’로 적던 문구는 갱신됐다. 이전 D 보고의 당시 유예 기록은 그대로 보존하는 것이 맞다. 모든 게임 상태를 완료했다고 과장한 문구는 최종 결과에서 발견하지 않았다.

## 남은 범위

합격은 이미 실행 중인 월드의 동료 확인과 `/ai` 조회를 실제 화면에서 관측하고 개발 그래프·원문으로 연결한 범위다. 전체 상태 전수, 목표/가방/기억 명령의 실제 사용, 멤버 전체 실행, 저장/재입장, 외부 모델/기억검색 성공, 게임 본체 renderer/control 추출, 76563bf 전체 전투판 추출·실행 coverage는 미완이다. inv는 실행 기술을 중단할 수 있고 mem은 초기화/저장/마이그레이션을 할 수 있다는 코드 경계도 유지한다.

C4의 명령 발견, 발화자·목표 구분, 메시지 가독성/보존, 휠 부수 효과, 조회 부수 효과는 후속 제품 판단 목록이다. 이 검수는 이를 승인된 제품 수정이나 새 정책으로 만들지 않는다.
