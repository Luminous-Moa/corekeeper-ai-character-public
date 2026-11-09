# 길 계산 최적화 실게임 검증

## 현재 상태

2026-11-08 1차 실기를 완료하고 시험 전 상태로 복원했다. 후보 2a0112d04559ff84fcaf87e1e2e2b2292ee22d2c는 실기 미통과다. 대량 준비가 분 단위로 이어져 재시작됐고, 최종258개 요청에서 API255개 상한 오류가 확인됐다. 게임은 정상 종료되었으며 복원 검증은 차이0이다. 아래 준비·인계 기록은 시간순 경과이며 마지막 절이 최종 상태다.

HEAD는 이번 실기 담당에게 검수 수용 후보 임시 설치, 버림용 시험 월드 백업, 대표 실기와 복원을 명시적으로 허용했다. review-criteria.md의 게임 조작 제한은 독립 코드 검수자 역할에 한정함을 정정했다. 사용자 월드의 중대한 변경은 승인 범위에 포함하지 않는다.

## 환경과 보호

- CoreKeeper 프로세스가 없는 상태에서 준비했다. 세션 대화 목록에 게임을 조작 중이라고 명시한 별도 작업은 없었다. Unity CompileCheck는 구현 담당이 관리하며 종료 후 게임을 실행한다.
- 기존 버림용 ai2는 월드 슬롯5, guid b39072f4c49d28bf6ab43b7b6a702d7f다. 과거 실기 호스트는 캐릭터4다. 최근 hang의 aiui1106은 슬롯8, guid a66565fae4bf489b0ec9c119ec31add3다.
- 게임 설치본은 C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike다.
- 기존 보호 도구 C:/Dev/_scratch/refactor-runs/livectl.ps1의 전체 내용을 검토했다. backup/verify를 C:/Dev/_scratch/path-opt-live-20261108/backup에 실행했다. 새 백업 디렉터리임을 먼저 검사했다.
- 백업 검증은 mod116파일, AIPlayers53파일, localsteam268파일, Steam remote49파일 및 레지스트리와 시험 스위치 모두 차이0이다. AI 비용 파일 decisions-usage.json은 실제 소비 기록으로 복원 대상에서 제외된다. 비밀값을 출력하지 않았다.
- 기존 모델 설정 gpt-6-luna, 월 한도200USD, 누적22.623288837USD, 분당30회 가드를 확인했다. 설정 변경은 하지 않았다.

## 준비한 시험 흐름

1. 검수 수용 후보 해시와 Unity 완료를 구현 담당에게 받는다. 설치 직전 게임 프로세스와 설치본 해시를 다시 확인한다.
2. 승인된 후보만 임시 설치하고 기존 전용 ai2 월드를 호스트 캐릭터4로 연다.
3. testcmds.on와 decisionlog.on로 전체 후보 key·설명을 별도 기록한다. Gemini 1명을 연결하고 갑옷을 만들어 달라는 자연어 명령에서 상위 응답, 후보 준비, 하위 요청과 응답, 실행을 연결한다.
4. 준비 frames/work/plans n/cached, frontier found/offered, SEND 후보수, PERF 긴 프레임, UI 응답과 stop 취소를 확인한다. HTTP400이면 새 마스킹 오류 로그와 request id를 보존한다.
5. 게임을 정상 종료하고 새 로그를 작업 폴더에 보존한다. 시험 전 설치본·AI자료·월드·remote·레지스트리·스위치를 기존 도구로 복원 후 차이0을 확인한다.

모든 후보 key 집합 보존과 수천 타깃의 프레임 시간은 실제 관측 범위에 따라 판정한다. 작은 ai2 장면만 실행됐을 경우 원래2179타깃 hang 재현 해소로 확대 해석하지 않는다.

## 1차 실기 결과 · 후보 2a0112d

10시 전후 BuildAndInstall이 exit0으로 끝났다. 설치된 C#116개는 후보 소스와 SHA256가 모두 일치한다. live-build-install.log와 live-installed-compare.json을 보존했다. 게임 PID69172로 실행해 UI에서 ai2와 호스트 캐릭터4를 선택했다. 런타임 로그 world id도 ai2와 일치했다.

기존 ai2에는 지난 전투 시험의 적이 코어 가까이에 남아 있었다. 호스트가 입장 직후 사망하고 다시 생성되었다. 이 상태 변화는 백업 복원 대상이다. Gemini 한 명을 연결하고 @Gemini 나에게 입을 갑옷 하나 만들어서 줘.를 전송했다.

