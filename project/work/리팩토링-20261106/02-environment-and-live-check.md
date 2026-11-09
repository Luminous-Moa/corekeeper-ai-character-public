# 격리 환경 준비와 실게임 확인 절차

2026-11-06. 부모가 맡긴 범위는 격리 worktree 준비와 기존 절차 조사다. 제품 소스·운영 자료를 수정하지 않았고 게임·Unity·브리지·모델·테스트를 실행하지 않았다. AGENTS.md와 HEAD start-job을 읽었고 세션 연결·저장은 부모가 담당한다.

## 준비한 환경

| 항목 | 결과 |
|---|---|
| worktree | `C:/Dev/CoreKeeperModSDK-refactor-20261106` |
| Git branch / 시작점 | `refactor-maint-20261106` / `a07166f38086e0241c37c601c16452664236813e` |
| 생성 | 기존 같은 폴더/브랜치가 없음을 확인한 뒤 `git worktree add -b ... ... a07166f` |
| SDK 로컬 설정 | 본 저장소의 아래 7개 파일을 새 worktree의 같은 경로로 복사 |
| 게임 참조 자료 | `Assets/Plugins/CoreKeeper`: 440파일, 38,433,592바이트 복사 |
| KG 시험 자료 | `Tools/knowledge/data`: 483파일, 2,803,979바이트 복사. `current.txt` 있음 |
| 복사 검증 | 위 설정·게임 참조·KG 파일 전부 SHA256 대조, 불일치 0 |
| Library | 후속 Unity 검사 준비 요청으로 독립 복사 완료: 25,924파일·2,700,658,127바이트. dotnet 검사 자체에는 필요 없음 |
| 실행 도구 | dotnet SDK 10.0.403, Node, Git Bash, Unity 6000.0.59f2, 게임 Managed 경로 존재 |
| 공유 상태 | 조사 시점 CoreKeeper/Unity 프로세스 0, TCP 8331 listener 0. 실게임 직전 재확인 필요 |

복사한 설정은 `Assets/ModSDK/GameVersionRegistry.asset`, `Assets/Resources/ConditionsTable.asset`와 `.meta`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/ShaderGraphSettings.asset`이다. 이들은 기존 로컬 SDK 환경이며 리팩토링 커밋에 넣지 않는다. 소스 작성자는 명시적 파일 선택으로 커밋한다.

현재 worktree의 변경 표시는 이 7개 설정과 미추적 Plugins/CoreKeeper뿐이다(KG는 ignore). 원본 저장소의 status 목록은 조사 전후 동일하다. junction·symlink·공유 writable Library를 만들지 않았다. 운영 bridge data/logs나 사용자 AIPlayers를 복사하지 않았다.

`CompileCheck.csproj`는 게임 설치본 `CoreKeeper_Data/Managed/*.dll`을 읽기만 한다. 새 worktree의 dotnet 오프라인 검사에는 Library가 필요 없다. 초기 준비에서는 생략했으나 후속 Unity 검사 준비 요청으로 독립 사본을 추가했다. 복사 전 대상 부재·원본 일반 디렉터리·C 드라이브 여유 186,392,162,304바이트·Unity/CoreKeeper 미실행을 확인했다. `robocopy /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ`로 복사했고 종료 1(복사 성공), `/L` 재대조 종료 0, 누락·차이·실패·extras 0이다. 양쪽 파일 수·바이트 일치, 대상 reparse point 0. 삭제·이동·junction·Unity 실행은 하지 않았다. 이 대조는 파일 메타데이터/크기 기준이며 전체 Library SHA256 대조는 아니다.

## 다음 구현 담당자의 오프라인 명령

아래는 준비된 명령이며 이번 담당자가 실행한 테스트는 아니다. 직전 독립 소스 조사에서 a07166f의 관문 전체 통과(core 973)가 확인됐다.

```powershell
Set-Location -LiteralPath 'C:/Dev/CoreKeeperModSDK-refactor-20261106'
dotnet build Tools/compile-check -c Release -nologo
dotnet run -c Release --project Tools/core-tests
& 'C:/Program Files/Git/bin/bash.exe' Tools/core-tests/gates.sh --tag refactor-maint
```

`bash`가 현재 PowerShell PATH에 잡히지 않아 위처럼 명시 경로를 쓴다. gates는 local KG 사본을 기본으로 잡으므로 memgraph가 생략되어서는 안 된다. 기존 tests가 임시 자료를 쓰므로 필요하면 `TEMP`/`TMP`를 새 전용 임시 디렉터리로 지정하되 운영 경로를 재사용하지 않는다.

`compile-check`와 `core-tests`는 모두 **C# 8.0**이다. Core 시험은 Core/**/*.cs와 AIExpCore만 링크하며 Unity 형식이 들어오면 컴파일이 실패한다. 별도 배포 DLL은 만들지 않고 모드 .cs를 그대로 설치한다. 새 파일은 SDK가 제외하는 Editor/CodeGen 폴더에 두지 않는다.

