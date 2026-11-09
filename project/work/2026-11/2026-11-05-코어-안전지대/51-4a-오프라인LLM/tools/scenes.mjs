// Builds the offline real-LLM scenes (51): the geo of the 41 fixtures (and the scratch geo-out for the built states),
// reports in the shape AIPlanReport / AIEdge.ReportFields / AIEdgeFacts make at b016a5c. One body per scene x member.
// node scenes.mjs <outDir>
import fs from 'node:fs';
import path from 'node:path';
const FIX = 'C:/Dev/CoreKeeperModSDK-base/Tools/ai-bridge/test/fixtures/offline-llm';
const GEO = 'C:/Users/<USER>/AppData/Local/Temp/claude/c237366b8a8ea80b/c26cb1cd-599e-4e45-ab47-ce07c56a651b/scratch/geo-out';
const out = process.argv[2];
fs.mkdirSync(out, { recursive: true });
const NAME = { claude: 'Claude', gpt: 'ChatGPT', gemini: 'Gemini', grok: 'Grok' };
const geoOf = (dir, f) => JSON.parse(fs.readFileSync(path.join(dir, f + '.json'), 'utf8')).geo;
const optionsOf = (f) => JSON.parse(fs.readFileSync(path.join(FIX, f + '.json'), 'utf8')).report.base_edge.options;

function report(o) {
  const me = o.me;
  const bag = o.bag || { WallDirtBlock: 20, Wood: 12, CopperBar: 3, Torch: 2 };
  return {
    trigger: o.trigger,
    goal: o.goal || { source: '', text: '' },
    goal_now: o.goal_now || { text: '없음', by: 'none' },
    plan: null, plan_max: 5,
    host_order: o.host_order ?? null, host_orders_open: [],
    chat: o.chat || [],
    recent_fights: [], last_decision: o.last_decision ?? null,
    why_ended: o.why_ended || '',
    events: o.events || [],
    me: { hp: '100/100', hunger: 80, hand: 'CopperPickaxe', core_dist: o.core_dist ?? 5, tile: o.tile || '0,-3', bag, enemies_in_view: o.enemies_in_view ?? 0 },
    hp_note: '지금 100/100. 체력은 싸움에서 벗어나 5초 뒤부터 5초마다 최대 체력의 2%씩 저절로 참.',
    recent_downs: [],
    memory: '기억한 장소 3곳, 기억한 구리 2곳, 주석 0곳',
    can_do: {
      gather: ['WallDirtBlock', 'CopperOre', 'TinOre', 'Wood'], explore: true, follow: true, goto: true,
      craft: { '손': ['Torch: Wood ' + (bag.Wood ?? 0) + '/1'], WoodenWorkBench: ['WoodDoor: Wood ' + (bag.Wood ?? 0) + '/4·CopperBar ' + (bag.CopperBar ?? 0) + '/2', 'WoodenChest: Wood ' + (bag.Wood ?? 0) + '/8'] },
      craft_more: 0, place: ['Torch', ...(bag.WoodDoor ? ['WoodDoor'] : [])], place_more: 0, stations_in_view: ['WoodenWorkBench'],
      store: false, home: false, farm: false, cook: false, build: o.build ?? true, guard: o.guard ?? false,
    },
    base: { where: '(0,-1) 정하지 않아 기본값(코어 앞)', distance: 2 }, chests: [], bag_storable: '없음',
    base_edge: o.base_edge,
    places: ['core 코어 (0,2) 5칸', 'station:WoodenWorkBench@-2,-4 나무 작업대 2칸'], places_more: {},
    experience: { recent: [], goals: [] },
    craft_needed: '',
    cannot_yet: ['전투 자체를 목표로', '아이템 건네기'],
    host: { in_world: true, distance: 6 },
    visible_players: o.visible_players || [],
    last_answer_rejected: null,
  };
}
const items = (b, d, t) => `벽 블록 ${b ? `WallDirtBlock ${b}` : '0'}, 문 ${d}(WoodDoor: Wood 4·CopperBar 2), 횃불 ${t}(손제작 Torch: Wood 1)`;
const matesMine = (me) => `기지 B1: 구성원 ${me}, 우선권 나(다른 둘레로 바꾸는 결정만 — 혼자 다시 고르기는 안 함, 동료 제안은 proposals), 이어 짓기·지키기는 누구나`;
const matesOf = (owner) => `기지 B1: 구성원 ${owner}, 우선권 ${owner}(다른 둘레로 바꾸는 결정만) — 이와 겹치는 다른 둘레를 고르면 ${owner}에게 제안으로 감, 이어 짓기·지키기는 누구나`;
const noPlanNote = '기지 둘레를 짓지 않았음. build를 걸면 후보를 계산해 build_options로 다시 묻는다';
const planObj = (me, state, extra = {}) => ({ id: 'E1', owner: me === 'self' ? '나(우선권)' : `${me}(우선권)`, state, inside: 9, broken: '없음', self_opened: '없음', unchecked: '없음', enemies: '안쪽 0, 바깥 보이는 0', ...extra });

