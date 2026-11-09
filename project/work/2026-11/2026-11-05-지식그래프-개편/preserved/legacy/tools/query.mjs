#!/usr/bin/env node
// query.mjs — graph/graph.json 을 읽어 묻는다. 그래프는 바꾸지 않는다.
// 사용: node tools/query.mjs <명령> [인자]
//   list <타입>        정책 | 후보 | 기능 | 미결 | 코드 | 게임 | 제품코어
//   show <ID>          노드 하나와 들고 나는 연결
//   why <ID>           기능·코드·게임 지점·정책이 어떤 결정과 근거에 기대나
//   code <기능ID>      기능의 앵커, 선 위 함수(줄), 닿는 게임 지점(디컴파일 위치)
//   impact <ID>        코드·게임 지점·정책을 바꾸면 어느 기능·정책이 걸리나
//   unresolved [종류]  미결 목록
//   find <글>          ID·이름·규칙·로그 글에서 찾기
//   check              그래프가 원본과 맞는지(해시), 인용이 아직 원문에 있는지
// ID는 앞부분만 써도 된다(예: Step3, F.ai-char, SaveManager.GetCharacter).
import fs from 'node:fs';
import path from 'node:path';
import { ROOT, SESSION, cfg, sha256File, verifyQuote, cmp } from './lib.mjs';

const G = JSON.parse(fs.readFileSync(path.join(ROOT, cfg.out), 'utf8'));
const N = new Map(G.nodes.map((n) => [n.id, n]));
const out = (s = '') => console.log(s);
const outE = (id, type) => G.edges.filter((e) => e.from === id && (!type || (type instanceof RegExp ? type.test(e.type) : e.type === type)));
const inE = (id, type) => G.edges.filter((e) => e.to === id && (!type || (type instanceof RegExp ? type.test(e.type) : e.type === type)));
const label = (id) => {
  const n = N.get(id);
  if (!n) return id;
  const extra = n.status ? ` [${n.status}]` : n.firmness ? ` [${n.grade}·${n.firmness}]` : n.grade && n.type === '후보' ? ` [${n.grade}]` : '';
  return `${id}${extra}${n.title && n.title !== id && !id.endsWith(n.title) ? ' — ' + n.title : ''}${n.loc ? '  @' + n.loc : ''}`;
};

function staleness() {
  const changed = [];
  for (const s of G.meta.sources) {
    const p = /^[A-Za-z]:\//.test(s.path) ? s.path : path.join(SESSION, s.path);
    if (!fs.existsSync(p)) changed.push(`${s.path} (없음)`);
    else if (sha256File(p) !== s.sha256) changed.push(s.path);
  }
  // codeDirs 아래 .cs 중 그래프가 모르는 것(빌드 뒤 새로 생긴 파일)도 낡음으로 본다.
  const known = new Set(G.meta.sources.map((s) => s.path.toLowerCase()));
  const walk = (d, acc = []) => {
    for (const e of fs.readdirSync(d, { withFileTypes: true })) {
      const q = path.join(d, e.name);
      if (e.isDirectory()) walk(q, acc);
      else if (e.name.endsWith(".cs")) acc.push(q.split(path.sep).join("/"));
    }
    return acc;
  };
  for (const d of cfg.codeDirs || []) {
    const abs = path.join(path.resolve(cfg.codeRoot), d);
    if (!fs.existsSync(abs)) { changed.push(`${d} (codeDirs 폴더 없음)`); continue; }
    for (const f of walk(abs).sort(cmp)) if (!known.has(f.toLowerCase())) changed.push(`${f} (새 파일, codeFiles에 넣는다)`);
  }
  return changed;
}
function warnStale() {
  const c = staleness();
  if (c.length) {
    out(`주의: 그래프가 원본보다 낡았다. 바뀐 원본: ${c.join(', ')}`);
    out('      줄 번호와 선이 틀릴 수 있다. node tools/build.mjs 로 다시 만든다.');
    out();
  }
}

