// Template-level phrasing rules. Each rule rewrites the request for every case the same way (no per-case hints).
// Choice values, count and order are never changed; facts are only moved/restated from the same request.
// apply(caseObj, ["G1", ...]) -> { input, instructions, choices }

// ---------- helpers: facts already present in the request ----------
export function goalInfo(text) {
  const line = (text.match(/^목표: (.*)$/m) || [, ""])[1];
  const goalItems = new Set(), prepItems = new Set();
  let kind = "other", m;
  if ((m = line.match(/\((\w+)\) \d+개 모으기/))) { goalItems.add(m[1]); kind = "collect"; }
  else if ((m = line.match(/\((\w+)\) \d+개 만들기/))) { kind = "craft"; }
  else if (/^(없음|탐험|호스트 따라가기|자유 시간|기지로 돌아가기)/.test(line)) kind = "noitem";
  for (const p of text.match(/^준비 길.*$/gm) || []) {
    for (const x of p.matchAll(/(\w+) \d+개 더 필요/g)) prepItems.add(x[1]);
    for (const x of p.matchAll(/(\w+) (\d+)\/(\d+)/g)) if (+x[2] < +x[3]) prepItems.add(x[1]);
  }
  for (const g of goalItems) prepItems.delete(g);
  return { line, kind, goalItems: [...goalItems], prepItems: [...prepItems] };
}

// items this candidate says it yields (only from its own description text)
export function yieldsOf(value, d) {
  let m;
  if (/^계속하기/.test(d)) {
    if ((m = d.match(/'캐기'\(mine:[^)]*\) 이어 하기 \(벽 (\w+)/))) return [m[1]];
    return null;
  }
  // (fixed after the s1~r2 rounds: "광석" was not matched, so ore walls got no tag in those rounds)
  if ((m = d.match(/부수면 ((?:광석 )?\w+(?: \+ (?:광석 )?\w+)*) 이\(가\) 나올/))) return m[1].split("+").map(s => s.trim().replace(/^광석 /, "")).filter(Boolean);
  if ((m = d.match(/^보수 재료 모으기 — .*?→ (\w+) \d+개/))) return [m[1]];
  if (/^뿌리 캐기/.test(d)) {
    if ((m = d.match(/나오는 것: 관찰 \d+번: ([^.]*)/))) return [...m[1].matchAll(/(\w+) \d+번/g)].map(x => x[1]);
    if (/나무가 나올 수 있음/.test(d)) return ["Wood"];
  }
  if ((m = d.match(/^부수기 — .*?부수면 (\w+)/))) return [m[1]];
  if ((m = d.match(/^(?:줍기|파 들어가기) — 떨어진 (\w+) x\d+/))) return [m[1]];
  if ((m = d.match(/^상자에서 꺼내기 — (\w+) \d+개/))) return [m[1]];
  if ((m = d.match(/거기서 (\w+) \d+개를 꺼낼 수 있음/))) return [m[1]];
  if ((m = d.match(/목표 광석 (?:쪽으로 — )?(\w+)/))) return [m[1]];
  return null;
}

function relevanceTag(gi, ys) {
  const hitG = ys.filter(y => gi.goalItems.includes(y)), hitP = ys.filter(y => gi.prepItems.includes(y));
  if (hitG.length) return `목표 품목 ${hitG.join("·")} 나옴`;
  if (hitP.length) return `목표에 필요한 재료 ${hitP.join("·")} 나옴`;
  const need = [...gi.goalItems, ...gi.prepItems];
  return `목표 품목·필요 재료(${need.join("·")}) 안 나옴`;
}

// split "라벨 — 나머지" at the first em dash
const head = d => { const i = d.indexOf(" — "); return i < 0 ? [d, ""] : [d.slice(0, i), d.slice(i + 3)]; };

