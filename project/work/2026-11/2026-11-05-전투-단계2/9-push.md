# 9. GitHub 올리기 (전투 단계 2 + 시험 도구) — 멈춤

결과: **올리지 않음.** 검사 "실제 기록"에 한 파일이 걸렸다. 커밋·push 없음. mine/main은 그대로 1c2a61b.

## 걸린 것 (실제 기록)
- `Tools/core-tests/fixtures/asm-L1.log.gz` (gz 163,754 B, 풀면 4.8 MB / 1,728줄)
  - 리팩토링 L1 실게임 결정 로그(`runs/L1/collect/decisions-010704.log`, rf-pre-L1 빌드)에서 ASM-IN 864줄, ASM-OUT 864줄만 뽑은 것. 모두 `@Claude`. 커밋 99a3620(Refactor S6b)에서 들어옴.
  - 내용: 실제 플레이 중 후보 조립기의 입력(목표, 주변 적·벽·좌표, 가방, 배고픔 등 게임 상태)과 출력(후보 목록과 설명문). 모델에게 가는 후보 설명문의 원본이지만 모델의 원문 입출력(프롬프트 전체, 답)은 아님. 비밀값·사용자 경로·이메일은 없음(풀어서 검사함).
  - 바이너리(gzip)이기도 함. 1 MB는 넘지 않음.
  - 쓰는 곳: `AssemblerReplayTests.cs`(S6b 동등성 증거). 이 파일을 빼면 core-tests가 `File.OpenRead`에서 예외로 죽어 관문 core-tests가 FAIL이 됨(publish worktree에서 확인). 빼고 올리려면 그 시험이 파일 없을 때 건너뛰게 바꾸거나, 이 고정 자료를 손으로 만든 기록으로 바꿔야 함.
- HEAD가 사용자와 정할 것: 이 파일을 그대로 올릴지, 빼고(시험 수정 필요) 올릴지.

## 나머지 검사 (모두 통과)
대상: fc8161b의 Assets/AIPlayerSpike(+.meta), Assets/HeadlessTools(+.meta), Tools/ai-bridge(test, replay.mjs 포함), Tools/core-tests, Tools/compile-check, Tools/exp-sim, Tools/knowledge — 추적 파일 151개(README 포함).
- 비밀값: API 키·토큰·Bearer·쿠키·비밀번호 꼴 0. `.env`, settings/usage json, 키 파일 0. 시험 봉투의 `usage`는 null 또는 가짜 문구("usage 99% > 85% (fake bridge)").
- 개인 정보: 이메일 0(AIPlanner.cs `a.@for.report`는 C# 식별자라 오탐), `C:\Users`·사용자 이름 0, Steam ID 0. 남는 로컬 경로는 게임 설치 경로(compile-check `GameManaged` 기본값, Steam 공용 경로), `C:/Dev/CoreKeeperModSDK-kg/...`(real-data.mjs 기본값), memgraph.mjs의 `os.homedir()` 기준 경로 — 실제 사용자 값은 아님.
- 그 밖의 시험 자료는 손으로 만든 것: fixtures/plan(make-cases.mjs가 만든 가짜 브리지 응답), fake-seq/l0.json(가짜 응답 순서), memgraph·bridge-wiring·recall-loop 시험은 events.jsonl을 임시 폴더에 직접 만들어 씀, logshape.mjs selftest 줄은 손 예시, exp-sim은 합성 데이터. `logshape-allow-combat-s2.txt`는 실게임 로그에서 본 줄의 "모양"(필드 이름만)과 코드 위치·작업 문서 이름 — 실제 값 없음. `test/real-data.mjs`는 실제 기록을 읽는 스크립트일 뿐 기록을 담지 않음.
- 큰 파일: 1 MB 넘는 파일 0. 바이너리: asm-L1.log.gz 하나(empty-body.json은 0바이트 텍스트).
- 지워질 파일: mine/main에 있고 fc8161b에 없는 파일 0.

## 관문 (publish worktree, 오프라인, Unity 단계 뺌)
worktree `C:\Dev\CoreKeeperModSDK-publish`(브랜치 publish-combat-s2, mine/main 1c2a61b에서) 위에 fc8161b의 위 경로를 그대로 복사 — 경로별 git 트리 해시가 fc8161b와 같음 확인. 81개 파일 변경(추가 71, 수정 10) 준비만 하고 커밋하지 않음.
`bash Tools/core-tests/gates.sh` → failed gates: 0 (1분 5초)
- compile-check OK 오류 0개 / core-tests OK (297) / exp-sim OK / recall-loop OK (41) / bridge-wiring OK (21) / memgraph SKIPPED(Tools/knowledge/data 없음, 추적 안 되는 자료라 예상대로) / protocol OK (63) / fake-bridge, logshape, movecheck SELFTEST OK
- 따로: asm-L1.log.gz를 빼고 core-tests만 돌리면 FileNotFoundException으로 실패.

## 정리
- publish worktree 지움. 브랜치 `publish-combat-s2`는 남김(커밋 없음, 1c2a61b = mine/main 그대로).
- 메인 트리 ai-character(fc8161b, 커밋 밖 Unity 변경)와 combat-s0·combat-s2 worktree는 건드리지 않음.
- README 절은 쓰지 않음(올리지 않았으므로).

## 다시 할 때
결정이 나면 같은 방식으로: mine/main에서 worktree → 위 경로를 fc8161b에서 복사(빼기로 하면 fixture 제외 + core-tests 수정 커밋을 ai-character에 먼저) → README 시험 도구 절 → gates.sh → 한 커밋 → `git push mine publish-combat-s2:main`.

## 추가: 사용자 결정 후 push (2026-11-05)
사용자 결정: `asm-L1.log.gz`를 그대로 포함해 올림("그대로 올려줘"). README 절은 더하지 않음.
- worktree `C:\Dev\CoreKeeperModSDK-publish`를 브랜치 publish-combat-s2(1c2a61b = mine/main, fetch 후 같음)에서 다시 만들고, 위 경로를 fc8161b에서 복사. 81파일(추가 71, 수정 10, 지움 0), fixture 포함.
- 경로별 트리 해시: Assets/AIPlayerSpike(+.meta), Assets/HeadlessTools(+.meta), Tools/ai-bridge, Tools/core-tests, Tools/compile-check, Tools/exp-sim, Tools/knowledge 모두 fc8161b와 같음. README.md는 mine/main 것 그대로(바꾸지 않음).
- 같은 내용으로 앞서 gates.sh 오프라인 관문 failed gates 0(위 절).
- 커밋 **4ce938c** "Combat stage 2 and the offline test tools"(Co-Authored-By 줄 포함), 81 files changed, +6614 −74.
- `git push mine publish-combat-s2:main` → 1c2a61b..4ce938c (fast-forward, force 아님). `git ls-remote mine main` = 4ce938c54c6d1417d26b17b7885d5b603eceaa77.
- publish worktree 지움. 브랜치 publish-combat-s2는 4ce938c로 남김. 메인 트리 ai-character(fc8161b, 커밋 밖 변경 13줄 그대로)와 combat-s0·combat-s2 worktree는 건드리지 않음.
