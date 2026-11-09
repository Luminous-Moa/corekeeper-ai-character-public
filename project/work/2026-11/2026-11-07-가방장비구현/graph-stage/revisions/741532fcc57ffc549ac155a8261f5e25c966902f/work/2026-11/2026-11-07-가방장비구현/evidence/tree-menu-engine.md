# 가방·장비 선택 트리 엔진

[트리 합의](tree-agreement.md)와 [실행 명세](tree-spec.md)에 따라 `1fc89b9`의 정상 조작 primitive를 재사용해 메뉴 상태기를 구현했다. 소유 변경은 `AIInventoryMenu.cs`와 meta, `AIInventory.cs`이며 Loop/공통 실행/최종 직렬화 연결은 상위 실행 소유자가 맡는다. 이 보고서는 코드 동결 직전 모듈 결과이며 최종 고정 커밋·독립 검수·Unity·게임 성공의 판정이 아니다.

## 공개 연결점

- `AIInventoryMenu.Active`: 현재 캐릭터가 메뉴 안에 있는지.
- `Candidates(World, Entity, AIObservation, string currentKey)`: 비활성이면 인벤토리 진입 하나, 활성이면 현재 단계의 자식과 뒤로/전체 취소. 상한/페이지를 사용하지 않는다.
- `Stale(World, Entity, AICandidate)`: 메뉴 revision·현재 노드·목표/캐릭터/월드·실제 출발 물품/대상 조건을 대조한다. 오래된 답을 다른 노드에서 해석하지 않는다.
- `ApplyNavigation(World, Entity, AICandidate, out string note)`: `kind=inventory_menu`의 경로 변경만 처리한다. 물품 요청·몸 중단·목표 변경 API를 호출하지 않는다.
- `NoteLeafSelected(AICandidate)`: 정상 skill 시작 직전에 호출한다. 최종 조작은 메뉴를 닫으며, 접근/열어보기 leaf는 실제 관측 대기 상태로 남긴다. `inventoryMenu.IsLeaf`로 식별한다. 화면 밖 상자 접근은 기존 walk/dig 후보일 수 있으므로 kind가 inventory인 경우에만 한정하면 안 된다.
- `PromptLine()` 및 `(World,Entity)` overload, `Reset(reason)`.
- `AIInventoryMenuChoice`는 현재 revision/node/action, 다음 단계의 복사본 및 `IsLeaf`를 보존한다. 부모가 `AICandidate.inventoryMenu`를 추가해 기존 cN 응답 경로와 연결했다.

## 메뉴 의미

일반 판단의 인벤토리 진입은 실제 가방·핫바·현재 활성 착용 슬롯의 분류로 연결된다. 같은 노드에 자기 상자에서 꺼내기 진입도 둔다. 분류는 실제 `ObjectInfo.objectType`에 근거한 새 보유 메뉴 분류이며 native creative UI의 이름을 복제했다고 주장하지 않는다. 장비·무기(BeamWeapon 포함)·도구·사용 아이템·섭취물·설치물·생물·귀중품·특수 제작 부품·중요 물품·악기·재료/기타와 분류 불명 fallback을 사용한다.

보유 목록은 목표 item으로 필터하지 않고 objectID가 있는 실제 슬롯을 보존한다. amount 0의 고장 장비도 1개 보유로 남긴다. info가 없으면 원시 상태와 수량/상태 불명을 표시하며 실행 근거를 추정하지 않는다. 모델 문구에는 위치·종류·변형·상태·개수만 넣고, auxDataIndex/variationUpdateCount는 정확한 선택 스냅샷 내부에서만 사용한다.

물품을 고르면 현재 위치와 조건에서 검수된 기존 조작을 펼친다. 가방 물품은 착용/교체·들기·보관·내려놓기·전달, 착용 물품은 해제, 관측한 자기 상자 물품은 꺼내기로 연결한다. 종류·고장·슬롯 요구·소유권·거리 조건에 따라 실제로 만들 수 없는 조작은 leaf로 제시하지 않는다. 기존 eat는 정확 출발 슬롯을 받지 않으므로 selected-item eat를 추가하지 않았다. 기존 Loop의 먹기 대응은 상위 연결에서 유지한다.

목적 슬롯은 첫 fit을 확정하지 않고 모든 유효 슬롯을 전달한다. 수량은 1개·현재 부탁 잔량·가능 전량으로 흔한 흐름을 짧게 하고, 그 외 값은 직접 자리 선택/지우기/확정으로 모든 유효 정수에 접근한다. 범위는 실제 출발 수량·목적 슬롯 공간·현재 목표와 일치할 때의 reserve 조건으로 정하며 임의 수량 상한을 두지 않는다.

자기 상자는 기억한 소유 상자에서 고르되 내용은 실제 접근/열어보기 뒤에만 노출한다. 접근/inspect가 끝나면 같은 메뉴 경로에서 실제 상자의 분류와 슬롯을 고른다. 화면 밖 기억은 현재 내용이 아니라 가서 확인할 근거로만 쓴다.

## Primitive 연결과 목표 receipt

`AIInventory.MenuMoves`는 메뉴가 고른 실제 source/slot/mode와 인자를 받아 모든 유효 Swap/MoveAmount/DropItem leaf를 만든다. `MenuMaximum`은 실제 수량/공간을, `MenuGoal`은 현재 목표와 조작·품목·출발 슬롯·지정 대상이 일치하는지 확인한다. 다른 물품의 유효 준비 행동에는 goal을 묶지 않는다. 예를 들어 흉갑 목표 중 예비 검을 내려놓은 성공은 흉갑 완료가 아니다.

