# Unity 검사와 최종 실게임 기록

## 1. Unity compile-only — 통과

2026-11-06 16:40:18~16:42:38 KST. 부모 지시에 따라 최종 제품 소스를 고정하고 격리 worktree의 `HeadlessSdk.CompileCheck`만 실행했다. 설치·게임·브리지·모델은 실행하지 않았다. 이후 실게임/설치는 부모의 다음 지시를 기다린다.

- 커밋 `902721ec505df42d74d0b2d245ed34a049faadad`, branch `refactor-maint-20261106`, worktree `C:/Dev/CoreKeeperModSDK-refactor-20261106`.
- 시작 전 Unity/CoreKeeper 프로세스 0. status는 보존 대상 SDK 7파일과 미추적 게임 참조 `Assets/Plugins/CoreKeeper/`뿐이었다. 독립 Library 디렉터리를 사용했다.
- 실행: Unity 6000.0.59f2, `-batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK-refactor-20261106 -executeMethod HeadlessSdk.CompileCheck -logFile C:/Dev/CoreKeeperModSDK-refactor-20261106/Logs/headless-compile-902721e.log`. `Start-Process -WindowStyle Hidden -Wait -PassThru`로 종료를 기다렸다.
- 결과: **exit 0, `[HeadlessSdk] CompileCheck: ok` 1건, `error CS` 0**. import/스크립트 컴파일 완료 후 정상 종료했다. 게임 자체 모드 컴파일 성공은 설치 후 별도로 확인해야 한다.
- 소스/메타: 시작 전 Scripts 아래 모든 `.cs`/`.meta` SHA256 목록과 종료 뒤 비교해 **바이트 차이 0**. 새 `.meta` 31개는 모두 대응 소스 존재, Assets 전체에서 같은 GUID가 정확히 1개, GUID 충돌 0. `.meta` 없는 `.cs` 0.
- 컴파일 포함: 파일별 import 메시지는 로그에 없으므로 그것을 import 증거로 삼지 않았다. 대신 Unity가 실제 사용한 `Library/Bee/artifacts/1900b0aE.dag/AIPlayerSpike.rsp`에 새 소스 31개와 전체 모드 Scripts `.cs` 84개가 각각 포함됨을 확인했다. 원본 로그의 Csc AIPlayerSpike.dll → ILPostProcess → Library/ScriptAssemblies 복사 순서와 16:41의 새 DLL/PDB가 근거다.
- 기존 GUID 유지: `AIBodySkills.cs.meta`→`AIBody.cs.meta` = `4895d03a542f2be47b5c2a240ac23fc9`; `AIExplore.cs.meta`→`AIMemory.cs.meta` = `529af9d7954e3fa43b4ea51d91600503`. 기준 a07166f의 원본과 일치한다.
- Unity가 미추적 `.vsconfig` 1개를 생성했다. 삭제/커밋하지 않고 보존한다. 제품 소스 변화는 없다. SDK 7파일의 기존 변경도 보존한다.
- 로그에는 `com.unity.editor.headless` entitlement가 없다는 licensing 404 진단과 종료 중 Curl callback abort가 있다. 이를 숨기거나 “모든 로그 오류 0”이라고 하지 않는다. 최종 CompileCheck 성공/exit0/C# 오류0과 구별한 환경 진단이며 이번 compile을 막지 않았다.

증거: [Unity 원본 로그](C:/Dev/CoreKeeperModSDK-refactor-20261106/Logs/headless-compile-902721e.log), `unity-result.json`, `unity-status-before.txt`, `unity-status-after.txt`, `unity-source-before.json`, `unity-new-meta-check.json`, `unity-new-import-check.json`. 마지막 파일은 새 소스별 실제 compiler response 포함을 기록한다. 종료 뒤 Unity/CoreKeeper 프로세스 0, SHA 동일을 확인했다. 이 검사는 공통 게임 설치본을 배포하는 BuildAndInstall을 호출하지 않았다.

