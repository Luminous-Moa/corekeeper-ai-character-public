// 판단 시험 시나리오 조립기 (2026-11-05). 게임 없이 판단 모델에 보낼 요청을 만든다.
// 바탕: 2026-11-04 실게임 decisions 로그의 실제 전투 요청 @Claude DECISION #773 (base-claude-773.input.log).
// 바꾸는 곳: 적 관측 부분(전투 상황 줄 + 새 관측 줄)과 후보 목록. 그 밖에 1:1로 만들려고 바꾼 줄은 DEVIATIONS에 적는다.
// 후보 문장은 사실만 쓴다(도착 시간, 거리 변화, 돌진 선, 도착지 뒤 벽, 막다른 곳). 위험해도 빼지 않는다.
// 빼는 것: 벽 칸, 적 몸이 있는 칸(적 중심 0.6칸 안), 도착지가 다른 후보와 같은 것(사거리 안에서의 접근 = 유지).
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const HERE = path.dirname(fileURLToPath(import.meta.url));

// ---- 가정값 (게임에서 아직 재지 않음). 문서의 가정값 목록과 같다.
export const ASSUME = {
  walkTilesPerSec: 5.7,        // 실측(movetrace 11-04, 모드 상수 WalkSpeed) — 가정 아님, 참고로 여기 둠
  swingReach: 1.3,             // 모드 AttackSkill이 휘두르는 거리(AIBodySkills.cs, d <= 1.3f) — 코드 값
  chargeHalfWidth: 0.8,        // 가정: 돌진에 맞는 폭(선 중심에서 양옆) = hitRadius + 몸 반경, 값 모름
  anticipationTotal: 0.9,      // 가정: 전조 길이 0.9초 (S1·S2에서 0.3초 지남, 0.6초 남음)
  chargeSpeed: 12,             // 가정: 돌진 속도 12칸/초 (S6)
  chargeMaxLeft: 0.9,          // 가정: 돌진이 앞으로 최대 0.9초 더
  vulnLeft: 1.5,               // 과제 지정: 취약 남은 시간 약 1.5초 (S3·S4)
  enemyBodyBlock: 0.6,         // 가정: 적 중심 0.6칸 안 칸은 몸이 있어 갈 수 없음
  holdSeconds: 0.3,            // 가정: 제자리 유지 = 다음 판단까지(전투 판단 주기 약 0.25~0.3초)
};

const W = ASSUME.walkTilesPerSec;
const ORIGIN = { x: 2, y: -15 };   // 바탕 요청의 내 위치 (2,-15)
const ENEMY_ID = 64692, ENEMY_HP = '260/540';

export const DEVIATIONS = [
  '내 상태 줄: S7만 체력을 45/100으로 바꿈(나머지는 바탕 114/100 그대로).',
  '가방 줄: 먹기 후보를 두려고 "Mushroom 2"를 더함(바탕 가방에는 음식이 없었음).',
  '지금 하는 일: "물러나기 retreat:0,-1 0초째" → "없음(서 있음)". 새 설계는 짧은 행동 뒤 바로 다시 판단하므로 계속하기 후보가 없다. 그래서 그에 딸린 "지금 동작" 줄과 "최근 10초 동안 행동을 4번 바꿈" 줄도 뺐다.',
  '화면에 보이는 다른 플레이어: 바탕에는 호스트가 적 옆 0.6칸에서 싸우고 있었음 → 1:1 시험이라 "없음"으로 바꿈.',
  '지금 다시 판단하는 이유: 시나리오에 맞춤(S1·S2·S6은 긴급 계기, 나머지는 "전투 중 계속 다시 판단").',
  '그대로 둔 줄: 첫 줄(역할), 목표(나무 10개 모으기 0/10), 보이는 범위, 최근 결과, 탐색 기억. 질문 instructions 문장도 코드 그대로(BuildJson).',
];

