# 책임별 기존 동작 근거와 수정판 실게임 계획

2026-11-06. 대상 [03 명세 §8](03-spec-spatial-ownership.md). 기존 보고서와 실제 로그 파일의 존재, a07166f 기준 소스의 저장 경계를 읽었다. 게임·Unity·설치·브리지·테스트는 실행하지 않았다. 원본 runtime 자료 내용을 이 문서로 복제하지 않는다.

**M 착수 의견: 조건부로 충분하다.** 같은 타입/본문의 기계적 파일 이동은 M 직전 커밋→M 후 커밋의 토큰 동일성, 타입·attribute·정적 필드 순서·scope·호출 순서 보존, 기존 로그 근거, 최종 수정판의 실제 E2E를 합쳐 검증하면 된다. 모든 과거 동작을 구판 게임에서 다시 실행할 필요는 없다. 저장 후 동일 월드 재입장의 완전한 사전 대조는 아래 기록에서 찾지 못했지만, 저장 경로/API가 명확하고 동일본문 이동이 입증되면 **수정판 실제 저장·종료·재입장 대조**를 필수로 두어 M을 시작할 수 있다는 의견이다. 최종 착수 판단은 HEAD 담당이다.

## 근거 묶음

| 기호 | 실제 확인된 빌드·보고 | 원자료/상태 |
|---|---|---|
| A | a07166f [34-내문-실게임](../2026-11/2026-11-05-코어-안전지대/34-내문-실게임.md) | `C:/Dev/_scratch/mydoor-live/collect/Player.log`, `decisions-142138.log`, `movetrace-142138.csv` 존재. 보고는 AIPlayers-mydoortest의 memory 사본도 근거로 명시 |
| D | 4238cce [29-문단계-실게임](../2026-11/2026-11-05-코어-안전지대/29-문단계-실게임.md) | `C:/Dev/_scratch/door-settle-live/collect/Player.log`, `all-nudges.txt` 존재. 보고의 collect decisions/movetrace 및 tools/an.mjs 대응 사용 |
| P | 9df72b7 [제작대-놓기15](../2026-11/2026-11-05-제작대-놓기/15-실게임4.md) | 같은 폴더 `runs4/Player.log.gz` 존재. decisions-124101.log.gz, bridge-plan.jsonl, notes.txt는 보고에 명시 |
| N | 7fcef73 [자연어명령11](../2026-11/2026-11-05-자연어-명령/11-실게임4.md) | 같은 폴더 `runs4/Player.log.gz` 존재. bridge-plan.jsonl, notes.txt는 보고에 명시 |
| L | e0c671b [11-04 리팩토링 L2](../2026-11/2026-11-04-리팩토링/9-L2.md) | `runs/L2/collect/Player.log` 존재. decisions·ASM corpus·fake requests·replay 결과는 보고의 수집물 목록 |

이전 빌드의 근거는 a07166f 전체 동작의 직접 실행 증거가 아니다. A가 최신 직접 근거이며 D/P/N/L은 책임별 역사적 근거다. M의 직접 소스 대조 기준은 앞 단계를 포함한 **M 직전 실제 커밋**이다. 이미 바뀐 단계 0~2를 a07166f와 “전부 동일본문”이라고 보고하지 않는다.

## 책임 대응표

