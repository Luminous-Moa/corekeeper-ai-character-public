// 이상 응답 재확인: 같은 요청 파일을 N번 다시 보낸다. node recheck.mjs <요청 json 경로> [N]
import fs from 'fs';
const body = fs.readFileSync(process.argv[2], 'utf8'); const N = Number(process.argv[3] || 2);
const key = process.env.OPENAI_API_KEY; if (!key) { console.error('no key'); process.exit(2); }
const out = [];
for (let i = 0; i < N; i++) {
  const t0 = performance.now();
  const r = await fetch('https://api.openai.com/v1/decisions', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + key }, body });
  const text = await r.text(); const lat = (performance.now() - t0) / 1000;
  let a = {}; try { const j = JSON.parse(text); a = { type: j.answers[0].type, choice: j.answers[0].choice, tokens: j.usage.input_tokens, top: (j.answers[0].probabilities || []).sort((x, y) => y.probability - x.probability).slice(0, 3) }; } catch { a = { raw: text.slice(0, 200) }; }
  out.push({ file: process.argv[2], status: r.status, latency: +lat.toFixed(3), ...a });
  console.log(JSON.stringify(out[out.length - 1]));
}
fs.appendFileSync('raw/recheck.jsonl', out.map((x) => JSON.stringify(x)).join('\n') + '\n');