export const KNOWLEDGE_LINE = '알려진 사실(이 적 종류): 버섯 야수(MushroomBrute)는 취약 밖에서 한 번에 1 피해만 받는다. 돌진이 벽·물체에 부딪히면 잠시 취약해진다.';

// ---- 지도: 벽 함수. 좌표는 내 칸 (0,0) 기준, x 동쪽, y 북쪽. 경계 |x|,|y| > 12 는 벽.
const box = (f) => (x, y) => Math.abs(x) > 12 || Math.abs(y) > 12 || f(x, y);
const MAPS = {
  wallWest3: box((x) => x <= -3),   // 내 서쪽 2칸 열리고 3번째 칸부터 벽(남북으로 길게)
  wallWest4: box((x) => x <= -4),   // 서쪽 3칸 열리고 벽
  wallWest2: box((x) => x <= -2),   // 서쪽 1칸 열리고 벽 (S3: 적이 이 벽에 부딪힘)
  open: box(() => false),           // 12칸까지 열림
};

// ---- 시나리오 (HEAD가 정한 표)
export const SCENARIOS = [
  { id: 'S1', title: '전조 중(남은 약 0.6초), 나를 조준, 돌진 선 위, 내 뒤 1~2칸에 벽',
    map: 'wallWest3', enemy: { x: 1.2, y: 0 }, hp: 114, hpChange: '0', state: 'telegraph',
    trigger: '긴급: 적 MushroomBrute 돌진 준비 시작(나를 겨눔)' },
  { id: 'S2', title: '전조 중, 돌진 선 위, 뒤가 트임',
    map: 'open', enemy: { x: 1.2, y: 0 }, hp: 114, hpChange: '0', state: 'telegraph',
    trigger: '긴급: 적 MushroomBrute 돌진 준비 시작(나를 겨눔)' },
  { id: 'S3', title: '취약 중(남은 약 1.5초), 사거리 안',
    map: 'wallWest2', enemy: { x: -1.1, y: 0 }, hp: 114, hpChange: '0', state: 'vulnerable',
    trigger: '전투 중 계속 다시 판단' },
  { id: 'S4', title: '취약 중(남은 약 1.5초), 사거리 밖 1~2칸',
    map: 'wallWest3', enemy: { x: -2.5, y: 0 }, hp: 114, hpChange: '0', state: 'vulnerable',
    trigger: '전투 중 계속 다시 판단' },
  { id: 'S5', title: '평소(추적 중, 전조 아님, 다음 돌진까지 약 2초), 사거리 안',
    map: 'wallWest4', enemy: { x: 1.2, y: 0 }, hp: 114, hpChange: '0', state: 'chase', cooldown: 2.0,
    trigger: '전투 중 계속 다시 판단' },
  { id: 'S6', title: '이미 돌진 출발, 돌진 선 위, 거리 3칸',
    map: 'wallWest4', enemy: { x: 3.0, y: 0 }, hp: 114, hpChange: '0', state: 'charging',
    trigger: '긴급: 적 MushroomBrute 돌진 시작(나를 향함)' },
  { id: 'S7', title: '한 번 더 맞으면 쓰러질 체력, 전조 없음',
    map: 'open', enemy: { x: 1.2, y: 0 }, hp: 45, hpChange: '-54', state: 'chase', cooldown: 1.0,
    trigger: '전투 중 계속 다시 판단' },
];