## 2. 실게임 준비 계약

적용 계약은 00 합의, 01 목표/후보 명세, 03 공간/책임 명세 §8, 08 책임별 증거표와 02 환경/복원 절차다. 수정판의 자연어 제작·놓기→후보→내 문 통과→시설/재료 접근→결과·기억·다음 판단, 목표교체/allstop, 대표 위협/공간 회귀, **정상 저장·종료·동일 시험 월드 재입장**을 관측한다. 과거 미관측을 통과로 바꾸지 않는다.

사용자 월드 1~6은 건드리지 않고 새 시험 월드를 만든다. 설치 전 게임/Unity0 재확인, 새 회차 백업/verify, 설치 source hash/manifest, 게임 compile 로그, 끝의 게임 안 시험 월드 삭제·Cloud 반영·복원을 기록한다. 제품 코드 회귀가 있으면 증거를 HEAD로 반환하고 이 담당자가 수정하지 않는다.

FoldRemote는 원격 연결·입력 허용·트랙패드 모드다. 태블릿 영상 문제는 사용자 지시에 따라 조사하지 않는다. Windows는 computer-use sky로 관찰/입력하고, 실제 필요할 때만 승인된 Android 원격 입력 경로를 사용한다. 유지 입력은 현재 게임 UI와 시험 대상 확인 뒤 Windows 캡처로 결과를 검증한다.

## 3. 최종판 설치와 시험 월드 검증

- 2026-11-06 16:46:30~16:48:08 KST, `HeadlessSdk.BuildAndInstall` exit 0. 설치 전 게임/Unity 0, 백업/검증 0 differences. 설치 84개 C# 소스는 최종 worktree와 EOL 정규화 후 차이 0, manifest의 84개 모두 존재.
- 증거/백업 루트 `C:/Dev/_scratch/refactor-maint-live-20261106`. `backup`은 mod 54파일, AIPlayers 43, localsteam 248, remote 40 및 registry/switches. 실제 사용량 기록은 복원 제외로 보존한다. `install-result.json`, `installed-source-sha256.json`, `build-install.log`, `verify-before.log` 참조.
- 16:48:46 게임 PID 80332 시작. `Successfully compiled AIPlayerSpike safetyCheck=False` 확인. Unity 실행 종료 상태. 실제 bridge PID 74200(이번 회차 소유), logs `bridge-logs`. Gemini 계정/모델 preflight 정상; GPT week81이므로 자연어 대표 시나리오는 Gemini 사용. 가짜 bridge는 아직 사용하지 않음.
- 빈 슬롯 7에 `rfmaint1106` 생성, 호스트 캐릭터 4. 사용자 월드 1~6은 선택/변경하지 않았다. 시드 UI는 선택/붙여넣기를 반영하지 않아 생성 화면 실제값 `46564281929`로 진행(아래 최종 파일값 확인). 기존 자료의 고정 시드를 사용했다고 주장하지 않는다.
- worldID `97b88eb024da6d5794f65879c2592dcc`. t331.6 새 월드 AI 설정 OFF 확인 → `/ai on gemini` t359~365 참가/자기 캐릭터 주입·spawn 정상. 시험 명령으로 CopperAnvil 1, CopperBar 12 지급. t426.2 `@Gemini make one CopperSword for yourself` 실제 모델 요청, host instruction1/report1.
- 게임 UI는 sky 단일 행동→관측으로 조작. 새 시험 월드 외 데이터에 대한 read/write 시험은 하지 않았다. 검증 이후 종료·원복 결과는 §4에 기록했다.

### 3.1 실제 자연어/명령/문 — 관측 결과

