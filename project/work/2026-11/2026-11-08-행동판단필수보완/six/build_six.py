import json, hashlib, pathlib, sys
sys.stdout.reconfigure(encoding='utf-8')
S=pathlib.Path('C:/Dev/.head/sessions/코어키퍼-AI-캐릭터')
P=S/'work/2026-11/2026-11-08-행동판단필수보완'
O=P/'six'; C=S/'work/게임원본-상호작용/code'; RAW=S/'work/게임원본-상호작용/raw/components.jsonl'
R=S/'work/2026-11/2026-11-08-행동그래프추가기록/discovery'
G=P/'baseline-246/graph.json'
TABLE=S/'work/2026-11/2026-11-07-행동결과완결/full/A/condition-effect-table-crosswalk-v2/condition-effect-table-crosswalk.json'
def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
graph=read(G); nodes=graph['nodes']; records=[]; consulted={G, RAW, TABLE, P/'00-agreement.md',R/'residual-report.md',R/'residual-B/new-bounded-findings-evidence.json',R/'residual-D/specific-static-attribution-candidates.json', S/'work/게임원본-상호작용/final/collection.json'}
consulted.add(pathlib.Path('C:/Users/<USER>/.codex/plugins/cache/head-agent-core/head-context/0.9.6/skills/start-job/SKILL.md'))
# Fixed-source component lookup only. This is not a new source census.
gos={'3659196512904756013','-4978199038008246069','-6326547763910474403','2990425448905211277','-6765861840663272179','6996190252284572541','6613728040975621475','-678199301658897363','-781565855912073988'}
raw={}
for n,line in enumerate(RAW.open(encoding='utf-8-sig'),1):
 v=json.loads(line)
 if str(v.get('data',{}).get('m_GameObject',{}).get('m_PathID','')) in gos:raw[n]=v

def binding(oid,key,role='has_conditional_effect'):
 return {'sourceKey':'CAB-2ebcba97367158c9b08f790c25d642c9:'+key,'objectId':oid,'variation':0,'role':role}
def rec(group,suffix,kind,action,bs,summary,reuse):
 r={'id':'completion:essential:'+suffix,'group':group,'kind':kind,'action':action,'sourceBindings':bs,'links':[{'relation':'reuses_consumer','to':x} for x in reuse], 'value':{'summary':summary,'conditions':[],'costs':[],'flow':[],'results':[],'failures':[],'boundary':[],'codeReferences':[],'sourceEvidence':[],'existingReuse':reuse}}
 for b in bs:
  k='source:'+b['sourceKey'];r['value']['sourceEvidence'].append({'nodeId':k,'origin':nodes[k]['origin']})
 records.append(r);return r
def ref(r,key,path,a,b):
 path=pathlib.Path(path);consulted.add(path)
 r['value']['codeReferences'].append({'id':key,'path':path.as_posix(),'sha256':sha(path),'startLine':a,'endLine':b})
def code(r,key,path,a,b):ref(r,key,C/path,a,b)
def fact(r,section,text,*refs):r['value'][section].append({'rule':text,'refs':list(refs)})
def rawref(r,key,n):ref(r,key,RAW,n,n)
def table(r,key,cid):
 lines=TABLE.read_text('utf-8-sig').splitlines()
 start=next(i for i,l in enumerate(lines,1) if l.strip()=='"conditionID": '+str(cid)+',')
 end=next(i for i in range(start,len(lines)+1) if '"effectName"' in lines[i-1])
 ref(r,key,TABLE,start,end)
 r['value'].setdefault('tableEvidence',[]).append({'path':TABLE.as_posix(),'sha256':sha(TABLE),'conditionID':cid,'pointer':'/groups/'+str({39:12,102:17,206:21}[cid]),'tableAsset':'resources.assets:27162','tableRawByteSha256':'ee1c78681e166cbed06c78a9ed58abe2df5725ad53d43bdb4571e8dba10c1eb7'})
def shared(r):
 code(r,'convert','Pug.ECS.Conversion/SupportConditionsConverter.cs',23,62)
 code(r,'summary','Pug.Other/SummarizeConditionsSystem.cs',113,153)
 code(r,'map','Pug.Other/SummarizeConditionsSystem.cs',432,453)
 fact(r,'flow','Active SupportsConditionsAuthoring supplies initialConditions; active AreaLevelAuthoring invokes seeded LevelToConditionValues before ConditionsBuffer insertion. Authored values are not unconditional runtime summaries.','convert')
 fact(r,'flow','Summarization is chunk order/change gated; it clears summary arrays and includes own ConditionsBuffer except ordinary conditions of a buffsOwner pet. Table-valid ConditionID values are added unchanged to the ID summary and its mapped effect; the summary method does not invert isNegative values or multiply valueMultiplier again.','summary','map')