// ---- 후보 판정 범주. good/bad 는 HEAD 표를 후보 키로 옮긴 것. 나머지는 'other'(판정 밖).
const SIDE_SHORT = ['m1_left', 'm1_right', 'm1_backleft', 'm1_backright', 'm1_frontleft', 'm1_frontright'];
const SIDE_LONG = ['m3_left', 'm3_right', 'm3_backleft', 'm3_backright'];
const AWAY = ['m1_away', 'm3_away'];
const MOVES_ALL = ['m1_toward', 'm1_away', ...SIDE_SHORT, 'm3_away', ...SIDE_LONG, 'approach'];
export const JUDGE = {
  S1: { good: SIDE_SHORT, bad: ['hit', 'hold', 'wait', ...AWAY, 'm1_toward', 'approach'] },
  S2: { good: SIDE_SHORT.concat(SIDE_LONG), bad: ['hit', 'hold', 'wait', ...AWAY] },
  S3: { good: ['hit'], bad: [...MOVES_ALL, 'retreat', 'hold', 'wait'] },
  S4: { good: ['approach', 'm1_toward', 'm1_frontleft', 'm1_frontright'], bad: ['m1_away', 'm3_away', 'm1_backleft', 'm1_backright', 'm3_backleft', 'm3_backright', 'retreat'] },
  S5: null,
  S6: { good: SIDE_SHORT.concat(SIDE_LONG), bad: ['hit', 'hold', 'wait', ...AWAY] },
  S7: { good: ['retreat', 'eat'], bad: ['m1_toward', 'approach', 'hit'] },
};

// ---- 기하
const r1 = (v) => (Math.round(v * 10) / 10).toFixed(1);
const r2 = (v) => (Math.round(v * 100) / 100).toFixed(2);
const compass = (dx, dy) => {
  const ns = dy > 0.3 ? '북' : dy < -0.3 ? '남' : '';
  const ew = dx > 0.3 ? '동' : dx < -0.3 ? '서' : '';
  return (ns + ew) + '쪽' === '쪽' ? '제자리' : ns + ew + '쪽';
};
const dist = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);

function frame(enemy) {
  // f: 나→적 단위벡터(앞). l: 적을 바라볼 때 왼쪽. 시나리오는 적이 동/서 축 위라 칸 방향과 맞는다.
  const n = Math.hypot(enemy.x, enemy.y);
  const f = { x: Math.round(enemy.x / n), y: Math.round(enemy.y / n) };
  const l = { x: -f.y, y: f.x };
  return { f, l, b: { x: -f.x, y: -f.y }, r: { x: f.y, y: -f.x } };
}

// 돌진 선: 적 위치에서 나를 향한 방향(전조·돌진 중일 때). 칸 p의 선 중심에서 옆 거리, 그리고 적보다 앞쪽인지.
function lateral(enemy, p) {
  const n = Math.hypot(enemy.x, enemy.y);
  const d = { x: -enemy.x / n, y: -enemy.y / n };   // 적→나 방향
  const v = { x: p.x - enemy.x, y: p.y - enemy.y };
  const along = v.x * d.x + v.y * d.y;
  const lat = Math.abs(v.x * d.y - v.y * d.x);
  return { lat, along };
}

// 적→p 방향으로 p 다음 칸부터 세어 벽 전까지 열린 칸 수(8칸까지, 넘으면 null)
function wallBehind(isWall, enemy, p) {
  const vx = p.x - enemy.x, vy = p.y - enemy.y; const n = Math.hypot(vx, vy);
  const ux = vx / n, uy = vy / n;
  let open = 0, last = `${Math.round(p.x)},${Math.round(p.y)}`;
  for (let s = 0.25; s <= 12; s += 0.25) {
    const cx = Math.round(p.x + ux * s), cy = Math.round(p.y + uy * s);
    const k = `${cx},${cy}`; if (k === last) continue; last = k;
    if (isWall(cx, cy)) return open;
    if (++open > 8) return null;
  }
  return null;
}
function openNeighbours(isWall, p) {
  let k = 0;
  for (let dx = -1; dx <= 1; dx++) for (let dy = -1; dy <= 1; dy++) if ((dx || dy) && !isWall(p.x + dx, p.y + dy)) k++;
  return k;
}
function openRun(isWall, dir) {   // 내 칸에서 dir 방향 열린 칸 수(8칸까지)
  for (let s = 1; s <= 8; s++) if (isWall(dir.x * s, dir.y * s)) return s - 1;
  return 8;
}

