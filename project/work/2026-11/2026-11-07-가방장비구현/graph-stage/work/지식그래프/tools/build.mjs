#!/usr/bin/env node
// build.mjs — 원본에서 그래프를 처음부터 다시 만든다.
//   원본: 기획 문서(제품코어·정책·기능·미결), 모드 코드(C#), 디컴파일한 게임 코드.
//   결과: graph/graph.json(그래프), graph/요약.md(사람이 읽는 요약).
// 같은 원본이면 결과가 바이트까지 같다. 그래프 파일을 손으로 고치지 않는다.
// 사용: node tools/build.mjs [--strict]
//   --strict: 인용 깨짐·앵커 없음·없는 ID가 하나라도 있으면 종료 코드 1.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { ROOT, cfg, readText, fileState, verifyQuote, cmp } from './lib.mjs';

const STRICT = process.argv.includes('--strict');
const nodes = new Map();
const edges = [];
const unresolved = []; // 계산으로 생긴 미결
const execForms = {}; // 호출 꼴 → 개수

function addNode(n) {
  if (nodes.has(n.id)) Object.assign(nodes.get(n.id), n);
  else nodes.set(n.id, n);
  return nodes.get(n.id);
}
function addEdge(from, type, to, cls, extra = {}) {
  edges.push({ from, type, to, class: cls, ...extra });
}
function autoU(kind, key, title, closeBy, related = [], items = undefined) {
  unresolved.push({ id: `auto:${kind.replace(/\s+/g, '-')}:${key}`, kind, title, closeBy, related, items });
}
function bump(form, n = 1) {
  execForms[form] = (execForms[form] || 0) + n;
}

