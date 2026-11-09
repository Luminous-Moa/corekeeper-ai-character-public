# hold·tp·F2 수정 실게임 확인

2026-11-08 16:05~16:30 KST. 판: 3dbae75(worktree `C:/Dev/CoreKeeperModSDK-cheat-commands`, F1~F4 수정판 8a9fab4 위에 hold·tp를 더한 것). 시험 월드 ai2, 캐릭터 4 Luminous, AI는 Gemini 하나. 코드는 고치지 않았다.

## 결론

| 항목 | 판정 |
|---|---|
| (1) hold 중 모델 호출 0, 진행 중 행동 즉시 정지 | 통과: 멘션, `/ai goal`, hold 직전에 진행 중이던 요청, 5개 hold 구간 전체<br>미확인: 적 공격·쓰러짐(적이 붙어 있어도 hold된 AI가 피해를 받지 않음) |
| (2) hold off 뒤 다시 판단, 보류 상위 요청 한 번 | 통과 |
| (3) tp x z, tp host, 걸을 수 없는 칸, hold 중 tp → hold off | 통과. 걸을 수 없는 칸은 게임이 코어 스폰으로 보냄(아래 참고) |
| (4) F2: 3칸·1칸 옆 아이템 | 3칸은 바닥에 남음(통과). 1칸은 AI가 주움 |
| (5) hold 상태에서 지난번 시험장 다시 만들기 | 명령 11개, 57초, 모델 호출 0 |

## 준비와 복원

- 시작 전 확인: CoreKeeper 실행 없음. Unity는 다른 worktree(gap-maint)의 CompileCheck였고, 그것이 끝난 뒤 진행했다. 시작 상태의 설치 모드 해시는 1차 확인의 기준과 같았다(09e, 이전 복원 보류 상태 그대로).
- 백업: `C:/Dev/_scratch/cheat-commands-live2-20261108/backup`(모드 117, AIPlayers 56, LocalLow Steam 268, remote 49, registry, 스위치). 직후 검증 차이 0.
- 설치: BuildAndInstall 종료코드 0, 설치된 .cs 118/118이 경로 기준으로 3dbae75 소스와 일치([비교](evidence/control-installed-compare.txt)). Unity 부재 확인과 게임 실행은 별도 호출로 나눴다.
- 복원: 저장 후 종료 → 게임 종료 → 20초 대기 → restore. 모든 그룹 동일, 차이 0([복원 검증](evidence/control-restore-verify.txt)). 이번 decisions 로그는 증거로 옮기고 AIPlayers에서 지웠다. 설치 모드는 09e로 돌아왔다. 이전에 보류된 복원·최종 설치는 손대지 않았다.
- 사용량: 99,814 → 99,879회(+65), 24.0323 → 24.1424 USD(+0.110)([전](evidence/control-usage-before.json), [후](evidence/control-usage-after.json)).
  - 로그상 하위 SEND 67건은 모두 hold가 꺼진 구간에 있다. 첫 hold 전 7건, hold off 확인 구간 60건이다.
  - 상위 report 8건도 모두 hold가 꺼진 구간에 있다.
- 사용자 직접 조작 정황: 없음.
- 입력 스크립트는 `~` 처리 수정분을 썼다.

## (1) hold 중 호출 0

hold 구간별 하위 SEND와 상위 report 수([구간 집계](evidence/control-hold-windows.txt), [전체 로그](evidence/control-Player.log)):

| hold 구간(게임 시간) | 그 안에서 한 일 | SEND | report |
|---|---|---|---|
| 120.6~521.5 | 적 생성·접근, tp, 멘션, `/ai goal dirt 5` | 0 | 0(host_said·host_goal은 보류) |
| 557.0~573.7 | 진행 중 상위 요청을 가진 채 hold | 0 | 0 |
| 594.3~619.4 | 탐색 중 hold | 0 | 0 |
| 638.1~729.6 | 벽 블록 설치, 벽 속으로 tp | 0 | 0 |
| 743.0~종료(약 1060) | F2 아이템, 적·체력 시험, 시험장 재구성 | 0 | 0 |