| 책임/옮길 형식 | 기존에 실제 본 것·로그 대응 | 미관측/한계 | 수정판 최소 확인 |
|---|---|---|---|
| 명령 대상·all stop: Mod.Chat, Body.Commands | N §1: 멘션 19/19(@모두·한글 이름 포함), 멤버 없는 `/ai stop`으로 네 AI 정지. Grok periodic 요청 중 stop(t=1812.9): `request dropped`, 열린 지시 멈춤, 다음 질문에서 멈춘 일을 다시 걸지 않음. L §3⑨⑩: 캐릭터별 목표/rev/출처, `/ai all stop` 후 동시 auto_on, 타 멤버 source 0 | L은 자연어 개편 전. N의 완료 순간 set 답 갈래는 미확인(keep만 관측). 로그가 모든 dispatch 분기를 증명하지 않음 | 단일 지정 목표→다른 캐릭터 상태 불변, 두 캐릭터 서로 다른 지시, 진행 중 `/ai all stop`→모든 목표/요청 정지·후속 적용 없음. 자연어 1건의 member/order/source 연결 |
| 입력·patch·Body/문 기술 | D: 문 통과 32번/놓기6번, 누름72번 첫 누름. nudge113개에서 다음 틱 이동 모두0, 2틱0, **0틱2**. `BODY: settle nudge`의 tick과 movetrace server-applied 연결. 공격 교체 중 꼬리 닫기 실제1초. A: 목표 완료 후 내 문 닫기2번(0.1/0.7초), `/ai off` 후22초 SEND0 | D의 0틱2·벽을 모르는 보정·비관적 닫기 보고 유지. **취소 직전 아직 미전송인 press queue를 stop으로 버리는 장면**은 이 보고들로 직접 입증되지 않음 | 문 열기→통과→닫기, 실행 중 stop/목표교체 후 원래 입력 미재개. nudge/use tick 로그와 actual 문 상태 대조. 미전송 취소는 가능한 타이밍을 관측하거나 미관측 명시+기존 queue 시험 근거 분리 |
| Memory/CharacterStore/WorldSettings | A: 문 기억이 memory에 남음, off→on(t=801), 부활·순간이동 직후 엉뚱한 forgotten0. N: 네 AI off 시 “저장하고 나감”. L: 멤버별 goals의 타인 출처0, on/off 반복 후 목표 없음 | **시험 월드를 저장하고 완전히 나간 뒤 동일 월드에 들어와 문·기억·가방·참여 상태를 항목별 대조한 기록은 이 근거 묶음에서 찾지 못함.** off/on은 호스트 월드 재입장과 다름. L의 재기동은 복원 설치본/Cloud 확인이지 동일 시험 월드 재입장 아님 | 동일 시험 월드/캐릭터의 문·기억 좌표·식별 가능한 가방 품목/수·참여 멤버를 저장 전 기록→정상 저장 종료→같은 월드 재입장→새 시험 give 없이 비교. path/world/member 동일·이중 생성 없음. 필요 시 두 멤버 값을 달리해 교차 오염 확인 |
| 공간 사실·문·물체·이동/Tunnel | A: 내 문 너머 reach8줄 위협 아님, 시야 밖 문 제거 후100초 기억 유지→다시 본1.1초 후 forgotten, pick2번의 옛 문 이전, 내 문 통과/메우기. P: 물가 다리4칸 실제 놓기·땅 거절, 새 모루 위치 보고, 모루 놓기→검 제작, 둘레 메우기3벽1문. L: 길 없음 실패→90초 후보 제외와 목표 기지 복귀 | A: 시야 밖 문 너머 적 못 옴·물/용암·unloaded 접속 미관측, 후퇴 경로 오류1건. P: digedge·두 문 사이·Q1·둘레 재계산 중단·home 미관측. **기억 시설 재접근·밭 direct 탐색의 대표 실행도 이 묶음에서 확정 못 함** | 문 소유/현재·시야 밖·재관측 흐름, 제작 시설 접근·다리/놓기. Q/R 변경부는 첫/마지막/any 의미·PlanC 순서/예산/캐시를 정적 계약/기존 시험과 연결. 실제 밭/기억 시설/재계산 중단을 보지 못하면 그대로 남김 |
| 판단 교체·위협 기록/캐릭터 scope | A: 닫힌 내 문 밖 걷는 적 동안 판단/자기방어0, 문 열리고 적1.4칸→SELF-DEFENCE 시작, 문 잊은 뒤3.9칸 적 반응. N: 요청 중 stop 폐기와 지시 부활 없음. L: 4명 member/source 분리·Cur outside scope0, `queued answer not used`, ASM 2406 기록 일관성 | L ASM은 동일 rf-S7 기록 재생으로 **구판과의 동등성 증거 아님**. N `plan_done` set 갈래 미관측. L replay BAD28은 fake0초 응답의 기존 timing 결과. **위협 기록의 범위 밖 update/evaluation 분리 전체를 라이브로 봤다는 근거 없음** | 두 캐릭터 교대, 닫힌 문 뒤 적→열린 문/보이는 적, 목표교체·stop 중 늦은 답 폐기. O는 기존 replay/정적 호출순서와 새 실제 로그를 결합하고 update/query 타이밍을 임의 변경하지 않음 |

