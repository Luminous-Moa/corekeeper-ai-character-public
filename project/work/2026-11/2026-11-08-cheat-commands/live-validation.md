# 시험용 지형·물체 명령 실게임 확인

2026-11-08 15:21~15:50 KST. 판: c0bd393(worktree `C:/Dev/CoreKeeperModSDK-cheat-commands`). 시험 월드 ai2(GUID b39072f4c49d28bf6ab43b7b6a702d7f), 캐릭터 4 Luminous, AI는 Gemini 하나만 참가. 사용자 월드 aiui1106은 쓰지 않았다.

질문: 보류된 실게임 검증(채집·이동·귀환, 생활 가공·제련, 장비 전달, 통로 탐험)에 필요한 조건을 새 명령과 기존 `/ai` 시험 명령으로 바로 세팅할 수 있는가.

## 결론

다섯 가지 세팅 모두 채팅 명령만으로 몇 초 안에 됐다. 서버에 바로 반영됐고, 호스트 화면에도 보였으며, AI도 인식했다. 남은 문제는 세팅 자체가 아니라 세 가지다.
- 세팅하는 동안 AI가 판단 모델을 계속 호출한다.
- 일부 조건(흑요석·금광석, AI 임의 위치 이동)은 만들 수 없다.
- 아이템 spawn이 근처 AI 가방으로 빨려 들어간다(검수 F2가 실제로 재현됨).

## 준비와 복원

- 시작 전 확인: CoreKeeper 실행 없음. Unity는 다른 대화(move-deliver)의 CompileCheck였고, 그것이 끝날 때까지 기다렸다가 진행했다.
- 백업: `C:/Dev/_scratch/cheat-commands-live-20261108/backup`. 기존 `livectl.ps1`과 같은 범위(설치 모드 117, AIPlayers 56, LocalLow Steam 268, Steam remote 49, registry, 시험 스위치)이며 직후 검증 차이 0. 시작 상태는 09e 설치·이전 복원 보류 상태 그대로였다.
- 설치: BuildAndInstall 종료코드 0, 설치된 .cs 117/117이 경로 기준으로 소스와 일치([설치 비교](evidence/installed-compare.txt)). Unity 부재 확인과 게임 실행은 별도 호출로 나눴다.
- 복원: 게임을 정상 종료(저장 후 종료 → 종료)하고 20초 기다린 뒤 restore. 검증 결과 모든 그룹 동일, 차이 0([복원 검증](evidence/restore-verify.txt)). 이번 실행의 decisions 로그는 증거로 옮기고 AIPlayers에서 지웠다. 설치 모드도 09e 상태로 돌아왔다. 이전에 보류된 복원·최종 설치는 하지 않았다.
- 사용량: 99,711 → 99,814회(+103), 23.7780 → 24.0323 USD(+0.254)([전](evidence/usage-before.json), [후](evidence/usage-after.json)).
- 사용자 직접 조작 정황: 없음.

## 세팅별 결과

시간은 채팅 입력(글자당 약 40ms)을 포함한 명령 하나의 왕복이다. 서버 처리는 응답과 같은 틱에 끝났다.

| 세팅 | 명령 | 시간 | 실제 결과 | AI 인식 |
|---|---|---|---|---|
| 1. 갈림길·막다른 길 통로 | `placetilearea WallDirtBlock 14 -10 26 0`(143칸) 다음 `removetilearea wall` 4회 | 명령마다 4.8~5.6초, 전체 약 25초 | 서쪽 입구, 4방향 갈림길(20,-5), 북·남 막다른 길, 동쪽은 자연 동굴로 이어짐([화면](evidence/s1-corridor-junction.png)) | goto가 새 벽을 보고 다시 계획해 입구로 들어감. 이후 AI가 스스로 통로를 탐색하며 북쪽 막다른 길(20,-1)에서 남쪽으로 다시 고름 |
| 2. 광석 벽·흙벽 놓기·지우기 | `placetile Copper ore 15 -4`, `placetile Tin ore 16 -4`, `placetile CopperOre 17 -6`, `removetile ore 16 -4`, `removetile WallDirtBlock 15 -6` | 4.0~4.9초 | 흙벽 위에 구리·주석 광석이 겹쳐 놓임, 제거도 됨([화면](evidence/s2-ore-walls.png)) | `/ai look`의 oreTiles가 정확히 (15,-4) 구리, (16,-4) 주석, (17,-6) 구리. 제거 뒤에는 2곳·벽 201로 줄어듦 |
| 3. 작업대·상자·화로 | `spawn WoodenWorkBench 11 -3`, `spawn Furnace 11 -7`, `spawn InventoryChest 9 -5` | 4.1~4.8초 | 세 개 모두 E로 열림. 상자 18칸, 작업대 제작 목록, 화로 제련 창([상자](evidence/s3-chest-open.png), [작업대](evidence/s3-workbench-open.png), [화로](evidence/s3-furnace-open.png)) | look objects에 세 개 모두 좌표대로 나옴. 기억한 제작대 12 → 14 |
| 4. 적 생성 | 기존 `arena spawn 2 MushroomEnemy`, 새 `spawn MushroomEnemy 20 -8` | 4.8~4.9초 | arena는 고정 장소 (-32,32) 주변에만 생성(적 6 치우고 2마리). 새 spawn은 지정 좌표에 생성([화면](evidence/s4-spawn-enemy.png)) | 새 spawn 적을 0.5초 안에 자기방어 판단으로 공격해 2.5초 만에 처치 |
| 5. 장비 전달 조건 | `give WoodHelm`·`WoodBreastArmor`·`WoodPantsArmor`, `hunger 50`, `hp 80`, `hostto ai`·`hostto x z` | 3.0~4.5초 | 가방 21~23번 칸에 지급, 배고픔·체력 설정, 호스트 순간이동 약 6초 | `/ai inv`에 그대로 나옴 |

