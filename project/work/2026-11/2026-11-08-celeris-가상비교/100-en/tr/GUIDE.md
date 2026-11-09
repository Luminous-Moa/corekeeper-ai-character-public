# Translating Decisions-prompt templates (Korean -> English)

Context: an AI character in the game Core Keeper (2D top-down mining/survival) asks a decision model to pick its next
action. The program writes a situation description and one description per choice, all in Korean. We are testing what
happens if that text were English. You translate the **format templates** the program uses, as an implementer would when
rewriting the code's string literals in English.

## Input / output
- Each chunk file is a JSON array of `{i, key, slots, example, context?}`.
  - `key`: the Korean template. Every `{}` is a slot (a number, coordinate, ASCII id such as `WoodSword`, `MushroomBrute`,
    `step:away:1`, or a direction word already turned into English such as `east`, or `(N)`).
  - `example`: one real text with the slots filled.
  - `context`: when present, the key is a comma- or semicolon-separated **fragment** of the longer sentence shown here.
    Translate only the fragment, so that `fragment1, fragment2` reads naturally when joined with the same separator: a
    fragment that continues a sentence starts lowercase and has no capital/extra period; the first fragment of a sentence
    starts with a capital. Keep a trailing period only if the key has one.
- Output: a JSON object `{"<i>": "<English template>"}` with **every** `i` of your chunk. In the English, refer to slots as
  `{0}`, `{1}`, ... in the order they appear in the Korean key (`{0}` = first `{}`); you may reorder them. Every slot must
  be used exactly once (unless a slot is a pure particle artefact — there are none; use them all).
- Run `node check.js <chunk.json> <out.json>` (in this folder) before finishing; it must print `OK`.

## Rules
- Keep every fact, number, unit, condition and hedge ("약" = about, "추정" = estimated, "모름" = unknown, "확인 못 함" = not
  confirmed). Do not add facts or advice. Do not drop parenthetical notes.
- Natural, compact English (status-report style, present tense, no articles where a label suffices). Not word-for-word.
- Keep punctuation structure: parentheses, `→`, `—`, `…`, `/`, `:`. `·` between items -> `, ` or `/` (choose `/` inside
  short lists like `벽·물체` -> `wall/object`). A trailing `…` (cut text) stays.
- Units: `{}칸` -> `{n} tiles` (1 칸 = 1 tile; use "tiles" even for 1.0), `{}초` -> `{n}s`, `{}번` -> `{n} times` or
  `x{n}` as fits, `{}개` -> just the count (`{n} blocks` if a noun is there), `{}곳` -> `{n} spots`, `{}마리` -> `{n}`
  (enemies), `{}타` -> `{n} hits`, `{}/번` -> `{n} per hit`.
- Korean particles glued to slots (`{}를`, `{}이(가)`, `{}와`, `{}의`, `(으)로`) disappear in English.
- Direction slots already English (`east`): `{}쪽 {}칸` -> `{1} tiles {0}`; `{}쪽으로` -> `toward the {0}`/`{0}ward`.

## Glossary (use consistently)
| Korean | English |
|---|---|
| 칸 | tile(s) |
| 체력 / 최대 체력 | health / max health |
| 배고픔 | hunger |
| 가방 | bag |
| 손에 든 것 / 맨손 / 손 칸 | holding / bare hands / hand slot |
| 적 / 이 종류 | enemy / this kind (of enemy) |
| 버섯 야수 | Mushroom Brute |
| 돌진 / 돌진선 / 돌진 단계 | charge / charge line / charge phase |
| 취약 | vulnerable (취약 중 = vulnerable now; 취약이 열림 = vulnerability opens) |
| 겨누다 / 겨누는 대상 | aim / aim target |
| 휘두르기 / 휘두르는 거리 | swing / swing reach |
| 근접 공격 / 타격 / 한 타 | melee attack / hit / one hit |
| 처치 | kill |
| 쓰러짐 / 쓰러지다 | down / go down |
| 피해 / 받은 피해 | damage / damage taken |
| 기지 / 둘레 / 코어 / 코어 스폰 자리 | base / perimeter / core / core spawn point |
| 상자 / 제작대 / 솥 | chest / workbench / pot |
| 밭 / 일구다 / 씨앗 / 물뿌리개 | farm / till / seed / watering can |
| 뿌리 / 큰 뿌리 | root / large root |
| 광석 / 벽 / 블록 / 문 / 횃불 / 울타리 | ore / wall / block / door / torch / fence |
| 탐색 기억 / 구역 / 탐색 끝 | exploration memory / area / exploration frontier |
| 길 / 열린 길 / 막힘 / 길찾기 | path / open path / blocked / pathfinding |
| 호스트 / 동료 / AI 동료 | host / teammate / AI teammate |
| 위쪽 LLM | upper LLM |
| 목표 / 목표 품목 / 자유 시간 | goal / goal item / free time |
| 지금 동작 / 끊기 좋은 때 | current motion / good moment to switch |
| 프로그램 | the program |
| 판단 / 다시 판단 | decision / re-decide |
| 실행 (#n) | run (#n) |
| 관찰 / 추정 / 예상 / 가정 | observed / estimated / forecast / assuming |
| 규칙 | rule |
| 까닭 | reason |
| 재료 | material(s) |
| 요리 / 접시 | dish / serving |

Action names (used as option heads and skill names; keep these exact English forms):
계속하기 Continue · 기다리기 Wait · 제자리 유지 Hold position · 짧은 이동 Short move · 긴 이동 Long move ·
한 번 치기 Single hit · 사거리까지 접근 Approach to reach · 복합 Composite · 복합 행동 Composite action ·
거리 벌리기 Keep distance · 물러나기 Retreat · 공격 Attack · 공격(계속) Attack (continuous) · 먹기 Eat · 캐기 Mine ·
파들어가기 / 파 들어가기 Dig through · 걷기 Walk · 따라가기 Follow · 만들기 Craft · 놓기 Place · 부수기 Break ·
뿌리 캐기 Dig root · 줍기 Pick up · 탐험 Explore · 탐험 이동 Explore move · 상자에 넣기 Store in chest ·
상자에서 꺼내기 Take from chest · 밭 일구기 Till · 씨앗 심기 Plant seeds · 물 주기 Water · 물 채우기 Refill water ·
거두기 Harvest · 밭 일 Farm work · 요리하기 Cook · 요리 꺼내기 Take out dish · 요리 기다리기 Wait for cooking ·
보기 Look · 문 닫기 Close door · 둘레 메우기 Fill perimeter.
Relative directions (`적 기준`): 적 쪽(다가가기) toward the enemy (approach) · 뒤(멀어지기) away (retreat) · 왼쪽 left ·
오른쪽 right · 왼쪽 앞(대각) front-left (diagonal) · 오른쪽 뒤(대각) back-right (diagonal) …