// ───────────────────────────────────────── 1. 기획 문서
function parseDoc(rel) {
  const text = readText(path.join(ROOT, rel));
  const lines = text.split('\n');
  const out = [];
  let cur = null;
  let inFields = false;
  lines.forEach((ln, i) => {
    const h = ln.match(/^###\s+(\S+)\s+—\s+(.+?)\s*$/);
    if (h) {
      cur = { id: h[1], title: h[2], fields: {}, doc: rel, line: i + 1 };
      out.push(cur);
      inFields = true;
      return;
    }
    if (cur && inFields) {
      const f = ln.match(/^- ([^:]+):\s*(.*)$/);
      if (f) {
        (cur.fields[f[1].trim()] ||= []).push(f[2].trim());
        return;
      }
      inFields = false;
    }
    if (/^#{1,2}\s/.test(ln)) cur = null;
  });
  return { nodes: out, lines };
}

function list(v) {
  return (v || []).flatMap((s) => s.split(',')).map((s) => s.trim()).filter(Boolean);
}
function one(n, k) {
  return (n.fields[k] || [])[0] || '';
}

function sources(n) {
  return (n.fields['근거'] || []).map((s) => {
    const m = s.match(/^(\S+)\s+"(.*)"\s*$/);
    if (!m) return { raw: s, status: 'bad-format' };
    return { path: m[1], quote: m[2], status: verifyQuote(m[1], m[2]) };
  });
}

const docNodes = [];
let classification = [];
for (const rel of cfg.docs) {
  const { nodes: ns, lines } = parseDoc(rel);
  docNodes.push(...ns);
  if (rel.endsWith('기능.md')) {
    let on = false;
    lines.forEach((ln, i) => {
      if (/^##\s+선 밖 코드 분류/.test(ln)) on = true;
      else if (/^##\s/.test(ln)) on = false;
      const m = on && ln.match(/^\|\s*`([^`]+)`\s*\|\s*([^|]+?)\s*\|\s*([^|]*?)\s*\|/);
      if (m) classification.push({ method: m[1], kind: m[2], why: m[3], doc: rel, line: i + 1 });
    });
  }
}

const TYPE_BY_PREFIX = [
  [/^core$/, '제품코어'],
  [/^pol\./, '정책'],
  [/^cand\./, '후보'],
  [/^F\./, '기능'],
  [/^U\./, '미결'],
];
for (const d of docNodes) {
  const t = (TYPE_BY_PREFIX.find(([re]) => re.test(d.id)) || [])[1];
  if (!t) {
    autoU('없는 ID', `doc:${d.id}`, `문서 항목 ID ${d.id}의 접두를 모른다 (${d.doc}:${d.line})`, 'ID 접두를 core/pol./cand./F./U. 중 하나로 고친다');
    continue;
  }
  const src = sources(d);
  const n = { id: d.id, type: t, title: d.title, doc: `${d.doc}:${d.line}`, sources: src };
  if (t === '제품코어') Object.assign(n, { class: '결정', grade: one(d, '등급') });
  if (t === '정책')
    Object.assign(n, {
      class: '결정',
      rule: one(d, '규칙'),
      grade: one(d, '등급'),
      firmness: one(d, '확정도'),
      kind: one(d, '종류'),
      date: one(d, '날짜'),
      note: one(d, '메모') || undefined,
    });
  if (t === '후보')
    Object.assign(n, { class: '해석', content: one(d, '내용'), grade: one(d, '등급'), date: one(d, '날짜'), note: one(d, '메모') || undefined });
  if (t === '기능')
    Object.assign(n, {
      class: '해석',
      status: one(d, '상태'),
      kind: one(d, '종류'),
      summary: one(d, '한 줄'),
      observe: one(d, '관측 지점') || undefined,
      _policies: list(d.fields['정책']),
      _anchors: list(d.fields['코드 앵커']),
      _sites: d.fields['게임 지점'] || [],
      verifiedLine: one(d, '확인한 선') || undefined,
    });
  if (t === '미결')
    Object.assign(n, {
      class: one(d, '종류') === '가설' ? '가설' : '해석',
      kind: one(d, '종류'),
      closeBy: one(d, '닫는 법'),
      origin: '기록',
      _related: list(d.fields['관련']),
    });
  addNode(n);
  src.forEach((s, i) => {
    if (s.status !== 'ok')
      autoU('인용 깨짐', `${d.id}:${i + 1}`, `${d.id}의 근거 ${i + 1}을 원문에서 못 찾음 (${s.status}): ${s.path ?? s.raw}`, '원문이 바뀌었으면 근거를 고치고, 원문에 없는 내용이면 항목을 다시 본다', [d.id]);
  });
}

// ───────────────────────────────────────── 2. C# 읽기(주석·문자열 가리기)
function lex(src) {
  const clean = src.split('');
  const noCom = src.split('');
  const strings = [];
  let i = 0;
  const blank = (arr, a, b) => {
    for (let k = a; k < b; k++) if (arr[k] !== '\n') arr[k] = ' ';
  };
  function code(stopBrace) {
    let depth = 0;
    while (i < src.length) {
      const c = src[i], n = src[i + 1];
      if (c === '/' && n === '/') {
        const s = i;
        while (i < src.length && src[i] !== '\n') i++;
        blank(clean, s, i); blank(noCom, s, i);
        continue;
      }
      if (c === '/' && n === '*') {
        const s = i;
        i += 2;
        while (i < src.length && !(src[i] === '*' && src[i + 1] === '/')) i++;
        i += 2;
        blank(clean, s, i); blank(noCom, s, i);
        continue;
      }
      if (c === '"' || ((c === '@' || c === '$') && (n === '"' || ((n === '@' || n === '$') && src[i + 2] === '"')))) {
        str();
        continue;
      }
      if (c === "'") {
        const s = i;
        i++;
        if (src[i] === '\\') i += 2; else i++;
        while (i < src.length && src[i] !== "'") i++;
        i++;
        blank(clean, s + 1, i - 1);
        continue;
      }
      if (stopBrace) {
        if (c === '{') depth++;
        else if (c === '}') {
          if (depth === 0) return;
          depth--;
        }
      }
      i++;
    }
  }
  function str() {
    const s = i;
    let interp = false, verb = false;
    while (src[i] !== '"') {
      if (src[i] === '$') interp = true;
      if (src[i] === '@') verb = true;
      i++;
    }
    i++;
    const contentStart = i;
    const segs = [];
    let segStart = i;
    while (i < src.length) {
      const c = src[i];
      if (!verb && c === '\\') { i += 2; continue; }
      if (verb && c === '"' && src[i + 1] === '"') { i += 2; continue; }
      if (c === '"') break;
      if (interp && c === '{') {
        if (src[i + 1] === '{') { i += 2; continue; }
        segs.push([segStart, i]);
        i++;
        code(true);
        i++;
        segStart = i;
        continue;
      }
      if (interp && c === '}' && src[i + 1] === '}') { i += 2; continue; }
      i++;
    }
    segs.push([segStart, i]);
    for (const [a, b] of segs) blank(clean, a, b);
    strings.push({ start: s, end: i + 1, raw: src.slice(contentStart, i), interp });
    i++;
  }
  code(false);
  return { clean: clean.join(''), noCom: noCom.join(''), strings };
}

function lineIndex(text) {
  const starts = [0];
  for (let i = 0; i < text.length; i++) if (text[i] === '\n') starts.push(i + 1);
  return (pos) => {
    let lo = 0, hi = starts.length - 1;
    while (lo < hi) {
      const mid = (lo + hi + 1) >> 1;
      if (starts[mid] <= pos) lo = mid; else hi = mid - 1;
    }
    return lo + 1;
  };
}

const KW = new Set('if for foreach while switch catch using lock fixed return new nameof typeof sizeof default checked unchecked get set add remove base this throw await in is as out ref var when'.split(' '));
const normHeader = (h) => h.replace(/\[[^\]]*\]/g, ' ').replace(/\s+/g, ' ').trim();

function methodHeader(h, typeName) {
  const m = h.match(/^(.*?)\b([A-Za-z_]\w*)\s*(<[^<>]*>)?\s*\((.*)\)\s*(?:where\s.*)?$/s);
  if (!m) return null;
  const pre = m[1].trim(), name = m[2];
  if (KW.has(name) || /=/.test(pre)) return null;
  if (!pre && name !== typeName) return null;
  return { name, pre, params: m[4] };
}
const splitBases = (s) => s.split(',').map((b) => b.replace(/<.*$/, '').trim()).filter(Boolean);

function structure(clean) {
  const types = [], methods = [], typeLevelBlocks = [];
  const stack = [];
  let headerStart = 0;
  for (let i = 0; i < clean.length; i++) {
    const c = clean[i];
    if (c === '{') {
      const raw = clean.slice(headerStart, i);
      const h = normHeader(raw);
      const top = stack[stack.length - 1];
      const atTypeLevel = !top || top.kind === 'ns' || top.kind === 'type';
      let frame = { kind: 'block', start: i };
      if (atTypeLevel) {
        let m;
        if ((m = h.match(/\b(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*(?::\s*(.*?))?\s*(?:where\s.*)?$/))) {
          const outer = top && top.kind === 'type' ? top.qname : null;
          frame = { kind: 'type', tkind: m[1], name: m[2], qname: outer ? `${outer}.${m[2]}` : m[2], outer, bases: m[3] ? splitBases(m[3]) : [], start: i, nameAt: headerStart + raw.search(new RegExp(`\\b${m[2]}\\b`)), fields: [] };
          types.push(frame);
        } else if (/\bnamespace\b/.test(h)) frame = { kind: 'ns', start: i };
        else if (top && top.kind === 'type' && top.tkind !== 'enum') {
          const mm = methodHeader(h, top.name);
          if (mm) frame = { kind: 'method', type: top.qname, ...mm, headerStart, nameAt: headerStart + raw.search(new RegExp(`\\b${mm.name}\\s*(<[^<>]*>)?\\s*\\(`)), start: i };
          else if (h) typeLevelBlocks.push({ type: top.qname, header: h, at: i });
        }
      }
      stack.push(frame);
      headerStart = i + 1;
    } else if (c === '}') {
      const f = stack.pop();
      if (f) {
        f.end = i;
        if (f.kind === 'method') methods.push(f);
      }
      headerStart = i + 1;
    } else if (c === ';') {
      const top = stack[stack.length - 1];
      if (top && top.kind === 'type') {
        const raw = clean.slice(headerStart, i);
        const h = normHeader(raw);
        const arrow = h.indexOf('=>');
        const mm = arrow >= 0 ? methodHeader(h.slice(0, arrow).trim(), top.name) : null;
        if (mm) methods.push({ kind: 'method', type: top.qname, ...mm, headerStart, nameAt: headerStart + raw.search(new RegExp(`\\b${mm.name}\\b`)), start: headerStart, end: i, expr: true });
        else if (h) top.fields.push({ text: h, at: headerStart });
      }
      headerStart = i + 1;
    }
  }
  return { types, methods, typeLevelBlocks };
}

// ───────────────────────────────────────── 3. 디컴파일 색인(게임 형식)
const DECOMP = path.resolve(cfg.decompRoot);
function walk(dir, out = []) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p, out);
    else if (e.name.endsWith('.cs')) out.push(p);
  }
  return out;
}
const decompFiles = walk(DECOMP).map((p) => path.relative(DECOMP, p).split(path.sep).join('/')).sort(cmp);
const decompLines = new Map();
const gTypes = []; // {name,qname,ns,kind,file,line,indent,end,bases,partial,outer}
const TYPE_DECL = /^(\t*)(?:\[[^\]]*\]\s*)*((?:(?:public|internal|private|protected|static|sealed|abstract|partial|readonly|unsafe|new|ref|file)\s+)*)(class|struct|interface|enum|record(?:\s+struct)?)\s+([A-Za-z_]\w*)(?:<[^>]*>)?\s*(?::\s*(.*?))?\s*(?:where\s.*)?$/;
for (const rel of decompFiles) {
  const lines = readText(path.join(DECOMP, rel)).split('\n');
  decompLines.set(rel, lines);
  let ns = '', blockNs = false;
  const open = [];
  for (let i = 0; i < lines.length; i++) {
    const ln = lines[i];
    let m;
    if ((m = ln.match(/^namespace\s+([\w.]+)\s*;\s*$/))) { ns = m[1]; continue; }
    if ((m = ln.match(/^namespace\s+([\w.]+)\s*$/))) { ns = m[1]; blockNs = true; continue; }
    if (blockNs && /^\}\s*$/.test(ln)) { ns = ''; blockNs = false; continue; }
    if ((m = ln.match(TYPE_DECL))) {
      const indent = m[1].length;
      while (open.length && open[open.length - 1].indent >= indent) open.pop();
      const outer = open[open.length - 1] || null;
      let end = lines.length;
      const close = '\t'.repeat(indent) + '}';
      for (let k = i + 1; k < lines.length; k++) if (lines[k].replace(/\s+$/, '') === close) { end = k + 1; break; }
      const t = { name: m[4], qname: outer ? `${outer.qname}.${m[4]}` : m[4], ns, kind: m[3].replace(/\s+/g, ' '), file: rel, line: i + 1, indent, end, bases: m[5] ? splitBases(m[5]) : [], partial: /\bpartial\b/.test(m[2]), outer: outer ? outer.qname : null };
      gTypes.push(t);
      open.push(t);
    }
  }
}
const gByName = new Map();
for (const t of gTypes) {
  if (!gByName.has(t.name)) gByName.set(t.name, []);
  gByName.get(t.name).push(t);
}

const MEMBER_MODS = '(?:(?:public|private|internal|protected|static|readonly|const|virtual|override|abstract|sealed|new|extern|unsafe|volatile|event|ref|partial|async|required)\\s+)*';
function findMember(t, member, depth = 0) {
  const lines = decompLines.get(t.file);
  const ind = '\t'.repeat(t.indent + 1);
  const reDecl = new RegExp(`^${ind}(?:\\[.*\\]\\s*)*${MEMBER_MODS}([\\w.<>\\[\\],? ]+?)\\s+${member}\\s*(\\(|=>|=|;|\\{|<|$)`);
  const reType = new RegExp(`^${ind}(?:\\[.*\\]\\s*)*${MEMBER_MODS}(?:class|struct|enum|interface|record)\\s+${member}\\b`);
  const reEnum = new RegExp(`^${ind}${member}\\s*(=|,|$)`);
  for (let k = t.line; k < t.end - 1; k++) {
    const ln = lines[k];
    if (!ln.startsWith(ind) || ln[ind.length] === '\t') continue;
    if (reType.test(ln)) return { owner: t, line: k + 1, declType: `${t.qname}.${member}`, nested: true };
    if (t.kind === 'enum' && reEnum.test(ln)) return { owner: t, line: k + 1, declType: null };
    const m = ln.match(reDecl);
    if (m && !/^(return|new|throw|case|else)$/.test(m[1].trim())) {
      const declType = m[1].trim().split(/\s+/).pop().replace(/<.*$/, '').replace(/\[\]$/, '').replace(/\?$/, '');
      return { owner: t, line: k + 1, declType, isMethod: m[2] === '(' || m[2] === '<' };
    }
  }
  if (depth < 4) {
    for (const b of t.bases) {
      const bt = pickAny(b);
      if (bt) {
        const r = findMember(bt, member, depth + 1);
        if (r) return r;
      }
    }
  }
  return null;
}
function pickAny(name) {
  const short = name.split('.').pop();
  const c = (gByName.get(short) || []).filter((t) => !t.outer || name.includes('.'));
  if (c.length === 1) return c[0];
  if (name.includes('.')) return gTypes.find((t) => t.qname === name || t.qname.endsWith(`.${name}`)) || null;
  return c.length ? c.slice().sort((a, b) => cmp(a.file, b.file))[0] : null;
}

// ───────────────────────────────────────── 4. 모드 코드
const CODE_ROOT = path.resolve(cfg.codeRoot);
const sourceStates = [];
// codeDirs 아래 .cs가 codeFiles에 빠졌으면 알린다(하위 폴더 포함). 새 파일을 config에 넣는 것을 잊지 않게.
const listedCode = new Set(cfg.codeFiles.map((f) => f.path));
for (const d of cfg.codeDirs || []) {
  const absDir = path.join(path.resolve(cfg.codeRoot), d);
  if (!fs.existsSync(absDir)) { autoU("원본 없음", d, `codeDirs 폴더가 없다: ${d}`, "config.json의 codeDirs를 고친다"); continue; }
  for (const p of walk(absDir).sort(cmp)) {
    const rel = path.relative(path.resolve(cfg.codeRoot), p).split(path.sep).join("/");
    if (!listedCode.has(rel)) autoU("원본 빠짐", rel, `코드 폴더의 파일이 codeFiles에 없다: ${rel}`, "config.json의 codeFiles에 넣는다");
  }
}
const modTypes = new Map(); // qname → info
const modMethods = new Map(); // id → info
const allFiles = [];

for (const f of cfg.codeFiles) {
  const abs = path.join(CODE_ROOT, f.path);
  if (!fs.existsSync(abs)) {
    autoU('원본 없음', f.path, `코드 파일이 없다: ${f.path}`, 'config.json의 codeFiles를 고친다');
    continue;
  }
  const src = readText(abs);
  const state = fileState(abs);
  sourceStates.push({ role: '코드', path: abs.split(path.sep).join('/'), ...state });
  const { clean, noCom, strings } = lex(src);
  const lineOf = lineIndex(src);
  const usings = [...src.matchAll(/^using\s+([\w.]+)\s*;/gm)].map((m) => m[1]);
  const st = structure(clean);
  allFiles.push({ rel: f.path, container: f.container, src, clean, noCom, strings, lineOf, usings, ...st, state });
}

// 모드가 선언한 이름(게임 이름과 헷갈리지 않게 뺀다)
const modNames = new Set();
for (const F of allFiles) {
  for (const t of F.types) {
    modNames.add(t.name);
    if (t.tkind === 'enum') {
      const body = F.clean.slice(t.start + 1, t.end);
      for (const m of body.matchAll(/\b([A-Za-z_]\w*)\b/g)) modNames.add(m[1]);
    }
    for (const fd of t.fields) {
      const m = fd.text.match(/([A-Za-z_]\w*)\s*(=|$)/);
      if (m) modNames.add(m[1]);
    }
  }
  for (const m of F.methods) modNames.add(m.name);
}

function visibleGame(name, usings) {
  const c = (gByName.get(name) || []).filter((t) => !t.outer && (t.ns === '' || usings.includes(t.ns)));
  if (!c.length) return { status: 'none' };
  const keys = [...new Set(c.map((t) => `${t.ns}|${t.qname}`))];
  if (keys.length === 1) return { status: 'ok', type: c.slice().sort((a, b) => cmp(a.file, b.file) || a.line - b.line)[0], all: c };
  return { status: 'ambiguous', all: c };
}
const gId = (t) => `game:${t.qname}`;
function gameTypeNode(t) {
  return addNode({ id: gId(t), type: '게임', kind: t.kind, title: t.qname, class: '관측', ns: t.ns || '(전역)', loc: `${t.file}:${t.line}` });
}

const ambiguousNames = new Map();
const externalNames = new Map(); // name → Set(method ids)
const memberUnresolved = new Map(); // "Type.member" → Set(method ids)

for (const F of allFiles) {
  const fileId = `file:${F.rel}`;
  addNode({ id: fileId, type: '코드', kind: '파일', title: F.rel, class: '관측', container: F.container, loc: `${F.rel}:1`, sha256: F.state.sha256, lines: F.state.lines });
  for (const t of F.types) {
    const id = `code:${t.qname}`;
    modTypes.set(t.qname, { ...t, file: F });
    addNode({ id, type: '코드', kind: t.tkind, title: t.qname, class: '관측', container: F.container, loc: `${F.rel}:${F.lineOf(t.nameAt)}` });
    addEdge(id, '속함', t.outer ? `code:${t.outer}` : fileId, '관측');
  }
  // 같은 이름 함수가 둘 이상이면 매개변수 수로 나눈다.
  const nameCount = {};
  for (const m of F.methods) nameCount[`${m.type}.${m.name}`] = (nameCount[`${m.type}.${m.name}`] || 0) + 1;
  for (const m of F.methods) {
    const base = `${m.type}.${m.name}`;
    const pc = m.params.trim() ? m.params.split(',').length : 0;
    const key = nameCount[base] > 1 ? `${base}/${pc}` : base;
    const info = { ...m, key, id: `code:${key}`, file: F, lineStart: F.lineOf(m.nameAt), lineEnd: F.lineOf(m.end) };
    modMethods.set(key, info);
  }
}
const methodsByTypeName = new Map();
for (const m of modMethods.values()) {
  const k = `${m.type}|${m.name}`;
  if (!methodsByTypeName.has(k)) methodsByTypeName.set(k, []);
  methodsByTypeName.get(k).push(m);
}
const simpleTypeToQ = new Map();
for (const q of modTypes.keys()) simpleTypeToQ.set(q.split('.').pop(), q);

function methodAt(F, pos) {
  let best = null;
  for (const m of modMethods.values()) if (m.file === F && m.start <= pos && pos <= m.end && (!best || m.start > best.start)) best = m;
  return best;
}

// 게임 인터페이스 구현 → 게임이 부르는 함수
const entryOf = new Map(); // method key → 이유
function interfaceMembers(t) {
  const lines = decompLines.get(t.file);
  const names = new Set();
  for (let k = t.line; k < t.end - 1; k++) {
    const m = lines[k].match(/^\t+(?:[\w<>\[\],.? ]+\s+)?([A-Za-z_]\w*)\s*\(/);
    if (m) names.add(m[1]);
  }
  return names;
}
for (const t of modTypes.values()) {
  for (const b of t.bases) {
    const r = visibleGame(b, t.file.usings);
    if (r.status !== 'ok') continue;
    const gt = r.type;
    gameTypeNode(gt);
    addEdge(`code:${t.qname}`, '구현', gId(gt), '관측');
    if (gt.kind === 'interface') {
      bump('게임 인터페이스 구현 함수(게임이 부름)', 0);
      const names = interfaceMembers(gt);
      for (const m of modMethods.values())
        if (m.type === t.qname && names.has(m.name)) {
          entryOf.set(m.key, `게임이 부름: ${gt.qname} 구현`);
          bump('게임 인터페이스 구현 함수(게임이 부름)');
        }
    }
  }
}

// 함수마다: 호출, 게임 이름, 로그 글
const LOG_CALL = /(?:\bLog|\bLogError|Debug\.Log|Debug\.LogError)\s*\(\s*$/;
for (const m of modMethods.values()) {
  const F = m.file;
  const body = F.clean.slice(m.headerStart, m.end + 1);
  const off = m.headerStart;
  const props = {
    id: m.id, type: '코드', kind: '함수', title: m.key, class: '관측', container: F.container,
    loc: `${F.rel}:${m.lineStart}-${m.lineEnd}`, signature: normHeader(`${m.pre} ${m.name}(${m.params})`),
  };
  if (entryOf.has(m.key)) props.entry = entryOf.get(m.key);
  addNode(props);
  addEdge(m.id, '속함', `code:${m.type}`, '관측');

  // 람다는 감싼 함수의 일로 본다(근사)
  const lambdas = (body.match(/=>/g) || []).length - (m.expr ? 1 : 0);
  if (lambdas > 0) bump('람다·대리자(감싼 함수에 붙임, 근사)', lambdas);

  // 호출
  const seen = new Set();
  for (const c of body.matchAll(/\b([A-Za-z_]\w*)\s*(?:<[^<>()]*>)?\s*\(/g)) {
    const name = c[1];
    if (KW.has(name)) continue;
    const at = c.index;
    if (off + at === m.nameAt) continue; // 자기 선언
    let j = at - 1;
    while (j >= 0 && /\s/.test(body[j])) j--;
    let qual = null, isNew = false;
    if (body[j] === '.') {
      let k = j - 1;
      while (k >= 0 && /\s/.test(body[k])) k--;
      const e = k + 1;
      while (k >= 0 && /\w/.test(body[k])) k--;
      qual = body.slice(k + 1, e);
    } else if (body.slice(Math.max(0, j - 2), j + 1) === 'new') isNew = true;
    if (isNew) {
      const q = simpleTypeToQ.get(name);
      if (q) {
        const key = `new:${q}`;
        if (!seen.has(key)) { seen.add(key); addEdge(m.id, '만듦', `code:${q}`, '관측'); bump('new 모드 형식(형식만 잇고 따라가지 않음)'); }
      }
      continue;
    }
    let targets = [];
    if (qual) {
      const q = simpleTypeToQ.get(qual);
      if (q) targets = methodsByTypeName.get(`${q}|${name}`) || [];
    } else {
      let ty = m.type;
      while (ty && !targets.length) {
        targets = methodsByTypeName.get(`${ty}|${name}`) || [];
        ty = modTypes.get(ty)?.outer || null;
      }
    }
    for (const t of targets) {
      const key = `call:${t.key}`;
      if (seen.has(key)) continue;
      seen.add(key);
      addEdge(m.id, '부름', t.id, '관측', { line: F.lineOf(off + at) });
      bump(qual ? '직접 호출(형식 이름 붙임)' : '직접 호출(같은 형식)');
    }
  }

  // 게임 형식 참조
  const gseen = new Set();
  // 서식 지정자({x:F1})와 초기화 대입 대상(Name = …)은 형식 이름이 아니다.
  for (const c of body.matchAll(/(?<![.\w])([A-Z]\w*)\b(?!\s*=[^=>])/g)) {
    const name = c[1];
    if (name.length < 2 || modNames.has(name) || /^[CDEFGNPRX]\d*$/.test(name)) continue;
    const r = visibleGame(name, F.usings);
    if (r.status === 'ok') {
      const id = gId(r.type);
      if (gseen.has(id)) continue;
      gseen.add(id);
      gameTypeNode(r.type);
      addEdge(m.id, '참조', id, '관측', { line: F.lineOf(off + c.index) });
    } else if (r.status === 'ambiguous') {
      if (!ambiguousNames.has(name)) ambiguousNames.set(name, { cands: r.all, users: new Set() });
      ambiguousNames.get(name).users.add(m.key);
    } else {
      if (!externalNames.has(name)) externalNames.set(name, new Set());
      externalNames.get(name).add(m.key);
    }
  }

  // 게임 정적 멤버 사슬: Type.member.member…
  const mseen = new Set();
  for (const c of body.matchAll(/(?<![.\w])([A-Z]\w*)((?:\s*\.\s*[A-Za-z_]\w*)+)/g)) {
    const head = c[1];
    if (modNames.has(head)) continue;
    const r = visibleGame(head, F.usings);
    if (r.status !== 'ok') continue;
    let cur = r.type;
    const segs = c[2].split('.').map((s) => s.trim()).filter(Boolean);
    for (const seg of segs) {
      const found = findMember(cur, seg);
      if (!found) {
        const k = `${cur.qname}.${seg}`;
        if (!memberUnresolved.has(k)) memberUnresolved.set(k, new Set());
        memberUnresolved.get(k).add(m.key);
        break;
      }
      const owner = found.owner;
      const id = `game:${owner.qname}.${seg}`;
      addNode({ id, type: '게임', kind: found.nested ? '형식' : found.isMethod ? '함수' : '멤버', title: `${owner.qname}.${seg}`, class: '관측', loc: `${owner.file}:${found.line}` });
      gameTypeNode(owner);
      if (!nodes.get(id)._linked) { addEdge(id, '속함', gId(owner), '관측'); nodes.get(id)._linked = true; }
      if (!mseen.has(id)) { mseen.add(id); addEdge(m.id, '사용', id, '관측', { line: F.lineOf(off + c.index) }); }
      if (!found.declType) break;
      const next = found.nested ? gTypes.find((t) => t.qname === found.declType) : visibleGame(found.declType, F.usings).type || pickAny(found.declType);
      if (!next) break;
      cur = next;
    }
  }

  // 로그 글(Player.log 에서 보이는 관측 지점)
  const logs = [];
  for (const s of F.strings) {
    if (s.start < m.start || s.start > m.end) continue;
    const before = F.noCom.slice(Math.max(0, s.start - 40), s.start);
    if (!LOG_CALL.test(before)) continue;
    let t = s.interp ? s.raw.split('{')[0] : s.raw;
    t = t.trim();
    if (t.length > 70) t = t.slice(0, 70) + '…';
    if (t && !logs.includes(t)) logs.push(t);
  }
  if (logs.length) nodes.get(m.id).logs = logs;
}

// 형식 수준 필드의 게임 형식
for (const t of modTypes.values()) {
  const s = new Set();
  for (const fd of t.fields)
    for (const c of fd.text.matchAll(/(?<![.\w])([A-Z]\w*)\b/g)) {
      if (c[1].length < 2 || modNames.has(c[1])) continue;
      const r = visibleGame(c[1], t.file.usings);
      if (r.status === 'ok' && !s.has(r.type.qname)) {
        s.add(r.type.qname);
        gameTypeNode(r.type);
        addEdge(`code:${t.qname}`, '참조', gId(r.type), '관측', { via: '필드' });
      }
    }
}
for (const F of allFiles) for (const b of F.typeLevelBlocks) bump('형식 수준 블록(속성 본문 등, 분석 안 함)');

// Harmony 패치와 리플렉션 필드
const hookTargets = [];
for (const F of allFiles) {
  const text = F.noCom;
  const AM = /AccessTools\.Method\s*\(\s*typeof\s*\(\s*(\w+)\s*\)\s*,\s*(?:"(\w+)"|nameof\s*\(\s*(?:\w+\.)*(\w+)\s*\))\s*\)/g;
  const found = [...text.matchAll(AM)].map((x) => ({ at: x.index, type: x[1], member: x[2] || x[3] }));
  const stmtOf = (pos) => [text.lastIndexOf(';', pos), text.indexOf(';', pos)];
  const groups = new Map();
  for (const f of found) {
    const k = stmtOf(f.at).join(':');
    if (!groups.has(k)) groups.set(k, []);
    groups.get(k).push(f);
  }
  for (const g of groups.values()) {
    const target = g.find((f) => !simpleTypeToQ.has(f.type));
    const handlers = g.filter((f) => simpleTypeToQ.has(f.type));
    const roleAt = (at) => (/postfix\s*:\s*$/.test(text.slice(Math.max(0, at - 30), at)) ? 'postfix' : /prefix\s*:\s*$/.test(text.slice(Math.max(0, at - 30), at)) ? 'prefix' : '역할 모름');
    if (!target && handlers.length) {
      // 패치 대상을 실행 중에 계산한다(예: 리플렉션으로 찾은 MethodInfo 변수). 그래프는 대상을 모른다.
      const [a, b] = stmtOf(handlers[0].at);
      const stmt = text.slice(a + 1, b).replace(/\s+/g, ' ').trim();
      const pm = stmt.match(/(?:TryPatch|\.Patch)\s*\(\s*([^,]+),/);
      if (!pm) continue;
      const reg = methodAt(F, handlers[0].at);
      for (const h of handlers) {
        const hm = (methodsByTypeName.get(`${simpleTypeToQ.get(h.type)}|${h.member}`) || [])[0];
        if (!hm) continue;
        const role = roleAt(h.at);
        if (reg) addEdge(reg.id, '패치 등록', hm.id, '관측', { line: F.lineOf(h.at) });
        entryOf.set(hm.key, `게임이 부름: Harmony ${role}, 대상은 실행 중 계산(${pm[1].trim()}) — 그래프가 대상을 모름`);
        nodes.get(hm.id).entry = entryOf.get(hm.key);
        bump('Harmony 패치(대상을 실행 중 계산, 대상 모름)');
      }
      autoU('처리 안 한 꼴', `패치대상:${F.rel}:${F.lineOf(handlers[0].at)}`, `${reg ? reg.key : F.rel}의 Harmony 패치 대상 "${pm[1].trim()}"은 실행 중에 계산된다. 그래프는 어느 게임 함수에 걸리는지 모른다`, '대상을 찾는 코드를 읽어 해당 기능에 "게임 지점"으로 적는다. 같은 꼴이 늘면 build.mjs에 규칙을 더한다', reg ? [reg.id] : []);
      continue;
    }
    if (!target || !handlers.length) continue;
    const reg = methodAt(F, target.at);
    const r = visibleGame(target.type, F.usings);
    if (r.status !== 'ok') continue;
    const gt = r.type;
    const fm = findMember(gt, target.member);
    const tid = `game:${(fm ? fm.owner : gt).qname}.${target.member}`;
    addNode({ id: tid, type: '게임', kind: '함수', title: `${(fm ? fm.owner : gt).qname}.${target.member}`, class: '관측', loc: fm ? `${fm.owner.file}:${fm.line}` : `${gt.file}:${gt.line}` });
    gameTypeNode(gt);
    addEdge(tid, '속함', gId(fm ? fm.owner : gt), '관측');
    for (const h of handlers) {
      const role = /postfix\s*:\s*$/.test(text.slice(Math.max(0, h.at - 30), h.at)) ? 'postfix' : /prefix\s*:\s*$/.test(text.slice(Math.max(0, h.at - 30), h.at)) ? 'prefix' : '역할 모름';
      const hm = (methodsByTypeName.get(`${simpleTypeToQ.get(h.type)}|${h.member}`) || [])[0];
      if (!hm) continue;
      addEdge(hm.id, `패치(${role})`, tid, '관측', { line: F.lineOf(h.at) });
      if (reg) addEdge(reg.id, '패치 등록', hm.id, '관측', { line: F.lineOf(h.at) });
      entryOf.set(hm.key, `게임이 부름: ${(fm ? fm.owner : gt).qname}.${target.member} 의 Harmony ${role}`);
      nodes.get(hm.id).entry = entryOf.get(hm.key);
      bump('Harmony 패치 처리 함수(게임 함수가 부름)');
    }
    hookTargets.push({ id: tid, member: target.member, decl: fm ? `${fm.owner.file}:${fm.line}` : null });
  }
  for (const x of text.matchAll(/AccessTools\.Field\s*\(\s*typeof\s*\(\s*(\w+)\s*\)\s*,\s*"(\w+)"\s*\)/g)) {
    const r = visibleGame(x[1], F.usings);
    if (r.status !== 'ok') continue;
    const fm = findMember(r.type, x[2]);
    const id = `game:${r.type.qname}.${x[2]}`;
    addNode({ id, type: '게임', kind: '멤버', title: `${r.type.qname}.${x[2]}`, class: '관측', loc: fm ? `${fm.owner.file}:${fm.line}` : `${r.type.file}:${r.type.line}`, private: true });
    gameTypeNode(r.type);
    addEdge(id, '속함', gId(r.type), '관측');
    const reg = methodAt(F, x.index);
    if (reg) addEdge(reg.id, '리플렉션 필드', id, '관측', { line: F.lineOf(x.index) });
    bump('리플렉션 필드(AccessTools.Field)');
  }
  if (/\[\s*HarmonyPatch/.test(text)) bump('Harmony 속성 패치(처리 안 함)');
  for (const x of text.matchAll(/FindType\s*\(\s*"([\w.]+)"\s*\)/g)) {
    const reg = methodAt(F, x.index);
    bump('리플렉션 문자열 이름(FindType, 색인 밖)');
    const k = `${x[1]} (리플렉션)`;
    if (!externalNames.has(k)) externalNames.set(k, new Set());
    if (reg) externalNames.get(k).add(reg.key);
  }
}

// 패치 대상 게임 함수를 게임 안에서 누가 부르나
for (const h of hookTargets) {
  const callers = [];
  const re = new RegExp(`\\b${h.member}\\s*\\(`);
  const reDecl = new RegExp(`^\\s*(?:\\[.*\\]\\s*)*(?:public|private|internal|protected)[^=(]*\\b${h.member}\\s*\\(`);
  for (const rel of decompFiles) {
    const lines = decompLines.get(rel);
    for (let i = 0; i < lines.length; i++) if (re.test(lines[i]) && !reDecl.test(lines[i])) callers.push(`${rel}:${i + 1}`);
  }
  nodes.get(h.id).gameCallers = callers;
}

// ───────────────────────────────────────── 5. 기능 선(앵커에서 '부름'을 따라 닿는 곳)
const callOut = new Map();
for (const e of edges) if (e.type === '부름') {
  if (!callOut.has(e.from)) callOut.set(e.from, []);
  callOut.get(e.from).push(e.to);
}
const reachedBy = new Map(); // code id → Set(feature)
function enclosingGame(rel, line) {
  const t = gTypes.filter((x) => x.file === rel && x.line <= line && line <= x.end).sort((a, b) => b.indent - a.indent)[0];
  if (!t) return null;
  const lines = decompLines.get(rel);
  const ind = lines[line - 1].match(/^\t*/)[0].length;
  for (let k = line - 1; k >= t.line; k--) {
    const m = lines[k].match(/^(\t+)(?:(?:public|private|internal|protected|static|override|virtual|unsafe|async)\s+)+[^;=]*?\b([A-Za-z_]\w*)\s*\(/);
    if (m && m[1].length < ind) return { type: t, method: m[2] };
  }
  return { type: t, method: null };
}
for (const f of [...nodes.values()].filter((n) => n.type === '기능')) {
  const anchors = [];
  for (const a of f._anchors) {
    const key = a.replace(/^code:/, '');
    if (!modMethods.has(key)) {
      autoU('앵커 없음', `${f.id}:${key}`, `${f.id}의 코드 앵커 ${key}가 코드에 없다`, '함수 이름이 바뀌었으면 기능.md의 앵커를 고친다. 기능이 없어졌으면 상태를 고친다', [f.id]);
      continue;
    }
    anchors.push(`code:${key}`);
    addEdge(f.id, '처리', `code:${key}`, '해석');
  }
  const seen = new Set(anchors);
  const q = [...anchors];
  while (q.length) {
    const x = q.shift();
    for (const y of callOut.get(x) || []) if (!seen.has(y)) { seen.add(y); q.push(y); }
  }
  for (const x of [...seen].sort(cmp)) {
    addEdge(f.id, '지남', x, '투영');
    if (!reachedBy.has(x)) reachedBy.set(x, new Set());
    reachedBy.get(x).add(f.id);
  }
  // 선 서명: 선 위 함수 본문(주석·문자열 뺀 것, 공백 접음)의 해시. 실게임 확인 뒤 선이 바뀌었는지 본다.
  if (seen.size) {
    const h = crypto.createHash('sha256');
    for (const x of [...seen].sort(cmp)) {
      const mm = modMethods.get(x.replace(/^code:/, ''));
      h.update(`${mm.key}\n${mm.file.clean.slice(mm.headerStart, mm.end + 1).replace(/\s+/g, ' ').trim()}\n`);
    }
    f.lineHash = h.digest('hex').slice(0, 12);
    const verifiedStatus = ['실게임 확인', '사용 확인'].includes(f.status);
    if (verifiedStatus && !f.verifiedLine)
      autoU('확인 시점 코드 모름', f.id, `${f.id}는 "${f.status}"인데 어느 코드에서 확인했는지 적혀 있지 않다(지금 선 서명 ${f.lineHash})`, '지금 코드로 다시 시험해 통과하면 기능.md에 "확인한 선: <지금 선 서명>"을 적는다', [f.id]);
    else if (verifiedStatus && f.verifiedLine !== f.lineHash)
      autoU('확인 뒤 코드 바뀜', f.id, `${f.id}의 선이 확인 뒤 바뀌었다(확인 ${f.verifiedLine} → 지금 ${f.lineHash})`, '바뀐 코드로 다시 시험한다. 통과하면 확인한 선을 새 서명으로 고친다', [f.id]);
  }
  // 선이 닿는 게임 지점
  const touch = new Set();
  for (const e of edges) if (seen.has(e.from) && e.to.startsWith('game:') && ['참조', '사용', '리플렉션 필드'].includes(e.type) || (seen.has(e.from) && e.type.startsWith('패치('))) touch.add(e.to);
  for (const g of [...touch].sort(cmp)) addEdge(f.id, '닿음', g, '투영');
  // 기록에 적힌 게임 지점
  for (const s of f._sites) {
    const m = s.match(/^(\S+):(\d+)\s+(\S+)$/);
    if (!m) { autoU('없는 ID', `${f.id}:site:${s}`, `${f.id}의 게임 지점 꼴을 모른다: ${s}`, '`경로:줄 글` 꼴로 고친다', [f.id]); continue; }
    const [, rel, ln, tok] = m;
    const lines = decompLines.get(rel);
    const id = `game:site:${rel}:${ln}`;
    if (!lines || !lines[+ln - 1] || !lines[+ln - 1].includes(tok)) {
      autoU('게임 지점 낡음', `${f.id}:${rel}:${ln}`, `${f.id}의 게임 지점 ${rel}:${ln}에 "${tok}"가 없다`, '디컴파일이 바뀌었으면 줄을 다시 찾아 고친다', [f.id]);
      continue;
    }
    const enc = enclosingGame(rel, +ln);
    addNode({ id, type: '게임', kind: '자리', title: `${tok} @ ${enc ? enc.type.qname + (enc.method ? '.' + enc.method : '') : rel}`, class: '관측', loc: `${rel}:${ln}`, text: lines[+ln - 1].trim().slice(0, 160) });
    if (enc) { gameTypeNode(enc.type); addEdge(id, '속함', gId(enc.type), '관측'); }
    addEdge(f.id, '게임 지점', id, '해석');
  }
}

// ───────────────────────────────────────── 6. 정책·후보·미결 연결과 검사
const known = (id) => nodes.has(id) || nodes.has(`code:${id}`);
for (const f of [...nodes.values()].filter((n) => n.type === '기능')) {
  for (const p of f._policies) {
    if (!nodes.has(p)) { autoU('없는 ID', `${f.id}:${p}`, `${f.id}가 가리키는 ${p}가 없다`, '정책.md에 있는 ID로 고친다', [f.id]); continue; }
    const t = nodes.get(p).type;
    addEdge(p, t === '정책' ? '적용' : '관련', f.id, '해석');
  }
}
for (const u of [...nodes.values()].filter((n) => n.type === '미결' && n.origin === '기록')) {
  for (const r of u._related) {
    if (!known(r)) { autoU('없는 ID', `${u.id}:${r}`, `${u.id}가 가리키는 ${r}가 없다`, '있는 ID로 고친다', [u.id]); continue; }
    addEdge(u.id, '관련', nodes.has(r) ? r : `code:${r}`, '해석');
  }
}

// 선 밖 코드
const classified = new Map(classification.map((c) => [c.method, c]));
for (const c of classification) {
  if (!modMethods.has(c.method)) autoU('없는 ID', `분류:${c.method}`, `선 밖 코드 분류의 ${c.method}가 코드에 없다 (${c.doc}:${c.line})`, '분류표를 고친다');
}
for (const m of [...modMethods.values()].sort((a, b) => cmp(a.key, b.key))) {
  const n = nodes.get(m.id);
  if (reachedBy.has(m.id)) n.features = [...reachedBy.get(m.id)].sort(cmp);
  else if (classified.has(m.key)) n.offline = `${classified.get(m.key).kind}: ${classified.get(m.key).why}`;
  else autoU('선 밖 코드', m.key, `${m.key}는 어떤 기능 선에도 없고 분류도 안 됐다${n.entry ? ` (${n.entry})` : ''}`, '기능.md에서 어느 기능의 앵커로 잇거나, 선 밖 코드 분류표에 적는다', [m.id]);
}

// 정책 대 기능(약속의 층에서 맞대기)
const BUILT = new Set(['실게임 확인', '사용 확인', '구현됨·미확인', '구현 중']);
for (const p of [...nodes.values()].filter((n) => n.type === '정책')) {
  const fs_ = edges.filter((e) => e.from === p.id && e.type === '적용').map((e) => nodes.get(e.to));
  if (p.kind !== '처리') continue;
  if (!fs_.length) autoU('의도만 있음', p.id, `${p.id}: 이 정책을 지는 기능이 없다`, '지는 기능을 기능.md에 잇거나, 아직 없는 기능이면 설계·구현한다', [p.id]);
  else if (!fs_.some((f) => BUILT.has(f.status))) autoU('의도만 있음(구현 전)', p.id, `${p.id}: 지는 기능이 모두 설계만 있다 (${fs_.map((f) => f.id).join(', ')})`, '해당 기능을 구현한다', [p.id, ...fs_.map((f) => f.id)]);
}
for (const f of [...nodes.values()].filter((n) => n.type === '기능')) {
  if (!BUILT.has(f.status)) continue;
  const pols = f._policies.filter((p) => nodes.get(p)?.type === '정책');
  if (!pols.length) autoU('구현만 있음', f.id, `${f.id}: 만들어진 기능인데 근거가 되는 정책이 없다`, '사용자가 이 동작을 정하면 정책으로 적는다. 아니면 기능을 다시 본다', [f.id]);
}
for (const c of [...nodes.values()].filter((n) => n.type === '후보' && n.grade === '추정 결정'))
  autoU('추정 결정 확인', c.id, `${c.id}: 기록상 결정처럼 쓰이지만 사용자 확정 기록이 없다`, '사용자가 확정하면 정책.md 위쪽으로 옮긴다', [c.id]);

for (const [name, v] of [...ambiguousNames].sort((a, b) => cmp(a[0], b[0])))
  autoU('게임 이름 모호', name, `게임 이름 ${name}이 여러 곳에 있다`, '모드 코드의 using을 보고 어느 것인지 정한다', [...v.users].sort(cmp).map((k) => `code:${k}`), v.cands.map((t) => `${t.ns || '(전역)'} ${t.qname} @ ${t.file}:${t.line}`).sort(cmp));
if (externalNames.size)
  autoU('색인 밖 이름', '외부', `모드가 쓰는 이름 ${externalNames.size}개가 디컴파일 색인에 없다(Unity·System·Harmony 등). 이 이름들은 게임 지점으로 잇지 않았다`, '필요하면 해당 어셈블리(예: Unity.Entities, Unity.Transforms)를 디컴파일해 decompRoot에 더하고 다시 만든다', [], [...externalNames].sort((a, b) => cmp(a[0], b[0])).map(([k, s]) => `${k} ← ${[...s].sort(cmp).join(', ')}`));
if (memberUnresolved.size)
  autoU('게임 멤버 못 찾음', '멤버', `게임 형식은 찾았는데 멤버 ${memberUnresolved.size}개의 선언을 못 찾았다(상속·제네릭·확장 함수일 수 있음)`, '해당 멤버를 디컴파일에서 직접 찾아보고, 꼴이 반복되면 build.mjs의 멤버 찾기 규칙을 고친다', [], [...memberUnresolved].sort((a, b) => cmp(a[0], b[0])).map(([k, s]) => `${k} ← ${[...s].sort(cmp).join(', ')}`));
for (const [form, n] of Object.entries(execForms))
  if (n > 0 && /분석 안 함|처리 안 함|근사/.test(form)) autoU('처리 안 한 꼴', form, `${form}: ${n}곳`, '이 꼴이 기능 선을 바꾸는지 보고, 그렇다면 build.mjs에 따라가는 규칙을 더한다');

for (const u of unresolved) {
  addNode({ id: u.id, type: '미결', title: u.title, kind: u.kind, closeBy: u.closeBy, class: '투영', origin: '계산', items: u.items });
  for (const r of u.related) if (nodes.has(r)) addEdge(u.id, '관련', r, '투영');
}

// ───────────────────────────────────────── 7. 쓰기
for (const d of cfg.docs) sourceStates.push({ role: '기획 문서', path: `work/지식그래프/${d}`, ...fileState(path.join(ROOT, d)) });
const outNodes = [...nodes.values()]
  .map((n) => {
    const o = {};
    for (const k of Object.keys(n).sort(cmp)) if (!k.startsWith('_') && n[k] !== undefined) o[k.endsWith('_') ? k.slice(0, -1) : k] = n[k];
    return o;
  })
  .sort((a, b) => cmp(a.id, b.id));
const seenE = new Set();
const outEdges = edges
  .filter((e) => {
    const k = `${e.from}|${e.type}|${e.to}`;
    if (seenE.has(k)) return false;
    seenE.add(k);
    return true;
  })
  .sort((a, b) => cmp(a.from, b.from) || cmp(a.type, b.type) || cmp(a.to, b.to));
const counts = {};
for (const n of outNodes) counts[n.type] = (counts[n.type] || 0) + 1;
const unresolvedByKind = {};
for (const n of outNodes) if (n.type === '미결') unresolvedByKind[n.kind] = (unresolvedByKind[n.kind] || 0) + 1;
const graph = {
  meta: {
    schema: 1,
    note: '원본에서 tools/build.mjs가 만든 색인. 손으로 고치지 않는다. 결정의 원문은 세션 기록이다.',
    sources: sourceStates.sort((a, b) => cmp(a.path, b.path)),
    decomp: { root: DECOMP.split(path.sep).join('/'), files: decompFiles.length, types: gTypes.length },
    executionModel: Object.fromEntries(Object.entries(execForms).sort((a, b) => cmp(a[0], b[0]))),
    counts: Object.fromEntries(Object.entries(counts).sort((a, b) => cmp(a[0], b[0]))),
    unresolvedByKind: Object.fromEntries(Object.entries(unresolvedByKind).sort((a, b) => cmp(a[0], b[0]))),
    edges: outEdges.length,
  },
  nodes: outNodes,
  edges: outEdges,
};
const outPath = path.join(ROOT, cfg.out);
fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, JSON.stringify(graph, null, 1) + '\n');

// 요약
const byType = (t) => outNodes.filter((n) => n.type === t);
const S = [];
S.push('# 그래프 요약 (자동 생성 — 손으로 고치지 않는다)', '');
S.push('`node tools/build.mjs`가 만든다. 원본 상태:', '');
S.push('| 원본 | sha256 앞 12자 | 줄 | 수정 시각(UTC) |', '|---|---|---|---|');
for (const s of graph.meta.sources) S.push(`| ${s.path} | ${s.sha256.slice(0, 12)} | ${s.lines} | ${s.mtime} |`);
S.push('', `디컴파일: ${graph.meta.decomp.files}개 파일, 형식 ${graph.meta.decomp.types}개 색인.`, '');
S.push('## 개수', '', Object.entries(graph.meta.counts).map(([k, v]) => `- ${k}: ${v}`).join('\n'), `- 연결: ${outEdges.length}`, '');
S.push('## 기능', '', '| ID | 상태 | 이름 | 선 위 함수 | 닿는 게임 지점 | 선 서명 | 확인한 선 |', '|---|---|---|---|---|---|---|');
for (const f of byType('기능')) {
  const reach = outEdges.filter((e) => e.from === f.id && e.type === '지남').length;
  const touch = outEdges.filter((e) => e.from === f.id && (e.type === '닿음' || e.type === '게임 지점')).length;
  S.push(`| ${f.id} | ${f.status} | ${f.title} | ${reach} | ${touch} | ${f.lineHash || ''} | ${f.verifiedLine || ''} |`);
}
S.push('', '## 정책', '', '| ID | 등급 | 확정도 | 규칙 | 지는 기능 |', '|---|---|---|---|---|');
for (const p of byType('정책')) S.push(`| ${p.id} | ${p.grade} | ${p.firmness} | ${p.rule} | ${outEdges.filter((e) => e.from === p.id && e.type === '적용').map((e) => e.to).join(', ') || '없음'} |`);
S.push('', '## 후보(정책 아님)', '', '| ID | 등급 | 내용 |', '|---|---|---|');
for (const c of byType('후보')) S.push(`| ${c.id} | ${c.grade} | ${c.content} |`);
S.push('', '## 미결', '', Object.entries(graph.meta.unresolvedByKind).map(([k, v]) => `- ${k}: ${v}`).join('\n'), '');
S.push('| ID | 종류 | 내용 |', '|---|---|---|');
for (const u of byType('미결')) S.push(`| ${u.id} | ${u.kind} | ${u.title} |`);
S.push('', '## 호출 꼴(실행 모델)', '', '| 꼴 | 곳 |', '|---|---|');
for (const [k, v] of Object.entries(graph.meta.executionModel)) S.push(`| ${k} | ${v} |`);
fs.writeFileSync(path.join(ROOT, cfg.summary), S.join('\n') + '\n');

const hard = unresolved.filter((u) => ['인용 깨짐', '앵커 없음', '없는 ID', '원본 없음', '원본 빠짐', '게임 지점 낡음'].includes(u.kind));
console.log(`그래프: 노드 ${outNodes.length}, 연결 ${outEdges.length} → ${cfg.out}`);
console.log(Object.entries(graph.meta.counts).map(([k, v]) => `${k} ${v}`).join(' · '));
console.log(`미결: ${Object.entries(graph.meta.unresolvedByKind).map(([k, v]) => `${k} ${v}`).join(' · ')}`);
for (const s of sourceStates.filter((s) => s.role === '코드')) console.log(`코드 원본: ${s.path} sha256=${s.sha256.slice(0, 12)} 줄=${s.lines} 수정=${s.mtime}`);
if (hard.length) {
  console.log(`주의: 원본을 고쳐야 할 항목 ${hard.length}개`);
  for (const u of hard) console.log(`  - ${u.title}`);
  if (STRICT) process.exit(1);
}
