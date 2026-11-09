# 코드 검수 — 근접전 계속하기 경고 문구 (268e676, 부모 0869165)

- worktree: `C:\Dev\CoreKeeperModSDK-contwarn`, 브랜치 cont-warning
- 범위: `AIBrain.Candidates.cs`, 새 `Core/AIContinueText.cs`(+.meta), `Tools/core-tests/ContinueWarningTests.cs`, `Program.cs`
- 코드는 고치지 않았다.

## 확인한 것

### 1. 같은 문장 보장 — 문제 없음
- 비근접전 계속하기(`Continue`, Candidates.cs:133): 부모의 인라인 `{(cur.Tag.StartsWith("explore:") ? $"({cur.Tag.Substring(8)}쪽 탐험 걷기)" : "")}`를 같은 식으로 만든 `cExplore`(:131)로 바꾸기만 했다. 나머지 글자는 diff상 같다. 출력은 같다.
- 경고가 없는 근접전 계속하기:
  - 남은 시간 문장(:954 → AIContinueText.cs:22): 부모의 `$" 남은 시간: 이 행동은 약 {rem:F2}초 뒤 끝남(이 판단의 답은 약 {ans:F2}초 뒤 옴){("")}."`와 글자가 같다. `{x:F2}`와 `x.ToString("F2")`는 둘 다 현재 문화권을 써서 결과가 같다.
  - 진행 문장(:960): 부모 `초). 계속하면: {Exit}`이고, 새것은 `초).` + `" 계속하면: "` + `{Exit}`라서 같다.
  - 남은 시간을 모를 때(rem = -1, 예외 포함): `EndsFirst`가 false이고 `rem >= 0f`도 false라서, 부모처럼 아무것도 붙지 않는다. rem = ans이면 부모처럼 경고가 없다(`<`).
- 다른 후보와 상황 문장은 diff에서 바뀌지 않았다. 다만 :1063 '진행 중 이동' 상황 문장의 "계속하면"은 별개 문자열이고 그대로다.

### 2. 새 상태 변수(cSince·cProg·cMotion·cExplore) — 문제 없음
- `CandBuild`는 판단마다 새로 만든다(`BuildCandidates`, AIBrain.Loop.cs:1186). 연장 재빌드(Candidates.cs:1138)도 `BuildCandidates`를 다시 불러 새 객체를 만든다. 그래서 이전 판단의 값이 남지 않는다.
- 호출 순서는 `Prep → Continue → Enemies → CombatOptions → Brute`(Loop.cs:1187-1191)다. 네 값은 `Continue`에서 `lCont.Add`(:133) 바로 앞(:131)에 함께 채워진다.
- `Brute`의 `cont`는 `lCont.Count == 1`일 때만 `lCont[0]`이다(:884). `lCont`에 넣는 곳은 :133 하나뿐이다(grep으로 확인). 그래서 `cont != null`이면 네 값은 이번 판단에서 채운 값이다. 미리 가져오기(prefetch)일 때는 `lCont`가 비어 `cont`가 null이다. 그래서 초기값 ""가 문장에 들어가는 경로는 없다.
- `cur`·`curKey`는 `Prep`에서 한 번 정해지고 바뀌지 않는다. 그래서 `EndsFirstHead(cSince, cur.Name, curKey, …)`에 들어가는 이름·키는 :133의 값과 같다.

### 3. endsFirst에서 desc를 통째로 바꿀 때 — 문제 없음
- :133에서 :953 사이에 `lCont[0].desc`를 고치는 곳이 없다. `Enemies`·`CombatOptions`는 `lCont`를 건드리지 않는다. `Brute`의 `Put`(:889)과 :943은 목록에 넣기만 한다. 그래서 통째로 바꿔도 사라지는 덧붙임은 없다. 사라지는 것은 의도한 '이어 하기'와 '바꾸는 비용'뿐이다.
- 순서: 머리 문장(경고 → 하던 행동 → prog → motion·DoorStepNote) → `StepSkill`이면 " 진행: …" + " 이 행동이 끝날 때(이 답이 오기 전): " + 예상 → (조립 뒤) 문 열어 둠 덧붙임(:1149). 덧붙임은 끝에 붙고, 부모와 순서가 같다. `cont.pred`도 그대로 정해진다.
- `HitOnceSkill`의 "한 번 더 치기"(:963-969)는 별도 후보라서 영향이 없다.

