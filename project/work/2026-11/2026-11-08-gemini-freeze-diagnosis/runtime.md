# 2026-11-08 Gemini 응답 뒤 전체 정지 — 런타임 증거

조사 08:08~08:10 KST. 게임·서비스 실행/종료·설정 변경·명령·AI 요청 없이 기존 로그, 해당 프로세스 기본 수치, Windows 이벤트를 읽고 증거 사본만 작성했다. 사용자가 응답 없음 상태에서 직접 종료했다고 확인한 사실을 HEAD에게 전달받았다.

## 판단

**오늘 증상은 Windows가 응답 없음으로 분류한 hang이며, Gemini 상위 응답 뒤 게임 내부 하위 판단 후보/경로 계산 폭증이 가장 강한 원인 후보다.** 두 실행에서 동일하게 첫 후보 준비 약 2.3초 → 하위 API HTTP400 → 3초 후 재시도 → 탐험 경계 2,179곳의 경로를 만들며 약 12초 → 다음 SEND 기록 없이 로그 중단으로 이어진다. 사용자의 “잠깐 멈췄다 풀린 뒤 완전 정지”와 잘 맞는다.

단, 종료 후 확보한 호출 스택/덤프가 없어 **영구 무한루프·deadlock이 확정된 것은 아니다.** 매우 긴 유한 계산, 그 뒤 위험 문구용 재계산, 로그 I/O, 다른 Unity 내부 대기가 최종 멈춤 시간을 얼마나 차지했는지는 남는다. 11/7의 ReportJson434.5ms를 오늘 정지 원인으로 재사용하지 않는다. 오늘 report 생성은 각각62.5/60.7ms다.

## 증거 보존

`evidence/20261108-080811-snapshot.json`에 원본 경로·크기·mtime·보존 사본 SHA256를 남겼다. 모든 파일은 시각 suffix로 새로 만들었다.

| 사본 | 의미 | 크기 | 원본 최종 수정 KST |
|---|---|---:|---|
| 20261108-080811-1-Player.log | 오늘 두 번째 실행 | 215,040bytes | 08:06:26.3646701 |
| 20261108-080811-2-Player-prev.log | 오늘 첫 번째 실행 | 204,791bytes | 08:04:17.9800328 |
| 20261108-080811-3-plan.jsonl | 11/8 활성 브리지 start 및 오늘 두 응답 | 3,190bytes | 08:06:07.5133228 |
| 20261108-080811-4-recall.jsonl | 위 브리지 기억 인덱스 로그 | 2,278bytes | 08:06:07.5133228 |
| 20261108-080811-5-plan.jsonl | 소스 폴더의 과거 로그, 오늘 활성 기록 아님 | 122,890bytes | 11/7 09:58:55.4343059 |

이벤트1001/1002는 `20261108-080905-application-events.json` 및 각각 event XML 사본에 보존했다. `*-timeline.txt`에는 두 실행의 핵심행을 원래 행번호와 보존했다. 원본의 긴 후보·보고 본문은 사본에 그대로 있고 외부 설명에서는 필요한 부분만 인용한다. 마무리 때 원본 사본5개 모두 manifest의 SHA256와 일치함을 다시 확인했다.

## 두 실행의 타임라인

월드는 둘 다 `a66565fae4bf489b0ec9c119ec31add3`(게임명 aiui1106), 활성 멤버 Gemini 1명이다. Player 로그와 브리지는 world/report_id/member/ms로 연결한다. 게임 `t=`는 프로세스 시작 후 초에 근접한 상대시각이며 각 실행 사이에 직접 비교하지 않는다.