r=rec('grapple8373-8374','grapple-input-aim-v1','action_completion','grapple_offhand_input_aim_request',[binding(8373,'1753535845213101411','has_completion'),binding(8374,'6907783803396625453','has_completion')], '8373:0/8374:0 offhand mechanic7 input → UseOffHand → selected aim → direct grappling request/state writes, bounded before downstream grappling movement.', ['completion:C:equip-gear-swap-v1'])
rawref(r,'hook8373',79520);rawref(r,'hook8374',81520);rawref(r,'cool8373',66295);rawref(r,'cool8374',8778)
code(r,'offhand-enum','Pug.Base/OffHandMechanic.cs',1,11)
code(r,'offhand-convert','Pug.ECS.Conversion/OffHandConverter.cs',6,22)
code(r,'input','Pug.Other/PlayerEquipment/OffHandEquipmentUpdateSystem.cs',190,233)
code(r,'interaction','Pug.Other/PlayerController.cs',1111,1174)
code(r,'enter','Pug.Other/PlayerState/UseOffHand.cs',15,60)
code(r,'aim-request','Pug.Other/PlayerState/UseOffHand.cs',255,323)
code(r,'update','Pug.Other/PlayerState/UseOffHand.cs',521,530)
fact(r,'conditions','Both exact active authorings have mechanic7=GrapplingHook; converter copies mechanicValue, minMaxTime and mechanicMultiplier. Authored mechanicValue is 7 for8373 and10 for8374, minMaxTime approximately0.1..0.6; no extra range inferred from mechanicMultiplier.','hook8373','hook8374','offhand-enum','offhand-convert')
fact(r,'conditions','UseOffHand_HeldDown must be set; CurrentStateAllowInteractions must pass for SecondInteract_HeldDown && !Interact_HeldDown. Explicitly reject Release, MinecartRiding, Fishing, BoatRiding, VehicleRiding, UseOffHand and MountRiding. The offhand slot must be non-None, unbroken, resolve a prefab with non-None/non-Bait OffHandCD and have no unelapsed running offhand cooldown.','input')
fact(r,'conditions','CurrentStateAllowInteractions rejects restricted guest mode (guestMode && adminPrivileges<1), spawning/release/death/teleport and non-idle sleep; Sitting, PlayingInstrument, vehicle/mount and secondary-use slot rules follow the referenced method.','interaction')
fact(r,'costs','Entering starts offHandCooldownTimer from prefab CooldownCD, fallback0.5 seconds. Exact active CooldownAuthoring values are8373≈0.8s and8374≈0.6s; this cost starts before hit success. No mana/item/durability debit is present in this bounded input/Enter/StartGrapplingRoutine path.','cool8373','cool8374','enter','aim-request','input')
fact(r,'flow','GrapplingHook pushes UseOffHand instead of SetNextState. Enter stores selected objectID/variation and sets useGrapplingNextUpdate; Update clears the flag before invoking StartGrapplingRoutine.','input','enter','update')
fact(r,'flow','Keyboard/mouse uses normalizesafe(aimDirection). Pad starts from adjustedMovementVelocity, overrides it with aimDirection when wasAiming, uses facingDirection only if resulting length is zero, then normalizes.','aim-request')
fact(r,'flow','Request resolves stored hook prefab and mechanicValue, obtains current/history collision worlds, constructs the offset-adjusted grapplingDirection, then calls tile, world, enemy-stop and enemy-pushback helpers. These are exact direct helper calls; their full downstream behavior is outside this record.','aim-request')
fact(r,'results','Regardless of didHit, method starts grapplingTimer using lerp(minMaxTime, closestWalkableTileDistance/mechanicValue), writes hook position/distance/hitState and increments grapplingUseIndex. didHit sets Success or DidNotReachWallSuccess after walkable-distance comparison and permits activeRoutine=Grappling.','aim-request')
fact(r,'failures','No held input, blocked interaction/state, invalid/broken/unsupported offhand or cooldown returns before push. A pressed button during cooldown records lastPressedOnCooldownTick before return; that write alone does not prove a queued retry.','input')
fact(r,'failures','NothingHit leaves hit state NothingHit and does not newly set activeRoutine=Grappling; Update requests Walk when the resulting active routine is none of Shielding/Dashing/Grappling. Failed hit still follows the timer/position/index writes and already-started cooldown.','aim-request','update','enter')
fact(r,'boundary','Completion here means the static direct request and field writes only. PlayerGrapplingRoutine travel/collision/end, helper internals, successful traversal, runtime DB/physics and network outcome are not claimed. Do not apply this mechanic7 rule to every offhand.','aim-request','update')
r['value']['judgmentGapPrevented']='From either hook item, distinguish a usable input/aim request, cooldown rejection and NothingHit from a successful traversal; gear equip alone cannot provide that distinction.'

