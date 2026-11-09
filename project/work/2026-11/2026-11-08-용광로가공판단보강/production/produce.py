"""Produce the ProcessResources chain records with exact code/raw pins and existing-node origins."""
from pathlib import Path
import hashlib,json
P=Path(__file__).resolve().parent;F=P.parent;W=F.parents[1]
G=W/'게임행동-그래프';CODE=W/'게임원본-상호작용/code';RAW=W/'게임원본-상호작용/raw/components.jsonl'
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def encode(v):return (json.dumps(v,ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+'\n').encode('utf-8')
K='CAB-2ebcba97367158c9b08f790c25d642c9:'
SRC={3905:K+'1851371803100069835',4004:K+'7493579351412767700',4009:K+'8084277474615349155',4015:K+'-4282708749011488034',4032:K+'-7713936278173450791',4044:K+'7364225312847973667',1200:K+'-5362974161099365890'}
VISIBLE=[3905,4004,4009,4015,4032,4044];ALL=VISIBLE+[1200]
# Review F1/F2: 4044 has a zero-material tag recipe (no click path); 3905's recipe material is rejected by its input slot.
CLICK=[3905,4004,4009,4015,4032];MOVE=[4004,4009,4015,4032]
OBJ=W/'게임원본-상호작용/raw/objects.json'
# (id, relative code path or RAW, start, end, anchors that must occur inside the range)
REFS=[
 ('crafting-cd','Pug.ECS.Components/CraftingCD.cs',22,38,['IsProcessAutoCrafter','CraftingType.ProcessResources']),
 ('crafting-type','Pug.Base/CraftingType.cs',1,14,['ProcessResources']),
 ('ui-manager','Pug.Other/UIManager.cs',799,809,['case CraftingType.ProcessResources:','processResourcesCraftingUI.ShowCraftingUI()']),
 ('pr-ui-show','Pug.Other/ProcessResourcesCraftingUI.cs',35,83,['hideRecipes','recipeUI.HideContainerUI()','recipeUI.ShowContainerUI()']),
 ('pr-ui-activate','Pug.Other/ProcessResourcesCraftingUI.cs',93,109,['requiredObjectsToCraft.Count == 1','HasMaterialsInCraftingInventoryToCraftRecipe','Create.ActivateRecipeSlot(activeCraftingHandler.craftingEntity, player.entity, 1, mod1, visibleSlotIndex)']),
 ('pr-ui-electricity','Pug.Other/ProcessResourcesCraftingUI.cs',125,142,['RequiresElectricity()','HasElectricity()']),
 ('recipe-slot-ui','Pug.Other/RecipeSlotUI.cs',91,131,['canActivateCrafting','timeBetweenCraftsWhenHolding = 0.1f','ActivateRecipeSlot(inventorySlotIndex, mod1, mod2)']),
 ('create-action','Pug.Other/Inventory/Create.cs',438,450,['CraftAction.ActivateRecipeSlot','int0 = multiplier','bool0 = mod1','int1 = recipeIndex']),
 ('craft-job-inputs','Pug.Other/Inventory/InventoryUpdateSystem.cs',409,436,['worldIsReadOnly','adminPrivileges <= 0','inventories.Add(in craftActionData2.mainInventoryEntity)','GetNearbyChestsForCraftingByDistance']),
 ('craft-job-dispatch','Pug.Other/Inventory/InventoryUpdateSystem.cs',462,464,['case CraftAction.ActivateRecipeSlot:','InventoryUtility.ActivateRecipeSlot(']),
 ('server-activate','Pug.Other/Inventory/InventoryUtility.cs',3063,3102,['if (recipeInfo.objectID == ObjectID.None)','requiredObjectsToCraft.Length == 1','MoveAllToOrDrop(in inventoryHandlerShared, craftingEntity, 0, playerEntity, 0, -1, position)','MoveAllToOrDrop(in inventoryHandlerShared, craftingEntity, componentData.outputSlotIndex, playerEntity, 0, -1, position)','requiredObjectsToCraft[0].amount * 10','FindAndMoveTo(']),
 ('get-recipe-info','Pug.Other/Inventory/InventoryUtility.cs',2214,2235,['bufferData.Length > index','PugDatabase.HasObject']),
 ('has-materials','Pug.Other/Inventory/InventoryUtility.cs',2743,2764,['GetAnyMaterialCostMultiplier','* multiplier','if (num2 < num)']),
 ('find-and-move','Pug.Other/Inventory/InventoryUtility.cs',2766,2794,['bufferData2[j].variation == objectData.variation','num2 -= num3;','MoveAmount(']),
 ('try-find-slot','Pug.Other/Inventory/InventoryUtility.cs',509,588,['ObjectIsValidToPutInInventory','amount < 9999','for (int i = 0; i < inventoryBuffer.Length; i++)']),
 ('try-move','Pug.Other/Inventory/InventoryUtility.cs',607,647,['TryFindSlotToAddTo(','if (num == -1)','GetAmountToMoveToInventory(']),
 ('move-amount-cap','Pug.Other/Inventory/InventoryUtility.cs',2549,2564,['9999 - objectDataTo.amount']),
 ('move-all-or-drop','Pug.Other/Inventory/InventoryUtility.cs',3206,3244,['for (int i = otherStartIndex; i < otherEndIndex; i++)','for (int j = otherStartIndex; j < otherEndIndex; j++)','DropItem(in inventoryHandlerShared, ourInventoryEntity, indexFrom, int.MaxValue, position)']),
 ('drop-item','Pug.Other/Inventory/InventoryUtility.cs',695,735,['isFirstTimeFullyPredictingTick','EntityUtility.DropNewEntity(']),
 ('nearby-chests','Pug.Other/Inventory/InventoryUtility.cs',3389,3433,['inventoryAutoTransferEnabledLookup.HasComponent(outHits[i].Entity)','nativeList.Sort()','math.min(nearbyChestsEntityWithDistanceSorted.Length, maxInventories)']),
 ('nearby-chests-crafting','Pug.Other/Inventory/InventoryUtility.cs',3796,3799,['10f, 20']),
 ('can-craft','Pug.Other/Inventory/InventoryUtility.cs',1611,1706,['objectData2.amount >= 9999','!entityObjectInfo.isStackable','canOnlyUseAnyMaterialsWithTag','* objectToCraft.amount']),
 ('craft','Pug.Other/Inventory/InventoryUtility.cs',1709,1854,['useCraftingCostMultiplier ? GetAnyMaterialCostMultiplier','ConsumeObject(','ConsumeObjectWithTag(','entityObjectInfo.initialAmount * objectToCraft.amount','dynamicBuffer2[craftingCD.outputSlotIndex]','PlayerController.AddSkill(playerEntity, SkillID.Crafting']),
 ('converter-auto-transfer','Pug.ECS.Conversion/InventoryConverter.cs',199,202,['autoTransferEnabled','InventoryAutoTransferEnabledCD']),
 ('converter-output-slot','Pug.ECS.Conversion/InventoryConverter.cs',278,328,['SetupCanCraftBuffer(craftingAuthoring)','outputSlotIndex = AddToBuffer<ContainedObjectsBuffer>(default)']),
 ('start-craft-query','Pug.Automation/Pug.Automation/PugAutomationStartCraftSystem.cs',68,83,['BigEntityCraftingDataChangedTriggerCD','SetChangedVersionFilter']),
 ('start-craft','Pug.Automation/Pug.Automation/PugAutomationStartCraftSystem.cs',180,226,['inventoryEntities.Add(in value)','InventoryUtility.CanCraft(','reference2.currentlyCraftingIndex = i','craftingTimeOverride > 0f','componentData2.isActive','math.min(1f, reference.timeLeftToCraft)']),
 ('trigger-changed','Pug.Automation/Pug.Automation/TriggerCrafterChangedSystem.cs',37,52,['DidChange(ref pugAutomationTypeHandle','DidChange(ref containedObjectsBufferTypeHandle','DidOrderChange']),
 ('craft-finish','Pug.Automation/Pug.Automation/PugAutomationCraftingSystem.cs',162,222,['!componentData2.isActive','reference.timeLeftToCraft--','StartTimer(ecb, entity2, 1f','InventoryUtility.Craft(in inventoryHandlerShared, value, mainEntityObjectData, inventoryEntities, objectToCraft, 0, componentData4.Position, useCraftingCostMultiplier: false)','reference2.currentlyCraftingIndex = -1']),
 ('automation-active','Pug.Automation/Pug.Automation/PugAutomationInstantiateSystem.cs',1125,1125,['hasEnoughElectricityToPowerStuff']),
 ('object-valid','Pug.Other/Inventory/InventoryUtility.cs',2368,2455,['ObjectIsValidToPutInInventory','acceptsObjectsWithTags','acceptsObjectIds','SlotRequirementFulfillment.FailedRequirement']),
 ('recipe-info','Pug.Other/CraftingHandler.cs',14,36,['public struct RecipeInfo','requiredObjectsToCraft => _objectInfo.requiredObjectsToCraft']),
 ('can-pickup','Pug.Other/Inventory/InventoryUtility.cs',1562,1574,['public static bool CanPickup(','componentData.outputSlotIndex >= 0','dynamicBuffer[componentData.outputSlotIndex].objectID == ObjectID.None']),
 ('mover-placed','Pug.Automation/Pug.Automation/PugAutomationSystem.cs',192,208,['PlacedAtPosition.Add(mover.stop, mover.inventoryEntity)','DropInEndOfMoveLookup']),
 ('storage-place','Pug.Automation/Pug.Automation/PugAutomationSystem.cs',2398,2434,['InventoryUtility.CanPickup(','StorageAtPosition.TryAdd','ObjectIsValidToPutInInventory','Create.MoveOrDropAmount(item, 0, storage.inventoryEntity','Create.DropAllItems(item']),
 ('automation-crafter','Pug.Automation/Pug.Automation/PugAutomationInstantiateSystem.cs',1294,1365,['AutomationType.Crafter','case CraftingType.ProcessResources:','CraftingTimerTriggerCD','slotIndex = num4']),
]
# Raw authored components per source: CraftingAuthoring line, InventoryAuthoring line, ElectricityAuthoring line (if any).
RAWLINES={3905:(28914,436,None),4009:(32584,32429,62223),4015:(43743,28963,None),1200:(69100,43384,82374),4044:(69335,35714,None),4032:(74095,33476,None),4004:(75121,67869,None)}
GO={3905:'-3493388418596782133',4009:'-3870210828545831005',4015:'-5581143035285179682',1200:'-2416314992690142722',4044:'2415615054703119651',4032:'-621031106254733863',4004:'-1800024213135492140'}
lines=RAW.read_text(encoding='utf-8').splitlines();rawsha=sha(RAW)
checks=[];refs={}
def coderef(i,rel,a,b,anchors):
    path=CODE/rel;text=path.read_text(encoding='utf-8-sig').splitlines();segment='\n'.join(text[a-1:b])
    missing=[x for x in anchors if x not in segment]
    if missing:raise ValueError('anchor missing '+i+': '+repr(missing))
    checks.append({'id':i,'path':str(path).replace('\\','/'),'startLine':a,'endLine':b,'anchors':anchors,'status':'pass'})
    refs[i]={'id':i,'path':str(path).replace('\\','/'),'sha256':sha(path),'startLine':a,'endLine':b}
for r in REFS:coderef(*r)
for oid,(c,inv,el) in RAWLINES.items():
    for kind,n,cls in (('crafting',c,'CraftingAuthoring'),('inventory',inv,'InventoryAuthoring'),('electricity',el,'ElectricityAuthoring')):
        if n is None:continue
        row=json.loads(lines[n-1]);data=row['data']
        if data['m_GameObject']['m_PathID']!=GO[oid] or '"class":"'+cls+'"' not in lines[n-1].replace(': ',':'):raise ValueError('raw line mismatch '+str((oid,kind)))
        expect={}
        if kind=='crafting':
            if data['craftingType']!=1:raise ValueError('not ProcessResources '+str(oid))
            expect={'craftingType':1}
        if kind=='inventory':
            if (data['sizeX'],data['sizeY'],data['autoTransferEnabled'])!=(1,1,0):raise ValueError('inventory shape '+str(oid))
            expect={'sizeX':1,'sizeY':1,'autoTransferEnabled':0}
        i='raw-%s-%d'%(kind,oid)
        refs[i]={'id':i,'path':str(RAW).replace('\\','/'),'sha256':rawsha,'startLine':n,'endLine':n}
        checks.append({'id':i,'gameObject':GO[oid],'class':cls,'checked':expect,'status':'pass'})
# Every ProcessResources source also has AutomatedStorageAuthoring (mover feed/pickup path).
STORAGE={3905:55461,4004:48899,4009:45220,4015:20781,4032:16140,4044:29253,1200:658}
for oid,n in STORAGE.items():
    row=json.loads(lines[n-1])
    if row['data']['m_GameObject']['m_PathID']!=GO[oid] or '"class":"AutomatedStorageAuthoring"' not in lines[n-1].replace(': ',':'):raise ValueError('storage line mismatch '+str(oid))
    i='raw-storage-%d'%oid;refs[i]={'id':i,'path':str(RAW).replace('\\','/'),'sha256':rawsha,'startLine':n,'endLine':n}
    checks.append({'id':i,'gameObject':GO[oid],'class':'AutomatedStorageAuthoring','status':'pass'})
STORAGE_REFS=['mover-placed','storage-place']+['raw-storage-%d'%o for o in STORAGE]
# Per-source recipe material vs input-slot acceptance (static table).
objects=json.loads(OBJ.read_text(encoding='utf-8'));objsha=sha(OBJ);byid={}
for idx,x in enumerate(objects):byid.setdefault(x['objectId'],[]).append((idx,x))
OBJPATH=str(OBJ).replace('\\','/')
recipeTable={}
for oid,(c,inv,el) in RAWLINES.items():
    recipes=json.loads(lines[c-1])['data']['canCraftObjects'];slots=json.loads(lines[inv-1])['data']['slotRequirements']
    if len(slots)!=1:raise ValueError('slot requirement shape '+str(oid))
    acc={'ids':slots[0]['acceptsObjectIds'],'tags':slots[0]['acceptsObjectsWithTags']};rows=[]
    for r in recipes:
        found=byid.get(r['objectID'],[])
        if len(found)!=1:raise ValueError('product identity '+str(r['objectID']))
        idx,x=found[0];info=x['objectInfo'];req=info.get('requiredObjectsToCraft',[]);tag=info.get('craftingSettings',{}).get('canOnlyUseAnyMaterialsWithTag',0)
        accepted=None
        if len(req)==1:
            m=byid[req[0]['objectID']][0][1]['objectInfo']
            accepted=(req[0]['objectID'] in acc['ids']) if acc['ids'] else bool(set(m.get('tags',[]))&set(acc['tags']))
        rows.append({'product':r['objectID'],'objectsIndex':idx,'requiredObjectsToCraft':req,'canOnlyUseAnyMaterialsWithTag':tag,'craftingTime':info.get('craftingTime'),'clickGate':len(req)==1,'materialAcceptedByInputSlot':accepted})
        refs['obj-%d'%r['objectID']]={'id':'obj-%d'%r['objectID'],'path':OBJPATH,'sha256':objsha,'pointer':'/%d'%idx}
    recipeTable[str(oid)]={'inputSlotAccepts':acc,'recipes':rows}
for oid in CLICK:assert all(x['clickGate'] for x in recipeTable[str(oid)]['recipes']),oid
for oid in MOVE:assert all(x['materialAcceptedByInputSlot'] for x in recipeTable[str(oid)]['recipes']),oid
assert not any(x['clickGate'] for x in recipeTable['4044']['recipes'])
assert not any(x['materialAcceptedByInputSlot'] for x in recipeTable['3905']['recipes'])
coal=byid[5192][0];refs['obj-5192']={'id':'obj-5192','path':OBJPATH,'sha256':objsha,'pointer':'/%d'%coal[0]}
checks.append({'id':'recipe-material-vs-input-slot','table':recipeTable,'coalTags':coal[1]['objectInfo'].get('tags'),'clickSources':CLICK,'moveSources':MOVE,'status':'pass'})
# No other ProcessResources CraftingAuthoring exists in the fixed raw components.
pr=[n+1 for n,l in enumerate(lines) if '"class":"CraftingAuthoring"' in l.replace(': ',':') and json.loads(l)['data'].get('craftingType')==1]
if sorted(pr)!=sorted(v[0] for v in RAWLINES.values()):raise ValueError('ProcessResources population changed '+str(pr))
checks.append({'id':'process-resources-population','rawCraftingAuthoringLinesWithCraftingType1':sorted(pr),'count':len(pr),'status':'pass'})
graph=json.loads((F/'baseline-260/graph.json').read_text(encoding='utf-8'))['nodes']
def ev(node):
    n=graph[node]
    if not n.get('origin'):raise ValueError('no origin '+node)
    return {'nodeId':node,'origin':n['origin']}
def need(node):
    if node not in graph:raise ValueError('missing node '+node)
    return node
def binding(oid):
    s=graph['source:'+SRC[oid]]
    if (s['objectId'],s['variation'])!=(oid,0):raise ValueError('source identity '+str(oid))
    return {'sourceKey':SRC[oid],'objectId':oid,'variation':0,'role':'has_completion'}
def R(*ids):return [refs[i]['id'] for i in ids]
sel=lambda oid:need('supp:D::'+SRC[oid]+'#process_recipe_selection')
auto=lambda oid:need('supp:D::'+SRC[oid]+'#automatic_recipe_result')
prod=lambda oid:need('completion:full-d:production:supp:D::'+SRC[oid]+'#automatic_recipe_result')
def inv(oid):
    base='base:D::'+SRC[oid]+'#inventory_transfer'
    return need(base if base in graph else 'supp:D::'+SRC[oid]+'#inventory_transfer_extended')
A='completion:process-resources:recipe-activation-v1';M='completion:process-resources:material-move-v1';T='completion:process-resources:craft-timer-v1';O='completion:process-resources:output-v1'
NOTE_RUNTIME='실게임에서 이 사슬의 성공(재료가 입력 칸에 들어가고, 시간이 지나 출력 칸에 제품이 생기는 것)은 아직 관측하지 않았다. 정적 코드·원본 authoring 근거다.'
records=[
{'id':A,'group':'process_resources','kind':'action_completion','action':'process_recipe_activation',
 'sourceBindings':[binding(o) for o in CLICK],
 'links':[{'relation':'refines','to':sel(o)} for o in CLICK]+[{'relation':'excludes_source_claim','to':sel(4044)}]+[{'relation':'refines','to':need('rule:supp:D:process_recipe_selection/condition/conditions')},{'relation':'refines','to':need('rule:supp:D:process_recipe_selection/failure/failureBranches')}],
 'value':{
  'summary':'ProcessResources 시설 중 레시피 클릭 요청이 성립하는 3905·4004·4009·4015·4032에서 레시피 칸을 눌렀을 때 서버의 재료 준비 요청이 받아들여지기까지의 조건. 제품을 만들지 않는다. 4044·1200에는 클릭 경로가 없다.',
  'conditions':[
   {'rule':'열린 crafting handler의 craftingType이 ProcessResources(=1)이면 UIManager가 processResourcesCraftingUI를 연다. Simple 타입의 즉시 제작 화면(simpleCraftingUIContainer)이 아니다. CraftingCD.IsProcessAutoCrafter도 ProcessResources를 자동 가공 타입으로 분류한다.','refs':R('ui-manager','crafting-type','crafting-cd')},
   {'rule':'레시피 칸은 시설 표시 컴포넌트의 hideRecipes가 false일 때만 보인다. 6개 원본은 보이고, 같은 ProcessResources인 1200 EggIncubator는 hideRecipes=true라 이 경로가 없다(기존 판정 hidden_recipe_ui_container_only).','refs':R('pr-ui-show')},
   {'rule':'원본별 레시피 재료: 3905·4004·4009·4015·4032의 모든 레시피는 DB requiredObjectsToCraft가 1종이다. 4044 GlassSmelter의 유일한 레시피 1025 Glass는 requiredObjectsToCraft가 비어 있고 canOnlyUseAnyMaterialsWithTag=29인 태그 레시피라, 레시피 칸은 보여도 클라이언트 관문(RecipeInfo.requiredObjectsToCraft.Count==1)과 서버 관문(Length==1)이 모두 거짓이다. 4044의 입력은 플레이어 행동으로는 손으로 넣기(인벤토리 이동)이고, 그 밖에 자동 이동기 투입 경로가 있다(craft-timer-v1 입력 경로 조건).','refs':R('recipe-info','pr-ui-activate','server-activate','raw-crafting-4044','obj-1025','storage-place','raw-storage-4044')},
   {'rule':'클라이언트: RecipeSlotUI 클릭(canActivateCrafting일 때; 누르고 있으면 둘째부터 0.1초 간격 반복)이 ProcessResourcesCraftingUI.ActivateRecipeSlot(visibleSlotIndex, mod1)을 부른다. 레시피 재료가 정확히 1종이고 HasMaterialsInCraftingInventoryToCraftRecipe(가방 포함, 근처 상자 포함)가 참일 때만 UIInputAction.Craft와 CraftActionData(ActivateRecipeSlot, multiplier=1, bool0=mod1, int1=visibleSlotIndex)를 큐에 넣는다.','refs':R('recipe-slot-ui','pr-ui-activate','create-action')},
   {'rule':'서버 수용: InventoryUpdateSystem의 처리에서 읽기 전용 월드이고 요청 플레이어의 adminPrivileges<=0이면 건너뛴다. 재료를 찾을 inventories는 [요청 플레이어] 다음에 시설 위치 반경 10 안의 InventoryAutoTransferEnabledCD 보관함을 거리순 최대 20개 붙인 목록이다. 7개 ProcessResources 시설의 InventoryAuthoring.autoTransferEnabled는 0이라 시설 자신은 이 목록 대상이 아니다.','refs':R('craft-job-inputs','craft-job-dispatch','nearby-chests','nearby-chests-crafting','converter-auto-transfer','raw-inventory-4004')},
   {'rule':'서버 판정: GetRecipeInfo는 시설 CanCraftObjectsBuffer[recipeIndex]의 제품을 읽는다. 시설에 buffer가 없거나 index>=buffer 길이이거나 제품이 DB에 없으면 None이라 종료한다. 이 helper는 상한(bufferData.Length > index)만 검사하고 index>=0 하한 검사는 없다. 정상 UI는 보이는 레시피 칸 번호(0 이상)를 보낸다는 전제 위의 판정이다. DB requiredObjectsToCraft가 정확히 1종이고 HasMaterials(inventories, multiplier=1)가 참이어야 다음 단계로 간다. HasMaterials는 재료마다 max(1, round(amount×anyMaterialCostMultiplier))×multiplier를 inventories 합계와 비교한다.','refs':R('server-activate','get-recipe-info','has-materials')}],
  'flow':[{'rule':'RecipeSlotUI → ProcessResourcesCraftingUI.ActivateRecipeSlot → Create.ActivateRecipeSlot → UIInputAction.Craft → InventoryUpdateSystem.ProcessCraftingJob(case ActivateRecipeSlot) → InventoryUtility.ActivateRecipeSlot. 판정을 통과하면 같은 함수 안에서 재료 이동 단계(material-move-v1)가 이어진다.','refs':R('recipe-slot-ui','pr-ui-activate','create-action','craft-job-dispatch','server-activate')}],
  'results':[{'rule':'이 단계가 하는 일은 재료 이동 단계를 실행하게 하는 것뿐이다. 제품을 만들거나 타이머를 직접 시작하지 않고, 누른 레시피를 시설에 기록하는 필드 쓰기도 없다. 누른 레시피 index는 옮길 재료의 종류와 양, 출력 칸 정리 기준(제품 ID)을 정하는 데만 쓰인다. 실제로 만들어질 레시피는 타이머 단계에서 시설 인벤토리로 다시 고른다.','refs':R('server-activate','start-craft')},
   {'rule':'원본별 결과: 4004·4009·4015·4032는 다음 재료 이동 단계에서 재료가 입력 칸 0에 들어간다. 3905 SantasLittleHelper는 레시피 10200/10206의 DB 재료가 LumpOfCoal(5192, 태그 38)인데 입력 칸은 태그 17/18(조리 음식)만 받는다. 석탄이 충분하면 클릭은 받아들여지지만, 재료 이동 단계는 입력 칸의 다른 물건(넣어 둔 음식)과 출력 칸의 다른 물건을 플레이어 위치에 떨어뜨리기만 하고 석탄은 옮기지 못한다.','refs':R('server-activate','move-all-or-drop','object-valid','raw-crafting-3905','raw-inventory-3905','obj-10200','obj-10206','obj-5192')}],
  'failures':[{'rule':'hideRecipes 원본(1200)과 0종 재료 태그 레시피 원본(4044)은 클릭 경로가 없다. 재료가 1종이 아닌 레시피, 재료 부족(클라이언트나 서버 판정), buffer 없음·index>=buffer 길이·DB에 없는 제품, 읽기 전용 월드의 비관리자 요청, 플레이어 없음이면 아무것도 바뀌지 않는다. 이 경로에는 실패를 알리는 별도 응답 코드가 없다.','refs':R('pr-ui-show','pr-ui-activate','craft-job-inputs','server-activate','get-recipe-info','has-materials')}],
  'boundary':[NOTE_RUNTIME,'실제 DB 내용, 화면 visibleSlotIndex와 서버 buffer index의 일치, 네트워크 수용, anyMaterialCostMultiplier의 실제 값은 관측하지 않았다.','음수 recipeIndex에 대한 하한 방어가 GetRecipeInfo(InventoryUtility.cs:2218)에 없다. 비정상 요청이 음수 index를 보낼 때의 결과(예외 여부 등)는 확인하지 않았고, 그런 요청을 무해한 거절로 보지 않는다.'],
  'codeReferences':[refs[i] for i in ('storage-place','raw-storage-4044','recipe-info','raw-crafting-4044','obj-1025','move-all-or-drop','object-valid','raw-crafting-3905','raw-inventory-3905','obj-10200','obj-10206','obj-5192','ui-manager','crafting-type','crafting-cd','pr-ui-show','recipe-slot-ui','pr-ui-activate','create-action','craft-job-inputs','craft-job-dispatch','nearby-chests','nearby-chests-crafting','converter-auto-transfer','raw-inventory-4004','server-activate','get-recipe-info','has-materials','start-craft')],
  'sourceEvidence':[ev(sel(o)) for o in VISIBLE]+[ev(need('rule:supp:D:process_recipe_selection/condition/conditions')),ev(need('disposition:D:recipe_ui_craft:'+SRC[1200]))],
  'existingReuse':[sel(o) for o in CLICK]+['rule:supp:D:process_recipe_selection'],
  'decisionImpact':'레시피 클릭을 제작 완료로 보지 않는다. 플레이어가 레시피 클릭으로 재료를 넣을 수 있는 시설(4004·4009·4015·4032)과 플레이어 행동으로는 손으로 넣어야 하는 시설(3905·4044·1200)을 구별한다. 자동 이동기 공급은 별도 경로다. 클릭이 받아들여지는 조건(레시피 표시, 재료 1종, 가방·근처 상자 합계로 충분한 재료)과 실패하면 아무 변화가 없다는 점을 구별한다.'}},
{'id':M,'group':'process_resources','kind':'action_completion','action':'process_material_move',
 'sourceBindings':[binding(o) for o in MOVE],
 'links':[{'relation':'continues_after','to':A},{'relation':'excludes_source_claim','to':sel(3905)},{'relation':'refines','to':need('rule:supp:D:process_recipe_selection/result/result')}],
 'value':{
  'summary':'4004·4009·4015·4032에서 레시피 활성 판정을 통과한 뒤 서버가 입력 칸 0과 출력 칸을 정리하고 재료를 가방·근처 보관함에서 입력 칸 0으로 옮기는 단계.',
  'conditions':[{'rule':'recipe-activation-v1의 서버 판정을 통과했다. 옮길 양은 레시피 재료 amount이고 mod1이면 amount×10이다. 비용 배수와 multiplier는 옮길 양에 쓰이지 않는다.','refs':R('server-activate')}],
  'flow':[
   {'rule':'입력 칸 0에 레시피 재료가 아닌 물건이 있으면 MoveAllToOrDrop(시설, 0, 플레이어, 0, -1, 플레이어 위치)를 부른다. otherEndIndex가 -1이라 두 반복문은 돌지 않고 indexToHint도 기본 -1이므로, 정적 읽기로는 그 물건을 가방에 넣지 않고 플레이어 위치에 DropItem으로 모두 떨어뜨린다.','refs':R('server-activate','move-all-or-drop','drop-item')},
   {'rule':'출력 칸(CraftingCD.outputSlotIndex)에 레시피 제품이 아닌 물건이 있으면 같은 방식으로 떨어뜨린다. 같은 제품이면 그대로 둔다.','refs':R('server-activate','move-all-or-drop')},
   {'rule':'FindAndMoveTo(재료 objectID, variation 0, 시설, 시작 0, 크기 -1, inventories, 양, hint 0): inventories 순서(플레이어 → 가까운 보관함)대로 각 InventoryBuffer 구간의 칸을 앞에서부터 보고, objectID와 variation이 같은 칸에서 min(남은 요청량, 칸 수량)을 MoveAmount→TryMove로 시설에 보낸다.','refs':R('find-and-move','try-move')},
   {'rule':'TryMove의 목적지는 TryFindSlotToAddTo(시설, hint 0, end -1)이다. 7개 시설의 InventoryAuthoring은 1×1이고, 출력 칸은 InventoryConverter가 InventoryBuffer 구간 밖에 덧붙인 칸이라 대상은 입력 칸 0뿐이다. 칸 0이 slotRequirements를 만족하고, 비었거나 같은 물건·variation으로 9999 미만이어야 한다. 실제 옮기는 양은 min(출처 칸 수량, 요청량, 9999-칸 0 수량)이다.','refs':R('try-find-slot','try-move','move-amount-cap','converter-output-slot','raw-inventory-4004')}],
  'results':[{'rule':'4004·4009·4015·4032는 모든 레시피 재료가 입력 칸 slotRequirements(acceptsObjectIds)에 들어 있다. 입력 칸 0에 재료가 레시피 1회분(mod1이면 10회분)까지 쌓이고 출처 칸은 그만큼 줄어든다. 이 인벤토리 버퍼 변경이 자동화 타이머 단계(craft-timer-v1)를 다시 평가하게 한다. 이 단계에서 제품은 생기지 않는다. 다른 물건이 있던 입력·출력 칸은 비워지고 그 물건은 플레이어 위치에 떨어진다(정적 읽기).','refs':R('server-activate','find-and-move','move-all-or-drop','trigger-changed')}],
  'failures':[
   {'rule':'mod1(10회분)이어도 서버는 1회분 재료만 확인한다. 10회분이 없으면 있는 만큼만 옮긴다(부분 이동).','refs':R('server-activate','has-materials')},
   {'rule':'입력 칸 0이 9999에 찼거나 slotRequirements가 그 재료를 받지 않으면 TryFindSlotToAddTo가 -1을 돌려 그 출처 칸은 옮기지 않는다. FindAndMoveTo는 옮김 성공과 상관없이 남은 요청량을 먼저 줄이므로, 실패한 몫을 다른 보관함에서 다시 채우지 않는다.','refs':R('find-and-move','try-find-slot','try-move')},
   {'rule':'3905(이 기록에 묶지 않음): 클릭이 받아들여져도 DB 재료 LumpOfCoal(태그 38)이 입력 칸 태그 요구 17/18을 만족하지 못해 ObjectIsValidToPutInInventory가 FailedRequirement다. 석탄은 옮겨지지 않고, 입력 칸에 있던 음식과 출력 칸의 다른 물건만 플레이어 위치에 떨어진다.','refs':R('server-activate','move-all-or-drop','object-valid','try-find-slot','raw-inventory-3905','obj-10200','obj-10206','obj-5192')},
   {'rule':'variation이 0이 아닌 같은 objectID 재료는 옮기지 않는다(ObjectDataCD variation 기본값 0으로 찾음). 서버가 확인한 양에는 비용 배수가 들어가지만 옮기는 양에는 들어가지 않는다.','refs':R('server-activate','find-and-move','has-materials')}],
  'boundary':[NOTE_RUNTIME,'떨어진 물건의 생성은 isFirstTimeFullyPredictingTick일 때 ECB로 예약된다. 실제 떨어짐·다시 줍기, 잠금 칸, 네트워크 예측은 관측하지 않았다. craft-diagnosis의 \'다른 광석은 가방으로 돌려준다\'는 설명과 달리 정적 코드는 떨어뜨림으로 읽힌다. 실게임으로 확인해야 한다.'],
  'codeReferences':[refs[i] for i in ('object-valid','raw-inventory-3905','obj-10200','obj-10206','obj-5192','raw-inventory-4009','raw-inventory-4015','raw-inventory-4032','server-activate','move-all-or-drop','drop-item','find-and-move','try-move','try-find-slot','move-amount-cap','converter-output-slot','raw-inventory-4004','trigger-changed','has-materials')],
  'sourceEvidence':[ev(need('rule:supp:D:process_recipe_selection/result/result'))]+[ev(sel(o)) for o in MOVE+[3905]],
  'existingReuse':['rule:supp:D:process_recipe_selection',A],
  'decisionImpact':'넣기 성공을 입력 칸 0의 재료 수량으로 확인하고, 부분 이동·다른 물건이 떨어지는 부작용·9999 상한을 구별한다.'}},
{'id':T,'group':'process_resources','kind':'action_completion','action':'process_craft_timer',
 'sourceBindings':[binding(o) for o in ALL],
 'links':[{'relation':'continues_after','to':M}]+[{'relation':'refines','to':auto(o)} for o in ALL]+[{'relation':'reuses_consumer','to':prod(o)} for o in ALL]+[{'relation':'clarifies_open_statement','to':inv(o)} for o in ALL],
 'value':{
  'summary':'입력 칸이 바뀐 ProcessResources 시설(7개)이 시설 인벤토리만으로 만들 레시피를 고르고 1초 단위 타이머로 가공 시간을 세는 단계.',
  'conditions':[
   {'rule':'시설에 AutomatedCrafterAuthoring이 있고 PugAutomationCD.type에 Crafter가 있으며 isActive이면 PugAutomationInstantiateSystem이 CraftingTimerSlotBuffer 칸마다 crafter 엔티티(CrafterForSlotCD.slotIndex, CraftingTimerTriggerCD)를 만든다. ProcessResources는 Simple·Cooking과 같은 crafterArchetype이다.','refs':R('automation-crafter')},
   {'rule':'isActive = ElectricityCD가 없거나 hasEnoughElectricityToPowerStuff. authoring으로는 4009 TableSaw와 1200 EggIncubator에 ElectricityAuthoring이 있고(전력 필요), 3905·4004·4015·4032·4044에는 없다. 전기 변환과 충분 기준은 기존 production 완결 기록을 따른다.','refs':R('automation-active','pr-ui-electricity','raw-electricity-4009','raw-electricity-1200','raw-crafting-4004')},
   {'rule':'입력 경로(플레이어 행동): 4004·4009·4015·4032는 레시피 클릭(앞 단계) 또는 손으로 넣기, 3905·4044·1200은 손으로 넣기(인벤토리 이동)로 입력 칸을 채운다. 3905·4044는 태그 레시피라 CanCraft가 태그 물건 하나 이상으로 판정한다.','refs':R('can-craft','raw-crafting-3905','raw-crafting-4044','obj-1025','obj-10200')},
   {'rule':'입력 경로(자동 이동기): 7개 원본 모두 AutomatedStorageAuthoring이 있다. 이동기가 이동을 마치면 도착 위치에 PlacedAtPosition으로 등록되고, 그 위치의 StorageCD에 대해 PlaceInStorageJob이 물건이 slotRequirements를 만족하면 MoveOrDropAmount로 시설 인벤토리에 넣고, 만족하지 않으면 그 자리에 떨어뜨리는 요청을 만든다. 이 경로의 조건(이동기 배치·방향·전력)과 결과는 이 기록에서 판정하지 않았고 실게임으로 관측하지 않았다. 어느 경로로 입력 칸이 바뀌어도 아래 평가 계기는 같다.','refs':R(*STORAGE_REFS)},
   {'rule':'평가 계기: PugAutomationStartCraftSystem은 BigEntityCraftingDataChangedTriggerCD 변경 필터로만 실행된다. TriggerCrafterChangedSystem은 시설의 PugAutomationCD·ContainedObjectsBuffer·ObjectDataCD chunk가 바뀌거나 순서가 바뀌면 그 trigger를 건드린다. 재료 이동 단계나 손으로 넣기·꺼내기가 이 계기가 된다.','refs':R('start-craft-query','trigger-changed')}],
  'flow':[
   {'rule':'레시피 선택: CanCraftObjectsBuffer를 앞에서부터 보며 시설 자신의 인벤토리(inventoryEntities=[시설])만으로 CanCraft가 참인 첫 레시피 i를 고른다. 플레이어가 누른 레시피가 아니라 이 순서와 시설에 든 것이 정한다.','refs':R('start-craft','can-craft')},
   {'rule':'고를 레시피가 없으면 timeLeftToCraft=0, currentlyCraftingIndex=-1. i가 이전과 다르면 먼저 timeLeftToCraft를 InstantCrafting이면 0, 아니면 제품 DB craftingTime으로 정하고, 그 뒤 레시피 craftingTimeOverride가 양수이면 그 값으로 덮어쓴다(InstantCrafting이어도 양수 override가 이긴다). 그리고 currentlyCraftingIndex=i. i가 이전과 같으면 남은 시간을 유지한다.','refs':R('start-craft')},
   {'rule':'i≠-1, 진행 중인 타이머 없음, isActive이면 StartTimer(min(1초, 남은 시간)). 타이머가 끝날 때마다 PugAutomationCraftingSystem은 비활성이면 감소 없이 끝내고, 아니면 timeLeftToCraft를 1 줄여 0보다 크면 1초 타이머를 다시 건다. 0 이하가 되면 출력 단계(output-v1)를 실행한다.','refs':R('start-craft','craft-finish')}],
  'results':[{'rule':'재료가 있고 출력 칸이 받을 수 있는 동안 남은 시간이 1초씩 줄고, 약 craftingTime번의 만료 뒤 출력 단계가 실행된다. 원본별 레시피와 craftingTime은 기존 production 완결의 resolvedRecipeInputs에 있다(예: 4004의 제품 1001은 7).','refs':R('start-craft','craft-finish')}],
  'failures':[
   {'rule':'CanCraft가 참인 레시피가 없으면(재료 부족·다른 재료, 출력 칸이 막힘) 타이머를 시작하지 않고 진행을 0/-1로 되돌린다.','refs':R('start-craft','can-craft')},
   {'rule':'진행 중 재료를 빼거나 바꿔서 첫 CanCraft 레시피가 없어지면 0/-1로, 다른 레시피로 바뀌면 새 레시피 시간으로 초기화된다(진행 손실). 재료를 일부 빼도 같은 첫 레시피가 계속 선택되면 남은 시간은 보존된다. 이미 걸린 타이머가 끝나도 index가 0보다 작으면 아무것도 만들지 않는다.','refs':R('start-craft','craft-finish')},
   {'rule':'전력이 필요한 시설(4009·1200)에서 전력이 모자라면 isActive=false라 타이머를 시작하지도 줄이지도 않는다.','refs':R('automation-active','start-craft','craft-finish')}],
  'boundary':[NOTE_RUNTIME,'tick rate 환산, ECB 지연, 자동화 재구성 때 남은 시간 보존 여부, 실제 회로 전력은 관측하지 않았다. 실제 경과 시간은 재지 않았다.'],
  'codeReferences':[refs[i] for i in tuple(STORAGE_REFS)+('obj-1025','obj-10200','automation-crafter','automation-active','pr-ui-electricity','raw-electricity-4009','raw-electricity-1200','raw-crafting-4004','start-craft-query','trigger-changed','start-craft','can-craft','craft-finish')]+[refs['raw-crafting-%d'%o] for o in ALL if o!=4004],
  'sourceEvidence':[ev(prod(o)) for o in ALL]+[ev(auto(o)) for o in ALL]+[ev(inv(o)) for o in ALL],
  'existingReuse':[prod(o) for o in ALL]+['rule:supp:D:automatic_recipe_result'],
  'decisionImpact':'넣은 뒤 바로 제품이 생긴다고 보지 않고 기다림이 필요하다고 판단한다. 기다리는 동안 재료를 빼거나 바꿔 첫 가능 레시피가 없어지거나 달라지면 진행이 초기화되고(같은 레시피가 유지되면 보존), 전력 필요 시설은 전력이 있어야 진행된다.'}},
{'id':O,'group':'process_resources','kind':'action_completion','action':'process_craft_output',
 'sourceBindings':[binding(o) for o in ALL],
 'links':[{'relation':'continues_after','to':T}]+[{'relation':'reuses_consumer','to':prod(o)} for o in ALL]+[{'relation':'clarifies_open_statement','to':inv(o)} for o in ALL],
 'value':{
  'summary':'타이머가 끝난 ProcessResources 시설이 시설 인벤토리의 재료를 소비해 출력 칸에 제품을 쓰는 단계와 그 실패 분기.',
  'conditions':[{'rule':'craft-timer-v1에서 timeLeftToCraft가 0 이하가 되었고 currentlyCraftingIndex가 CanCraftObjectsBuffer 범위 안이며 시설이 활성이다.','refs':R('craft-finish')}],
  'flow':[
   {'rule':'InventoryUtility.Craft(시설, inventoryEntities=[시설], CanCraftObjectsBuffer[current], additionalFreeAmount=0, useCraftingCostMultiplier=false, playerEntity 없음)를 부른다.','refs':R('craft-finish','craft')},
   {'rule':'Craft는 먼저 CanCraft(배수 1)를 다시 본다. 출력 칸은 비었거나, 같은 제품이며 제품이 stackable이고 수량이 9999 미만이어야 한다. canOnlyUseAnyMaterialsWithTag가 있으면 그 태그 재료가 하나 이상, 없으면 requiredObjectsToCraft의 각 재료가 시설 인벤토리 전체에 round(amount)×레시피 amount만큼 있어야 한다.','refs':R('can-craft')},
   {'rule':'통과하면 소비 분기가 둘로 갈린다. 제품 DB의 canOnlyUseAnyMaterialsWithTag가 없으면 requiredObjectsToCraft의 각 재료를 round(amount×1)×레시피 amount만큼 시설 인벤토리에서 ConsumeObject로 소비한다(4004·4009·4015·4032·1200). 태그가 있으면 required 목록은 쓰지 않고 그 태그의 물건을 round(1×1)×레시피 amount, 곧 레시피 amount 1인 이 원본들에서는 1개를 ConsumeObjectWithTag로 소비한다(3905·4044). 그다음 출력 칸에 제품을 새로 쓰거나 더한다. stackable이면 initialAmount×레시피 amount, 아니면 initialAmount다. playerEntity가 없으므로 Crafting 스킬은 오르지 않는다.','refs':R('craft')},
   {'rule':'3905 SantasLittleHelper: 같은 제품 DB(10200/10206)에 requiredObjectsToCraft=[LumpOfCoal×1]/[×2]와 canOnlyUseAnyMaterialsWithTag=17/18이 함께 있다. 레시피 칸 표시와 클릭 관문은 석탄 required 목록을 쓰지만, 자동 CanCraft와 Craft는 태그를 먼저 보고 시설 안의 조리 음식(태그 17/18) 1개를 소비한다. 그래서 UI에 보이는 석탄 비용은 자동 가공의 실제 소비가 아니다. 음식은 플레이어 손 이동이나 자동 이동기로 입력 칸 0에 들어갈 수 있다. 출력 칸은 입력 0 뒤에 붙은 1번 칸이다.','refs':R('can-craft','craft','pr-ui-activate','server-activate','converter-output-slot','storage-place','raw-crafting-3905','raw-inventory-3905','raw-storage-3905','obj-10200','obj-10206','obj-5192')},
   {'rule':'만든 뒤 같은 레시피로 CanCraft가 다시 참이면 남은 시간을 InstantCrafting이면 0, 아니면 DB craftingTime으로 정한 뒤 양수 override로 덮어쓰고, 타이머를 다시 건다(연속 가공). 아니면 0/-1로 멈춘다.','refs':R('craft-finish')}],
  'results':[{'rule':'출력 칸의 제품 수량이 늘고 시설에 든 재료가 1회분 줄어든다. 제품은 출력 칸에 남고 이 단계는 그것을 가방이나 보관함으로 옮기지 않는다. 플레이어 행동으로는 인벤토리 이동으로 꺼낸다(기존 inventory_transfer 계열 기록). 이와 별도로 자동 이동기 경로가 있다: CanPickup은 출력 칸이 있는 crafting 시설에서 출력 칸이 비어 있지 않을 때 참이고, 그때 PlaceInStorageJob이 그 위치를 집어 갈 수 있는 저장소로 등록한다. 이동기의 실제 집기 조건과 결과는 이 기록에서 판정하지 않았고 실게임으로 관측하지 않았다.','refs':R('craft','converter-output-slot','can-pickup','storage-place')}],
  'failures':[
   {'rule':'출력 칸에 다른 물건이 있거나, 같은 제품이 9999 이상이거나, 제품이 stackable이 아닌데 출력 칸이 차 있으면 CanCraft가 거짓이다. 제품은 생기지 않고 재료는 남으며 진행은 -1로 멈춘다. 출력 칸을 비우는 등 버퍼가 바뀌면 타이머 단계에서 다시 평가한다.','refs':R('can-craft','craft-finish','start-craft')},
   {'rule':'타이머 도중 재료가 모자라게 되면 Craft가 거짓이라 제품이 없다.','refs':R('craft','craft-finish')},
   {'rule':'출력 칸 9999 검사는 더하기 전 수량만 본다. 더한 결과가 9999를 넘는지는 검사하지 않는다.','refs':R('can-craft','craft')}],
  'boundary':[NOTE_RUNTIME,'실제 DB 제품·수량, ECB, 네트워크 동기화는 관측하지 않았다. 표시 효과(출력 수량 변화에 따른 소리·빛)는 이 기록 범위 밖이다.'],
  'codeReferences':[refs[i] for i in ('craft-finish','craft','can-craft','start-craft','converter-output-slot','can-pickup','storage-place','pr-ui-activate','server-activate','raw-crafting-3905','raw-inventory-3905','raw-storage-3905','obj-10200','obj-10206','obj-5192')],
  'sourceEvidence':[ev(prod(o)) for o in ALL]+[ev(inv(o)) for o in ALL],
  'existingReuse':[prod(o) for o in ALL]+['rule:supp:D:automatic_recipe_result'],
  'decisionImpact':'제품 수령은 출력 칸 수량으로 확인하고 꺼내기를 따로 해야 한다고 판단한다. 출력 칸이 막히면 가공이 멈춘다는 실패를 구별한다.'}},
]
for r in records:
    for f in ('conditions','flow','results','failures','boundary'):assert f in r['value']
    for field in ('conditions','flow','results','failures'):
        for item in r['value'][field]:
            for x in item['refs']:assert x in refs and x in {c['id'] for c in r['value']['codeReferences']},(r['id'],x)
(P/'supplement.json').write_bytes(encode({'records':records,'runtimeTested':False,'wholeScopeComplete':False}))
(P/'static-check.json').write_bytes(encode({'status':'pass','checks':checks,'runtimeTested':False}))
print(json.dumps({'records':len(records),'codeRefs':len(refs),'checks':len(checks),'supplementSha256':sha(P/'supplement.json')}))