| 단계 | 첫 실행: Player-prev.log | 두 번째: Player.log |
|---|---|---|
| OS 프로세스 시작 | 08:02:20.8190679 PID73712 | 08:04:37.9079004 PID45320 |
| 상위 report #1 전송 | 321행 t69.2, build62.5ms | 409행 t71.4, build60.7ms |
| 상위 브리지 정상 완료 | 08:03:56.897, total14039ms/CLI14026ms | 08:06:07.512, total12909ms/CLI12906ms |
| 답변 적용 | 332행 t83.2 give WoodBreastArmor1 Luminous | 420행 t84.4 give CopperBreastArmor1 Luminous |
| 첫 하위 SEND | 343행 t85.6, 후보452, prep2298.8ms | 431행 t86.7, 후보452, prep2269.5ms |
| 그중 경로 계획 | 2201.4ms, n707, cached0, tunnel707 | 2182.5ms, n707, cached0, tunnel707 |
| 경계 탐색 완료 | 345행 t85.7, 9프레임/39ms, found2179 | 433행 t86.7, 9프레임/41ms, found2179 |
| 하위 API 실패 | 347행 t87.4 HTTP400, error空 | 435행 t86.8 HTTP400, error空 |
| 재시도 시작 흔적 | 350행 t90.4 FOOD | 438행 t89.8 FOOD |
| 1차 frontier 경로 계산 뒤 | 351행 t104.3, expanded0, elapsed12762ms, found2179/offered2179 | 439행 t103.2, expanded0, elapsed12349ms, found2179/offered2179 |
| 마지막 행 | 352행 t104.3 BRAIN blocked targets | 440행 t103.2 BRAIN blocked targets |
| Windows hang 종료 기록 | 08:04:32.9459323 Event1002 | 08:06:36.2786311 Event1002 |

두 마지막 blocked targets는 기억한 광석(32,8)의 “아직 못 본 곳을 지나야 함”이다. 이는 마지막으로 출력된 문구이지, 해당 광석 검색이 최종 정지 위치라는 스택 증거는 아니다. 마지막 파일 수정→Windows 닫힘 기록 간격은 약14.97초/9.91초다. 이 간격을 전체 hang 지속시간이나 무한루프 실행시간으로 읽으면 안 된다. 로그 mtime 및 종료 이벤트의 기록/버퍼링 지연이 있고, 계산이 이미 시작된 시점도 더 이르다.

08:08 확인 때 CoreKeeper 프로세스는 없었다. 08:10 활성 Node 브리지 PID51816 누적CPU0.28125초/working set26,439,680/private34,545,664bytes였다. 두 상위 요청은 정상 완료했고 추가 요청은 없으며, 브리지 자체의 지속 CPU 폭주를 지지하지 않는다. 게임 hang 시점 CPU·메모리 표본은 확보하지 못했다. 이전 조사 중 첫 실행 시작 직후 PID73712의 약1.45GB working set 표본은 있었지만 hang 순간 메모리나 OOM 판정에 쓰지 않는다.

## Windows 종료 유형과 덤프

- Application1002 두 건의 AppName=CoreKeeper.exe, AppVersion=6000.0.59.10268, HangType=Unknown. 메시지는 Windows와 상호작용을 멈춰 닫혔다는 내용이다.
- 바로 앞 Application1001 두 건의 EventName=AppHangB1. 사용자 직접 종료 확인과 함께 hang 이후 종료로 분류하는 근거다.
- 같은 최근1시간 조회에서 해당 CoreKeeper/UnityPlayer Application1000은 없었다. **1000이 없다는 사실만으로 모든 native crash 가능성을 배제하는 것은 아니다.** 현재는 AppHang와 사용자 확인이 직접 증거다.
- 두 게임 로그에 해당 정지 직전 managed exception/OutOfMemory/native crash stack은 없고 정상 종료 시퀀스도 없다.
- `Temp/Pugstorm/Core Keeper/Crashes`는 없었다. `Temp/UnityCBMCrashes`, `Local/CrashDumps`에는 08:00 이후 파일이 없었다. `*-crash-dump-locations.json`에 조회 범위를 기록했다. 최근 CoreKeeper WER archive/queue 보고서는 찾지 못했고 이벤트가 지목한 WER 임시 첨부 파일도 조사 때 이미 존재하지 않았다. 따라서 대용량 덤프 복제나 스택 분석은 수행할 자료가 없었다.
- WER 두 이벤트에는 누적 CPU/게임 메모리 수치가 없다. 일반적인 AppHang 분류만으로 CPU spin/lock wait를 구분하지 못한다.

## 코드와 반복 숫자의 교차 확인

설치된 `.../Mods/AIPlayerSpike/Scripts/Scripts`를 직접 읽었다. 이 실행의 로드 commit/hash는 로그에 식별자가 없어 확정하지 않았고, 디스크 설치 메타와 런타임 버전을 동일하다고 단정하지 않는다.

