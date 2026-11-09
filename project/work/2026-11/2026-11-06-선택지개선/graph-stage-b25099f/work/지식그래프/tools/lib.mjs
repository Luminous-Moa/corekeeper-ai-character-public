// 공용: 설정, 파일 읽기, 해시, 인용 확인. build.mjs 와 query.mjs 가 같이 쓴다.
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

export const HERE = path.dirname(fileURLToPath(import.meta.url));
export const ROOT = path.resolve(HERE, '..'); // work/지식그래프
export const cfg = JSON.parse(readText(path.join(HERE, 'config.json')));
export const SESSION = path.resolve(ROOT, cfg.sessionRoot);

export function readText(p) {
  let s = fs.readFileSync(p, 'utf8');
  if (s.charCodeAt(0) === 0xfeff) s = s.slice(1);
  return s.replace(/\r\n?/g, '\n');
}

export function sha256File(p) {
  return crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
}

export function fileState(p) {
  const st = fs.statSync(p);
  const text = readText(p);
  return {
    sha256: sha256File(p),
    bytes: st.size,
    lines: text.split('\n').length,
    mtime: st.mtime.toISOString(),
  };
}

// 근거 경로: 절대 경로(C:/...)면 그대로, 아니면 세션 폴더 기준.
export function resolveSourcePath(p) {
  if (cfg.stagedDocumentRoot && p.startsWith('work/지식그래프/')) return path.join(cfg.stagedDocumentRoot, p.slice('work/지식그래프/'.length));
  if (/^[A-Za-z]:[\\/]/.test(p)) return path.resolve(p);
  return path.resolve(SESSION, p);
}

const norm = (s) => s.replace(/\s+/g, ' ').trim();

// 인용이 원문에 그대로 있는지 본다. 결과: 'ok' | 'missing-file' | 'not-found'
export function verifyQuote(p, quote) {
  const abs = resolveSourcePath(p);
  if (!fs.existsSync(abs)) return 'missing-file';
  return norm(readText(abs)).includes(norm(quote)) ? 'ok' : 'not-found';
}

// 문자열 비교로 정렬한다(로캘에 따라 바뀌지 않게).
export const cmp = (a, b) => (a < b ? -1 : a > b ? 1 : 0);

