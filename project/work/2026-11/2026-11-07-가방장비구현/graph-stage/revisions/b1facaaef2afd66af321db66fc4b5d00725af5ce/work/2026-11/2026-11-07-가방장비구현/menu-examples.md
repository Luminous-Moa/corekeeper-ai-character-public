# 현재 메뉴 노드 표본

고정 체크포인트 b1facaaef2afd66af321db66fc4b5d00725af5ce의 단독 메뉴 어댑터 표본이다. 실제 menu/inventory 소스와 대역 World/큐·수동 결과를 사용했다. 실제 외부 모델 호출, 연결 Loop 전체 trace, 게임 소비자 실행 또는 최종 수용을 뜻하지 않는다. 원본 전체 payload와 로그는 implementation-evidence.json에 SHA-256로 고정되어 있다.

| 표본 | 현재 자식 수 | 인벤토리 요청 수 |
|---|---:|---:|
| root | 1 | 0 |
| enter | 1 | 0 |
| all possessed categories | 4 | 0 |
| category:착용 장비 | 4 | 0 |
| more than fourteen exact items | 28 | 0 |
| item:0 | 28 | 0 |
| back | 4 | 0 |
| item:24 | 28 | 0 |
| cancel | 4 | 0 |
| enter | 1 | 0 |
|  | 5 | 0 |
| category:무기 | 5 | 0 |
|  | 4 | 0 |
| back | 4 | 0 |
| category:기타·분류 불명 | 5 | 0 |
|  | 3 | 0 |
| cancel | 3 | 0 |
| enter | 1 | 0 |
| category:무기 | 4 | 0 |
| item:12 | 3 | 0 |
| operation:hold | 4 | 0 |
| all physical hotbar destinations | 12 | 0 |
| destination:1:5 | 12 | 0 |
| execute | 3 | 0 |
| enter | 1 | 0 |
| category:무기 | 3 | 0 |
| item:12 | 3 | 0 |
| operation:drop | 4 | 0 |
| execute | 3 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 4 | 0 |
| item:0 | 3 | 0 |
| operation:store | 4 | 0 |
| chest:1:0 | 3 | 0 |
| execute | 3 | 0 |
| all thirty-six destination slots | 38 | 0 |
| destination:2:0 | 38 | 0 |
| short quantity paths | 6 | 0 |
| custom-quantity | 6 | 0 |
| digit:2 | 12 | 0 |
| digit:3 | 14 | 0 |
| number-confirm | 4 | 0 |
| execute | 3 | 0 |
| enter | 1 | 0 |
| own-chests | 4 | 0 |
| unopened chest list | 3 | 0 |
| chest:1:0 | 3 | 0 |
| execute | 3 | 0 |
| category:재료·기타 | 3 | 0 |
| observed chest items | 3 | 0 |
| back | 3 | 0 |
| back | 3 | 0 |
|  | 3 | 0 |
| cancel | 3 | 0 |
| enter | 1 | 0 |
| category:무기 | 3 | 0 |
| item:12 | 3 | 0 |
|  | 4 | 0 |
|  | 3 | 0 |
|  | 3 | 0 |
| back | 3 | 0 |
| cancel | 3 | 0 |
| enter | 1 | 0 |
|  | 3 | 0 |
| enter | 1 | 0 |
| cancel | 5 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 5 | 0 |
| cancel | 3 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 5 | 0 |
| item:0 | 3 | 0 |
| cancel | 5 | 0 |
| enter | 1 | 0 |
| category:무기 | 5 | 0 |
| item:12 | 3 | 0 |
| operation:hold | 6 | 0 |
| cancel | 12 | 0 |
| enter | 1 | 0 |
| category:무기 | 5 | 0 |
| item:12 | 3 | 0 |
| operation:hold | 6 | 0 |
| destination:1:0 | 12 | 0 |
| cancel | 3 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 5 | 0 |
| item:0 | 3 | 0 |
| operation:drop | 5 | 0 |
| cancel | 5 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 5 | 0 |
| item:0 | 3 | 0 |
| operation:drop | 5 | 0 |
| custom-quantity | 5 | 0 |
| cancel | 12 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 5 | 0 |
| item:0 | 3 | 0 |
| operation:store | 5 | 0 |
| cancel | 3 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 5 | 0 |
| item:0 | 3 | 0 |
| operation:store | 5 | 0 |
| chest:1:0 | 3 | 0 |
| cancel | 3 | 0 |
| enter | 1 | 0 |
| category:재료·기타 | 5 | 0 |
| item:0 | 3 | 0 |
| operation:give | 5 | 0 |
| cancel | 3 | 0 |

