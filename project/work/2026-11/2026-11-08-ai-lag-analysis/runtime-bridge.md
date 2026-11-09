# 브리지·기존 런타임 독립 조사

2026-11-08 08:03 KST 기준. 코드·로그·설치 메타데이터·해당 프로세스 기본 수치만 읽었다. 실행·종료·설정 변경·게임 명령·AI 요청 생성은 하지 않았다. GET /v1/health는 bridge.mjs와 preflight.mjs를 읽어 캐시 상태 반환만 함을 확인한 뒤 1회 읽었다. 작업기록 도구에는 쓰지 않았다.

## 결론 강도

1. **통신을 시작하기 전 게임 내부의 보고서 생성이 순간 멈춤을 만들 수 있다는 실제 근거가 강해졌다.** 11/7 실제 Gemini 플레이에서 report #2의 ReportJson wall time이 **434.5ms**였다. 해당 소스는 ReportJson을 동기 호출하고 반환한 뒤 UnityWebRequest를 시작한다. 434.5ms에는 요청 전송·LLM 대기·뒤이어 찍는 전체 보고서 로그 시간이 포함되지 않는다. OS 스케줄링 정지·GC가 이 구간에 겹친 시간은 포함될 수 있으므로 434.5ms를 순수 CPU 계산량으로 읽지는 않는다.
2. **LLM이 12~18초 생각하는 동안 게임 메인 스레드가 기다리고 있다는 근거는 없다.** AIPlanner.Tick은 `_req.isDone`이 아니면 즉시 return한다. 실제 응답 대기와 120FPS 근처 프레임 창도 공존한다. 브리지 역시 비동기 HTTP 핸들러와 별도 CLI 프로세스를 await한다.
3. **브리지 메모리 그래프 작업이나 JSON 로그가 대표적인 큰 끊김의 주원인이라는 근거는 약하다.** 일치하는 11/7 report #1~13의 브리지 total-CLI 시간은 #11을 제외하면 3~11ms였다. #11만 5,339ms이며 인증/사용량 조회 등이 포함될 수 있지만 stage 계측이 없어 특정 작업으로 귀속할 수 없다. 이 수치는 브리지 처리시간이며 게임 ReportJson 434.5ms와 별개다.
4. **현재 사용자 증상의 정확한 재현으로 확정하지 않는다.** 위 로그는 11/7 09:50~09:58 KST의 실제 월드다. 11/8 새 설치·새 브리지와 같은 실행은 아니다. 조사 중 11/8 08:02경 사용자가 게임을 새로 켠 것으로 보이며 Player.log가 회전했다. 그 뒤 마지막 08:03 읽기에는 아직 신규 PERF/report가 없었다. 게임에는 개입하지 않았다.

## 자료 대응과 시간

- 이전 원본: `C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/Player-prev.log`, 660,372bytes, 마지막 수정 2026-11-07 09:59:12 KST. 조사 시작에는 Player.log였으며 새 실행에 의해 Player-prev.log로 이동했다.
- 이전 로그 224행: world `a66565fae4bf489b0ec9c119ec31add3`, hosted ServerWorld/ClientWorld0, members `[gemini]`. 38행에는 설치 경로의 AIPlayerSpike 로드가 있다. **이전 프로세스에 로드된 commit/hash는 로그만으로 확정하지 못했다.**
- 브리지 대응: `C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/logs/plan.jsonl`의 2026-11-07T00:50:28.641Z~00:58:55.433Z, 같은 world/member/report_id, 응답 ms가 게임 응답 body와 일치한다. 모든 13개가 ok=true, error=null, CLI 1회, recall rounds=0이다.
- 현재 설치자료: `2026-11-08-제미나이-인게임세팅/installation.json`에는 commit `7af8f91e5d26e4fd9ce5996809ae41e213340118`, 115개 스크립트 일치, BridgePID51816/port8331가 적혀 있다. 이는 현재 디스크 설치와 11/8 시작 브리지의 메타이며 11/7 로드 버전 증명으로 쓰지 않았다.
- 현재 bridge-logs/plan.jsonl: 1,629bytes, 11/8 07:58:30 마지막 수정, start만 있으며 아직 plan 요청 기록이 없다. PID51816 node의 한 시점 누적 CPU=0.25초, working set=65,482,752bytes, private=32,817,152bytes. 현재 유휴 상태 간접 근거일 뿐 과거 통신 순간 CPU 측정이 아니다.
- 현재 health GET: ok=true, busy=[], Gemini found/login/ok=true. usage 캐시 시각은 시작 때 값. GET 때문에 인증·quota 프로세스를 새로 실행하지 않는다.

