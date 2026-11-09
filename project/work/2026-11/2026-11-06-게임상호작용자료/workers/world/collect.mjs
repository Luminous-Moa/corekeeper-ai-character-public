import {readFileSync, writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {dirname, resolve} from 'node:path';
const here=dirname(fileURLToPath(import.meta.url));
const root=resolve(here,'../..');
const input=JSON.parse(readFileSync(resolve(root,'input-world.json'),'utf8'));
const file='C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerController.cs';
const file2='C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlaceObjectSlot.cs';
function source(path,line,endLine){const b=readFileSync(path);return {file:path,line,endLine,sha256:createHash('sha256').update(b).digest('hex'),excerpt:b.toString('utf8').split(/\r?\n/).slice(line-1,endLine).map(s=>s.trim()).join(' ')};}
const sources=[source(file,3916,3919),source(file2,37,46),source(file2,52,64),source(file2,79,83),source(file2,95,98)];
const rule={id:'world-placeable-placement',objectType:'PlaceablePrefab',selection:'input-world.json에서 raw.type과 type이 모두 PlaceablePrefab이고 noPrefab 표시가 없는 key',exclusions:'noPrefab 표시, raw 또는 타입 정보 부재·불일치; 세부 프리팹 기능과 실제 배치 성공은 제외',description:'타입을 배치 슬롯으로 라우팅하고 조건부 배치 상태에 진입하는 코드 후보. 버전 대응과 실행은 미확인.',sources};
const candidates=[
 {id:'world-chest-use-candidate',description:'Chest.Use는 플레이어가 있을 때 인벤토리 핸들러를 활성화하고 상자 UI를 연다. 호출 연결과 ID별 프리팹은 미확인.',sources:[source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/Chest.cs',21,30)]},
 {id:'world-crafting-use-candidate',description:'CraftingBuilding.Use는 제작 핸들러를 활성화하고 인벤토리 UI를 연다. 호출 연결과 ID별 프리팹은 미확인.',sources:[source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/CraftingBuilding.cs',204,208)]},
 {id:'world-sitting-candidate',description:'Sitting 상태는 상호작용 소비 시 조건부로 걷기 상태로 복귀하며 SittableObject의 좌석 위치를 참조한다. 진입과 ID별 연결은 미확인.',sources:[source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerState/Sitting.cs',52,65),source('C:/Dev/_ref/corekeeper-decomp/Pug.Other/PlayerState/Sitting.cs',89,95)]},
 {id:'world-tile-placement-candidate',description:'배치 프리팹에 tile 컴포넌트가 있을 때 AddTile과 인벤토리 변경을 요청한다. ID별 tile 컴포넌트 보유 여부는 미확인.',sources:[source(file2,105,113)]},
 {id:'world-gate-visual-candidate',description:'Gate는 variation에 따라 시각 표현을 변경한다. 플레이어 조작 경로와 개별 프리팹 연결은 미확인.',sources:[source('C:/Dev/_ref/corekeeper-decomp/Pug.Objects/Gate.cs',66,86)]}
].map(x=>({...x,status:'unmapped_code_candidate',objectTypeCondition:'특정 ObjectType만으로 적용하지 않음',exclusions:'ID·variation과 프리팹 구성요소 및 호출 연결 미확인; 어떤 입력 key에도 미적용'}));
const records=input.map(x=>{const valid=x.type==='PlaceablePrefab' && x.raw?.type==='PlaceablePrefab' && !x.noPrefab && !x.raw?.noPrefab; const interactions=[...(x.interactions||[])]; if(valid)interactions.push({verb:'place',label:'배치하기',objectRole:'target',evidenceLevel:'code_candidate',applicability:'덤프의 해당 key의 ObjectType이 PlaceablePrefab인 경우 배치 슬롯 경로 후보; 프리팹 연결 및 빌드 일치와 실행은 미확인',conditions:{input:'두 번째 상호작용을 누르고 마우스에 아이템이 없어야 함',placement:'canPlaceObject 및 CanPlaceItem 검사 통과',inventory:'슬롯 아이템 1개에 대한 CanConsumeEntityInSlot 검사 통과',other:'위치·프리팹 및 세계 상태에 따른 추가 조건 적용'},result:{state:'PlaceObject 상태에 진입; 실제 배치 완료는 이 근거만으로 미확인'},ruleId:rule.id,sources}); return {key:x.key,interactions,unresolved:[...(x.unresolved||[]),valid?'개별 프리팹 구성요소와 연결이 없어 상자 열기·문 조작·파괴 등 세부 행동 및 실제 배치 성공 미확인':'배치 슬롯 적용에 필요한 타입 또는 프리팹 정보 미확인; 개별 행동 미확인'],reviewMethod:'고정 입력의 기존 관계 보존; 타입별 코드 경로와 조건을 대조'};});
if(new Set(input.map(x=>x.key)).size!==input.length || new Set(records.map(x=>x.key)).size!==records.length || records.some((x,i)=>x.key!==input[i].key)) throw Error('key coverage mismatch');
const result={group:'world',records,rules:[rule,...candidates],coverage:{inputCount:input.length,outputCount:records.length,unresolvedCount:records.filter(x=>x.unresolved.length).length}};
writeFileSync(resolve(here,'results.json'),JSON.stringify(result,null,2)+'\n');

