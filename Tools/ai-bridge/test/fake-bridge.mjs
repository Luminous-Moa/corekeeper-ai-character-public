// fake-bridge.mjs — a stand-in for bridge.mjs that answers POST /v1/plan from a fixed sequence of replies (refactor plan
// 2-계획.md A.3 and B.1 scenario 10, review F8). It runs the mod's planner branches a real CLI almost never produces
// (limit with retry_after_s, parse, 409 busy, an answer the mod's check drops, hold, an upper-LLM follow goal), so a live
// run before and after a refactor step can compare what the mod does with the same replies.
// No CLI, no memory, no network beyond 127.0.0.1. Owned by the C# refactor worker (plan A.3), not by the bridge split.
//
//   node test/fake-bridge.mjs --seq test/fake-seq/l0.json [--port 8331] [--log <requests.jsonl>]
//        Step i answers the i-th POST /v1/plan; after the last step the last one repeats. In a step's body the strings
//        "{{report_id}}" / "{{goal_rev}}" become the request's numbers. GET /v1/health answers {ok:true, fake:true}.
//        Every request is logged (time, step, member, report_id, goal_rev, trigger, last_answer_rejected) to --log
//        (default: logs-fake/requests-<time>.jsonl next to this file) and to the console.
//   node test/fake-bridge.mjs selftest      starts on a free port, checks order, substitution, repeat, log; exits.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));

export function loadSeq(file) {
  const j = JSON.parse(fs.readFileSync(file, 'utf8').replace(/^﻿/, ''));
  if (!Array.isArray(j.steps) || j.steps.length === 0) throw new Error(`${file}: no steps`);
  for (const s of j.steps) if (!Number.isInteger(s.status) || typeof s.body !== 'object') throw new Error(`${file}: step "${s.name}" needs status and body`);
  return j.steps;
}

export function render(step, req) {
  const num = (v) => (Number.isFinite(+v) ? String(+v) : 'null');
  return JSON.stringify(step.body)
    .split('"{{report_id}}"').join(num(req.report_id))
    .split('"{{goal_rev}}"').join(num(req.goal_rev));
}

