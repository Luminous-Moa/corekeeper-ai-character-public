import json, hashlib
from pathlib import Path
S=Path('C:/Dev/.head/sessions/코어키퍼-AI-캐릭터')
P=S/'work/2026-11/2026-11-08-행동판단필수보완'
A=S/'work/2026-11/2026-11-08-행동그래프미포함점검/areas'
C=S/'work/게임원본-상호작용/code'
O=P/'four'
readset=set()
def read(p):
 p=Path(p); readset.add(p); return p.read_text(encoding='utf-8-sig')
def js(p): return json.loads(read(p))
def dump(v): print(json.dumps(v,ensure_ascii=True,indent=2))
g=js(P/'baseline-246/graph.json'); ns=g['nodes']
def code(p,a,b):
 t=read(C/p).splitlines(); print('\n'+p); print('\n'.join(f'{i+1}: {t[i]}' for i in range(a-1,min(b,len(t)))))
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def ref(p,a,b):
 t=read(C/p).splitlines(); assert 1<=a<=b<=len(t)
 return dict(path=(C/p).as_posix(),sha256=sha(C/p),startLine=a,endLine=b)
def write(name,v): (O/name).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def ev(k): return dict(nodeId=k,origin=ns[k].get('origin'))
def binding(oid):
 return [dict(sourceKey=n['sourceKey'],objectId=oid,variation=n['variation'],role='has_completion') for n in ns.values() if n.get('kind')=='source' and n.get('objectId')==oid]
def record(key,action,bs,reuse,summary,conditions,flow,results,failures,boundary,refs,extra=None):
 value=dict(summary=summary,conditions=conditions,flow=flow,results=results,failures=failures,boundary=boundary,codeReferences=[ref(*r) for r in refs],sourceEvidence=[ev('source:'+b['sourceKey']) for b in bs]+[ev(k) for k in reuse],existingReuse=reuse)
 if extra: value.update(extra)
 return dict(id='completion:essential:'+key,kind='action_completion',action=action,sourceBindings=bs,links=[dict(relation='reuses_consumer' if ns[k].get('kind') in ['flow','condition'] else 'supports_action',to=k) for k in reuse],value=value)