## 저장 API 계약과 M 착수 한계

a07166f 소스에서 확인한 경로(사용자 파일을 열어 내용 복제하지 않음):

- `AIWorldSettings`: `AIPlayerSpikeMod.cs`의 `AIPlayers/<worldId>/settings.json`.
- `AICharacterStore`: `AIPlayers/<worldId>/<aiName>.json`, `StateOf(a)`는 `AISlot.CharacterStore`; 구형 루트 단일 파일과 월드별 파일 구별을 그대로 유지.
- `AIMemory`: `AIExplore.cs`의 `CharDir`는 `AIPlayers/<worldId>/<AIAgent.Cur.Id>`, `Ensure`/`Save`/`Load`와 migration 순서를 보존. `SeenTile` 같은 읽기가 Ensure를 부르는 것도 이동 전후 동일해야 함.

이는 타입 이동을 시작할 계약 근거이며 실제 재입장 복원 성공을 대신하지 않는다. M의 `movecheck`는 partial/using/주석/경로 외 실변경0, 모든 타입 포함·중복0, static field 순서·attribute·namespace 동일을 요구한다. 새 meta·Unity/game source inclusion은 최종 compile/install manifest/hash로 확인한다.

다음 경우만 M 전 추가 관측/명세 확인으로 돌린다: Unity/Harmony가 파일명·선언 배치에 의존하는 미확인 등록 경계, serializer가 옮길 형식의 정체성을 바꾸는 구조, state 초기화나 저장 scope/시점 변경, 본문 이동으로 설명되지 않는 소스/API 불명확성. 단순히 과거 로그가 없다는 이유만으로 모든 책임의 구판 라이브를 새로 요구하지 않는다. Q/R/O의 의미 변경 검증은 M의 동일본문 증명에 포함되지 않는다.

## 수정판 E2E 순서와 판정 자료

1. 최종 후보 커밋/설치 .cs 목록·hash·manifest와 게임 컴파일 성공을 연결한다. 백업/시험 월드 정리는 02 문서 절차.
2. 두 캐릭터 scope와 단일 지정→자연어 제작/놓기→문 통과·재료/시설 접근→제작/놓기 결과·기억까지 연결한다. 가짜 응답이면 실제 상위 LLM 실행과 구별한다.
3. 진행 중 교체/stop/allstop과 늦은 답, 문 뒤 적/보이는 적을 확인한다. Player의 명령·GOAL·PLANNER·SKILL, decisions의 SEND/RESULT·후보, movetrace의 tick/use/nudge를 같은 member/time으로 맞춘다.
4. 저장·완전 종료·동일 시험 월드 재입장을 실제로 수행하고 저장 경로/멤버/문·기억/가방/참여 상태를 항목별 대조한다. 성공한 경우에만 저장 경계 실게임 통과로 기록한다.
5. 보지 못한 조건과 기존 결함을 그대로 남긴다. 로그 문구가 같다는 이유로 새 구조의 모든 행동을 통과 처리하지 않는다. 신규 오류·후보 차이·저장 불일치가 있으면 원인 확인 전 완료로 보고하지 않는다.

화면 상태: FoldRemote 연결 뒤 Windows sky 캡처는 확보됐으나 태블릿 자체는 마지막에 “새 화면 대기”였다. 본 표 준비 중 UI 추가 조작은 하지 않았다. 유지 입력·시험 월드 정리는 live 담당이 확인한다.