r=rec('lightning9135','lightning-chain-target-v1','action_completion','lightning_first_and_chain_target_selection',[binding(9135,'6805886266268017916','has_completion')], 'Lightning9135:0 exact first aim and chain buffer selection with conditional chain attacks; existing common beam/cost/health behavior reused.', ['beam','common:new-conditions-buffer-consumer'])
code(r,'order','Pug.Other/PlayerEquipment/ChainBeamTargetsSystem.cs',21,24)
code(r,'first','Pug.Other/PlayerEquipment/ChainBeamTargetsSystem.cs',246,346)
code(r,'chain','Pug.Other/PlayerEquipment/ChainBeamTargetsSystem.cs',347,418)
code(r,'first-helper','Pug.Other/PlayerEquipment/ChainBeamTargetsSystem.cs',420,482)
code(r,'chain-helper','Pug.Other/PlayerEquipment/ChainBeamTargetsSystem.cs',484,525)
code(r,'active','Pug.Other/PlayerEquipment/ChainBeamTargetsSystem.cs',528,554)
code(r,'interaction','Pug.Other/PlayerController.cs',1111,1174)
code(r,'beam-cost','Pug.Other/BeamWeaponSlot.cs',13,42)
rawref(r,'beam-source',54335);rawref(r,'beam-cooldown',71221)
fact(r,'conditions','Before PlayerAttackSystem, clear PlayerChainTargetsBuffer. Continue only for ObjectID.LightningGun, held amount>0, Interact_HeldDown, neither VehicleRiding nor MountRiding, CurrentStateAllowInteractions(false), and a BeamWeaponCD on equipmentPrefab. The interaction method includes guest/state/sleep rules.','order','first','active','interaction')
fact(r,'flow','Take source weapon damage from reinforced/level prefab when present (fallback1). Use history worlds at interpolation Delay and zero delay. Beam start uses SpecialWeaponHandler; normalize targetingDirection; initial endpoint=start+direction*5+(0,0.25,0), filter selected by pvpEnabled.','first')
fact(r,'flow','Cast rays in delayed then zero-delay worlds, each helper accepting only matching UseLagCompensation presence, excluding self and ground tile entities. Player branch uses faction attack permission; other first hits exclude pets/minions. Select smaller hit fraction. If no hit, sphere-cast radius1 along original endpoint, with same rules plus forward dot>0. Then sample wall tiles at0.2 spacing and write PlayerAimPosition.position/isHittingSomething.','first','first-helper','active')
fact(r,'flow','If firstHitEntity is nonnull add its position, then at most3 iterations overlap within5 of previous position in both worlds. Candidate must match lag-compensation branch, be EnemyCD or permitted PlayerGhost, have HealthCD>0 and LocalTransform, be nonself and not already targeted. Compare squared hit-position distances, sample walls to candidate transform, and append chosen transform position.','chain','chain-helper')
fact(r,'conditions','Preserve actual hitWall state: Execute initializes it once before the3-iteration loop; GetCollisionFromOverlapChained sets the ref flag true on any sampled wall and never resets it for each candidate or each subsequent hop. A prior wall therefore can suppress later candidate assignments; do not replace this with an idealized nearest-visible-target algorithm.','chain','chain-helper')
fact(r,'costs','Target/chain selection itself contains no new mana/durability debit. Reuse beam attack activation: ConsumeAnyRequiredMana, calculated item cooldown, QueueHit and beam active timer. Source authored cooldown≈0.2s; shared beam source-specific cost/availability and damage consumers remain conditional, and selection alone does not prove that activation or payment occurred.','first','chain','beam-cost','beam-cooldown')
fact(r,'results','PlayerAimPosition is updated even when no first entity exists (possibly wall endpoint); chain buffer remains empty without a first entity. With a first entity buffer contains first target plus up to3 distinct chosen chain positions.','first','chain')
fact(r,'results','Only newly selected chain targets receive this method’s AttackHelper.Attack request, when cooldown.GetElapsedTicks(currentTick)==1 and !isPartialTick; parameter attacker=player, attackOffset=targetTransform-playerPosition+0.5up, radius0.1, damage/playerDamage=num, predicted and beforePhysics true. It does not set isRanged in this parameter initializer. The first target damage uses existing beam path.','chain')
fact(r,'failures','Inactive item/input/state or missing BeamWeaponCD returns after clearing chain buffer. Missing firstHitEntity returns after aim write. No eligible next entity breaks chain. Walls, repeated/self/dead/missing-transform targets or incompatible lag/faction reject candidate; cooldown/partial tick suppress this method’s attack request without suppressing selected positions.','first','chain','first-helper','chain-helper','active')
fact(r,'boundary','Ray and sphere helpers differ from chain helper: a first nonpet/nonminion collision is not necessarily a healthy EnemyCD. Wall tile endpoint update does not clear firstHitEntity. Selection/buffer/AttackHelper request is static evidence, not observed hit/health success. Existing beam node supplies shared payment/activation and actual health/tile consumers.','first','first-helper','chain','beam-cost')
r['value']['judgmentGapPrevented']='From9135, expose aim-only and chain-position results separately from cooldown-gated attacks and reused beam costs, preventing a selected chain from being treated as damage already dealt.'

