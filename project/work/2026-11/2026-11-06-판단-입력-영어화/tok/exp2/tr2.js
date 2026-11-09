// Template translator for logged Decisions requests (experiment 2).
// A real implementation would translate each code format string once; this mirrors that: the logged text is cut into
// segments, numbers / coordinates / ASCII ids / direction words are masked into slots, and each masked segment (a
// "key") is looked up in dict.json (Korean key with {} slots -> English with {0} {1} ... slots, may reorder).
// A few combinatorial lines (recent results, current task, trigger, switch history, held switch) are parsed in code
// so their parts are translated separately. Unknown keys stay Korean and are counted.
const fs = require('fs');
const path = require('path');
const H = /[가-힣]/;
const HG = /[가-힣]/g;
let DICT = {};
const dictPath = path.join(__dirname, 'dict.json');
if (fs.existsSync(dictPath)) DICT = JSON.parse(fs.readFileSync(dictPath, 'utf8'));
const MISS = {};           // key -> {n, ex}
let CTX = null;            // current request (for truncated desc lookup)
const STATUS = { 완료: 'done', 실패: 'failed', 취소: 'cancelled' };
const DIRW = { 동: 'east', 서: 'west', 남: 'south', 북: 'north' };
const DIRS = { 동: 'E', 서: 'W', 남: 'S', 북: 'N' };