- t426.2 host instruction 1 → t441.6 실제 Gemini `gemini-3.8-flash-low` report1(set, order follow): `place CopperAnvil base → craft CopperSword 1`. t444.4 모루(-2,-1) 설치 완료/기억, t445.0 검 제작 가방+1·주괴-5, EXP 두 goal run completed. `plan_done` report2 t458.2 새 `gather Wood 15`를 선택했다. 이후 Wood15, CopperPantsArmor, WoodenWorkBench 제작도 완료했다. 가짜 상위 응답을 쓰지 않았다.
- t503.1 멤버 없는 `/ai stop`: pending report4 `request dropped`, auto off. 16:57:27 이번 실제 bridge PID 74200을 종료했다(이후 명시 목표 중 상위 연결 실패 알림은 의도적인 시험 환경 상태이며 제품 회귀로 세지 않음). 하위 행동 판단 모델 호출은 실제로 계속 사용했다.
- ChatGPT 참가 후 Gemini 탐험 rev11/source host, ChatGPT Wood100 rev1/source host → follow rev2로 교체. t659.4 `/ai all stop`에 두 멤버 목표가 각각 cancelled로 끝남; Gemini 진행 중 터널 기술도 취소. 후속 조작 전 추가 SEND가 재개되지 않았다(원본 로그 최종 대조 완료: t659.4 이후 DECISION SEND 0).
- 탐험 중 t605.9 Gemini health100→-1, DOWN1, 5.1초 뒤 부활. 직전 계획에 자연 폭발벽 `WallNaturalExplosiveBlock 2`가 포함되고 터널 중 사망했다. 폭발벽 원인 후보이나 VITALS 진단은 보이는 적/투사체 없음으로 원인 모름. 이를 회귀 또는 정상으로 단정하지 않고 HEAD에 전달, `down-t600-619.txt` 보존. 이후 자동 부활/목표 재판단 관측.
- 시험 장면 준비는 제품의 testcmd 사용: `arena go 0 -2`(체력/무기 재지급 부작용도 명시), `dig 0 -8`(실제 경로 이미 열려 벽0), WoodDoor1 지급, `place WoodDoor 0 -5`. t786.8 설치 완료, edge plan 없이 my door(0,-5) 기억.
- `goto 0 -2`: t816.1 자기 문 감지 → t816.4 열기1회 → 통과 → t817.6 닫기1회 → t819.1 목적지 도착(4.1초). settle nudge/use tick의 Player 로그가 근거다. 이번 movetrace CSV는 생성되지 않았고, 계측은 Claude 전용인데 Claude를 켜지 않았으므로 프레임 CSV 근거로 주장하지 않는다. 문 닫힌 뒤 자기 위치가 문 타일로 반올림되어 mod A* 재계획1회가 있었으나 목적지 정상 도착. baseline과 다른 오류로 단정하지 않는다.
- 저장 전 snapshot `before-save/97b88eb024da6d5794f65879c2592dcc`. 두 참여 멤버 gpt/gemini, Gemini 기억 seen198/visited28/stations4/myDoor(0,-5). 17:04대 게임 메뉴의 정상 저장 후 종료를 선택했다. 완전 process 종료→같은 월드 재입장 비교는 §3.2에 기록했다.

### 3.2 완전 종료 후 실제 재입장

- 첫 게임 t941.3 ECSManager unloading에서 두 캐릭터 저장. 제목 화면의 끝내기로 종료하고 17:05:48 프로세스 0 확인. `Player-first.log`, `after-save-exit` 보존.
- 17:05:59 새 PID 64160으로 시작, 같은 슬롯 7 `rfmaint1106`와 호스트 캐릭터 4로 재입장. 두 번째 프로세스 t161.5 gpt/gemini ON, Claude/Grok OFF. `/ai on` 재발행 없이 기존 캐릭터 GUID와 자기 인벤토리를 로드했다.
- 실제 spawn 후 서버 인벤토리 로그와 파일 snapshot 대조: 멤버마다 charGuid, inventory, inventoryObjectNames, inventoryAuxData, skills, seen, visited, oreXY, oreId, stationId, stationXY, stationPlaced, edgeMyDoors, edgeDoors, drops, **15필드 × 2명 = 30/30 동일**. 참여 설정도 동일. 근거 `persistence-comparison.json`, `after-save-exit`, `after-rejoin`, `rejoin-members.png`.
- GPT 가방 6종, Gemini 9종과 내구도/수량이 실제 서버 로그에서도 일치했다. 처음 제작한 CopperSword는 중간 `arena` testcmd가 무기를 교체하므로 최종 가방 비교 대상에는 WoodSword가 있다. 이를 저장 유실로 해석하지 않는다.
- 추가 지급/설치 없이 `/ai gemini goto 0 -8`: 두 번째 프로세스 t238.6→242.4, 복원된 닫힌 자기 문(0,-5)을 열기 1회→통과→닫기 1회, 3.8초 뒤 DONE. 기억 파일 동일성 외에 실제 복원된 문 사용을 확인했다.
- 두 번째 정상 저장 후 제목 화면 복귀. 시험 월드 삭제/최종 원복 결과는 아래 정리 절에 기록한다.