## 원본에서 보존한 최소 발췌

아래 행번호는 현재 Player-prev.log를 UTF-8로 Get-Content한 기준이다. 보고서 본문·대화문·접속 식별 정보는 생략했다.

```text
318:[AIPlayerSpike] t=69.1 @Gemini PLANNER: report #1 sent trigger=host_said rev=0 reportChars=5453 buildMs=43.2
327:[AIPlayerSpike] t=79.6 PERF 30s: AIs in world 1; frames 3545 (118.2 fps); frame ms avg 8.51 p50 8.33 p95 8.33 p99 8.33 max 158.3; server ticks/s 19.9; mod per AI [Gemini 0.12ms avg/81.5ms max]
328:[AIPlayerSpike] t=82.8 @Gemini PLANNER: report #1 answer after 13.7s code=200 error=null
355:[AIPlayerSpike] t=92.3 @Gemini PLANNER: report #2 sent trigger=host_said rev=0 reportChars=5694 buildMs=434.5
363:[AIPlayerSpike] t=106.7 @Gemini PLANNER: report #2 answer after 14.7s code=200 error=null
377:[AIPlayerSpike] t=109.6 PERF 30s: AIs in world 1; frames 2647 (88.2 fps); frame ms avg 11.33 p50 8.33 p95 8.33 p99 33.34 max 1683.4; server ticks/s 15.6; mod per AI [Gemini 0.65ms avg/773.4ms max]
561:[AIPlayerSpike] t=139.6 PERF 30s: AIs in world 1; frames 3400 (113.3 fps); frame ms avg 8.82 p50 8.33 p95 8.33 p99 16.67 max 591.7; server ticks/s 19.5; mod per AI [Gemini 0.43ms avg/573.1ms max]
973:[AIPlayerSpike] t=219.7 @Gemini PLANNER: report #5 sent trigger=plan_done rev=12 reportChars=6488 buildMs=7.4
975:[AIPlayerSpike] t=229.7 PERF 30s: AIs in world 1; frames 3592 (119.7 fps); frame ms avg 8.35 p50 8.33 p95 8.33 p99 8.33 max 25.0; server ticks/s 20.0; mod per AI [Gemini 0.12ms avg/27.8ms max]
983:[AIPlayerSpike] t=232.9 @Gemini PLANNER: report #5 answer after 13.3s code=200 error=null
2149:[AIPlayerSpike] t=545.3 @Gemini PLANNER: report #11 sent trigger=host_said rev=20 reportChars=6985 buildMs=5.7
2162:[AIPlayerSpike] t=563.4 @Gemini PLANNER: report #11 answer after 18.2s code=200 error=null
2223:[AIPlayerSpike] t=589.7 PERF 30s: AIs in world 1; frames 3566 (118.7 fps); frame ms avg 8.38 p50 8.33 p95 8.35 p99 8.35 max 75.0; server ticks/s 19.9; mod per AI [Gemini 0.14ms avg/154.3ms max]
```

434.5ms 보고 생성·773.4ms AI Tick·1683.4ms 최장 프레임은 같은 30초 창에 있으나 동일 프레임이라는 ID 연결은 없다. AI Tick 최대보다 프레임 최대가 훨씬 크고, 마지막 창에는 Tick154.3ms/프레임75ms처럼 크기 순서도 다르다. 전체 최장 프레임을 보고서 생성 단독으로 설명하거나 세 값을 합산하면 안 된다. 다만 synchronous ReportJson 호출은 반환할 때까지 호출 스레드가 진행하지 못한다는 직접적 구조 근거가 있다.

## 브리지 경로와 수치