새 .cs/폴더의 `.meta`는 Unity import 뒤 생성물을 확인해 커밋하는 것이 기존 절차다. 기존 파일의 이동은 `.meta`도 같이 옮겨 GUID를 보존한다. 현행 .cs.meta는 `fileFormatVersion: 2`와 고정 guid 형태이지만, GUID를 임의 재사용하지 않는다. Unity import가 새 파일을 인식하고 `git status`의 새 meta 목록이 빠지지 않았는지 확인한다.

## Unity 검사·설치의 실제 경로

Unity 실행 파일은 `C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Unity.exe`다. `Assets/HeadlessTools/Editor/HeadlessSdk.cs`에 두 진입점이 있다.

- `HeadlessSdk.CompileCheck`: 프로젝트 import/컴파일 뒤 종료. 게임 설치 폴더를 건드리지 않는다. 성공은 exit 0, 로그 `CompileCheck: ok`, `error CS` 0이다.
- `HeadlessSdk.BuildAndInstall`: 공통 설치본을 바꾼다. 게임/Unity 미실행, 현재 설치본 백업, 다른 대화의 사용 조율을 먼저 한다. 성공은 exit 0, `BuildAndInstall done`, error CS 0이다.

```powershell
& 'C:/Program Files/Git/bin/bash.exe' Tools/core-tests/gates.sh --unity --tag refactor-maint
& 'C:/Program Files/Unity/Hub/Editor/6000.0.59f2/Editor/Unity.exe' -batchmode -quit -projectPath 'C:/Dev/CoreKeeperModSDK-refactor-20261106' -executeMethod HeadlessSdk.BuildAndInstall -logFile 'C:/Dev/CoreKeeperModSDK-refactor-20261106/Logs/build-refactor-maint.log'
```

두 번째 명령은 **실게임 담당의 설치 단계 전용**이다. `gates --unity`는 CoreKeeper.exe 또는 Unity.exe가 떠 있으면 컴파일을 거절한다. BuildAndInstall 자체에는 같은 외부 프로세스 가드가 없으므로 담당자가 먼저 확인한다. 설치본은 `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`, 소스는 그 아래 `Scripts/Scripts/`다. 실제 설치 후 경로별 .cs 수·정규화 해시를 해당 커밋과 대조하고 ModManifest 파일 목록을 확인한다. 게임 시작 뒤 `Player.log`의 `Successfully compiled AIPlayerSpike`도 별도로 확인한다.

## 실게임 준비와 보존

기존 도구는 `C:/Dev/_scratch/refactor-runs/livectl.ps1`이다. 이번에 읽기만 했다. backup/restore는 게임이 꺼져 있어야 한다. 이 도구는 `/MIR`와 레지스트리 복원을 수행하므로 실행 담당자는 실제 절대 대상 경로와 새 백업 디렉터리를 먼저 검증해야 한다. 기존 백업을 재사용해 덮지 않는다.

백업 범위는 설치 mod 전체, `%USERPROFILE%/AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers`, 같은 LocalLow의 `Steam/435751233`, `C:/Program Files (x86)/Steam/userdata/435751233/1621690/remote`, 게임 레지스트리, 주요 시험 스위치다. 설정된 Steam 사용자 경로가 실제 대상과 일치하는지도 다시 확인한다. 내용을 대화에 출력할 필요는 없다.

```powershell
& 'C:/Dev/_scratch/refactor-runs/livectl.ps1' backup '<이번 회차 새 절대 백업 폴더>'
& 'C:/Dev/_scratch/refactor-runs/livectl.ps1' verify '<같은 폴더>'
```

backup 직후 verify 차이 0을 확인한 뒤에만 설치/설정 변경으로 간다. 실제 과금 누계 `decisions-usage.json`, 결정 로그, knowledge-dump는 백업/복원 제외다. **사용량을 되돌리지 않는 것은 의도된 동작**이다. 현재 설정 내용을 보존하고 시험 전후 비용 차이를 기록한다.

