// 판단 시험 실행기. 한 명령: node run.mjs            (요청 만들기 + 보내기 + 집계)
//                       node run.mjs --dry       (요청만 만들어 requests/ 에 씀, API 호출 없음)
//                       node run.mjs --smoke     (S1-K-14 한 번만 보내 응답 모양 확인)
//                       node run.mjs --perturb   (2차: 작은 변화 3종 × 28변형, 1번씩 → raw/perturb.jsonl)
//                       node run.mjs --analyze   (raw/results.jsonl 로 집계만 다시)
// 키: 환경 변수 OPENAI_API_KEY(프로세스, 없으면 Windows 사용자 환경 변수). 키는 출력·저장하지 않는다.
// 모델: 게임 설정 파일 decisions-settings.json 의 model(읽기만). 없으면 코드 기본값 gpt-6-luna.
// 제한: 동시 요청 2개, 전체 호출 상한 600. 429 한 번 또는 연속 실패 3번이면 멈춘다.
import fs from 'fs';
import os from 'os';
import path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';
import { allVariants, perturbVariants } from './scenarios.mjs';
import { analyze, analyzePerturb } from './analyze.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const URL_ = 'https://api.openai.com/v1/decisions';   // AIDecisions.cs Url
const REPS = Number(process.env.REPS || 5);
const CONC = Math.min(2, Number(process.env.CONC || 2));
const MAX_CALLS = 600;
const TIMEOUT_MS = 12000;   // 설정 timeoutSeconds 10 + 2 (UnityWebRequest.timeout)

function settingsModel() {
  const p = path.join(os.homedir(), 'AppData', 'LocalLow', 'Pugstorm', 'Core Keeper', 'AIPlayers', 'decisions-settings.json');
  try {
    const j = JSON.parse(fs.readFileSync(p, 'utf8').replace(/^﻿/, ''));
    return { model: j.model || 'gpt-6-luna', usdPerM: j.usdPerMillionInputTokens ?? 0.1, from: p };
  } catch { return { model: 'gpt-6-luna', usdPerM: 0.1, from: 'code default' }; }
}

function apiKey() {
  if (process.env.OPENAI_API_KEY) return process.env.OPENAI_API_KEY;
  try {   // Windows 사용자 환경 변수 (AIDecisions.ApiKey 와 같은 순서)
    const k = execFileSync('powershell.exe', ['-NoProfile', '-Command', "[Environment]::GetEnvironmentVariable('OPENAI_API_KEY','User')"], { encoding: 'utf8' }).trim();
    if (k) return k;
  } catch { }
  return '';
}

async function callOnce(key, body) {
  const ctl = new AbortController();
  const timer = setTimeout(() => ctl.abort(), TIMEOUT_MS);
  const t0 = performance.now();
  let status = 0, text = '', err = '';
  try {
    const r = await fetch(URL_, { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + key }, body: JSON.stringify(body), signal: ctl.signal });
    status = r.status;
    text = await r.text();
  } catch (e) { err = String(e && e.name || e); }
  clearTimeout(timer);
  return { status, text, err, latency: (performance.now() - t0) / 1000 };
}

function parse(text) {
  let j = null;
  try { j = JSON.parse(text); } catch { return { parseError: true }; }
  const a = j && j.answers && j.answers[0];
  return {
    respModel: j && j.model,
    type: a && a.type, choice: a && a.choice, confidence: a && a.confidence,
    probabilities: a && a.probabilities,
    inputTokens: j && j.usage && j.usage.input_tokens,
    totalTokens: j && j.usage && j.usage.total_tokens,
    cachedTokens: j && j.usage && j.usage.input_tokens_details && j.usage.input_tokens_details.cached_tokens,
    error: j && j.error && j.error.message,
  };
}

