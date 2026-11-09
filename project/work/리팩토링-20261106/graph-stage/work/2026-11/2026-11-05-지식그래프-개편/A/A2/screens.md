# A-2 실제 관측 출력설계서

입력은 A/A1-exploration.md와 그 문서에 명시된 새 raw 원문이다. 제품 소스·B 결과·기존 기획·A-3는 읽지 않았다. CLI/API이므로 그림 대신 원문 파일과 JSON pointer 또는 1부터 시작하는 줄 위치를 남긴다. 모든 경로는 A 기준 상대경로다.

제품 출력은 BRG-01~BRG-05의 5종이다. ENV-01~ENV-02는 제품 정상 화면과 분리한 환경·설치 관측 2종으로, 설계서 형식만 재사용하며 제품 화면·화면요소 노드에서는 제외하고 environmentObservations 배열로 보존한다. requestMappings는 실제 HTTP 응답 4개만 담는다. clientStatus 000은 HTTP 상태 코드가 아니다. 프로세스 시작 OBS-02·OBS-03은 HTTP 요청이 아니므로 requestMappings에서 제외한다. BRG-01 화면 URL은 stdout에 표시된 listening 주소다.

각 요소의 보이는 조건은 해당 관측 1회에 한정한다. 화면요소 표의 문자열은 원문 문구를 유지하며 줄바꿈만 표 안에서 표시한다. JSON 값은 구조·값을 보존한 compact 표기로 적었다. 부모 객체 요소와 하위 필드 요소는 같은 출력의 포함 관계다. 헤더의 날짜·연결 값도 그때의 관측값일 뿐 고정 표시 정책이 아니다. 정책은 전 화면에서 없음이며 JSON에 정책 노드·정책 연결도 없다.

실행 조건은 A-1의 별도 포트 18331·빈 AI 데이터·members 빈 설정·preflight 비활성·fake chat 미설정이다. 정상 plan 결과, 로그인된 계정, 실제 게임과 운영 설치판은 미관측이다. 사용자 후속 지시에 따라 실게임 화면·컴퓨터유즈 검증은 후속으로 유예되었으며 이 문서는 현재 HTTP·시작 출력만 다룬다. 제품 화면 전체 검증 완료를 뜻하지 않는다. 화면 흐름은 관측 조작의 서술이며 화면 간 navigate 연결을 만들지 않는다. 이 문서는 기능·코드 노드와 추측 연결을 생성하지 않는다.

## 출력 목록

| ID | 관측 | 분류 | 상태 |
|---|---|---|---|
| BRG-01 | OBS-03 | 제품 시작 콘솔 | graph 경고 뒤 listening |
| BRG-02 | OBS-04 | 제품 HTTP | 200, ok:false, 빈 격리 환경 |
| BRG-03 | OBS-05 | 제품 HTTP | 404, not found |
| BRG-04 | OBS-06 | 제품 HTTP | 400, unknown member |
| BRG-05 | OBS-07 | 제품 HTTP | 500, bridge / parse detail |
| ENV-01 | OBS-01 | 클라이언트·환경 | curl exit 7, HTTP 응답 없음 |
| ENV-02 | OBS-02 | 설치·런타임 | 모듈 누락, HTTP 서비스 미시작 |

## BRG-01 격리 브리지 시작: 그래프 경고와 listening

- ID: `BRG-01`
- 이름: 격리 브리지 시작: 그래프 경고와 listening
- 분류: `product-startup`
- 관측: `OBS-03`
- URL: http://127.0.0.1:18331
- 상태: 프로세스가 graph NOT FOUND 안내 후 listening 주소를 출력함. 전체 제품 준비 완료 의미는 아님.
- 원문 파일: `raw/bridge-isolated.stdout.txt`, `raw/bridge-isolated.stderr.txt`, `raw/bridge-isolated.meta.json`
- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.

### BRG-01 개요

A-1의 완전 패키지를 별도 포트·빈 데이터로 실행한 직후 받는 콘솔 출력이다.

### BRG-01 화면 흐름

Node 실행 → stdout 첫 줄의 game graph 안내 읽기 → 둘째 줄의 listening 주소 읽기. A-1은 이후 별도 health 요청을 보냈다. 자동 화면 이동은 관측되지 않았다.

### BRG-01 화면요소

공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: A-1에 기록된 격리 실행 1회. stderr는 빈 파일이다.

| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |
|---|---|---|---|
| 1 / `BRG-01#1` (게임 그래프 안내) | <code>ai-bridge: game graph NOT FOUND (looked in C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-05-지식그래프-개편/A/isolated/no-kg ; C:\Dev\.head\sessions\코어키퍼-AI-캐릭터\work\2026-11\2026-11-05-지식그래프-개편\A\isolated\runtime\Tools\knowledge\data ; C:\Dev\.head\sessions\코어키퍼-AI-캐릭터\work\2026-11\2026-11-05-지식그래프-개편\A\isolated\CoreKeeperModSDK-kg\Tools\knowledge\data): no game queries, object names only. Set kgData in config.json</code> | `raw/bridge-isolated.stdout.txt` 줄 1–1 | 읽기 / 위 공통 조건 |
| 2 / `BRG-01#2` (대기 주소 안내) | <code>ai-bridge listening on http://127.0.0.1:18331</code> | `raw/bridge-isolated.stdout.txt` 줄 2–2 | 읽기 / 위 공통 조건 |

### BRG-01 화면 정책

정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.

## BRG-02 health: 격리 환경의 준비되지 않은 값

- ID: `BRG-02`
- 이름: health: 격리 환경의 준비되지 않은 값
- 분류: `product-http`
- 관측: `OBS-04`
- URL: http://127.0.0.1:18331/v1/health
- 상태: 격리 환경에서 HTTP 200, ok:false, busy:[]를 반환함.
- 원문 파일: `raw/isolated-health.body.txt`, `raw/isolated-health.headers.txt`, `raw/isolated-health.meta.json`
- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.

### BRG-02 개요

브리지 응답을 확인하려고 GET /v1/health를 요청한 결과다. A-1에 따르면 members 빈 설정으로 로그인·할당량 preflight가 비활성인 빈 환경이다. 이 값으로 운영 계정·사용자 설치의 장애나 인증 실패를 주장할 수 없다.

### BRG-02 화면 흐름

GET 요청 → HTTP 200 헤더 확인 → ok·members·busy 읽기. HTTP 200은 응답 전송 성공이며 AI 사용 준비 완료를 뜻하지 않는다.

### BRG-02 화면요소

공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: OBS-04에 기록된 GET http://127.0.0.1:18331/v1/health 1회 응답. 비스트리밍 JSON이며 스트림 이벤트 관측 없음.

| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |
|---|---|---|---|
| 1 / `BRG-02#1` (HTTP 응답 헤더) | <code>HTTP/1.1 200 OK<br>Content-Type: application/json; charset=utf-8<br>Date: Thu, 05 Nov 2026 01:03:42 GMT<br>Connection: keep-alive<br>Keep-Alive: timeout=5<br>Transfer-Encoding: chunked</code> | `raw/isolated-health.headers.txt` 줄 1–6 | 읽기 / 위 공통 조건 |
| 2 / `BRG-02#2` (ok) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/ok` | 읽기 / 위 공통 조건 |
| 3 / `BRG-02#3` (members) | <code>{"claude":{"cli":"claude","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":false,"skipWhy":null,"skipAbovePct":85,"ok":false},"gpt":{"cli":"codex","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":true,"skipWhy":"usage unknown ()","skipAbovePct":85,"ok":false},"gemini":{"cli":"agy","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":true,"skipWhy":"usage unknown ()","skipAbovePct":85,"ok":false},"grok":{"cli":"grok","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":true,"skipWhy":"usage unknown ()","skipAbovePct":85,"ok":false}}</code> | `raw/isolated-health.body.txt` 줄 1, `/members` | 읽기 / 위 공통 조건 |
| 3-1 / `BRG-02#3-1` (claude) | <code>{"cli":"claude","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":false,"skipWhy":null,"skipAbovePct":85,"ok":false}</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude` | 읽기 / 위 공통 조건 |
| 3-1-1 / `BRG-02#3-1-1` (claude.cli) | <code>"claude"</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/cli` | 읽기 / 위 공통 조건 |
| 3-1-2 / `BRG-02#3-1-2` (claude.found) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/found` | 읽기 / 위 공통 조건 |
| 3-1-3 / `BRG-02#3-1-3` (claude.login) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/login` | 읽기 / 위 공통 조건 |
| 3-1-4 / `BRG-02#3-1-4` (claude.subscription) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/subscription` | 읽기 / 위 공통 조건 |
| 3-1-5 / `BRG-02#3-1-5` (claude.authMethod) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/authMethod` | 읽기 / 위 공통 조건 |
| 3-1-6 / `BRG-02#3-1-6` (claude.authNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/authNote` | 읽기 / 위 공통 조건 |
| 3-1-7 / `BRG-02#3-1-7` (claude.weekPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/weekPct` | 읽기 / 위 공통 조건 |
| 3-1-8 / `BRG-02#3-1-8` (claude.maxPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/maxPct` | 읽기 / 위 공통 조건 |
| 3-1-9 / `BRG-02#3-1-9` (claude.windows) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/windows` | 읽기 / 위 공통 조건 |
| 3-1-10 / `BRG-02#3-1-10` (claude.weekResets) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/weekResets` | 읽기 / 위 공통 조건 |
| 3-1-11 / `BRG-02#3-1-11` (claude.quotaReadAt) | <code>0</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/quotaReadAt` | 읽기 / 위 공통 조건 |
| 3-1-12 / `BRG-02#3-1-12` (claude.quotaNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/quotaNote` | 읽기 / 위 공통 조건 |
| 3-1-13 / `BRG-02#3-1-13` (claude.subscriptionOk) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/subscriptionOk` | 읽기 / 위 공통 조건 |
| 3-1-14 / `BRG-02#3-1-14` (claude.skip) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/skip` | 읽기 / 위 공통 조건 |
| 3-1-15 / `BRG-02#3-1-15` (claude.skipWhy) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/skipWhy` | 읽기 / 위 공통 조건 |
| 3-1-16 / `BRG-02#3-1-16` (claude.skipAbovePct) | <code>85</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/skipAbovePct` | 읽기 / 위 공통 조건 |
| 3-1-17 / `BRG-02#3-1-17` (claude.ok) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/claude/ok` | 읽기 / 위 공통 조건 |
| 3-2 / `BRG-02#3-2` (gpt) | <code>{"cli":"codex","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":true,"skipWhy":"usage unknown ()","skipAbovePct":85,"ok":false}</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt` | 읽기 / 위 공통 조건 |
| 3-2-1 / `BRG-02#3-2-1` (gpt.cli) | <code>"codex"</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/cli` | 읽기 / 위 공통 조건 |
| 3-2-2 / `BRG-02#3-2-2` (gpt.found) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/found` | 읽기 / 위 공통 조건 |
| 3-2-3 / `BRG-02#3-2-3` (gpt.login) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/login` | 읽기 / 위 공통 조건 |
| 3-2-4 / `BRG-02#3-2-4` (gpt.subscription) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/subscription` | 읽기 / 위 공통 조건 |
| 3-2-5 / `BRG-02#3-2-5` (gpt.authMethod) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/authMethod` | 읽기 / 위 공통 조건 |
| 3-2-6 / `BRG-02#3-2-6` (gpt.authNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/authNote` | 읽기 / 위 공통 조건 |
| 3-2-7 / `BRG-02#3-2-7` (gpt.weekPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/weekPct` | 읽기 / 위 공통 조건 |
| 3-2-8 / `BRG-02#3-2-8` (gpt.maxPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/maxPct` | 읽기 / 위 공통 조건 |
| 3-2-9 / `BRG-02#3-2-9` (gpt.windows) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/windows` | 읽기 / 위 공통 조건 |
| 3-2-10 / `BRG-02#3-2-10` (gpt.weekResets) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/weekResets` | 읽기 / 위 공통 조건 |
| 3-2-11 / `BRG-02#3-2-11` (gpt.quotaReadAt) | <code>0</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/quotaReadAt` | 읽기 / 위 공통 조건 |
| 3-2-12 / `BRG-02#3-2-12` (gpt.quotaNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/quotaNote` | 읽기 / 위 공통 조건 |
| 3-2-13 / `BRG-02#3-2-13` (gpt.subscriptionOk) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/subscriptionOk` | 읽기 / 위 공통 조건 |
| 3-2-14 / `BRG-02#3-2-14` (gpt.skip) | <code>true</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/skip` | 읽기 / 위 공통 조건 |
| 3-2-15 / `BRG-02#3-2-15` (gpt.skipWhy) | <code>"usage unknown ()"</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/skipWhy` | 읽기 / 위 공통 조건 |
| 3-2-16 / `BRG-02#3-2-16` (gpt.skipAbovePct) | <code>85</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/skipAbovePct` | 읽기 / 위 공통 조건 |
| 3-2-17 / `BRG-02#3-2-17` (gpt.ok) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gpt/ok` | 읽기 / 위 공통 조건 |
| 3-3 / `BRG-02#3-3` (gemini) | <code>{"cli":"agy","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":true,"skipWhy":"usage unknown ()","skipAbovePct":85,"ok":false}</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini` | 읽기 / 위 공통 조건 |
| 3-3-1 / `BRG-02#3-3-1` (gemini.cli) | <code>"agy"</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/cli` | 읽기 / 위 공통 조건 |
| 3-3-2 / `BRG-02#3-3-2` (gemini.found) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/found` | 읽기 / 위 공통 조건 |
| 3-3-3 / `BRG-02#3-3-3` (gemini.login) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/login` | 읽기 / 위 공통 조건 |
| 3-3-4 / `BRG-02#3-3-4` (gemini.subscription) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/subscription` | 읽기 / 위 공통 조건 |
| 3-3-5 / `BRG-02#3-3-5` (gemini.authMethod) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/authMethod` | 읽기 / 위 공통 조건 |
| 3-3-6 / `BRG-02#3-3-6` (gemini.authNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/authNote` | 읽기 / 위 공통 조건 |
| 3-3-7 / `BRG-02#3-3-7` (gemini.weekPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/weekPct` | 읽기 / 위 공통 조건 |
| 3-3-8 / `BRG-02#3-3-8` (gemini.maxPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/maxPct` | 읽기 / 위 공통 조건 |
| 3-3-9 / `BRG-02#3-3-9` (gemini.windows) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/windows` | 읽기 / 위 공통 조건 |
| 3-3-10 / `BRG-02#3-3-10` (gemini.weekResets) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/weekResets` | 읽기 / 위 공통 조건 |
| 3-3-11 / `BRG-02#3-3-11` (gemini.quotaReadAt) | <code>0</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/quotaReadAt` | 읽기 / 위 공통 조건 |
| 3-3-12 / `BRG-02#3-3-12` (gemini.quotaNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/quotaNote` | 읽기 / 위 공통 조건 |
| 3-3-13 / `BRG-02#3-3-13` (gemini.subscriptionOk) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/subscriptionOk` | 읽기 / 위 공통 조건 |
| 3-3-14 / `BRG-02#3-3-14` (gemini.skip) | <code>true</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/skip` | 읽기 / 위 공통 조건 |
| 3-3-15 / `BRG-02#3-3-15` (gemini.skipWhy) | <code>"usage unknown ()"</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/skipWhy` | 읽기 / 위 공통 조건 |
| 3-3-16 / `BRG-02#3-3-16` (gemini.skipAbovePct) | <code>85</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/skipAbovePct` | 읽기 / 위 공통 조건 |
| 3-3-17 / `BRG-02#3-3-17` (gemini.ok) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/gemini/ok` | 읽기 / 위 공통 조건 |
| 3-4 / `BRG-02#3-4` (grok) | <code>{"cli":"grok","found":false,"login":false,"subscription":null,"authMethod":null,"authNote":"","weekPct":null,"maxPct":null,"windows":null,"weekResets":null,"quotaReadAt":0,"quotaNote":"","subscriptionOk":false,"skip":true,"skipWhy":"usage unknown ()","skipAbovePct":85,"ok":false}</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok` | 읽기 / 위 공통 조건 |
| 3-4-1 / `BRG-02#3-4-1` (grok.cli) | <code>"grok"</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/cli` | 읽기 / 위 공통 조건 |
| 3-4-2 / `BRG-02#3-4-2` (grok.found) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/found` | 읽기 / 위 공통 조건 |
| 3-4-3 / `BRG-02#3-4-3` (grok.login) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/login` | 읽기 / 위 공통 조건 |
| 3-4-4 / `BRG-02#3-4-4` (grok.subscription) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/subscription` | 읽기 / 위 공통 조건 |
| 3-4-5 / `BRG-02#3-4-5` (grok.authMethod) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/authMethod` | 읽기 / 위 공통 조건 |
| 3-4-6 / `BRG-02#3-4-6` (grok.authNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/authNote` | 읽기 / 위 공통 조건 |
| 3-4-7 / `BRG-02#3-4-7` (grok.weekPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/weekPct` | 읽기 / 위 공통 조건 |
| 3-4-8 / `BRG-02#3-4-8` (grok.maxPct) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/maxPct` | 읽기 / 위 공통 조건 |
| 3-4-9 / `BRG-02#3-4-9` (grok.windows) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/windows` | 읽기 / 위 공통 조건 |
| 3-4-10 / `BRG-02#3-4-10` (grok.weekResets) | <code>null</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/weekResets` | 읽기 / 위 공통 조건 |
| 3-4-11 / `BRG-02#3-4-11` (grok.quotaReadAt) | <code>0</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/quotaReadAt` | 읽기 / 위 공통 조건 |
| 3-4-12 / `BRG-02#3-4-12` (grok.quotaNote) | <code>""</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/quotaNote` | 읽기 / 위 공통 조건 |
| 3-4-13 / `BRG-02#3-4-13` (grok.subscriptionOk) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/subscriptionOk` | 읽기 / 위 공통 조건 |
| 3-4-14 / `BRG-02#3-4-14` (grok.skip) | <code>true</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/skip` | 읽기 / 위 공통 조건 |
| 3-4-15 / `BRG-02#3-4-15` (grok.skipWhy) | <code>"usage unknown ()"</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/skipWhy` | 읽기 / 위 공통 조건 |
| 3-4-16 / `BRG-02#3-4-16` (grok.skipAbovePct) | <code>85</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/skipAbovePct` | 읽기 / 위 공통 조건 |
| 3-4-17 / `BRG-02#3-4-17` (grok.ok) | <code>false</code> | `raw/isolated-health.body.txt` 줄 1, `/members/grok/ok` | 읽기 / 위 공통 조건 |
| 4 / `BRG-02#4` (busy) | <code>[]</code> | `raw/isolated-health.body.txt` 줄 1, `/busy` | 읽기 / 위 공통 조건 |

### BRG-02 화면 정책

정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.

## BRG-03 없는 경로: not found

- ID: `BRG-03`
- 이름: 없는 경로: not found
- 분류: `product-http`
- 관측: `OBS-05`
- URL: http://127.0.0.1:18331/
- 상태: GET / 요청에 HTTP 404와 not found를 반환함.
- 원문 파일: `raw/isolated-not-found.body.txt`, `raw/isolated-not-found.headers.txt`, `raw/isolated-not-found.meta.json`
- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.

### BRG-03 개요

A-1이 루트 경로를 요청하여 받은 오류 출력이다. 다른 미관측 경로의 결과는 일반화하지 않는다.

### BRG-03 화면 흐름

GET / 요청 → HTTP 404 확인 → ok:false와 error 문구 읽기. 응답 안의 후속 조작은 관측되지 않았다.

### BRG-03 화면요소

공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: OBS-05에 기록된 GET http://127.0.0.1:18331/ 1회 응답. 비스트리밍 JSON이며 스트림 이벤트 관측 없음.

| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |
|---|---|---|---|
| 1 / `BRG-03#1` (HTTP 응답 헤더) | <code>HTTP/1.1 404 Not Found<br>Content-Type: application/json; charset=utf-8<br>Date: Thu, 05 Nov 2026 01:04:10 GMT<br>Connection: keep-alive<br>Keep-Alive: timeout=5<br>Transfer-Encoding: chunked</code> | `raw/isolated-not-found.headers.txt` 줄 1–6 | 읽기 / 위 공통 조건 |
| 2 / `BRG-03#2` (ok) | <code>false</code> | `raw/isolated-not-found.body.txt` 줄 1, `/ok` | 읽기 / 위 공통 조건 |
| 3 / `BRG-03#3` (error) | <code>"not found"</code> | `raw/isolated-not-found.body.txt` 줄 1, `/error` | 읽기 / 위 공통 조건 |

### BRG-03 화면 정책

정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.

## BRG-04 알 수 없는 멤버: unknown member

- ID: `BRG-04`
- 이름: 알 수 없는 멤버: unknown member
- 분류: `product-http`
- 관측: `OBS-06`
- URL: http://127.0.0.1:18331/v1/plan
- 상태: 기록된 unknown-member 입력에 HTTP 400을 반환함.
- 원문 파일: `raw/isolated-unknown-member.body.txt`, `raw/isolated-unknown-member.headers.txt`, `raw/isolated-unknown-member.meta.json`
- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.

### BRG-04 개요

A-1의 requests/unknown-member.json을 POST /v1/plan으로 보낸 결과다. 입력 원문 내용은 이 설계서에서 추측하지 않고 요청 메타의 파일 참조를 보존한다.

### BRG-04 화면 흐름

기록된 POST 요청 → HTTP 400 확인 → error와 detail의 멤버 이름 읽기. 안내 이름을 정상 호출해 확인한 기록은 없다.

### BRG-04 화면요소

공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: OBS-06에 기록된 POST http://127.0.0.1:18331/v1/plan 1회 응답. 비스트리밍 JSON이며 스트림 이벤트 관측 없음.

| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |
|---|---|---|---|
| 1 / `BRG-04#1` (HTTP 응답 헤더) | <code>HTTP/1.1 400 Bad Request<br>Content-Type: application/json; charset=utf-8<br>Date: Thu, 05 Nov 2026 01:04:11 GMT<br>Connection: keep-alive<br>Keep-Alive: timeout=5<br>Transfer-Encoding: chunked</code> | `raw/isolated-unknown-member.headers.txt` 줄 1–6 | 읽기 / 위 공통 조건 |
| 2 / `BRG-04#2` (ok) | <code>false</code> | `raw/isolated-unknown-member.body.txt` 줄 1, `/ok` | 읽기 / 위 공통 조건 |
| 3 / `BRG-04#3` (error) | <code>"unknown member"</code> | `raw/isolated-unknown-member.body.txt` 줄 1, `/error` | 읽기 / 위 공통 조건 |
| 4 / `BRG-04#4` (detail) | <code>"members: claude, gpt, gemini, grok"</code> | `raw/isolated-unknown-member.body.txt` 줄 1, `/detail` | 읽기 / 위 공통 조건 |

### BRG-04 화면 정책

정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.

## BRG-05 JSON 구문 오류: bridge

- ID: `BRG-05`
- 이름: JSON 구문 오류: bridge
- 분류: `product-http`
- 관측: `OBS-07`
- URL: http://127.0.0.1:18331/v1/plan
- 상태: 단일 { 입력에 HTTP 500과 JSON parse detail을 반환함.
- 원문 파일: `raw/isolated-malformed.body.txt`, `raw/isolated-malformed.headers.txt`, `raw/isolated-malformed.meta.json`
- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.

### BRG-05 개요

A-1이 requests/malformed.json의 단일 {를 POST /v1/plan으로 보낸 결과다.

### BRG-05 화면 흐름

기록된 POST 요청 → HTTP 500 확인 → error와 detail의 위치 설명 읽기. 일반 JSON 오류 전부의 상태 코드를 확정하지 않는다.

### BRG-05 화면요소

공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: OBS-07에 기록된 POST http://127.0.0.1:18331/v1/plan 1회 응답. 비스트리밍 JSON이며 스트림 이벤트 관측 없음.

| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |
|---|---|---|---|
| 1 / `BRG-05#1` (HTTP 응답 헤더) | <code>HTTP/1.1 500 Internal Server Error<br>Content-Type: application/json; charset=utf-8<br>Date: Thu, 05 Nov 2026 01:04:12 GMT<br>Connection: keep-alive<br>Keep-Alive: timeout=5<br>Transfer-Encoding: chunked</code> | `raw/isolated-malformed.headers.txt` 줄 1–6 | 읽기 / 위 공통 조건 |
| 2 / `BRG-05#2` (ok) | <code>false</code> | `raw/isolated-malformed.body.txt` 줄 1, `/ok` | 읽기 / 위 공통 조건 |
| 3 / `BRG-05#3` (error) | <code>"bridge"</code> | `raw/isolated-malformed.body.txt` 줄 1, `/error` | 읽기 / 위 공통 조건 |
| 4 / `BRG-05#4` (detail) | <code>"Expected property name or '}' in JSON at position 1 (line 1 column 2)"</code> | `raw/isolated-malformed.body.txt` 줄 1, `/detail` | 읽기 / 위 공통 조건 |

### BRG-05 화면 정책

정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.

## ENV-01 클라이언트 연결 실패: curl exit 7

- ID: `ENV-01`
- 이름: 클라이언트 연결 실패: curl exit 7
- 분류: `environment-client`
- 관측: `OBS-01`
- URL: http://127.0.0.1:8331/v1/health
- 상태: HTTP 응답 없음. clientStatus 000, httpStatus null, curl exitCode 7.
- 원문 파일: `raw/health-02.stderr.txt`, `raw/health-02.meta.json`
- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.

### ENV-01 개요

기본 주소로 접속을 시도했으나 curl이 연결 실패를 출력한 환경 관측이다. 제품 HTTP 오류 화면이 아니다.

### ENV-01 화면 흐름

GET 시도 → curl stderr 읽기 → 메타에서 HTTP 응답 없음 확인. A-1의 환경 조사와 패키지 실행은 별도 조작이다.

### ENV-01 화면요소

공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: OBS-01 재수집 요청 health-02에서 관측. health-01과 합쳐 하나의 HTTP 응답으로 취급하지 않는다.

| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |
|---|---|---|---|
| 1 / `ENV-01#1` (curl 연결 실패 문구) | <code>curl: (7) Failed to connect to 127.0.0.1:8331 after 2046 ms: Could not connect to server</code> | `raw/health-02.stderr.txt` 줄 1–1 | 읽기 / 위 공통 조건 |

### ENV-01 화면 정책

정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.

## ENV-02 설치 패키지 시작 실패: ERR_MODULE_NOT_FOUND

- ID: `ENV-02`
- 이름: 설치 패키지 시작 실패: ERR_MODULE_NOT_FOUND
- 분류: `environment-installation`
- 관측: `OBS-02`
- URL: 해당 없음 — 프로세스 실행 출력
- 상태: Node 시작 실패. result 파일에 httpServiceStarted:false, processRunning:false.
- 원문 파일: `raw/bridge-start.stderr.txt`, `raw/bridge-start.meta.json`, `raw/bridge-start.result.json`
- 그림/공통 영역: 없음. CLI/API 출력 원문과 아래 줄 위치·JSON pointer가 위치 매칭본이다.

### ENV-02 개요

최초 전달 패키지를 실행했을 때 누락 모듈 때문에 받은 Node 런타임 출력이다. 제품 HTTP 응답이나 실제 게임 행동 출력이 아니다. 오류 문자열 속 B 경로는 출력 문구로만 보존했으며 해당 파일을 열람하지 않았다.

### ENV-02 화면 흐름

최초 Node 실행 → 누락 모듈 오류와 스택 읽기 → Node 버전 읽기. A-1은 이후 완전 패키지를 별도로 실행했다.

### ENV-02 화면요소

공통 조작: 출력 읽기. 출력 자체의 버튼·입력·링크 조작은 관측되지 않았다. 공통 표시 조건: OBS-02의 최초 패키지 실행 1회. 일반 제품 시작 화면과 분리한다.

| 번호 / 요소 ID | 보이는 값·문구 | 원문 위치 | 조작 / 조건 |
|---|---|---|---|
| 1 / `ENV-02#1` (런타임 오류 위치) | <code>node:internal/modules/esm/resolve:272<br>    throw new ERR_MODULE_NOT_FOUND(<br>          ^</code> | `raw/bridge-start.stderr.txt` 줄 1–3 | 읽기 / 위 공통 조건 |
| 2 / `ENV-02#2` (누락 모듈 오류) | <code>Error [ERR_MODULE_NOT_FOUND]: Cannot find module 'C:\Dev\.head\sessions\코어키퍼-AI-캐릭터\work\2026-11\2026-11-05-지식그래프-개편\B\snapshot\Tools\knowledge\kg.mjs' imported from C:\Dev\.head\sessions\코어키퍼-AI-캐릭터\work\2026-11\2026-11-05-지식그래프-개편\B\snapshot\Tools\ai-bridge\memgraph.mjs</code> | `raw/bridge-start.stderr.txt` 줄 5–5 | 읽기 / 위 공통 조건 |
| 3 / `ENV-02#3` (런타임 스택) | <code>    at finalizeResolution (node:internal/modules/esm/resolve:272:11)<br>    at moduleResolve (node:internal/modules/esm/resolve:879:10)<br>    at defaultResolve (node:internal/modules/esm/resolve:1006:11)<br>    at #cachedDefaultResolve (node:internal/modules/esm/loader:705:20)<br>    at #resolveAndMaybeBlockOnLoaderThread (node:internal/modules/esm/loader:725:38)<br>    at ModuleLoader.resolveSync (node:internal/modules/esm/loader:763:56)<br>    at #resolve (node:internal/modules/esm/loader:687:17)<br>    at ModuleLoader.getOrCreateModuleJob (node:internal/modules/esm/loader:607:35)<br>    at ModuleJob.syncLink (node:internal/modules/esm/module_job:276:33)<br>    at ModuleJob.link (node:internal/modules/esm/module_job:381:17) {</code> | `raw/bridge-start.stderr.txt` 줄 6–15 | 읽기 / 위 공통 조건 |
| 4 / `ENV-02#4` (오류 code·url) | <code>  code: 'ERR_MODULE_NOT_FOUND',<br>  url: 'file:///C:/Dev/.head/sessions/%EC%BD%94%EC%96%B4%ED%82%A4%ED%8D%BC-AI-%EC%BA%90%EB%A6%AD%ED%84%B0/work/2026-11/2026-11-05-%EC%A7%80%EC%8B%9D%EA%B7%B8%EB%9E%98%ED%94%84-%EA%B0%9C%ED%8E%B8/B/snapshot/Tools/knowledge/kg.mjs'<br>}</code> | `raw/bridge-start.stderr.txt` 줄 16–18 | 읽기 / 위 공통 조건 |
| 5 / `ENV-02#5` (Node 버전) | <code>Node.js v24.21.0</code> | `raw/bridge-start.stderr.txt` 줄 20–20 | 읽기 / 위 공통 조건 |

### ENV-02 화면 정책

정책 없음. 이 원문은 해당 실행에서 보인 값·문구의 증거이며, 다른 입력·환경에서도 적용되는 표시 규칙을 입증하지 않는다. 정책 노드와 화면→정책 연결은 생성하지 않는다.

## 파싱용 그래프

```json
{
  "nodes": [
    {
      "id": "BRG-01",
      "type": "화면",
      "summary": "격리 브리지 시작: 그래프 경고와 listening",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-01 격리 브리지 시작: 그래프 경고와 listening"
      },
      "observationId": "OBS-03",
      "classification": "product-startup",
      "url": "http://127.0.0.1:18331",
      "state": "프로세스가 graph NOT FOUND 안내 후 listening 주소를 출력함. 전체 제품 준비 완료 의미는 아님.",
      "rawFiles": [
        "raw/bridge-isolated.stdout.txt",
        "raw/bridge-isolated.stderr.txt",
        "raw/bridge-isolated.meta.json"
      ],
      "policyIds": [],
      "elements": [
        "BRG-01#1",
        "BRG-01#2"
      ]
    },
    {
      "id": "BRG-01#1",
      "type": "화면요소",
      "summary": "게임 그래프 안내",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-01 화면요소"
      },
      "number": "1",
      "visibleValue": "ai-bridge: game graph NOT FOUND (looked in C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-05-지식그래프-개편/A/isolated/no-kg ; C:\\Dev\\.head\\sessions\\코어키퍼-AI-캐릭터\\work\\2026-11\\2026-11-05-지식그래프-개편\\A\\isolated\\runtime\\Tools\\knowledge\\data ; C:\\Dev\\.head\\sessions\\코어키퍼-AI-캐릭터\\work\\2026-11\\2026-11-05-지식그래프-개편\\A\\isolated\\CoreKeeperModSDK-kg\\Tools\\knowledge\\data): no game queries, object names only. Set kgData in config.json",
      "location": {
        "rawFile": "raw/bridge-isolated.stdout.txt",
        "lines": [
          1,
          1
        ]
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-03의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-01#2",
      "type": "화면요소",
      "summary": "대기 주소 안내",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-01 화면요소"
      },
      "number": "2",
      "visibleValue": "ai-bridge listening on http://127.0.0.1:18331",
      "location": {
        "rawFile": "raw/bridge-isolated.stdout.txt",
        "lines": [
          2,
          2
        ]
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-03의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02",
      "type": "화면",
      "summary": "health: 격리 환경의 준비되지 않은 값",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 health: 격리 환경의 준비되지 않은 값"
      },
      "observationId": "OBS-04",
      "classification": "product-http",
      "url": "http://127.0.0.1:18331/v1/health",
      "state": "격리 환경에서 HTTP 200, ok:false, busy:[]를 반환함.",
      "rawFiles": [
        "raw/isolated-health.body.txt",
        "raw/isolated-health.headers.txt",
        "raw/isolated-health.meta.json"
      ],
      "policyIds": [],
      "elements": [
        "BRG-02#1",
        "BRG-02#2",
        "BRG-02#3",
        "BRG-02#3-1",
        "BRG-02#3-1-1",
        "BRG-02#3-1-2",
        "BRG-02#3-1-3",
        "BRG-02#3-1-4",
        "BRG-02#3-1-5",
        "BRG-02#3-1-6",
        "BRG-02#3-1-7",
        "BRG-02#3-1-8",
        "BRG-02#3-1-9",
        "BRG-02#3-1-10",
        "BRG-02#3-1-11",
        "BRG-02#3-1-12",
        "BRG-02#3-1-13",
        "BRG-02#3-1-14",
        "BRG-02#3-1-15",
        "BRG-02#3-1-16",
        "BRG-02#3-1-17",
        "BRG-02#3-2",
        "BRG-02#3-2-1",
        "BRG-02#3-2-2",
        "BRG-02#3-2-3",
        "BRG-02#3-2-4",
        "BRG-02#3-2-5",
        "BRG-02#3-2-6",
        "BRG-02#3-2-7",
        "BRG-02#3-2-8",
        "BRG-02#3-2-9",
        "BRG-02#3-2-10",
        "BRG-02#3-2-11",
        "BRG-02#3-2-12",
        "BRG-02#3-2-13",
        "BRG-02#3-2-14",
        "BRG-02#3-2-15",
        "BRG-02#3-2-16",
        "BRG-02#3-2-17",
        "BRG-02#3-3",
        "BRG-02#3-3-1",
        "BRG-02#3-3-2",
        "BRG-02#3-3-3",
        "BRG-02#3-3-4",
        "BRG-02#3-3-5",
        "BRG-02#3-3-6",
        "BRG-02#3-3-7",
        "BRG-02#3-3-8",
        "BRG-02#3-3-9",
        "BRG-02#3-3-10",
        "BRG-02#3-3-11",
        "BRG-02#3-3-12",
        "BRG-02#3-3-13",
        "BRG-02#3-3-14",
        "BRG-02#3-3-15",
        "BRG-02#3-3-16",
        "BRG-02#3-3-17",
        "BRG-02#3-4",
        "BRG-02#3-4-1",
        "BRG-02#3-4-2",
        "BRG-02#3-4-3",
        "BRG-02#3-4-4",
        "BRG-02#3-4-5",
        "BRG-02#3-4-6",
        "BRG-02#3-4-7",
        "BRG-02#3-4-8",
        "BRG-02#3-4-9",
        "BRG-02#3-4-10",
        "BRG-02#3-4-11",
        "BRG-02#3-4-12",
        "BRG-02#3-4-13",
        "BRG-02#3-4-14",
        "BRG-02#3-4-15",
        "BRG-02#3-4-16",
        "BRG-02#3-4-17",
        "BRG-02#4"
      ]
    },
    {
      "id": "BRG-02#1",
      "type": "화면요소",
      "summary": "HTTP 응답 헤더",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "1",
      "visibleValue": "HTTP/1.1 200 OK\nContent-Type: application/json; charset=utf-8\nDate: Thu, 05 Nov 2026 01:03:42 GMT\nConnection: keep-alive\nKeep-Alive: timeout=5\nTransfer-Encoding: chunked",
      "location": {
        "rawFile": "raw/isolated-health.headers.txt",
        "lines": [
          1,
          6
        ]
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#2",
      "type": "화면요소",
      "summary": "ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3",
      "type": "화면요소",
      "summary": "members",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3",
      "visibleValue": {
        "claude": {
          "cli": "claude",
          "found": false,
          "login": false,
          "subscription": null,
          "authMethod": null,
          "authNote": "",
          "weekPct": null,
          "maxPct": null,
          "windows": null,
          "weekResets": null,
          "quotaReadAt": 0,
          "quotaNote": "",
          "subscriptionOk": false,
          "skip": false,
          "skipWhy": null,
          "skipAbovePct": 85,
          "ok": false
        },
        "gpt": {
          "cli": "codex",
          "found": false,
          "login": false,
          "subscription": null,
          "authMethod": null,
          "authNote": "",
          "weekPct": null,
          "maxPct": null,
          "windows": null,
          "weekResets": null,
          "quotaReadAt": 0,
          "quotaNote": "",
          "subscriptionOk": false,
          "skip": true,
          "skipWhy": "usage unknown ()",
          "skipAbovePct": 85,
          "ok": false
        },
        "gemini": {
          "cli": "agy",
          "found": false,
          "login": false,
          "subscription": null,
          "authMethod": null,
          "authNote": "",
          "weekPct": null,
          "maxPct": null,
          "windows": null,
          "weekResets": null,
          "quotaReadAt": 0,
          "quotaNote": "",
          "subscriptionOk": false,
          "skip": true,
          "skipWhy": "usage unknown ()",
          "skipAbovePct": 85,
          "ok": false
        },
        "grok": {
          "cli": "grok",
          "found": false,
          "login": false,
          "subscription": null,
          "authMethod": null,
          "authNote": "",
          "weekPct": null,
          "maxPct": null,
          "windows": null,
          "weekResets": null,
          "quotaReadAt": 0,
          "quotaNote": "",
          "subscriptionOk": false,
          "skip": true,
          "skipWhy": "usage unknown ()",
          "skipAbovePct": 85,
          "ok": false
        }
      },
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1",
      "type": "화면요소",
      "summary": "claude",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1",
      "visibleValue": {
        "cli": "claude",
        "found": false,
        "login": false,
        "subscription": null,
        "authMethod": null,
        "authNote": "",
        "weekPct": null,
        "maxPct": null,
        "windows": null,
        "weekResets": null,
        "quotaReadAt": 0,
        "quotaNote": "",
        "subscriptionOk": false,
        "skip": false,
        "skipWhy": null,
        "skipAbovePct": 85,
        "ok": false
      },
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-1",
      "type": "화면요소",
      "summary": "claude.cli",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-1",
      "visibleValue": "claude",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/cli",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-2",
      "type": "화면요소",
      "summary": "claude.found",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/found",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-3",
      "type": "화면요소",
      "summary": "claude.login",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-3",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/login",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-4",
      "type": "화면요소",
      "summary": "claude.subscription",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-4",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/subscription",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-5",
      "type": "화면요소",
      "summary": "claude.authMethod",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-5",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/authMethod",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-6",
      "type": "화면요소",
      "summary": "claude.authNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-6",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/authNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-7",
      "type": "화면요소",
      "summary": "claude.weekPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-7",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/weekPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-8",
      "type": "화면요소",
      "summary": "claude.maxPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-8",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/maxPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-9",
      "type": "화면요소",
      "summary": "claude.windows",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-9",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/windows",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-10",
      "type": "화면요소",
      "summary": "claude.weekResets",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-10",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/weekResets",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-11",
      "type": "화면요소",
      "summary": "claude.quotaReadAt",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-11",
      "visibleValue": 0,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/quotaReadAt",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-12",
      "type": "화면요소",
      "summary": "claude.quotaNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-12",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/quotaNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-13",
      "type": "화면요소",
      "summary": "claude.subscriptionOk",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-13",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/subscriptionOk",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-14",
      "type": "화면요소",
      "summary": "claude.skip",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-14",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/skip",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-15",
      "type": "화면요소",
      "summary": "claude.skipWhy",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-15",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/skipWhy",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-16",
      "type": "화면요소",
      "summary": "claude.skipAbovePct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-16",
      "visibleValue": 85,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/skipAbovePct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-1-17",
      "type": "화면요소",
      "summary": "claude.ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-1-17",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/claude/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2",
      "type": "화면요소",
      "summary": "gpt",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2",
      "visibleValue": {
        "cli": "codex",
        "found": false,
        "login": false,
        "subscription": null,
        "authMethod": null,
        "authNote": "",
        "weekPct": null,
        "maxPct": null,
        "windows": null,
        "weekResets": null,
        "quotaReadAt": 0,
        "quotaNote": "",
        "subscriptionOk": false,
        "skip": true,
        "skipWhy": "usage unknown ()",
        "skipAbovePct": 85,
        "ok": false
      },
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-1",
      "type": "화면요소",
      "summary": "gpt.cli",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-1",
      "visibleValue": "codex",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/cli",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-2",
      "type": "화면요소",
      "summary": "gpt.found",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/found",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-3",
      "type": "화면요소",
      "summary": "gpt.login",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-3",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/login",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-4",
      "type": "화면요소",
      "summary": "gpt.subscription",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-4",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/subscription",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-5",
      "type": "화면요소",
      "summary": "gpt.authMethod",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-5",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/authMethod",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-6",
      "type": "화면요소",
      "summary": "gpt.authNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-6",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/authNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-7",
      "type": "화면요소",
      "summary": "gpt.weekPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-7",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/weekPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-8",
      "type": "화면요소",
      "summary": "gpt.maxPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-8",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/maxPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-9",
      "type": "화면요소",
      "summary": "gpt.windows",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-9",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/windows",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-10",
      "type": "화면요소",
      "summary": "gpt.weekResets",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-10",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/weekResets",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-11",
      "type": "화면요소",
      "summary": "gpt.quotaReadAt",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-11",
      "visibleValue": 0,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/quotaReadAt",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-12",
      "type": "화면요소",
      "summary": "gpt.quotaNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-12",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/quotaNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-13",
      "type": "화면요소",
      "summary": "gpt.subscriptionOk",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-13",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/subscriptionOk",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-14",
      "type": "화면요소",
      "summary": "gpt.skip",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-14",
      "visibleValue": true,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/skip",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-15",
      "type": "화면요소",
      "summary": "gpt.skipWhy",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-15",
      "visibleValue": "usage unknown ()",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/skipWhy",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-16",
      "type": "화면요소",
      "summary": "gpt.skipAbovePct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-16",
      "visibleValue": 85,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/skipAbovePct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-2-17",
      "type": "화면요소",
      "summary": "gpt.ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-2-17",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gpt/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3",
      "type": "화면요소",
      "summary": "gemini",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3",
      "visibleValue": {
        "cli": "agy",
        "found": false,
        "login": false,
        "subscription": null,
        "authMethod": null,
        "authNote": "",
        "weekPct": null,
        "maxPct": null,
        "windows": null,
        "weekResets": null,
        "quotaReadAt": 0,
        "quotaNote": "",
        "subscriptionOk": false,
        "skip": true,
        "skipWhy": "usage unknown ()",
        "skipAbovePct": 85,
        "ok": false
      },
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-1",
      "type": "화면요소",
      "summary": "gemini.cli",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-1",
      "visibleValue": "agy",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/cli",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-2",
      "type": "화면요소",
      "summary": "gemini.found",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/found",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-3",
      "type": "화면요소",
      "summary": "gemini.login",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-3",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/login",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-4",
      "type": "화면요소",
      "summary": "gemini.subscription",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-4",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/subscription",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-5",
      "type": "화면요소",
      "summary": "gemini.authMethod",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-5",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/authMethod",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-6",
      "type": "화면요소",
      "summary": "gemini.authNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-6",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/authNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-7",
      "type": "화면요소",
      "summary": "gemini.weekPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-7",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/weekPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-8",
      "type": "화면요소",
      "summary": "gemini.maxPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-8",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/maxPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-9",
      "type": "화면요소",
      "summary": "gemini.windows",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-9",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/windows",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-10",
      "type": "화면요소",
      "summary": "gemini.weekResets",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-10",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/weekResets",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-11",
      "type": "화면요소",
      "summary": "gemini.quotaReadAt",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-11",
      "visibleValue": 0,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/quotaReadAt",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-12",
      "type": "화면요소",
      "summary": "gemini.quotaNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-12",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/quotaNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-13",
      "type": "화면요소",
      "summary": "gemini.subscriptionOk",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-13",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/subscriptionOk",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-14",
      "type": "화면요소",
      "summary": "gemini.skip",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-14",
      "visibleValue": true,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/skip",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-15",
      "type": "화면요소",
      "summary": "gemini.skipWhy",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-15",
      "visibleValue": "usage unknown ()",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/skipWhy",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-16",
      "type": "화면요소",
      "summary": "gemini.skipAbovePct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-16",
      "visibleValue": 85,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/skipAbovePct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-3-17",
      "type": "화면요소",
      "summary": "gemini.ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-3-17",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/gemini/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4",
      "type": "화면요소",
      "summary": "grok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4",
      "visibleValue": {
        "cli": "grok",
        "found": false,
        "login": false,
        "subscription": null,
        "authMethod": null,
        "authNote": "",
        "weekPct": null,
        "maxPct": null,
        "windows": null,
        "weekResets": null,
        "quotaReadAt": 0,
        "quotaNote": "",
        "subscriptionOk": false,
        "skip": true,
        "skipWhy": "usage unknown ()",
        "skipAbovePct": 85,
        "ok": false
      },
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-1",
      "type": "화면요소",
      "summary": "grok.cli",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-1",
      "visibleValue": "grok",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/cli",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-2",
      "type": "화면요소",
      "summary": "grok.found",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/found",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-3",
      "type": "화면요소",
      "summary": "grok.login",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-3",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/login",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-4",
      "type": "화면요소",
      "summary": "grok.subscription",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-4",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/subscription",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-5",
      "type": "화면요소",
      "summary": "grok.authMethod",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-5",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/authMethod",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-6",
      "type": "화면요소",
      "summary": "grok.authNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-6",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/authNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-7",
      "type": "화면요소",
      "summary": "grok.weekPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-7",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/weekPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-8",
      "type": "화면요소",
      "summary": "grok.maxPct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-8",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/maxPct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-9",
      "type": "화면요소",
      "summary": "grok.windows",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-9",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/windows",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-10",
      "type": "화면요소",
      "summary": "grok.weekResets",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-10",
      "visibleValue": null,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/weekResets",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-11",
      "type": "화면요소",
      "summary": "grok.quotaReadAt",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-11",
      "visibleValue": 0,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/quotaReadAt",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-12",
      "type": "화면요소",
      "summary": "grok.quotaNote",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-12",
      "visibleValue": "",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/quotaNote",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-13",
      "type": "화면요소",
      "summary": "grok.subscriptionOk",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-13",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/subscriptionOk",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-14",
      "type": "화면요소",
      "summary": "grok.skip",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-14",
      "visibleValue": true,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/skip",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-15",
      "type": "화면요소",
      "summary": "grok.skipWhy",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-15",
      "visibleValue": "usage unknown ()",
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/skipWhy",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-16",
      "type": "화면요소",
      "summary": "grok.skipAbovePct",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-16",
      "visibleValue": 85,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/skipAbovePct",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#3-4-17",
      "type": "화면요소",
      "summary": "grok.ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "3-4-17",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/members/grok/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-02#4",
      "type": "화면요소",
      "summary": "busy",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-02 화면요소"
      },
      "number": "4",
      "visibleValue": [],
      "location": {
        "rawFile": "raw/isolated-health.body.txt",
        "jsonPointer": "/busy",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-04의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-03",
      "type": "화면",
      "summary": "없는 경로: not found",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-03 없는 경로: not found"
      },
      "observationId": "OBS-05",
      "classification": "product-http",
      "url": "http://127.0.0.1:18331/",
      "state": "GET / 요청에 HTTP 404와 not found를 반환함.",
      "rawFiles": [
        "raw/isolated-not-found.body.txt",
        "raw/isolated-not-found.headers.txt",
        "raw/isolated-not-found.meta.json"
      ],
      "policyIds": [],
      "elements": [
        "BRG-03#1",
        "BRG-03#2",
        "BRG-03#3"
      ]
    },
    {
      "id": "BRG-03#1",
      "type": "화면요소",
      "summary": "HTTP 응답 헤더",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-03 화면요소"
      },
      "number": "1",
      "visibleValue": "HTTP/1.1 404 Not Found\nContent-Type: application/json; charset=utf-8\nDate: Thu, 05 Nov 2026 01:04:10 GMT\nConnection: keep-alive\nKeep-Alive: timeout=5\nTransfer-Encoding: chunked",
      "location": {
        "rawFile": "raw/isolated-not-found.headers.txt",
        "lines": [
          1,
          6
        ]
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-05의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-03#2",
      "type": "화면요소",
      "summary": "ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-03 화면요소"
      },
      "number": "2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-not-found.body.txt",
        "jsonPointer": "/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-05의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-03#3",
      "type": "화면요소",
      "summary": "error",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-03 화면요소"
      },
      "number": "3",
      "visibleValue": "not found",
      "location": {
        "rawFile": "raw/isolated-not-found.body.txt",
        "jsonPointer": "/error",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-05의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-04",
      "type": "화면",
      "summary": "알 수 없는 멤버: unknown member",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-04 알 수 없는 멤버: unknown member"
      },
      "observationId": "OBS-06",
      "classification": "product-http",
      "url": "http://127.0.0.1:18331/v1/plan",
      "state": "기록된 unknown-member 입력에 HTTP 400을 반환함.",
      "rawFiles": [
        "raw/isolated-unknown-member.body.txt",
        "raw/isolated-unknown-member.headers.txt",
        "raw/isolated-unknown-member.meta.json"
      ],
      "policyIds": [],
      "elements": [
        "BRG-04#1",
        "BRG-04#2",
        "BRG-04#3",
        "BRG-04#4"
      ]
    },
    {
      "id": "BRG-04#1",
      "type": "화면요소",
      "summary": "HTTP 응답 헤더",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-04 화면요소"
      },
      "number": "1",
      "visibleValue": "HTTP/1.1 400 Bad Request\nContent-Type: application/json; charset=utf-8\nDate: Thu, 05 Nov 2026 01:04:11 GMT\nConnection: keep-alive\nKeep-Alive: timeout=5\nTransfer-Encoding: chunked",
      "location": {
        "rawFile": "raw/isolated-unknown-member.headers.txt",
        "lines": [
          1,
          6
        ]
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-06의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-04#2",
      "type": "화면요소",
      "summary": "ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-04 화면요소"
      },
      "number": "2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-unknown-member.body.txt",
        "jsonPointer": "/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-06의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-04#3",
      "type": "화면요소",
      "summary": "error",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-04 화면요소"
      },
      "number": "3",
      "visibleValue": "unknown member",
      "location": {
        "rawFile": "raw/isolated-unknown-member.body.txt",
        "jsonPointer": "/error",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-06의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-04#4",
      "type": "화면요소",
      "summary": "detail",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-04 화면요소"
      },
      "number": "4",
      "visibleValue": "members: claude, gpt, gemini, grok",
      "location": {
        "rawFile": "raw/isolated-unknown-member.body.txt",
        "jsonPointer": "/detail",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-06의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-05",
      "type": "화면",
      "summary": "JSON 구문 오류: bridge",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-05 JSON 구문 오류: bridge"
      },
      "observationId": "OBS-07",
      "classification": "product-http",
      "url": "http://127.0.0.1:18331/v1/plan",
      "state": "단일 { 입력에 HTTP 500과 JSON parse detail을 반환함.",
      "rawFiles": [
        "raw/isolated-malformed.body.txt",
        "raw/isolated-malformed.headers.txt",
        "raw/isolated-malformed.meta.json"
      ],
      "policyIds": [],
      "elements": [
        "BRG-05#1",
        "BRG-05#2",
        "BRG-05#3",
        "BRG-05#4"
      ]
    },
    {
      "id": "BRG-05#1",
      "type": "화면요소",
      "summary": "HTTP 응답 헤더",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-05 화면요소"
      },
      "number": "1",
      "visibleValue": "HTTP/1.1 500 Internal Server Error\nContent-Type: application/json; charset=utf-8\nDate: Thu, 05 Nov 2026 01:04:12 GMT\nConnection: keep-alive\nKeep-Alive: timeout=5\nTransfer-Encoding: chunked",
      "location": {
        "rawFile": "raw/isolated-malformed.headers.txt",
        "lines": [
          1,
          6
        ]
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-07의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-05#2",
      "type": "화면요소",
      "summary": "ok",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-05 화면요소"
      },
      "number": "2",
      "visibleValue": false,
      "location": {
        "rawFile": "raw/isolated-malformed.body.txt",
        "jsonPointer": "/ok",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-07의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-05#3",
      "type": "화면요소",
      "summary": "error",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-05 화면요소"
      },
      "number": "3",
      "visibleValue": "bridge",
      "location": {
        "rawFile": "raw/isolated-malformed.body.txt",
        "jsonPointer": "/error",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-07의 기록된 실행·요청 조건에서 관측"
    },
    {
      "id": "BRG-05#4",
      "type": "화면요소",
      "summary": "detail",
      "source": {
        "document": "A2/screens.md",
        "section": "BRG-05 화면요소"
      },
      "number": "4",
      "visibleValue": "Expected property name or '}' in JSON at position 1 (line 1 column 2)",
      "location": {
        "rawFile": "raw/isolated-malformed.body.txt",
        "jsonPointer": "/detail",
        "line": 1
      },
      "interaction": "출력 읽기; 직접 조작 관측 없음",
      "observedCondition": "OBS-07의 기록된 실행·요청 조건에서 관측"
    }
  ],
  "edges": [
    {
      "source": "BRG-01",
      "target": "BRG-01#1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-01",
      "target": "BRG-01#2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3",
      "target": "BRG-02#3-1",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-1",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-2",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-3",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-4",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-4",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-5",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-5",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-6",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-6",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-7",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-7",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-8",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-8",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-9",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-9",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-10",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-10",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-11",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-11",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-12",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-12",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-13",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-13",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-14",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-14",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-15",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-15",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-16",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-16",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-1-17",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-1",
      "target": "BRG-02#3-1-17",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3",
      "target": "BRG-02#3-2",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-1",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-2",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-3",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-4",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-4",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-5",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-5",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-6",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-6",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-7",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-7",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-8",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-8",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-9",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-9",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-10",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-10",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-11",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-11",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-12",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-12",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-13",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-13",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-14",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-14",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-15",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-15",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-16",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-16",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-2-17",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-2",
      "target": "BRG-02#3-2-17",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3",
      "target": "BRG-02#3-3",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-1",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-2",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-3",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-4",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-4",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-5",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-5",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-6",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-6",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-7",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-7",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-8",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-8",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-9",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-9",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-10",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-10",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-11",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-11",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-12",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-12",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-13",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-13",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-14",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-14",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-15",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-15",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-16",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-16",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-3-17",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-3",
      "target": "BRG-02#3-3-17",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3",
      "target": "BRG-02#3-4",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-1",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-2",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-3",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-4",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-4",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-5",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-5",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-6",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-6",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-7",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-7",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-8",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-8",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-9",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-9",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-10",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-10",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-11",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-11",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-12",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-12",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-13",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-13",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-14",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-14",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-15",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-15",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-16",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-16",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#3-4-17",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-02#3-4",
      "target": "BRG-02#3-4-17",
      "type": "화면요소→화면요소"
    },
    {
      "source": "BRG-02",
      "target": "BRG-02#4",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-03",
      "target": "BRG-03#1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-03",
      "target": "BRG-03#2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-03",
      "target": "BRG-03#3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-04",
      "target": "BRG-04#1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-04",
      "target": "BRG-04#2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-04",
      "target": "BRG-04#3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-04",
      "target": "BRG-04#4",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-05",
      "target": "BRG-05#1",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-05",
      "target": "BRG-05#2",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-05",
      "target": "BRG-05#3",
      "type": "화면→화면요소"
    },
    {
      "source": "BRG-05",
      "target": "BRG-05#4",
      "type": "화면→화면요소"
    }
  ],
  "requestMappings": [
    {
      "observationId": "OBS-04",
      "outputId": "BRG-02",
      "method": "GET",
      "url": "http://127.0.0.1:18331/v1/health",
      "path": "/v1/health",
      "status": 200,
      "rawBody": "raw/isolated-health.body.txt",
      "rawMeta": "raw/isolated-health.meta.json",
      "classification": "product-http",
      "rawHeaders": "raw/isolated-health.headers.txt",
      "clientExitCode": 0,
      "clientStatus": "200"
    },
    {
      "observationId": "OBS-05",
      "outputId": "BRG-03",
      "method": "GET",
      "url": "http://127.0.0.1:18331/",
      "path": "/",
      "status": 404,
      "rawBody": "raw/isolated-not-found.body.txt",
      "rawMeta": "raw/isolated-not-found.meta.json",
      "classification": "product-http",
      "rawHeaders": "raw/isolated-not-found.headers.txt",
      "clientExitCode": 0,
      "clientStatus": "404"
    },
    {
      "observationId": "OBS-06",
      "outputId": "BRG-04",
      "method": "POST",
      "url": "http://127.0.0.1:18331/v1/plan",
      "path": "/v1/plan",
      "status": 400,
      "rawBody": "raw/isolated-unknown-member.body.txt",
      "rawMeta": "raw/isolated-unknown-member.meta.json",
      "classification": "product-http",
      "rawHeaders": "raw/isolated-unknown-member.headers.txt",
      "requestBody": "requests/unknown-member.json",
      "clientExitCode": 0,
      "clientStatus": "400"
    },
    {
      "observationId": "OBS-07",
      "outputId": "BRG-05",
      "method": "POST",
      "url": "http://127.0.0.1:18331/v1/plan",
      "path": "/v1/plan",
      "status": 500,
      "rawBody": "raw/isolated-malformed.body.txt",
      "rawMeta": "raw/isolated-malformed.meta.json",
      "classification": "product-http",
      "rawHeaders": "raw/isolated-malformed.headers.txt",
      "requestBody": "requests/malformed.json",
      "clientExitCode": 0,
      "clientStatus": "500"
    }
  ],
  "environmentObservations": [
    {
      "id": "ENV-01",
      "summary": "클라이언트 연결 실패: curl exit 7",
      "source": {
        "document": "A2/screens.md",
        "section": "ENV-01 클라이언트 연결 실패: curl exit 7"
      },
      "observationId": "OBS-01",
      "classification": "environment-client",
      "url": "http://127.0.0.1:8331/v1/health",
      "state": "HTTP 응답 없음. clientStatus 000, httpStatus null, curl exitCode 7.",
      "rawFiles": [
        "raw/health-02.stderr.txt",
        "raw/health-02.meta.json"
      ],
      "status": null,
      "elements": [
        {
          "id": "ENV-01#1",
          "summary": "curl 연결 실패 문구",
          "source": {
            "document": "A2/screens.md",
            "section": "ENV-01 화면요소"
          },
          "number": "1",
          "visibleValue": "curl: (7) Failed to connect to 127.0.0.1:8331 after 2046 ms: Could not connect to server",
          "location": {
            "rawFile": "raw/health-02.stderr.txt",
            "lines": [
              1,
              1
            ]
          },
          "interaction": "출력 읽기; 직접 조작 관측 없음",
          "observedCondition": "OBS-01의 기록된 실행·요청 조건에서 관측"
        }
      ],
      "method": "GET",
      "path": "/v1/health",
      "clientStatus": "000",
      "clientExitCode": 7,
      "rawBody": null,
      "rawMeta": "raw/health-02.meta.json",
      "rawStderr": "raw/health-02.stderr.txt"
    },
    {
      "id": "ENV-02",
      "summary": "설치 패키지 시작 실패: ERR_MODULE_NOT_FOUND",
      "source": {
        "document": "A2/screens.md",
        "section": "ENV-02 설치 패키지 시작 실패: ERR_MODULE_NOT_FOUND"
      },
      "observationId": "OBS-02",
      "classification": "environment-installation",
      "url": null,
      "state": "Node 시작 실패. result 파일에 httpServiceStarted:false, processRunning:false.",
      "rawFiles": [
        "raw/bridge-start.stderr.txt",
        "raw/bridge-start.meta.json",
        "raw/bridge-start.result.json"
      ],
      "status": null,
      "elements": [
        {
          "id": "ENV-02#1",
          "summary": "런타임 오류 위치",
          "source": {
            "document": "A2/screens.md",
            "section": "ENV-02 화면요소"
          },
          "number": "1",
          "visibleValue": "node:internal/modules/esm/resolve:272\n    throw new ERR_MODULE_NOT_FOUND(\n          ^",
          "location": {
            "rawFile": "raw/bridge-start.stderr.txt",
            "lines": [
              1,
              3
            ]
          },
          "interaction": "출력 읽기; 직접 조작 관측 없음",
          "observedCondition": "OBS-02의 기록된 실행·요청 조건에서 관측"
        },
        {
          "id": "ENV-02#2",
          "summary": "누락 모듈 오류",
          "source": {
            "document": "A2/screens.md",
            "section": "ENV-02 화면요소"
          },
          "number": "2",
          "visibleValue": "Error [ERR_MODULE_NOT_FOUND]: Cannot find module 'C:\\Dev\\.head\\sessions\\코어키퍼-AI-캐릭터\\work\\2026-11\\2026-11-05-지식그래프-개편\\B\\snapshot\\Tools\\knowledge\\kg.mjs' imported from C:\\Dev\\.head\\sessions\\코어키퍼-AI-캐릭터\\work\\2026-11\\2026-11-05-지식그래프-개편\\B\\snapshot\\Tools\\ai-bridge\\memgraph.mjs",
          "location": {
            "rawFile": "raw/bridge-start.stderr.txt",
            "lines": [
              5,
              5
            ]
          },
          "interaction": "출력 읽기; 직접 조작 관측 없음",
          "observedCondition": "OBS-02의 기록된 실행·요청 조건에서 관측"
        },
        {
          "id": "ENV-02#3",
          "summary": "런타임 스택",
          "source": {
            "document": "A2/screens.md",
            "section": "ENV-02 화면요소"
          },
          "number": "3",
          "visibleValue": "    at finalizeResolution (node:internal/modules/esm/resolve:272:11)\n    at moduleResolve (node:internal/modules/esm/resolve:879:10)\n    at defaultResolve (node:internal/modules/esm/resolve:1006:11)\n    at #cachedDefaultResolve (node:internal/modules/esm/loader:705:20)\n    at #resolveAndMaybeBlockOnLoaderThread (node:internal/modules/esm/loader:725:38)\n    at ModuleLoader.resolveSync (node:internal/modules/esm/loader:763:56)\n    at #resolve (node:internal/modules/esm/loader:687:17)\n    at ModuleLoader.getOrCreateModuleJob (node:internal/modules/esm/loader:607:35)\n    at ModuleJob.syncLink (node:internal/modules/esm/module_job:276:33)\n    at ModuleJob.link (node:internal/modules/esm/module_job:381:17) {",
          "location": {
            "rawFile": "raw/bridge-start.stderr.txt",
            "lines": [
              6,
              15
            ]
          },
          "interaction": "출력 읽기; 직접 조작 관측 없음",
          "observedCondition": "OBS-02의 기록된 실행·요청 조건에서 관측"
        },
        {
          "id": "ENV-02#4",
          "summary": "오류 code·url",
          "source": {
            "document": "A2/screens.md",
            "section": "ENV-02 화면요소"
          },
          "number": "4",
          "visibleValue": "  code: 'ERR_MODULE_NOT_FOUND',\n  url: 'file:///C:/Dev/.head/sessions/%EC%BD%94%EC%96%B4%ED%82%A4%ED%8D%BC-AI-%EC%BA%90%EB%A6%AD%ED%84%B0/work/2026-11/2026-11-05-%EC%A7%80%EC%8B%9D%EA%B7%B8%EB%9E%98%ED%94%84-%EA%B0%9C%ED%8E%B8/B/snapshot/Tools/knowledge/kg.mjs'\n}",
          "location": {
            "rawFile": "raw/bridge-start.stderr.txt",
            "lines": [
              16,
              18
            ]
          },
          "interaction": "출력 읽기; 직접 조작 관측 없음",
          "observedCondition": "OBS-02의 기록된 실행·요청 조건에서 관측"
        },
        {
          "id": "ENV-02#5",
          "summary": "Node 버전",
          "source": {
            "document": "A2/screens.md",
            "section": "ENV-02 화면요소"
          },
          "number": "5",
          "visibleValue": "Node.js v24.21.0",
          "location": {
            "rawFile": "raw/bridge-start.stderr.txt",
            "lines": [
              20,
              20
            ]
          },
          "interaction": "출력 읽기; 직접 조작 관측 없음",
          "observedCondition": "OBS-02의 기록된 실행·요청 조건에서 관측"
        }
      ],
      "method": null,
      "path": null,
      "rawBody": null,
      "rawMeta": "raw/bridge-start.meta.json",
      "rawStderr": "raw/bridge-start.stderr.txt",
      "rawResult": "raw/bridge-start.result.json"
    }
  ]
}
```