시험용 스위치는 AIPlayers의 `testcmds.on`, `decisionlog.on`, 필요 시 `movetrace.on`이다. 시험 품목 지급은 해당 월드 캐릭터 저장과 같은 자리에 `<캐릭터>.testgive`를 두는 기존 경로가 있으며 testcmds가 켜져야 한다(`AIPlayerSpikeMod.cs:2135`). 실게임 담당자가 필요한 경우에만 사용한다. 운영 모델·프롬프트·사용량 정책은 바꾸지 않는다.

상위 LLM 경로는 worktree의 `Tools/ai-bridge/bridge.mjs`를 node로 실행하고 `http://127.0.0.1:8331/v1/health`를 확인한다. `AI_BRIDGE_LOGS`를 회차 자료 폴더로 지정할 수 있고 작업 cwd/data는 worktree 쪽에 생성된다. `AI_BRIDGE_CONFIG`는 별도 설정 경로, `KG_DATA`는 KG 사본 경로다. 포트가 이미 쓰이면 소유 프로세스를 확인해 HEAD에 보고하며 임의 종료하지 않는다. background Start-Process 사용 시 Hidden으로 실행한다. 실제 bridge 시작은 CLI 인증/사용량 조회를 하고, `/v1/plan`은 실제 호출이므로 최종 검증 담당 범위다.

고정 응답 분기는 `Tools/ai-bridge/test/fake-bridge.mjs --seq test/fake-seq/l0.json --port 8331 --log <회차 요청 로그>`로 확인할 수 있다. health에 `fake:true`가 나온다. 실제 자연어 지시 흐름 통과와 가짜 응답 분기 시험은 결과에서 구분한다. 자연어 → 목표 → 실행 → 결과의 실제 대표 경로는 fake 통과로 대신하지 않는다.

## UI와 입력: 현재 도구 경계

기존 UI 자료는 `C:/Dev/_scratch/refactor-runs/ui/`의 `safechat2.ps1`, `chat.sh`, `gkey.ps1`, `ui.ps1`, `hold.ps1`, `win.ps1`이다. 과거에는 전경·채팅 열림 픽셀 검사 후 입력했고, 세계 삭제에는 hold.ps1의 3초 마우스 누름을 썼다. 이는 이전 실행의 근거이며 현재 좌표나 호출 방식을 그대로 재사용하는 지시가 아니다.

현재 [computer-use skill](C:/Users/<USER>/.codex/plugins/cache/openai-bundled/computer-use/26.1027.30824/skills/computer-use/SKILL.md)과 guidance는 Windows 조작을 node_repl의 `@oai/sky` JS API로 하도록 한다. 같은 턴에 직접 PowerShell UI 자동화를 혼합하지 않는 조건도 있다. 최종 담당자는 skill·guidance·api·confirmations를 읽고, `sky.list_apps/list_windows`에서 반환한 유일한 Core Keeper 창을 골라 현재 화면 관측 → 한 동작 → 새 관측 순서로 입력한다. 과거 고정 좌표를 적용하지 않는다. UI 실행은 이번 조사에서 하지 않았다.

초기화는 `const { sky } = await import('@oai/sky'); globalThis.sky = sky` 형태이며 node_repl의 반환 문서를 따른다. 게임은 이미 설치된 `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper.exe`다. 채팅 입력은 실제 입력칸/focus를 확인하고 문자 입력과 Enter를 구분한다. 시험 명령 자체는 `/ai [claude|gpt|gemini|grok|all] ...`의 기존 게임 경로를 쓴다.

**게임 시작 전 해결할 조건:** 과거 세계 삭제는 3초 유지가 필요했지만, 읽은 sky API에는 hold/duration이 문서화되어 있지 않다. 이것만으로 현재 UI에서 삭제할 수 없다고 결론내리지 않는다. 최종 라이브 담당자가 현재 삭제 UI와 지원 API를 확인하고 시험 월드 정리의 안전한 경로를 먼저 확보한다. 실제로 필요한 입력이 도구 범위를 넘으면 HEAD로 돌린다. 삭제/외부 행동의 승인 해석은 현재 사용자 승인과 상위 지침을 포함하여 HEAD가 판단하며 이 문서가 새 승인을 대신하지 않는다.

## 기록·복원·최종 확인

대표 흐름은 승인 계획의 자연어 지시 → 목표 수용 → 후보·선택 → 문 통과/이동 → 놓기·제작 → 완료/막힘·기억이다. 목표 교체/취소와 변경 책임별 확인은 명세가 정한다. 좌표/지형은 현재 시험 월드에서 관찰한 값만 쓴다. 이전 판을 그대로 재연했다고 가정하지 않는다.