async function main() {
  const args = new Set(process.argv.slice(2));
  const S = settingsModel();
  const PERTURB = args.has('--perturb');
  const variants = PERTURB ? perturbVariants(S.model) : allVariants(S.model);
  fs.mkdirSync(path.join(HERE, args.has('--perturb') ? 'requests-perturb' : 'requests'), { recursive: true });
  for (const v of variants) fs.writeFileSync(path.join(HERE, PERTURB ? 'requests-perturb' : 'requests', `${v.id}.json`), JSON.stringify(v.body, null, 2));
  console.log(`model=${S.model} (from ${S.from}); variants=${variants.length}; reps=${REPS}; conc=${CONC}`);
  for (const v of variants) console.log(`  ${v.id}: candidates=${v.cands.length} inputChars=${v.input.length}`);
  if (args.has('--dry')) return;
  if (args.has('--analyze')) { analyze(HERE, S.usdPerM); if (fs.existsSync(path.join(HERE, 'raw', 'perturb.jsonl'))) analyzePerturb(HERE); return; }

  const key = apiKey();
  if (!key) { console.error('OPENAI_API_KEY 없음'); process.exit(2); }
  if (args.has('--smoke')) {   // 한 번만 보내 응답 모양 확인(결과 파일에 안 씀)
    const v = variants.find((x) => x.id === 'S1-K-14');
    const r = await callOnce(key, v.body);
    console.log(r.status, r.latency.toFixed(3), r.text.slice(0, 1500));
    return;
  }

  // 작업 목록: 반복을 바깥 고리로 두어 같은 변형이 연달아 붙지 않게 함(시간대 치우침 줄이기)
  const jobs = [];
  for (let rep = 1; rep <= (PERTURB ? 1 : REPS); rep++) for (const v of variants) jobs.push({ v, rep });
  if (jobs.length > MAX_CALLS) { console.error(`호출 ${jobs.length} > 상한 ${MAX_CALLS}`); process.exit(2); }
  fs.mkdirSync(path.join(HERE, 'raw'), { recursive: true });
  const outPath = path.join(HERE, 'raw', PERTURB ? 'perturb.jsonl' : 'results.jsonl');
  const out = fs.createWriteStream(outPath, { flags: args.has('--append') ? 'a' : 'w' });
  let next = 0, calls = 0, consecFail = 0, stop = '';
  const runStart = new Date().toISOString();
  async function worker(w) {
    while (!stop && next < jobs.length) {
      const { v, rep } = jobs[next++];
      calls++;
      const r = await callOnce(key, v.body);
      const p = r.status === 200 ? parse(r.text) : (() => { try { return { error: JSON.parse(r.text).error.message }; } catch { return { error: r.text.slice(0, 300) }; } })();
      const ok = r.status === 200 && p.type === 'choice' && !!p.choice;
      consecFail = ok ? 0 : consecFail + 1;
      const rec = { t: new Date().toISOString(), worker: w, id: v.id, scenario: v.scenario, know: v.know, set: v.set, rep, nCands: v.cands.length, inputChars: v.input.length,
        status: r.status, netErr: r.err, latency: +r.latency.toFixed(4), ok, ...p,
        chosenKey: ok ? (v.cands.find((c) => c.value === p.choice) || {}).key : undefined, body: r.text };
      out.write(JSON.stringify(rec) + '\n');
      console.log(`${calls}/${jobs.length} ${v.id} rep${rep} ${r.status} ${r.latency.toFixed(2)}s ${ok ? p.choice : 'FAIL ' + (p.error || r.err)}`);
      if (r.status === 429) stop = '429 속도 제한';
      else if (consecFail >= 3) stop = '연속 실패 3번';
    }
  }
  await Promise.all(Array.from({ length: CONC }, (_, i) => worker(i)));
  await new Promise((res) => out.end(res));
  fs.writeFileSync(path.join(HERE, 'raw', PERTURB ? 'perturb-meta.json' : 'run-meta.json'), JSON.stringify({ runStart, runEnd: new Date().toISOString(), model: S.model, settingsFrom: S.from, reps: REPS, conc: CONC, calls, stoppedBecause: stop || null }, null, 2));
  if (stop) console.error('멈춤: ' + stop);
  if (PERTURB) analyzePerturb(HERE); else analyze(HERE, S.usdPerM);
}

main();
