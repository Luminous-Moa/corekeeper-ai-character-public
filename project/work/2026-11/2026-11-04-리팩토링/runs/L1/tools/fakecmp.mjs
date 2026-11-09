// fakecmp.mjs <L0 fake-requests.jsonl> <L1 fake-requests.jsonl>: step, status, trigger, gap to the previous request, last_answer_rejected
import fs from 'node:fs';
const rows = (f) => fs.readFileSync(f, 'utf8').trim().split('\n').map((l) => JSON.parse(l));
const fmt = (r, i, a) => `step ${r.step} ${r.status} trigger=${r.trigger} gap=${i ? ((Date.parse(r.t) - Date.parse(a[i - 1].t)) / 1000).toFixed(1) : '-'}s rejected=${JSON.stringify(r.last_answer_rejected)}`;
const [A, B] = process.argv.slice(2).map(rows);
const n = Math.max(A.length, B.length);
let diff = 0;
for (let i = 0; i < n; i++) {
  const x = A[i] ? fmt(A[i], i, A) : '-', y = B[i] ? fmt(B[i], i, B) : '-';
  const same = A[i] && B[i] && A[i].step === B[i].step && A[i].status === B[i].status && A[i].trigger === B[i].trigger && JSON.stringify(A[i].last_answer_rejected) === JSON.stringify(B[i].last_answer_rejected);
  if (!same) diff++;
  console.log(`${same ? 'same' : 'DIFF'} | L0 ${x}\n     | L1 ${y}`);
}
console.log(`requests L0 ${A.length}, L1 ${B.length}, differing rows ${diff}`);
