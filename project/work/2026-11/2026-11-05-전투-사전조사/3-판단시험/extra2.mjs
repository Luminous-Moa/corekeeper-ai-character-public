// 추가 시험 2(2026-11-05, HEAD 요청). extra.mjs 를 복사해 바꾼 것(원본은 그대로).
// T5: T1·T1m·T2 에 이동·제자리 후보마다 '그 선택의 예상 결과'를 같은 형식으로 붙임(가정 드러냄, 권유 말 없음).
// T6: T3(시간 사실) + T4b(쓰러짐·받은 피해·최근 결과 줄 뺌).
// 원 설명: "모델이 일부러 돌진을 벽에 부딪히게 하는가".
// 같은 바탕 요청(@Claude #773)·모델·요청 형식. 후보는 14 세트만. 기존 파일은 고치지 않고 읽기만(scenarios.mjs 의 바탕·형식 함수).
//   node extra2.mjs --dry     요청만 만들어 requests-extra2/ 에 씀
//   node extra2.mjs           보내기(동시 2, 상한 200, 429 한 번·연속 실패 3번이면 멈춤) → raw/extra2.jsonl, 집계
//   node extra2.mjs --analyze 집계만 → summary-extra2.json / summary-extra2.txt
// 시험마다: 지식 줄 없음/있음 × 후보 순서 원래/뒤바꿈(왼·오른 짝) × {기본, 적 0.1칸 멀리, 후보 순서 섞기} = 12회.
import fs from 'fs';
import os from 'os';
import path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';
import { ASSUME, KNOWLEDGE_LINE, loadBase, buildBody } from './scenarios.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const W = ASSUME.walkTilesPerSec, H = ASSUME.chargeHalfWidth;
const ORIGIN = { x: 2, y: -15 }, ENEMY_ID = 64692;
const r1 = (v) => (Math.round(v * 10) / 10).toFixed(1);
const r2 = (v) => (Math.round(v * 100) / 100).toFixed(2);
const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const compass = (dx, dy) => { const ns = dy > 0.3 ? '북' : dy < -0.3 ? '남' : ''; const ew = dx > 0.3 ? '동' : dx < -0.3 ? '서' : ''; return ns + ew ? ns + ew + '쪽' : '제자리'; };
const box = (f) => (x, y) => Math.abs(x) > 12 || Math.abs(y) > 12 || f(x, y);

// 지도 (내 칸 (0,0), x 동, y 북)
const MAPS = {
  // T1: 북서쪽 덩어리 벽(x<=-2, y>=2). 적(동쪽 1.2칸)이 왼쪽(북) 1칸 자리로 다시 겨누면 그 선은 2칸 뒤 벽에 닿고, 오른쪽(남) 1칸 자리로 겨누면 트임.
  t1: box((x, y) => x <= -2 && y >= 2),
  t1m: box((x, y) => x <= -2 && y <= -2),   // 거울: 벽이 남서쪽(오른쪽 피하기가 벽 쪽)
  // T2: 서쪽 벽이 북쪽 절반에만(x<=-2, y>=0). 뒤·왼쪽 뒤로 가면 바로 뒤가 벽, 오른쪽 계열은 뒤 트임.
  t2: box((x, y) => x <= -2 && y >= 0),
  // S4와 같은 지도(적 뒤가 벽)
  s4: box((x) => x <= -3),
};