### 3.3 관측 범위와 독립 검토

자연어 제작/놓기→후보→실제 완료/경험→다음 상위 판단, 두 캐릭터 목표 교체/allstop, 문 설치와 통과, 저장/완전 종료/재입장은 실제 수정판 관측이다. 제작 재료 지급·문 장면 준비에는 기존 testcmd를 썼으며 모델 응답을 가짜로 만들지 않았다. 통제된 물 다리, 닫힌 문 너머 위협, 청크 unload/문 소실 등 모든 과거 장면은 이번에 전수 재현하지 않았다. 동일본문 이동/계약 증명과 과거 실제 로그는 08 대응표로 구분한다.

DOWN1의 피해원은 여전히 미확정이다. [18 독립 검토](18-review-live-observation.md)는 a07166f→902721e 채굴/터널/피해 타입과 Body 생명주기 동일, TunnelSkill의 검수된 WallFact 치환을 확인했다. 이번 관측만으로 새 리팩토링 회귀 blocker 근거는 없다는 한정 판정이며, 폭발 원인을 확정하거나 모든 피해 동작을 검증했다는 뜻이 아니다.

BuildAndInstall 후 `Assets/AIPlayerSpike/AIPlayerSpike.asset`가 git status M으로 표시됐다. 원본 HEAD·index·현재 경로 정규화 blob은 모두 `6c3af303677ca4e3f3e745b352943c8db483bd20`, diff/numstat/cached diff는 비어 있었다. SDK 생성 과정의 파일 갱신 표시와 LF→CRLF 진단이 있으나 제품 내용 차이는 관측되지 않았다. `git add --refresh`만으로 표시는 사라지지 않았으며 강제 내용 복원/스테이징은 하지 않았다. 제품 Scripts는 변경하지 않았다.
## 4. 종료·복원 및 남은 정리 1건

2026-11-06 17:23 KST 기준 제품 검증은 완료했고, **시험 월드 게임 내 삭제/Cloud 삭제는 미완료**다. 두 번째 저장 후 삭제 확인창에서 짧은 click/Return/Space는 삭제되지 않았다. 승인된 Android FoldRemote trackpad tap+hold와 포인터 이동 시도도 PC 입력 변화가 없었다. 실제 앱 toast가 “지금은 PC로 입력을 보낼 수 없습니다”였고, 동일 저장 연결 <HOST>:47800의 정상 끊기→재연결 1회 후에도 같았다. Windows sky 문서에는 유지 시간/down/up API가 없어 문서 밖 입력 주입으로 우회하지 않았다. 부모 지시로 추가 원격 디버깅을 중단하고 삭제창을 취소했다.