function split(text) { // depth-0 ". ", " — ", " / ", keeping separators
  const out = []; let depth = 0, cur = '';
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (ch == '(' || ch == '[') depth++; else if (ch == ')' || ch == ']') depth = Math.max(0, depth - 1);
    if (depth == 0) {
      if (ch == '.' && text[i + 1] == ' ') { out.push(cur + '.'); out.push({ sep: ' ' }); cur = ''; i++; continue; }
      if (text.startsWith(' — ', i)) { out.push(cur); out.push({ sep: ' — ' }); cur = ''; i += 2; continue; }
      if (text.startsWith(' / ', i)) { out.push(cur); out.push({ sep: ' / ' }); cur = ''; i += 2; continue; }
    }
    cur += ch;
  }
  out.push(cur); return out;
}
function splitAt(text, seps) { // depth-0 split at the given separators (kept)
  const out = []; let depth = 0, cur = '';
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (ch == '(' || ch == '[') depth++; else if (ch == ')' || ch == ']') depth = Math.max(0, depth - 1);
    let hit = null;
    if (depth == 0) for (const s of seps) if (text.startsWith(s, i)) { hit = s; break; }
    if (hit) { out.push(cur); out.push({ sep: hit }); cur = ''; i += hit.length - 1; continue; }
    cur += ch;
  }
  out.push(cur); return out;
}
const TOK = /(동|서|남|북)쪽|\((동|서|남|북)\)|[-+]?[A-Za-z0-9_][A-Za-z0-9_:.,\-+#]*[A-Za-z0-9_%]|[-+]?[A-Za-z0-9_]/g;
function mask(seg) {
  const vals = [];
  const key = seg.replace(TOK, (m, d1, d2) => {
    if (d1) { vals.push(DIRW[d1]); return '{}쪽'; }
    if (d2) { vals.push('(' + DIRS[d2] + ')'); return '{}'; }
    vals.push(m); return '{}';
  });
  return { key, vals };
}
function fill(en, vals) { return en.replace(/\{(\d+)\}/g, (m, i) => vals[+i] !== undefined ? vals[+i] : m); }
function look(seg) { // whole-segment lookup; null if unknown
  const lead = seg.match(/^\s*/)[0], trail = seg.match(/\s*$/)[0];
  const core = seg.trim();
  if (!H.test(core)) return seg;
  const { key, vals } = mask(core);
  if (Object.prototype.hasOwnProperty.call(DICT, key)) return lead + fill(DICT[key], vals) + trail;
  return null;
}
function miss(seg, parent) { const { key } = mask(seg.trim()); const m = MISS[key] || (MISS[key] = { n: 0, ex: seg.trim(), ctx: (parent || seg).trim() }); m.n++; }
function T(seg) { // segment translate with fallbacks
  if (!H.test(seg)) return seg;
  const s = rules(seg); if (s !== null) return s;
  let r = look(seg); if (r !== null) return r;
  // a trailing period: try without it
  if (/\.$/.test(seg.trim())) { r = look(seg.trim().slice(0, -1)); if (r !== null) return r + '.'; }
  // finer: depth-0 ", " and "; "
  const parts = splitAt(seg, ['; ', ', ']);
  if (parts.length > 1) {
    const out = parts.map(p => typeof p == 'string' ? (H.test(p) ? (rules(p) ?? look(p) ?? (miss(p, seg), p)) : p) : p.sep);
    return out.join('');
  }
  miss(seg); return seg;
}
function TT(text) { // a text with sentences
  const l = lineRules(text); if (l !== null) return l;
  return split(text).map(p => typeof p == 'string' ? T(p) : p.sep).join('');
}
function NAME(n) { return look(n) ?? (miss(n), n); }
function TRUNC(ko) { // a truncated desc (ends with …) or a whole short desc: translate the matching full option
  const pre = ko.replace(/…$/, '');
  if (!ko.endsWith('…')) return TT(ko);
  const full = CTX && CTX.opts.find(o => o.desc.startsWith(pre));
  if (full) { const en = TT(full.desc); const n = Math.max(8, Math.round(pre.length * en.length / full.desc.length)); return en.slice(0, n).trimEnd() + '…'; }
  // not among this request's options: translate what can be, segment by segment
  const ps = split(pre); const out = [];
  for (const p of ps) { if (typeof p != 'string') { out.push(p.sep); continue; } const t = rules(p) ?? look(p); if (t !== null) out.push(t); else { const h = p.split(' — ')[0]; const th = look(h); out.push(th !== null ? th : (miss(p), p)); } }
  return out.join('') + '…';
}
function TRIG(t) {
  let m;
  if ((m = t.match(/^기술 끝\((.+) (완료|실패|취소)\)$/))) return `skill ended (${NAME(m[1])} ${STATUS[m[2]]})`;
  if ((m = t.match(/^곧 끝남: 지금 하는 '(.+)'\((\S+)\)이 약 ([\d.]+)초 뒤 끝남(.*)$/))) return `ending soon: the current '${NAME(m[1])}' (${m[2]}) ends in about ${m[3]}s${m[4] ? TT(m[4]) : ''}`;
  if ((m = t.match(/^자유 시간 시작: (.+)$/))) return `free time started: ${TT(m[1])}`;
  if ((m = t.match(/^자기 방어: (.+)$/))) return `self-defense: ${TT(m[1])}`;
  if ((m = t.match(/^재시도\((.+)\)$/))) return `retry (${TT(m[1])})`;
  if ((m = t.match(/^이전 답 버림\((.+)\)$/))) return `previous answer dropped (${TT(m[1])})`;
  if ((m = t.match(/^기다리던 바꾸기 버림\((.+)\)$/))) return `pending switch dropped (${TT(m[1])})`;
  if ((m = t.match(/^미리 받은 답이 맞지 않음\((.+)\)$/))) return `prefetched answer does not fit (${TT(m[1])})`;
  if ((m = t.match(/^다음 휘두르기 못 함\((.+)\)$/))) return `cannot do the next swing (${TT(m[1])})`;
  return TT(t);
}
function RECENT(item) { // "{Name} 완료: {Reason} (x초)"
  const m = item.match(/^(.+?) (완료|실패|취소): (.+) \(([\d.]+)초\)$/);
  if (!m) return null;
  return `${NAME(m[1])} ${STATUS[m[2]]}: ${REASON(m[3])} (${m[4]}s)`;
}
function REASON(r) {
  let m;
  if ((m = r.match(/^새 기술 (.+)\(으\)로 바뀜$/))) return `switched to new skill ${NAME(m[1])}`;
  if (r.endsWith('…')) { const t = TT(r.slice(0, -1)); return t + '…'; }
  return TT(r);
}
function rules(seg) { // segment-level structural rules; null if none applies
  return null;
}
function lineRules(line) {
  let m;
  if ((m = line.match(/^(최근 결과|직전 행동 결과): (.+)\.$/))) {
    const label = m[1] == '최근 결과' ? 'Recent results' : 'Last action result';
    if (m[2] == '없음') return `${label}: none.`;
    const items = m[2].split(' / ').map(it => RECENT(it) ?? TT(it));
    return `${label}: ${items.join(' / ')}.`;
  }
  if ((m = line.match(/^지금 하는 일: (.+) (\S+) (\d+)초째\.$/))) return `Current task: ${NAME(m[1])} ${m[2]}, ${m[3]}s in.`;
  if ((m = line.match(/^지금 다시 판단하는 이유: (.+)\.$/))) return `Why deciding again now: ${TRIG(m[1])}.`;
  if ((m = line.match(/^바꾸기 대기 중: 앞 판단이 '(.+)'을\(를\) 골라 지금 동작이 끝나기를 기다리는 중\. (.*)$/))) return `Switch pending: the previous decision chose '${TRUNC(m[1])}' and is waiting for the current motion to end. ${TT(m[2])}`;
  if (/^최근 10초 동안 (행동을|긴 동작|짧은 동작)/.test(line)) return SWITCHES(line);
  return null;
}
function ITEMS(s) { // "from(t초 만에)→to, from(...)→to"
  const parts = s.split(/, (?=[^,()]*\([\d.]+초 만에\)→)/);
  return parts.map(p => { const m = p.match(/^(.+?)\(([\d.]+)초 만에\)→(.*)$/); if (!m) { miss(p); return p; } const to = m[3] == '계속' ? 'continue' : TRUNC(m[3]); return `${NAME(m[1])} (after ${m[2]}s)→${to}`; }).join(', ');
}
function SWITCHES(line) {
  let m, out = [];
  let rest = line;
  if ((m = rest.match(/^최근 10초 동안 행동을 (\d+)번 바꿈: (.+)\. 바꿀 때마다 새 행동은 처음부터 시작했다\.$/))) return `Switches in the last 10s: ${m[1]}: ${ITEMS(m[2])}. Each time, the new action started from the beginning.`;
  if ((m = rest.match(/^최근 10초 동안 긴 동작\(공격\(계속\)·거리 벌리기·먹기 등\)이 걸린 전환 (\d+)번: (.+?)\. 긴 동작은 바꾸면 처음부터 다시 시작한다\.( |$)/))) {
    out.push(`Switches involving a long action (attack (continuous), keep distance, eat, ...) in the last 10s: ${m[1]}: ${ITEMS(m[2])}. A long action restarts from the beginning when switched.`);
    rest = rest.slice(m[0].length);
  }
  if ((m = rest.match(/^최근 10초 동안 짧은 동작\(짧은 이동·한 번 치기·접근·제자리·복합\)끼리 바꾼 것 (\d+)번\(짧은 동작은 고르면 바로 시작하고 끝나면 바로 다시 판단하게 되어 있다\)\.$/))) {
    out.push(`Switches between short actions (short move, single hit, approach, hold, composite) in the last 10s: ${m[1]} (a short action starts as soon as it is chosen, and a new decision follows as soon as it ends).`);
    rest = '';
  }
  if (rest.trim()) { out.push(TT(rest)); }
  return out.join(' ');
}
// sentence starts upper-case (fragments translated apart can leave a lower-case start after ". ")
const CAP = t => t.replace(/(^|\. )([a-z])/g, (m, a, b) => a + b.toUpperCase());
function translateRequest(x) {
  CTX = x;
  const input = x.input.split('\n').map(l => CAP(TT(l))).join('\n');
  const opts = x.opts.map(o => ({ value: o.value, desc: CAP(TT(o.desc)) }));
  CTX = null;
  return { input, opts };
}
const INSTR_KO = '목표와 지금 상황에서 {N}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).';
const INSTR_EN = 'Given the goal and the current situation, choose the one action {N} should take next. Every choice is an action that can actually be started now. If the current action (continue) still fits, keep it; switch when a new fact has appeared (a new action starts from the beginning).';
module.exports = { translateRequest, MISS, INSTR_KO, INSTR_EN, H, HG, mask, split, DICT };
