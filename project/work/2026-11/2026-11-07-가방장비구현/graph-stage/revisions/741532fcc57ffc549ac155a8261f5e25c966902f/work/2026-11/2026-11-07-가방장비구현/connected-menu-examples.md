# 실제 선택 경로 연결 표본

고정판 741532fcc57ffc549ac155a8261f5e25c966902f의 실제 메뉴→공개surface/BuildJson→실제Apply/동작등록→InventorySkill 연결 검사102/102다. 로컬 Decisions 요청구성19회·navigation17회이며 외부 모델 호출은0이다. 실제 본문의 추출·직접 링크와 대역 World/DB/네트워크/소비상태 수동공급 범위를 구별한다. 이 기록은 완전한 런타임 Tick·Unity·게임 소비자·최종 수용의 근거가 아니다. fixture source-pin의11개 원본 해시와 Git blob 텍스트 대응은 connected-source-check.json에서 확인했다.

| 요청 | 선택 | nav | choices | 요청큐 | 몸 | goal 보존 | order |
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

## 로컬 요청 원문과 선택 상태

### 요청 1 / enter

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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

### 요청 2 / category:무기

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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

### 요청 3 / item:12

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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

### 요청 4 / back

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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

### 요청 8 / execute

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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

### 요청 12 / cancel

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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

### 요청 18 / execute

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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

### 요청 19 / cancel

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
        "instructions": "목표와 지금 상황에서 Gemini가 다음에 할 선택 하나를 고르시오. 몸 동작과 가방 메뉴 선택은 다르다. 인벤토리 진입·분류·물품·인자 선택·뒤로·선택 전체 취소는 선택 단계만 바꾸고 현재 몸 동작과 원래 부탁을 유지한다. 물품 조작은 필요한 인자를 고른 뒤 최종 실행 항목을 골랐을 때만 한다. 현재 상황과 위험을 함께 보고, 지금 하는 행동(계속하기)이 여전히 알맞으면 이어 간다. 새로운 몸 동작을 고르면 그 동작은 처음부터 시작한다.",
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