- AIBrain.Candidates.cs:615에서 Explore의 wantExplore=true. give 목표여도 사용 가능한 탐험 선택지를 전부 만든다.
- 689~700행은 frontier `fr`의 각 좌표에 동기 PlanC 호출. 성공하면 lDigExplore에 추가하고 added++한다. 701행 `found fr.Count/offered added` 로그다. **오늘 offered2179는 2,179 경계 모두 1차 PlanC 성공 및 lDigExplore 추가를 뜻한다.** 단순 BFS expanded 수가 아니다. `expanded0`은 이 호출에서 경계 탐색 자체를 새로 돌린 것이 없다는 뜻이며, 동기 경로 계획 2,179개가 없다는 뜻이 아니다.
- FrontierJob 자체는 9프레임에39~41ms로 분산됐다. 12초짜리 로그 elapsed에는 1차 PlanC 루프가 포함된다. “background 탐색이12초” 또는 네트워크가12초 막았다고 읽으면 잘못이다.
- 1118행 blocked targets는 위 1차 루프와 조합 처리 뒤에 출력한다. 뒤이어 Finish/AssembleLifePage와 1151/1162행 위험·기지 경계 문구용 PlanC가 더 존재한다.
- Loop.cs:1122는 cache Count>64면 전체 Clear. 오늘 2,179개 1차 계산에서는 캐시가 반복 지워지며, 이어지는 PlanC에서 재사용이 크게 깨질 수 있다. 성공계획TTL5초보다 1차 계산12초가 길다는 점도 재계산 가능성을 높인다.
- **첫 SEND의 후보452를 두 번째 최종 후보수로 사용하지 않는다.** 두 번째는 SEND에 도달하지 않아 Finish/AssembleLifePage 후 최종 list 크기·위험 문구용 PlanC 처리 수·전체 prep 시간은 미측정이다. 2179를 “최종 API에 전송된 후보 수” 또는 “2차 재탐색 완료 수”라고 주장하지 않는다.
- 이 구조와 두 실행의 동일 로그는 계산 폭증을 강하게 지지한다. 최종 중단 지점은 스택 없이 후보 Finish/위험 문구 재계산 중 하나로 좁혀졌을 뿐 특정 행 확정은 아니다. 아주 긴 유한 계산을 무한루프라고 부르지 않는다.

## HTTP400의 의미와 남은 원인

상위 Gemini 브리지는 HTTP200/ok=true다. **400은 별도의 OpenAI 하위 Decisions API 요청**이며 AIDecisionsApi.cs:28의 `/v1/decisions` 경로다. 두 로그의 `error=''`는 구체 서버 오류를 설명하지 않는다. AIDecisionsApi.Read는 응답의 error.message만 JsonUtility로 추출하며 원문 body를 실패 로그에 남기지 않는다. 별도 body 보존 파일도 확인되지 않아 validation/후보수 제한/요청 크기 제한 등을 확정할 수 없다. 실제452개 후보/약19,9xx inputChars는 사실이지만 그것만으로400 사유를 단정하지 않는다.

400이 3초 뒤 재시도를 유발해 이미 완성된2,179개 경계를 다음 후보 생성에 넣는 흐름은 두 실행에서 반복됐다. 첫2.3초 준비는400 수신 전이므로400 자체가 최초 짧은 멈춤의 원인은 아니다. 실패 응답과 반복 계산은 별개 결함일 수 있다.

현재 우선순위는 (1) frontier 후보별 경로 계산과 후속 재계산의 프레임 예산/계획 재사용 구조, (2) HTTP400 원문 오류 확보, (3) hang 순간 스택 확인이다. 단순 모델 변경이나 브리지 응답속도 개선으로 게임 내부2.3/12초 계산을 해결한다고 설명하면 안 된다. OOM·GPU native crash·다른프로세스 경쟁은 지금 자료에서 적극적으로 지지되지 않으며 완전 배제도 못 한다.

다음 재현이 별도 승인될 때는 [Microsoft ProcDump의 -h 옵션](https://learn.microsoft.com/en-us/sysinternals/downloads/procdump)으로 창이5초 이상 메시지에 응답하지 않을 때 덤프를 받는 방법을 검토할 수 있다(HEAD가 공식 문서 확인). 이번 조사에서는 설치·설정·감시 실행을 하지 않았다. 구현 변경도 하지 않았다.