- `Tools/ai-bridge/bridge.mjs`: POST /v1/plan에서 요청 수신 뒤 `await plan(...)`; 멤버별 busy를 이용해 중복 제한. 다른 멤버는 병행 가능. GET /v1/health는 `health()` 캐시만 직렬화한다.
- `plan.mjs`: preflight → context/index/prefetch → await recallLoop(CLI) → 응답/로그. 동기 memory 작업은 Node 브리지 프로세스에서 한다.
- `preflight.mjs`: 로그인 최초 1회, quota는 10분마다 await CLI 조회. 갱신은 plan 경로에 있으므로 답이 늦어질 수 있다. health 조회에서는 새 검사하지 않는다.
- `memgraph.mjs:97~120`: readdirSync/statSync/openSync/readSync, 회전 시 기존 이벤트 전체 readFileSync가 있다. 기본은 추가된 이벤트만 읽는다. CPU/I/O 버스트 가능성은 있지만 게임과는 별도 프로세스이며 실제 성능 측정으로 귀속해야 한다.
- `bridge.mjs`: plan/recall 로그 appendFileSync와 JSON stringify. 별도 프로세스의 이벤트 루프를 막을 수 있다. 게임 쪽에도 보고 본문 전체를 로그로 남기는 AIPlanner.cs:442가 있다. 로그 I/O·직렬화 할당은 비용 후보지만 본 조사에 디스크 stall/GC pause 계측은 없다.
- `ai-chatroom/lib/agents/cli/agy.mjs:83` 및 run.mjs:46: 별도 agy 프로세스를 spawn하고 비동기로 기다린다. LLM 네트워크 대기 전체를 게임 CPU 사용시간으로 읽을 수 없다.

| 11/7 report | 브리지 전체 ms | CLI ms | 차이 ms | 게임 보고 생성 ms |
|---|---:|---:|---:|---:|
| 1 | 13645 | 13634 | 11 | 43.2 |
| 2 | 13868 | 13861 | 7 | 434.5 |
| 3 | 14046 | 14041 | 5 | 5.1 |
| 5 | 13285 | 13281 | 4 | 7.4 |
| 11 | 18168 | 12829 | 5339 | 5.7 |
| 13 | 13350 | 13347 | 3 | 6.8 |

差分は preflight·index·解析等の待ち時間を含み、ログ append実行前に totalMsを確定するため末尾ログI/O時間は含まない。#11は開始から約10分でquota再照会の時期と整合するが原因確定ではない。余分な待ち時間があってもその間ゲームが同じ長さ停止する構造ではない。

## 優先順位と次の検証

1. 最優先はゲーム側 ReportJson と既知の候補/経路計画の重い同期区間。ReportJson内を観測・レシピ/制作可否・記憶経験・JSON生成に分け、frameCount・開始/終了時刻・allocation/GC差分と一緒に計測する。報告生成の434.5msは経路計画prepとは別の具体的候補を追加する。
2. 応答適用・次計画開始・観測/保存・ログ出力も同じフレームIDで計測する。通常build4~7msでもAI Tick573msなどが別窓にあるため、報告生成のみの修正で全て解消すると約束しない。
3. 計測後、ゲームデータを安全に読むスナップショットと複数フレームの予算処理、レシピ・経路・観測の適切なキャッシュを検討する。Unity/ECS Worldをそのままワーカースレッドに渡す案にはしない。全JSONログの要約/非同期化も対象にするが、実測順序を守る。
4. 応答の遅さは別改善として、10分ごとのquota再照会やCLI起動時間を分けて計測できる。モデル変更・ブリッジHTTP非同期化だけではゲーム内434.5msを直せない。HTTP待機は既に非同期。
5. GCや他プロセス競合を断定しない。ログ末尾にComputeBufferのGarbageCollector警告があるが終了時解放に関する文言で、プレイ中の特定hitchの収集時間を示さない。現在nodeの軽い累積CPUも過去の競合否定材料にはできない。

今回の調査では検証用コマンド・AI応答を新たに発生させていない。現ユーザーの新しい実プレイから自然に残るPERF/reportを同じframe IDで取得できる段階の検証は別途必要。
