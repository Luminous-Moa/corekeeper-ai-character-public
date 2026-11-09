# A-1 실제 실행 탐색

기준: 2026-11-05, HEAD 지정 코드 `e82eb87`의 실행 패키지. 패키지는 내용 열람 없이 A/isolated/runtime로 복제했다. 운영 설치판·게임판과 같다는 주장은 하지 않는다. Node 실행판은 실패 출력에 `v24.21.0`로 나타났다.

## 실제로 확인한 출력 × 상태

| 관측 ID | 받는 출력 | 상태와 가는 법 | 새 원문 | 판정 |
|---|---|---|---|---|
| OBS-01 | 로컬 접속 실패 | GET http://127.0.0.1:8331/v1/health | raw/health-02.stderr.txt, raw/health-02.meta.json | curl exit 7. HTTP 응답 없음. 클라이언트/환경 관측이며 제품 응답이 아님 |
| OBS-02 | 시작 오류 | 최초 HEAD 브리프의 B/snapshot bridge를 Node로 실행 | raw/bridge-start.stderr.txt, raw/bridge-start.meta.json, raw/bridge-start.result.json | ERR_MODULE_NOT_FOUND, knowledge/kg.mjs 누락. 실제 설치 패키지 시작 실패. 게임 실행 증거 아님 |
| OBS-03 | 브리지 시작 출력 | 완전 패키지를 A로 복제, 별도 포트·빈 데이터로 Node 실행 | raw/bridge-isolated.stdout.txt, raw/bridge-isolated.stderr.txt, raw/bridge-isolated.meta.json | game graph NOT FOUND 안내 뒤 listening on http://127.0.0.1:18331 |
| OBS-04 | 건강 상태 JSON | GET http://127.0.0.1:18331/v1/health | raw/isolated-health.body.txt, .headers.txt, .meta.json | HTTP 200, ok:false, 4멤버 found:false/login:false/ok:false, busy:[] |
| OBS-05 | 없는 경로 오류 JSON | GET http://127.0.0.1:18331/ | raw/isolated-not-found.body.txt, .headers.txt, .meta.json | HTTP 404, ok:false, error:"not found" |
| OBS-06 | 알 수 없는 멤버 오류 JSON | POST http://127.0.0.1:18331/v1/plan, requests/unknown-member.json | raw/isolated-unknown-member.body.txt, .headers.txt, .meta.json | HTTP 400, error:"unknown member", 허용 이름 claude/gpt/gemini/grok 안내 |
| OBS-07 | JSON 구문 오류 JSON | POST http://127.0.0.1:18331/v1/plan, requests/malformed.json (단일 `{`) | raw/isolated-malformed.body.txt, .headers.txt, .meta.json | HTTP 500, error:"bridge", JSON parse detail |

health-01은 최초 연결 실패 수집이며 PowerShell이 stderr를 장식했다. health-02는 동일 실패를 다시 직접 실행해 네이티브 stderr 원문을 별도 파일로 보존한 결과다. 두 번 모두 새 요청이다.

## 조작 순서와 상태 변화

1. 기본 주소 GET 실패 → 프로세스/포트 읽기 전용 조사. raw/environment-01.json에 8331 리스너와 필터에 맞는 CoreKeeper/bridge 프로세스가 비어 있음을 보존했다. 모든 다른 프로세스가 없다는 뜻은 아니다.
2. 초기 실행 패키지 시작 실패 → 부모에게 실행 절차 보완 요청. 살아 있는 최초 PID 없음 확인.
3. 완전 패키지 격리 시작 → 18331 응답 확인.
4. health → root 경로 → unknown member → malformed JSON 순서로 요청. 각각 도구가 URL·method·시작/끝 시각·status·명령 인수·응답 원문을 저장했다. 응답은 비스트리밍 HTTP JSON이며 스트림 이벤트는 관측되지 않았다.

요청 identity는 메서드와 경로 그대로 보존한다. 기능 ID나 구현 연결은 이 단계에서 추측하지 않는다. health의 HTTP 200은 전송 성공이며 AI 사용 준비 완료를 뜻하지 않는다.

## 격리 조건

- 포트 18331, `AI_BRIDGE_MEMBERS=','`, `AI_BRIDGE_FAKE_CHAT` 미설정.
- AI_BRIDGE_CONFIG와 AI_BRIDGE_LOGS는 A 하위. aiPlayers는 빈 A 전용 폴더, kgData는 존재하지 않는 A 전용 경로.
- 부모의 실행 안전 브리프에 따라 members 빈 설정은 로그인/할당량 preflight를 하지 않는다. A는 이를 소스로 확인하지 않았다. 이 환경의 found/login:false를 실제 사용자 설치·로그인 상태의 증거로 해석하면 안 된다.
- 외부 CLI를 호출하는 정상 plan 요청, 빈 body, `{}`는 보내지 않았다. unknown member와 malformed 두 입력만 부모가 사전 검증 경로로 확인해 주었다.
- 서버가 내보낸 경고와 오류를 수정하지 않았고 실제 게임을 시작하거나 조작하지 않았다.

## 미관측 상태

| 범위 | 상태 | 이유 |
|---|---|---|
| health | ok:true, 로그인됨, 사용량/구독 확인, busy 비어 있지 않음 | 외부 인증/모델 호출과 운영 계정 조작 제외 |
| plan | 정상 목표, recall, hold, ask, 사용량 제한, 동시 요청/취소/timeout | 외부 CLI 또는 게임 상태가 필요할 수 있어 미실행 |
| 게임 | /ai status, 참여·퇴장, 캐릭터 생성, 이동·전투·제작·농사·저장·동료 대화 | 부모 조사상 /ai status도 게임 chat 입력 경로. 독립 읽기 CLI 미확인, 게임 조작 금지 |
| 데이터 | 기존 월드/캐릭터/기억/KG가 있는 정상 상태 | 사용자 데이터를 이번 격리 실행에 연결하지 않음 |
| 버전 | 현재 운영 설치판과 기준 커밋 일치 | 운영판 실행 관측 없음 |

이 목록은 부분 관측이다. 모든 명령과 모든 상태를 실행했다거나 실제 AI 동료 플레이를 검증했다고 주장하지 않는다. 사용자 후속 지시로 실게임 화면·컴퓨터 유즈 확인은 후속으로 유예되었다. HTTP 출력 실측만 현재 결과로 쓰고, 추가 게임 화면 경로 조사는 중단했다.