const SCENES = {
  's1-cave': (m, N) => {
    const opt = optionsOf('1-cave-corridor');
    return { rid: 21, rev: 3, geo: geoOf(FIX, '1-cave-corridor'), report: report({
      trigger: 'build_options', tile: '0,-3',
      goal_now: { text: '기지 둘레 짓기', by: 'me', for_s: 6, note: `둘레 후보 ${opt.list.length}개(판 ${opt.rev}) — base_edge.options에서 option으로 고른다` },
      host_order: { id: 2, text: `@${N} 코어 둘레에 기지 만들어 줘`, ago_s: 40, state: '따름' },
      chat: [{ from: '호스트', to: N, to_me: true, ago_s: 40, new: false, text: `@${N} 코어 둘레에 기지 만들어 줘` }],
      events: [`둘레 후보 ${opt.list.length}개 계산됨(판 ${opt.rev})`],
      base_edge: { plan: null, plan_note: noPlanNote, options: opt, items: items(20, 0, 2), mates: '같은 기지 (0,-1): 동료 계획 없음', map: '지도 조회 가능(q map)' },
    }) };
  },
  's2-open': (m, N) => {
    const opt = optionsOf('2-open-field');
    return { rid: 31, rev: 2, geo: geoOf(FIX, '2-open-field'), report: (() => { const r = report({
      trigger: 'build_options', tile: '0,-3',
      goal_now: { text: '기지 둘레 짓기', by: 'me', for_s: 6, note: `둘레 후보 ${opt.list.length}개(판 ${opt.rev}) — base_edge.options에서 option으로 고른다` },
      host_order: { id: 2, text: `@${N} 여기 기지 좀 막아 줘`, ago_s: 40, state: '따름' },
      chat: [{ from: '호스트', to: N, to_me: true, ago_s: 40, new: false, text: `@${N} 여기 기지 좀 막아 줘` }],
      events: [`둘레 후보 ${opt.list.length}개 계산됨(판 ${opt.rev})`],
      base_edge: { plan: null, plan_note: noPlanNote, options: opt, items: items(20, 0, 2), mates: '같은 기지 (0,0): 동료 계획 없음', map: '지도 조회 가능(q map)' },
    }); r.places = ['core 코어 (0,2) 5칸']; r.base = { where: '(0,0) 정하지 않아 기본값(코어)', distance: 3 }; r.can_do.stations_in_view = []; r.can_do.craft = { '손': ['Torch: Wood 12/1'] }; return r; })() };
  },
  's3-statue': (m, N) => {
    const opt = optionsOf('3-statue-outside');
    return { rid: 41, rev: 4, geo: geoOf(FIX, '3-statue-outside'), report: (() => { const r = report({
      trigger: 'build_options', tile: '0,-3',
      goal_now: { text: '기지 둘레 짓기', by: 'me', for_s: 6, note: `둘레 후보 ${opt.list.length}개(판 ${opt.rev}) — base_edge.options에서 option으로 고른다` },
      host_order: { id: 2, text: `@${N} 코어 둘레 막아 줘`, ago_s: 40, state: '따름' },
      chat: [{ from: '호스트', to: N, to_me: true, ago_s: 40, new: false, text: `@${N} 코어 둘레 막아 줘` }],
      events: [`둘레 후보 ${opt.list.length}개 계산됨(판 ${opt.rev})`],
      base_edge: { plan: null, plan_note: noPlanNote, options: opt, items: items(20, 0, 2), mates: '같은 기지 (0,-1): 동료 계획 없음', map: '지도 조회 가능(q map)' },
    }); r.places.push('statue:SlimeBossStatue@1,8 석상 11칸(140초 전에 봄)'); return r; })() };
  },
  's4-proposal-in': (m, N) => {
    const P = N === 'Gemini' ? 'Claude' : 'Gemini';
    const geo = geoOf(FIX, '4-proposal-to-priority');
    for (const p of geo.proposals) p.label = p.label.replace('Gemini', P).replace('rev 7', '판 7');
    return { rid: 51, rev: 6, geo, report: report({
      trigger: 'edge_proposal', tile: '0,-3', build: '이어 짓기', guard: true, bag: { WallDirtBlock: 20, Wood: 8, CopperBar: 1, Torch: 2, WoodDoor: 1 },
      goal_now: { text: '기지 둘레 짓기', by: 'me', for_s: 60 },
      chat: [{ from: P, to: N, to_me: false, ago_s: 10, new: true, text: `[${P}] ${N}, 석상까지 감싸는 안을 보냈어요` }],
      events: [`동료 ${P}가 기지 둘레 다른 안 #3(${P}의 A1)을 제안함 — edge_proposal로 accept/decline`],
      visible_players: [{ name: P, role: 'ai_teammate', distance: 4, direction: '서', hp: '100/100', doing: '기지 둘레 짓기(제안 기다림)' }],
      base_edge: {
        plan: planObj('self', '짓는 중: 틈 1곳 중 1곳 열림(남은 칸 8), 문 자리 (0,-3) 안 달림, 횃불 0'),
        proposals: [{ id: 3, from: P, ago_s: 10, option: `${P}의 A1(판 7)`, facts: '틈 1곳, 막을 칸 1칸(가장 긴 틈 1칸), 블록 1개 필요, 가진 블록 20개, 둘러싸이는 넓이 120칸, 안쪽에 놓을 수 있는 칸 111칸, 틈 1의 문 자리 (-5,2): 문을 달려면 양옆에 벽 2칸이 더 필요, 막을 칸 1칸 너머는 본 적 있는 바깥 공간, 지금 계획과: 이미 놓은 벽 없음, 내 지형에서 닫힘', why: '석상까지 품자 / say: 석상까지 감싸는 안을 보냈어요' }],
        items: items(20, 1, 2), mates: matesMine(N), map: '지도 조회 가능(q map)',
      },
    }) };
  },
  's5-declined': (m, N) => {
    const O = N === 'Claude' ? 'Gemini' : 'Claude';
    const geo = geoOf(FIX, '5-proposal-result-declined');
    const res = `${O}가 내 안 A1을 거절함: 지금 둘레를 이미 짓고 있어 바꾸지 않음`;
    return { rid: 61, rev: 3, geo, report: report({
      trigger: 'proposal_result', tile: '-3,-1', build: '이어 짓기', guard: true, bag: { WallDirtBlock: 6, Wood: 2, Torch: 0 },
      goal: { source: `llm:${m}#60`, text: '기지 둘레 짓기' }, why_ended: res,
      goal_now: { text: '없음', by: 'none' },
      host_order: { id: 4, text: `@${N} 석상까지 넣어서 기지 지어`, ago_s: 200, state: '막힘', note: `기지 둘레 짓기: ${res}` },
      chat: [{ from: '호스트', to: N, to_me: true, ago_s: 200, new: false, text: `@${N} 석상까지 넣어서 기지 지어` }, { from: O, to: N, to_me: false, ago_s: 2, new: true, text: `[${O}] 지금 안으로 계속 지을게요` }],
      events: [`호스트 지시 #4을 막힘(기지 둘레 짓기: ${res})`],
      visible_players: [{ name: O, role: 'ai_teammate', distance: 5, direction: '동', hp: '100/100', doing: '기지 둘레 짓기' }],
      base_edge: { plan: planObj(O, '짓는 중: 틈 1곳 중 1곳 열림(남은 칸 8), 문 자리 (0,-3) 안 달림, 횃불 0'), items: '벽 블록 WallDirtBlock 6, 문 0(WoodDoor: Wood 4·CopperBar 2), 횃불 0(손제작 Torch: Wood 1)', mates: matesOf(O), map: '지도 조회 가능(q map)' },
    }) };
  },
  's6-paused': (m, N) => {
    const why = '문 없음 — WoodDoor(Wood 4·CopperBar 2) 필요, 짓기는 잠시 멈춤: 문을 마련할 단계를 주면 그 뒤 이어 짓는다';
    return { rid: 71, rev: 5, geo: geoOf(GEO, '8-paused-walls'), report: report({
      trigger: 'build_paused', tile: '0,-1', core_dist: 3, build: '이어 짓기', guard: true, bag: { WallDirtBlock: 4, Wood: 12, CopperBar: 3, Torch: 2 },
      goal_now: { text: '기지 둘레 짓기', by: 'me', for_s: 300, note: why },
      events: ['블록 모자람: 둘레 아닌 벽을 캐 모음(5개)', `기지 둘레 짓기 잠시 멈춤 — ${why}`],
      base_edge: { plan: planObj('self', '짓는 중: 틈 1곳 중 0곳 열림(남은 칸 0), 문 자리 (0,-3) 안 달림, 횃불 0'), items: items(4, 0, 2), mates: matesMine(N), map: '지도 조회 가능(q map)' },
    }) };
  },
  's7-done': (m, N) => {
    const note = '기지 둘레 다 막음(E1, 벽 8·문 (0,-3)), 횃불 못 놓음 — 안쪽에 놓을 자리 없음';
    return { rid: 81, rev: 6, geo: geoOf(GEO, '9-done-walls'), report: report({
      trigger: 'goal_done', tile: '0,-1', core_dist: 3, build: '이미 다 지음', guard: true, bag: { WallDirtBlock: 3, Wood: 8, CopperBar: 1, Torch: 2 },
      goal: { source: `llm:${m}#78`, text: '기지 둘레 짓기' }, why_ended: note,
      host_order: { id: 2, text: `@${N} 코어 둘레에 기지 만들어 줘`, ago_s: 900, state: '끝남', note: `기지 둘레 짓기: ${note}` },
      events: [],
      base_edge: { plan: planObj('self', '닫힘, 문 (0,-3) 달림, 횃불 0'), items: items(3, 0, 2), mates: matesMine(N), map: '지도 조회 가능(q map)' },
    }) };
  },
  's8-breach': (m, N) => ({ rid: 91, rev: 7, geo: geoOf(GEO, '10-breach'), report: report({
    trigger: 'base_breach', tile: '-1,-4', core_dist: 6, build: '이어 짓기', guard: true, bag: { WallDirtBlock: 3, Wood: 8, CopperBar: 1, Torch: 2 }, enemies_in_view: 1,
    goal_now: { text: '나무 모으기 Wood 10', by: 'me', for_s: 40 },
    events: ['기지 둘레 부서짐 1곳 (2,0)(보임)'],
    base_edge: { plan: planObj('self', '짓는 중: 틈 1곳 중 1곳 열림(남은 칸 1), 문 자리 (0,-3) 달림, 횃불 0', { broken: '(2,0) 놓았던 벽이 없어짐', enemies: '안쪽 0, 바깥 보이는 1(Slime (5,0) 6칸)' }), items: items(3, 0, 2), mates: matesMine(N), map: '지도 조회 가능(q map)' },
  }) }),
};

let n = 0;
for (const [sid, make] of Object.entries(SCENES)) {
  for (const m of Object.keys(NAME)) {
    const s = make(m, NAME[m]);
    const world = sid.replace(/\D/g, '').padStart(2, '0') + String(Object.keys(NAME).indexOf(m)).padStart(2, '0') + '0'.repeat(32);
    const geo = { ...s.geo, world: world.slice(0, 32), character: m, report_id: s.rid };
    fs.writeFileSync(path.join(out, `${sid}.${m}.json`), JSON.stringify({ scene: sid, member: m, world: world.slice(0, 32), report_id: s.rid, goal_rev: s.rev, report: s.report, geo }, null, 1));
    n++;
  }
}
console.log(`${n} bodies in ${out}`);