r=rec('effect58','effect58-input-direction-v1','condition_consumer','effect58_buttonmask_and_direction_support',[], 'Two exact Execute consumers of summarized Effect58(Stunned): clear buttonSetMask and condition facing-direction copy; no source producer census.', ['common:new-conditions-buffer-consumer','D114:pet1270-projectile-shatter:common','beam'])
r['links']=[{'relation':'supports_action','to':x} for x in r['value']['existingReuse']]
code(r,'clear-order','Pug.Other/ClearInputOnStunnedSystem.cs',13,17)
code(r,'clear-query','Pug.Other/ClearInputOnStunnedSystem.cs',54,60)
code(r,'clear','Pug.Other/ClearInputOnStunnedSystem.cs',124,130)
code(r,'direction-order','Pug.Other/SetPlayerDirectionSystem.cs',15,22)
code(r,'direction-query','Pug.Other/SetPlayerDirectionSystem.cs',69,78)
code(r,'direction','Pug.Other/SetPlayerDirectionSystem.cs',144,150)
code(r,'effect-enum','Pug.Base/ConditionEffect.cs',58,64)
fact(r,'conditions','ClearInputOnStunned runs after SummarizeConditionsSystem and SetClientInputFromClientInputDataSystem in BeforePredictedFixedStepSimulationSystemGroup OrderFirst; query requires Simulate, SummarizedConditionEffectsBuffer and writable ClientInput.','clear-order','clear-query')
fact(r,'flow','When summarizedConditionEffectsBuffer[58].value>0, set ClientInput.buttonSetMask=0u. Otherwise this Execute performs no write. It does not zero aimDirection, movement or every ClientInput field.','clear')
fact(r,'conditions','SetPlayerDirection runs after PlayerVelocityCalculationSystem in BeforeUpdateStateSystemGroup. Query requires Simulate, PlayerStateCD, PlayerOrientationCD, ClientInput, summarized effects and writable AnimationOrientationCD. This is its declared order, not a newly inferred total order between the two systems.','direction-order','direction-query')
fact(r,'flow','Copy ClientInput.facingDirection into AnimationOrientationCD.facingDirection only with none of Death/Sleep/Casting/Fishing/IgnoreAllInput, !reorientationBlocked and effect58<=0.','direction')
fact(r,'results','Positive effect58 clears all buttonSetMask bits and prevents this direction consumer from copying facing direction. Nonpositive58 only removes the stun predicate; other direction gates can still block copying.','clear','direction')
fact(r,'costs','These two Execute bodies have no resource debit; their relevant constraint is input/direction suppression while the exact predicates hold.','clear','direction')
fact(r,'failures','Missing query components or disabled Simulate prevent the corresponding job match. State/orientation flags can block direction independent of stun; effect58<=0 does not restore prior cleared buttons, generate a retry, or itself request an action.','clear-query','direction-query','clear','direction')
fact(r,'boundary','Effect58 and ConditionID58 are separate namespaces. This records typed consumers only; exact supply, timing/expiry and whole stun effects remain under existing producers/shared condition processing. It does not assert motor velocity is set to zero.','effect-enum','clear','direction')
r['value']['sourceEvidence']=[{'nodeId':x,'origin':nodes[x]['origin']} for x in r['value']['existingReuse']]
r['value']['judgmentGapPrevented']='Existing beam/condition consumers can expose why held-button actions or direction changes are suppressed despite an action being otherwise available; no invented Effect58 supplier is needed.'

r=rec('condition39-6839','condition39-source6839-v1','conditional_relation','initial_condition39_to_effect2_source6839',[binding(6839,'-8092099457503870773')], '6839:0 AFPestElectric own initial Condition39→Effect2 MovementSpeed with signed conversion, summary cap and source movement-speed write.', ['gap-reused-fragment:condition-buffer-consumer:0'])
shared(r);table(r,'table',39);rawref(r,'source',63569);rawref(r,'level',65557);rawref(r,'speed-source',20988)
code(r,'level-condition','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',155,202)
code(r,'scaling','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',824,835)
code(r,'cap','Pug.Other/SummarizeConditionsSystem.cs',390,421)
code(r,'speed-convert','Pug.ECS.Conversion/MovementSpeedConverter.cs',5,11)
code(r,'speed-query','Pug.Other/UpdateMovementSpeedSystem.cs',240,252)
code(r,'speed-write','Pug.Other/UpdateMovementSpeedSystem.cs',31,35)
code(r,'multiplier','Pug.Other/EntityUtility.cs',1431,1445)
fact(r,'conditions','Exact active source has initial Condition39 duration1,value-256,valueMultiplier5; its active AreaLevel is level7 and authored MovementSpeed20. Table maps39→Effect2 MovementSpeed and isNegative=true. This is a condition on6839 itself, not proof its attacks apply39 to victims.','source','level','speed-source','table')
fact(r,'flow','Level conversion for negative MovementSpeed uses base-50, authored multiplier5 and seeded deviation0.9..1.1 divided by condition divisor, rounded with sign-aware nonzero clamp. Preserve duration; do not multiply authored-256 by5 or invert its negative sign during summary.','level-condition','scaling','source','map')
fact(r,'results','Summary’s MovementSpeed effect is lower-capped at-900. MovementSpeedConverter sets originalSpeed/speed from authored20; UpdateMovementSpeedSystem, after summary and on changed effects, writes max(0,originalSpeed*(1+effect2/1000)) for its matching simulated nondestroyed source. This is the nonplayer consumer relevant to6839.','cap','speed-convert','speed-query','speed-write','multiplier')
fact(r,'conditions','Movement-speed query requires Simulate, MovementSpeedCD and summarized effects, excludes EntityDestroyedCD and applies a changed-version filter to effect buffer. Other effects can contribute to aggregate2; source authored data does not establish exact runtime speed.','speed-query','map')
fact(r,'costs','This initial-condition conversion/summary/movement write has no item or mana debit in the cited bodies. Its behavioral constraint is the signed aggregate movement multiplier.','convert','map','speed-write','multiplier')
fact(r,'failures','Invalid table ID is logged and skipped; ordinary buffsOwner-pet own conditions are skipped, unchanged chunks skip summary, and unmatched movement query has no write. After condition removal a later recomputed aggregate controls speed; authored duration1 alone is not a measured one-second movement outcome.','summary','map','speed-query','source')
fact(r,'boundary','Use exact source6839 attribution and existing shared condition description, without extending to all MovementSpeed sources or inferring an electric-hit slow. Runtime condition lifetime, other summary contributions and actual displacement are not established here.','source','summary','map','speed-write')
r['value']['judgmentGapPrevented']='From6839, expose its own signed slow and matching movement write so behavior is not inferred from unrelated health evidence or incorrectly assigned to its victims.'