function frame(enemy) { const n = Math.hypot(enemy.x, enemy.y); const f = { x: Math.round(enemy.x / n), y: Math.round(enemy.y / n) }; return { f, l: { x: -f.y, y: f.x }, b: { x: -f.x, y: -f.y }, r: { x: f.y, y: -f.x } }; }
function lateral(enemy, p) { const n = Math.hypot(enemy.x, enemy.y); const d = { x: -enemy.x / n, y: -enemy.y / n }; const v = { x: p.x - enemy.x, y: p.y - enemy.y }; return { lat: Math.abs(v.x * d.y - v.y * d.x), along: v.x * d.x + v.y * d.y }; }
function wallBehind(isWall, enemy, p) {
  const vx = p.x - enemy.x, vy = p.y - enemy.y; const n = Math.hypot(vx, vy); const ux = vx / n, uy = vy / n;
  let open = 0, last = `${Math.round(p.x)},${Math.round(p.y)}`;
  for (let s = 0.25; s <= 12; s += 0.25) { const cx = Math.round(p.x + ux * s), cy = Math.round(p.y + uy * s); const k = `${cx},${cy}`; if (k === last) continue; last = k; if (isWall(cx, cy)) return open; if (++open > 8) return null; }
  return null;
}
function openNeighbours(isWall, p) { let k = 0; for (let dx = -1; dx <= 1; dx++) for (let dy = -1; dy <= 1; dy++) if ((dx || dy) && !isWall(p.x + dx, p.y + dy)) k++; return k; }
function openRun(isWall, dir) { for (let s = 1; s <= 8; s++) if (isWall(dir.x * s, dir.y * s)) return s - 1; return 8; }

// 도착지 뒤 사실 — 시험마다 같은 형식으로 모든 후보에 붙임(한쪽만 꾸미지 않음)
const VULN_ASSUMED = 2.5;   // 가정: 부딪힌 뒤 취약 약 2.5초(정적 조사의 로그 참고 2.5~2.8초 덩어리, 실제 값 모름)
function outcome(T, wb) {
  if (!T.outcome) return '';
  const pre = T.behind === 't1' ? '예상(가정: 조준이 출발 직전까지 이 자리로 따라오고, 돌진에 내가 맞지 않을 때)' : '예상(가정: 다음 돌진이 이 자리로 겨눠 오고, 돌진에 내가 맞지 않을 때)';
  return wb === null ? ` ${pre}: 돌진이 부딪힐 것 없음, 취약 안 됨.` : ` ${pre}: 돌진이 벽에 부딪혀 버섯 야수가 약 ${VULN_ASSUMED}초 취약(그동안 피해 제한 없음).`;
}
function behindText(T, wb, here) {
  const at = here ? '지금 자리' : '도착지';
  if (T.behind === 't1') {
    const s = wb === null ? '8칸 넘게 열림(부딪힐 것 없음)' : wb === 0 ? '바로 뒤 칸이 벽(돌진이 벽에 부딪힐 것으로 예상)' : `열린 칸 ${wb}칸 다음 벽(돌진이 벽에 부딪힐 것으로 예상)`;
    return `적이 ${at}로 다시 겨누면 돌진 선은 ${at}를 지나 이어짐 — ${at} 뒤(적 반대쪽): ${s}`;
  }
  if (T.behind === 't2') {
    const s = wb === null ? '8칸 넘게 열림(돌진 선이 끝날 벽 없음)' : wb === 0 ? '바로 뒤 칸이 벽(돌진 선이 이 벽에서 끝남)' : `열린 칸 ${wb}칸 다음 벽(돌진 선이 이 벽에서 끝남)`;
    return `${at} 뒤(적 반대쪽, 적이 돌진하면 돌진 선이 이어지는 쪽): ${s}`;
  }
  return `${at} 뒤(적 반대쪽): ${wb === null ? '8칸 넘게 열림' : wb === 0 ? '바로 벽' : '열린 칸 ' + wb + '칸 다음 벽'}`;
}
function chargeLine(T, from, to, len) {
  if (T.state !== 'telegraph') return '돌진 선: 해당 없음(지금 돌진 준비·돌진 중 아님)';
  const a = lateral(T.enemy, from), b = lateral(T.enemy, to);
  const onNow = a.lat <= H && a.along > 0, onDest = b.lat <= H && b.along > 0;
  let s = `지금 돌진 선: 지금 ${onNow ? '선 위' : '선 밖'} → 도착지 ${b.along <= 0 ? '적 뒤쪽' : onDest ? `선 위(선 중심에서 ${r1(b.lat)}칸)` : `선 밖(선 중심에서 ${r1(b.lat)}칸)`}`;
  if (onNow && !onDest && b.along > 0 && len > 0) s += `, 선 밖으로 나가기까지 약 ${r2(H / (W * (Math.abs(b.lat - a.lat) / len)))}초`;
  else if (onNow && onDest) s += ', 가는 내내 선 위';
  return s;
}
// T3: 끝날 때까지 시간과 그때 취약 남은 시간·거리(모든 후보 같은 형식)
function timeText(T, secs, secsHi, dEnd) {
  if (!T.timeFacts) return '';
  const left = (s) => Math.max(0, ASSUME.vulnLeft - s);
  const sec = secsHi ? `약 ${r2(secs)}~${r2(secsHi)}초` : `약 ${r2(secs)}초`;
  const vl = secsHi ? `약 ${r2(left(secsHi))}~${r2(left(secs))}초` : `약 ${r2(left(secs))}초`;
  const inR = dEnd <= ASSUME.swingReach + 1e-6;
  return ` 시간: 끝날 때까지 ${sec}, 그때 적 취약 남은 ${vl}${left(secsHi || secs) <= 0 ? '(취약 끝남)' : ''}, 그때 적과 거리 ${r1(dEnd)}칸(휘두르는 거리 ${inR ? '안' : '밖'}, 적이 제자리일 때).`;
}

