# 51 — 단계 4a 오프라인 실제 LLM 확인 (설계 36 7절)

2026-11-06. 기준 worktree `C:\Dev\CoreKeeperModSDK-base`, `base-4a` `b016a5c`. 코드 고침·게임·공통 설치·push 없음.
**중간에 멈춤**: HEAD 지시(사용량 부족)로 새 호출을 멈추고, 그때까지 받은 결과만 정리했다. 못 한 것은 '보류'로 적었다.

## 방법

- 브리지는 실제 것을 그대로 썼다: `bridge.mjs`를 따로 띄움(포트 8397, 설정·로그는 이 폴더). 길은 replay.mjs `run`과 같다. 요청은 plan 요청 → preflight(로그인·한도) → recall 루프 → `protocol.shape` 검사 → 봉투. 게임 그래프는 `C:/Dev/CoreKeeperModSDK/Tools/knowledge/data`. 구독 CLI만 썼고 API 키는 지웠다.
- 자료: [51-4a-오프라인LLM/scenes/](51-4a-오프라인LLM/scenes/) 8장면 × 멤버(이름은 멤버마다 바꿈). 장면마다 world id를 따로 줘서 prefetch가 다른 장면으로 넘어가지 않게 했다.
  - geo: s1~s5는 41 몫 2의 fixture geo를 그대로 썼다. s6~s8은 실제 코드(Options·Plan·GeoJson)로 새로 만들었다. 지금 격자에 놓은 벽·문을 그렸다. 시험 프로젝트를 scratch에 복사해 `OfflineLlm.cs`에 장면만 더했고, 저장소 코드는 그대로다([tools/OfflineLlm-scratch.cs](51-4a-오프라인LLM/tools/OfflineLlm-scratch.cs), 결과는 [geo/](51-4a-오프라인LLM/geo/)).
  - report: `AIPlanReport.ReportJson`, `AIEdge.ReportFields`, `AIEdgeFacts`, `AIHostOrders`의 실제 꼴로 다시 썼다. 바뀐 것은 다음과 같다.
    - 키: `hand`, bag 객체, `plan_note`, `goal{source,text}`, host_order 상태.
    - 계획 상태: `PlanStateText`.
    - 동료·제안 문장: mates(`Mates`), proposals(`ProposalsJson`+`ProposalCompare`).
    - proposal_result: 실제처럼 `why_ended`와 host_order `막힘` 사건으로 실었다(아래 '자료 문제' 1 참고).
  - 장면: s1 동굴 통로, s2 트인 곳, s3 석상 밖, s4 제안 받은 우선권 AI, s5 호스트가 시킨 짓기의 제안이 거절된 나중 AI(X2), s6 build_paused(문 없음), s7 goal_done(횃불 못 놓음), s8 base_breach(다른 목표 중).
- 도구: [tools/scenes.mjs](51-4a-오프라인LLM/tools/scenes.mjs)(자료), [run.mjs](51-4a-오프라인LLM/tools/run.mjs)(브리지 띄우고 보냄), [analyze.mjs](51-4a-오프라인LLM/tools/analyze.mjs).
- 결과 원본: `results-r0/r1.jsonl`, `table.jsonl`, `logs/plan.jsonl`(호출별 ms·토큰), `logs/recall.jsonl`(질의와 결과 전문).

## 멤버 상태

| 멤버 | 모델 | 한도(시작 때) | 실행 |
|---|---|---|---|
| Claude | claude-opus-5-5 low | 주간 **98%** → 브리지가 `limit`으로 거절(85% 넘음) | **보류**(호출 0) |
| ChatGPT | gpt-6.1-sol low | 주간 13% | 8장면 + s1 한 번 더 |
| Gemini | gemini-3.8-flash-low | 5h 0%, 주간 14.6% | 8장면 + s1 한 번 더 |
| Grok | grok-4.7 low | 주간 1% | s1~s6(+ s1 한 번 더). **s7·s8 보류**(s7은 날아가던 중 멈춤 — 호출 1번 버림) |

두 번째 돌림(멤버마다 1~2번)은 s1 하나만 했고, 나머지는 **보류**다.

## 결과 표

답 꼴: 받은 답 21개 모두 `shape` 통과(parse 0, 다시 묻기 0). option·door id는 모두 그 보고의 후보이고 틈 수 안이다(E4·E3·E2는 틈 1곳 → door 1). edge_proposal은 `keep` + `{id:3, answer, why}`로 맞았다. 쓰인 일이 없는 것: build_after(필요한 장면 없음), hold(품을 것을 시키는 장면 없음 — 보류).