- t157.6 상위 요청. 첫 응답은 plan[2] bad goal.do로 파싱 실패했다. 기존 자동 재시도로 t192.2 정상 응답을 적용했다. 계획은 Wood10 채집, WoodBreastArmor1 제작, Luminous에게 주기다.
- t203.4 frontier 탐색이77프레임, 373ms 작업으로30492타일을 확장해7526지점을 찾았다. 이 숫자는 최종 전송 후보수가 아니다.
- t192.2부터 t340.1까지147.9초 동안 후보 준비가 완료되지 않았다. 이후 idle threat set changed로 취소됐다. 취소 로그는17647프레임, 54636.3ms 누적 작업을 기록했다. 해당 기간 최종 SEND가 없었다.
- t200.8~320.8의30초 구간 프레임은118.5~120.0fps, max16.7~100.0ms다. t260.8 구간은120fps, frame max16.7ms, AI 평균3.18ms/최대11.2ms였다. GUI 응답과20ticks/s 서버 시뮬레이션은 유지되었다.
- t350.3 도구부족 후보 재구성이 발생했고 t351.0과351.2 위험 변화로 다시 취소됐다. 신규 하위 호출은 관측되지 않았고 decisions-usage.json의 누적 호출99216/22.623288837USD는 시험 전 값과 같았다.

판정은 실기 미통과다. 화면 전체의 긴 동기 정지는 이 구간에서 관측되지 않았지만 대량 경로 준비 총량이 남아 행동으로 진행하지 못한다. 모든 후보 보존, 최종 하위 응답 및 실행은 아직 검증되지 않았다. 구현 담당에게54.6초 CPU/147.9초 경과와 재취소 증거를 전달했고 수정 작업을 재개했다.

## 사용자 화면 사용 감지와 현재 인계

t299.3에 실기 담당이 입력하지 않은 @Gemini 왜 안움직이니 요청이 추가됐다. 사용자 화면 사용으로 판단해 UI 입력·게임 종료·복원을 중단하고 HEAD에 알렸다. 담당자의 마지막 UI 입력은 Return이며 stop 명령은 보내지 않았다. HEAD가 사용자에게 화면 사용 종료와 복원을 확인 중이다.

현재 후보2a0112d 설치와 게임PID69172가 유지되고 testcmds.on/decisionlog.on도 켜져 있다. 아직 설치본·세이브 복원은 수행하지 않았다. 백업 C:/Dev/_scratch/path-opt-live-20261108/backup은 보존되어 있다. 새 후보 설치나 종료는 UI 소유가 정리된 뒤 진행한다.

보존 증거는 live-evidence/ai2-wait-1.Player.log, ai2-wait-1.decisions.log, ai2-handoff.Player.log, ai2-handoff.decisions.log다. 사용자 추가 입력 시점 이후 로그는 사용자 조작이 섞인 관측이다.


## 1차 실기 최종 요청·정상 종료·복원

사용자는 HEAD에 정상 종료해도 된다고 명시했다. 화면을 다시 인수해 stop와 게임 메뉴 정상 종료를 수행했다.

- t422.5 첫 하위 SEND가 발생했다. trigger는 바로 옆 새 적 AggressiveSlimeBlob, set=close-fight, candidates=258, inputChars=22812다. prep3850.8ms,1199frames,wall10001.7ms,plans3617.8ms,n179,cached345였다. 이 수치는 앞서 끝나지 못한7526frontier 준비와 다른 긴급 전투 build다. 7526개 최종 목록의 완성을 뜻하지 않는다.
- t423.2 HTTP400 응답은 questions[0].choices 최대255개에258개가 전달됐다는 array_above_max_length다. request id는 req_58555125c51b484e94f86e6a7214d33f, latency0.71초다. 원문은 ai2-final.Player.log와 ai2-timeline.txt에 보존했다. 따라서 이번 실행은 실제 API 요청 도달까지 확인했고 성공 응답·몸 행동은 미확인이다.
- t505.5 /ai gemini stop이 처리돼 goal cleared와 준비 취소가 같은 시각에 기록됐다. 취소된 마지막 build는4282frames,13201.0ms 작업이었다. 화면에도 목표 해제 안내가 표시됐다.
- UI의 저장 후 종료로 타이틀 복귀, 종료 버튼으로 프로세스 종료를 확인했다. 강제 종료는 사용하지 않았다. 런타임 마지막 로그·decisionlog·비용 파일을 live-evidence에 복사하고 SHA256 manifest.json을 기록했다.
- 복원 직전 네 대상 경로와 백업 경로의 Resolve-Path 결과가 기대 절대경로와 일치함을 검사했다. 기존 livectl.ps1 restore를 실행했고 mod116,AIPlayers53,localsteam268,Steam remote49 및 registry/시험 스위치가 모두 원본과 같았다. restore-verify.txt의 차이 그룹은0이다.
- 실기 전부터 존재하던 Node 브리지 PID51816은 유지되어 있다. CoreKeeper와 Unity 프로세스는 없다. decisions-usage.json의 누적99216회/22.623288837USD는 그대로 보존했다. 이번 상위 구독 CLI 호출4회는1회 파싱 실패·1회 정상 계획·사용자 질문1회·주기 관찰1회로 로그에 남았다.

후속 후보는 이번 세이브 백업 상태에서 다시 시험할 수 있다. 수정 구현의 대량 준비 완료와 API 선택지 전송 구조가 준비되면 동일 대표 흐름을 재검증해야 한다.