- 로그 원본: `%USERPROFILE%/AppData/LocalLow/Pugstorm/Core Keeper/Player.log`, AIPlayers의 `decisions-*.log`, `movetrace-*.csv`, 시험 월드 캐릭터별 memory/events/goals, bridge plan/recall 로그. 계정 비밀을 포함하는 설정은 결과물로 공개하지 않는다.
- 읽기 보조 `ui/lg.ps1`는 Player.log를 FileShare.ReadWrite로 읽어 패턴/시간 이후 줄을 출력한다. UI 입력을 하지 않는 로그 도구다.
- `Tools/core-tests/asmcheck.mjs <결정로그>`는 ASM-IN/OUT 자료의 짝·후보 포함·필드 누락을 검사한다. `logshape.mjs diff <전 로그> <후 로그> --allow <파일>`은 새로운 로그 모양을 찾는 보조 증거다. 실제 의미 동등성 검수를 대신하지 않는다.
- `movecheck.mjs <기준> <끝> -- <대상>`은 이동 코드의 공백 정규화 잔여 차이를 확인한다. 파일 분리 묶음에서 허용한 실제 변경만 남는지 검토한다.
- bridge `replay.mjs pick/check`는 기존 로그 재구성 검사에 쓸 수 있다. **run은 실제 CLI 호출**이므로 일반 오프라인 검사로 실행하지 않는다.

새 시험 월드를 썼다면 로그·월드 AI 자료를 먼저 수집하고, 게임 세계 선택 화면에서 이번 시험 월드 이름/ID를 확인해 삭제한다. 과거 `livectl restore`만으로는 Steam Cloud에 올라간 새 월드가 지워지지 않아 세계 슬롯이 가득 찼다. 삭제 후 메뉴로 정상 종료하고 Steam 동기화가 끝났는지 확인한 뒤 restore한다.

```powershell
& 'C:/Dev/_scratch/refactor-runs/livectl.ps1' restore '<이번 회차 백업 폴더>'
& 'C:/Dev/_scratch/refactor-runs/livectl.ps1' verify '<같은 폴더>'
```

최종 성공은 mod·AIPlayers·localsteam·remote·레지스트리·시험 스위치 비교 차이 0(도구의 명시적 제외항목은 별도 기록), 게임/Unity 종료, 시험 bridge 종료, 이번 자료 보존이다. 기존 `endrun.sh`에는 고정 좌표·강제 종료가 있으므로 그대로 실행하지 않는다. 최종 설치본을 리팩토링판으로 남길지는 병합/설치 단계의 HEAD 결정에 따른다. 복원 뒤 자동으로 임의 최신판을 다시 설치하지 않는다.

## 근거

- [기존 환경 준비](../2026-11/2026-11-04-리팩토링/4-S0.md)
- [내 문 실게임](../2026-11/2026-11-05-코어-안전지대/34-내문-실게임.md)
- [제작대 놓기 실게임4](../2026-11/2026-11-05-제작대-놓기/15-실게임4.md)
- [시험 월드 정리와 클라우드 문제](../2026-11/2026-11-05-코어-안전지대/30-시험월드-정리.md)
- 현재 worktree의 `Tools/core-tests/gates.sh`, compile-check/core-tests csproj, `Assets/HeadlessTools/Editor/HeadlessSdk.cs`, bridge/fake-bridge/replay, 본문에 적은 기존 livectl/UI 도구를 읽어 대조했다.

## 후속: 화면 부재 시 태블릿 FoldRemote 경로

사용자 조건부 지시: PC 화면이 모니터 부재로 보이지 않으면 ADB 연결 태블릿의 FoldRemote로 이 PC에 원격 연결해 진행한다. 아래 조사는 읽기만 수행했고 앱 시작·연결·UI 입력·설치·설정/권한 변경은 하지 않았다. Windows 직접 PowerShell UI 입력과 구분하며 Windows UI에는 computer-use skill을 적용한다.