const REL = { toward: ['앞(다가가기)', (F) => F.f], away: ['뒤(멀어지기)', (F) => F.b], left: ['왼쪽', (F) => F.l], right: ['오른쪽', (F) => F.r],
  backleft: ['왼쪽 뒤', (F) => ({ x: F.b.x + F.l.x, y: F.b.y + F.l.y })], backright: ['오른쪽 뒤', (F) => ({ x: F.b.x + F.r.x, y: F.b.y + F.r.y })] };

function buildCands(T) {
  const isWall = MAPS[T.map], F = frame(T.enemy), O = { x: 0, y: 0 };
  const d0 = dist(O, T.enemy), inRange = d0 <= ASSUME.swingReach + 1e-6;
  const out = [];
  const move = (rel, cells, isLong) => {
    const [name, vec] = REL[rel]; const v = vec(F); const diag = v.x !== 0 && v.y !== 0;
    let ok = 0; for (let s = 1; s <= cells; s++) { const c = { x: v.x * s, y: v.y * s }; if (isWall(c.x, c.y) || dist(c, T.enemy) < ASSUME.enemyBodyBlock) break; ok = s; }
    if (isLong ? ok < 2 : ok < 1) return;
    const n = isLong ? ok : 1, to = { x: v.x * n, y: v.y * n }, len = Math.hypot(to.x, to.y);
    const wb = wallBehind(isWall, T.enemy, to), k = openNeighbours(isWall, to), d1 = dist(to, T.enemy);
    out.push({ value: `${isLong ? 'm3' : 'm1'}_${rel}`, wb, desc: `${isLong ? '긴' : '짧은'} 이동: 적 기준 ${name} ${n}칸${diag ? '(대각)' : ''} — (${ORIGIN.x + to.x},${ORIGIN.y + to.y})로 감(${compass(to.x, to.y)}). 도착까지 약 ${r2(len / W)}초. 적과 거리 ${r1(d0)}→${r1(d1)}칸(적이 제자리일 때). ${chargeLine(T, O, to, len)}. ${behindText(T, wb, false)}.${outcome(T, wb)} 도착지 둘레 열린 칸 ${k}/8${k <= 3 ? '(막다른 곳)' : '(막다른 곳 아님)'}. 도착하거나 막히면 끝나고 바로 다시 판단(가는 중에도 바꿀 수 있음). 그동안 공격 안 함.${timeText(T, len / W, 0, d1)}` });
  };
  for (const r of ['toward', 'away', 'left', 'right', 'backleft', 'backright']) move(r, 1, false);
  for (const r of ['away', 'backleft', 'backright']) move(r, (r === 'away') ? 3 : 2, true);
  const wbHere = wallBehind(isWall, T.enemy, O);
  const onLine = T.state === 'telegraph' ? ' 지금 돌진 선 위에 그대로.' : '';
  const stayFacts = T.behind ? `${behindText(T, wbHere, true)}.${outcome(T, wbHere)}` : '';
  const vuln = T.state === 'vulnerable';
  if (inRange) out.push({ value: 'hit', wb: wbHere, desc: `한 번 치기 — MushroomBrute(${ENEMY_ID}) ${r1(d0)}칸에게 WoodSword 휘두르기 1회. 타격은 시작 약 0.07초 뒤, 휘두르는 0.15초 동안 방향 고정. 이번 타격 예상 피해: ${vuln ? '약 36(지금 이 적 피해 제한 없음, 관찰한 이 무기 피해 약 36/타)' : '1(지금 이 적은 한 번에 최대 1, 관찰한 이 무기 피해 약 36/타)'}. 그동안 제자리.${onLine} ${stayFacts} 휘두르기가 끝나면 바로 다시 판단.${timeText(T, 0.4, 0, d0)}` });
  else {
    const need = d0 - ASSUME.swingReach + 0.05, to = { x: F.f.x * need, y: F.f.y * need };
    out.push({ value: 'approach', wb: null, desc: `사거리까지 접근 — MushroomBrute(${ENEMY_ID}) 쪽으로 걸어 ${r1(ASSUME.swingReach)}칸 안에 들면 멈춤. 약 ${r1(need)}칸, 약 ${r2(need / W)}초. 적과 거리 ${r1(d0)}→${r1(dist(to, T.enemy))}칸(적이 제자리일 때). ${chargeLine(T, O, to, need)}. 공격은 안 함. 도착하면 바로 다시 판단.${timeText(T, need / W, 0, dist(to, T.enemy))}` });
  }
  out.push({ value: 'hold', wb: wbHere, desc: `제자리 유지 — 서서 다음 판단까지(약 ${r1(ASSUME.holdSeconds)}초) 기다림. 공격 안 함. 적과 거리 ${r1(d0)}칸 그대로.${onLine} ${stayFacts}${timeText(T, ASSUME.holdSeconds, 0, d0)}` });
  out.push({ value: 'eat_Mushroom', wb: wbHere, desc: `먹기 — Mushroom(20초 동안 체력 회복(값 21)), 가방에 2개. 손에 들고 먹는 데 약 0.5~1초, 그동안 공격 안 함. 내 체력 ${T.hp}/100. 그동안 제자리.${onLine} ${stayFacts}${timeText(T, 0.5, 1.0, d0)}` });
  const fs1 = { x: 0, y: 1 }; const rel = fs1.x === F.l.x && fs1.y === F.l.y ? '왼쪽' : '오른쪽';
  const wbR = wallBehind(isWall, T.enemy, fs1);
  out.push({ value: 'retreat_0_-1', wb: wbR, desc: `물러나기(싸움을 떠남) — 코어 스폰 자리 (0,-1)까지 열린 아는 길 19칸, 예상 약 3초. 가는 동안 공격·목표 일 안 함. 10칸 안에 보이는 적이 2초 없으면(4칸 이상 움직인 뒤) 또는 도착하면 멈춤 — 이 싸움을 이기기 어려워 벗어날 때. 적이 따라오는지는 모름. 가는 쪽: 적(${compass(T.enemy.x, T.enemy.y)})의 옆쪽(북쪽). 첫 걸음: 적 기준 ${rel} 1칸, ${chargeLine(T, O, fs1, 1)}.${T.behind ? ' 첫 걸음 ' + behindText(T, wbR, false) + '.' + outcome(T, wbR) : ''}${timeText(T, 3.0, 0, dist({ x: 0, y: 19 }, T.enemy))}` });
  out.push({ value: 'wait', wb: wbHere, desc: `기다리기 — 2초 제자리에 서 있기(할 만한 것이 없을 때).${timeText(T, 2.0, 0, d0)}` });
  return out;
}