def generate():
 # Reconsult everything that materially informed production; pins are content hashes, not path-name claims.
 for p in [P/'00-agreement.md',A.parent/'recovery/report.md',A.parent/'review/recovery-independent.md',A/'D/report.md',A/'E/report.md',A/'D/compared-nodes.json',A/'E/current-comparison-exact.json',A/'B/evidence/code-evidence.json',A/'B/evidence/raw-objects.json',A/'B/evidence/graph-source-neighborhood.json',A/'D/breed-source-candidates.json']:
  read(p)
 bc=js(A/'B/evidence/raw-components.json'); cow=js(A/'D/cow-breed-authoring.json')
 rawOff=[dict(componentKey=x['key'],rawByteSha256=x['rawByteSha256'],entityMonoPathId=x['data']['entityMono']['m_PathID'],mechanic=x['data']['mechanic'],mechanicValue=x['data']['mechanicValue'],mechanicMultiplier=x['data']['mechanicMultiplier']) for x in bc if x.get('script',{}).get('class')=='OffHandAuthoring']
 breeder=record('breed-downstream-cow1300-v1','breed_downstream_child_request',binding(1300),['completion:C:breed-toggle-v1/flow/consumer','animal-feed'],
  'After the existing toggle/request and Breed entry, recheck the actual partner and Meals before requesting a child; reset both Meals only in the eligible branch. Representative exact Cow1300:0 binding only.',
  ['Current StateInfo must be StateID.Breed; otherwise return. The consumer immediately calls LeaveState before partner checks.', 'Partner must be non-null with BreedStateCD and MealsEatenCD. Both own and partner Meals must satisfy that entity\'s mealsToTrigger (HasEatenEnough uses >=). Existing request consumer retains distance, type, fullness and toggle checks; these are not invented as new downstream rechecks.'],
  ['Read BreedStateCD and own actual Meals, recheck partner components and both Meals.', 'GetChildVariation uses property list 239678920. If no list exists, return parent variation. Otherwise sample rng.NextFloat and return the first Variation whose AccumulatedProbability is >= sample; if none matches return parent variation.', 'Request EntityUtility.CreateEntity at parent transform+(0,0,-0.1), babyType, amount 1 and selected variation. Then ECB.SetComponent resets own and partner Meals.Value to 0.'],
  ['Eligible branch schedules child creation and both Meals resets; Cow raw authoring babyType=1304, mealsToTrigger=30, minDistanceToBreed=2.0.', 'Cow mutationChance=0.4000000059604645 and weights (variation0:2,1:2,2:1,3:1,4:0.6000000238418579) feed converter cumulative probabilities; unselected tail falls back to parent. The converter preserves meals threshold/babyType and ensures Meals and BreedToggle components.'],
  ['Invalid/missing partner components or insufficient Meals exits Breed without child creation/reset. A stale prior request is insufficient.', 'CreateEntity can return Entity.Null for missing runtime DB primary. This code does not branch on returned entity before scheduling Meals resets: a reset request is not proof of a live child.'],
  ['ECB playback, live DB prefab availability, RNG outcome and actual birth are untested.', 'Only the independently evidenced Cow source is bound. This generic system is not an authoring audit of all breeding species. Existing feeding, toggle and growth results are retained.'],
  [('Pug.Other/BreedStateSystem.cs',155,198),('Pug.ECS.Components/BreedStateCD.cs',3,26),('Pug.ECS.Conversion/BreedStateConverter.cs',5,33),('Pug.Other/EntityUtility.cs',155,193)],
  dict(authoringEvidence=dict(path=(A/'D/cow-breed-authoring.json').as_posix(),rawComponentsPath=cow['path'],line=cow['line'],rawLineSha256=cow['rawLineSha256'],componentKey=cow['record']['key'],rawByteSha256=cow['record']['rawByteSha256'])))
 commonOff=[('Pug.Other/PlayerEquipment/OffHandEquipmentUpdateSystem.cs',190,232),('Pug.Other/PlayerState/UseOffHand.cs',15,60),('Pug.ECS.Conversion/OffHandConverter.cs',5,23),('Pug.Base/OffHandMechanic.cs',1,11)]
 dash=record('dash8340-8341-v1','offhand_dash',binding(8340)+binding(8341),['completion:C:equip-gear-swap-v1/flow/transitions'],
  'Offhand dash activation initializes direction and move timer, then writes collision-adjusted movement force until expiry requests Walk.',
  ['UseOffHand_HeldDown, CurrentStateAllowInteractions and no Release/MinecartRiding/Fishing/BoatRiding/VehicleRiding/UseOffHand/MountRiding state; offhand object is non-None and not broken.', 'Runtime primary must have OffHandCD with a supported mechanic; an unelapsed offHandCooldownTimer blocks this entry (pressed-on-cooldown tick may be recorded). Exact authoring maps 8340 and 8341 to mechanic 2=Dash.', 'Equip/swap is the existing prerequisite consumer only; it does not perform a dash. Runtime component conversion and state request acceptance remain conditions.'],
  ['Offhand entry starts offHandCooldownTimer from primary CooldownCD, default 0.5 seconds if absent, clears useDashNextUpdate then sets it for Dash. Update consumes that flag once.', 'StartDashingRoutine reads adjustedMovementVelocity. initialDashDirection preserves that vector; zero uses facingDirection, nonzero length<1 is normalized. A vector of length>=1 is preserved.', 'moveTimer starts with 0.15*(1+SummarizedConditions[205].value/100*0.7) seconds; activeRoutine=Dashing.', 'While Dashing and moveTimer not elapsed, use historical collision world at currentTick offset1, corner smoothing on initialDashDirection, and current offhand primary OffHandCD. force = cornerSmoothedDirection*movementSpeed*mechanicValue minus Walk.GetPushOutOfEnemiesForce; write PlayerMovementForceCD.Value.', 'On moveTimer expiry set routine Inactive and request next Walk. UseOffHand.Exit clears Shielding/Dashing routine and, when nextState is not Walk, zeroes movement force.'],
  ['Saved authoring mechanicValue is 2.0 for8340 and2.6000001430511475 for8341; saved cooldown is0.699999988079071 for both. This is input to the conditional runtime formula, not a fixed displacement.', 'Movement force write and later Walk request are explicit static outcomes; steering after dash start does not replace initialDashDirection in this consumer.'],
  ['Cooldown/state/item gates can reject activation. Missing runtime OffHandCD during Dashing skips the new force write.', 'Collision smoothing/enemy push-out, current speed and current offhand change the force. No fixed distance, pass-through, hit avoidance or immunity is established by this dash path.'],
  ['State acceptance, physics movement/collisions and displacement are not runtime tested.', 'No durability or mana deduction is introduced from code absence; the recorded explicit time cost is cooldown and dash timer.'],
  commonOff+[('Pug.Other/PlayerState/UseOffHand.cs',127,158),('Pug.Other/PlayerState/UseOffHand.cs',504,545),('Pug.Other/PlayerEquipment/PlayerDashingRoutineSystem.cs',207,228),('Pug.ECS.Conversion/CooldownConverter.cs',5,25)],
  dict(authoringEvidence=[x for x in rawOff if x['mechanic']==2]))
 minion=record('minion-detonator8330-8331-v1','offhand_owned_minion_detonation',binding(8330)+binding(8331),['completion:C:equip-gear-swap-v1/flow/transitions','completion:B:summon-9176-0/flow/transitions'],
  'Offhand MinionDetonation marks only tracked minions owned by the player for dismissal and detonation, then conditionally initializes explosive damage. Reuse generic summon/dismiss and tracker consumers.',
  ['UseOffHand_HeldDown must be set. CurrentStateAllowInteractions must pass using SecondInteract_HeldDown && !Interact_HeldDown for its second-interaction argument, and player state must exclude Release, MinecartRiding, Fishing, BoatRiding, VehicleRiding, UseOffHand and MountRiding. Read the item from containedObjectsBuffer[equipmentCD.offHandIndex]; its objectID must be non-None and ItemIsBroken must be false. This overload classifies amount<=0 as broken only when the runtime primary exists, has DurabilityCD and its object info is nonstackable; there is no separate unconditional positive-amount gate here. The item runtime primary must expose OffHandCD with mechanic neither None nor Bait; the exact 8330/8331 authoring selects mechanic5=MinionDetonation, distinct from RemoteDetonator. A running offHandCooldownTimer must be elapsed; otherwise return (a fresh UseOffHand_Pressed records lastPressedOnCooldownTick). The Shield-only drill/beam/Attacking restriction does not apply to MinionDetonation, so this supported mechanic requests SetNextState(UseOffHand); state acceptance remains conditional.', 'MinionDetonateJob acts only while the player routine is MinionDetonating and only on trackedMinions entries with owner equal to this player.', 'DeathTriggerEffectsJob requires destroyed simulated minion with OwnerReferenceCD, MinionLevelCD and IsExplosiveCD; it reads owner summarized conditions. Needed runtime buffers/components remain prerequisites.'],
  ['UseOffHand entry starts cooldown and sets useMinionDetonationNextUpdate. Update consumes flag once and starts MinionDetonating routine with minionDetonationTimer=0.5seconds.', 'For every tracked minion belonging to this player set markedForDismissal=true, willDetonate=true, detonatedByOffhand=true. Other owners are not marked.', 'Reuse generic dismissal: existing tracking consumer reduces dismissTimer, enables EntityDestroyedCD and removes from trackedMinions when <=0; invalid owner paths can set skipTriggeringDeathEffects.', 'Death effects first read condition317/100 and set hasTriggeredDeathEffects=true. Return if ((!willDetonate && condition317<=0) || skipTriggeringDeathEffects). Otherwise clear willDetonate; require owner condition and condition-effect buffers.', 'Initialize multiplier=1. If detonatedByOffhand and owner inventory/equipment exist, read CURRENT offhand. Only MinionDetonator or MinionDetonatorMinor with runtime OffHandCD replaces multiplier with mechanicValue.', 'GetMinionDamage(minion,level,ownerConditions,ownerConditionEffects); damage=(int)(minionDamage*(detonatedByOffhand ? multiplier : condition317/100)). Clear detonatedByOffhand, write IsExplosiveCD.damage and tileDamage equally, ignoreExploding=false.', 'UseOffHand.Update requests Walk for routines other than Shielding/Dashing/Grappling (including MinionDetonating). Dedicated timer consumer changes MinionDetonating to Inactive after expiry.'],
  ['Saved mechanicValue is5.0 for8330 and3.0 for8331; saved cooldown1.0seconds for both. Damage uses mechanicValue, not mechanicMultiplier; current offhand at death-effects consumption determines the replacement multiplier.', 'The owned minions are spent through dismissal. Explosive fields are initialized only in the eligible death-effects branch, with integer truncation from float multiplication.'],
  ['No owned tracked minions means no targets are marked. Generic summon dismissal without these flags is not equivalent detonation.', 'skipTriggeringDeathEffects or missing required owner buffers prevents explosive initialization. Missing/currently different detonator leaves multiplier at1 for the offhand-marked branch; it does not preserve an item multiplier at activation.', 'Setting explosive fields does not prove explosion scheduling, collision, damage delivery or a kill.'],
  ['Generic summon mana/reservation/tracker behavior is reused only in its existing scope; this offhand record does not request summoning or imply its mana charge.', 'Damage setup only; actual blast and game success remain runtime boundaries. No unrelated loot/damage path is used as equivalence.'],
  commonOff+[('Pug.Other/PlayerController.cs',7224,7235),('Pug.Other/PlayerState/UseOffHand.cs',84,103),('Pug.Other/PlayerState/UseOffHand.cs',504,545),('Pug.Other/PlayerEquipment/PlayerMinionDetonatingSystem.cs',132,138),('Pug.Other/MinionHandlerSystem.cs',155,187),('Pug.Other/MinionHandlerSystem.cs',503,519),('Pug.Other/MinionHandlerSystem.cs',1399,1407),('Pug.Other/MinionHandlerSystem.cs',1451,1460),('Pug.Other/MinionHandlerSystem.cs',1541,1574)],
  dict(authoringEvidence=[x for x in rawOff if x['mechanic']==5]))
 graves=[n for n in ns.values() if n.get('kind')=='source' and n.get('objectId')==6002]
 grave=record('player-death-grave-inventory-v1','player_death_grave_inventory',[],['object:6002','completion:C:container-inventory-transfer-v2/flow/transitions'],
  'Health-zero lifecycle requests PlayerGrave6002, player GUID and mode-specific initial inventory transfer; the transfer consumer has death-only item exclusions and conditional destination writes.',
  ['health<=0 and player lacks SpawningFromCore and Death. This runtime-player predicate is not bound to a fixed catalog player source.', 'Grave creation is requested in all these modes. Casual gets no InitialMoveInventoryFromCD transfer request. Noncasual uses inventoryBuffer[0].maxSize; nonhardcore starts at slot10, hardcore at slot0, amount=maxSize-start.', 'MoveInventory dispatch requires valid inventory/buffer presence and non-destroyed inventories. Existing container consumer is reused for the shared dispatcher validity gates only, not its MoveAmount/TryMove algorithm.'],
  ['Create PlayerGrave amount1 using EntityUtility (default variation0); set transform to round(playerPosition*16)/16 and ClaimedByPlayerGuidCD to playerGhost.playerGuid.', 'Noncasual adds InitialMoveInventoryFromCD(entityFrom=deadPlayer,startSlotToMove,amountToMove). InitMoveInventory disables the initial component and, if source non-null, queues MoveInventory(fromPlayerKilled=true,toStartIndex=0).', 'InventoryUpdateSystem dispatches MoveInventory to InventoryUtility.MoveInventory. It updates destination InventoryBuffer.extraSize=maxSize-sizeX*sizeY, limits source loop to requested range and source buffer length, and clamps destination start>=0.', 'For each source slot, if fromPlayerKilled and non-None item primary has IgnoreTransferInventoryWhenPlayerKilled, skip it (source remains). Otherwise if destination index is outside buffer append source item; if existing destination is empty assign source item; only in these branches clear source slot and ResetLockedObject.', 'Both source and destination indices advance per source iteration, including an excluded item; occupied destination leaves the source item intact.'],
  ['Normal noncasual transfer range is [10,maxSize); hardcore is [0,maxSize). Casual retains inventory in this producer path but still requests a grave.', 'Eligible transfer writes destination and clears matching source conditionally; grave request/GUID assignment alone does not establish all possessions moved.'],
  ['Already Death/SpawningFromCore or positive health skips this producer.', 'Null initial source disables initial component without enqueuing transfer. Invalid inventories, ignored-death-transfer prefabs and occupied destination slots prevent the corresponding movement.', 'Missing DB primary can return Entity.Null; subsequent ECB requests/playback and actual grave existence are not proven.'],
  ['6002 catalog source variants are not chosen runtime DB primaries by this evidence. Their existing placement/damage/loot/healing roles remain distinct.', 'No catalog source is assigned has_completion for the runtime-player lifecycle. Shared inventory gates are reused; death MoveInventory semantics are newly stated.', 'No claim of hardcore character-save deletion, successful grave retrieval or actual inventory loss is made.'],
  [('Pug.Other/SpawnGraveForDeadPlayerSystem.cs',160,182),('Pug.Other/InitMoveInventorySystem.cs',123,134),('Pug.Other/Inventory/InventoryUpdateSystem.cs',357,382),('Pug.Other/Inventory/InventoryUpdateSystem.cs',797,824),('Pug.Other/Inventory/InventoryUpdateSystem.cs',911,913),('Pug.Other/Inventory/InventoryUtility.cs',959,1003),('Pug.Other/EntityUtility.cs',155,193)],
  dict(catalogTargetEvidence=[ev(n['id']) for n in graves]))
 armor=record('player-death-armor-cost-v1','player_death_armor_durability',[],['completion:B:melee-basic-19001-0:cost-followup-v1/flow/transitions'],
  'Health-zero requests locked Death and, on nonhardcore server, adds0.15 to armor durability percentage. Armor-specific consumer adds flat loss and requests SetAmount; cost is not exactly15percent.',
  ['health<=0 and no SpawningFromCore/Death; SetNextState(Death,nextStateLocked=true) is requested.', 'Only !hardcore && isServer enables ReduceDurabilityOfAllEquipmentTriggerCD and increments its percentage by0.15. Other accumulated damage/percentage may also be present.', 'Armor consumer targets helmSlotIndex,breastSlotIndex,pantsSlotIndex only. Each slot must have amount>0 and primary prefab DurabilityCD.'],
  ['Consumer disables trigger, copies damage and percentage, then resets trigger to default.', 'Condition118 probability, playerInvincibility or godMode can zero damage; damage<3percent of maxHealthWithConditions is also zeroed. These branches do not clear copied percentage.', 'If damage!=0 or percentage!=0, fraction=clamp(damage/maxHealthWithConditions,0,1); flatDurabilityLoss=(int)max(1,round(fraction*5)). Thus a lone nonzero death percentage with zero damage still computes flat loss1.', 'Each eligible armor slot computes percentLoss=round(maxDurability*percentage), newAmount=max(oldAmount-flatDurabilityLoss-percentLoss,0); queues SetAmount with same objectID and playerEntity. Zero outcome reports break and may request EquipmentBreak effect.', 'Reuse existing inventory processing and same-ID SetAmount consumer: valid current inventory and unchanged slot objectID are required before amount is written. This armor helper does not enqueue the weapon helper\'s TryReplaceBrokenObject.'],
  ['Conditional armor cost is round(maxDurability*accumulatedPercentage)+flatLoss capped by current amount; the death producer contribution is0.15, not a guaranteed total percentage or exact15percent current durability.', 'Only conditional SetAmount(0 or positive) and possible break effect are established. Existing weapon cost and item replacement semantics stay in their own scope.'],
  ['Hardcore/client path does not produce this death armor percentage increment. Empty/non-durable armor slots are skipped.', 'Inventory dispatcher rejection or changed slot objectID can prevent the queued amount write. A break effect is not proof of accepted inventory change.'],
  ['Runtime player has no invented source binding. This is the death producer plus armor branch, not a new weapon-cost rule.', 'Exact runtime armor maxima, accumulated trigger values, RNG and accepted inventory writes remain untested.'],
  [('Pug.Other/CheckForDeadPlayerSystem.cs',153,177),('Pug.Other/PlayerEquipment/ChangeDurabilitySystem.cs',652,717),('Pug.Other/Inventory/InventoryUpdateSystem.cs',357,382),('Pug.Other/Inventory/InventoryUpdateSystem.cs',797,807),('Pug.Other/Inventory/InventoryUpdateSystem.cs',850,855),('Pug.Other/Inventory/InventoryUtility.cs',464,473)])
 respawn=record('player-death-respawn-gates-v1','player_death_respawn_gates_resources',[],['bed-sleep/flow/transitions','completion:B:new-spectate-next-player/condition/conditions','completion:B:new-spectate-next-player/flow/transitions','completion:B:melee-basic-19001-0:cost-followup-v1/flow/transitions'],
  'Death state locks and gates timed respawn; once eligible it reuses stored-bed/default position and restores HP/hunger plus respawn tick and immunity condition request. Permakill reuses existing spectate path.',
  ['Death.Enter locks state, sets isDyingOrDead=true and spawnedPlayer=false, starts respawnTimer at current tick and enables DisablePhysics.', 'IsPermakill=characterType.IsHardcore() && !allowHardcoreRespawn. Update immediately returns in that branch; elapsed timer does not bypass it.', 'Otherwise use respawnTimer.GetPercentageFinished(currentTick): below5/6 no respawn; at or above5/6 spawn only when spawnedPlayer was false; at or above1 unlock and request Walk.'],
  ['At threshold set spawnedPlayer=true before calling RespawnPlayer, preventing repeated resource restore on later updates.', 'RespawnPlayer clears isDyingOrDead and reuses existing bed-sleep consumer: claimed bed position unless exactly(0,0), otherwise default playerSpawnPosition.xz; write LocalTransform.', 'PlayerController restores HealthCD.health to GetMaxHealthWithConditions when summarized condition effects buffer exists, otherwise base maxHealth. Set HungerCD.hunger=70 and lastRespawnTick=currentTick.', 'AddOrRefreshCondition requests ImmuneToDamageAfterRespawn,value1,duration0 using conditions buffers/table/tick. Actual effective condition depends on that common condition system; duration0 here is not proof of permanent immunity.', 'Hit consumers RegisterFakePlayerHit and RegisterPlayerHit return false while summarized ImmuneToDamageAfterRespawn>0, alongside their other death/NoClip/godMode/login gates; reuse existing combat gate semantics.', 'At timer>=1 set isStateLocked=false and SetNextState(Walk). Death.Exit sets allowHardcoreRespawn=false and disables DisablePhysics.'],
  ['Eligible respawn writes HP,hunger70,lastRespawnTick and requests immunity once; complete timer requests unlock/Walk.', 'Hardcore without allowHardcoreRespawn retains this update gate and existing local spectate behavior. Position selection and spectate are reused, not new discoveries.'],
  ['Permakill gate blocks this automatic respawn even after timer completion. Before5/6 no resource restoration. Already spawnedPlayer prevents a second call.', 'State/RPC/ECB acceptance, condition summarization and actual movement/hit outcomes are not proven by requests.'],
  ['Runtime player has no fabricated catalog source. No timer duration in wall-clock seconds or save permanent deletion is asserted here.', 'Immunity production and shown hit gate are recorded; duration selection/removal outside this bounded lifecycle is not newly audited.', 'Six records in this supplement still represent four bounded gaps: the three death records are one grouped lifecycle.'],
  [('Pug.Other/PlayerState/Death.cs',14,53),('Pug.Other/PlayerState/Death.cs',82,94),('Pug.Other/PlayerState/Death.cs',122,142),('Pug.Other/PlayerController.cs',6988,7004),('Pug.Other/AttackPlayerSystem.cs',1355,1362),('Pug.Other/AttackPlayerSystem.cs',1469,1477)])
 records=[breeder,dash,minion,grave,armor,respawn]
 for r,group in zip(records,['breeding','dash','detonator','death','death','death']): r['group']=group
 for r in [dash,minion]:
  r['value']['authoringArtifact']=dict(path=(A/'B/evidence/raw-components.json').as_posix(),sha256=sha(A/'B/evidence/raw-components.json'))
  offkeys={x['componentKey'] for x in r['value']['authoringEvidence']}
  gameObjects={x['data']['m_GameObject']['m_PathID'] for x in bc if x['key'] in offkeys}
  r['value']['cooldownEvidence']=[dict(componentKey=x['key'],gameObjectPathId=x['data']['m_GameObject']['m_PathID'],cooldown=x['data']['cooldown'],rawByteSha256=x['rawByteSha256']) for x in bc if x.get('script',{}).get('class')=='CooldownAuthoring' and x['data']['m_GameObject']['m_PathID'] in gameObjects]
 result=dict(scope='Only four independently confirmed static gaps; append-only supplement; Cow representative breeding, dash8340/8341, miniondetonator8330/8331, grouped player death lifecycle. No publication.',runtimeTested=False,wholeScopeComplete=False,records=records)
 write('supplement.json',result)
 # Exact baseline comparison, retaining graph origins and every source role rather than keyword absence.
 sourceIds=set('source:'+b['sourceKey'] for r in records for b in r['sourceBindings'])|set(n['id'] for n in graves)
 reuseIds=set(k for r in records for k in r['value']['existingReuse'])
 compareIds=sourceIds|reuseIds
 exact={k:ns[k] for k in sorted(compareIds)}
 relevantEdges=[e for e in g['edges'] if e[0] in sourceIds]
 checks=[]
 for r in records:
  assert r['id'] not in ns
  assert all(b['role']=='has_completion' and ns['source:'+b['sourceKey']]['objectId']==b['objectId'] and ns['source:'+b['sourceKey']]['variation']==b['variation'] for b in r['sourceBindings'])
  assert all(l['to'] in ns for l in r['links'])
  checks.append(dict(id=r['id'],appendOnlyIdAbsent=True,allBindingIdentitiesMatch=True,allReuseTargetsExist=True,codeReferencesHashAndRangeValid=True,existingReuse=r['value']['existingReuse'],addedSemantics=r['value']['summary']))
 # Pin and independently resolve exact origin pointers for current source/reused consumer values.
 originChecks=[]
 graphRoot=S/'work/게임행동-그래프'
 for k,n in exact.items():
  org=n.get('origin')
  if not org: continue
  p=(graphRoot/org['path']).resolve(); raw=js(p); cur=raw
  for bit in org.get('pointer','').split('/')[1:]:
   bit=bit.replace('~1','/').replace('~0','~'); cur=cur[int(bit)] if isinstance(cur,list) else cur[bit]
  normalized=hashlib.sha256((json.dumps(cur,ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+'\n').encode('utf-8')).hexdigest()
  originChecks.append(dict(nodeId=k,path=p.as_posix(),pointer=org.get('pointer',''),expectedNormalizedSha256=org.get('normalizedValueSha256'),actualNormalizedSha256=normalized,matches=normalized==org.get('normalizedValueSha256')))
 assert all(x['matches'] for x in originChecks)
 write('static-check.json',dict(status='PASS_STATIC_SELF_CHECK_ONLY',baselineGraphPath=(P/'baseline-246/graph.json').as_posix(),baselineGraphSha256=sha(P/'baseline-246/graph.json'),recordCount=len(records),boundedGapCount=4,sourceBindingCount=sum(len(r['sourceBindings']) for r in records),runtimeTested=False,wholeScopeComplete=False,checks=checks,originChecks=originChecks,currentExactNodes=exact,currentSourceOutgoingEdges=relevantEdges,graveSourceComparison=[dict(nodeId=n['id'],sourceKey=n['sourceKey'],objectId=6002,variation=n['variation'],currentRoles=[e for e in relevantEdges if e[0]==n['id']],supplementBindingAssigned=False,reason='Catalog PlayerGrave target; existing placement/damage/loot/healing roles do not identify actual runtime DB primary or a fixed player producer.') for n in graves]))
 readset.add(Path(__file__))
 read(S/'work/게임행동-그래프/legacy_graph.py')
 # Include previously consulted support excerpts/pins as local evidence, without extending scope to their other candidates.
 for f in ['D/code-excerpts.txt','D/code-pins.json','E/code-excerpts.txt','E/code-pins.json','B/evidence/exact-alias-stage-scan.json','B/evidence/rules.json']:
  read(A/f)
 for f in ['Pug.ECS.Components/PlayerState/DeathStateCD.cs','Pug.ECS.Conversion/CooldownConverter.cs']:
  read(C/f)
 write('pins.json',dict(algorithm='SHA256',currentGraphInput='immutable baseline-246 copy of the initially consulted published graph; original observed SHA equals this copy',files=[dict(path=p.as_posix(),sha256=sha(p),bytes=p.stat().st_size) for p in sorted(readset,key=str)],outputHashes={f:sha(O/f) for f in ['supplement.json','static-check.json']}))
 report='''# 네 범위 정적 보완 제안

6개 append-only 레코드로 4개 확인 공백을 보완한다. 정본 게시·제품·게임·원본·세션 기록은 변경하지 않았다. `runtimeTested=false`, `wholeScopeComplete=false`다. 사망 3개 레코드는 한 lifecycle 범위다.

| 범위 | 기존 표현 재사용 | 추가한 최소 의미 | 오행동 방지 |
|---|---|---|---|
| 번식 후속 | breed-toggle consumer·animal-feed | 대표 Cow1300:0의 실제 partner/양쪽 Meals 재검증→babyType 생성 요청·양쪽 Meals reset·variation 누적확률/부모 fallback | Breed 진입만 보고 새끼가 생겼다고 판단하거나 식사량을 재사용하지 않게 한다. |
| dash8340/8341 | equip-gear-swap consumer | offhand gate·초기 방향·condition205 timer·현재 speed/OffHand 값·충돌 보정 force·만료Walk | 장착만으로 발동했다고 보거나 고정 거리/무적 이동으로 오인하지 않게 한다. |
| miniondetonator8330/8331 | equip consumer·generic summon/dismiss tracker | 소유 tracked minion 세 플래그·death-effect gate·현재 offhand 배율→explosive damage/tileDamage 초기화 | 다른 소유자 소환수를 대상으로 잡거나 일반 해제를 폭발 성공으로 오인하지 않게 한다. |
| 플레이어 사망 lifecycle | object6002, shared inventory dispatch gate, 기존 SetAmount gate, bed/default 위치·관전·combat immunity gate | 모드별 무덤/이전, armor producer와 감산식, permakill/timer·HP/hunger/tick/condition 요청 | Casual/일반/hardcore의 손실과 복귀를 구분하고 정확히15% 비용 또는 즉시 부활을 가정하지 않게 한다. |

`supplement.json`이 통합 입력이다. `static-check.json`은 기준 그래프의 exact source ID/variation/원문 origin 및 역할·기존 값·추가 의미를 보존한다. 재사용 링크는 모두 기준 노드에 실제 존재하며 새 ID는 기준에 없다. 기준 origin JSON 포인터의 정규화 해시는 전부 일치했다. `pins.json`에는 읽은 코드·조사 근거·원문·고정 기준 그래프 해시가 있다. 줄 범위와 SHA는 각 레코드 codeReferences에 있다. 이 자기 정적검사는 독립 수용 검수가 아니다.

번식은 원문 authoring을 별도 확인한 Cow1300:0만 source binding을 부여했다. 시스템 일반성과 다른 종별 authoring 수용을 혼동하지 않는다. 6002 원본7개 variation0의 기존 역할은 그대로 비교했고 runtime player 사망/armor/respawn에 임의 source를 부여하지 않았다. 6002 source를 has_completion으로 붙이지 않고 object:6002를 생성 대상 문맥으로 연결한다. shared container 링크는 inventory validity dispatcher만 재사용하며 MoveAmount 알고리즘을 death MoveInventory와 동등화하지 않는다. armor는 weapon TryReplaceBrokenObject를 상속하지 않는다.

ECB·state·inventory·condition 요청과 실제 출생/이동/폭발/손실/복귀 성공은 구별한다. minion 실제 blast, DB primary 선택, immunity 실제 지속시간, 전체 게임 완전성은 미확정 경계다. 수면·탈것·다른 미완 후보는 확대 조사하지 않았다.
'''
 (O/'report.md').write_text(report,encoding='utf-8')
 print(json.dumps(dict(records=len(records),boundedGapCount=4,sourceBindings=sum(len(r['sourceBindings']) for r in records),originChecks=len(originChecks),pins=len(readset)),ensure_ascii=False))
if __name__=='__main__':
 import sys
 if sys.argv[1]=='generate': generate()
 if sys.argv[1]=='inspect':
  for k,n in ns.items():
   if k.startswith('source:') and n.get('objectId') in [1300,8340,8341,8330,8331,6002]: dump(n)
  print('consumer candidates')
  for k,n in ns.items():
   if any(x in k.lower() for x in ['summon','minion','breed','grave','death','respawn','durability']):
    if n.get('kind') not in ['source','object_source'] and not k.startswith('source:'): print(k,n.get('kind'),str(n.get('value',''))[:100])
  for f in ['B/evidence/raw-components.json','B/evidence/raw-objects.json','D/breed-source-candidates.json']:
   x=js(A/f); print(f, str(x[0] if isinstance(x,list) else x)[:1800])
 if sys.argv[1]=='code':
  for args in [('Pug.Other/BreedStateSystem.cs',100,215),('Pug.Other/PlayerState/UseOffHand.cs',15,180),('Pug.Other/PlayerEquipment/PlayerDashingRoutineSystem.cs',190,240),('Pug.Other/MinionHandlerSystem.cs',475,530),('Pug.Other/MinionHandlerSystem.cs',1525,1585)]: code(*args)
