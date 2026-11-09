// node build.js -> dict-kop.json (11-06 dict_kop 819 + add-kop 358), tr4.js (KO' translator from 100-en/tr/tr2.js),
// kop-pre.json (KO' requests), missing.json, degree.json. Also prints the fact check and change degree.
const fs = require('fs'), path = require('path');
const D = __dirname;
const rd = f => JSON.parse(fs.readFileSync(f, 'utf8'));
const EN = path.join(D, '..', '..', '100-en', 'tr');
const KOP11 = path.join(D, '..', '..', '..', '2026-11-06-판단-입력-영어화', 'tok', 'exp3', 'dict_kop.json');

// 1) dict
const need = rd(path.join(D, 'need.json'));
const add = require('./add-kop-src.js');
const keys = Object.keys(need);
if (add.length !== keys.length) throw new Error(`add ${add.length} != need ${keys.length}`);
const bad = [];
const addDict = {};
keys.forEach((k, i) => {
  const n = (k.match(/\{\}/g) || []).length, v = add[i];
  const used = (v.match(/\{(\d+)\}/g) || []).map(s => +s.slice(1, -1)).sort((a, b) => a - b);
  const want = [...Array(n).keys()];
  if (JSON.stringify(used) !== JSON.stringify(want)) bad.push(`${i}: slots ${n} used [${used}] :: ${k}`);
  addDict[k] = v;
});
if (bad.length) { console.log(bad.join('\n')); throw new Error('slot check failed'); }
const dict = { ...rd(KOP11), ...addDict };
fs.writeFileSync(path.join(D, 'dict-kop.json'), JSON.stringify(dict, null, 1));
fs.writeFileSync(path.join(D, 'add-kop.json'), JSON.stringify(addDict, null, 1));

// 2) tr4.js = 100-en tr2.js with the 11-06 make_tr3 replacements
let s = fs.readFileSync(path.join(EN, 'tr2.js'), 'utf8');
const R = (a, b) => { if (!s.includes(a)) throw new Error('not found: ' + a.slice(0, 70)); s = s.split(a).join(b); };
R("const dictPath = path.join(__dirname, 'dict.json');", "const dictPath = path.join(__dirname, 'dict-kop.json');\nconst HITS = {};");
R("const STATUS = { 완료: 'done', 실패: 'failed', 취소: 'cancelled' };", "const STATUS = { 완료: '마침', 실패: '실패함', 취소: '취소됨' };");
R("const DIRW = { 동: 'east', 서: 'west', 남: 'south', 북: 'north' };", "const DIRW = { 동: '동', 서: '서', 남: '남', 북: '북' };");
R("const DIRS = { 동: 'E', 서: 'W', 남: 'S', 북: 'N' };", "const DIRS = { 동: '동', 서: '서', 남: '남', 북: '북' };");
R("if (Object.prototype.hasOwnProperty.call(DICT, key)) return lead + fill(DICT[key], vals) + trail;", "if (Object.prototype.hasOwnProperty.call(DICT, key)) { HITS[key] = (HITS[key] || 0) + 1; return lead + fill(DICT[key], vals) + trail; }");
R("return `skill ended (${NAME(m[1])} ${STATUS[m[2]]})`;", "return `기술 종료(${NAME(m[1])} ${STATUS[m[2]]})`;");
R("return `ending soon: the current '${NAME(m[1])}' (${m[2]}) ends in about ${m[3]}s${m[4] ? TT(m[4]) : ''}`;", "return `곧 종료: 진행 중인 '${NAME(m[1])}'(${m[2]})이(가) 약 ${m[3]}초 후 끝남${m[4] ? TT(m[4]) : ''}`;");
R("return `free time started: ${TT(m[1])}`;", "return `자유 시간이 시작됨: ${TT(m[1])}`;");
R("return `self-defense: ${TT(m[1])}`;", "return `자기 방어 상황: ${TT(m[1])}`;");
R("return `retry (${TT(m[1])})`;", "return `다시 시도(${TT(m[1])})`;");
R("return `previous answer dropped (${TT(m[1])})`;", "return `앞선 답을 버림(${TT(m[1])})`;");
R("return `pending switch dropped (${TT(m[1])})`;", "return `대기 중이던 전환을 버림(${TT(m[1])})`;");
R("return `prefetched answer does not fit (${TT(m[1])})`;", "return `먼저 받아 둔 답이 맞지 않음(${TT(m[1])})`;");
R("return `cannot do the next swing (${TT(m[1])})`;", "return `다음 휘두르기를 할 수 없음(${TT(m[1])})`;");
R("return `${NAME(m[1])} ${STATUS[m[2]]}: ${REASON(m[3])} (${m[4]}s)`;", "return `${NAME(m[1])}: ${STATUS[m[2]]}, ${REASON(m[3])} (${m[4]}초)`;");
R("return `switched to new skill ${NAME(m[1])}`;", "return `새 기술 ${NAME(m[1])}(으)로 전환됨`;");
R("const label = m[1] == '최근 결과' ? 'Recent results' : 'Last action result';", "const label = m[1] == '최근 결과' ? '최근의 결과' : '바로 앞 행동의 결과';");
R("if (m[2] == '없음') return `${label}: none.`;", "if (m[2] == '없음') return `${label}: 없음.`;");
R("return `Current task: ${NAME(m[1])} ${m[2]}, ${m[3]}s in.`;", "return `현재 진행 중인 일: ${NAME(m[1])}(${m[2]}), ${m[3]}초 경과.`;");
R("return `Why deciding again now: ${TRIG(m[1])}.`;", "return `이번에 다시 결정하는 이유: ${TRIG(m[1])}.`;");
R("return `Switch pending: the previous decision chose '${TRUNC(m[1])}' and is waiting for the current motion to end. ${TT(m[2])}`;", "return `전환 대기: 앞선 결정에서 '${TRUNC(m[1])}'을(를) 골랐고, 지금 동작이 끝나기를 기다리고 있음. ${TT(m[2])}`;");
R("const to = m[3] == '계속' ? 'continue' : TRUNC(m[3]); return `${NAME(m[1])} (after ${m[2]}s)→${to}`;", "const to = m[3] == '계속' ? '계속 진행' : TRUNC(m[3]); return `${NAME(m[1])}(${m[2]}초 뒤)→${to}`;");
R("return `Switches in the last 10s: ${m[1]}: ${ITEMS(m[2])}. Each time, the new action started from the beginning.`;", "return `최근 10초 사이 행동 전환 ${m[1]}번: ${ITEMS(m[2])}. 전환할 때마다 새 행동은 처음부터 다시 시작했다.`;");
R("out.push(`Switches involving a long action (attack (continuous), keep distance, eat, ...) in the last 10s: ${m[1]}: ${ITEMS(m[2])}. A long action restarts from the beginning when switched.`);", "out.push(`최근 10초 사이 긴 동작(공격하기(지속)·간격 두기·먹기 등)이 낀 전환 ${m[1]}번: ${ITEMS(m[2])}. 긴 동작은 전환하면 처음부터 다시 시작된다.`);");
R("out.push(`Switches between short actions (short move, single hit, approach, hold, composite) in the last 10s: ${m[1]} (a short action starts as soon as it is chosen, and a new decision follows as soon as it ends).`);", "out.push(`최근 10초 사이 짧은 동작(짧게 이동·한 번 휘두르기·접근·자리 지키기·묶음 행동)끼리의 전환 ${m[1]}번(짧은 동작은 고르는 즉시 시작되고, 끝나는 즉시 다시 결정하도록 되어 있다).`);");
R("const CAP = t => t.replace(/(^|\\. )([a-z])/g, (m, a, b) => a + b.toUpperCase());", "const CAP = t => t;");
R("module.exports = { translateRequest, MISS,", "module.exports = { HITS, translateRequest, MISS,");
fs.writeFileSync(path.join(D, 'tr4.js'), "// GENERATED by build.js from 100-en/tr/tr2.js (KO' rewording, same replacements as 11-06 make_tr3.js).\n" + s);