- 17:20:43 제목 메뉴 끝내기로 두 번째 게임 정상 완전 종료. 원복 직전 CoreKeeper/Unity 0. 실제 bridge PID 74200은 앞서 종료, 가짜 bridge는 사용하지 않았다.
- 시험 월드 `rfmaint1106`, UI 슬롯 7 = 파일 인덱스 6, GUID `97b88eb024da6d5794f65879c2592dcc`. 보존한 `6.worldinfo` 실제값은 seedString `46564281929`, seed `3830115688`; `6.json`의 globalSeed는 같은 비트값의 signed 표현 `-464851608`이다. 시험 파일 보존 `trial-world-before-restore`.
- Steam cloud_log 17:20:43~44는 `worldinfos/6.worldinfo`, `worlds/6.world.gzip`, `worldgenparams/6.json` **Upload OK**를 기록했다. `cloud-after-exit.txt`, `remotecache-after-exit.vdf` 보존. 서버에서 삭제됐다는 근거는 없으며, 로컬 원복만으로 Cloud 삭제 완료라고 판단하지 않는다. 이후 실행에서 다시 내려올 수 있는 정리 과제다.
- 부모가 승인한 fallback에 따라 각 복원 source/target의 절대 경로·재분석점 없음·프로세스 0을 확인한 뒤 기존 `livectl.ps1 restore .../backup` 실행. **mod 54, AIPlayers 43, localsteam 248, remote 40 파일과 registry 모두 동일, verify 0 difference groups**. `restore.log`가 근거다. 기존 SDK/제품 소스·원본 저장소 내용은 이 담당이 수정하지 않았다.
- 원복 뒤 로컬 Steam 저장 및 Steam userdata remote의 위 3개 시험 슬롯 파일은 모두 없음, trial AIPlayers GUID 폴더도 없음. 사용자 월드 1~6과 기존 캐릭터/설정은 백업 해시로 복원 검증했다. Cloud 잔존은 위 별도 한계로 남긴다. `restored-state.json`.
- 이번 생성 `decisions-165456.log`, `decisions-170902.log`는 증거 루트에 복사 후 SHA256 일치 확인하고 원래 AIPlayers 위치의 해당 2개만 제거. 실제 `decisions-usage.json`은 원복하지 않고 실제 비용 기록 그대로 보존했다. Player-first/second와 bridge 로그도 보존. first/second 모두 `error CS` 0, `Exception:` 0. 단순 error 문자열은 상위 정상응답의 error=null도 포함하므로 오류 건수로 세지 않는다.
- movetrace.on을 만들었으나 프레임 계측은 Claude(Index0) 전용이고 이번 활성 멤버가 GPT/Gemini여서 CSV가 생성되지 않았다. 문/input 판단 근거는 실제 Player 로그다. allstop t659.4 이후 첫 프로세스 끝까지 DECISION SEND 0 확인.
- FoldRemote 원래 **보기 전용 켬**을 정상 UI로 복원했고 “보기 전용: PC로 입력을 보내지 않습니다” toast 확인. trackpad는 원래 모드 유지. 도구 모음을 길게 눌러 열고 정상 끊기 확인 후 연결 시작 화면으로 돌아왔다. `tablet-viewonly-restored.png`, `tablet-end-disconnected.png`. 기존 host 설치/권한/계정/서비스는 변경하지 않았다.

남은 1건: 입력이 가능한 정상 UI 환경에서 위 **시험 월드 1개만** 게임 내 삭제하고 Steam Cloud 반영을 확인해야 한다. 사용자 월드 1~6을 대상에 포함하지 않는다. 제품 검증 통과와 이 정리 미완료를 분리해 인계한다. 원복 이후 게임/Unity를 다시 실행하지 않았다.
## 5. 사용자 요청에 따른 컴퓨터 유즈 직접 입력 재확인

17:29~17:33 KST. 사용자의 “컴퓨터 유즈로 긴 클릭” 지시에 따라 CUA 현재 문서까지 다시 조회했다. 공통 ClickOptions/PressKeyOptions에는 durationMs가 있으나 **Windows native apps reject key and durationMs options**라고 명시되어 있다. native drag는 from/to와 screenshotId만 받는다. 지원되지 않는 duration 옵션을 추정 호출하지 않았다.