Leaf는 기존 `InventorySkill`과 양 슬롯/실제 손/바닥 실소비·지정 수령 증가의 관측을 그대로 사용한다. 물품 identity와 실제 상태가 바뀌면 선택을 다시 받으며, navigation은 이미 실행 중인 몸 동작을 멈추지 않는다. 실제 Loop의 prefetch/held/current/order 보존은 상위 연결 검증의 범위다.

기존 flat API는 과거 검사와 legacy 준비용으로 남기되 모델의 새 root surface에서는 부모가 제거한다. 그 API의 가방 8/12/20 및 상자12 cut, 해제 첫 빈 슬롯 break도 제거했다. 일반 world/assembler 쪽 상한 제거는 별도 소유자가 맡았다.

## 실제 소스 연결 검사

[tree-menu-adapter](tree-menu-adapter/TreeMenu.csproj)는 실제 `AIInventoryMenu.cs`, `AIInventory.cs`, Core 규칙을 직접 링크한다. [검사 코드](tree-menu-adapter/Program.cs)와 [로그](tree-menu-adapter/results.log), [현재 노드 payload](tree-menu-adapter/payloads.json), [읽기 쉬운 예시](tree-menu-payload-example.md)를 남겼다.

결과는 **326 checks / 0 failures**다. 95개의 선택 전이에서 fresh token·request queue 불변·동일 goal을 반복 확인하는 검사도 포함하므로 이를 서로 독립적인 326개 게임 시나리오로 세지 않는다. 주요 흐름은 다음과 같다.

- 현재 흉갑 목표에도 다른 보유 무기 분류 유지, 25개 ring과 착용 helm 26개를 전부 노출, 서로 다른 슬롯/변형/상태 구별.
- root→분류→물품→뒤로→다른 물품에서 큐 없음. 10개 단계의 전체 취소에서 원래 물품·goal·요청 큐 불변.
- 고장 장비·unknown info·BeamWeapon·넓은 Eatable 분류 유지.
- 점유칸을 포함한 핫바 10개와 자기 상자 목적 슬롯 36개를 전부 선택 가능. 기존 8/14 상한을 넘는 실제 자식 전달.
- hold 최종 leaf 전에는 요청 없음, 실제 Swap 한 번 뒤 두 슬롯과 실제 손 관측으로 완료.
- 현재 흉갑 목표와 다른 검 drop 성공이 현재 목표를 완료하지 않음.
- 상자 선택 전/열기 전 내용 미공개, 실제 inspect 뒤 해당 상자 슬롯 공개, 뒤로가 상자 선택으로 복귀.
- 수량 23의 요청 preset과 전량 접근, 직접 2→3→확정으로 같은 유효 수량 도달, 실제 source 감소/destination 증가 관측.
- 물품 상태 변경·뒤로 이후 토큰·새 goal의 이전 응답은 실행 없이 무효화.

### 호출 수 의미

여기서는 외부 모델을 호출하지 않았다. 정상적으로 매 응답이 다음 선택을 고르는 경우의 **선택 단계 수**는 hold 6회(내비 5+실행 leaf1), 비중첩 drop 5회(내비4+leaf1), 열지 않은 상자의 요청 수량 보관 preset 9회(내비7+접근/inspect leaf1+이동 leaf1)다. 직접 두 자리 수량은 같은 보관 흐름에서 12회로, preset보다 3개의 선택이 더 필요하다. 실제 시간·토큰·모델 실패율은 측정하지 않았다. Loop의 기존 호출/비용 cap을 올리지 않으며 실제 예산 제한 동작은 상위 연결 검사에서 확인한다.

World/DB/길찾기/요청 큐는 최소 대체 구현이고 버퍼 변화는 시험이 수동 공급한다. 이 모듈 검사에서 직접 만든 skill을 실행하므로 실제 Loop.Apply/정상 등록의 연결은 부모의 `tree-connected`에서 별도로 검증한다. 게임 소비자·실제 모델·실게임 성공을 이 결과로 대신하지 않는다.

## checkpoint 독립 검수 후 역할 binding 보완

독립 검수에서 물품 버퍼는 그대로인데 활성 preset의 head 위치가 40→60으로 바뀌면, 이전 head40 물품 선택 토큰을 stale로 구별하지 못하고 가능한 조작만 사라지는 경계를 발견했다. 선택 Frame에 그 물품의 source 역할(핫바/주 가방/현재 착용 gear name/관측 상자)을 보존하고 **선택된 슬롯의 현재 역할만** 대조하도록 보완했다. 프리셋 전체 서명을 사용하지 않으므로 가방 slot12를 탐색하는 동안 무관한 head 위치가 바뀌어도 메뉴를 무효화하지 않는다.

[role-results.log](tree-menu-adapter/role-results.log)의 실제 소스 어댑터는 348 checks/0 failures다. 기존 326개의 회귀에 두 역할 경계의 전이와 검사를 더한 수치다. head40→60 이전 토큰은 실제 변화 사유와 함께 물품 부모 단계로 돌아가고 현재 지원되는 head60 물품을 노출하며, 별도로 가방 무기 선택은 무관한 preset 변경 후에도 유효함을 확인했다. [tree-menu-role-compile.log](tree-menu-role-compile.log)는 경고0/오류0이다. 해당 source 수정은 `AIInventoryMenu.cs`에만 한정했고 primitive·호출·예산·후보 구조는 변경하지 않았다.
