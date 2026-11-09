# 길 계산 최적화 실게임 검증

## 현재 상태

2026-11-08 2차 후보43c3817fc1a36142c8591fb087ed867665633fe1의 대표 실기 흐름을 HEAD가 수용했다. 자연어 계획 수신→하위 정상 응답→실제 이동을 확인했으며 선택지19~56개, API 요청31회에서 HTTP 오류가 없었다. 이동 중 장애물 변경에 따른 재준비 지연은 남아 있고 아래에 기록했다. 게임 정상 종료 후 시험 전 자료를 복원·검증한 다음, HEAD 지시에 따라 모드만 최종43c3817로 설치했다. 설정·AI자료·세이브·Steam remote는 백업과 일치한다. 아래는 시간순 기록이며 마지막 절이 최종 설치 상태다.

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


## 2차 대표 실기 · 43c3817

최종 후보는43c3817fc1a36142c8591fb087ed867665633fe1이다. 구현 담당의 독립 검수·Unity 수용 확인 후 설치했다. live2-build-install.log는 exit0이며 live2-installed-compare.json에서 C#116개가 후보 소스와 일치한다. 시험 직전 기존 백업의 모든 그룹 차이0을 다시 확인했다.

동일 ai2, 동일 호스트 캐릭터4, Gemini1명을 사용했다. 기존 시험장 적에 의해 호스트가 입장 직후 쓰러지고 재생성됐다. 시험 자료는 종료 후 복원했다. 이번에는 실기 담당 외 추가 사용자 채팅·조작이 관측되지 않았다.

### 대표 흐름

| 시각 t | 관측 |
|---|---|
|153.2|연결 직후 자기방어 첫 SEND.45개, prep110.4ms,7frames,wall152.7ms,plans1. 첫 사용 초기화가 포함된 표본이다.|
|154.4|정상 RESULT APPLY, latency1.20초. 물러나기 실제 실행 시작.|
|157.0~158.8|물러나기와 여러 짧은 이동 DONE. 위치 변화 확인.|
|176.0|동일 자연어 갑옷 요청을 입력했다. 기존 상위 응답이 진행 중이어서 요청이 기다렸다.|
|203.9|상위 report3이 Wood10 채집→WoodBreastArmor1 제작→호스트에게 주기를 적용했다. 해당 상위 호출 latency14.6초.|
|212.8|새 목표의 하위 SEND29개. prep570.7ms, SEND frames121/wall1002.4ms, plans333.4ms/n16/cached17. PREP finished는 전체184frames/work571.4ms/maxSlice9.96ms/trees1/expanded30513/tiles33207을 기록했다. 서로 다른 프레임 계측 범위를 섞지 않는다.|
|213.1|정상 RESULT APPLY latency0.27초. 동쪽 통로 탐색 START, 후속 좌표 이동(-18.07,1.00)에서(-14,12.05)까지 확인.|
|215.4~271.5|후속 SEND26/23/19/29개, 정상 선택 응답. 마지막 #31은 계속하기를 선택했다.|
|310.5|/ai gemini stop 처리로 목표 해제. 정상 저장 종료와 게임 종료를 수행했다.|

전체 SEND31회, 선택 응답 파싱31회, RESULT 표시29회다. #28/#29는 응답 파싱 뒤 통로 분기에서 한 칸 경로가 적용되어 RESULT 문구가 별도 출력되지 않는다. RESULT 중2회는 이미 끝난 기술의 continue를 버린 정상 수명 검증이다. 스킬 START18회, DONE12회이며 후보 최소19개·최대56개, HTTP4xx/5xx 로그0이다. 비용 기록은31회 증가해99247회,22.667075637766434USD이며 이번 증가0.043786800652480196USD다. 기존 비용 가드는 수정하지 않았다.

### 프레임과 남은 재준비 지연

- AI 활성30초 구간 t174.7~324.7에서118.4~120.0fps, p95는모두8.33ms, p99는8.33~8.34ms다. 구간별 frame max는100.0/25.0/100.0/8.3/33.3/83.3ms였다. 서버는19.9~20.0ticks/s였다.
- 최초 PREP maxSlice45.65ms와 최초110.4ms 누적 준비를 반복 구간과 구별한다. t264.7 안정 구간은120fps/frame max8.3ms/AI평균0.86ms·최대8.0ms였다. 공유 검색을 쓴 새 목표 완료 표본의 maxSlice는9.96ms다. 3ms 하드 상한을 입증하는 결과로 쓰지 않는다.
- 장애물 위치 변경 취소는전체26회다. 초기2회, t182.9~199.2 통로 이동 중17회, 갑옷 목표 적용 후 t205.0~211.2에7회다. 앞선17회 구간도 t200.8에 SEND로 회복했다. 새 목표는 t203.9→212.8의8.9초 후 SEND에 도달했다. 각 재시작이약1초 이어지며 누적 대기시간이 남는다.
- 이번에는 첫 시험의147.9초 미완료와54.6초 CPU 작업 뒤 취소가 반복되는 증상은 관측되지 않았다. 이동 중 재준비가 모두 즉시 끝나는 것은 아니며, 위8.9초와17회 재시작 구간은 잔여 성능 관측으로 남긴다.

### 판정 범위

HEAD가 요청한 계획 수신→후보 준비→실제 API 정상 응답→몸 이동의 대표 흐름은 충족했다. 모델은 일반 통로 행동을 골랐으며 원정 메뉴의 대량 leaf 화면은 선택하지 않았다. 따라서7526개 대상 전체를 메뉴로 열어 모두 접근하는 실게임 검증은 미실시다.118개 전수 오프라인 검증과 구별한다. 갑옷 최종 제작·전달은 이번 대표 흐름의 완료 조건에 포함하지 않았고 관측하지 않았다. 채집 확보는 마지막 관측에서Wood0이다.

## 2차 복원과 최종 사용자 설치

- 게임을 메뉴로 정상 종료하고 프로세스·게임 창이 없음을 확인했다. live2-evidence/에 최종 Player.log, decisions.log, 비용 기록, timeline, metrics.json과 SHA256 manifest.json을 보존했다.
- 기존 백업을 복원한 직후 mod116, AIPlayers53, localsteam268, Steam remote49, registry/스위치는 모두 차이0이었다. 이는 시험 전 설치본7af8f91 계열로 돌아온 단계다. 근거는 live2-evidence/restore-verify.txt다.
- 이후 HEAD가 최적화 완료판을 사용할 수 있게 모드만43c3817로 설치하도록 지시했다. final-build-install.log는 exit0이다. final-installed-compare.json은116개 소스 SHA256와 설치 SHA256의 완전 일치를 기록한다. 해당 Scripts 소스와 커밋43c3817의 git diff도 비어 있었다.
- 최종 보존 검사 final-preservation-verify.txt에서 AIPlayers53, localsteam268, remote49, registry/스위치는 백업과 일치한다. mod그룹만 승인된 최종후보로 달라 총1개 차이 그룹이다. 최종 설치는43c3817이며 시험 전7af8f91 설치본 백업은 보존되어 있다.
- 최종 설치 후 게임은 다시 실행하지 않았다. CoreKeeper와Unity 프로세스는 없고 원래 브리지PID51816은 유지됐다. 현재 세이브·설정은 시험 전 상태이며 실제 API 비용 기록은 보존됐다.