function buildInput(T, know) {
  const isWall = MAPS[T.map], F = frame(T.enemy), d0 = dist({ x: 0, y: 0 }, T.enemy);
  const { lines } = loadBase();
  const towardMe = compass(-T.enemy.x, -T.enemy.y);
  let state, line = '', extra = '';
  if (T.state === 'telegraph') {
    state = '돌진 준비(전조) 0.3초째, 돌진 출발까지 약 0.6초 남음. 겨누는 대상: 나';
    line = ` 돌진 방향: ${towardMe}(나를 향함). 나는 돌진 선 위(선 중심에서 0.0칸; 돌진에 맞는 폭은 선 양옆 약 ${r1(H)}칸). 조준: 출발 직전까지 나를 다시 겨눔(게임 작성기 기본값 기준, 이 적의 실제 값은 모름) — 출발 전에 자리를 옮기면 새 자리로 다시 겨눔.`;
  } else if (T.state === 'vulnerable') {
    state = `돌진이 벽에 부딪힌 뒤 멈춰 있음, 이 상태 약 ${r1(ASSUME.vulnLeft)}초 남음`; line = ' 돌진 선: 없음(돌진 준비·돌진 중 아님).';
  } else { state = '나를 쫓는 중(추적), 나를 바라봄'; line = ' 돌진 선: 없음(돌진 준비·돌진 중 아님).'; extra = ` 다음 돌진 준비가 가능해지기까지 약 ${r1(T.cooldown)}초.`; }
  const cap = T.state === 'vulnerable' ? '없음(지금 취약: 정상 피해가 들어감)' : '한 번에 최대 1(지금 취약 아님)';
  const eAbs = { x: ORIGIN.x + T.enemy.x, y: ORIGIN.y + T.enemy.y };
  const taken = T.dropHistory ? '' : ' 이 종류에게 받은 피해(관찰 71번) 약 54/번.';
  const enemyObs = `적 관측(0.02초 전, 새 정보): MushroomBrute(${ENEMY_ID}) ${compass(T.enemy.x, T.enemy.y)} ${r1(d0)}칸 (${r1(eAbs.x)},${r1(eAbs.y)}), 체력 ${T.ehp || '260/540'}. 상태: ${state}.${line} 받는 피해 제한: ${cap}.${extra}${taken}`;
  const me = `나: 체력 ${T.hp}/100, 무기 WoodSword(휘두르는 거리 ${r1(ASSUME.swingReach)}칸 안, 휘두르기 간격 0.4초, 지금 휘두를 수 있음), 걷기 약 ${W}칸/초(1칸 약 ${r2(1 / W)}초), 지금 동작: 서 있음. 적이 ${d0 <= ASSUME.swingReach + 1e-6 ? '휘두르는 거리 안' : '휘두르는 거리 밖'}.`;
  const dirs = [['뒤', F.b], ['왼쪽', F.l], ['오른쪽', F.r], ['왼쪽 뒤', { x: F.b.x + F.l.x, y: F.b.y + F.l.y }], ['오른쪽 뒤', { x: F.b.x + F.r.x, y: F.b.y + F.r.y }]];
  const runs = dirs.map(([n, v]) => { const k = openRun(isWall, v); return `${n}(${compass(v.x, v.y)}) ${k >= 8 ? '8칸 넘게' : k + '칸'}`; }).join(', ');
  const wb = wallBehind(isWall, T.enemy, { x: 0, y: 0 });
  const around = `주변(적 기준 방향별, 내 자리에서 벽 전까지 열린 칸): ${runs}. 적에서 나를 잇는 선을 따라 내 뒤: ${wb === null ? '8칸 넘게 열림' : wb === 0 ? '바로 벽' : '열린 칸 ' + wb + '칸 다음 벽'}. 내 자리 둘레 열린 칸 ${openNeighbours(isWall, { x: 0, y: 0 })}/8.`;
  const out = [];
  for (const l of lines) {
    if (l.startsWith('내 상태:')) out.push(l.replace(/체력 \d+\/100/, `체력 ${T.hp}/100`));
    else if (l.startsWith('가방:')) out.push(l.replace(/\.$/, ', Mushroom 2.'));
    else if (l.startsWith('지금 하는 일:')) out.push('지금 하는 일: 없음(서 있음).');
    else if (l.startsWith('지금 동작:') || l.startsWith('최근 10초 동안')) continue;
    else if (l.startsWith('최근 결과:') && T.dropRecent) out.push('최근 결과: 없음.');
    else if (l.startsWith('화면에 보이는 다른 플레이어:')) out.push('화면에 보이는 다른 플레이어: 없음. 다른 플레이어는 같은 편이라 공격 대상이 아니다.');
    else if (l.startsWith('전투 상황:')) {
      out.push(`전투 상황: 보이는 적 1, 가장 가까운 적 MushroomBrute ${r1(d0)}칸, 최근 5초 내 체력 변화 0${T.dropHistory ? '' : ', 이번 접속 쓰러짐 2번'}. 프로그램은 스스로 피하거나 물러나지 않는다(고른 행동만 함).`);
      out.push(enemyObs, me, around);
      if (know) out.push(KNOWLEDGE_LINE);
    } else if (l.startsWith('지금 다시 판단하는 이유:')) out.push(`지금 다시 판단하는 이유: ${T.trigger}.`);
    else out.push(l);
  }
  return out.join('\n');
}

