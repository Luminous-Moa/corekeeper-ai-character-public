# 실제 선택 경로 연결 표본

같은 통합판 39b22df96dffe869ab0ccd474b18d31462b71d70의 실제 AILife·가방 메뉴·공개surface/BuildJson·Apply·등록·InventorySkill을 연결한 192/0 검사다. 로컬 요청 32개/nav 30개이며 실제 외부모델·게임소비자는0이다. 분야별 farm/cook 경계,World/DB/네트워크·소비상태는 대역/수동공급이므로 완전한 스케줄링/플레이 QA로 확대하지 않는다. source-pin 14개 원본의 rawhash와 Gitblob 텍스트 대응을 별도 기록했다.

생활 lossless paging/읽기전용 대기 선택과 가방 무페이지/새선택 권한 처리, 작업 수량/Runs/진척 보존은 integration-agreement의 다른 소비자 계약이다.

| 요청 | 선택 | nav | 선택지수 | 큐 | 몸 | goal 보존 | order |
|---:|---|---|---:|---:|---|---|---|
| 1 | enter | true | 5 | 0 | existing | true | 따름 |
| 2 | category:무기 | true | 8 | 0 | existing | true | 따름 |
| 3 | item:12 | true | 8 | 0 | existing | true | 따름 |
| 4 | back | true | 8 | 0 | existing | true | 따름 |
| 5 | item:13 | true | 8 | 0 | existing | true | 따름 |
| 6 | operation:hold | true | 8 | 0 | existing | true | 따름 |
| 7 | destination:1:5 | true | 16 | 0 | existing | true | 따름 |
| 8 | execute | false | 7 | 0 | inventory:hold:1:13:1:5:1:3:2:0:300:0 | true | 따름 |
| 9 | enter | true | 5 | 0 | existing | true | 따름 |
| 10 | category:무기 | true | 7 | 0 | existing | true | 따름 |
| 11 | item:12 | true | 7 | 0 | existing | true | 따름 |
| 12 | cancel | true | 8 | 0 | existing | true | 따름 |
| 13 | enter | true | 5 | 0 | existing | true | 따름 |
| 14 | category:재료·기타 | true | 8 | 0 | existing | true | 따름 |
| 15 | item:0 | true | 7 | 0 | existing | true | 따름 |
| 16 | operation:store | true | 8 | 0 | existing | true | 따름 |
| 17 | chest:1:0 | true | 7 | 0 | existing | true | 따름 |
| 18 | execute | false | 7 | 0 | inventory:inspect:2 | true | 따름 |
| 19 | cancel | true | 6 | 0 | inventory:inspect:2 | true | 따름 |
| 20 | enter | true | 5 | 0 | existing | true | 따름 |
| 21 | category:무기 | true | 7 | 0 | existing | true | 따름 |
| 22 | item:12 | true | 7 | 0 | existing | true | 따름 |
| 23 | operation:hold | true | 8 | 0 | existing | true | 따름 |
| 24 | destination:1:5 | true | 16 | 0 | existing | true | 따름 |
| 25 | cancel | true | 7 | 0 | existing | true | 따름 |
| 26 | enter | true | 5 | 0 | existing | true | 따름 |
| 27 | cancel | true | 7 | 0 | existing | true | 따름 |
| 28 | enter | true | 5 | 0 | existing | true | 따름 |
| 29 | cancel | true | 7 | 0 | existing | true | 따름 |
| 30 | enter | true | 5 | 0 | existing | true | 따름 |
| 31 | cancel | true | 7 | 0 | existing | true | 따름 |
| 32 | cancel | true | 7 | 0 | existing | true | 따름 |

## 로컬 요청 원문

### 1 / enter