// ---------- rules ----------
export const RULES = {
  // G1: goal-relevance tag on every candidate whose description states its yield (moved fact: goal/prep items from the
  // 목표 and 준비 길 lines of the same input). Placed right after the action label.
  G1(req, c) {
    const gi = goalInfo(req.input);
    if (!gi.goalItems.length && !gi.prepItems.length) return;
    req.choices = req.choices.map(ch => {
      const ys = yieldsOf(ch.value, ch.description); if (!ys) return ch;
      const [h, rest] = head(ch.description);
      return { ...ch, description: `${h} [${relevanceTag(gi, ys)}] — ${rest}` };
    });
  },
  // G1e: same tag but appended at the end of the description (placement variant)
  G1e(req, c) {
    const gi = goalInfo(req.input);
    if (!gi.goalItems.length && !gi.prepItems.length) return;
    req.choices = req.choices.map(ch => {
      const ys = yieldsOf(ch.value, ch.description); if (!ys) return ch;
      return { ...ch, description: `${ch.description} (${relevanceTag(gi, ys)})` };
    });
  },
  // G1s: end tag, short wording: "(목표 Wood 나옴)" / "(목표 재료 Wood 나옴)" / "(목표 Wood 안 나옴)"
  G1s(req, c) {
    const gi = goalInfo(req.input);
    if (!gi.goalItems.length && !gi.prepItems.length) return;
    req.choices = req.choices.map(ch => {
      const ys = yieldsOf(ch.value, ch.description); if (!ys) return ch;
      const hitG = ys.filter(y => gi.goalItems.includes(y)), hitP = ys.filter(y => gi.prepItems.includes(y));
      const t = hitG.length ? `목표 ${hitG.join("·")} 나옴` : hitP.length ? `목표 재료 ${hitP.join("·")} 나옴` : `목표 ${[...gi.goalItems, ...gi.prepItems].join("·")} 안 나옴`;
      return { ...ch, description: `${ch.description} (${t})` };
    });
  },
  // G1n: end tag only on candidates that do NOT yield goal/prep items (negative marking only)
  G1n(req, c) {
    const gi = goalInfo(req.input);
    if (!gi.goalItems.length && !gi.prepItems.length) return;
    req.choices = req.choices.map(ch => {
      const ys = yieldsOf(ch.value, ch.description); if (!ys) return ch;
      if (ys.some(y => gi.goalItems.includes(y) || gi.prepItems.includes(y))) return ch;
      return { ...ch, description: `${ch.description} (${relevanceTag(gi, ys)})` };
    });
  },
  // G2e: for goals with no item, yield-only actions get "(목표 일 아님)" at the end (G1e's counterpart)
  G2e(req, c) {
    const gi = goalInfo(req.input);
    if (gi.kind !== "noitem") return;
    req.choices = req.choices.map(ch => {
      if (/^계속하기/.test(ch.description)) return ch;
      if (!/^(캐기|줍기|파 들어가기 — 떨어진)/.test(ch.description)) return ch;
      if (/목표 품목|목표 일은 아님/.test(ch.description)) return ch;
      return { ...ch, description: `${ch.description} (목표 일 아님)` };
    });
  },
  // G2: for goals with no item (탐험·따라가기·기지로 돌아가기·자유 시간·없음), yield-only actions (wall mining, pickup,
  // dig-to-pickup) get "목표 일 아님" — the same phrase the game already uses on break_* candidates.
  G2(req, c) {
    const gi = goalInfo(req.input);
    if (gi.kind !== "noitem") return;
    req.choices = req.choices.map(ch => {
      if (/^계속하기/.test(ch.description)) return ch;
      if (!/^(캐기|줍기|파 들어가기 — 떨어진)/.test(ch.description)) return ch;
      if (/목표 품목|목표 일은 아님/.test(ch.description)) return ch;
      const [h, rest] = head(ch.description);
      return { ...ch, description: `${h} [목표 일 아님] — ${rest}` };
    });
  },
  // X1: exploration candidates lead with their comparison numbers (new area, time, walls, direction vs enemy),
  // then the rest in original order. Same text, reordered.
  X1(req, c) {
    req.choices = req.choices.map(ch => {
      const d = ch.description; if (!/^탐험 이동/.test(d)) return ch;
      const [h, rest] = head(d);
      const parts = [];
      let r = rest;
      const take = re => { const m = r.match(re); if (m) { parts.push(m[0].replace(/[.,]\s*$/, "").trim()); r = r.replace(m[0], ""); } };
      take(/거기 서면 새로 보일 구역 \d+칸\([^)]*\)\.?/);
      take(/예상 약 \d+초[.,]?/);
      take(/부술 벽 \d+개(\([^)]*\))?[.,]?/);
      // the direction clause stays in place (rule D1 moves it)
      r = r.replace(/\s+,/g, ",").replace(/,\s*,/g, ",").replace(/[,\s]+\./g, ".").replace(/\s{2,}/g, " ").trim();
      return { ...ch, description: `${h} — ${parts.join(", ")} · ${r}` };
    });
  },
  // D1: movement candidates put the direction-vs-enemy clause ("가는 쪽: ...") right after the label.
  D1(req, c) {
    req.choices = req.choices.map(ch => {
      const d = ch.description, m = d.match(/\s*가는 쪽: ([^.]*)\./); if (!m) return ch;
      const [h, rest] = head(d.replace(m[0], ""));
      return { ...ch, description: `${h} [가는 쪽: ${m[1]}] — ${rest.trim()}` };
    });
  },
  // I1: instruction sentence about goal relevance (template text, same for all cases)
  I1(req) {
    req.instructions += " 목표 품목이나 목표에 필요한 재료가 나오지 않는 캐기·줍기는 목표를 앞당기지 않는다.";
  },
  // I2: instruction sentence about comparing same-kind candidates by their stated numbers
  I2(req) {
    req.instructions += " 같은 종류의 후보끼리는 설명에 적힌 수치(새로 보일 구역, 걸리는 시간, 거리, 위험)를 비교해 고른다.";
  },
  // I3: instruction sentence about danger
  I3(req) {
    req.instructions += " 몇 번 맞으면 쓰러지는지와 가는 쪽이 적 쪽인지를 보고, 쓰러질 위험이 크면 적에게서 멀어지는 행동을 앞에 둔다.";
  },
  // I3b: wording variant of I3 (robustness check)
  I3b(req) {
    req.instructions += " 체력과 적을 함께 보고, 한두 번 더 맞으면 쓰러질 상황이면 적 쪽으로 가는 행동보다 적에게서 멀어지는 행동을 먼저 고려한다.";
  },
  // Q1: restate the goal line (moved fact) at the start of the instructions
  Q1(req) {
    const gi = goalInfo(req.input);
    if (gi.line) req.instructions = `목표: ${gi.line} ` + req.instructions;
  },
  // O1: move the 목표 line (and 준비 길 lines) to the end of the input, just before the choices
  O1(req) {
    const lines = req.input.split("\n");
    const keep = [], move = [];
    for (const l of lines) (/^(목표: |준비 길)/.test(l) ? move : keep).push(l);
    req.input = [...keep, ...move].join("\n");
  },
};

export function apply(c, rules) {
  const req = { input: c.text, instructions: c.instructions, choices: c.cands.map(x => ({ value: x.value, description: x.description })) };
  for (const r of rules) { if (!RULES[r]) throw new Error("unknown rule " + r); RULES[r](req, c); }
  // invariants: same values, same order, same count
  if (JSON.stringify(req.choices.map(x => x.value)) !== JSON.stringify(c.cands.map(x => x.value))) throw new Error("choice values changed " + c.id);
  return req;
}
