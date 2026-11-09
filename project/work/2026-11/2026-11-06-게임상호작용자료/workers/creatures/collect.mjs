import {readFileSync,writeFileSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import {dirname,resolve} from 'node:path';
import {createHash} from 'node:crypto';
const dir=dirname(fileURLToPath(import.meta.url)), base=resolve(dir,'../..');
const input=JSON.parse(readFileSync(resolve(base,'input-creatures.json'),'utf8'));
const hash=s=>createHash('sha256').update(s).digest('hex');
function source(file,line,endLine,excerpt){const data=readFileSync(file); const lines=data.toString('utf8').split(/\r?\n/); if(!lines.slice(line-1,endLine).join('\n').includes(excerpt)) throw Error('missing source excerpt '+file); return {file,line,endLine,sha256:hash(data),excerpt};}
const inventory=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/Inventory/InventoryUtility.cs',3551,3557,'slot.slotType == ItemSlotsUIType.PetSlot && objectInfo.objectType == ObjectType.Pet');
const pool=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerController.cs',3881,3944,'case ObjectType.Critter:');
const inputPlace=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlaceObjectSlot.cs',35,45,'if (!secondInteractHeld)');
const place=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlaceObjectSlot.cs',49,98,'if (!valueRW.canPlaceObject)');
const catchSource=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerAttackSystem.cs',474,494,'playerAttackLookups.critterLookup.HasComponent(entity)');
const damage=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/EntityUtility.cs',2256,2263,'healthLookup[entityToCheck].health <= 0');
const eat=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/EatStateSystem.cs',182,195,'eatState.timer.IsTimerElapsed(time)');
const feed=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/EatStateSystem.cs',226,235,'AchievementID.FeedCattle');
const npc=source('C:/Dev/_ref/corekeeper-decomp/Pug.Objects/NPC.cs',16,34,'Manager.ui.OnVendorOpen()');
const sound=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/EffectEventExtensions.cs',926,927,'EffectID.PettingSound');
const cage=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerState/Casting.cs',424,470,'lookupStateUpdateData.cattleLookup.TryGetComponent(entity, out var _)');
const leash=source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerState/Casting.cs',407,421,'componentData.leashedToEntity != Entity.Null');
const rules=[
 {ruleId:'creatures-damage-candidate',appliesTo:'유효 적 피해 판정: EnemyCD 또는 MerchantCD 또는 해당 ObjectProperties 플래그와 양의 HealthCD, 비파괴 상태; PlayerGhost는 별도 경로',excludes:'판정은 전체 공격 성공을 보장하지 않음. 개별 ID의 컴포넌트·충돌·진영·무적 조건 미확인',sources:[damage]},
 {ruleId:'creatures-feed-candidate',appliesTo:'먹기 상태의 엔티티가 유효한 거리에서 지정된 held/contained 음식을 먹는 경우',excludes:'EatStateCD 및 먹이 선택·프리팹 연결 미확인. FeedCattle 업적명만으로 모든 Creature에 적용하지 않음',sources:[eat,feed]},
 {ruleId:'creatures-vendor-candidate',appliesTo:'NPC 인스턴스의 Interact 호출 시 구매·판매 인벤토리와 상점 UI를 여는 경로',excludes:'이름이나 상속만으로 ObjectID 연결하지 않음. 개별 프리팹의 NPC 연결 및 호출 조건 미확인',sources:[npc]},
 {ruleId:'creatures-petting-sound-observation',appliesTo:'PettingSound 효과 이벤트가 재생되면 소리 출력',excludes:'소리와 애니메이션 이름만으로 쓰다듬기 입력·대상·행동을 증명하지 못함',sources:[sound]},
 {ruleId:'creatures-cage-candidate',appliesTo:'CattleCage를 장착하고 선택·충돌 검색을 거쳐 CattleCD, 위치 및 ObjectData를 가진 대상',excludes:'개별 프리팹 CattleCD 연결과 거리·서버 처리 미확인',sources:[cage]},
 {ruleId:'creatures-leash-candidate',appliesTo:'선택된 근거리 대상에 LeashedCD가 있고 이미 묶이지 않았으며 위치가 있는 경우',excludes:'개별 프리팹 LeashedCD 연결 및 도구·검색 경로 미확인',sources:[leash]},
 {ruleId:'creatures-pet-slot',appliesTo:'dump raw.type == Pet; 입력 키에 한함',excludes:'Pet 외 타입; 슬롯 적합성만 확인하며 입수·소환 성공 보장은 아님',sources:[inventory,pool]},
 {ruleId:'creatures-place',appliesTo:'dump raw.type == Critter 또는 TrainingDummy; 입력 키에 한함',excludes:'다른 타입과 미확인 프리팹 연결; 배치 조건 실패 시 제외',sources:[pool,inputPlace,place]},
 {ruleId:'creatures-catch-component-candidate',appliesTo:'CritterCD와 충돌 필터를 충족하는 엔티티',excludes:'ObjectType Critter라는 이유만으로 적용하지 않음; 개별 프리팹과 컴포넌트 연결 미확인',sources:[catchSource]},
 {ruleId:'creatures-creature-place-component-candidate',appliesTo:'Creature에서 CattleCD 또는 MountCD가 있는 경우, 또는 Creative 모드',excludes:'개별 프리팹 컴포넌트 연결 미확인; 모든 Creature에 배치 행동 미부여',sources:[pool,place]}
];
const records=input.map(x=>{
 const interactions=[...x.interactions], unresolved=[...x.unresolved];
 if(x.raw?.type==='Pet') interactions.push({verb:'equip_pet_slot',label:'펫 슬롯에 장착하기',objectRole:'target',evidenceLevel:'code_candidate',applicability:'덤프 Pet 타입의 해당 변형; 슬롯 적합성 및 장비 슬롯 경로에 한함. 소지·실제 실행과 버전 대응은 미확인',conditions:{slotType:'PetSlot',objectType:'Pet'},result:{slotMatches:true},ruleId:'creatures-pet-slot',sources:[inventory,pool]});
 if(['Critter','TrainingDummy'].includes(x.raw?.type)) interactions.push({verb:'place_from_inventory',label:'소지한 대상을 배치하기',objectRole:'target',evidenceLevel:'code_candidate',applicability:'덤프 Critter 또는 TrainingDummy 타입의 해당 변형에 대한 배치 경로 후보; 프리팹과 실제 실행 미확인',conditions:{objectType:x.raw.type,secondInteractHeld:true,noItemInMouse:true,canPlaceObject:true,canPlaceItem:true,canConsumeEntityInSlot:true},result:{state:'PlaceObject'},ruleId:'creatures-place',sources:[pool,inputPlace,place]});
 if(x.raw?.type==='Critter') unresolved.push('포획 경로는 충돌 필터와 Critter 컴포넌트를 요구한다. 이 변형의 컴포넌트 연결 및 실제 포획은 확인되지 않았다 (creatures-catch-component-candidate).');
 if(x.raw?.type==='Creature') unresolved.push('Creature의 배치 경로는 Creative 모드 또는 CattleCD/MountCD 조건이 필요하다. 이 변형의 컴포넌트 연결은 확인되지 않았다 (creatures-creature-place-component-candidate).');
 unresolved.push('피해·먹이·상점·쓰다듬기·가축 우리·목줄 후보의 개별 프리팹과 컴포넌트 연결 및 상태 조건은 확인되지 않았다. rules의 creatures-damage-candidate, creatures-feed-candidate, creatures-vendor-candidate, creatures-petting-sound-observation, creatures-cage-candidate, creatures-leash-candidate 참조.');
 return {key:x.key,interactions,unresolved,reviewMethod:'고정 입력의 기존 관계 보존 및 raw.type을 코드의 명시적 분기와 대조; 프리팹 연결과 실행은 미확인'};
});
if(new Set(input.map(x=>x.key)).size!==input.length || new Set(records.map(x=>x.key)).size!==records.length || records.some((r,i)=>r.key!==input[i].key)) throw Error('key mismatch');
for(let i=0;i<input.length;i++) if(JSON.stringify(records[i].interactions.slice(0,input[i].interactions.length))!==JSON.stringify(input[i].interactions)) throw Error('existing interaction changed');
const result={group:'creatures',records,rules,coverage:{inputCount:input.length,outputCount:records.length,unresolvedCount:records.filter(r=>r.unresolved.length).length}};
writeFileSync(resolve(dir,'results.json'),JSON.stringify(result,null,2)+'\n');