```json
{
  "decision": 1,
  "action": "enter",
  "value": "c5",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 닫힘. 인벤토리 진입에서 실제 보유 물품의 분류부터 선택. 목표/캐릭터/월드 변경으로 이전 가방 선택을 닫음. 몸 동작과 부탁은 메뉴가 변경하지 않음",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 닫힘. 인벤토리 진입에서 실제 보유 물품의 분류부터 선택. 목표/캐릭터/월드 변경으로 이전 가방 선택을 닫음. 몸 동작과 부탁은 메뉴가 변경하지 않음",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "인벤토리에서 물품 분류와 가능한 조작 고르기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 2 / category:무기

```json
{
  "decision": 2,
  "action": "category:무기",
  "value": "c5",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "분류 무기 — 실제 보유 슬롯 2개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "분류 재료·기타 — 실제 보유 슬롯 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c8",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 3 / item:12

```json
{
  "decision": 3,
  "action": "item:12",
  "value": "c5",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "가방 12 · CopperSword (변형 1, 상태 100, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "가방 13 · CopperSword (변형 2, 상태 300, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c8",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 4 / back

```json
{
  "decision": 4,
  "action": "back",
  "value": "c7",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 12 CopperSword. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 12 CopperSword",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 12 CopperSword. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 12 CopperSword",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "조작 손에 들기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "조작 바닥에 내려놓기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c8",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 8 / execute

```json
{
  "decision": 8,
  "action": "execute",
  "value": "c5",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 13 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 13 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 13 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 13 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "실행 확인 — hold CopperSword(변형 2, 상태 300, 개수 1) 슬롯 13 → hotbar:5(5), 이전 Wood(변형 0, 상태 20, 개수 20) → 슬롯 13. 실제 양 슬롯/장비 상태 변화를 확인함."
          },
          {
            "value": "c6",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": false,
  "queuedInventoryRequests": 0,
  "body": "inventory:hold:1:13:1:5:1:3:2:0:300:0",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 12 / cancel

```json
{
  "decision": 12,
  "action": "cancel",
  "value": "c8",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Equip CopperHelm. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 12 CopperSword. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 12 CopperSword",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Equip CopperHelm. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 12 CopperSword. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 12 CopperSword",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "조작 손에 들기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "조작 바닥에 내려놓기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c8",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 18 / execute

```json
{
  "decision": 18,
  "action": "execute",
  "value": "c5",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Store Wood. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 상자 1,0 접근/열기. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 상자 1,0 접근/열기",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Store Wood. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 상자 1,0 접근/열기. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 상자 1,0 접근/열기",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "실행 확인 — 자기 상자 chest:1,0에 접근해 열어 살펴보기. 내용을 본 뒤 물품을 고름. 내용 관측 후 다음 행동은 다시 고름."
          },
          {
            "value": "c6",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": false,
  "queuedInventoryRequests": 0,
  "body": "inventory:inspect:2",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 19 / cancel

```json
{
  "decision": 19,
  "action": "cancel",
  "value": "c6",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Store Wood. 현재 몸: inventory:inspect:2. 가방 선택 메뉴: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 상자 1,0 접근/열기. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 기억한 상자에 도착함. 내용은 아직 공개하지 않고 열어보기 선택을 기다림",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Store Wood. 현재 몸: inventory:inspect:2. 가방 선택 메뉴: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 상자 1,0 접근/열기. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 기억한 상자에 도착함. 내용은 아직 공개하지 않고 열어보기 선택을 기다림",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "inventory:inspect:2",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 25 / cancel

```json
{
  "decision": 25,
  "action": "cancel",
  "value": "c7",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 12 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 12 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Hold CopperSword. 현재 몸: existing. 가방 선택 메뉴: 인벤토리 → 무기 → 슬롯 12 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기 → 슬롯 12 CopperSword → 손에 들기 목적 슬롯 → 실행 확인 1개",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "실행 확인 — hold CopperSword(변형 0, 상태 100, 개수 1) 슬롯 12 → hotbar:5(5), 이전 빈칸 → 슬롯 12. 실제 양 슬롯/장비 상태 변화를 확인함."
          },
          {
            "value": "c6",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 27 / cancel

```json
{
  "decision": 27,
  "action": "cancel",
  "value": "c7",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "분류 무기 — 실제 보유 슬롯 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 29 / cancel

```json
{
  "decision": 29,
  "action": "cancel",
  "value": "c7",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "분류 무기 — 실제 보유 슬롯 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 31 / cancel

```json
{
  "decision": 31,
  "action": "cancel",
  "value": "c7",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "분류 무기 — 실제 보유 슬롯 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

### 32 / cancel

```json
{
  "decision": 32,
  "action": "cancel",
  "value": "c7",
  "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
  "requestJson": {
    "model": "gpt-6-luna",
    "input": "원래 부탁: 정확한 장비를 갖추고 원래 보급 부탁을 이어가. 현재 목표: Farm None. 현재 몸: existing. 가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
    "questions": [
      {
        "type": "choice",
        "name": "action",
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 생활 목록 조회는 현재 몸 행동·대기 선택·목표를 유지한다. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
        "choices": [
          {
            "value": "c1",
            "description": "현재 몸 동작 계속"
          },
          {
            "value": "c2",
            "description": "몸 동작만 중단"
          },
          {
            "value": "c3",
            "description": "현재 단계 완료"
          },
          {
            "value": "c4",
            "description": "상위 재계획"
          },
          {
            "value": "c5",
            "description": "분류 무기 — 실제 보유 슬롯 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c6",
            "description": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          },
          {
            "value": "c7",
            "description": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음"
          }
        ]
      }
    ]
  },
  "navigation": true,
  "queuedInventoryRequests": 0,
  "body": "existing",
  "goalUnchanged": true,
  "orderState": "따름"
}
```

## Fixture가 공급한 결과 이벤트

```json
[
  {
    "eventName": "manual consumer buffer updates",
    "requestCount": 1,
    "fromSlot": 13,
    "fromObject": "Wood",
    "toSlot": 5,
    "toObject": "CopperSword",
    "variation": 2,
    "skill": "Done",
    "goalUnchanged": true,
    "order": "따름"
  },
  {
    "eventName": "cross-domain ownership",
    "expiredRepliesRevived": false,
    "lifeRemaining": 2,
    "bodyUnchanged": true,
    "externalModelCalls": 0
  }
]
```
