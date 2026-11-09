# 현재 메뉴 노드 표본

고정판 741532fcc57ffc549ac155a8261f5e25c966902f의 단독 메뉴 역할 어댑터348/0 표본이다. 실제 menu/inventory 소스와 대역 World/큐·수동 결과를 사용했다. 실제 외부 모델 호출, 연결 Loop 전체 trace, 게임 소비자 성공 또는 최종 수용을 뜻하지 않는다. 원본 전체 payload·fixture·로그는 implementation-evidence.json의 해시로 고정한다.

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
| category:착용 장비 | 3 | 0 |
| item:40 | 3 | 0 |
| wearing role before preset switch | 3 | 0 |
| selected role changed, recovered item parent | 3 | 0 |
| enter | 1 | 0 |
| category:무기 | 3 | 0 |
| item:12 | 3 | 0 |
| bag item before unrelated preset switch | 4 | 0 |
|  | 4 | 0 |
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

### more than fourteen exact items

```json
{
  "sample": "more than fourteen exact items",
  "input": "가방 선택 메뉴: 인벤토리 → 착용 장비. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 가방 선택 단계: 인벤토리 → 착용 장비",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "핫바 0 · Ring (변형 0, 상태 100, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:0",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "핫바 1 · Ring (변형 1, 상태 99, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:1",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "핫바 2 · Ring (변형 2, 상태 98, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:2",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "핫바 3 · Ring (변형 3, 상태 97, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:3",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c5",
      "kind": "inventory_menu",
      "desc": "핫바 4 · Ring (변형 4, 상태 96, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:4",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c6",
      "kind": "inventory_menu",
      "desc": "핫바 5 · Ring (변형 5, 상태 95, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:5",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c7",
      "kind": "inventory_menu",
      "desc": "핫바 6 · Ring (변형 6, 상태 94, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:6",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c8",
      "kind": "inventory_menu",
      "desc": "핫바 7 · Ring (변형 7, 상태 93, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:7",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c9",
      "kind": "inventory_menu",
      "desc": "핫바 8 · Ring (변형 8, 상태 92, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:8",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c10",
      "kind": "inventory_menu",
      "desc": "핫바 9 · Ring (변형 9, 상태 91, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:9",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c11",
      "kind": "inventory_menu",
      "desc": "가방 10 · Ring (변형 10, 상태 90, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:10",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c12",
      "kind": "inventory_menu",
      "desc": "가방 11 · Ring (변형 11, 상태 89, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:11",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c13",
      "kind": "inventory_menu",
      "desc": "가방 12 · Ring (변형 12, 상태 88, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:12",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c14",
      "kind": "inventory_menu",
      "desc": "가방 13 · Ring (변형 13, 상태 87, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:13",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c15",
      "kind": "inventory_menu",
      "desc": "가방 14 · Ring (변형 14, 상태 86, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:14",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c16",
      "kind": "inventory_menu",
      "desc": "가방 15 · Ring (변형 15, 상태 85, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:15",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c17",
      "kind": "inventory_menu",
      "desc": "가방 16 · Ring (변형 16, 상태 84, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:16",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c18",
      "kind": "inventory_menu",
      "desc": "가방 17 · Ring (변형 17, 상태 83, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:17",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c19",
      "kind": "inventory_menu",
      "desc": "가방 18 · Ring (변형 18, 상태 82, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:18",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c20",
      "kind": "inventory_menu",
      "desc": "가방 19 · Ring (변형 19, 상태 81, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:19",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c21",
      "kind": "inventory_menu",
      "desc": "가방 20 · Ring (변형 20, 상태 80, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:20",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c22",
      "kind": "inventory_menu",
      "desc": "가방 21 · Ring (변형 21, 상태 79, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:21",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c23",
      "kind": "inventory_menu",
      "desc": "가방 22 · Ring (변형 22, 상태 78, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:22",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c24",
      "kind": "inventory_menu",
      "desc": "가방 23 · Ring (변형 23, 상태 77, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:23",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c25",
      "kind": "inventory_menu",
      "desc": "가방 24 · Ring (변형 24, 상태 76, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:24",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c26",
      "kind": "inventory_menu",
      "desc": "현재 착용 head 슬롯 40 · CopperHelm (변형 0, 상태 20, 개수 1) · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "item:40",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c27",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c28",
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

### all thirty-six destination slots

```json
{
  "sample": "all thirty-six destination slots",
  "input": "가방 선택 메뉴: 인벤토리 → 재료·기타 → 슬롯 0 Wood → 자기 상자 → 자기 상자에 보관 목적 슬롯. 현재 단계 자식만 선택하며 뒤로/전체 취소 가능. 자기 상자를 실제로 열어 관측함. 현재 슬롯에서 다음 선택",
  "inventoryRequests": 0,
  "choices": [
    {
      "value": "c1",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 0 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:0",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c2",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 1 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:1",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c3",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 2 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:2",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c4",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 3 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:3",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c5",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 4 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:4",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c6",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 5 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:5",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c7",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 6 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:6",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c8",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 7 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:7",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c9",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 8 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:8",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c10",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 9 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:9",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c11",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 10 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:10",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c12",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 11 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:11",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c13",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 12 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:12",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c14",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 13 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:13",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c15",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 14 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:14",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c16",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 15 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:15",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c17",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 16 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:16",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c18",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 17 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:17",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c19",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 18 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:18",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c20",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 19 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:19",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c21",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 20 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:20",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c22",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 21 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:21",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c23",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 22 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:22",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c24",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 23 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:23",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c25",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 24 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:24",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c26",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 25 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:25",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c27",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 26 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:26",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c28",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 27 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:27",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c29",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 28 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:28",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c30",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 29 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:29",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c31",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 30 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:30",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c32",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 31 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:31",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c33",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 32 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:32",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c34",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 33 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:33",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c35",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 34 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:34",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c36",
      "kind": "inventory_menu",
      "desc": "목적 슬롯 선택 — 보관 Wood(변형 0, 상태 30, 개수 30) 1개, 출발 슬롯 0 → 목적 슬롯 35 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "destination:2:35",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c37",
      "kind": "inventory_menu",
      "desc": "이전 선택 단계로 돌아가기 · 선택 단계만 변경, 물품/몸 동작/목표는 변경하지 않음",
      "action": "back",
      "leaf": false,
      "itemId": 0,
      "itemVariation": 0,
      "itemCount": 0
    },
    {
      "value": "c38",
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