const REL = {
  toward: ['앞(다가가기)', (F) => F.f], away: ['뒤(멀어지기)', (F) => F.b],
  left: ['왼쪽', (F) => F.l], right: ['오른쪽', (F) => F.r],
  backleft: ['왼쪽 뒤', (F) => ({ x: F.b.x + F.l.x, y: F.b.y + F.l.y })],
  backright: ['오른쪽 뒤', (F) => ({ x: F.b.x + F.r.x, y: F.b.y + F.r.y })],
  frontleft: ['왼쪽 앞', (F) => ({ x: F.f.x + F.l.x, y: F.f.y + F.l.y })],
  frontright: ['오른쪽 앞', (F) => ({ x: F.f.x + F.r.x, y: F.f.y + F.r.y })],
};

function chargeLineText(sc, from, to, pathLen) {
  if (sc.state !== 'telegraph' && sc.state !== 'charging') return '돌진 선: 해당 없음(지금 돌진 준비·돌진 중 아님)';
  const H = ASSUME.chargeHalfWidth;
  const a = lateral(sc.enemy, from), b = lateral(sc.enemy, to);
  const onNow = a.lat <= H && a.along > 0, onDest = b.lat <= H && b.along > 0;
  const where = (x) => (x.along <= 0 ? '적 뒤쪽' : x.lat <= H ? `선 위(선 중심에서 ${r1(x.lat)}칸)` : `선 밖(선 중심에서 ${r1(x.lat)}칸)`);
  let s = `돌진 선: 지금 ${onNow ? '선 위' : '선 밖'} → 도착지 ${where(b)}`;
  if (onNow && !onDest && b.along > 0 && pathLen > 0) {
    const sinT = Math.abs(b.lat - a.lat) / pathLen;   // 칸당 옆으로 벌어지는 비율
    s += `, 선 밖으로 나가기까지 약 ${r2(H / (W * sinT))}초`;
  } else if (onNow && onDest) s += ', 가는 내내 선 위';
  if (b.along <= 0) s += '(적을 지나쳐 적 뒤로 감)';
  return s;
}

function moveCand(sc, isWall, F, relKey, cells, value, isLong) {
  const [relName, vec] = REL[relKey];
  const v = vec(F);
  const diag = v.x !== 0 && v.y !== 0;
  // 길을 따라 칸마다 벽·적 몸 확인. 긴 이동은 3칸(대각 2칸)을 시도, 막히면 2칸까지 줄임.
  let ok = 0;
  for (let s = 1; s <= cells; s++) {
    const c = { x: v.x * s, y: v.y * s };
    if (isWall(c.x, c.y) || dist(c, sc.enemy) < ASSUME.enemyBodyBlock) break;
    ok = s;
  }
  if (isLong ? ok < 2 : ok < 1) return null;
  const n = isLong ? ok : 1;
  const to = { x: v.x * n, y: v.y * n };
  const len = Math.hypot(to.x, to.y);
  const d0 = dist({ x: 0, y: 0 }, sc.enemy), d1 = dist(to, sc.enemy);
  const wb = wallBehind(isWall, sc.enemy, to);
  const k = openNeighbours(isWall, to);
  const abs = { x: ORIGIN.x + to.x, y: ORIGIN.y + to.y };
  const desc = `${isLong ? '긴' : '짧은'} 이동: 적 기준 ${relName} ${n}칸${diag ? '(대각)' : ''} — (${abs.x},${abs.y})로 감(${compass(to.x, to.y)}). `
    + `도착까지 약 ${r2(len / W)}초. 적과 거리 ${r1(d0)}→${r1(d1)}칸(적이 제자리일 때). `
    + `${chargeLineText(sc, { x: 0, y: 0 }, to, len)}. `
    + `도착지 뒤(적 반대쪽): ${wb === null ? '8칸 넘게 열림' : wb === 0 ? '바로 벽' : '열린 칸 ' + wb + '칸 다음 벽'}. `
    + `도착지 둘레 열린 칸 ${k}/8${k <= 3 ? '(막다른 곳)' : '(막다른 곳 아님)'}. `
    + `도착하거나 막히면 끝나고 바로 다시 판단(가는 중에도 바꿀 수 있음). 그동안 공격 안 함.`;
  return { value, key: value === 'approach' ? 'approach' : `${isLong ? 'm3' : 'm1'}_${relKey}`, desc };
}