r=rec('condition102-3101','condition102-source3101-v1','conditional_relation','initial_condition102_source3101_range_projectile_stun',[binding(3101,'965635080865528621')], '3101:0 MoldTentacle initial102→Effect59 on its own range-projectile9222:0 caller, with inheritance and conditional receiver Stunned request.', ['gap-reused-fragment:condition-buffer-consumer:0','D114:pet1270-projectile-shatter:common','gap-consumer:condition-direct-four-v4','common:new-conditions-buffer-consumer'])
shared(r);table(r,'table',102);rawref(r,'source',21595);rawref(r,'range-source',65260);rawref(r,'level',64147)
code(r,'range-convert','Pug.ECS.Conversion/RangeAttackStateConverter.cs',7,59)
code(r,'range-gate','Pug.Other/RangeAttackStateSystem.cs',179,235)
code(r,'shoot','Pug.Other/RangeAttackStateSystem.cs',238,305)
code(r,'spawn-call','Pug.Other/RangeAttackStateSystem.cs',367,385)
code(r,'end','Pug.Other/RangeAttackStateSystem.cs',405,410)
code(r,'spawn','Pug.Other/EntityUtility.cs',2331,2381)
code(r,'inherit','Pug.Other/EntityUtility.cs',2740,2763)
code(r,'inherit-test','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',14,20)
code(r,'stun','Pug.Other/EntityUtility.cs',1902,1917)
code(r,'damage-out','Pug.Other/AttackSystem.cs',1194,1235)
code(r,'level-value','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',155,185)
code(r,'stun-base','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',434,439)
code(r,'value-scale','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',824,835)
code(r,'projectile-attack','Pug.Other/ProjectileSystem.cs',323,345)
fact(r,'conditions','Exact active source initial102 has authored value106,multiplier40,duration0, active AreaLevel level9. Table maps102→Effect59 and isInheritedByProjectiles=true. Active RangeAttackStateAuthoring is enabled/not disabled, projectile9222 variation0 and projectilesPerShot1.','source','level','range-source','table')
fact(r,'flow','Source102 can be recalculated by base5 stun-chance mapping, multiplier40, seeded deviation and condition divisor before buffer insertion; authored106 is not unconditional runtime106%. Converter copies exact projectile ID/variation into RangeAttackStateCD.','convert','level-value','stun-base','value-scale','range-convert')
fact(r,'conditions','RangeAttackStateSystem requires current StateID.RangeAttack and non-disabled state. Anticipation/preparation/shooting timers precede AimAndShootProjectile; shooting stops on elapsed attack timer or configured damage interruption. Projectile prefab must have ProjectileCD. This record begins at that state; it does not assert all world AI selectors always choose it.','range-gate','shoot')
fact(r,'flow','AimAndShootProjectile calls SpawnProjectile with firing3101 entity as owner. Successful create writes OwnerReferenceCD and InheritAttackData; nonzero owner summary IDs that pass ConditionCanBeInherited are appended as ConditionsBuffer with duration-1. Table inheritance=true makes102 eligible; projectile summary maps it to59.','spawn-call','spawn','inherit','inherit-test','table','map')
fact(r,'flow','Projectile attack parameters use isRanged=true. The shared damage consumer’s effect59 branch requires ranged, positive attackerEffect59/100 and random<=that chance, receiverEffect58==0 and60==0. If passed it requests Stunned(value1,duration2*(1-receiverEffect53/100+attackerEffect84/100)), with factor floor0.1 when nonpositive.','projectile-attack','stun')
fact(r,'results','Shared AttackSystem first calculates damage and applies damage-reduction continue gate; surviving receiver effects buffer gets appliedConditions appended to NewConditionsBuffer. Reuse common new-condition consumer for AddOrRefresh, clearing buffer, subsequent summarize/expiry. This is a conditional stun request, not proof a projectile hit or stun already occurred.','damage-out','stun')
fact(r,'costs','Range attack uses internal timers then starts cooldown=random(minCooldown,maxCooldown)/(1+effect41/1000+effect65/1000 if source effect buffer exists) and leaves state. Authored3101 cooldown range2..3 seconds is the input to that formula; no item/mana debit is present in cited range caller.','end','range-source','shoot','spawn-call')
fact(r,'failures','Wrong/disabled state, timing gate or missing projectile component prevents applicable spawn. CreateEntity returning null prevents setup/inheritance; missing RandomCD logs after earlier setup requests. Zero/missing owner summary or table inheritance rejection prevents inherited102. Receiver already stunned/immune, failed chance or prior damage-resolution gates can prevent a new stun.','range-gate','shoot','spawn','inherit','stun','damage-out')
fact(r,'boundary','Reuse1270 only for common inheritance/stun consumer, not its pet/shatter producer scope. This establishes3101’s own9222 caller independently; runtime target/AI-state entry, hit authority, receiver buffers and final expiry remain conditional.','range-source','spawn-call','stun','damage-out')
r['value']['judgmentGapPrevented']='From3101, expose its own ranged projectile and receiver stun predicates instead of borrowing pet1270’s producer identity or assuming any hit must stun.'