## 원문 표본

### root

```json
{
  "sample": "root",
  "input": "가방 선택 메뉴: 닫힘. 인벤토리 진입에서 실제 보유 물품의 분류부터 선택. 목표/캐릭터/월드 변경으로 이전 가방 선택을 닫음. 몸 동작과 부탁은 메뉴가 변경하지 않음",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "인벤토리에서 물품 분류와 가능한 조작 고르기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "enter",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### enter

```json
{
  "sample": "enter",
  "input": "가방 선택 메뉴: 닫힘. 인벤토리 진입에서 실제 보유 물품의 분류부터 선택. 목표/캐릭터/월드 변경으로 이전 가방 선택을 닫음. 몸 동작과 부탁은 메뉴가 변경하지 않음",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "인벤토리에서 물품 분류와 가능한 조작 고르기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "enter",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### all possessed categories

```json
{
  "sample": "all possessed categories",
  "input": "가방 선택 메뉴: 인벤토리. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "분류 무기 — 실제 보유 슬롯 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "category:무기",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "분류 착용 장비 — 실제 보유 슬롯 26개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "category:착용 장비",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### back

```json
{
  "sample": "back",
  "input": "가방 선택 메뉴: 인벤토리 → 착용 장비 → 슬롯 0 Ring. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 착용 장비 → 슬롯 0 Ring",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "조작 착용·교체 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "operation:equip",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "조작 바닥에 내려놓기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "operation:drop",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### cancel

```json
{
  "sample": "cancel",
  "input": "가방 선택 메뉴: 인벤토리 → 착용 장비 → 슬롯 24 Ring. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 착용 장비 → 슬롯 24 Ring",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "조작 착용·교체 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "operation:equip",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "조작 바닥에 내려놓기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "operation:drop",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### back

```json
{
  "sample": "back",
  "input": "가방 선택 메뉴: 인벤토리 → 무기. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 무기",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "핫바 0 · CopperSword (변형 0, 상태 0, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:0",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "핫바 2 · 778 (변형 0, 상태 100, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:2",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### cancel

```json
{
  "sample": "cancel",
  "input": "가방 선택 메뉴: 인벤토리 → 기타·분류 불명. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 기타·분류 불명",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "핫바 1 · 777 (변형 7, 원시 상태 44, 수량/상태 불명) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:1",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### short quantity paths

```json
{
  "sample": "short quantity paths",
  "input": "가방 선택 메뉴: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 자기 상자에 보관 목적 슬롯 → 수량. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 자기 상자에 보관 목적 슬롯 → 수량",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "수량 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "quantity:1",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "현재 부탁 잔량 23개 중 이 슬롯에서 가능한 23개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "quantity:23",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "이 물품/목적 슬롯에서 현재 가능한 전량 30개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "quantity:30",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "다른 정확한 수량 입력 — 1~30, 각 자리 선택 후 확정 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "custom-quantity",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c5",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c6",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### custom-quantity

```json
{
  "sample": "custom-quantity",
  "input": "가방 선택 메뉴: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 자기 상자에 보관 목적 슬롯 → 수량. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 자기 상자에 보관 목적 슬롯 → 수량",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "수량 1개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "quantity:1",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "현재 부탁 잔량 23개 중 이 슬롯에서 가능한 23개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "quantity:23",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "이 물품/목적 슬롯에서 현재 가능한 전량 30개 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "quantity:30",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "다른 정확한 수량 입력 — 1~30, 각 자리 선택 후 확정 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "custom-quantity",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c5",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c6",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```

### back

```json
{
  "sample": "back",
  "input": "가방 선택 메뉴: 인벤토리 → 자기 상자 → 재료·기타. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 자기 상자 → 재료·기타",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "자기 상자 chest:1,0 슬롯 0 · Wood (변형 0, 상태 50, 개수 50) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:0",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "가방 선택 전체 취소 — 원래 부탁과 진행 중 몸 동작은 유지 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "cancel",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    }
  ]
}
```