function stay(sc) {
  if (sc.state === 'telegraph' || sc.state === 'charging') return '그동안 제자리(돌진 선 위에 그대로)';
  return '그동안 제자리';
}

export function buildCandidates(sc, setName) {
  const isWall = MAPS[sc.map];
  const F = frame(sc.enemy);
  const d0 = dist({ x: 0, y: 0 }, sc.enemy);
  const inRange = d0 <= ASSUME.swingReach;
  const vuln = sc.state === 'vulnerable';
  const big = setName === '20';
  const out = [];
  const add = (c) => { if (c) out.push(c); };
  const shortDirs = big ? ['toward', 'away', 'left', 'right', 'backleft', 'backright', 'frontleft', 'frontright']
    : ['toward', 'away', 'left', 'right', 'backleft', 'backright'];
  for (const r of shortDirs) add(moveCand(sc, isWall, F, r, 1, `m1_${r}`, false));
  const longDirs = big ? ['away', 'backleft', 'backright', 'left', 'right'] : ['away', 'backleft', 'backright'];
  for (const r of longDirs) add(moveCand(sc, isWall, F, r, (r === 'backleft' || r === 'backright') ? 2 : 3, `m3_${r}`, true));
  if (inRange) {
    add({ value: 'hit', key: 'hit', desc: `한 번 치기 — MushroomBrute(${ENEMY_ID}) ${r1(d0)}칸에게 WoodSword 휘두르기 1회. 타격은 시작 약 0.07초 뒤, 휘두르는 0.15초 동안 방향 고정. `
      + `이번 타격 예상 피해: ${vuln ? '약 36(지금 이 적 피해 제한 없음, 관찰한 이 무기 피해 약 36/타)' : '1(지금 이 적은 한 번에 최대 1, 관찰한 이 무기 피해 약 36/타)'}. ${stay(sc)}. 휘두르기가 끝나면 바로 다시 판단.` });
  } else {
    // 사거리까지 접근: 적 쪽으로 걸어 1.3칸 안에 들면 멈춤
    const need = d0 - ASSUME.swingReach + 0.05;
    const to = { x: F.f.x * need, y: F.f.y * need };
    add({ value: 'approach', key: 'approach', desc: `사거리까지 접근 — MushroomBrute(${ENEMY_ID}) 쪽으로 걸어 ${r1(ASSUME.swingReach)}칸 안에 들면 멈춤. 약 ${r1(need)}칸, 약 ${r2(need / W)}초. 적과 거리 ${r1(d0)}→${r1(dist(to, sc.enemy))}칸(적이 제자리일 때). `
      + `${chargeLineText(sc, { x: 0, y: 0 }, to, need)}. 공격은 안 함. 도착하면 바로 다시 판단.` });
  }
  add({ value: 'hold', key: 'hold', desc: `제자리 유지 — 서서 다음 판단까지(약 ${r1(ASSUME.holdSeconds)}초) 기다림. 공격 안 함. 적과 거리 ${r1(d0)}칸 그대로. ${sc.state === 'telegraph' || sc.state === 'charging' ? '돌진 선 위에 그대로.' : ''}`.trim() });
  add({ value: 'eat_Mushroom', key: 'eat', desc: `먹기 — Mushroom(20초 동안 체력 회복(값 21)), 가방에 2개. 손에 들고 먹는 데 약 0.5~1초, 그동안 공격 안 함. 내 체력 ${sc.hp}/100. ${stay(sc)}.` });
  // 물러나기: 바탕 요청의 실제 후보 문장(ChatGPT #909 형식) + 첫 걸음 사실. 코어 스폰 (0,-1)은 내 북쪽.
  const firstStep = { x: 0, y: 1 };
  const rel = firstStep.x === F.l.x && firstStep.y === F.l.y ? '왼쪽' : firstStep.x === F.r.x && firstStep.y === F.r.y ? '오른쪽' : '?';
  const eDir = compass(sc.enemy.x, sc.enemy.y).replace('쪽', '');
  add({ value: 'retreat_0_-1', key: 'retreat', desc: `물러나기(싸움을 떠남) — 코어 스폰 자리 (0,-1)까지 열린 아는 길 19칸, 예상 약 3초. 가는 동안 공격·목표 일 안 함. 10칸 안에 보이는 적이 2초 없으면(4칸 이상 움직인 뒤) 또는 도착하면 멈춤 — 이 싸움을 이기기 어려워 벗어날 때. 적이 따라오는지는 모름. 가는 쪽: 적(${eDir}쪽)의 옆쪽(북쪽). `
    + `첫 걸음: 적 기준 ${rel} 1칸, ${chargeLineText(sc, { x: 0, y: 0 }, firstStep, 1)}.` });
  add({ value: 'wait', key: 'wait', desc: '기다리기 — 2초 제자리에 서 있기(할 만한 것이 없을 때).' });
  return out;
}

