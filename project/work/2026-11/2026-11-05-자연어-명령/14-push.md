# 14. GitHub 올리기 (자연어 명령 + 게임 규칙, 런타임과 시험 도구)

사용자 결정: 2번 "런타임과 시험 도구까지 올리기"(전투 단계 2 때와 같은 범위). 방식은 [9-push.md](../2026-11-05-전투-단계2/9-push.md)와 같다.

## 결과
- mine/main **4ce938c → e469f9f** (fast-forward, force 아님). `git ls-remote mine main` = e469f9f220cfbad3e11b2321a7c53f7395a93156.
- 커밋 e469f9f "Natural-language chat for the AI characters, and the game rules"(Co-Authored-By 줄 포함). 42 files changed, +2539 −147(추가 15, 수정 27, 지움 0).
- 원본: ai-character b2fa4cc. 그 사이 다른 대화의 게임 규칙 커밋 a355e63·ffd79a7(Tools/knowledge, memgraph.mjs)도 같은 경로 범위라 함께 올라감.

## 경로 범위와 대조
Assets/AIPlayerSpike(+.meta), Assets/HeadlessTools(+.meta), Tools/ai-bridge, Tools/core-tests, Tools/compile-check, Tools/exp-sim, Tools/knowledge — push 뒤 e469f9f의 경로별 트리 해시가 모두 b2fa4cc와 같음. 바뀐 경로는 Assets/AIPlayerSpike, Tools/ai-bridge, Tools/core-tests, Tools/knowledge. README.md는 mine/main 것 그대로(지난번과 같음).

## 올리기 전 검사
- 범위 밖 변경: fc8161b→b2fa4cc 사이 범위 밖 파일 변경 0.
- 비밀값·개인 정보: 추가된 줄에서 API 키·토큰·Bearer·비밀번호 꼴 0, `C:\Users`·사용자 이름·Steam ID 0. 이메일 꼴은 NlChatTests.cs의 시험 문자열 `a@claude.com`(이메일은 멘션이 아님을 보는 시험) 하나뿐.
- 바이너리 0, 200 KB 넘는 파일 0. 새 실게임 원자료(fixture) 없음. 새 `logshape-allow-nl-chat.txt`는 로그 줄 모양·코드 위치·작업 문서 이름만 담음. `Tools/knowledge/rules.json`은 손으로 쓴 게임 규칙.
- 뺀 것: Unity 생성 변경(메인 트리 커밋 밖 14줄), 로그, Tools/ai-bridge/data·logs, Tools/knowledge/data(추적 안 됨), 작업 세션 폴더.

## 관문 (publish worktree, 오프라인, Unity 뺌)
`bash Tools/core-tests/gates.sh`(KG_DATA=본 저장소 Tools/knowledge/data) → failed gates 0: compile-check 오류 0, core-tests 445, exp-sim, recall-loop 41, bridge-wiring 23, memgraph 80, protocol 68, fake-bridge·logshape·movecheck SELFTEST OK.

## 정리
publish worktree(`C:\Dev\CoreKeeperModSDK-publish`) 지움. 브랜치 publish-combat-s2는 e469f9f로 남김. 메인 트리 ai-character(b2fa4cc, 커밋 밖 변경 14줄 그대로)와 다른 worktree는 건드리지 않음.
