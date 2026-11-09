# Core Keeper 검증용 기존 치트 모드 조사

조사일: 2026-11-08

## 목적과 범위

AI 캐릭터 구현의 실게임 검증에 필요한 시험 재료·지형·적·캐릭터 조건을 준비할 기존 모드를 조사한다. 이번 결과는 모드 존재와 적합성 평가이며 설치나 실게임 호환 검증은 수행하지 않았다.

기존 작업 기록상 가방·장비, 생활 행동, 채집·이동·귀환 등의 실게임 검증이 치트 환경 준비 뒤로 남아 있다. 이 조사에서는 그 환경 준비에 쓸 수 있는 도구를 우선한다.

## 로컬에서 확인한 사실

- `C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/Player.log:28`에 게임 버전 `1.3.0.7-5554`가 기록되어 있다.
- 같은 로그에는 AIPlayerSpike의 로컬 로드가 기록되어 있다. mod.io 로더는 인증되지 않아 모드를 로드하지 못했다고 기록한다. 따라서 게임 내 구독 설치 경로를 사용한다면 인증 여부부터 확인할 필요가 있다. 수동 설치 경로도 공식 문서에 있다.
- 이번 조사로 게임 파일, 모드, 월드 또는 캐릭터를 변경하지 않았다.

## 후보와 판단

| 후보 | 확인한 기능·조건 | 검증용 판단 |
|---|---|---|
| ChatCommands / limoka8 | 아이템 지급, 엔티티 생성, 타일 설치·삭제, 회복·포만도, 스킬 조작, 무적·적 행동·noclip 조작. 현행 mod.io 목록은 2026-09-21 갱신, 게임 1.3.0 태그, Client/Server 및 CoreLib 필요를 표시 | 정해진 수량·좌표로 시험 조건을 재현할 우선 후보 |
| Item Browser / moorowl | 아이템·생물·요리 검색, Object ID 검색, 출처·용도 확인. 기본 Z로 창 열기, 옵션에서 Cheat mode 활성화 뒤 기본 가운데 클릭으로 아이템 지급. 치트는 Creative 또는 관리자 권한 필요. 배포 페이지에 1.3.0 태그와 9월 21일 갱신 표시 | 가방·장비·제작·요리 시험 재료를 수동으로 준비하는 보조 도구 |
| the cheat mod english version / limitless33, Captain_Stupid | F11 메뉴, God mode, noclip, 아이템·엔티티 생성, 맵 순간이동, 펫 특성 등. 공개 파일 1.6.9 및 1.1.1.1 이하 태그. 제작진의 유지보수 담당자 모집, 현행 버전 작동 실패·실행 실패 보고 존재 | 기능은 넓지만 현재 시험의 기본 도구로 권하지 않음 |

추천은 **ChatCommands + 그 현행 의존성(CoreLib), 필요하면 Item Browser 추가**다. 시험 환경의 재료·적·지형 준비부터 기존 모드를 시도할 근거가 충분하다. 현행 게임 1.3.0.7과 AIPlayerSpike를 함께 실행한 호환성은 아직 확인되지 않았다.

ChatCommands의 기능 목록은 제작자의 구 Thunderstore 배포 설명에서 확인했다. 현행 mod.io 목록에서 같은 제작자의 1.3.0 대응 배포 존재와 CoreLib 필요를 확인했지만, 개별 mod.io 페이지는 웹 도구에 JavaScript 안내만 반환했다. 브라우저 보강 확인도 브라우저 호스트 연결 불가로 수행하지 못했다. 따라서 현행 파일의 정확한 전체 의존성·최신 명령 구문·세부 동작은 설치 시 파일/배포 페이지와 게임 내 도움말로 확인해야 한다. 오래된 Thunderstore BepInEx 설치 안내를 현행 모드 설치법으로 사용하지 않는다.

## 이 프로젝트에 적용할 때의 경계

- ChatCommands의 플레이어 대상 회복·스킬·무적 명령, Item Browser의 인벤토리 지급이 AIPlayerSpike가 만든 AI 캐릭터에도 직접 적용된다는 근거는 없다. 사용자 플레이어에게 아이템을 지급해 시험장/상자에 배치하는 용도로는 후보가 되지만, AI별 상태·가방 자동 설정까지 해결한다고 단정하지 않는다.
- 시험 준비가 끝나면 noclip, 적 수동 행동, 시야 확장 등 시험 결과를 바꾸는 옵션을 정상값으로 돌려 실제 행동을 검증한다. 특히 이동·전투·채집 검증에는 원래의 충돌·적 행동·회수 조건이 필요하다.
- 새 치트 모드를 만들 필요가 확정된 상황은 아니다. 기존 모드로 시험장을 준비하고, 실제 AI 몸을 대상으로 반복 설정하는 기능이 부족하다고 확인될 때 그 부분만 별도 도구로 판단한다.

## 출처

- [현행 ChatCommands](https://mod.io/g/corekeeper/m/chatcommands3)
- [현행 mod.io 목록: ChatCommands·Item Browser의 1.3.0 태그와 갱신 정보](https://mod.io/g/corekeeper/m?_limit=20&_sort=-ratings_weighted_aggregate&revenue_type=3)
- [제작자의 ChatCommands 구 배포 기능 목록](https://thunderstore.io/c/core-keeper/p/kremnev8/ChatCommands/)
- [커뮤니티 문서: ChatCommands와 CoreLib.Commands 구분, 채팅창에서 명령 실행](https://github.com/CoreKeeperMods/Core-Keeper-Docs/blob/main/playing-with-mods/troubleshooting.md)
- [Item Browser mod.io](https://mod.io/g/corekeeper/m/itembrowser)
- [Item Browser 제작자 Steam 배포 설명·권한·단축키·게임 버전](https://steamcommunity.com/sharedfiles/filedetails/3673289586)
- [Item Browser 소스 저장소](https://github.com/moorowl/ItemBrowser)
- [Item Browser 제작자 소개](https://www.reddit.com/r/CoreKeeperGame/comments/1sbigj7/i_made_a_mod_that_lets_you_easily_see_all_items/)
- [구 치트 메뉴 모드 설명·유지보수 모집·실패 보고](https://mod.io/g/corekeeper/m/the-cheat-mod-english-version)
- [게임 제작사 공식 설치 문서: mod.io 구독 및 수동 설치](https://github.com/Pugstorm/CoreKeeperModDocs/blob/main/modding-documentation/playing-with-mods/installing-mods.md)

Steam Workshop의 비로그인 응답에는 제거됨·호환 안 됨 문구가 내용과 함께 나타났다. Workshop의 실제 이용 가능 상태를 확정하지 않았으며 설치 경로로는 mod.io를 우선한다.