aurabs=[binding(1355,'3487780934490719581'),binding(5875,'-8070639519151522419'),binding(5942,'23149843004925197'),binding(5953,'7086359454936462205')]
r=rec('condition206-four-aura','condition206-four-source-aura-v1','conditional_relation','initial_condition206_direct_id_aura_attribution',aurabs, 'Four exact initial206 sources → enabled aura marker → nearby enemy attribution → max positive206 → receiver207 radioactive condition/timer; Effect0 None is not a no-action conclusion.', ['gap-reused-fragment:condition-buffer-consumer:0','partial-role-consumer:A-condition-immunity4:0'])
shared(r);table(r,'table',206)
for key,n in [('s1355',40971),('s5875',15215),('s5942',82911),('s5953',57045),('level1355',2389),('level5875',23637),('level5942',8714),('level5953',16836),('radius5875',5352),('radius5942',46837)]:rawref(r,key,n)
code(r,'converter-support','Pug.ECS.Conversion/SupportConditionsConverter.cs',80,117)
code(r,'aura-enable','Pug.Other/SummarizeConditionsSystem.cs',379,413)
code(r,'aura-query','Pug.Other/AuraConditionsSystem.cs',22,26)
code(r,'register','Pug.Other/AuraConditionsSystem.cs',164,214)
code(r,'receiver-query','Pug.Other/AuraConditionsSystem.cs',440,446)
code(r,'read206','Pug.Other/AuraConditionsSystem.cs',554,631)
code(r,'drop-source','Pug.Other/AuraConditionsSystem.cs',655,661)
code(r,'apply207','Pug.Other/AuraConditionsSystem.cs',724,745)
code(r,'faction','Pug.Other/AuraConditionsSystem.cs',1147,1177)
code(r,'schedule','Pug.Other/AuraConditionsSystem.cs',1182,1228)
code(r,'radius-convert','Pug.ECS.Conversion/AuraDistanceOverrideConverter.cs',5,10)
code(r,'condition-base','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',724,726)
code(r,'scaling','Pug.ECS.ConditionExtensions/ConditionExtensions.cs',824,835)
code(r,'tick','Pug.Other/DealDamageFromConditionSystem.cs',1387,1426)
fact(r,'conditions','Four active SupportsConditionsAuthoring rows:1355 value57,multiplier1,level16;5875 value49,multiplier1,level12;5942 value50,multiplier1,level14;5953 value80,multiplier≈1.3,level16. All initial206 duration0 and cantBeAffectedByEnvironment=0,cantBeAffectedByAuras=0. Table206 mapsEffect0 None, so direct summarized ID206 is the applicable consumer selector.','s1355','s5875','s5942','s5953','level1355','level5875','level5942','level5953','table')
fact(r,'flow','Converter ensures initially disabled HasAuraConditionCD when environment-affectable; SummarizeConditionsSystem toggles that component from direct nonzero aura IDs including206. Therefore each exact active source has the component route to aura registration after eligible summary; no Effect0 consumer is invented. Level converter uses direct ID206 base (enemy:round(level*2), otherwise GetRadioactiveDamageOverTime(level)), multiplier/deviation/divisor; authored values remain inputs.','converter-support','aura-enable','condition-base','scaling','convert')
fact(r,'conditions','Aura emitter query has HasAuraConditionCD and excludes EntityDestroyedCD. On scheduled update (VariableSystemUpdate.ShouldUpdate(...,5,4f)), overlap radius is AuraDistanceOverrideCD.distance else5, collision filter26. Exact active overrides5875=11 and5942=7 are copied by converter;1355/5953 use default only if no override component at runtime. No tactic or guaranteed reach is inferred.','aura-query','schedule','register','radius5875','radius5942','radius-convert')
fact(r,'flow','Nearby EntityPart is resolved to main entity. Skip disabled Simulate nonminions, missing aura receiver buffer and enabled DisablePhysics. ID206 counts as enemy-affecting; registration calls ShouldAffectWithAura(emitterFaction, receiverFaction, ..., receiver), whose enemy branch calls CanAffectEnemyWithAura(emitterFaction, receiverFaction, receiver): emitter faction must CanAttack receiver faction and receiver cannot be Merchant/Critter. Add unique emitter reference to IsAffectedByAurasFromEntitiesBuffer and enable that buffer.','register','faction')
fact(r,'conditions','Receiver update requires LocalTransform, enabled IsAffectedByAurasFromEntitiesBuffer, writable ConditionsBuffer and ConditionTickTimerBuffer. Retained emitter must be nonnull, nondestroyed per entityDestroyedLookup.HasAndIsComponentDisabled, within current radius, have HasAuraConditionCD and summary buffer; receiver physics must not be disabled. Receiver summarized IDs187 or81>0 invalidate the source.','receiver-query','read206')
fact(r,'flow','Receiver update calls CanAffectEnemyWithAura(receiverFaction, emitterFaction, receiver): receiver faction must CanAttack emitter faction and receiver cannot be Merchant/Critter. This call direction differs from registration (emitterFaction, receiverFaction, receiver); do not assume symmetry. With that update predicate and receiver HealthCD, read each retained source’s summary[206]; keep maximum positive value (not sum). An entry supplying no applicable aura values is removed. When max206>0 and receiver summary243==0, always AddOrRefreshCondition207 AuraRadioactiveDamageOverTime(value=max206,multiplier1); only when RadioActiveConditionCD is disabled, enable it and call GetOrCreateTickTimer. An already enabled flag skips this aura-application timer call.','read206','drop-source','apply207','faction')
fact(r,'results','When no positive206 or receiver243 immunity is nonzero, always RemoveCondition207; only when RadioActiveConditionCD is enabled, disable it and call RemoveTickTimer. An already disabled flag skips this aura-removal timer call. These are direct aura application/removal outcomes; shared victim immunity/tick consumer remains reused.','apply207')
fact(r,'costs','Aura registration and update bodies debit no player item/mana; constraints are source/receiver eligibility, current radius and scheduled updates. Do not infer consumption or currency cost from initial206.','register','read206','apply207')
fact(r,'results','Existing radioactive tick begins a missing timer at1s and on elapsed valid target queues HealthChange amount=-summary207 with bypassMaxDamagePerHit and restarts2s; immune damage/invalid destroyed/physics/collider/health gate suppresses that append. Afterwards invalid target or effect96>0 disables radioactivity/removes timer; a due append can precede that effect96 check.','tick')
fact(r,'failures','No active aura marker, no overlap/eligible receiver, faction rejection, distance/source invalidation, IDs187/81 or243 immunity can prevent/undo applicable aura application. A registered source or positive authored206 alone does not prove current positive summary,207, due damage or health change consumed.','aura-query','register','faction','read206','apply207','tick')
fact(r,'boundary','Exact four sources only, not all206 supply or all auras. Effect0 records a table mapping while actual behavior reads ConditionID206. Runtime DB/source entity, enabled marker/receiver components, faction and physics remain conditions; queued health change is not observed damage.','table','register','read206','apply207','tick')
r['value']['judgmentGapPrevented']='From each of1355/5875/5942/5953, expose the direct-ID emitter, radius/receiver/immunity and removal gates so EffectNone or victim-only damage evidence cannot hide the actual aura path.'