function enemyLines(sc) {
  const isWall = MAPS[sc.map];
  const F = frame(sc.enemy);
  const d0 = dist({ x: 0, y: 0 }, sc.enemy);
  const eAbs = { x: ORIGIN.x + sc.enemy.x, y: ORIGIN.y + sc.enemy.y };
  const eDir = compass(sc.enemy.x, sc.enemy.y);
  const towardMe = compass(-sc.enemy.x, -sc.enemy.y);
  let state, line = '', cap, extra = '';
  const H = ASSUME.chargeHalfWidth;
  if (sc.state === 'telegraph') {
    const el = r1(ASSUME.anticipationTotal - 0.6);
    state = `돌진 준비(전조) ${el}초째, 돌진 출발까지 약 0.6초 남음. 겨누는 대상: 나`;
    line = ` 돌진 방향: ${towardMe}(나를 향함). 나는 돌진 선 위(선 중심에서 0.0칸; 돌진에 맞는 폭은 선 양옆 약 ${r1(H)}칸).`;
  } else if (sc.state === 'charging') {
    state = `돌진 중 0.1초째, 나를 향해 약 ${ASSUME.chargeSpeed}칸/초로 오는 중(이 속도면 약 ${r2(d0 / ASSUME.chargeSpeed)}초 뒤 내 자리), 돌진은 최대 약 ${r1(ASSUME.chargeMaxLeft)}초 더`;
    line = ` 돌진 방향: ${towardMe}(나를 향함). 나는 돌진 선 위(선 중심에서 0.0칸; 돌진에 맞는 폭은 선 양옆 약 ${r1(H)}칸).`;
  } else if (sc.state === 'vulnerable') {
    state = `돌진이 벽에 부딪힌 뒤 멈춰 있음, 이 상태 약 ${r1(ASSUME.vulnLeft)}초 남음`;
    line = ' 돌진 선: 없음(돌진 준비·돌진 중 아님).';
  } else {
    state = '나를 쫓는 중(추적), 나를 바라봄';
    line = ' 돌진 선: 없음(돌진 준비·돌진 중 아님).';
    extra = ` 다음 돌진 준비가 가능해지기까지 약 ${r1(sc.cooldown)}초.`;
  }
  cap = sc.state === 'vulnerable' ? '없음(지금 취약: 정상 피해가 들어감)' : '한 번에 최대 1(지금 취약 아님)';
  const enemyObs = `적 관측(0.02초 전, 새 정보): MushroomBrute(${ENEMY_ID}) ${eDir} ${r1(d0)}칸 (${r1(eAbs.x)},${r1(eAbs.y)}), 체력 ${ENEMY_HP}. 상태: ${state}.${line} 받는 피해 제한: ${cap}.${extra} 이 종류에게 받은 피해(관찰 71번) 약 54/번.`;
  const me = `나: 체력 ${sc.hp}/100, 무기 WoodSword(휘두르는 거리 ${r1(ASSUME.swingReach)}칸 안, 휘두르기 간격 0.4초, 지금 휘두를 수 있음), 걷기 약 ${W}칸/초(1칸 약 ${r2(1 / W)}초), 지금 동작: 서 있음. 적이 ${d0 <= ASSUME.swingReach ? '휘두르는 거리 안' : '휘두르는 거리 밖'}.`;
  const dirs = [['뒤', F.b], ['왼쪽', F.l], ['오른쪽', F.r], ['왼쪽 뒤', { x: F.b.x + F.l.x, y: F.b.y + F.l.y }], ['오른쪽 뒤', { x: F.b.x + F.r.x, y: F.b.y + F.r.y }]];
  const runs = dirs.map(([n, v]) => { const k = openRun(isWall, v); return `${n}(${compass(v.x, v.y)}) ${k >= 8 ? '8칸 넘게' : k + '칸'}`; }).join(', ');
  const wb = wallBehind(isWall, sc.enemy, { x: 0, y: 0 });
  const around = `주변(적 기준 방향별, 내 자리에서 벽 전까지 열린 칸): ${runs}. 적에서 나를 잇는 선을 따라 내 뒤: ${wb === null ? '8칸 넘게 열림' : wb === 0 ? '바로 벽' : '열린 칸 ' + wb + '칸 다음 벽'}. 내 자리 둘레 열린 칸 ${openNeighbours(isWall, { x: 0, y: 0 })}/8.`;
  return [enemyObs, me, around];
}