| 장면 | ChatGPT | Gemini | Grok | 맞는 답 |
|---|---|---|---|---|
| s1 동굴 | E4 door1. r0는 craft 문 → build, r1은 build만(문 없음) | craft 문 → E4 (두 번 같음) | craft 문 → E4 (두 번). map plan E1·E4·E2·objects·rule 문, 읽기 맞음("서쪽 틈", "작업대 안") | E4 ✔ (셋 다) |
| s2 트인 곳 | **E3**(20개 모자람), 모을 단계 없음 | craft 문 → E2(1개 모자람이라고 말함) | E2, 문 단계 없음 | E2 + 블록 모으기. 아무도 모으기를 넣지 않음, 문 양옆 2칸까지 치면 3개 모자람 |
| s3 석상 | E4, 문 단계 없음, "석상까지 품는" ✔ | craft 문 → E4 | craft 문 → E4 | E4 ✔ |
| s4 제안 받음 | accept ✔ "벽 0개, 더 넓게" | accept ✔ "블록 1개, 120칸" | **decline** "문 자리 양옆 벽 2칸이 더 있어야" — 사실 오독 | accept |
| s5 거절 결과(X2) | say로 알림 ✔, order defer, [craft Torch → build] | say ✔, defer, build(이어 짓기) | say ✔, defer, build + problem(불필요) | say로 알림 + 이어 짓기 ✔ (셋 다) |
| s6 build_paused | craft WoodDoor 1 ✔, say | craft WoodDoor 1 ✔, say | craft WoodDoor 1 ✔, say | craft만(저절로 이어 짓기) ✔ |
| s7 완성(L5) | say "다 막음, 횃불 자리 없음" ✔ → follow | say "다 막음" ✔(횃불 말 없음) → 상자 만들어 놓기 | 보류 | say ✔ |
| s8 부서짐 | say ✔, **build**(guard 아님), 하던 나무 모으기는 버림 | say ✔, **build**, 같음 | 보류 | say + guard(보수) 뒤 나무로 돌아가기 |

지도 조회: Grok만 썼다(s1 두 번, 한 판단에 질의 3개). 좌표 실수 0. 계획 미리보기에서 문 방향·안쪽 물체를 맞게 읽었다. ChatGPT·Gemini는 한 번도 안 썼다. 후보 문장만으로 답했고 그 답은 맞았다. brief가 "모양이 궁금할 때만"이라 이상하지는 않다.
Grok은 s3·s4에서도 찾으려 했지만 "남은 시간이 모자라" 강제 마감됐다. 첫 호출 18~23초가 버려지고 한 번 더 불렸다(calls 2, searches 0). s1의 p90(90초)이 다음 판단의 검색을 막은 것이다.

## 지연·토큰·호출 수(구독, API 비용 0)

| 멤버 | 판단 | CLI 호출 | 판단당 ms | 입력 토큰/호출 | 출력 |
|---|---|---|---|---|---|
| ChatGPT | 9 | 9 | 7.4k~13.1k (중간 9.4k) | 약 20k(캐시 9k) | 84~199 (추론 0~73) |
| Gemini | 9 | 9 | 13.4k~14.6k | 약 16.3k | 85~212 |
| Grok | 7 | 10 + 멈춘 1 | 9.3k~90.9k (중간 43k) | 약 17.5~20.8k, 2번 불리면 2배 | 544~5.6k (추론 440~5.3k) |
| Claude | 0 | 0 | — | — | — |

모두 **CLI 호출 30번**(Claude 0)이다. 한도 확인(`/usage` 등)은 모델 호출이 아니다. 실행 시간은 대략 첫 smoke 2분 + 돌림 1 약 6분이다. 프롬프트는 약 18.0~22.2k자(brief 약 15k + 보고 턴)였다.

## 멤버별 약점

- **ChatGPT**: 문이 가방에 없어도 craft 단계 없이 build option만 걸 때가 많다(s1 r1·s2·s3). 문 없음으로 멈춘 뒤 build_paused로 한 번 더 불린다. s2에서 20개 모자란 E3를 골랐고 이유("코어가 둘레에 걸리지 않음")는 맞지만 재료를 보지 않았다. s8에서 횃불을 놓겠다고 했는데, 놓을 자리는 보고에 없는 사실이다.
- **Gemini**: 빠르고 고르다. s7에서 완성 뒤 상자를 만들어 `place base`했는데, 안쪽은 모두 스폰 받침이라 상자는 둘레 밖에 놓일 것이다. 횃불을 못 놓은 사실도 말하지 않았다.
- **Grok**: 지도를 쓰고 읽기는 맞다. 제안 판단에서 "문을 달려면 양옆에 벽 2칸이 더 필요"를 '문을 못 단다'로 읽어 좋은 안을 거절했다(s4). 지연이 크고(추론 토큰이 많음) 검색이 시간 규칙에 막혀 호출을 버린다. 답에 problem을 쓸데없이 넣는다(s5).
- **공통**: 다른 목표 중 base_breach에서 guard가 아니라 build를 걸고 하던 일로 돌아가지 않는다(s8). 트인 곳에서 블록 모으기 단계를 넣지 않는다(s2).

