import {readFileSync, writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const dir=path.dirname(fileURLToPath(import.meta.url));
const root=path.resolve(dir,'../..');
const input=JSON.parse(readFileSync(path.join(root,'input-equipment.json'),'utf8'));
const dump=JSON.parse(readFileSync(path.join(root,'inputs/dump.json'),'utf8'));
const decomp='C:/Dev/_ref/corekeeper-decomp/Pug.Other';
function source(name,start,end,excerpt){const file=`${decomp}/${name}.cs`, bytes=readFileSync(file), lines=bytes.toString('utf8').split(/\r?\n/); if(!lines.slice(start-1,end).join('\n').includes(excerpt)) throw Error(`source mismatch ${file}:${start}`); return {file,line:start,endLine:end,sha256:createHash('sha256').update(bytes).digest('hex'),excerpt};}
const mapping=source('PlayerController',4062,4122,'return EquipmentSlotType.ShovelSlot;');
const gear=source('EquipGearSlot',11,104,'inventoryChangeData = Create.Swap');
const shovel=source('ShovelSlot',24,59,'if (!valueRW.canPlaceObject || diggableEntityAndInfos.Length <= 0)');
const fishing=source('FishingRodSlot',10,60,'equipmentUpdateAspect.playerStateCD.ValueRW.PushState(PlayerStateEnum.Fishing);');
const dispatch=source('PlayerEquipment/EquipmentUpdateSystem',156,193,'HoeSlot.UpdateEquipment');
const attacks=source('PlayerEquipment/AttackWithEquipmentSystem',144,186,'MeleeWeaponSlot.AttackWithItem');
const gearTypes=['Helm','BreastArmor','PantsArmor','Necklace','Lantern','Bag','Offhand','Pouch','Ring'];
const rules=[
 {id:'equipment-gear-swap',types:gearTypes,exclusions:'그 밖의 ObjectType, 프리팹의 ObjectData 조회 실패, 부서짐, 보조 상호작용 미입력 또는 마우스에 아이템이 있는 경우. 코드가 찾은 슬롯이 없으면 제외.',description:'유형에 맞는 장비 슬롯과 현재 슬롯의 교체를 대기열에 넣는다. 실제 프리팹과 빌드 대응은 미확인.',sources:[mapping,gear]},
 {id:'equipment-shovel-dig',types:['Shovel'],exclusions:'그 밖의 ObjectType; 부서짐, 보조 상호작용 미입력, 마우스 아이템 보유, 배치 불가 또는 파기 후보 없음.',description:'삽 슬롯에서 유효한 배치와 후보가 있으면 Dig 상태를 시작한다. 구체적인 대상별 결과는 별도 조건에 의존한다.',sources:[mapping,shovel]},
 {id:'equipment-fishing-state',types:['FishingRod'],exclusions:'그 밖의 ObjectType; 부서짐, 보조 상호작용 미입력, 마우스 아이템 보유, 쿨다운 중. 이미 낚시 상태인 경우에는 별도 끌어올리기 분기.',description:'낚싯대 슬롯에서 낚시 상태가 아닐 때 낚시 상태를 시작한다. 물이나 어획 성공을 보장하지 않는다.',sources:[mapping,fishing]}
];
const more=[
 ['melee',['MeleeWeapon','MiningPick','Sledge','DrillTool'],'근접 공격 처리하기','melee_attack',source('MeleeWeaponSlot',9,16,'queueHitLookup.SetComponentEnabled'),{attackDispatchReached:true,broken:false,attackNotCanceled:true,aoeOverride:false,activeRoutine:'Inactive 또는 Shielding'},{hitQueued:true}],
 ['range',['RangeWeapon','ThrowingWeapon'],'원거리 공격 발사 요청하기','ranged_attack',source('RangeWeaponSlot',18,29,'rangedWeaponSpawnProjectileTriggerTagLookup.SetComponentEnabled'),{attackDispatchReached:true,broken:false,attackNotCanceled:true,aoeOverride:false,activeRoutine:'Inactive 또는 Shielding',rangedWeaponComponent:true,projectileIdNotNone:true},{projectileTriggerEnabled:true}],
 ['beam',['BeamWeapon'],'광선 공격 처리하기','beam_attack',source('BeamWeaponSlot',13,36,'beamWeaponSpawnProjectileTriggerTagLookup.SetComponentEnabled'),{attackDispatchReached:true,broken:false,attackNotCanceled:true,aoeOverride:false,activeRoutine:'Inactive 또는 Shielding',currentWindupPositive:true},{projectileTriggerEnabled:true}],
 ['summon',['SummoningWeapon'],'소환 처리 요청하기','summon_minion',source('SummoningWeaponSlot',11,73,'MinionSummonAndDismissSystem.SpawnMinion'),{attackDispatchReached:true,broken:false,attackNotCanceled:true,aoeOverride:false,activeRoutine:'Inactive 또는 Shielding',currentWindupPositive:true,hasSecondaryUse:true,atMaxWindup:false,sufficientMana:true,firstTimeFullyPredictingTick:true},{spawnMinionCalled:true}],
 ['hoe',['Hoe'],'괭이로 땅 다루기','use_hoe',source('HoeSlot',27,109,'PlayerStateEnum.Flatten'),{secondaryInteractHeld:true,broken:false,itemInMouse:false,canPlaceObject:true},{digOrFlattenStateStarted:true}],
 ['water',['WaterCan'],'물뿌리개로 밭에 물 주기','water_ground',source('WaterCanSlot',19,145,'TileType.wateredGround'),{secondaryInteractHeld:true,itemInMouse:false,canPlaceObject:true,amountPositive:true,dugUpGroundInArea:true,noWaterRemovableEntityTakesPrecedence:true},{dugUpGroundConvertedToWateredGround:true}],
 ['bucket',['Bucket'],'양동이로 액체 담기','fill_bucket',source('BucketSlot',18,109,'InventoryHandler.SetVariation'),{secondaryInteractHeld:true,canPlaceObject:true,emptyOrWaterSourceEntityPresent:true,waterTileOrWaterSourcePresent:true},{fillAmountAndVariationRequested:true,refillWaterStateStarted:true}],
 ['bucket-pour',['Bucket'],'양동이 액체 붓기','pour_bucket',source('BucketSlot',34,119,'PlayerStateEnum.PlaceWater'),{secondaryInteractHeld:true,canPlaceObject:true,amountPositive:true,waterSourceEntityAbsent:true},{amountDecrementRequested:true,placeWaterStateStarted:true}],
 ['paint',['PaintTool'],'대상 칠하기','paint_entity',source('PaintToolSlot',13,58,'ValueRW.color = (PaintableColor)paintIndex'),{secondaryInteractHeld:true,canPlaceObject:true,paintToolComponent:true,amountPositive:true,server:true,paintableEntityPresent:true},{entityColorSet:true}],
 ['instrument',['Instrument'],'악기 연주 시작하기','play_instrument',source('InstrumentSlot',10,26,'PlayerStateEnum.PlayingInstrument'),{secondaryInteractPressed:true,itemInMouse:false,broken:false,onCooldown:false,alreadyPlaying:false},{playingInstrumentStateStarted:true}],
 ['roof',['RoofingTool'],'지붕 전환 처리하기','toggle_roof',source('RoofingToolSlot',17,92,'PlayerStateEnum.Flatten'),{secondaryInteractHeld:true,broken:false,itemInMouse:false,canPlaceObject:true,objectIdNotNone:true},{flattenStateStarted:true}]
 ,['seed',['Seeder'],'씨앗 심기 요청하기','plant_seed',source('SeederSlot',18,148,'Create.ConsumeEntityAt'),{secondaryInteractHeld:true,broken:false,itemInMouse:false,canPlaceObject:true,compatibleSeedAndGround:true,seedAmountPositive:true},{seedPlacementInventoryRequestQueued:true}]
];
for(const rule of rules) rule.sources.splice(1,0,dispatch);
for(const [id,types,label,verb,src,conditions,result] of more){const attack=['melee','range','beam','summon'].includes(id);rules.push({id:'equipment-'+id,types,exclusions:'열거하지 않은 ObjectType 및 기록된 조건을 만족하지 않는 분기. 프리팹 구성과 개별 변형, 빌드 대응 미확인.',description:label,sources:[mapping,attack?attacks:dispatch,src],extra:{verb,label,objectRole:'tool',conditions,result}})}
function pointer(p){return p.split('/').slice(1).reduce((v,k)=>v?.[k.replace(/~1/g,'/').replace(/~0/g,'~')],dump)}
const keys=new Set();
const records=input.map(r=>{
 if(keys.has(r.key)) throw Error('duplicate input '+r.key); keys.add(r.key);
 if(r.raw && JSON.stringify(dump.objects[r.dumpIndex])!==JSON.stringify(r.raw)) throw Error('raw mismatch '+r.key);
 for(const i of r.interactions) for(const s of i.sources||[]){if(s.file!=='inputs/dump.json') throw Error('unexpected source'); const value=pointer(s.pointer); if(!Array.isArray(value)) throw Error('invalid pointer '+s.pointer); if(i.verb==='craft' && !value.some(x=>Array.isArray(x)?x[0]===r.id:x===r.id)) throw Error('craft pointer mismatch '+r.key); if(i.verb==='use_as_ingredient' && !value.some(x=>Array.isArray(x)&&x[0]===r.id)) throw Error('ingredient pointer mismatch '+r.key);}
 for(const i of r.interactions){if(i.verb==='craft'){if(i.subject.variation!==null||i.variationApplicability!=='unknown') throw Error('variation claim '+r.key);if(i.recipeObservation && JSON.stringify(i.recipeObservation.ingredients)!==JSON.stringify(pointer(i.recipeObservation.source.pointer))) throw Error('recipe observation '+r.key);for(const s of i.conditions.stations){const obj=dump.objects[s.dumpIndex];if(`${obj.id}:${obj.var}`!==s.key||!obj.canCraft.some(x=>JSON.stringify(x)===JSON.stringify(s.rawEntry)))throw Error('station entry '+r.key)}} if(i.verb==='use_as_ingredient')for(const s of i.conditions.recipes){const obj=dump.objects[s.dumpIndex];if(`${obj.id}:${obj.var}`!==s.key||!obj.req.some(x=>x[0]===r.id&&x[1]===s.amount))throw Error('ingredient amount '+r.key)}}
 const interactions=structuredClone(r.interactions);
 let rule, extra;
 if(gearTypes.includes(r.type)){rule=rules[0]; extra={verb:'equip_gear_swap',label:'장비 슬롯으로 교체하기',objectRole:'target',conditions:{secondaryInteractHeld:true,broken:false,itemInMouse:false,objectDataAvailable:true,matchingSlotFound:true},result:{inventorySwapQueued:true}};}
 else if(r.type==='Shovel'){rule=rules[1];extra={verb:'dig_with_shovel',label:'삽으로 파기',objectRole:'tool',conditions:{secondaryInteractHeld:true,broken:false,itemInMouse:false,canPlaceObject:true,diggableCandidatesPresent:true},result:{digStateStarted:true}};}
 else if(r.type==='FishingRod'){rule=rules[2];extra={verb:'start_fishing',label:'낚시 상태 시작하기',objectRole:'tool',conditions:{secondaryInteractHeld:true,broken:false,itemInMouse:false,onCooldown:false,alreadyFishing:false},result:{fishingStateStarted:true}};}
 if(rule) interactions.push({...extra,evidenceLevel:'code_candidate',applicability:`덤프의 ${r.type} 유형에 대한 슬롯 코드 후보; 프리팹 연결 및 빌드 대응 미검증`,ruleId:rule.id,sources:rule.sources});
 for(const moreRule of rules.slice(3).filter(x=>x.types.includes(r.type))) interactions.push({...moreRule.extra,evidenceLevel:'code_candidate',applicability:`덤프의 ${r.type} 유형에 대한 슬롯 코드 후보; 프리팹 연결 및 빌드 대응 미검증`,ruleId:moreRule.id,sources:moreRule.sources});
 return {key:r.key,interactions,unresolved:['프리팹 연결과 디컴파일 빌드 대응, 실제 실행 결과 미확인','개별 아이템 및 변형의 특수 행동과 추가 상호작용 미확인',...(!rule&&!rules.slice(3).some(x=>x.types.includes(r.type))?[`${r.type} 유형의 세부 사용 조건 및 프리팹 연결은 이번 수집에서 확인하지 못함`]:[])],reviewMethod:'고정 입력의 모든 key와 기존 데이터 관계를 검사하고 유형별 코드 조건을 대조'};
});
const result={group:'equipment',records,rules,coverage:{inputCount:input.length,outputCount:records.length,unresolvedCount:records.filter(r=>r.unresolved.length).length}};
writeFileSync(path.join(dir,'results.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({...result.coverage,rules:rules.map(x=>({id:x.id,count:records.filter(r=>r.interactions.some(i=>i.ruleId===x.id)).length}))}));