function pick(q) {
  if (!q) die('ID가 필요하다');
  for (const k of [q, `code:${q}`, `game:${q}`]) if (N.has(k)) return k;
  const lq = q.toLowerCase();
  const c = G.nodes.filter((n) => n.id.toLowerCase().includes(lq) && !n.id.startsWith('auto:'));
  if (c.length === 1) return c[0].id;
  const exactTail = c.filter((n) => n.id.toLowerCase().endsWith(lq));
  if (exactTail.length === 1) return exactTail[0].id;
  if (!c.length) die(`"${q}"에 맞는 ID가 없다. find 로 찾아본다. (빈 결과는 "그런 것이 없다"는 뜻이 아니다. 그래프가 그 해상도로 담지 않았을 수 있다.)`);
  out(`"${q}"에 맞는 ID가 여럿이다:`);
  for (const n of c.slice(0, 40)) out(`  ${label(n.id)}`);
  process.exit(2);
}
// 게임 형식을 물으면 그 멤버, 안쪽 형식, 그 안에 속한 기록 자리까지 함께 본다.
function gameFamily(id) {
  const fam = new Set([id]);
  for (const n of G.nodes) if (n.type === '게임' && n.id.startsWith(id + '.')) fam.add(n.id);
  for (const e of G.edges) if (e.type === '속함' && fam.has(e.to) && e.from.startsWith('game:site:')) fam.add(e.from);
  return [...fam];
}
function die(m) {
  out(m);
  process.exit(2);
}
function printSources(n, ind = '  ') {
  for (const s of n.sources || []) out(`${ind}근거${s.status === 'ok' ? '' : '(원문에서 못 찾음!)'}: ${s.path} "${s.quote}"`);
}
function unresolvedFor(ids) {
  const set = new Set(ids);
  return [...new Set(G.edges.filter((e) => e.type === '관련' && set.has(e.to) && N.get(e.from)?.type === '미결').map((e) => e.from))].sort(cmp);
}
function policiesOf(fid) {
  return {
    pol: inE(fid, '적용').map((e) => e.from),
    cand: inE(fid, '관련').map((e) => e.from).filter((x) => N.get(x)?.type === '후보'),
  };
}

const [cmd, ...args] = process.argv.slice(2);
const arg = args.join(' ');

if (cmd !== 'check') warnStale();

