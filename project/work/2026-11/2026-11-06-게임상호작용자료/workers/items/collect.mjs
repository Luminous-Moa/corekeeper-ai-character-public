import {readFileSync, writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {dirname, resolve} from 'node:path';
import {createHash} from 'node:crypto';
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const input=JSON.parse(readFileSync(resolve(root,'input-items.json'),'utf8'));
const source=(file,line,endLine,summary)=>{
 const bytes=readFileSync(file), lines=bytes.toString('utf8').split(/\r?\n/);
 const selected={4105:4105,4109:4109,13:23,42:44,14:24,28:30,55:55,82:86,837:839,1062:1071,1099:1105,2135:2143,2191:2193,4096:4100}[line] ?? line;
 const excerpt=lines[selected-1].trim();
 if(!excerpt || selected<line || selected>endLine) throw Error('invalid source excerpt');
 return {file,line,endLine,sha256:createHash('sha256').update(bytes).digest('hex'),excerpt,summary};
};
const player='C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerController.cs';
const eat='C:/Dev/_ref/corekeeper-decomp/Pug.Other/EatableSlot.cs';
const cast='C:/Dev/_ref/corekeeper-decomp/Pug.Other/CastingItemSlot.cs';
const eatSources=[source(player,4105,4106,'case ObjectType.Eatable: return EquipmentSlotType.EatableSlot;'),source(eat,13,30,'if (hasItemInMouse) ... petCandyLookup.TryGetComponent ... FeedPet ... else ... EatItem'),source(eat,42,54,'if (PlayerController.CanConsumeEntityInSlot(...)) ... Create.ConsumeEntityAt(..., 1, destroy: true, ...)')];
const castSources=[source(player,4109,4110,'case ObjectType.CastingItem: return EquipmentSlotType.CastingSlot;'),source(cast,14,24,'if (!secondInteractHeld) ... if (hasItemInMouse) ... CastItem'),source(cast,28,38,'HasAnyState(PlayerStateEnum.Casting) ... parchementRecipeLookup ... HasRequiredMaterials'),source(cast,55,59,'componentData.requiresNearbyObject != ObjectID.None ... IsAtRequiredObject'),source(cast,82,87,'SetNextState(PlayerStateEnum.Casting); EquipmentSlot.StartCooldownForItem')];
const rules=[{id:'items-eatable-conditional-consume',applies:'ObjectType Eatable, dump에 noPrefab 표시가 없는 입력 key; PetCandyCD가 없는 경우에만 EatItem 경로',excludes:'noPrefab, 그 외 타입; PetCandyCD 보유 시 FeedPet 분기이며 해당 분기의 연결은 미확인',sources:eatSources},{id:'items-casting-state',applies:'ObjectType CastingItem, dump에 noPrefab 표시가 없는 입력 key',excludes:'noPrefab 및 그 외 타입; CastItem 컴포넌트 존재 여부에 따라 CastingSlot이 되는 다른 타입은 제외',sources:castSources}];
const inventory='C:/Dev/_ref/corekeeper-decomp/Pug.Other/Inventory/InventoryUtility.cs';
rules.push(
 {id:'items-inventory-move-candidate',status:'unmapped_candidate',applies:'현재 개별 key에 적용하지 않음',excludes:'개별 소지 가능성과 이동 경로 미확인 항목 전부',needed:'인벤토리에 들어갈 수 있는 ID·variation, 슬롯·대상 인벤토리 제한과 호출 경로',sources:[source(inventory,837,842,'if (!TryMoveAll(...)) { DropItem(...); }')]},
 {id:'items-sell-candidate',status:'unmapped_candidate',applies:'현재 개별 key에 적용하지 않음',excludes:'CantBeSoldCD·Legendary·실제 가격과 판매 맥락이 미확인인 전체 항목',needed:'프리팹 CantBeSoldCD, 희귀도 해석, 계산된 가격과 판매 호출·구매자 조건',sources:[source(inventory,1062,1078,'cantBeSoldLookup.HasComponent(primaryPrefabEntity) || entityObjectInfo.rarity == Rarity.Legendary ... if (coinValue > 0) ... SellObject'),source(inventory,1099,1108,'entityObjectInfo.sellValue ... num < 0 ... GetRaritySellValue')]},
 {id:'items-repair-candidate',status:'unmapped_candidate',applies:'현재 개별 key에 적용하지 않음',excludes:'LevelEntitiesBuffer와 DurabilityCD 프리팹 연결 및 수리 조건 미확인 항목 전부',needed:'개별 프리팹 컴포넌트, 현재 내구도 및 재료·작업대 조건',sources:[source(inventory,2135,2157,'CanBeRepaired(...) ... GetCraftingMaterialInfosForRecipe(...) ... item.amountAvailable < item.amountNeeded'),source(inventory,2191,2205,'!levelEntitiesBufferLookup.HasComponent(itemPrefab) || !durabilityLookup.TryGetComponent ... objectData.amount < componentData.maxDurability')]},
 {id:'items-conditional-cast-other-types',status:'unmapped_candidate',applies:'현재 개별 key에 적용하지 않음',excludes:'NonUsable, Valuable, UniqueCraftingComponent, KeyItem 포함 개별 CastItem 컴포넌트 연결 미확인',needed:'ObjectID와 variation별 CastItem 컴포넌트·시전 효과 연결',sources:[source(player,4096,4104,'case ObjectType.NonUsable ... KeyItem: if (castItemLookup.HasComponent(objectPrefab)) ... CastingSlot ... NonUsableSlot')]}
);
const records=input.map(x=>{
 const interactions=[...x.interactions]; const unresolved=[...x.unresolved];
 if(x.type==='Eatable' && x.raw && !x.raw.noPrefab){
  interactions.push({verb:'consume_eatable',label:'식품 사용하기',objectRole:'target',evidenceLevel:'code_candidate',applicability:'덤프 유형 Eatable에 대한 슬롯 경로 후보. 프리팹에 PetCandyCD가 없을 때 EatItem; 연결과 버전 미확인',conditions:{secondaryInteract:true,mouseItemAbsent:true,petCandyComponentAbsent:true,canConsumeEntityInSlot:true,otherGameConditions:'슬롯 갱신 및 플레이어 상호작용 조건 적용'},result:{inventoryChange:'소비 요청 1개, destroy=true; 결과 평가는 별도 시스템'},ruleId:'items-eatable-conditional-consume',sources:eatSources});
  unresolved.push('PetCandyCD 연결 여부 및 실제 소비 성공·효과와 펫 먹이 분기 미확인');
 } else if(x.type==='CastingItem' && x.raw && !x.raw.noPrefab){
  interactions.push({verb:'start_casting_item',label:'아이템 시전 시작하기',objectRole:'tool',evidenceLevel:'code_candidate',applicability:'덤프 유형 CastingItem의 슬롯 처리 후보; 실제 프리팹 연결 및 빌드 대응 미확인',conditions:{secondaryInteract:true,mouseItemAbsent:true,notAlreadyCasting:true,recipeConditions:'레시피 컴포넌트가 있으면 재료와 지정된 인근 오브젝트 조건 필요',otherGameConditions:'슬롯 갱신 및 플레이어 상호작용 조건 적용'},result:{playerState:'Casting 상태 전환 요청',cooldown:'아이템 쿨다운 시작'},ruleId:'items-casting-state',sources:castSources});
  unresolved.push('개별 시전 효과·대상·소모 조건과 실제 시전 성공 미확인');
 }
 unresolved.push('타입 공통 경로 밖의 개별 프리팹 및 특수 상호작용 미확인; 보관·이동·판매·수리는 프리팹/인벤토리 및 조건 연결 미확인');
 if(x.type==='KeyItem') unresolved.push('열쇠 대상 및 소모·해제 조건과 ID 매핑 미확인');
 return {key:x.key,interactions,unresolved,reviewMethod:'입력 key·기존 덤프 관계 보존; ObjectType과 게임 코드 슬롯 경로 대조, 개별 프리팹 연결 미확인'};
});
const keys=input.map(x=>x.key), out=records.map(x=>x.key);
if(new Set(keys).size!==keys.length || new Set(out).size!==out.length || JSON.stringify(keys)!==JSON.stringify(out)) throw Error('key mismatch');
const result={group:'items',records,rules,coverage:{inputCount:input.length,outputCount:records.length,unresolvedCount:records.filter(x=>x.unresolved.length).length}};
writeFileSync(resolve(here,'results.json'),JSON.stringify(result,null,2)+'\n');


