> **소스 반영 완료:** 공식 ai-character HEAD는39b22df입니다. [반영 전후 대조](source-integration.md)에서 기존 환경·미추적 파일·설치·bridge 보존을 확인했습니다. 설치·게임·외부 모델 실행은 하지 않았습니다.

> 최신 통합 후보는 **39b22df96dffe869ab0ccd474b18d31462b71d70**입니다. c4 생활 트리와 가방 트리를 함께 보존한 [현재 통합 결과](integration-runtime-plan.md)를 확인하세요. SDK 후보와 실행 계획은 [통합 런타임 계획](integration-runtime-plan.md)에 있습니다. 아래741 기록은 **변경하지 않는 중간판의 역사 근거**이며 현재 설치 대상으로 사용하지 않습니다.
# 트리 검증판 실게임 재개 준비

아래 역사 기록의 중간 후보는 `741532fcc57ffc549ac155a8261f5e25c966902f`이며 실게임·외부 모델·설치는 기존 보류를 유지한다. 이 문서는 승인 뒤 수행할 구체 실행안이다.

## 후보·설치·백업

- 검토 가능한 정상 SDK 후보: [AIPlayerSpike-tree-741532f.zip](AIPlayerSpike-tree-741532f.zip). ZIP SHA/파일 크기와 bridge 후보2개는 [tree-package.json](tree-package.json)에 있다. ZIP은 `AIPlayerSpike` 폴더 아래101 C#와 manifest를 담는다.
- 설치 대상: `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`. [읽기 전용 사전 관측](runtime-preflight.json)과 [후보 생성 후 비교](tree-artifact-manifest.json)는 기존96파일이 변하지 않았음을 확인했다.
- 새 상위 do/keep/slot을 쓰는 자연어 계획 시험은 `tree-741532f-bridge/brief.md`·`protocol.mjs`와 실제 사용하는 상위 bridge 버전도 일치해야 한다. 두 소스는 후보 복사만 했으며 현재 bridge를 배포/재시작하거나 비밀 설정을 복사하지 않았다. 승인 후 HEAD가 공식 source 반영·해당 bridge 실행판을 조정한다.
- 변경 직전 현재 설치와 JSON에 탐지된 LocalLow `Core Keeper/AIPlayers`·`Steam`, Steam userdata의 이 게임 `1621690/remote`를 `runtime-backup-before-741532f-YYYYMMDD-HHMMSS` 새 폴더에 복사하고 해시를 확인한다. 현재 시점에는 백업/설치/복원을 수행하지 않았다. 기존 백업을 덮지 않는다.
- 새 `BagEquipment-741532f` 월드와 `BagTest-741532f` 플레이어를 사용한다. 기존 사용자 월드/캐릭터를 시험 대상으로 쓰거나 삭제·자동 종료하지 않는다. 승인 당시 실제 설치 내용이 사전 관측과 다르면 실제 대상을 다시 읽고 변경된 조건을 먼저 확인한다.

## 실제 확인 순서

1. 시험 물품으로 변형/상태가 다른 검 두 개, 새/기존 흉갑, 곡괭이, 나무, 자기 소유 상자를 준비한다. 실제 주가방·핫바·착용 슬롯과 상태를 시작 기록으로 남긴다. 시야·소유권 밖 물품이나 원본 DB 전체를 보유 목록으로 제공하지 않는다.
2. 자연어 부탁으로 시작해 상위 계획/원래 부탁 → 실제 inventory 진입 → 보유 분류 → 물품 → 뒤로 → 다른 물품 → 조작 → 필요한 목적 슬롯/수량 → 최종 실행을 같은 요청에서 잇는다. 모델 요청 JSON/선택값/메뉴 path·revision/최종 후보/실제 UIActionBuffer/전후 슬롯을 기록한다.
3. leaf 전에는 물품 요청이0이고 현재 유효 몸 동작이 계속되는지, 마지막 선택 뒤 정상 Swap이1회 발생하는지 확인한다. 실제 게임 소비자가 교환한 양 슬롯·착용/EquippedObject를 확인한 뒤에만 성공을 기록한다. 메뉴 전체 취소는 원래 부탁·목표·진행 skill을 중단하지 않아야 한다.
4. 상자 경로는 열기 전 현재 내용을 보여주지 않는지, 열어본 뒤 그 물품을 분류로 찾고 필요한 수량만 넣고 꺼내는지 확인한다. full9999 상자와 남은1자리, 비중첩 장비 상태 보존을 확인한다. 표시된 자기 상자만 사용한다.
5. 8/14를 넘는 같은 분류 보유 물품/여러 유효 목적 슬롯과 일반 world 후보를 실제 생성해 model choices까지 누락되지 않는지 본다. 분류가 비거나 물품·활성 착용 역할이 바뀌는 경우 변경 사유/부모 복구, 오래된 답의 미실행을 확인한다.
6. 메뉴 단계별 취소와 inspect-await 취소에서 큐·몸·목표·원래 부탁을 비교한다. 이전 ordinary 응답·미리 선택한 동작이 새 menu enter/back/cancel 뒤 뒤늦게 시작하지 않는지 확인한다.
7. 내려놓기는 출발 감소와 이번 바닥 생성, 전달은 지정 대상 관측·실제 바닥 감소/소멸·수령자 같은 양 증가를 연결한다. 수령자 미확인·타인 선점·일부 수령은 사실대로 남기고 재전송하지 않는다.
8. 대표 생활 흐름은 자기 상자에서 물품/장비를 정한 만큼 꺼냄 → 선택 장비로 교체 → 고른 검/곡괭이로 행동 → 남은 물품·구 장비 보관 → 원래 부탁 계속하기다. 단계 완료는 모델의 finish_goal, 부탁 완료는 상위 order=done과 구별한다.

각 흐름의 navigation/실행 없는 모델 호출 수, 첫 요청부터 leaf/실제 결과까지 시간, 입력 토큰/비용, 기존 rate/cost cap 때문에 대기한 시간을 기록한다. 시험 편의를 위해 예산 상한을 높이지 않는다. 로컬 대체 소비자나 수동 입력 시험을 자연어·실제 모델·게임 소비자 확인으로 대신 기록하지 않는다.