보호 조건과 부하:
- 크리에이티브가 아닌 월드라서 코어 칸 (0,1) 설치가 거부됐다.
- tileset 2 설치도 wall·ground 모두 거부됐다. 지식 덤프상 tileset 2는 흑요석(Obsidian Block·Ground)과 금광석 오버레이(Gold Ore)다.
- 143칸 범위 명령은 프레임 튐이 눈에 띄지 않았다.
- 3,600칸(`placetilearea Dirt ground -30 -60 29 -1`)은 해당 30초 구간에 모드 처리 최대 59.5ms, 최대 프레임 66.7ms 한 번이었다. 평균 8.35ms는 유지됐다.
- 검수 F1(int 오버플로)은 작은 좌표 범위만 써서 건드리지 않았다.

## 결함·제약

1. **아이템 spawn이 AI 가방으로 들어감(검수 F2 재현).** `spawn Wood ~3 ~0`(AI에서 3칸)으로 떨군 나무가 바닥에 남지 않았고, AI가 움직이지 않았는데도(33.00,-12.04 그대로) AI 가방의 Wood가 5 → 6이 됐다. AI에서 약 21칸 떨어진 (12,-9)에 떨군 나무는 바닥에 그대로 남았다(화면으로 확인). 따라서 바닥 아이템 시험 조건(주워 가기, 전달 수령)을 AI 근처에 만들 수 없다.
2. **세팅하는 동안 AI를 조용히 둘 수 없음.**
   - 저장된 상태에서 Gemini가 자동 참가하며 기존 목표(구리 5)로 판단 호출을 시작했다.
   - `/ai stop` 뒤에도 슬라임이 다가오자 자기방어 판단이 나왔다. 그 직후 상위 계획이 다시 켜져 목표를 스스로 다시 세웠다(llm:gemini#5).
   - 새로 만든 통로를 스스로 탐색하기도 했다.
   - 이번 확인에서 +103회, 0.25 USD였다. 세팅 중 체력도 24까지 떨어졌다.
3. **arena spawn은 고정 장소 전용.** 임의 좌표 적 배치는 새 spawn으로 해결된다.
4. **AI를 임의 좌표로 옮기는 명령이 없음.** 호스트는 `hostto`로 옮길 수 있지만, AI는 `arena go` 장소나 goto 보행에 의존한다.
5. **흑요석·금광석 조건은 만들 수 없음**(원본 보호 조건, 크리에이티브 월드에서만 가능).
6. **같은 시험장을 다시 만들려면 명령을 여러 번 쳐야 함.** 통로 하나에 5개 명령, 약 25초가 걸렸다. 좌표가 절대값이라 순서만 지키면 같은 지형이 나오지만, 한 번에 적용하는 수단은 없다.

시험 도구 쪽 문제(제품 결함 아님):
- SendKeys는 `~`를 Enter로 보내므로 상대 좌표 입력 때 `{~}`로 바꿔 보내야 했다. 처음 한 번은 명령이 잘려 사용법 응답과 호스트 채팅 "0"이 나갔다.
- 해상도 3840x2160에서는 채팅창 판정 좌표를 (330,2010)/(600,2010)으로 바꿔야 했다.
- 상자를 우클릭했다가 손에 든 버섯 1개를 먹었다(호스트 상태 변경, 복원으로 되돌림).

## 새 명령 후보

- AI를 세계에 둔 채 판단 모델 호출과 상위 계획을 멈추는 시험 스위치(예: `/ai <멤버> hold`).
- AI 순간이동 `/ai <멤버> tp x z`(호스트 `hostto`와 같은 게임 텔레포트 방식).
- 아이템을 바닥에만 떨구는 spawn(F2 수정. 소유자 없이 떨구거나 줍기 지연).
- 여러 명령을 한 번에 적용하는 시험장 구성 수단(명령 묶음 파일 또는 기준 세이브).

## 근거

- [전체 Player.log](evidence/live-Player.log), [이번 판단 로그](evidence/decisions-153019.log), [명령·응답 시간 기록](evidence/live-notes.txt)
- 시험 도구: `C:/Dev/_scratch/cheat-commands-live-20261108/ui`(기존 refactor-runs/ui 사본에 채팅 판정·`~` 처리·우클릭·커서 이동을 더함)