// 바탕 요청 읽기 (input 줄들과 실제 후보 5개)
export function loadBase() {
  const raw = fs.readFileSync(path.join(HERE, 'base-claude-773.input.log'), 'utf8').trim();
  const inp = raw.match(/input=\[(.*)\] options\(\d+\):/)[1];
  const lines = inp.split(' ⏎ ');
  const optsPart = raw.split(/\] options\(\d+\):/)[1];
  const opts = optsPart.split(' || ').filter((s) => s.trim()).map((s) => {
    const m = s.trim().match(/^(\S+) \[(\S+) ([^\]]*)\] (.*)$/);
    return { value: m[1], kind: m[2], key: m[3], desc: m[4] };
  });
  return { lines, opts };
}

export function buildInput(sc, withKnowledge) {
  const { lines } = loadBase();
  const out = [];
  for (const l of lines) {
    if (l.startsWith('내 상태:')) out.push(l.replace(/체력 \d+\/100/, `체력 ${sc.hp}/100`));
    else if (l.startsWith('가방:')) out.push(l.replace(/\.$/, ', Mushroom 2.'));
    else if (l.startsWith('지금 하는 일:')) out.push('지금 하는 일: 없음(서 있음).');
    else if (l.startsWith('지금 동작:') || l.startsWith('최근 10초 동안')) continue;
    else if (l.startsWith('화면에 보이는 다른 플레이어:')) out.push('화면에 보이는 다른 플레이어: 없음. 다른 플레이어는 같은 편이라 공격 대상이 아니다.');
    else if (l.startsWith('전투 상황:')) {
      const d0 = dist({ x: 0, y: 0 }, sc.enemy);
      out.push(`전투 상황: 보이는 적 1, 가장 가까운 적 MushroomBrute ${r1(d0)}칸, 최근 5초 내 체력 변화 ${sc.hpChange}, 이번 접속 쓰러짐 2번. 프로그램은 스스로 피하거나 물러나지 않는다(고른 행동만 함).`);
      out.push(...enemyLines(sc));
      if (withKnowledge) out.push(KNOWLEDGE_LINE);
    } else if (l.startsWith('지금 다시 판단하는 이유:')) out.push(`지금 다시 판단하는 이유: ${sc.trigger}.`);
    else out.push(l);
  }
  return out.join('\n');
}

