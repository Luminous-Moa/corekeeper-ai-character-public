// Read-only audit of saved model calls; no network/model calls and no replacement of original valid flags.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const files=['runs/smoke/s1-A.json','runs/smoke/s1-B.json','runs/remaining/s2-B.json','runs/remaining/s2-A.json','runs/remaining/s3-B.json','runs/remaining/s3-A.json'];
const rows=[];
let calls={upper:0,decisions:0,search:0},usage={upperInput:0,upperCached:0,upperOutput:0,decisionsInput:0,decisionsCached:0};
for(const file of files){
  const s=JSON.parse(fs.readFileSync(path.join(root,file),'utf8'));
  const visibleIDs=new Set(['bench1','frontier1']);
  const evidenceIDs=new Set();
  const planChecks=[];
  for(const e of s.events){
    if(e.type==='search'){
      calls.search++;evidenceIDs.add(e.evidence.id);
      for(const m of JSON.stringify(e.evidence).matchAll(/\be:\d+\b/g))evidenceIDs.add(m[0]);
      for(const loc of e.evidence.locations||[])visibleIDs.add(loc.id);
    }
    if(e.type==='upper'){
      calls.upper++;usage.upperInput+=e.usage?.input||0;usage.upperCached+=e.usage?.cached||0;usage.upperOutput+=e.usage?.output||0;
      if(e.answer?.decision==='plan')planChecks.push({target:e.answer.target_id,targetKnown:e.answer.target_id==null||visibleIDs.has(e.answer.target_id),unsupportedEvidenceIDs:(e.answer.evidence_ids||[]).filter(id=>!evidenceIDs.has(id)),why:e.answer.why});
    }
    if(e.type==='decisions'){calls.decisions++;usage.decisionsInput+=e.response.usage?.input_tokens||0;usage.decisionsCached+=e.response.usage?.input_tokens_details?.cached_tokens||0;}
  }
  const expectedTarget={move_resource:'ore1',move_bench:'bench1',explore:'frontier1'}[s.execution?.action];
  const targetDelegated=s.plan.target_id==null;
  const targetMatches=targetDelegated||s.plan.target_id===expectedTarget;
  const unresolvedBoundary=s.scene==='s3'&&s.route==='B';
  rows.push({file,scene:s.scene,route:s.route,totalMs:s.totalMs,firstAction:s.execution?.action,endpoint:s.execution?.endpoint,originalAutoValid:s.execution?.valid,goalAppropriate:s.execution?.action===s.execution?.expected_action,planTargetMatchesOrDelegated:targetMatches,targetDelegated,planChecks,roleBoundary:unresolvedBoundary?'미정: gather 중 고갈 확인 후 탐색이 작업 내부 대상 찾기인지 상위 explore 재계획인지 프롬프트에 명시하지 않음':'시험 계약과 일치',automatedVerdictLimit:unresolvedBoundary?'원래 판정기는 plan.task===explore라는 숨은 조건으로 false. 낡은 광맥에 이동한 실패가 아니며 모델 오류로 집계하지 않음':'조회 존재뿐 아니라 목표 ID·근거 ID의 당시 공개 여부를 이 감사에서 별도 확인',trace:s.events.map(e=>e.type==='upper'?{type:e.type,ms:e.ms,answer:e.answer}:e.type==='search'?{type:e.type,ms:e.ms,id:e.id}:{type:e.type,ms:e.ms,choice:e.response.answers?.[0]?.choice})});
}
const output={calls,usage,rows,notes:['Decisions는 choice만 반환하므로 정보가 입력되었고 선택이 사실과 부합하는 것은 확인할 수 있지만 모델 내부의 근거 사용을 직접 입증하지 않는다.','원본 실행 결과와 자동 valid 플래그는 수정하지 않았다.','새 모델 호출 없이 기록된 입력·응답·검색 결과를 재검사했다.']};
fs.writeFileSync(path.join(root,'audit.json'),JSON.stringify(output,null,2));
console.log(JSON.stringify({calls,usage,rows:rows.map(r=>({scene:r.scene,route:r.route,auto:r.originalAutoValid,goalAppropriate:r.goalAppropriate,target:r.planTargetMatchesOrDelegated,unsupportedEvidence:r.planChecks.flatMap(p=>p.unsupportedEvidenceIDs)}))},null,2));