- adb: `C:/Codex/Apps/Development-SDKs-2026-09-20/android-sdk/platform-tools/adb.exe`.
- Tab S7+ `SM_T970`: `<LAN-IP>:45223`와 mDNS transport online. 일련번호는 출력하지 않고 내부 비교해 같은 태블릿임을 확인. 별도 Fold `SM_F971N` 항목은 offline. 실행 직전 `adb devices -l` 재확인 후 online 태블릿 하나를 `-s`로 지정한다. 무선 포트는 변할 수 있다.
- 설치 앱: `com.foldremotedesktop.client`, launcher `com.foldremotedesktop.client/.MainActivity`, versionName `0.1.0`, versionCode `1`. 시험 앱 `com.foldremotedesktop.testclient`는 설치 목록에 없다. 버전만으로 설치 커밋/기능을 단정하지 않는다.
- 기존 호스트 PID 25804, `C:/Codex/Apps/FoldRemoteDesktop/releases/20261007-133b55f2-20B3D0A2/frd-host.exe`, UDP `<TAILSCALE-IP>:47800`. 실행 중이므로 준비를 이유로 재시작하지 않는다. 현재 페어링/연결 성공은 아직 UI로 보지 않았다. 비밀 토큰·PIN·계정·인증서 원문을 읽거나 복사하지 않았다.

실행 담당자는 화면 부재를 확인한 뒤 태블릿의 새 캡처로 기존 정식 FoldRemote의 이 PC 항목과 연결 화면을 확인한다. 문서의 “원격 제어 — 이 기기만 사용”은 세션 동안 가상 모니터만 쓰고 정상 연결 종료 때 복원하는 방식이다. 활성 물리 출력 0개이면 가상 모니터가 유일한 출력이다. 보기 전용이면 입력이 전송되지 않으므로 현재 UI 상태를 확인한다. 새 페어링·설치·드라이버/영구 권한 변경은 준비 범위가 아니다.

**유지 입력 후보:** 직접 터치의 길게 누름은 문서상 우클릭이므로 Android 3초 터치가 게임 왼쪽 버튼 3초와 같다고 가정하지 않는다. 가상 트랙패드는 탭 후 250ms 안에 다시 대고 200ms 넘게 유지하면 왼쪽 DOWN, 떼면 UP이다. 탭 보류가 켜져 있으면 앞선 클릭 없이 누름 1번이 전달된다. 현재 설치 앱의 실제 화면에서 안전한 대상으로 전달 의미를 먼저 확인한다. 별도 ADB 명령 시작 지연으로 250ms 창을 놓칠 수 있어 실제 타이밍 검증이 필요하다.

태블릿 `adb shell input` 도움말은 `swipe x1 y1 x2 y2 [duration(ms)]`, `motionevent DOWN|UP|MOVE|CANCEL x y`, `keyevent --longpress`, `keycombination -t duration(ms)`를 명시한다. 도움말만 조회했고 입력은 보내지 않았다. `keyevent --longpress`는 원하는 3초를 지정하는 문법이 아니다. 실제 FoldRemote UI에 문서화된 Android 입력을 보내는 후보이며 Windows host에 직접 SendInput을 보내는 경로가 아니다. 분리된 DOWN 뒤에는 UP/취소와 결과 관찰이 필요하다. 좌표는 현재 영상/커서에서 정한다.

시험 월드만 정리하고 기존 livectl 복원을 끝낸 뒤 정상 연결 끊기로 디스플레이 복원을 확인한다. 기존 `C:/Codex/Apps/FoldRemoteDesktop/start-host.vbs`는 Explorer 사용자 세션용이며 README는 패키지 앱 셸에서 시작하면 APPDATA 가상화로 페어링 identity가 달라질 수 있다고 설명한다. 지금 호스트가 있으므로 호출하지 않았다. `C:/Dev/FoldRemoteDesktoptest/portable/START-HERE.md`의 별도 시험 앱/host(기본 UDP 47810)는 별도 identity이며 정식 host와 동시 실행하지 않는 절차다. 이번 경로를 위해 시험판으로 교체하지 않는다.

근거: `C:/Dev/FoldRemoteDesktop/README.md`, `docs/second-display.md` §1, `docs/physical-input.md` 가상 트랙패드/키보드 절, `docs/architecture.md` 입력 절, 두 저장소 Android manifest/Gradle, 태블릿 현행 `input` 도움말/패키지 조회. 연결 설정/runtime 데이터는 보존한다.

### 화면 사전 확인 실행 결과

문서 조사 후 부모의 추가 지시로 화면 확인과 조건부 연결을 실행했다. 기존의 “입력하지 않았다”는 위 조사 단계까지의 기록이며 이번 추가 실행은 아래와 같다.