- **진행 중 행동 정지:** hold를 켠 같은 틱에 행동이 끊겼다.
  - t120.6 기다리기, t557.0 물러나기, t594.3 통로 탐색이 `reason='/ai hold'`로 CANCELLED.
  - t638.1은 짧은 이동이 같은 틱에 끝났다.
  - hold 구간 안에서 새로 시작된 기술은 없었다. 시험 명령으로 부른 `inv`·`look` 같은 즉시 기술만 있었다.
- **진행 중 상위 요청:** t557.0·594.3·638.1·743.0에서 `PLANNER: request dropped (/ai hold); trigger 'action_replan' waits for the release`가 찍혔다.
- **멘션:** t479.2 `report 'host_said' held (/ai hold)`, 호출 없음.
- **`/ai goal`:** t491.1 목표는 rev2로 설정되고 `report 'host_goal' held`, 호출 없음.
- **적 접근·공격, 쓰러짐: 확인 못 함.**
  - 시도: (11,-5)와 (30,-20)에서 hold된 AI 0~1칸 옆에 SlimeBlob, AggressiveSlimeBlob, MushroomEnemy 2마리, MushroomBrute 2마리를 생성했다. 호스트는 15~32칸 떨어뜨리고 체력은 5로 낮춘 채 각 20~30초씩 기다렸다.
  - 결과: 체력은 100 → 100, 5 → 5로 변하지 않았고 쓰러짐도 없었다([화면](evidence/c1-held-enemies-no-damage.png)).
  - 같은 실행 안 hold가 풀린 구간의 싸움 기록(EXP)에도 "받은 피해 0"이 있었다. 이 세션에서는 적이 AI에게 피해를 주는 상황 자체가 만들어지지 않았다.
  - 그래서 피격·쓰러짐 계기에서 호출이 막히는지는 실제로 확인하지 못했다. 피해가 없었던 원인은 조사하지 않았다.
  - 그 사이 적이 접근한 것만으로는 SEND 0이었다.

## (2) hold off 뒤

- **t521.5 hold off**(목표: dirt 5, 보류: host_said·host_goal)
  - t521.6 상위 report #1이 trigger=host_goal로 **한 번** 나갔다. 내용의 host_orders_open에 보류된 멘션 "@Gemini hello…"가 함께 들어 있었다.
  - t521.8 하위 #8이 목표 '흙벽 블록 5개'로 판단했다. 바로 옆에 CavelingSpearman이 있어 trigger는 긴급이었다. 이어서 묶음 채집을 시작했다.
