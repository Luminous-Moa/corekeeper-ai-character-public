import assert from 'node:assert/strict';
import {read,live,write,indexes,readSources,featureTree,follow} from './live-lib.mjs';
const g=read(live('D/graph.json')),ix=indexes(g);
const code=name=>{const matches=g.nodes.filter(n=>n.type==='코드' && n.id.endsWith('#'+name));assert.equal(matches.length,1,'unique qualified symbol '+name);return matches[0].id;};
function shortest(from,to){const queue=[{id:from,depth:0}],parent=new Map([[from,null]]);while(queue.length){const x=queue.shift();if(x.id===to){const result=[];for(let current=to;parent.get(current);){const e=parent.get(current);result.unshift(e);current=e.from;}return result;}if(x.depth>=16)continue;for(const e of ix.out.get(x.id)||[])if((['유저 활동→화면요소','화면요소→기능','기능→코드','read'].includes(e.type)||follow(e))&&!parent.has(e.to)){parent.set(e.to,e);queue.push({id:e.to,depth:x.depth+1});}}return null;}
const status=code('AIPlayerSpikeMod.StatusAll()'),members=code('AIWorldSettings.Members(string)'),data='data:Assets/AIPlayerSpike/Scripts/AIWorldSettings.cs:p';
const specifications=[
 {question:'Claude가 켜짐/들어와 있음으로 보이는 값은 어디서 오는가?',elements:['CKAI-02#3','CKAI-02#2-7'],feature:'slash:ai',targets:[code('AIPlayerSpikeMod.TryHandleChatCommand(string)'),status,code('AIPlayerSpikeMod.StageText()'),members,code('AIWorldSettings.PathFor(string)')],data,answer:'인자 없는 /ai는 전체 상태 분기로 들어간다. StatusAll은 AIAgent 이름, 월드 설정 Members의 켜짐 여부, 현재 단계의 StageText를 합친다. Members의 파일 데이터 p는 PathFor가 정하는 AIPlayers/<world>/settings.json이다. A1·C3 화면은 Claude 켜짐/들어와 있음에 대응한다. 현재 설정 파일 내용은 읽지 않았고 함수 실행 trace는 없다.'},
 {question:'상태 뒤 /없음은 동료가 아무 행동도 안 한다는 뜻인가?',elements:['CKAI-02#2-7'],feature:'slash:ai',targets:[status],answer:'코드에서 Walking일 때 붙이는 Goal.Text 자리다. 이 문자열만으로 현재 기술·자기 방어·최근 로그의 발생 시점까지 해석할 수 없다. C3 사용자는 필드 의미와 현재 목표를 확인하지 못했다. 화면 관측과 소스 필드 대응을 구별한다.'},
 {question:'상태 응답은 어떻게 화면에 표시되며 게임 채팅 에코도 같은 결과인가?',elements:['CKAI-02#2-6','CKAI-02#2-7'],feature:'slash:ai',targets:[code('AIPlayerSpikeMod.Reply(string, ChatKind, string)'),code('AIPlayerSpikeMod.Update()'),code('AIPlayerSpikeMod.FlushReplies()')],answer:'상태 결과는 Reply 큐에 넣고 다음 Update가 FlushReplies에서 게임 ChatWindow.RenderText를 reflection으로 부른다. slash:ai 직접 호출 트리와 다음 프레임 전달은 구별하며 가짜 call/render 간선을 만들지 않았다. Luminous: /ai 에코는 게임 본체 출력으로 미매칭이다.'},
 {question:'플레이어 관리에서 AI 목표를 찾거나 최근 공격 메시지를 Claude 행동으로 확정할 수 있는가?',elements:['CKAI-03#7','CKAI-04#2-1','CKAI-04#3-1','CKAI-02#2-2'],targets:[],answer:'A3의 플레이어 관리는 관리자/차단 목록으로 목적에 맞지 않았다. 이 화면의 게임 본체 구현은 기존 모드 추출 범위 밖이다. 최근 공격 메시지는 보였지만 AI 접두어만으로 특정 동료와 현재 목표를 확정하지 않는다. 전투 실행판 전체도 기준판 코드 그래프와 다르다.'}
];
const results=specifications.map(s=>({...s,activity:'CKAI-ACT-01',elementSources:s.elements.map(id=>({id,sources:readSources(ix.nodes.get(id),g),boundary:ix.nodes.get(id).mappingBoundary||null})),paths:s.targets.map(id=>({id,path:shortest('CKAI-ACT-01',id),sources:readSources(ix.nodes.get(id),g)})),dataPath:s.data?shortest('CKAI-ACT-01',s.data):null,displayHook:s.question.includes('표시되며')?featureTree(g,'hook:AIPlayerSpikeMod.Update').lines.filter(l=>/Update|FlushReplies|경계/.test(l)):undefined,evidence:[live('A1/04-ai-help.png'),live('C3/flow-check.md'),live('C/화면-기능-원문.md')]}));
assert(results[0].dataPath?.some(e=>e.type==='read'));
assert(results[0].paths.every(p=>p.path));
write(live('D/questions.json'),{depthLimit:16,questions:results,judgment:'문서/기준 코드 원문 및 A1/C3 실제 PNG 대조. 네트워크 요청·함수 trace·현재 저장 파일 확인은 아님'});
console.log(JSON.stringify(results.map(r=>({question:r.question,sourceNodes:r.paths.length,dataPath:r.dataPath?.map(e=>e.to),unmatched:r.elementSources.filter(s=>s.boundary).map(s=>s.id)})),null,2));

