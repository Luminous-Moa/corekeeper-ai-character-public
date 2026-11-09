// raw/results.jsonl 집계 → summary.json, summary.txt
import fs from 'fs';
import path from 'path';
import { JUDGE, SCENARIOS } from './scenarios.mjs';

const pct = (a, q) => { if (!a.length) return NaN; const s = [...a].sort((x, y) => x - y); const i = (s.length - 1) * q; const lo = Math.floor(i), hi = Math.ceil(i); return s[lo] + (s[hi] - s[lo]) * (i - lo); };
const mean = (a) => (a.length ? a.reduce((x, y) => x + y, 0) / a.length : NaN);
const f2 = (v) => (Number.isFinite(v) ? v.toFixed(2) : '-');
const f3 = (v) => (Number.isFinite(v) ? v.toFixed(3) : '-');

function keyOfValue(rec, value) {
  // 요청의 후보 목록은 requests/<id>.json 에 있다. value → key 는 이름 규칙으로 얻는다.
  if (value.startsWith('m1_') || value.startsWith('m3_')) return value;
  if (value === 'hit' || value === 'approach' || value === 'hold' || value === 'wait') return value;
  if (value.startsWith('eat_')) return 'eat';
  if (value.startsWith('retreat_')) return 'retreat';
  return value;
}