## 자료 문제(41 fixture — 이번에는 고친 사본을 씀)

1. 보고와 geo가 어긋난다. places는 `WoodenWorkBench@3,2 … 기지 안`, 코어 (0,0), base (0,0)인데 geo는 작업대 (-2,-4)(E1 밖), 코어 (-1..1, 1..3), 기지 점 (0,-1)이다. 2번은 places에 작업대가 있지만 geo에는 없다.
2. 6·7번 geo는 짓기 전 격자(둘레 칸이 바닥)인데 보고는 "틈 다 막음"·"닫힘"이다. 6번은 bag WallDirtBlock 20과 items "벽 블록 0"이 다르다. 7번은 "횃불 1"인데 E1 안쪽은 놓을 칸이 0이라 실제로는 "횃불 못 놓음"으로 끝난다.
3. 실제 코드가 안 만드는 꼴이 섞여 있다. `base_edge.proposal_result` 키(실제로는 why_ended·host_order `막힘`·사건), `held`, bag 문자열, plan 없을 때 `plan_note` 없음, "rev 7"(실제 "판 7"), "가방 20"만 쓴 짧은 제안 사실.
→ 41 fixture를 이번 scenes로 바꾸거나 위를 맞추기를 제안한다.

## 고칠 것 제안(순위)

1. **사실 문장 — 문 양옆 벽과 블록 수**(Grok s4 오거절, s2 모자람 셈 틀림). 옵션의 "블록 N개 필요"·"M개 모자람"에 문 양옆 벽이 빠져 있다.
   - E4·A1: 틈 1칸이 바로 문 자리다. 지도 plan은 "놓을 칸 0칸"인데 문장은 "막을 칸 1칸, 블록 1개 필요"다.
   - 제안: `막을 칸 1칸(문 자리), 블록 2개 필요(문 양옆 벽 2 포함)`. 문 문장은 '못 단다'로 읽히지 않게 `문은 양옆에 벽 2칸을 더 놓고 단다(짓기가 함께 놓음)`.
2. **사실 문장 — 블록을 어디서 구하나**(s2). items나 options 끝에 `캘 수 있는 둘레 아닌 벽: 보임 N칸 / 안 보임 — 모자라면 gather WallDirtBlock 단계`. 트인 곳에서 모자란 후보를 고르면 멈출 것이 보이게 한다.
3. **brief — 문이 없을 때 build option**: 예(55행)는 option 없는 build뿐이다. `build_options에서 고를 때 문이 가방에 없으면 plan [craft WoodDoor, build option]`을 한 줄 더한다(ChatGPT).
4. **brief — base_breach**: `다른 목표 중 부서짐이면 guard(보수·적)로, 하던 일은 plan 뒤 단계로`를 더한다(s8 둘 다 build·하던 일 버림). guard·build 중 무엇이 맞는지 HEAD가 정할 일이다(설계 7절 5번은 guard → 보수).
5. **geo(실제 코드)**: 확정 계획 겹치기가 이미 놓은 벽·달린 문 위에도 `X 놓을 칸`·`G 문 자리`를 그린다(`AIBaseMap.Plan`이 gap 칸 전부를 place로 넣음). 그래서 s8의 부서진 (2,0)이 plan 보기에서 안 보인다(grid 보기에는 보임). 또 둘레 위 우리 문(D1)의 side가 "확정 둘레 바깥"으로 나온다(걸침이어야 함).
6. **브리지 — 느린 멤버의 검색 막힘**: p90이 크면(Grok 90초) 다음 판단의 recall이 "남은 시간이 모자라"로 강제 마감돼 호출이 하나 버려진다. 제안은 둘 중 하나다. 첫 턴에 이미 검색 0으로 알리기(호출 절약), 또는 map 질의(로컬, 즉시)는 이 시간 규칙에서 빼기.
7. 작은 것: brief 71행 "채팅에 뜨는 말은 네 say뿐"과 달리, 모드가 제안 결과를 `AI 둘레 제안 결과: …` 상태 줄로 이미 띄운다(`AIEdge.Owners.EndProposal`). say가 같은 말을 두 번 할 수 있다.

상한: map 결과는 3,000자 안에서 잘림 0이었다. base_edge는 손으로 만든 보고라 2,200자 상한은 여기서 보지 않았다(`BaseEdgeJson`을 거치지 않음).

## 보류(다음에 할 것)

- Claude 전 장면(주간 한도 98%).
- Grok s7·s8.
- 모든 멤버의 두 번째 돌림(s2·s4·s8처럼 갈린 장면이 먼저).
- hold 장면(호스트가 "작업대도 안에 넣어서" → hold 풀이).
- base_intruder.
- 지도 없는 보고에서 map 질의 → 다음 보고(map_asked).