1. computer-use skill의 sky로 현재 창을 열거하고 게임 관련 Steam 창 하나만 선택했다(다른 앱 내용은 열람하지 않음). 처음은 최소화 오류여서 `activate_window` 후 재조회했다. 캡처가 `IGraphicsCaptureItemInterop.CreateForMonitor ... Could not capture the given monitor (0x80070057)`로 실패했다.
2. ADB serial `<LAN-IP>:45223`로 태블릿을 캡처했다. 동일 기기의 보조 mDNS serial은 `adb-<DEVICE-SERIAL>._adb-tls-connect._tcp`다. 2800×1752 가로 화면에 정식 FoldRemote가 이미 열려 있었고 저장된 호스트 `<HOST>`, 포트 `47800`, “원격 제어 — 이 기기 화면만 사용”이 선택되어 있었다. 이전 연결 끊김 문구는 `host: Replaced another client connected`였으며 새 페어링은 하지 않았다.
3. 관찰한 “연결” 버튼을 `adb -s <LAN-IP>:45223 shell input tap 1400 596`으로 1회 눌렀다. 이 좌표는 당시 캡처 전용이며 재사용 좌표가 아니다. `PreparingDisplay` 뒤 “전송 설정 전환 중... (새 화면 대기)” 상태로 갔다.
4. sky로 Steam 창을 다시 복원/캡처하여 **866×514의 실제 Steam 화면을 얻었다**. 증거 `windows-capture-after-foldremote.png`. 따라서 원격 연결 뒤 Windows 가상 출력/캡처 확보는 성공했다. 반면 마지막 태블릿 캡처 `tablet-stream-ready.png`는 아직 검정 배경과 새 화면 대기 문구다. 파일명은 준비 확인 시도용이며 영상 수신 성공을 뜻하지 않는다. **태블릿 실제 스트림·PC 입력 전달·3초 유지 전달은 미확인**이다.
5. 게임/Unity 시작·설치·설정/권한 변경·추가 클릭은 하지 않았다. 호스트와 연결을 임의 종료하지 않고 다음 live 담당에게 인계했다. 최종 담당자는 연결 상태를 새 캡처로 확인하고 정상 연결 종료/디스플레이 복원을 완료한다.

화면 근거: `tablet-before-connect.png`, `tablet-after-connect.png`, `tablet-connected.png`, `tablet-stream-ready.png`, `windows-capture-after-foldremote.png`. 연결 전 화면의 공개 호스트/기기 지문은 문서에 전사하지 않았으며 비밀 토큰/계정 자료를 출력하지 않았다. 태블릿 화면이 계속 대기하면 이를 화면 수신 제한으로 보고하고 설정을 임의 변경하지 않는다. Windows sky가 현재 작동하므로 일반 게임 UI 검증 경로는 확보됐지만, 월드 삭제의 유지 입력 문제는 별도 실증이 필요하다.

### 정상 재연결 1회 확인(16:26~16:28)

부모의 후속 지시에 따라 정식 앱의 정상 UI로 끊기→같은 저장 호스트/포트/원격 제어 방식으로 재연결 1회를 수행했다. 게임·Unity·설치·코드·서비스·권한·계정은 변경하지 않았다. 전송 프로필/보기 전용 설정도 바꾸지 않았다.

- 메뉴 짧은 탭은 “보기 전용: 길게 눌러 도구 모음” 안내를 보였다. 따라서 **현재 보기 전용이 켜져 있음**을 실제 UI로 확인했다. 같은 메뉴에 Android의 900ms 동일좌표 swipe를 보내 도구 모음을 열었고, “끊기”→확인창 “끊기”로 정상 종료했다. 이것은 Android 로컬 메뉴 hold의 성공이지 PC 왼쪽 버튼 hold 성공이 아니다.
- 다시 연결하고 안정화 대기 후에도 태블릿은 검정 바탕·커서·“전송 설정 전환 중... (새 화면 대기)”를 보였다. Steam을 sky로 다시 표시/캡처한 화면은 정상이다. `tablet-reconnected-settled.png`, `tablet-after-steam-restore.png`가 비교 근거다.
- 해당 Android 앱 PID에 한정한 read-only logcat에서 재연결 뒤 `recv/dec/rel`은 약 60fps, `drop/frozen/refsLost/waitKey`는 0이었다. HEVC decoder 통계와 실제 태블릿 표시가 불일치하므로 **“네트워크 수신이 없다” 또는 “영상 표시 성공”으로 단정하지 않는다.** 원인은 이번 범위에서 미확정이다. 설정 변경을 반복하는 별도 FoldRemote 디버깅으로 넓히지 않았다.
- 연결과 도구 모음은 유지 중이며, 보기 전용은 원래처럼 켜져 있다. 추가 UI 조작 전에 새 캡처로 상태를 확인한다. host 재시작·시험판 설치·페어링 초기화는 하지 않았다.

