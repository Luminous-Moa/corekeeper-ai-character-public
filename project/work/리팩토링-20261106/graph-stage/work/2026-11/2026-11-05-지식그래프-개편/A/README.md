# A 단계: 실제 사용 관측 결과

이 결과는 **격리 브리지의 실제 HTTP·시작 출력에 한정한 부분 관측**이다. 실제 게임 화면·컴퓨터 유즈는 사용자 지시로 후속 유예했다. 정상 AI 동료 플레이나 전체 명령/상태의 검증 완료를 뜻하지 않는다.

## 읽는 순서

1. `scope.md`: 범위·자료 분리·후속 유예.
2. `A1-exploration.md`: 실제 실행·요청 순서, 출력 상태 목록, 미관측 항목.
3. `A2/screens.md`: 별도 출력 기획자 컨텍스트가 원문에서 작성한 출력설계서. BRG-01~BRG-05, 요소 ID, 원문 위치 및 요청 매핑.
4. `A3/A3-first-user-report.md`: 시작 주소·원하는 것·변경 금지 조건만 받은 새 사용자 컨텍스트의 실제 GET 결과. A1/A2 미제공.
5. `A4/inferences.md`: A1~A3만 받은 별도 제품 기획자의 부분 추정.
6. `graph-A.json`: A2/A4 문서 선언에서 재생성한 임시 A 그래프. 관측과 추정은 class로 구분한다.

## 재생성 및 확인

`node build-graph-A.mjs`는 명시된 A2/A4 문서의 JSON 선언만 파싱한다. 제품 코드나 B 그래프를 읽지 않는다. 중복 ID·끊어진 연결·연결 이름·HTTP method/path/status와 원문 메타데이터를 확인하고 `graph-A-validation.json`을 쓴다.

`node check-samples.mjs`는 seed 20261105로 JSON scalar 화면요소 7개를 뽑아 원문 JSON pointer 값과 대조해 `sample-check.json`을 쓴다. A2의 별도 재생성/원문 대조 도구는 `A2/build_screens.py`다. 구조 검사와 표본 일치는 전체 제품 동작 검증을 대신하지 않는다.

## D에서 사용할 요청 매핑

| 관측 | 출력 ID | 메서드 | 경로 | 실제 HTTP 상태 | 본문 |
|---|---|---|---|---|---|
| OBS-04 | BRG-02 | GET | /v1/health | 200 | raw/isolated-health.body.txt |
| OBS-05 | BRG-03 | GET | / | 404 | raw/isolated-not-found.body.txt |
| OBS-06 | BRG-04 | POST | /v1/plan | 400 | raw/isolated-unknown-member.body.txt |
| OBS-07 | BRG-05 | POST | /v1/plan | 500 | raw/isolated-malformed.body.txt |

각 raw 파일의 같은 접두사 `.meta.json`에 전체 URL·시각·인수·상태를 보존했다. 기능 ID는 A에서 만들지 않았다. 본문 화면요소 위치는 A2의 JSON pointer/줄 번호로 읽는다. 명령을 보냈다는 사실만으로 특정 내부 함수가 실행됐다고 단정하지 않는다.

## 실제 실행판과 환경

- 기준 코드: HEAD 지정 `e82eb87`. HEAD가 제공한 실행 패키지를 내용 열람 없이 `isolated/runtime`로 복제했다. 버전 provenance는 `raw/bridge-isolated.meta.json`에 있다.
- 실제 서비스: `http://127.0.0.1:18331`, 이번 작업 소유 PID `60784`. 부모의 C-3/D 읽기 검증 요청에 따라 인스턴스를 유지했다. 운영 게임/브리지 프로세스와 별개다. 후속 담당이 사용을 마치면 이 소유 PID와 포트의 일치를 확인하고 종료한다.
- `AI_BRIDGE_MEMBERS=','`, fake chat 미설정, 빈 AIPlayers, 없는 KG 경로, A 전용 config/logs. health의 false/null은 이 격리 조건에서 얻은 값이며 운영 계정/설치의 실패 증거가 아니다.
- 실제 새 결과: health HTTP 200이지만 본문 ok:false, 없는 경로 404, 알 수 없는 member 400, 잘못된 JSON 500. 시작 콘솔은 game graph 미발견과 listening을 표시했다.
- 기본 포트 접속 실패와 최초 불완전 실행 패키지의 모듈 누락은 환경 관측으로 별도 보존했다. curl의 000을 HTTP 상태나 제품 JSON 응답으로 세지 않는다.

원문은 `raw/`와 A3 폴더에 있다. 과거 로그·mock·replay를 새 실행 증거로 사용하지 않았다. 정당한 plan 요청과 외부 모델 호출, 게임 명령, 저장 변경은 수행하지 않았다.

## 실행 출처 보완: 배포 줄바꿈과 외부 의존성

실행 시작은 `2026-11-05T01:03:33.6410275Z`이며, 실행에 사용한 A/isolated/runtime는 최초 제공된 **git archive 배포본**이다. 이후 HEAD가 해당 archive의 Windows EOL 변환을 확인했고 B의 snapshot/runtime-package를 git cat-file 원바이트로 교정했다. A 실행 복제본은 교체하지 않았다. 따라서 A가 실행한 파일을 현재 B 교정본과 원바이트 동일하다고 주장하지 않는다.

`provenance/runtime-provenance.json`은 보관된 A 실행 복제본을 기준 커밋 `e82eb87f25641cc7c31e6216f64e20a34e58d0c0`의 git blob과 자동 비교한 해시 manifest다. 제품 텍스트를 출력하거나 해석하지 않고 바이트 해시와 CRLF→LF 정규화 해시만 비교했다.

- 비교 파일 53개: 바이트 일치 3개, EOL만 다른 파일 50개. LF 정규화 내용 불일치 0개, LF 줄바꿈 수 불일치 0개, 해당 추적 경로 중 배포본 누락 0개.
- 비교 시각은 `2026-11-05T01:16:54.909Z`다. **실행 후 보관된 배포 파일을 검사한 것**이며, 시작 순간에 해시를 채집했거나 프로세스가 실제 로드한 모든 바이트를 증명한 것은 아니다. 파일별 원본/정규화 SHA-256과 CRLF/LF 개수를 manifest에 보존했다.
- 외부 런타임 `C:/Dev/ai-chatroom`은 SDK 고정 커밋의 범위 밖이다. 제공된 원문 `../B/external-runtime.manifest.json`을 `provenance/external-runtime.baseline.json`에 바이트 그대로 보존하고 원문 해시로 연결했다. 구현 결과는 읽지 않았다.
- 외부 기준 HEAD는 `bdd6ad62b535c201bcd0b87b52b164ae1eada459`이며 기준 manifest에는 `M lib/agents/cli/agy.mjs` 작업 트리 수정이 명시되어 있다. 현재 외부 파일 11개의 바이트 해시는 기준 manifest와 11개 모두 일치한다. 이것도 검증 시점의 파일 경계 기록이며 실행 시점 로드 증명은 아니다.
- 보완 도구는 `capture-runtime-provenance.mjs`다. 그래프 원문 선언과 `graph-A.json`은 수정하지 않았다. 그래프 SHA-256은 `6a89b38fe6365dc2663484085fcffcd1ebb99c03f9f7dd2cf3f1489e5320d793`로 유지된다. 격리 서비스 18331/PID 60784도 그대로 유지했다.
