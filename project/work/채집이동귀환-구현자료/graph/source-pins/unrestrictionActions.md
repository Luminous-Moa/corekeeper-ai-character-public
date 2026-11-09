# 손 선택·무기 입력·범용 정밀 전투 확장

작업 폴더 C:/Dev/CoreKeeperModSDK-unrestricted. 부모의 제약 전반 해제 작업과 공유하며 아래 소유 파일에 한정해 변경했다.

지원행동 소유 변경 commit: `541937f` (26파일). 같은 worktree의 타담당 미커밋 변경은 stage하지 않았다. 부모의 후보 연결 한 줄은 별도 통합 변경에 남는다.

독립 검수 P1 소환 의미 수정 후속 commit: `338a8a4` (8파일). 최종 입력 검사는43개이며 검수자가 재실행·원본 대조 후 해결로 수용했다.

## 실제 변경

- AIInventory는 모든 실제 main 인벤토리 물품을 핫바에 정상 Swap하여 손에 들 수 있다. 기존 MiningPick/MeleeWeapon gate와 broken물품을 손에도 못 들던 gate를 제거했다. 장착 슬롯 적합성·정상 Swap·양쪽 슬롯과 실제 장착 관측·공간/소유권 검사는 유지한다. 기본/base 상자 검색의 반경10 필터도 제거했다.
- AIHandCapabilities는 게임 ObjectType 원문의5개 Weapon 계열(Melee/Range/Summoning/Throwing/Beam)을 모두 공격 선택 대상으로, MiningPick/DrillTool/Sledge를 채굴 도구로 인식한다. 단순 물품을 손에 든 것과 그 물품을 무기/채굴 도구로 사용할 수 있는 것은 구분한다. AIBody의 hotbar·가방·선택한 물품 lease 경로를 같은 분류로 맞췄다.
- AttackSkill은 선택한 원거리 계열을 들고 관측한 적을 조준해 정상 BodyInput.primary를 유지한다. 근접1.3칸까지 강제 접근하지 않는다. 보조 사용은 실제 SecondaryUseCD.hasSecondaryUse/hasWindup/windupTiers가 있을 때만 별도 후보로 제공한다. 일반 최대 충전 보조는 실제 EquipmentSlotCD.atMaxWindup 관측 뒤 해제한다. 소환은 별도 `summon` 후보를 추가해 필요한 최소 tier가 충족된 미최대 충전 상태에서 해제한다. AIActions.Make가 secondary/summon을 실제 Skill에 전달한다.
- 투사체·소환·빔·소모품 소비·마나·cooldown·피해는 게임이 처리한다. hp 변화를 관측하되 새로운 원거리 입력의 피해를 공격자 귀속이 확인된 무기 통계로 기록하지 않는다. 표적이 시야에서 나가면 보이지 않는 현재 좌표로 발사하지 않는다. 3초간 피해변화가 없으면 비용/장애물/사거리 문제를 재판단할 관측을 남기며 자동 취소하지 않는다.
- AICombatOptions는 관측한 적 종류마다 실제 hit/approach/8방향 짧은 step을 제공한다. 공격 불가 손 상태에서는 hit/무기 사용을 생성하지 않고 이동은 유지한다. 거리무기는 기본 사용과 실제 지원하는 보조 사용을 제공한다. 짧은 이동은 대상 몸/관측된 장애물과 충돌하지 않는 현재 보이는 경로를 확인한다. 각종 적의 공격 예고나 빈틈 시간을 임의로 지어내지 않는다.
- 버섯야수의 기존 실측 charge 예측 profile은 사실 근거가 있는 그 예측에만 남는다. 다른 적도 short controls를 가지므로 특정 종에만 제어가 허용되던 제한은 해제됐다. 해당 적에게 버섯야수의 고정 취약시간을 그대로 적용하지 않는다.

## 원본 근거

고정 로컬 원본 C:/Dev/_ref/corekeeper-decomp:

- Pug.Base/ObjectType.cs: 전체 enum과 위5무기/채굴 도구 유형.
- Pug.Other/EquipmentSlot.cs:51~117: 기본 버튼 유지→정상 공격 요청, warmup, secondary windup 및 버튼 해제→보조 요청. 실제 mana 부족과 broken 상태 조건은 게임이 검사한다.
- Pug.Other/PlayerEquipment/AttackWithEquipmentSystem.cs:143~190: 실제 slotType에 따라 melee/range/beam/summoning 소비자로 분기한다.
- Pug.Other/RangeWeaponSlot.cs:17~45: 실제 조준방향·projectile trigger·내구도/소비 요청. ThrowingWeapon은 이 정상 소비 경로를 사용한다.
- Pug.Other/BeamWeaponSlot.cs:13~49: 기본 사용과 windup에 따른 정상 빔 활성/투사체 요청.
- Pug.Other/SummoningWeaponSlot.cs:12~28,31~107와 MinionSummonAndDismissSystem.cs:200~260,955~1005: 기본 사용은 일반 명령 경로, 미최대 보조 해제는 SpawnMinion(dismissMatchingMinions=false), 최대 충전 해제는 ChargeUpMinionAction(dismissMatchingMinions=true)이다. 최대 해제는 같은 종류가 있으면 전부 해산하고 새로 소환하지 않으며, 없으면 다른 종류를 해산하고 전체 교체 요청을 처리한다. 최초 구현이 이 차이를 놓친 것을 독립 검수가 발견하여 별도 후보·실행으로 수정했다.
- Pug.Other/SpecialWeaponHandler.cs:177~186: DrillTool/BeamWeapon의 실제 held primary 사용 조건.