// 3) run on the 100 cases
const tr = require('./tr4.js');
const C = rd(path.join(D, '..', '..', '100-ko', 'cases.json'));
const out = [];
for (const c of C) {
  const x = { id: c.id, input: c.text, opts: c.cands.map(o => ({ value: o.value, desc: o.description })) };
  const e = tr.translateRequest(x);
  out.push({ id: c.id, input: e.input, choices: e.opts.map(o => ({ value: o.value, description: o.desc })) });
}
fs.writeFileSync(path.join(D, 'kop-pre.json'), JSON.stringify(out, null, 1));
const M = Object.entries(tr.MISS).map(([k, v]) => ({ key: k, n: v.n, ex: v.ex })).sort((a, b) => b.n - a.n);
fs.writeFileSync(path.join(D, 'missing.json'), JSON.stringify(M, null, 1));
console.log('dict', Object.keys(dict).length, 'missing keys', M.length, 'missing hits', M.reduce((a, m) => a + m.n, 0));

// 4) change degree: per-key (hit-weighted) changed share, char edit distance, eojeol Jaccard; whole-text unchanged share
const lev = (a, b) => { const m = a.length, n = b.length; if (!m) return n; if (!n) return m; let p = [...Array(n + 1).keys()], q = new Array(n + 1);
  for (let i = 1; i <= m; i++) { q[0] = i; for (let j = 1; j <= n; j++) q[j] = Math.min(p[j] + 1, q[j - 1] + 1, p[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1)); [p, q] = [q, p]; } return p[n]; };
let W = 0, chg = 0, ed = 0, jac = 0;
const norm = v => v.replace(/\{\d+\}/g, '{}');
for (const [k, h] of Object.entries(tr.HITS)) {
  const v = norm(dict[k]); W += h; if (v !== k) chg += h;
  ed += h * lev(k, v) / Math.max(k.length, v.length);
  const A = new Set(k.split(/\s+/)), B = new Set(v.split(/\s+/)); const inter = [...A].filter(x => B.has(x)).length;
  jac += h * (1 - inter / (A.size + B.size - inter));
}
// whole-text: share of Korean characters' lines unchanged
let lines = 0, same = 0;
for (const c of C) { const o = out.find(x => x.id === c.id); const a = (c.text + '\n' + c.cands.map(x => x.description).join('\n')).split('\n'), b = (o.input + '\n' + o.choices.map(x => x.description).join('\n')).split('\n');
  a.forEach((l, i) => { if (/[가-힣]/.test(l)) { lines++; if (l === b[i]) same++; } }); }
const degree = { keyHits: W, keysHit: Object.keys(tr.HITS).length, changedShareWeighted: +(chg / W).toFixed(4), editDistNorm: +(ed / W).toFixed(3), eojeolChange: +(jac / W).toFixed(3), koreanLines: lines, koreanLinesUnchanged: same };
fs.writeFileSync(path.join(D, 'degree.json'), JSON.stringify(degree, null, 1));
console.log('degree', JSON.stringify(degree));