const TESTS = [
  { id: 'T1o', title: 'T5: T1 + 예상 결과(벽은 왼쪽 피하기 쪽)', map: 't1', behind: 't1', outcome: true, enemy: { x: 1.2, y: 0 }, hp: 114, state: 'telegraph', trigger: '긴급: 적 MushroomBrute 돌진 준비 시작(나를 겨눔)' },
  { id: 'T1mo', title: 'T5: T1m + 예상 결과(벽은 오른쪽 피하기 쪽)', map: 't1m', behind: 't1', outcome: true, enemy: { x: 1.2, y: 0 }, hp: 114, state: 'telegraph', trigger: '긴급: 적 MushroomBrute 돌진 준비 시작(나를 겨눔)' },
  { id: 'T2o', title: 'T5: T2 + 예상 결과', map: 't2', behind: 't2', outcome: true, enemy: { x: 1.2, y: 0 }, hp: 114, state: 'chase', cooldown: 2.0, trigger: '전투 중 계속 다시 판단' },
  { id: 'T6', title: 'T3 시간 사실 + T4b 기록 줄 뺌', map: 's4', behind: '', enemy: { x: -2.5, y: 0 }, hp: 114, state: 'vulnerable', timeFacts: true, dropHistory: true, dropRecent: true, trigger: '전투 중 계속 다시 판단' },
];
const PAIRS = [['m1_left', 'm1_right'], ['m1_backleft', 'm1_backright'], ['m3_backleft', 'm3_backright']];
function mulberry32(a) { return () => { a |= 0; a = (a + 0x6D2B79F5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }

export function extraVariants(model) {
  const v = [];
  for (const T0 of TESTS) for (const know of [false, true]) for (const order of ['orig', 'swap']) for (const vary of ['base', 'dist', 'shuf']) {
    const T = vary === 'dist' ? { ...T0, enemy: { x: T0.enemy.x + Math.sign(T0.enemy.x) * 0.1, y: 0 } } : T0;
    let cands = buildCands(T);
    if (vary === 'shuf') { const rnd = mulberry32(1105 + T0.id.length * 31 + T0.id.charCodeAt(1) * 7 + (know ? 1 : 0)); for (let i = cands.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [cands[i], cands[j]] = [cands[j], cands[i]]; } }
    if (order === 'swap') for (const [a, b] of PAIRS) { const i = cands.findIndex((c) => c.value === a), j = cands.findIndex((c) => c.value === b); if (i >= 0 && j >= 0) [cands[i], cands[j]] = [cands[j], cands[i]]; }
    const input = buildInput(T, know);
    v.push({ id: `${T0.id}-${know ? 'K' : 'N'}-${order}-${vary}`, test: T0.id, know, order, vary, cands, input, body: buildBody(model, input, cands.map((c) => ({ value: c.value, desc: c.desc }))) });
  }
  return v;
}

// ---------------- 실행·집계
const S4_GOOD = ['approach', 'm1_toward'], S4_BAD = ['m1_away', 'm3_away', 'm1_backleft', 'm1_backright', 'm3_backleft', 'm3_backright', 'retreat_0_-1'];
function settingsModel() {
  const p = path.join(os.homedir(), 'AppData', 'LocalLow', 'Pugstorm', 'Core Keeper', 'AIPlayers', 'decisions-settings.json');
  try { const j = JSON.parse(fs.readFileSync(p, 'utf8').replace(/^﻿/, '')); return { model: j.model || 'gpt-6-luna', usdPerM: j.usdPerMillionInputTokens ?? 0.1 }; } catch { return { model: 'gpt-6-luna', usdPerM: 0.1 }; }
}
function apiKey() {
  if (process.env.OPENAI_API_KEY) return process.env.OPENAI_API_KEY;
  try { return execFileSync('powershell.exe', ['-NoProfile', '-Command', "[Environment]::GetEnvironmentVariable('OPENAI_API_KEY','User')"], { encoding: 'utf8' }).trim(); } catch { return ''; }
}

function analyze(usdPerM) {
  const recs = fs.readFileSync(path.join(HERE, 'raw', 'extra2.jsonl'), 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l));
  const L = []; const rows = [];
  const tot = recs.reduce((s, r) => s + (r.inputTokens || 0), 0);
  L.push(`calls=${recs.length} ok=${recs.filter((r) => r.ok).length} inputTokens=${tot} usd(at ${usdPerM}/M)=${(tot * usdPerM / 1e6).toFixed(4)}`);
  const pct = (a, q) => { const s = [...a].sort((x, y) => x - y); const i = (s.length - 1) * q; const lo = Math.floor(i), hi = Math.ceil(i); return s[lo] + (s[hi] - s[lo]) * (i - lo); };
  L.push(`latency p50=${pct(recs.map((r) => r.latency), 0.5).toFixed(3)} p95=${pct(recs.map((r) => r.latency), 0.95).toFixed(3)}`);
  for (const r of recs) {
    const tag = (val) => { const c = r.cands.find((x) => x.value === val); return c ? c.wb : undefined; };
    const wallish = (val) => { const wb = tag(val); return wb !== null && wb !== undefined && wb <= (r.test.startsWith('T1') ? 3 : 1); };
    const mass = (pred) => (r.probabilities || []).filter((q) => pred(q.value)).reduce((s, q) => s + q.probability, 0);
    let cls = '-';
    if (r.test.startsWith('T1')) cls = !r.ok ? 'FAIL' : wallish(r.choice) && /^m[13]_/.test(r.choice) ? 'wall-side' : /^m[13]_(left|right|backleft|backright)$/.test(r.choice) ? 'open-side' : 'other';
    else if (r.test.startsWith('T2')) { const wb = tag(r.choice); cls = !r.ok ? 'FAIL' : !/^m[13]_/.test(r.choice) ? 'non-move' : wb === null ? 'move, open behind' : wb <= 1 ? 'move, wall <=1' : 'move, wall 2-3'; }
    else cls = !r.ok ? 'FAIL' : S4_GOOD.includes(r.choice) ? 'good' : S4_BAD.includes(r.choice) ? 'bad' : 'other';
    const top3 = [...(r.probabilities || [])].sort((a, b) => b.probability - a.probability).slice(0, 4).map((q) => `${q.value}=${q.probability.toFixed(2)}`).join(' ');
    const wallMass = r.test.startsWith('T1') || r.test.startsWith('T2') ? mass((v) => /^m[13]_/.test(v) && wallish(v)) : null;
    const goodMass = !r.test.startsWith('T1') && !r.test.startsWith('T2') ? mass((v) => S4_GOOD.includes(v)) : null;
    const retreatP = mass((v) => v === 'retreat_0_-1');
    rows.push({ id: r.id, test: r.test, know: r.know, order: r.order, vary: r.vary, choice: r.choice, cls, wallMass, goodMass, retreatP, latency: r.latency, tokens: r.inputTokens, type: r.type });
    L.push(`${r.id}: ${r.choice || r.type} [${cls}] ${wallMass != null ? `wallMass=${wallMass.toFixed(2)} ` : ''}${goodMass != null ? `goodMass=${goodMass.toFixed(2)} ` : ''}retreatP=${retreatP.toFixed(2)} top: ${top3} lat=${r.latency.toFixed(2)}`);
  }
  const agg = {};
  for (const x of rows) { for (const k of [x.test, `${x.test}-${x.know ? 'K' : 'N'}`, `${x.test}-${x.order}`]) { agg[k] = agg[k] || { n: 0, cls: {}, wallMass: [], goodMass: [], retreatP: [] }; const a = agg[k]; a.n++; a.cls[x.cls] = (a.cls[x.cls] || 0) + 1; if (x.wallMass != null) a.wallMass.push(x.wallMass); if (x.goodMass != null) a.goodMass.push(x.goodMass); a.retreatP.push(x.retreatP); } }
  const mean = (a) => (a.length ? (a.reduce((s, v) => s + v, 0) / a.length).toFixed(2) : '-');
  L.unshift(...Object.entries(agg).map(([k, a]) => `${k}: n=${a.n} ${JSON.stringify(a.cls)} wallMass=${mean(a.wallMass)} goodMass=${mean(a.goodMass)} retreatP=${mean(a.retreatP)}`), '');
  fs.writeFileSync(path.join(HERE, 'summary-extra2.json'), JSON.stringify({ calls: recs.length, inputTokens: tot, usd: tot * usdPerM / 1e6, agg, rows }, null, 2));
  fs.writeFileSync(path.join(HERE, 'summary-extra2.txt'), L.join('\n'));
  console.log(L.join('\n'));
}

async function main() {
  const args = new Set(process.argv.slice(2));
  const S = settingsModel();
  if (args.has('--analyze')) { analyze(S.usdPerM); return; }
  const vs = extraVariants(S.model);
  fs.mkdirSync(path.join(HERE, 'requests-extra2'), { recursive: true });
  for (const v of vs) fs.writeFileSync(path.join(HERE, 'requests-extra2', `${v.id}.json`), JSON.stringify(v.body, null, 2));
  console.log(`model=${S.model} variants=${vs.length}`);
  if (args.has('--dry')) return;
  if (vs.length > 200) { console.error('상한 200 초과'); process.exit(2); }
  const out = path.join(HERE, 'raw', 'extra2.jsonl');
  if (fs.existsSync(out) && !args.has('--overwrite')) { console.error('raw/extra2.jsonl 이 이미 있음(--overwrite 로 덮어씀)'); process.exit(2); }
  const key = apiKey(); if (!key) { console.error('OPENAI_API_KEY 없음'); process.exit(2); }
  const ws = fs.createWriteStream(out, { flags: 'w' });
  let next = 0, consec = 0, stop = '';
  const runStart = new Date().toISOString();
  async function worker() {
    while (!stop && next < vs.length) {
      const v = vs[next++];
      const t0 = performance.now(); let status = 0, text = '', err = '';
      try { const r = await fetch('https://api.openai.com/v1/decisions', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + key }, body: JSON.stringify(v.body), signal: AbortSignal.timeout(12000) }); status = r.status; text = await r.text(); } catch (e) { err = String(e && e.name || e); }
      const latency = (performance.now() - t0) / 1000;
      let p = {}; try { const j = JSON.parse(text); const a = j.answers && j.answers[0]; p = { respModel: j.model, type: a && a.type, choice: a && a.choice, confidence: a && a.confidence, probabilities: a && a.probabilities, inputTokens: j.usage && j.usage.input_tokens, error: j.error && j.error.message }; } catch { }
      const ok = status === 200 && p.type === 'choice' && !!p.choice; consec = ok ? 0 : consec + 1;
      ws.write(JSON.stringify({ t: new Date().toISOString(), id: v.id, test: v.test, know: v.know, order: v.order, vary: v.vary, status, netErr: err, latency: +latency.toFixed(4), ok, ...p, cands: v.cands.map((c) => ({ value: c.value, wb: c.wb })), body: text }) + '\n');
      console.log(`${v.id} ${status} ${latency.toFixed(2)} ${ok ? p.choice : 'FAIL ' + (p.type || p.error || err)}`);
      if (status === 429) stop = '429'; else if (consec >= 3) stop = '연속 실패 3번';
    }
  }
  await Promise.all([worker(), worker()]);
  await new Promise((r) => ws.end(r));
  fs.writeFileSync(path.join(HERE, 'raw', 'extra2-meta.json'), JSON.stringify({ runStart, runEnd: new Date().toISOString(), model: S.model, calls: vs.length, stoppedBecause: stop || null }, null, 2));
  if (stop) console.error('멈춤: ' + stop);
  analyze(S.usdPerM);
}
main();
