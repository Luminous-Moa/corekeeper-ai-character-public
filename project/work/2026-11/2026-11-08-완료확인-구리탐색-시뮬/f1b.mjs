// F1b: the finish text as the code would build it from CompletionPending (pending / met / no measured condition).
//   node f1b.mjs <dir> <prefix> <state: pending|met>   (reads <prefix>-F0.jsonl and <prefix>-F3.jsonl, writes -F1b, -F13b)
import fs from 'fs';
import path from 'path';
const [dir, prefix, state] = process.argv.slice(2);
const meta = JSON.parse(fs.readFileSync(path.join(dir, `${prefix}-meta.json`), 'utf8'));
export function finishText(goal, measured, pending, pendingText) {
  if (!measured) return `단계 완료 처리 — '${goal}'은 수량·도착 같은 측정 완료 조건이 없음. 이 일을 끝낼 때 고르면 바로 끝나고 다음 계획 단계로 넘어감. 원래 부탁 완료는 위쪽 LLM이 판단.`;
  if (pending) return `단계 완료 처리 — '${goal}'. 지금 사실로는 아직 완료가 아님(${pendingText}). 이걸 고르면 완료되지 않고, 몸은 움직이지 않은 채 같은 단계에서 다시 판단함. 상태를 확인하는 행동이 아님. 원래 부탁 완료는 위쪽 LLM이 판단.`;
  return `단계 완료 처리 — '${goal}'. 지금 사실로 완료 조건이 충족됨. 고르면 이 단계를 끝내고 다음 계획 단계로 넘어감. 원래 부탁 완료는 위쪽 LLM이 판단.`;
}
for (const [src, dst] of [['F0', 'F1b'], ['F3', 'F13b']]) {
  const L = fs.readFileSync(path.join(dir, `${prefix}-${src}.jsonl`), 'utf8').split('\n').filter(Boolean).map(JSON.parse);
  const out = L.map((b, i) => {
    const km = meta[src][i].map;
    const goal = (b.input.match(/목표: (.+?)(?: — 지금 \d+\/\d+)?\./) || [])[1] || '?';
    const measured = !goal.includes('따라가기');
    const pend = (b.input.match(/단계 완료 선택을 현재 사실로 재검사함\. 목표 '[^']*'\. (.*?)\. 현재 단계와 원래 부탁을 유지함/) || [])[1] || '요청 수량 확보 0/8, 현재 가방 CopperOre 0';
    for (const c of b.questions[0].choices) if (km[c.value]?.kind === 'finish_goal') c.description = finishText(goal, measured, state === 'pending', pend);
    return JSON.stringify(b);
  });
  fs.writeFileSync(path.join(dir, `${prefix}-${dst}.jsonl`), out.join('\n') + '\n');
  meta[dst] = meta[src];
}
fs.writeFileSync(path.join(dir, `${prefix}-meta.json`), JSON.stringify(meta));
console.log('ok', prefix);
