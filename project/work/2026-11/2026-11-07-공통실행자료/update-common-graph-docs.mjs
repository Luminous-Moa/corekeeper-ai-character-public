import fs from 'node:fs';
import path from 'node:path';
const root='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/지식그래프/기획';
const updates={
 'F.action-select':'685dde4의 현장 판단은 수량 충족·실행 시간·무진척·호출 실패를 관측으로 전달하면서 유효한 현재 행동을 유지한다. AI가 stop_action·finish_goal·request_plan을 명시 선택한다. 응답 나이만으로 버리지 않고 목표·대상·실행 회차에 맞는지 검사한다. 명시 중단은 문닫기 후속 동작과 과거 예약까지 정리한다. 호출·비용 상한은 새 호출에 적용한다.',
 'F.candidates':'현재 관측·목표·대상에서 실행 후보를 만들고 실행 가능한 현재 계속하기는 근접전 묶음과 개수 상한 뒤에도 보존한다. 실패 횟수는 이유와 함께 설명하고 횟수만으로 후보를 숨기지 않는다. 진행 후보 없음과 반복 대기는 목표를 지우지 않고 AI에게 관측으로 전달한다.',
 'F.skill-attack':'AI가 고른 적에게 접근해 근접 타격한다. 45초 경과·무진척은 현재 실행의 관측으로 전달한다. 실제 적 처치·소실·도달 불가 결과는 보고하며, 새로운 AI 선택이 행동을 중단·전환한다.',
 'F.skill-dig':'관측하거나 기억한 대상까지 기존 길 찾기와 벽 채굴을 연결한다. 30초 무진척·300초 경과는 관측으로 전달하고 진행 가능한 선택을 유지한다. 실제 하위 실행 실패는 위치·이유·진척을 포함해 모델에 반환한다.',
 'F.charge-urgent':'위협 관측은 판단 요청과 실행 시점을 갱신한다. 호출 실패가 현재 목표와 유효한 몸 동작을 자동 취소하지 않으며, 비상 관측만으로 이전 AI 선택을 폐기하지 않는다.',
 'F.place-grid':'선택한 물건·놓을 칸·설 칸에 접근해 입력하고 실제 가방 감소와 새 물체로 결과를 확인한다. 시간 경과·자리 정렬 지연·요청 반영 지연은 관측으로 전달한다. 실패 횟수만으로 자리를 숨기지 않으며 pending 입력은 중복 전송하지 않는다.',
 'F.host-orders':'원래 사용자 부탁과 현재 계획 단계를 별도로 추적한다. 중간 목표가 성공해도 원래 부탁은 유지하며 상위 응답 order=done 또는 명시 orders 완료가 부탁을 끝낸다.',
 'F.plan-queue':'상위 LLM이 정한 계획을 유지하고 Decisions가 finish_goal로 현재 단계를 완료하면 다음 단계를 이어 간다. 수량 충족만으로 자동으로 단계를 넘기지 않는다. 명시 재계획은 현재 관측을 전달하며 실행 가능한 몸 동작을 유지한다.',
 'F.executor':'선택된 행동의 몸 입력을 수행하고 관측한 결과를 전달한다. 정책 시간·진척 조건은 AISkill.ObserveOnce와 AIBrain.ExecutionObservation을 통해 현장·상위 입력에 보존한다. 중단·전환은 새 AI 선택과 사용자의 직접 명령으로 실행한다.'
};
const featureFile=path.join(root,'기능.md');
let text=fs.readFileSync(featureFile,'utf8');
for(const [id,summary] of Object.entries(updates)){
 const escaped=id.replaceAll('.','\\.');
 const re=new RegExp('(^### '+escaped+' —[^\\n]*\\n)([\\s\\S]*?)(?=^### |$(?![\\s\\S]))','gm');
 let count=0;
 text=text.replace(re,(all,heading,body)=>{
  count++;
  body=body.replace(/^- 상태:.*$/m,'- 상태: 685dde4 코드·오프라인 확인; 과거 실게임 근거 보존');
  body=body.replace(/^- 한 줄: (.*)$/m,(_line,old)=>'- 한 줄: '+summary+'\n- 메모: 이전 구현 설명은 당시 기록이다. '+old);
  body=body.replace(/^- 확인한 선:.*\r?\n/gm,'');
  return heading+body;
 });
 if(!count)throw new Error('Missing capability '+id);
}
text+='\n### F.common-execution — AI의 명시 중단·단계 완료·재계획\n- 상태: 685dde4 코드·오프라인 확인; 새 실게임 미확인\n- 종류: 제품\n- 한 줄: stop_action은 몸만 중단하고 목표와 부탁을 유지한다. finish_goal은 현재 단계만 완료한다. request_plan은 유효한 몸 동작을 유지하며 상위에 현재 사실을 전달한다.\n- 정책: pol.ai-action-control, pol.original-request-completion\n- 코드 앵커: AIBrain.Apply, AIBrain.CancelPendingChoices, AIBrain.ObserveExecution, AIGoals.CheckDone, AIGoals.FinishSelectedGoal, AIPlanner.RequestReplan, AIHostOrders.GoalEnded\n- 근거: work/2026-11/2026-11-07-공통실행자료/contract.md "stop_action은 현재 몸 동작만 AI가 중단한다."\n- 근거: work/2026-11/2026-11-07-공통실행자료/implementation.md "AISkill.ObserveOnce는 실행 회차에 조건을 보존하고 재판단을 요청한다."\n';
fs.writeFileSync(featureFile,text);
const policyFile=path.join(root,'정책.md');
let policy=fs.readFileSync(policyFile,'utf8');
policy=policy.replace('- 내용: 대상별 최근 실패를 후보 설명에 붙이고, 30초 안에 두 번 실패한 대상은 30초 동안 후보에서 뺀다. 영구 금지는 아니다(v2 10장).','- 내용: 과거 구현 제안은 30초 안에 두 번 실패한 대상을 잠시 제외했다. 685dde4에서는 실패 원인과 횟수를 설명하며 횟수만으로 후보를 숨기지 않는다.');
policy+='\n### pol.ai-action-control — AI가 행동의 계속·중단·전환을 선택\n- 규칙: 현재 행동이 실행 가능하면 / 코드는 / 수량·시간·진척·호출 실패를 관측으로 전달하고 계속·중단·전환을 AI가 선택하게 한다.\n- 등급: 사용자 결정\n- 확정도: 확정\n- 종류: 처리\n- 날짜: 2026-11-07\n- 근거: work/2026-11/2026-11-07-행동취소-판단주체.md "현재 행동을 계속할지, 스스로 취소할지, 다른 행동으로 전환할지는 AI가 선택한다."\n\n### pol.original-request-completion — 중간 진척과 원래 부탁 완료를 구분\n- 규칙: 현재 단계의 수량·조건이 충족되면 / 코드는 / 관측 사실을 전달하고 원래 부탁의 종료는 위쪽 LLM이 판단하게 한다.\n- 등급: 사용자 결정\n- 확정도: 확정\n- 종류: 처리\n- 날짜: 2026-11-07\n- 근거: work/2026-11/2026-11-07-공통실행-합의.md "중간 단계 진척과 사용자 원래 부탁을 보존한다."\n';
fs.writeFileSync(policyFile,policy);
const unresolved=path.join(root,'미결.md');
let unknown=fs.readFileSync(unresolved,'utf8');
for(const id of ['U.fail-stop-3-live','U.goal-system-ends']){
 const re=new RegExp('(^### '+id.replaceAll('.','\\.')+' —[^\\n]*\\n)([\\s\\S]*?)(?=^### |$(?![\\s\\S]))','gm');
 unknown=unknown.replace(re,(all,head,body)=>head+body.replace(/^- 닫는 법: (.*)$/m,(_line,old)=>'- 닫는 법: 685dde4 공통 실행에서는 이 과거 자동종료 정책을 관측과 AI 명시 제어로 대체했다. 새 실게임에서 호출 실패·수량 충족 후 유효 행동 유지와 명시 중단을 확인한다.\n- 메모: 과거 미관측 항목으로 보존한다. 당시 확인 계획은 '+old));
}
fs.writeFileSync(unresolved,unknown);
console.log('Updated current capability summaries and preserved historical evidence.');