export function analyze(dir, usdPerM = 0.1) {
  const p = path.join(dir, 'raw', 'results.jsonl');
  const recs = fs.readFileSync(p, 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
  const groups = new Map();
  for (const r of recs) { if (!groups.has(r.id)) groups.set(r.id, []); groups.get(r.id).push(r); }
  const rows = [];
  for (const [id, rs] of groups) {
    const r0 = rs[0];
    const J = JUDGE[r0.scenario];
    const ok = rs.filter((r) => r.ok);
    const dist = {};
    let good = 0, bad = 0, other = 0;
    const goodMass = [], badMass = [], topP = [], conf = [], chosenP = [];
    for (const r of ok) {
      const k = keyOfValue(r, r.choice);
      dist[k] = (dist[k] || 0) + 1;
      if (J) { if (J.good.includes(k)) good++; else if (J.bad.includes(k)) bad++; else other++; }
      if (Array.isArray(r.probabilities)) {
        let g = 0, b = 0, top = 0, ch = NaN;
        for (const q of r.probabilities) {
          const kk = keyOfValue(r, q.value);
          if (J && J.good.includes(kk)) g += q.probability;
          if (J && J.bad.includes(kk)) b += q.probability;
          top = Math.max(top, q.probability);
          if (q.value === r.choice) ch = q.probability;
        }
        if (J) { goodMass.push(g); badMass.push(b); }
        topP.push(top); chosenP.push(ch);
      }
      if (typeof r.confidence === 'number') conf.push(r.confidence);
    }
    const counts = Object.values(dist);
    const modal = counts.length ? Math.max(...counts) / ok.length : NaN;
    // 평균 확률 분포(후보별)
    const probAvg = {};
    for (const r of ok) for (const q of r.probabilities || []) probAvg[keyOfValue(r, q.value)] = (probAvg[keyOfValue(r, q.value)] || 0) + q.probability / ok.length;
    rows.push({
      id, scenario: r0.scenario, know: r0.know, set: r0.set, nCands: r0.nCands, inputChars: r0.inputChars,
      n: rs.length, ok: ok.length, fail: rs.length - ok.length,
      good, bad, other, goodRate: J ? good / ok.length : null, badRate: J ? bad / ok.length : null,
      dist, distinct: counts.length, modalShare: modal,
      goodMass: mean(goodMass), badMass: mean(badMass), topP: mean(topP), chosenP: mean(chosenP), confidence: mean(conf),
      probAvg,
      lat: { p50: pct(rs.map((r) => r.latency), 0.5), p95: pct(rs.map((r) => r.latency), 0.95), max: Math.max(...rs.map((r) => r.latency)) },
      inputTokens: mean(ok.map((r) => r.inputTokens).filter((x) => x != null)),
      cachedTokens: mean(ok.map((r) => r.cachedTokens || 0)),
      respModels: [...new Set(ok.map((r) => r.respModel))],
    });
  }
  rows.sort((a, b) => a.id.localeCompare(b.id));

  // 묶음 집계
  const lat = (rs) => ({ n: rs.length, p50: pct(rs.map((r) => r.latency), 0.5), p95: pct(rs.map((r) => r.latency), 0.95), mean: mean(rs.map((r) => r.latency)), tokens: mean(rs.filter((r) => r.ok).map((r) => r.inputTokens)), chars: mean(rs.map((r) => r.inputChars)), cands: mean(rs.map((r) => r.nCands)) });
  const bySet = {};
  for (const s of ['14', '20', 'base']) bySet[s] = lat(recs.filter((r) => r.set === s));
  const byKnow = { N: lat(recs.filter((r) => r.set !== 'base' && !r.know)), K: lat(recs.filter((r) => r.set !== 'base' && r.know)) };
  const tokBins = {};
  for (const r of recs.filter((x) => x.ok)) {
    const b = r.inputTokens < 2400 ? '<2400' : r.inputTokens < 2800 ? '2400-2799' : r.inputTokens < 3200 ? '2800-3199' : '>=3200';
    (tokBins[b] = tokBins[b] || []).push(r);
  }
  const byTokens = Object.fromEntries(Object.entries(tokBins).map(([k, v]) => [k, lat(v)]));
  // 시나리오별 지식 효과·후보 수 효과
  const effects = [];
  for (const sc of SCENARIOS) {
    const g = (know, set) => rows.find((r) => r.scenario === sc.id && r.know === know && r.set === set);
    const cell = (r) => (r ? { good: r.good, bad: r.bad, other: r.other, ok: r.ok, goodMass: r.goodMass, badMass: r.badMass, modal: r.modalShare, distinct: r.distinct } : null);
    effects.push({ scenario: sc.id, N14: cell(g(false, '14')), K14: cell(g(true, '14')), N20: cell(g(false, '20')), K20: cell(g(true, '20')) });
  }
  const totalTok = recs.reduce((s, r) => s + (r.inputTokens || 0), 0);
  const fails = recs.filter((r) => !r.ok).map((r) => ({ id: r.id, rep: r.rep, status: r.status, netErr: r.netErr, error: r.error, type: r.type, body: (r.body || '').slice(0, 300) }));
  const summary = { calls: recs.length, ok: recs.filter((r) => r.ok).length, totalInputTokens: totalTok, usdAtSettingRate: totalTok * usdPerM / 1e6, usdPerM, latencyAll: lat(recs), bySet, byKnow, byTokens, effects, rows, fails };
  fs.writeFileSync(path.join(dir, 'summary.json'), JSON.stringify(summary, null, 2));

  // 사람이 읽는 표
  const L = [];
  L.push(`calls=${summary.calls} ok=${summary.ok} inputTokens=${totalTok} usd(at ${usdPerM}/M)=${summary.usdAtSettingRate.toFixed(4)}`);
  L.push(`latency all p50=${f3(summary.latencyAll.p50)} p95=${f3(summary.latencyAll.p95)}`);
  for (const [k, v] of Object.entries(bySet)) L.push(`set ${k}: n=${v.n} cands=${f2(v.cands)} chars=${f2(v.chars)} tokens=${f2(v.tokens)} p50=${f3(v.p50)} p95=${f3(v.p95)} mean=${f3(v.mean)}`);
  for (const [k, v] of Object.entries(byKnow)) L.push(`know ${k}: n=${v.n} tokens=${f2(v.tokens)} p50=${f3(v.p50)} p95=${f3(v.p95)}`);
  for (const [k, v] of Object.entries(byTokens)) L.push(`tokens ${k}: n=${v.n} p50=${f3(v.p50)} p95=${f3(v.p95)}`);
  L.push('');
  L.push('id | cands | chars | tok | ok | good/bad/other | goodMass | badMass | modal | distinct | conf | lat p50/p95 | dist');
  for (const r of rows) L.push(`${r.id} | ${r.nCands} | ${r.inputChars} | ${f2(r.inputTokens)} | ${r.ok}/${r.n} | ${r.good}/${r.bad}/${r.other} | ${f2(r.goodMass)} | ${f2(r.badMass)} | ${f2(r.modalShare)} | ${r.distinct} | ${f2(r.confidence)} | ${f3(r.lat.p50)}/${f3(r.lat.p95)} | ${Object.entries(r.dist).map(([k, v]) => `${k}×${v}`).join(' ')}`);
  L.push('');
  L.push('평균 확률(후보별, 0.03 이상):');
  for (const r of rows) L.push(`${r.id}: ${Object.entries(r.probAvg).filter(([, v]) => v >= 0.03).sort((a, b) => b[1] - a[1]).map(([k, v]) => `${k}=${v.toFixed(2)}`).join(', ')}`);
  L.push('');
  L.push(`fails: ${fails.length}`);
  for (const f of fails) L.push(JSON.stringify(f));
  fs.writeFileSync(path.join(dir, 'summary.txt'), L.join('\n'));
  console.log(L.join('\n'));
  return summary;
}

// 2차(흔들림) 집계: 기준(1차) 선택과 비교
export function analyzePerturb(dir) {
  const base = {};
  for (const l of fs.readFileSync(path.join(dir, 'raw', 'results.jsonl'), 'utf8').split('\n').filter(Boolean)) { const r = JSON.parse(l); if (r.rep === 1) base[r.id] = r; }
  const recs = fs.readFileSync(path.join(dir, 'raw', 'perturb.jsonl'), 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
  const L = []; const rows = [];
  let same = 0, n = 0;
  for (const r of recs) {
    const bid = r.id.replace(/-(swap|numeric|shuffle)$/, ''); const kind = r.id.match(/(swap|numeric|shuffle)$/)[1];
    const b = base[bid]; const J = JUDGE[r.scenario];
    const k = r.ok ? keyOfValue(r, r.choice) : 'FAIL'; const bk = keyOfValue(b, b.choice);
    const cls = (x) => (!J ? '-' : J.good.includes(x) ? 'good' : J.bad.includes(x) ? 'bad' : 'other');
    const pOf = (rec, key) => (rec.probabilities || []).filter((q) => keyOfValue(rec, q.value) === key).reduce((s, q) => s + q.probability, 0);
    n++; if (k === bk) same++;
    rows.push({ id: r.id, base: bk, now: k, same: k === bk, baseClass: cls(bk), nowClass: cls(k), pNow: pOf(r, k), pBaseKeyNow: pOf(r, bk), pBaseKeyBase: pOf(b, bk), latency: r.latency });
    L.push(`${r.id}: ${bk}(${cls(bk)}) → ${k}(${cls(k)}) ${k === bk ? '같음' : '바뀜'} p(now)=${pOf(r, k).toFixed(2)} p(base key) ${pOf(b, bk).toFixed(2)}→${pOf(r, bk).toFixed(2)} lat=${r.latency.toFixed(2)}`);
  }
  const byKind = {}; for (const x of rows) { const kd = x.id.match(/(swap|numeric|shuffle)$/)[1]; byKind[kd] = byKind[kd] || { n: 0, same: 0, classSame: 0 }; byKind[kd].n++; if (x.same) byKind[kd].same++; if (x.baseClass === x.nowClass) byKind[kd].classSame++; }
  const bySet = {}; for (const x of rows) { const st = x.id.split('-')[2]; bySet[st] = bySet[st] || { n: 0, same: 0, classSame: 0 }; bySet[st].n++; if (x.same) bySet[st].same++; if (x.baseClass === x.nowClass) bySet[st].classSame++; }
  L.unshift(`perturb: n=${n} 같은 선택 ${same}; 종류별 ${JSON.stringify(byKind)}; 후보 수별 ${JSON.stringify(bySet)}`);
  fs.writeFileSync(path.join(dir, 'summary-perturb.json'), JSON.stringify({ n, same, byKind, bySet, rows }, null, 2));
  fs.writeFileSync(path.join(dir, 'summary-perturb.txt'), L.join('\n'));
  console.log(L.join('\n'));
}
