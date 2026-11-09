# 원하는 동료 플레이와 현행 선택지의 차이

2026-11-07 기준. 제품 저장소 ai-character 11eb461의 후보 생성·조립·실행·상위 계획 경로와 보존된 실게임 로그를 대조했다. 4a 등 별도 개발 브랜치의 기능은 포함하지 않았다. 새 게임·모델 호출은 하지 않았다.

## 가장 먼저 끊기는 장면

구리 갑옷을 부탁받은 Gemini가 구리 10개를 확보하자 이미 시작한 다음 구리 광맥으로의 파기가 0.5초 만에 취소됐다. 중간 채집 완료와 함께 호스트의 갑옷 부탁까지 완료 상태로 기록됐다. 14.1초 뒤 위쪽 LLM이 용광로 설치·구리 작업대·갑옷 제작 계획을 제시하면서 원래 부탁이 다시 진행 상태가 됐다. 같은 코드에서 미리 짜 둔 계획에 다음 단계가 있으면 바로 이어 간다. 두 경우 모두 현장의 추가 채굴과 원계획 복귀를 비교하는 판단 시점은 없다. [실제 취소](<C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/Player.log:750>) · [상위 재계획](<C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/Player.log:773>) · [수량 판정](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIGoals.cs:288) · [상위 계획의 단계 종료](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs:319)

## 장면별 현 상태

- **목표 밖 자원과 탐색**. 채굴 후보는 목표와 일치하는 벽 최대 3개, 다른 벽은 일부 목표에서 거리순 최대 2개다. 채집 중에도 열린 방향으로 걷기와 다른 물건 줍기는 가능하다. 실제 구리 목표에서 주석 줍기도 제시됐다. 당장 진척 가능한 채굴·파기 후보가 있을 때에는 화면 밖을 향한 탐험 굴과 먼 미탐색 지점으로 가는 후보가 제한된다. 가까운 흙벽에 밀린 다른 광맥, 여유 있을 때 들를 곳, 계속 이동 중에 눈여겨볼 일을 비교할 기회가 제한된다. 평시 최종 14개 상한을 바꾸어도 생성되지 않은 후보는 나타나지 않는다. 현장 프롬프트에는 목표·진척·가방·체력 등이 있지만 상위 보고의 원래 부탁·계획 전체와 같지 않다. [후보 생성](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:332) · [탐험 조건](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Candidates.cs:648) · [현장 입력](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:1287) · [실제 주석 후보](<C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers/decisions-194122.log:22438>)
- **기억**. 광석·제작대 위치를 자동 관측한다. 실제로 Gemini는 13시간 전에 본 화면 밖 구리 위치를 선택해 경로 21칸을 따라 구리 1개를 얻었고, 광석이 사라진 것을 관측한 뒤 위치를 잊었다. 모델이 특별한 발견을 골라 이유를 붙이고 원래 일을 계속하는 선택은 현장 행동 목록에 없다. 특히 철광석은 상위 gather의 허용 목록에도 없으므로 발견을 기억해도 철 수집 목표까지 연결되지 않는다. [실제 선택과 획득](<C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/Player-prev.log:374>) · [공간 기억](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIMemory.cs:277) · [허용 품목](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlanner.cs:795)
- **전투 전환**. 적을 보고 근접 공격, 거리 벌리기, 후퇴, 먹기를 고를 수 있다. 일반 공격은 코드가 쓸 근접무기를 찾아 접근 후 타격한다. 무기별 교체·조준·원거리 발사 선택은 현행 행동 등록에 없다. 행동 결과 그래프가 장비 성질을 알아도, 모드에 원거리 무기 선택과 실제 공격 입력을 연결해야 한다. [실행 가능한 동작](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIActions.cs:18) · [공격 무기 선택](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBody.cs:694) · [근접 공격](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIAttackSkill.cs:111)
- **강화 귀환과 복귀**. 상위에는 기지 귀환과 제작이 있고, 현장에서 나무 채집 도중 실제로 곡괭이를 만들어 다시 채집한 기록도 있다. 일부 준비는 이미 수행된다. 반면 장비 강화 작업 자체는 상위 목표·현장 행동에 등록되어 있지 않다. 새 계획으로 전환할 때 원래 목적지·남은 수량·중단 이유를 보존해 재개하는 흐름은 전용 지원이 없다. 상위가 새 계획에 필요한 단계를 다시 명시할 수 있다. [실제 준비와 채집 재개](<C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers/decisions-202704.log:23>) · [지원 목표](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/Core/AIGoalKinds.cs:36) · [계획 교체](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/Core/AIPlanRules.cs:152)

별도 실게임 기록에서 가방의 나무검을 핫바로 옮긴 사례, 기지 안까지 후퇴하고 문을 닫은 사례, 모루를 놓고 구리검을 제작해 가방에 넣은 사례를 확인했다. 적이 많은 채집길에서 자발적으로 귀환해 장비를 강화하고 원래 위치에서 재개한 하나의 연속 사례는 확인하지 못했다. [근접무기 선택](C:/Dev/_scratch/base-s3r/collect/Player.log:702) · [기지 안 후퇴](C:/Dev/_scratch/base-s3r/collect/Player.log:1174) · [구리검 제작](C:/Dev/_scratch/place-s1d/collect/Player.log:444)

## 정비 우선순위 제안

1. 중간 목표량 달성과 사용자 부탁 완료를 따로 추적한다. 모델이 지금 더 캘지, 위치만 기억하고 다음 단계로 갈지 비교할 시점을 보존한다. 확인할 첫 장면은 구리 10개 이후의 한 번의 선택과 실제로 이어진 행동이다.
2. 현재 목표를 수행하는 동안 눈에 띈 자원·장소, 가방 여유와 위험, 원래 부탁의 수량 제한·시급함을 현장 선택에 반영한다. 관측·기억은 이미 있는 것을 활용하고, 실행 가능하며 목적이 다른 대안이 실제 후보 목록에 도달했는지 확인한다.
3. 무기 전환·원거리 사격·강화처럼 실행이 없는 행동에는 몸 입력과 결과 관측을 연결한다. 장비를 갖추려고 돌아가는 경우에는 원래 부탁·진척·장소를 보존해 다시 이어 갈 수 있어야 한다.