switch (cmd) {
  case 'list': {
    const t = arg || '기능';
    for (const n of G.nodes.filter((n) => n.type === t)) out(label(n.id) + (n.rule ? `\n    ${n.rule}` : n.summary ? `\n    ${n.summary}` : n.content ? `\n    ${n.content}` : ''));
    break;
  }
  case 'show': {
    const id = pick(arg);
    const n = N.get(id);
    out(JSON.stringify(n, null, 1));
    out('\n나가는 연결:');
    for (const e of outE(id)) out(`  -${e.type}-> ${label(e.to)}${e.line ? ' (줄 ' + e.line + ')' : ''}`);
    out('들어오는 연결:');
    for (const e of inE(id)) out(`  <-${e.type}- ${label(e.from)}${e.line ? ' (줄 ' + e.line + ')' : ''}`);
    break;
  }
  case 'why': {
    const id = pick(arg);
    const n = N.get(id);
    const showFeature = (fid, ind = '') => {
      const f = N.get(fid);
      out(`${ind}기능 ${label(fid)}`);
      if (f.summary) out(`${ind}  ${f.summary}`);
      if (f.observe) out(`${ind}  관측 지점: ${f.observe}`);
      printSources(f, ind + '  ');
      const { pol, cand } = policiesOf(fid);
      if (!pol.length) out(`${ind}  정책: 없음 (이 동작을 정한 사용자 결정이 그래프에 없다)`);
      for (const p of pol) {
        const P = N.get(p);
        out(`${ind}  정책 ${p} [${P.grade}·${P.firmness}] ${P.rule}`);
        printSources(P, ind + '    ');
      }
      for (const c of cand) {
        const C = N.get(c);
        out(`${ind}  후보(정책 아님) ${c} [${C.grade}] ${C.content}`);
        printSources(C, ind + '    ');
      }
    };
    if (n.type === '기능') showFeature(id);
    else if (n.type === '정책' || n.type === '후보') {
      out(label(id));
      out(`  ${n.rule || n.content}`);
      printSources(n);
      if (n.note) out(`  메모: ${n.note}`);
      const fs_ = outE(id).filter((e) => N.get(e.to)?.type === '기능').map((e) => e.to);
      out(`  이 ${n.type}에 기대는 기능: ${fs_.length ? fs_.join(', ') : '없음'}`);
    } else if (n.type === '코드') {
      const fs_ = inE(id, '지남').map((e) => e.from);
      out(label(id));
      if (n.entry) out(`  ${n.entry}`);
      if (n.offline) out(`  선 밖 코드로 분류됨: ${n.offline}`);
      if (!fs_.length && !n.offline) out('  어떤 기능 선에도 없다.');
      for (const f of fs_) showFeature(f, '  ');
    } else if (n.type === '게임') {
      out(label(id));
      const fam = gameFamily(id);
      if (fam.length > 1) out(`  (멤버·안쪽 형식·기록 자리 ${fam.length - 1}개 포함)`);
      const users = fam.flatMap((g) => inE(g, /^(참조|사용|리플렉션 필드|패치)/).map((e) => `${e.from} -${e.type}-> ${g}`));
      out(`  모드에서 닿는 곳: ${users.length ? '' : '없음'}`);
      for (const u of users) out(`    ${u}`);
      const fs_ = [...new Set(fam.flatMap((g) => inE(g, /^(닿음|게임 지점)$/).map((e) => e.from)))].sort(cmp);
      for (const f of fs_) showFeature(f, '  ');
    } else {
      out(label(id));
      out(JSON.stringify(n, null, 1));
    }
    const u = unresolvedFor([id, ...(n.type === '기능' ? policiesOf(id).pol : [])]);
    if (u.length) {
      out('\n관련 미결:');
      for (const x of u) out(`  ${x} [${N.get(x).kind}] ${N.get(x).title}`);
    }
    break;
  }
  case 'code': {
    const id = pick(arg);
    const n = N.get(id);
    if (n.type !== '기능') die(`${id}는 기능이 아니다. impact 나 why 를 쓴다.`);
    out(label(id));
    if (n.lineHash) out(`  선 서명: ${n.lineHash}${n.verifiedLine ? `  (실게임 확인한 선: ${n.verifiedLine}${n.verifiedLine === n.lineHash ? ', 같음' : ', 다름 — 다시 시험 필요'})` : ''}`);
    const anchors = outE(id, '처리').map((e) => e.to);
    if (!anchors.length) out('  코드 앵커 없음. 코드가 없는 기능이다(상태를 본다).');
    out('  앵커:');
    for (const a of anchors) out(`    ${label(a)}${N.get(a).entry ? '  (' + N.get(a).entry + ')' : ''}`);
    const line = outE(id, '지남').map((e) => e.to).filter((x) => !anchors.includes(x));
    out(`  선 위 함수(앵커에서 호출로 닿음) ${line.length}개:`);
    for (const x of line) out(`    ${label(x)}${(N.get(x).features || []).length > 1 ? '  (함께 쓰는 기능: ' + N.get(x).features.filter((f) => f !== id).join(', ') + ')' : ''}`);
    const touch = [...outE(id, '닿음'), ...outE(id, '게임 지점')].map((e) => ({ to: e.to, via: e.type }));
    const kinds = { 패치: [], '리플렉션(비공개 필드)': [], '함수·멤버': [], 형식: [], '게임 지점(기록)': [] };
    for (const t of touch) {
      const g = N.get(t.to);
      const patched = G.edges.find((e) => e.to === t.to && e.type.startsWith('패치('));
      if (t.via === '게임 지점') kinds['게임 지점(기록)'].push(`${label(t.to)}\n        ${g.text}`);
      else if (patched) kinds['패치'].push(`${label(t.to)}  ← ${patched.from} ${patched.type}${g.gameCallers ? `\n        게임 안 호출: ${g.gameCallers.join(', ')}` : ''}`);
      else if (g.private) kinds['리플렉션(비공개 필드)'].push(label(t.to));
      else if (!g.ns) kinds['함수·멤버'].push(label(t.to));
      else kinds['형식'].push(label(t.to));
    }
    out('  닿는 게임 지점:');
    for (const [k, v] of Object.entries(kinds)) if (v.length) {
      out(`    ${k} ${v.length}:`);
      for (const x of v) out(`      ${x}`);
    }
    break;
  }
  case 'impact': {
    const id = pick(arg);
    const n = N.get(id);
    out(label(id));
    if (n.type === '코드') {
      const callers = inE(id, '부름').map((e) => e.from);
      out(`  부르는 함수: ${callers.length ? callers.join(', ') : '없음'}${n.entry ? '  / ' + n.entry : ''}`);
      const fs_ = inE(id, '지남').map((e) => e.from);
      out(`  이 함수를 지나는 기능: ${fs_.length ? '' : '없음'}${n.offline ? ' (선 밖: ' + n.offline + ')' : ''}`);
      for (const f of fs_) {
        const { pol } = policiesOf(f);
        out(`    ${label(f)}\n      정책: ${pol.join(', ') || '없음'}`);
      }
    } else if (n.type === '게임') {
      const fam = gameFamily(id);
      if (fam.length > 1) out(`  (멤버·안쪽 형식·기록 자리 ${fam.length - 1}개 포함)`);
      const users = fam.flatMap((g) => inE(g, /^(참조|사용|리플렉션 필드|패치)/));
      out(`  이 게임 지점에 기대는 모드 함수 ${users.length}:`);
      for (const e of users) out(`    ${label(e.from)}  -${e.type}-> ${e.to}${e.line ? ' (줄 ' + e.line + ')' : ''}`);
      const fs_ = [...new Set(fam.flatMap((g) => inE(g, /^(닿음|게임 지점)$/).map((e) => e.from)))].sort(cmp);
      out(`  걸리는 기능: ${fs_.join(', ') || '없음'}`);
      if (n.gameCallers) out(`  게임 안에서 이 함수를 부르는 곳: ${n.gameCallers.join(', ')}`);
    } else if (n.type === '정책' || n.type === '후보') {
      const fs_ = outE(id).filter((e) => N.get(e.to)?.type === '기능').map((e) => e.to);
      for (const f of fs_) {
        out(`  ${label(f)}`);
        for (const a of outE(f, '처리')) out(`    앵커 ${label(a.to)}`);
      }
      if (!fs_.length) out('  이 결정을 지는 기능이 없다.');
    } else if (n.type === '기능') {
      for (const e of outE(id)) out(`  -${e.type}-> ${label(e.to)}`);
    }
    const u = unresolvedFor([id]);
    if (u.length) {
      out('\n관련 미결:');
      for (const x of u) out(`  ${x} [${N.get(x).kind}] ${N.get(x).title}`);
    }
    break;
  }
  case 'unresolved': {
    const us = G.nodes.filter((n) => n.type === '미결' && (!arg || n.kind.includes(arg)));
    out(`미결 ${us.length}개 (${Object.entries(G.meta.unresolvedByKind).map(([k, v]) => `${k} ${v}`).join(' · ')})`);
    for (const u of us.sort((a, b) => cmp(a.kind, b.kind) || cmp(a.id, b.id))) {
      out(`\n[${u.kind}] ${u.id} — ${u.title}${u.origin === '계산' ? '  (도구가 계산)' : ''}`);
      if (u.closeBy) out(`  닫는 법: ${u.closeBy}`);
      const rel = outE(u.id, '관련').map((e) => e.to);
      if (rel.length) out(`  관련: ${rel.join(', ')}`);
      printSources(u);
      if (u.items) for (const it of u.items) out(`    - ${it}`);
    }
    break;
  }
  case 'find': {
    if (!arg) die('찾을 글이 필요하다');
    const q = arg.toLowerCase();
    const hits = G.nodes.filter((n) => [n.id, n.title, n.rule, n.summary, n.content, n.observe, ...(n.logs || [])].some((v) => v && String(v).toLowerCase().includes(q)));
    if (!hits.length) out(`"${arg}"가 그래프에 없다. 그래프에 없다는 것은 세상에 없다는 뜻이 아니다. 원본(코드·기록)을 직접 찾아본다.`);
    for (const n of hits) {
      out(label(n.id));
      for (const l of (n.logs || []).filter((l) => l.toLowerCase().includes(q))) out(`    로그: "${l}"`);
    }
    break;
  }
  case 'check': {
    const c = staleness();
    out(c.length ? `낡음: 바뀐 원본 ${c.length}개\n  ${c.join('\n  ')}\n  → node tools/build.mjs` : '원본 해시가 모두 그래프와 같다.');
    let bad = 0;
    for (const n of G.nodes) for (const s of n.sources || []) {
      const now = verifyQuote(s.path, s.quote);
      if (now !== 'ok') { bad++; out(`  인용 못 찾음: ${n.id} ← ${s.path} "${s.quote.slice(0, 60)}"`); }
    }
    out(bad ? `인용 ${bad}개가 지금 원문에 없다.` : '인용이 모두 지금 원문에 있다.');
    const hard = G.nodes.filter((n) => n.type === '미결' && ['인용 깨짐', '앵커 없음', '없는 ID', '원본 없음', '원본 빠짐', '게임 지점 낡음'].includes(n.kind));
    out(hard.length ? `그래프 안 경고 ${hard.length}개: ${hard.map((h) => h.id).join(', ')}` : '그래프 안 경고(인용·앵커·ID) 없음.');
    process.exit(c.length || bad || hard.length ? 1 : 0);
  }
  default:
    out(fs.readFileSync(new URL(import.meta.url)).toString().split('\n').filter((l) => l.startsWith('//')).map((l) => l.slice(3)).join('\n'));
}