**PC 왼쪽 버튼 유지 검증 제안(미실행, 아래 최신 지시 반영):** Windows sky로 실제 게임창·현재 삭제 확인 UI와 이번 disposable 시험 월드가 맞음을 확인한 뒤 필요한 유지 입력만 시험한다. 태블릿 영상 자체는 필수 조건이 아니다. 가상 트랙패드는 tap→250ms 안 두 번째 접촉→200ms 후 left-down 전환→3초 유지→UP 문서 계약을 사용한다. 커서는 최신 Windows 관찰로 대상에 맞추며 sky의 존재하지 않는 hover/move API를 가정하지 않는다. 짧은 click이 해당 UI에서 어떤 효과인지 먼저 관찰하고, 그 후 태블릿 트랙패드가 마지막 PC 커서 좌표를 실제 사용한다는 것은 누름 진행 표시로 확인한다. 전후·유지 중 sky 화면에서 진행/취소·삭제 결과를 확인하고 right-click/context menu면 실패다. 별도 ADB 명령의 250ms 지연 문제는 실제 입력 시 해결하며, UP/취소를 빠뜨리지 않는다. 파일/창 드래그 시험, Windows 직접 SendInput 우회는 하지 않는다. 지금은 게임/PC 유지 입력을 실행하지 않았다.

### 최종 입력 상태와 사용자 최신 지시

사용자는 “태블릿 영상 안나와도 돼. 볼 일 없어”, “그냥 테스트에 문제 없으면 진행해”라고 지시했다. **태블릿 영상 표시 조사/복구는 여기서 중단**한다. Windows sky의 정상 캡처를 게임 검증의 관찰 경로로 사용하며, 필요한 유지 입력만 사용자 지정 FoldRemote/ADB 경로를 쓸 수 있다.

- 원래값 **보기 전용 켬**을 기록한 뒤 부모의 승인 범위 확인에 따라 도구 모음 “보기전용”을 1회 눌렀다. 실제 toast **“입력 켬”** 확인(`tablet-input-enabled.png`). 현재는 원격 제어 연결 + 입력 허용 상태다. 되돌릴 때 같은 버튼을 1회 눌러 보기 전용 안내를 확인한다.
- “모드” 버튼은 선택창이 아니라 즉시 토글이었다. 첫 탭에서 “터치 직접 모드”가 되어 직전이 트랙패드였음을 알았고, 바로 한 번 더 눌러 **“트랙패드 모드”로 원복**했다(`tablet-trackpad-restored.png`). 영상 영역에 터치/드래그/키를 보내지 않았고 PC 입력 시험은 하지 않았다.
- 최종 연결 유지, 입력 켬, 트랙패드 모드, 도구 모음 열림. 영상 대기 문구는 남지만 별도 blocker로 삼지 않는다. 마지막 실제 Windows sky Steam 캡처는 정상이다. 재연결/표시 전환 뒤 앱 창이 최소화될 수 있어 live 담당은 반환된 게임 window를 새로 조회하고 필요할 때 정상 activate한다.
- 게임·Unity·브리지·설치 실행 없음. live 종료 후 정상 연결 끊기/디스플레이 복원, 필요하면 보기 전용 원래값 복원을 인계한다.

## 후속: 삭제 입력 수단의 문서 대조

같은 날 부모 요청으로 현재 API 문서와 기존 기록·보존 화면을 추가 조사했다. 게임 실행·창 선택·클릭·키 입력·설치 없이 `cua.getState()`의 읽기 inventory와 첫 반환 API 문서만 확인했다. Windows 입력은 전혀 보내지 않았다.

### 문서화된 기능

| 수단 | 문서에 있는 기능 | 현재 문서에서 확인한 한계 |
|---|---|---|
| sky `click` | 좌표/접근성 element, mouse_button, click_count, screenshotId | 유지 시간·mouse-down/up 옵션 없음 |
| sky `press_key` | 키 또는 `+` 조합. Return/space/방향키 등 | duration·key-down/up 옵션 없음. 유지 문자열 문법은 문서화되지 않음 |
| sky `drag` | 시작·끝 좌표와 screenshotId | duration 없음. 같은 좌표 drag가 3초 유지라는 근거 없음 |
| sky `perform_secondary_action` | 현재 accessibility tree가 실제로 노출한 보조 동작 | 임의 `Delete`/`Invoke` 이름을 만들어 호출할 수 없음 |
| cua_repl App `click`/`pressKey` | 공통 타입에는 durationMs가 있음 | **첫 반환 Windows 문서가 'Windows native apps reject key and durationMs options'라고 명시**. 일반 타입만 보고 Windows 유지 입력을 지원한다고 판단하면 안 됨 |