- **t573.7 hold off**: report #4(보류된 action_replan) 한 번, 하위 #35 trigger='정지(hold) 풀림'.
- **t619.4, t729.6 hold off**: 각각 보류 report 한 번씩(#6, #8).
- 보류 report가 두 번 나간 경우는 없었다.

## (3) tp

| 시험 | 명령 시각 | 도착 | 도착 전 SEND | 도착 뒤 |
|---|---|---|---|---|
| `tp 11 3`(hold off 상태, 물러나기 중) | t577.0, 물러나기가 `reason='/ai tp'`로 CANCELLED | t583.1, 6.2초, (11.0,3.0) | 0 | t583.2 #41이 (11,3)에서 통로 탐색 시작 |
| `tp host`(호스트 (11,-5)) | t622.8, 통로 탐색 CANCELLED | t628.9, 6.2초, (10.0,-6.0) | 0 | t629.1 #51 trigger='순간이동 도착(10,-6)' |
| hold 중 `tp 11 -5` → hold off | t231.6 | hold off 때(t521.5) `arrived after 289.9s at (11.0,-5.0)` | 0 | t521.8 #8이 (11,-5)에서 판단 |
| hold 중 걸을 수 없는 칸 `tp 24 -10`(직접 깐 9x9 흙벽 한가운데) | t~700, 응답 "(걸을 수 있는 칸 확인 못함)" | hold off 때(t729.6) `not confirmed after 32.1s at (0.0,-0.1)` | 0 | t729.7 #65가 코어 스폰 옆에서 판단(기다리기) |

- **걸을 수 없는 칸:** AI는 벽 속에 남지 않고 코어 스폰 지점 (0,-0.1)에 있었다. 같은 칸으로 `hostto ai`한 호스트도 코어 쪽으로 옮겨졌다. 게임의 텔레포트 처리가 막힌 목표를 코어로 바꾸는 것으로 보인다(관측 결과이며 게임 코드는 확인하지 않음). 모드는 도착 미확인으로 처리하고 실제 위치에서 다시 판단했다.
- **hold 중 tp:** 순간이동 자체는 hold 중에도 실행됐다(hostto ai로 위치 확인). 다만 도착 처리와 판단은 hold off 때 한 번만 일어났다. 설계와 같다.

## (4) F2

hold된 AI가 (0,-0.1)에 서 있고 호스트는 (0,1.7)에 있는 상태에서 실행했다.

- `spawn Wood ~3 ~0`: (3,0) 바닥에 남았다. look items에 `Woodx1 (3.0,0.0)`([화면](evidence/c4-f2-items.png)).
- `spawn Wood ~1 ~0`: 바닥에 남지 않았고, AI 가방에 `14:Woodx1`이 새로 생겼다(전에는 Wood 없음, 빈 칸 26 → 25).
- 1차 확인에서는 3칸 옆 아이템이 AI에게 끌려갔으므로, 끌어당김 대상 제거(F2 수정)는 3칸에서 효과가 확인됐다.
- 1칸 옆 아이템이 들어간 경로는 hold로 입력이 없는 상태에서 일어났다. 게임의 근접 자동 줍기로 보이지만, 코드 경로로 구분하지는 않았다.
- 바닥 아이템 시험 조건은 플레이어로부터 2칸 이상 떨어뜨려 만들어야 한다.

## (5) hold 상태에서 시험장 다시 만들기

- AI를 hold한 채 1차 확인과 같은 명령 11개를 실행했다(흙벽 143칸 → 벽 제거 4회 → 광석 3개 → 작업대·화로·상자). 결과는 모두 1차와 같았다.
- 걸린 시간: 첫 명령 전송부터 마지막 응답까지 57초(명령당 약 5초, 채팅 입력 포함). 재시도는 없었다.
- 그 사이 SEND·report 증가 0([화면](evidence/c5-setup-under-hold.png)).
- 1차 확인은 세팅 구간에서 판단·상위 호출이 이어져 전체 +103회였다.

## 결함·관찰

- **hold된 AI가 피해를 받지 않아 피격·쓰러짐 계기 시험을 못 함.** 이 세션의 hold가 풀린 싸움에서도 받은 피해가 0이었다. hold 때문인지, 이 월드나 캐릭터 상태 때문인지는 확인하지 않았다. 쓰러짐 계기 시험에는 별도 수단(예: 시험용 피해·쓰러뜨리기 명령)이 필요하다.
- **아이템 1칸 옆 spawn은 AI가 주움**(위 (4)).
- **걸을 수 없는 칸으로 tp하면 게임이 코어 스폰으로 보냄.** 응답의 "(걸을 수 있는 칸 확인 못함)" 뒤 실제 위치는 목표와 다르다. 모드는 미확인 도착으로 처리했다.
- **채팅 입력 실패 2회**(시험 도구): `hp 5`, `spawn MushroomBrute` 전송 실패. 다시 보내서 처리했다.

## 근거

- [전체 Player.log](evidence/control-Player.log), [이번 판단 로그](evidence/control-decisions-161117.log), [명령·응답 시간 기록](evidence/control-live-notes.txt), [hold 구간 집계](evidence/control-hold-windows.txt)
- 시험 도구: `C:/Dev/_scratch/cheat-commands-live2-20261108/ui`