export function startFake({ steps, port = 8331, logFile, quiet = false }) {
  let i = 0;
  fs.mkdirSync(path.dirname(logFile), { recursive: true });
  const server = http.createServer(async (req, res) => {
    const send = (status, text) => { res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8' }); res.end(text); };
    try {
      if (req.method === 'GET' && req.url === '/v1/health') return send(200, JSON.stringify({ ok: true, fake: true, members: {}, busy: [] }));
      if (req.method === 'POST' && req.url === '/v1/plan') {
        let raw = '';
        for await (const c of req) raw += c;
        let body = {};
        try { body = JSON.parse(raw || '{}'); } catch { body = {}; }
        const k = Math.min(i, steps.length - 1); i++;
        const step = steps[k];
        const entry = { t: new Date().toISOString(), n: i, step: k + 1, name: step.name, status: step.status, member: body.member, report_id: body.report_id, goal_rev: body.goal_rev,
          trigger: body.report?.trigger, last_answer_rejected: body.report?.last_answer_rejected ?? null };
        fs.appendFileSync(logFile, JSON.stringify(entry) + '\n');
        if (!quiet) console.log(`fake-bridge: #${i} step ${k + 1} (${step.status}) member=${body.member} report=${body.report_id} rev=${body.goal_rev} trigger=${entry.trigger}`);
        return send(step.status, render(step, body));
      }
      send(404, JSON.stringify({ ok: false, error: 'not found' }));
    } catch (e) {
      send(500, JSON.stringify({ ok: false, error: 'bridge', detail: String(e.message || e).slice(0, 300) }));
    }
  });
  return new Promise((resolve) => server.listen(port, '127.0.0.1', () => resolve(server)));
}

function post(port, obj) {
  return new Promise((resolve, reject) => {
    const data = JSON.stringify(obj);
    const r = http.request({ host: '127.0.0.1', port, path: '/v1/plan', method: 'POST', headers: { 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(data) } }, (res) => {
      let t = ''; res.on('data', (c) => (t += c)); res.on('end', () => resolve({ status: res.statusCode, text: t }));
    });
    r.on('error', reject); r.end(data);
  });
}

async function selftest() {
  let fail = 0;
  const ok = (c, what) => { console.log(`  [${c ? 'OK' : 'FAIL'}] ${what}`); if (!c) fail++; };
  const seqFile = path.join(HERE, 'fake-seq', 'l0.json');
  const steps = loadSeq(seqFile);
  ok(steps.length === 6, `l0.json has 6 steps (${steps.length})`);
  const logFile = path.join(fs.mkdtempSync(path.join(os.tmpdir(), 'fake-bridge-')), 'requests.jsonl');
  const server = await startFake({ steps, port: 0, logFile, quiet: true });
  const port = server.address().port;
  const got = [];
  for (let n = 1; n <= 8; n++) got.push(await post(port, { member: 'claude', world: 'w', report_id: 40 + n, goal_rev: 7, report: { trigger: n === 1 ? 'auto_on' : 'retry' } }));
  ok(got.map((g) => g.status).join() === '429,200,200,409,200,200,200,200', `statuses in order, the last repeats (${got.map((g) => g.status).join()})`);
  const all = got.map((g) => JSON.parse(g.text));
  ok(all[0].error === 'limit' && all[0].retry_after_s === 120, 'step 1: limit with retry_after_s 120');
  ok(all[1].error === 'parse' && all[1].answer === null && all[1].report_id === 42, 'step 2: parse error, report_id substituted as a number');
  ok(all[2].answer.decision === 'set' && all[2].answer.goal.do === 'follow' && all[2].answer.for.report === 43 && all[2].answer.for.goal_rev === 7, 'step 3: set follow, for = the request');
  ok(all[3].error === 'busy', 'step 4: busy');
  ok(all[4].answer.goal.item === 'IronOre' && all[4].error === null, 'step 5: gather IronOre');
  ok(all[5].answer.decision === 'hold' && all[7].answer.decision === 'hold' && all[7].answer.for.report === 48, 'step 6: hold, repeated with the new report number');
  const keys = Object.keys(all[2]);
  ok(keys.indexOf('answer') < keys.indexOf('error') && keys[keys.length - 1] === 'recall_summary', 'envelope key order as bridge.mjs (answer before error, recall_summary last)');
  const lines = fs.readFileSync(logFile, 'utf8').trim().split('\n').map((l) => JSON.parse(l));
  ok(lines.length === 8 && lines[0].trigger === 'auto_on' && lines[7].step === 6, 'requests logged with step and trigger');
  const h = await new Promise((resolve) => http.get({ host: '127.0.0.1', port, path: '/v1/health' }, (res) => { let t = ''; res.on('data', (c) => (t += c)); res.on('end', () => resolve(JSON.parse(t))); }));
  ok(h.ok === true && h.fake === true, 'health answers');
  server.close();
  console.log(fail === 0 ? 'SELFTEST OK' : `SELFTEST FAILED ${fail}`);
  return fail;
}

const argv = process.argv.slice(2);
const opt = (k, d) => { const i = argv.indexOf(`--${k}`); return i >= 0 ? argv[i + 1] : d; };
if (argv[0] === 'selftest') process.exit(await selftest());
else if (argv.includes('--seq')) {
  const steps = loadSeq(path.resolve(opt('seq')));
  const port = Number(opt('port', 8331));
  const logFile = path.resolve(opt('log', path.join(HERE, 'logs-fake', `requests-${new Date().toISOString().replace(/[:.]/g, '-')}.jsonl`)));
  await startFake({ steps, port, logFile });
  console.log(`fake-bridge listening on http://127.0.0.1:${port} (${steps.length} steps, log ${logFile})`);
} else if (argv.length) {
  console.log('usage: node test/fake-bridge.mjs --seq <file> [--port 8331] [--log <file>] | selftest');
  process.exit(2);
}