따라서 현재 문서에 근거한 3초 mouse/key 유지 호출은 찾지 못했다. 이는 현재 게임 UI에 다른 합법적인 완료 방법이 전혀 없다는 판정은 아니다. direct SendInput/PowerShell UI, 문서 밖 key-down 문자열, 내부 객체/브리지 호출 등으로 우회하지 않는다.

### 기존 실행에서 실제로 확인한 것

- [시험 월드 정리](../2026-11/2026-11-05-코어-안전지대/30-시험월드-정리.md) 46행: 짧은 클릭 80ms와 Space 80·400ms는 삭제 확인창을 닫지 못했고, 왼쪽 버튼 3초 누름은 성공했다.
- 같은 기록은 기존 월드 10개에 대한 정상 삭제와 Cloud 반영을 보고한다. 이후 `mydoortest`와 `placetest3`도 X→3초 누름으로 삭제됐다는 notes/report가 있다.
- 보존 이미지 `C:/Dev/_scratch/refactor-runs/ui/mid.png`, `mid2.png`, `top.png`를 직접 봤다. **세계 선택 목록**에는 방향키 탐색·`SPACE 선택`·`ESC 돌아가기` 안내가 있다. 이것은 삭제 확인창의 입력 계약이 아니다. 이미지들은 삭제 뒤의 빈 슬롯과 남긴 세계 목록이며 삭제 modal의 키 안내를 보여주지 않는다.
- 조사한 기록에서 삭제 확인창의 **Return 성공**, 접근성 보조 동작 성공, 짧은 클릭만으로 성공한 사례는 찾지 못했다. Space 실패 사례에서 버튼 focus가 어떻게 설정됐는지까지는 기록에 없다. '키보드 방법은 모두 실패한다'고 확대하지 않는다.

### 최종 라이브 담당자가 확인할 후보 순서

1. 현재 게임의 세계 선택/삭제 확인 UI를 fresh screenshot과 가능하면 accessibility로 관측한다. 이번 시험 대상으로 확정된 disposable 세계만 대상으로 하고 이름·슬롯·세계 ID를 대응시킨다. 정리 수단 탐색 때문에 기존 사용자 세계를 삭제하지 않는다.
2. 확인창에 단순 활성화로 완료되는 버튼 또는 현재 노출된 보조 action이 있다면 그 **실제 안내/노출**를 근거로 한 번 시도하고 새 화면으로 결과를 확인한다. 과거 목록의 `SPACE 선택`만을 근거로 삭제 확인이 된다고 가정하지 않는다.
3. 현재 확인창이 focus/기본 확인 키를 명시하면, 관측한 focus와 키 안내에 맞춰 지원되는 단발 키 입력을 확인할 수 있다. Return은 기존 성공 근거가 없는 후보이며, 현재 안내가 없으면 여러 키를 무작정 보내지 않는다. 반복 click/pressKey는 연속 hold와 다르다.
4. UI가 연속 유지 입력만 요구하고 노출된 지원 API로 완료 경로를 확인하지 못하면, **그 시점의 화면·문구·API 제약**을 HEAD에 전달한다. 새 시험 세계를 정리할 수 있는지 확인하지 않은 채 새 세계를 계속 만들거나 Cloud 파일을 직접 지우는 경로로 바꾸지 않는다. 실제 UI 확인 전에는 이 조건을 확정 blocker로 부르지 않는다.

승인 규칙 문서도 대조했다. computer-use confirmations는 UI 삭제를 action-time 확인 분류로 설명하고, cua_repl의 반환 문서는 영구 삭제와 명시적 disposable 시험 자료의 recoverable 삭제를 구분한다. 현재 상위 지침은 이미 승인된 범위의 불필요한 재확인을 금지하고 사용자 지시가 skill보다 우선한다고 정한다. 그러므로 실행 담당은 현재 사용자 승인·시험 환경/대상·복원 가능성을 HEAD와 맞춰 적용한다. 이 문서 조사는 삭제 승인을 새로 만들거나 기존 승인을 철회하지 않는다.
