# Paraphrasing Decisions-prompt templates in Korean (control condition KO')

Context: an AI character in the game Core Keeper (2D top-down mining/survival) asks a decision model to pick its next
action. The program writes the situation and one description per choice in Korean. In an experiment the same requests
were sent in the Korean original (KO) and in an English translation (EN), and the model's choice often changed. To tell
"English vs Korean" apart from "any rewording", we need a **Korean rewording (KO')** of every template that differs
from the original about as much as the English translation does in structure and wording — but stays Korean and keeps
the meaning exactly.

## Input / output
- Each chunk file is a JSON array of `{i, key, slots, example, context?, english}`.
  - `key`: the Korean original template. Each `{}` is a slot (number, coordinate, ASCII id, or a direction word such as
    `동`, `(남)` — keep direction slots as they are; `{}쪽` means a direction + 쪽).
  - `example`: one real text with the slots filled. `context`: present when the key is a comma/semicolon fragment of the
    longer sentence shown; reword only the fragment so that fragments joined with the same separator still read as one
    Korean sentence (a continuing fragment must still connect grammatically: keep a connective ending such as -고, -며,
    -지만 when the original fragment ends in one, and keep a final period only if the key has one).
  - `english`: the English translation used in the experiment (slots `{0}`, `{1}`… = the n-th `{}` of the key).
- Output: a JSON object `{"<i>": "<Korean rewording>"}` for **every** `i`, slots written `{0}`, `{1}`, … (n-th `{}` of the
  key), each used exactly once, order free.
- Run `node check.js <chunk.json> <out.json>` (in this folder) until it prints `OK`. It also lists entries left unchanged;
  leave an entry unchanged only when it is a bare name or label with no natural Korean alternative.

## How to reword
- Keep every fact, number slot, condition, hedge (약, 추정, 모름, 확인 못 함) and the strength of every statement. Do not add
  or remove emphasis, warnings, advice or explanation. Do not drop parenthetical notes.
- Change the surface about as much as the English did: word order (move clauses, put the condition first or last),
  vocabulary (synonyms: 칸→타일 is NOT allowed — keep game units 칸/초/번/개; but e.g. 고르다↔선택하다, 이어 하다↔계속 진행하다,
  멈춤↔중단, 다가가다↔접근하다, 보이는↔눈에 띄는 …), sentence splitting/merging, noun-phrase ↔ clause style
  (e.g. "공격 안 함" ↔ "공격하지 않음"), label wording ("내 상태:" ↔ "나의 현재 상태:"). Use the English as a guide to
  where the structure was changed (reordered slots, split sentences), but do not just back-translate word for word, and
  do not copy the original.
- Action names (used as option heads and skill names) get one consistent reworded form each. Use these:
  계속하기→계속 진행 · 기다리기→대기 · 제자리 유지→자리 지키기 · 짧은 이동→짧게 이동 · 긴 이동→길게 이동 ·
  한 번 치기→한 번 휘두르기 · 한 번 더 치기→한 번 더 휘두르기 · 사거리까지 접근→공격 거리로 접근 · 복합→묶음 행동 ·
  복합 행동→묶음 행동 · 거리 벌리기→간격 두기 · 물러나기→후퇴 · 공격→공격하기 · 공격(계속)→공격하기(지속) · 먹기→먹기(섭취) ·
  캐기→채굴 · 파들어가기 / 파 들어가기→뚫고 가기 · 걷기→걸어가기 · 따라가기→뒤따르기 · 만들기→제작 · 놓기→설치 ·
  부수기→파괴 · 뿌리 캐기→뿌리 채굴 · 줍기→주워 오기 · 탐험→탐사 · 탐험 이동→탐사 이동 · 상자에 넣기→상자 보관 ·
  상자에서 꺼내기→상자에서 빼기 · 밭 일구기→밭 갈기 · 씨앗 심기→파종 · 물 주기→물 뿌리기 · 물 채우기→물 담기 ·
  거두기→수확 · 밭 일→밭일 · 요리하기→조리 · 요리 꺼내기→음식 빼기 · 요리 기다리기→조리 대기 · 보기→살펴보기 ·
  문 닫기→문 잠그기는 안 됨(뜻이 바뀜) → 문 닫아 두기 · 둘레 메우기→둘레 막기.
- Other recurring terms, keep consistent: 버섯 야수 (keep), 돌진 (keep), 취약→무방비는 안 됨; keep 취약 but you may say
  "취약 상태". 기지/둘레/코어 keep. 프로그램 keep. 판단→결정 is allowed (do it consistently: 판단→결정, 다시 판단→재결정).
- The warning in the continue option ("답이 오기 전에 끝남: 이 '계속하기'를 고르면 그 답은 쓰이지 않고 다음 답까지 서
  있음") must be reworded like any other sentence — same facts, same strength, no extra emphasis.