out={'scope':'Six agreed bounded static supplements only; no publication, product/source/game mutation, new broad census, whole grappling downstream or whole-game completeness claim.','runtimeTested':False,'wholeScopeComplete':False,'records':records}
O.mkdir(parents=True,exist_ok=True)
(O/'supplement.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
# Additional consulted code files: looked up to verify declared symbols/ranges, even when
# final evidence uses a narrower shared reference. Keep all such inputs pinned.
for p in ['Pug.Base/ObjectID.cs','Pug.Base/ConditionID.cs','Pug.Other/PlayerVelocityCalculationSystem.cs']:
 consulted.add(C/p)
consulted.add(pathlib.Path(__file__))
revision_before=O/'revision-206-before.json'
if revision_before.exists():consulted.add(revision_before)
pins={'scope':'Evidence actually read for six bounded supplements; graph publication input is immutable baseline246 copy.','graph':{'path':G.as_posix(),'sha256':sha(G)},'files':[{'path':p.as_posix(),'sha256':sha(p)} for p in sorted(consulted,key=lambda x:x.as_posix())]}
(O/'pins.json').write_text(json.dumps(pins,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
errors=[];checks=[]
def check(name,ok,detail=None):
 checks.append({'assertion':name,'passed':bool(ok),'detail':detail})
 if not ok:errors.append(name)
check('six_records_six_groups',len(records)==6 and len({r['group'] for r in records})==6)
check('append_only_unique_ids',len({r['id'] for r in records})==6 and all(r['id'] not in nodes for r in records))
check('exact_source_binding_exists',all(('source:'+b['sourceKey']) in nodes and nodes['source:'+b['sourceKey']]['objectId']==b['objectId'] and nodes['source:'+b['sourceKey']]['variation']==b['variation'] for r in records for b in r['sourceBindings']))
check('all_links_exact_existing_nodes',all(l['to'] in nodes for r in records for l in r['links']))
check('source_roles_allowed',all(b['role'] in ['has_completion','has_conditional_effect'] for r in records for b in r['sourceBindings']))
check('exact_nine_bound_sources_only',sorted(b['objectId'] for r in records for b in r['sourceBindings'])==[1355,3101,5875,5942,5953,6839,8373,8374,9135])
check('six_raw_condition_suppliers_match',all(raw[n]['data']['initialConditions'][0]=={'conditionID':cid,'duration':duration,'value':value,'valueMultiplier':mult} for n,cid,duration,value,mult in [(63569,39,1.0,-256,5.0),(21595,102,0.0,106,40.0),(40971,206,0.0,57,1.0),(15215,206,0.0,49,1.0),(82911,206,0.0,50,1.0),(57045,206,0.0,80,1.2999999523162842)]))
check('two_raw_hooks_mechanic7_and_source_ids_match',all(raw[n]['data']['m_Enabled']==1 and raw[n]['data']['mechanic']==7 and str(raw[n]['data']['entityMono']['m_PathID'])==sid for n,sid in [(79520,'1753535845213101411'),(81520,'6907783803396625453')]))
check('effect58_has_no_fabricated_source',records[2]['sourceBindings']==[] and len(records[2]['links'])>=2)
check('whole_scope_and_runtime_false',out['wholeScopeComplete'] is False and out['runtimeTested'] is False)
check('every_material_rule_has_reference_ids',all(v.get('refs') and set(v['refs'])<={x['id'] for x in r['value']['codeReferences']} for r in records for section in ['conditions','costs','flow','results','failures','boundary'] for v in r['value'][section]))
check('code_and_data_ref_sha_ranges_match',all(sha(pathlib.Path(q['path']))==q['sha256'] and 1<=q['startLine']<=q['endLine']<=len(pathlib.Path(q['path']).read_text('utf-8-sig').splitlines()) for r in records for q in r['value']['codeReferences']))
check('pins_match_all_final_evidence',all(any(p['path']==q['path'] and p['sha256']==q['sha256'] for p in pins['files']) for r in records for q in r['value']['codeReferences']))
if revision_before.exists():
 before=read(revision_before)['recordHashes']
 current={r['id']:hashlib.sha256(json.dumps(r,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode()).hexdigest() for r in records}
 check('revision206_other_five_records_unchanged',len([k for k in before if k!='completion:essential:condition206-four-source-aura-v1'])==5 and all(v==current[k] for k,v in before.items() if k!='completion:essential:condition206-four-source-aura-v1'),{'beforeRecordHashes':before,'afterRecordHashes':current,'changedOnly':'completion:essential:condition206-four-source-aura-v1'})
comparisons=[]
for r in records:
 source_ids=['source:'+b['sourceKey'] for b in r['sourceBindings']]
 edges=[e for e in graph['edges'] if e[0] in source_ids or e[2] in source_ids]
 comparisons.append({'group':r['group'],'newRecord':r['id'],'disposition':'additional_static_attribution_and_bounded_detail_with_existing_reuse','sourceNodes':source_ids,'existingReuse':r['value']['existingReuse'],'existingLinkedSourceEdges':edges,'judgmentGapPrevented':r['value']['judgmentGapPrevented']})
static={'passed':not errors,'runtimeTested':False,'wholeScopeComplete':False,'assertions':checks,'errors':errors,'comparison':comparisons,'limitation':'Producer self-check only. Independent source/generation review and installed/default CLI verification are parent tasks; this is not their substitute.'}
(O/'static-check.json').write_text(json.dumps(static,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
report=['# Six bounded static supplements','', '현재 정본은 immutable `baseline-246/graph.json`과 대조했다. 원문·게임·제품·게시물 변경 없이 supplement6개를 작성했다. `runtimeTested=false`, `wholeScopeComplete=false`이다. 독립 원문·생성규칙 검수 및 게시/기본CLI 검증은 통합 담당의 후속 단계다.','', '| 범위 | 추가 | 재사용 | 판단 공백 방지 |','|---|---|---|---|']
descs=[('8373/8374','offhand입력·상태·aim·직접grapple요청·실패/Walk·선행cooldown','기존equip/swap'),('9135','첫target/aim/chain buffer와 실제wall flag·조건부공격','기존beam·cost/health 소비'),('Effect58','정확한두Execute query/order·buttonmaskclear·directioncopy조건','기존조건buffer와pet1270공통소비·beam입구'),('6839 Condition39','정확초기값·signed level변환·source自身speed쓰기','공통initial/summary소비'),('3101 Condition102','고유range9222caller·inherited102→59·수신stun요청','공통inheritance/stun·NewConditionsBuffer'),('4source Condition206','HasAura활성·등록·반경/진영·max206→207·제거','공통initial/summary·기존victim면역/tick')]
for r,(name,add,reuse) in zip(records,descs):report.append('|'+name+'|'+add+'|'+reuse+'|'+r['value']['judgmentGapPrevented']+'|')
report+=['','원문 대조의 중요한 경계:','', '- Grapple 전체 이동/충돌 후속은 이 범위 밖이다. 입력 통과와 field write는 이동 성공이 아니다. failed hit에도 cooldown/timer/index가 이미 기록될 수 있다.', '- Lightning의 hitWall은 후보별로 초기화되지 않는다. 최초ray/sphere와 chain helper의 수신자 조건도 다르다. 표적buffer와 실제공격·자원차감은 구별했다.', '- 6839는 적 자신의 초기39이며 피해자에게electric slow를주는 근거가 아니다. isNegative는level변환기초값에관여하며summary에서부호반전하지않는다.', '- 3101 authored106와runtime summary106은동일명제가아니다. activeAreaLevel의재계산·테이블상속·발사체생성·수신공격조건을보존했다.', '- 206은EffectNone에도직접ID소비를통해작동한다. max선택이며합산이아니다. effect96검사는due tick피해queue후일수있다.', '- Effect58은공급자전수조사를하지않았고sourceBindings=[]다. 정확기존consumer/action링크를제공했으며통합생성기의역연결로기존행동조회에서도달하도록한다.', '', '각규칙의 refs는 같은레코드codeReferences의ID이며경로·전체파일SHA256·정확line범위를가리킨다. 자산설정은raw/components.jsonl의단일행,조건mapping은기존고정tablecrosswalk의정확범위와pointer를함께제시했다. sourceEvidence는현재source node의origin을보존한다. pins는모든최종근거및읽은입력파일을핀한다.', '', '비교검증: '+str(len(checks))+'개 assertion, 오류'+str(len(errors))+'개. 이는생산자정적self-check이며게임런타임·독립수용판정이아니다.']
(O/'report.md').write_text('\n'.join(report)+'\n',encoding='utf-8')
if revision_before.exists():
 with (O/'report.md').open('a',encoding='utf-8') as f:f.write('\n206 독립검수 정밀도 수정: 등록은 emitterFaction→receiverFaction, 갱신은 receiverFaction→emitterFaction의 CanAttack 호출이며 대상 인자는 둘 다 receiver다. aura 적용의 GetOrCreateTickTimer는 radio flag disabled일 때만, 제거의 RemoveTickTimer는 enabled일 때만 호출한다. 나머지 5개 레코드의 정규화 SHA256 불변을 static-check에서 확인했다.\n')
print(json.dumps({'records':len(records),'groups':len(set(r['group'] for r in records)),'sourceBindings':sum(len(r['sourceBindings']) for r in records),'pins':len(pins['files']),'assertions':len(checks),'errors':errors},ensure_ascii=False))