## 검증

- [입력·정밀 전투 adapter 로그](actions-input.log): node Tools/action-input-tests/run.mjs,43검사 PASS. 생산 AICombatOptions·AIWeaponInput·AIWeaponUseControl·AIHandCapabilities·AIAttackSkill을 직접 링크하고 실제 StepSkill과 AIActions.Make 생성식을 원본에서 추출한다. 실제 선택 후보→실제 Skill→BodyInput→관측 도착/충전 해제까지 실행했다. secondary/summon 전달, 개별소환용 미최대 해제와 최대해제의 해산/교체 차이를 검증한다. game consumer는 원본분기를 따른 fixture이며 실제 소환수 생성은 아니다.

개별소환의 해제window를 놓쳐 최대충전에 도달한 경우 보조hold를 유지하면서 재판단 관측을 남긴다. 이때 중지·시야이탈 등으로 실제 버튼을 놓으면 게임은 해산/교체할 수 있다. 이는 정상 게임입력의 타이밍 한계로 명시한다. 시야이탈 해제는 실패·실제소환 미확인으로 끝내며, 최대충전 관측이 있었으면 완료 문장에도 해산/교체 가능성을 남긴다. 절대 해산하지 않는다는 보장이나 ECS직접쓰기를 추가하지 않았다.
- [가방 adapter 로그](actions-inventory.log): dotnet run --project Tools/life-inventory-tests -c Release,126검사 FAIL0. 새7종 Bow/SummonTome/BeamStaff/Bomb/Drill/Sledge/Wood를 실제 생산 Candidates→InventorySkill→정상 Swap 요청1회→양쪽 슬롯/실제 손 장착 관측까지 확인한다.
- [컴파일 로그](actions-compile.log): 설치된 게임 DLL을 참조하는 dotnet compile-check 성공,0경고0오류.

adapter의 World/ECS·충돌·인벤토리 소비자·손 장착 상태·target health는 fixture다. 게임 실제 projectile/소환 성공, 명중률, 실제 도구 채굴, 네트워크 왕복은 이번 검증에 포함되지 않았다. 게임 실행·설치·추가 모델 호출은 하지 않았다. 새로운 공격 지원을 실게임 성공으로 승격하지 않는다.

## 통합 파일

AIInventory.cs, AIBody.cs, AIAttackSkill.cs, AIChargeWatch.cs(측정profile 주석), AIActions.cs(실제 secondary 바인딩), 새 AICombatOptions.cs/AIWeaponInput.cs/Core AIHandCapabilities.cs/AIWeaponUseControl.cs와 meta, Tools/action-input-tests 및 life-inventory-tests 확장, maintenance csproj helper 링크를 변경했다. AIBrain.Candidates.Brute의 generic 후보 합치는 한 줄은 별도 owner가 통합했다. actions-integration.patch는 전달 기록이며 중복 재적용하지 않는다.

### 명시 base 상자 범위 회귀 수정 (f222c50 → 4f60211)

- where 생략은 기존 자기 상자를 위치 반경 없이 목적지로 사용할 수 있다. 명시 `base`는 먼저 기지 기준점(기준점이 막혔으면 정상 BaseStand 도착)에 도달하고, 그때 관측되고 물리적으로 조작 가능한 자기 상자만 골라 목표 진척으로 삼는다. 새 기지 소속 반경은 추가하지 않았다.
- AIInventory 도착 검사 5곳은 AIBase.ArrivedAtBase(world,player,out _)를 공유한다. 목표 완료, 생활 선택, 후보 생성, 메뉴 transfer 진척, stale 검사가 동일 맥락을 쓴다. 실제 조작 상자가 없으면 그 사실을 제시한다.
- 생산 AIInventory 후보/MenuMoves/MenuGoal/InventorySkill/두 슬롯 관측을 실행한 adapter: 152 PASS / 0 FAIL. 100칸 밖의 가까운 자기 상자로 explicitbase를 완료하지 못함, omitted where는 같은 상자로 정상 완료, 기준점 도착 후 상자가 없다는 사실, 현장 상자 정상 완료, 막힌 기준점의 stand(3,0) 도착 후 Home 반복 없이 inspect→store→관측 완료를 포함한다.
- 이 inventory fixture에서 AIBase의 이동·공유 도착 결과/시야와 ECS는 어댑터 경계다. 실제 게임 소비자 실행이나 월드 내 이동을 증명하지 않는다. 공유 도착 함수 본체는 main의 별도 base/place 생산 어댑터 검증 범위다.
- compile-check: 0 warning / 0 error. life-craft: 29 PASS / 0 FAIL. 기록: actions-base-inventory.log, actions-base-craft.log.
- 확장 확인 maintenance run.ps1: 100 PASS / 4 FAIL. 기존 IntegrationCases의 전투 중 생활 배제/메뉴 닫힘 기대(40,77)와 후속 BagNav enter(105)가 현재 정책과 맞지 않는다. 이 범위의 테스트는 변경하지 않았으며 HEAD/main에게 알렸다. actions-base-maintenance.log에 원문을 보존했다.
- main 최종 Unity gate에 사용한 Inventory blob: 25bafb96c7e3b669d70ec1264ee1595133fdd0e9. 이후 제품 소스 변경 없음.