// AIDecisions.BuildJson 과 같은 모양
export const INSTRUCTIONS = '목표와 지금 상황에서 Claude가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).';
export function buildBody(model, input, cands) {
  return {
    model,
    input,
    questions: [{ type: 'choice', name: 'action', instructions: INSTRUCTIONS,
      choices: cands.map((c) => ({ value: c.value, description: c.desc })) }],
  };
}

export function allVariants(model) {
  const v = [];
  for (const sc of SCENARIOS) for (const know of [false, true]) for (const set of ['14', '20']) {
    const cands = buildCandidates(sc, set);
    const input = buildInput(sc, know);
    v.push({ id: `${sc.id}-${know ? 'K' : 'N'}-${set}`, scenario: sc.id, know, set, cands, input, body: buildBody(model, input, cands) });
  }
  // 바탕 요청 그대로(지연·길이 비교용)
  const { lines, opts } = loadBase();
  const input = lines.join('\n');
  v.push({ id: 'BASE-773', scenario: 'BASE', know: false, set: 'base', cands: opts.map((o) => ({ value: o.value, key: o.kind, desc: o.desc })), input, body: buildBody(model, input, opts.map((o) => ({ value: o.value, desc: o.desc }))) });
  return v;
}

// ---- 흔들림 시험(2차): 같은 입력이면 응답이 완전히 같아서(1차 결과), 결과와 상관없는 작은 변화를 준다.
//  swap:   왼쪽/오른쪽 짝 후보의 순서만 바꿈(내용 같음)
//  numeric: 적 위치 0.1칸 멀리(사거리 판정 안 바뀜), 적 체력 260→247
//  shuffle: 후보 전체 순서를 고정 시드로 섞음
function mulberry32(a) { return () => { a |= 0; a = (a + 0x6D2B79F5) | 0; let t = Math.imul(a ^ (a >>> 15), 1 | a); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
const PAIRS = [['m1_left', 'm1_right'], ['m1_backleft', 'm1_backright'], ['m1_frontleft', 'm1_frontright'], ['m3_backleft', 'm3_backright'], ['m3_left', 'm3_right']];
export function perturbVariants(model) {
  const v = [];
  for (const sc0 of SCENARIOS) for (const know of [false, true]) for (const set of ['14', '20']) for (const kind of ['swap', 'numeric', 'shuffle']) {
    let sc = sc0;
    if (kind === 'numeric') sc = { ...sc0, enemy: { x: sc0.enemy.x + Math.sign(sc0.enemy.x) * 0.1, y: sc0.enemy.y }, ehp: '247/540' };
    let cands = buildCandidates(sc, set);
    if (kind === 'swap') {
      const idx = (k) => cands.findIndex((c) => c.value === k);
      for (const [a, b] of PAIRS) { const i = idx(a), j = idx(b); if (i >= 0 && j >= 0) [cands[i], cands[j]] = [cands[j], cands[i]]; }
    }
    if (kind === 'shuffle') {
      const rnd = mulberry32(20261105 + sc0.id.charCodeAt(1) * 7 + (know ? 1 : 0) + (set === '20' ? 3 : 0));
      cands = [...cands]; for (let i = cands.length - 1; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [cands[i], cands[j]] = [cands[j], cands[i]]; }
    }
    let input = buildInput(sc, know);
    if (sc.ehp) input = input.split(ENEMY_HP).join(sc.ehp);
    v.push({ id: `${sc0.id}-${know ? 'K' : 'N'}-${set}-${kind}`, scenario: sc0.id, know, set, perturb: kind, cands, input, body: buildBody(model, input, cands) });
  }
  return v;
}
