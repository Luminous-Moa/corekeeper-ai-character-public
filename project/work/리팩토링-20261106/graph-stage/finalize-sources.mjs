import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const S=path.dirname(fileURLToPath(import.meta.url)),G=S+'/work/지식그래프',P=S+'/work/2026-11/2026-11-05-지식그래프-개편';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,''));const write=(p,v)=>fs.writeFileSync(p,JSON.stringify(v,null,2)+'\n');
const additions={
 'F.goal-cmd':['AIGoalKinds.Of','AIGoalKinds.IsHostWord','AIGoalFeatures.TryDone','AIGoalFeatures.StallNote'],
 'F.llm-goal':['AIGoalKinds.TryWire'],
 'F.exp-memory':['AIGoalKinds.ExperienceKey'],
 'F.candidates':['AIGoalFeatures.AddCandidates'],
 'F.base-reach':['AIEdge.AssessThreatWithinRange','AIEdge.ObserveThreat','AIEdge.ObserveAnyThreat','AIEdge.LastAssessedCannotCome'],
 'F.base-repair':['AIEdge.WallFact','AIBaseGuard.WallFact'],
 'F.my-door':['AIDoorFacts.InWindowLastByTile','AIDoorFacts.FirstAt','AIDoorFacts.OpenTilesExcept'],
 'F.place-grid':['AIWorldFacts.IsKnown','AIWorldFacts.IsExcludedPlacedEntity','AIWorldFacts.IsPlacedObjectOrDoor'],
 'F.tunnel-detour':['AIApproach.FirstCached','AIApproach.FirstImmediate','AIApproach.WalkCandidate']
};
const f=G+'/기획/기능.md';let feature=fs.readFileSync(f,'utf8'),current='';feature=feature.split('\n').map(l=>{const m=l.match(/^### (F\.[\w-]+)/);if(m)current=m[1];if(l.startsWith('- 코드 앵커:')&&additions[current])return l.replace(/\r$/,'')+', '+additions[current].join(', ');return l;}).join('\n');
feature += '\n\n## 2026-11-06 책임 분리 후보(902721e)\n\n기능 ID와 제품 정책은 유지한다. 목표 메타데이터/기능 진입, 후보의 의미 필드, 문·물체 사실 조회, 접근 탐색과 위협 관측/기록 소유를 나누었다. AIBodySkills의 몸·관측·길찾기·기술은 각 형식 파일, AIExplore의 기억·떨굼·물체·굴길은 각각의 파일로 이동했다. 명령은 AIBody.Commands 및 AIPlayerSpikeMod.Chat, 저장은 AICharacterStore/AIWorldSettings, 위협은 AIEdge.Reach가 진입점이다. 기존 확인한 선과 실게임 증거는 이 후보의 실행 확인으로 승격하지 않는다. 정확한 이동 목록은 work/리팩토링-20261106/09-move-manifest.md와 14-graph-stage-result.md에 있다.\n';fs.writeFileSync(f,feature);
const dev=read(G+'/graph/graph.json'),actual=new Set(dev.nodes.map(n=>n.id));
const inf=read(P+'/B/validation-review.json'),validation=read(P+'/B/validation.json');
const reasons={
 'hook:AIPlayerSpikePatches.CharacterDataPostfix':'AIPlayerSpikePatches.cs:107; AI playerGuid guard and character replacement body retained; registration AIPlayerSpikeMod.cs:519-520.',
 'cli:HeadlessSdk.CreateMod':'HeadlessSdk.cs:67-90; settings/create reflection is an external SDK boundary.',
 'hook:ThinClientDriverConstructor.CreateServerDriver':'AIPlayerSpikePatches.cs:51-54; interface entry throws InvalidOperationException.',
 'hook:AIPlayerSpikeMod.EarlyInit':'AIPlayerSpikeMod.cs:193-197; IMod entry logs policy, no gameplay state change inferred.',
 'hook:AIPlayerSpikePatches.SpawnLambdaPostfix':'AIPlayerSpikePatches.cs:99-102 clears InSpawnLambda; registration AIPlayerSpikeMod.cs:517-518.',
 'hook:AIPlayerSpikePatches.ChatSendPrefix':'AIPlayerSpikePatches.cs:71-83; guard, TryHandleChatCommand forwarding, exception fallback; registration AIPlayerSpikeMod.cs:522-523.',
 'cli:HeadlessSdk.BuildAndInstall':'HeadlessSdk.cs:107-145; settings/reflection/manifest code preserved; external BuildMod body is not executed by graph extraction.',
 'hook:AIPlayerSpikeMod.Shutdown':'AIPlayerSpikeMod.cs:212 onwards; DisposeAll and Harmony.UnpatchSelf boundary retained.'
};
for(const sample of inf.samples){sample.reason=reasons[sample.id];sample.handlers=validation.samples.find(x=>x.id===sample.id).handlers;}
inf.recheck += ' 2026-11-06 staged re-extraction 902721e: reread all eight declarations and patch registration sources. Four patch/interface declarations moved into AIPlayerSpikePatches.cs, registrations now 517-523; other four remain. No runtime verification is claimed.';inf.currentSourceCommit='902721ec505df42d74d0b2d245ed34a049faadad';write(P+'/B/validation-review.json',inf);
for(const p of [G+'/README.md',G+'/제품그래프-사용법.md',P+'/B/README.md'])fs.appendFileSync(p,'\n\n## 2026-11-06 격리 리팩토링 후보\n\n이 사본의 코드 기준은 `902721ec505df42d74d0b2d245ed34a049faadad`다. 공식 원본과 본 저장소 HEAD는 a07166f로 보존한다. 개발 그래프는 후보 worktree 85개 C# 소스, 제품 그래프는 Git blob 고정 snapshot을 읽는다. 새로운 제품 기능은 추가하지 않고 심볼·책임 위치만 갱신했다. 과거 화면 설치 기준 76563bf와 역사적 추론 e82eb87은 유지하며 새 실행 증거로 승격하지 않는다. 현재 상태와 검증 결과, 병합 승인 뒤 반영 순서는 `work/리팩토링-20261106/14-graph-stage-result.md` 참조.\n');
console.log('Updated nine existing feature anchor groups and eight source review samples; runtime evidence retained.');