### 4. 로그·채팅·시험·위쪽 LLM — 깨지는 곳 없음, 보이는 정보는 줄어듦(지적 S1)
- desc의 문구로 판정하는 곳은 `"기지 문"`·`"기지 안 횃불"`·`"기지 문 다시 달기"`·`"목표 계속하기"`의 StartsWith/Contains(AIEdge.cs:652-660, AICandidateAssembler.cs:241, GuardTests.cs:236)뿐이다. 계속하기 문구에 기대는 곳은 없다. 계속하기는 `kind == "continue"`로 가린다(Loop.cs:427 등).
- Tools 아래 .mjs·.py·.sh에도 "이어 하기"·"바꾸는 비용"·"계속하기 —"·"남은 시간:"으로 판정하는 곳이 없다.
- 위쪽 LLM에는 desc 원문이 그대로 간다(Loop.cs:1382 description). 이것이 의도한 변경이다.
- 자른 desc를 쓰는 곳에서는 새 문장 앞부분(약 190자)이 경고문이다. 그래서 하던 행동 이름·키가 잘려 나간다. 해당하는 곳은 SEND 로그 `Trim(desc,120)`(Loop.cs:786), RESULT 로그 80자(:856), "AI 판단:" 채팅 80자(:950), 쓰러짐 기록 'last choices' 50자(:949 → AICombat.cs:382)다. 동작은 깨지지 않는다. → S1

### 5. 시험
- `bash Tools/core-tests/gates.sh`(Unity 관문 없이) 결과: compile-check 오류 0, core-tests ALL CHECKS OK (790), exp-sim·recall-loop·bridge-wiring·protocol·fake-bridge·logshape·movecheck 모두 OK, memgraph는 KG 자료가 없어 건너뜀. failed gates: 0.
- `dotnet run -c Release --project Tools/core-tests -- CW`: 6/6 OK.
- CW 그룹이 확인하는 것:
  - 경계값: -1, 같음, 큼, 작음
  - 옛 남은 시간 문장이 글자까지 같은지
  - 오프라인에서 잰 V9 전문(E232)과 같은지. 측정과 코드가 어긋나는 것을 막으므로 의미가 있다.
  - prog가 없고 motion이 있는 경우
  - 두 예상 이름표
  - Candidates.cs가 새 함수를 쓰는지(원문 검사)
- 빈 곳: 비근접전 계속하기 문장(:133)이 그대로인지 지키는 시험은 없다. 이번에는 diff로 같음을 확인했다. → S2

## 지적

### S1. 자른 로그·채팅·쓰러짐 기록에서 계속하기의 행동 이름이 사라짐 — 제안
- 근거:
  - AIContinueText.cs:28-31: 행동 이름이 약 190자 뒤에 온다.
  - AIBrain.Loop.cs:786: SEND `Trim(c.desc, 120)`
  - AIBrain.Loop.cs:856: RESULT 80자
  - AIBrain.Loop.cs:949-950: NoteChoice 50자, "AI 판단:" 80자
  - AICombat.cs:382: DOWN 'last choices'
- 영향: 경고가 붙은 계속하기는 모두 "계속하기 — 답이 오기 전에 끝남: 이 행동은 약 …"로만 보인다. 어떤 짧은 행동이었는지는 FULL 로그나 running 정보를 따로 봐야 한다. 판단이나 동작에는 영향이 없다.
- 고칠 방향: 모델에 보내는 문장(측정한 V9)은 그대로 둔다. 자르는 쪽에서 `kind == "continue"`이면 key를 앞에 붙인다. 예: `Trim(c.kind == "continue" ? $"[계속 {c.key}] {c.desc}" : c.desc, n)`. 이렇게 하면 보이는 정보를 되살릴 수 있다. 이번 커밋을 막을 일은 아니다.

### S2. 비근접전·경고 없는 계속하기의 머리 문장을 지키는 시험이 없음 — 제안
- 근거: AIBrain.Candidates.cs:133은 인라인 문자열이고, ContinueWarningTests.cs:26-29는 새 호출이 있는지만 본다.
- 고칠 방향: 원문 검사에 :133의 고정 부분(`이어 하기{(string.IsNullOrEmpty(prog) ? "" : " (" + prog + ")")}.{motion} 바꾸는 비용: …`)이 그대로 있는지 확인하는 줄을 하나 더한다. 아니면 머리 문장도 `AIContinueText`의 함수로 옮겨 값으로 고정한다. 이번 변경에서 글자가 같은 것은 diff로 확인했다.

### S3. 지시문 인용이 원문과 글자가 다름 — 제안(바꾸지 않아도 됨)
- 근거:
  - AIContinueText.cs:30은 '지금 하는 행동이 여전히 알맞으면 이어 간다'로 적는다.
  - 실제 지시문(AIBrain.Loop.cs:1377)은 "지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고"다.
- 영향: 뜻은 같다. 측정한 문장(V9)을 그대로 고정한 것이므로 지금 바꾸면 측정이 무효가 된다. 나중에 지시문을 고칠 때 이 인용도 함께 맞춰야 한다는 점만 적어 둔다. 예: AIContinueText.cs 주석에 지시문 위치를 적는다.

## 종합 판정
**합쳐도 됨.** 경고가 없는 경우의 문장은 부모와 글자까지 같다. 새 상태 변수에 이전 판단의 값이나 빈 값이 들어가는 경로가 없다. 통째로 바꿔도 잃는 덧붙임이 없다. 문구에 기대는 소비처도 깨지지 않는다. 오프라인 관문은 모두 통과했다(core-tests 790, CW 6). 지적 세 개는 모두 '제안'이다.