원본 backup 재대조 0차이 후 시작. FoldRemote 미연결에서 Steam sky 캡처는 CreateForMonitor 0x80070057로 실패하여 동일 저장 연결을 보기전용 그대로 화면 확보용 연결했다. PC 캡처 회복 후 원본 설치본 게임 PID82160을 17:31:05 시작했다. 제품 테스트/월드 입장은 하지 않았다. Steam 17:31:03~04 로그는 시험 슬롯 3개 파일 Download OK, 로컬 worldinfo name/GUID/seedString 및 UI 슬롯7 `rfmaint1106` 동일 확인. 이는 앞선 Cloud 잔존 우려가 실제 재다운로드로 확인된 것이다.

현재 시험 월드 삭제 확인창에서 문서화된 sky.drag를 버튼 내부 (855,588)→(900,588) 1회 실행했다. 도구 정상 반환 후 창이 유지되고 시험 월드도 그대로였다. 기본 drag가 이 UI의 삭제 유지 요구를 충족하지 못했다. 길이/횟수를 임의 확장하거나 native 입력 코드를 만들지 않았다. 후속 종료/원복 결과를 아래 이어 기록한다.
### 5.1 사용자 중단 지시에 따른 최종 마무리 (17:37 KST)

사용자가 “시험월드 삭제 오래 걸리면 따로 시킬게. 마무리해”라고 지시하여 삭제 및 추가 입력경로 탐색을 즉시 중단했다. 삭제는 성공하지 않았다. 후속 삭제는 사용자 별도 요청으로 넘긴다.

중단 직전 마지막 후보인 OS 기본 Mouse Keys를 공식 Microsoft 문서로 확인했으나, sky의 Alt_L+Shift_L+Num_Lock 1회는 확인창/활성 변화를 만들지 않았다. Flags62 및 MaximumSpeed/TimeToMaximumSpeed/CtrlSpeed는 전후 동일(`mousekeys-before.json`, `mousekeys-after.json`). 숫자패드 0/놓기 입력은 실행하지 않아 버튼 DOWN 유지 상태는 없었다. 기본 Settings.exe는 프로세스만 나타났고 sky에 조작 가능한 설정 창은 반환되지 않았다. Control+Escape로 열린 시작 메뉴는 닫았고 설정 내용을 변경하지 않았다. NumLock의 사전 실제 토글 상태는 별도 수집하지 않았으므로 전후 동일을 주장하지 않는다. 추가 설정 탐색/입력은 하지 않았다.

공식 근거: [Microsoft Mouse Keys](https://support.microsoft.com/en-us/accessibility/windows/use-mouse-keys-to-move-the-mouse-pointer), [접근성 단축키](https://support.microsoft.com/en-gb/accessibility/windows/windows-keyboard-shortcuts-for-accessibility). 숫자패드의 버튼 유지/놓기 기능은 문서상 존재하지만 이번 실행 환경에서 활성화·게임 삭제까지 검증하지 못한 후보로만 남긴다.

게임 삭제창 취소→제목 메뉴 끝내기로 정상 종료. 17:36:28 게임/Unity 0 확인 후 기존 백업을 다시 복원했고 **mod54/AIPlayers43/localsteam248/remote40/registry 동일, verify0 차이**(`restore-after-cleanup-retry.log`). 이번 재시도에는 월드 입장·제품 테스트·모델/bridge 실행이 없었다. FoldRemote는 재시도 중 입력모드 변경 없이 화면 확보용으로만 사용했고, 정상 끊기 확인 후 연결 시작 화면 복귀(`tablet-final-disconnected.png`).

최종 상태는 원래 설치·사용자 저장·AI 설정 복원 완료, 실제 비용·검증 근거 보존, 시험 월드 Cloud 삭제만 사용자 결정에 따라 별도 후속으로 남음이다. 마지막 재시도에서도 Cloud가 정확한 시험 월드 파일을 재다운로드한 사실은 `cloud-cleanup-retry.txt`에 보존한다.