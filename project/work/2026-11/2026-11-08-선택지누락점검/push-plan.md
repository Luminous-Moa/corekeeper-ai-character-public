# GitHub 백업 push 준비 (커밋·push 전)

대상: mine/main(`https://github.com/Luminous-Moa/corekeeper-ai-character`, 비공개, 현재 51f8833). publish worktree `C:\Dev\CoreKeeperModSDK-publish-gap`(브랜치 publish-gap-20261108, mine/main 기준). 아직 git add·commit·push 하지 않았다. 방식은 [이전 push](../2026-11-05-자연어-명령/14-push.md)와 같다(경로 범위 복사 → 한 커밋 → fast-forward).

## 1. 코드 (ai-character 49bd748)
- 경로: `Assets/AIPlayerSpike`(+.meta), `Assets/HeadlessTools`(+.meta), `Tools/` 전체(이전 7개 + 시험 도구 action-input/execution/gather/info/inventory-menu/life*/maintenance/path-preparation/path-search-benchmark/restriction/travel-tests, test-boundaries, ui-font).
- 49bd748 추적 파일 495개, 6.9MB. `git archive`로 복사했고 비추적(ai-bridge/data·logs, Tools/knowledge/data)은 없다.
- 대조: 임시 인덱스로 만든 트리의 `Assets/AIPlayerSpike`·`Assets/HeadlessTools`·`Tools`·두 .meta 해시가 49bd748과 모두 같다. mine/main 대비 360파일 변경(+364,681 / −7,938).
- 가장 큰 파일: `Tools/ui-font/source/Galmuri9.bdf` 2.5MB(갈무리 글꼴 원본, 이미 공식 추적), `Tools/core-tests/fixtures/asm-L1.log.gz` 160KB(이전 push에서 사용자가 포함 결정).

## 2. 프로젝트 자료 (`project/` = 작업 세션 폴더 상대 경로 그대로)
선택 스크립트: `C:\Users\<USER>\AppData\Local\Temp\claude\c237366b8a8ea80b\44263ae2-99f5-4724-9951-89bf0b261264\scratchpad\push-gap\select.mjs` (재실행: `node select.mjs <세션폴더> <publish>/project repo-cs-names.txt report.json`; 같은 원본 sha의 xz는 재사용하므로 project/를 비울 때 *.xz·*.xz.sha256은 보존). 결과 목록 `report.json`(같은 폴더).

- 세션 폴더 45,199파일(약 40GB) 중 **10,593파일, 원본 1,562MB → 저장 1,036MB**(xz 3개 적용 뒤). 100MB 넘는 파일 없음(최대 47.6MB 원본 JSON). project/를 tar+gzip -6으로 묶으면 119MB라, git 압축 뒤 push 크기도 대략 그 정도로 추정한다(GitHub push 한도 2GB 안).

| 분류 | 파일 | 크기 |
|---|---|---|
| context.md·history.md·todo.md | 3 | 0.4MB |
| 문서 .md | 2,521 | 30.4MB |
| 문서 .txt(1MB 이하, 로그 이름 제외) | 1,687 | 19.1MB |
| 도구 스크립트(.mjs/.js/.py/.ps1/.sh 등, 5MB 이하) | 2,713 | 20.9MB |
| 모드 소스 사본 .cs(우리 저장소 파일명과 같은 것만) | 1,972 | 39.2MB |
| 그래프 정본 폴더(지식그래프, 기획연결, 개편 B·C, 실화면, 행동판단프로젝트연계, 게임행동-그래프) | 378 | 607.2MB |
| 행동 판단 그래프 manifest 입력(작업 세션 안) | 1,319 | 845.0MB |

- 압축(xz -9, 원본 sha256은 옆 `.xz.sha256`):
  - `work/게임행동-그래프/published/graph.json` 363.1MB → 11.7MB, `a16c73944d7bc0587640e6760d0d35388cc4ea1adb67b2e9963ecd8990de5713`
  - `work/게임행동-그래프/graph.json` 113.4MB → 3.1MB, `6a7a40eec2dfab052322afb6c085ffc2605ed8e9b079dcaba4d49d24973c8a69`
  - `work/2026-11/2026-11-07-상호작용미결보강/B-targets/catalog-evidence.json` 68.4MB → 4.0MB, `113082427925cbe65ab63c07adaa9e8f0acc95a328f25441b033ae4853d64729`
- 원본 기준 큰 파일(압축 뒤 제외): 실화면 D/graph.json 47.6, 상호작용미결보강 package/collection.json 46.8, 행동전체추출 package/collection.json 32.2, 행동결과완결 source-component-census(.v2) 30.1×2, 개편 B/csharp.raw.json 29.9, family-active-values-v2 28.7, direct-component-role-links(-v2) 20.8·20.2, extraction.json 16.3×5(같은 내용 사본, git이 한 blob으로 저장), 행동그래프추가기록 contract 16.0·selection 15.1, lifecycle-candidates 14.5MB.

## 3. 제외 규칙과 제외 크기
| 규칙 | 파일 | 크기 |
|---|---|---|
| 사본·후보·백업·로그 폴더(candidate*, history-current, baseline*, published-before-switch, *before*, replay-N, logs, __pycache__, obj) | 10,043 | 27,995MB |
| 게임 원본 추출물 `work/게임원본-상호작용`(게임 코드 5,552개·에셋 덤프·deps) | 6,963 | 631MB |
| 확장자(.sqlite .log .png .gz .zip .dll .exe .pdb .assets .bundle .pugbackup .worldinfo .bin 등) | 3,393 | 3,449MB |
| 위 선택에 안 드는 기타 JSON 등(그래프 정본·manifest 입력이 아닌 작업 중간 자료) | 14,090 | 7,208MB |
| 우리 것이 아닌 .cs(게임 코드 발췌 등) | 117 | 3.3MB |

- 행동 판단 그래프 manifest(작업 세션 안 2,745개 참조) 중 규칙으로 빠진 것: 게임원본-상호작용 859, 게임 코드 발췌 .cs 23, replay-1/2 39, history-current 12, baseline-* 12, candidate-v3 2, .assets 4, .bin 1. 없는 파일 0.
- 게임 설치 파일(Program Files, 13개: resources.assets, globalgamemanagers.assets, Pug.*.dll 4, ScriptableData.dll, monoscripts/defaultlocalgroup bundle, catalog.bin/hash, settings.json, appmanifest)은 제외. manifest에 sha256이 남아 있어 같은 게임 판인지 대조 가능. manifest가 작업 세션 밖 `~/.codex/plugins/.../start-job/SKILL.md`도 하나 가리키며 제외.
- **결과적으로 push본만으로 행동 판단 그래프를 처음부터 재생성할 수는 없다**(게임원본 추출물·이전 판 history 그래프·게임 설치 파일 필요). 게시본 자체(2,542case 전부 보존판)와 생성기·manifest·검수 기록은 들어간다.

## 4. 검사
- 비밀값: API 키·토큰 꼴(sk-/sk-ant-/AIza/xai-/ghp_/github_pat_/AKIA/Bearer 20자+/PRIVATE KEY/xox) **0건**. 키=값 꼴 1건은 환경변수 이름 문자열(`KeyVariable => "OPENAI_API_KEY"`)이라 비밀값 아님. API 호출 스크립트는 모두 `process.env.OPENAI_API_KEY`·`CELERIS_API_KEY`에서 읽는다. .env·.pem·secret·credential 이름 파일 0.
- 개인 정보(개수만): `C:\Users\<USER>` 경로 330회/188파일, 사용자 이메일 0, Steam ID 2회(게임 appmanifest 사본 `LastOwner` 2개 파일: 행동결과완결 pilot/evidence, projectile-helper26/fixed-event). 그 밖 이메일 꼴은 글꼴 저작자(quiple@quiple.dev)·시험 문자열·noreply@anthropic.com·support@celeris.ai.
- 바이너리로 잡힌 파일 547개: 대부분 UTF-16 등으로 저장된 .txt 534개(명령 출력), .xz 3, 빈/특수 json 6, 기타 4. 이미지·실행 파일 없음.

## 남은 확인
- Steam ID가 든 appmanifest 사본 2개를 올릴지(비공개 저장소). 빼려면 select.mjs에 `.acf` 제외를 더한다.
- 그래프 파일 독립 검수 뒤 select.mjs를 다시 돌려 갱신분을 복사한다.

## 결과 (push 완료)
- 커밋 `d6d1f3b4c4f618f23c34f2e935fc7ff791367adb` "Fill missing bag, life and maintenance choices, shared chests, and the project records"(Co-Authored-By 줄 포함). mine/main **51f8833 → d6d1f3b**(fast-forward, force 아님). `git ls-remote mine main` = d6d1f3b4c4f618f23c34f2e935fc7ff791367adb.
- push 직전 mine/main 51f8833, ai-character 49bd748로 준비 때와 같았다.
- 트리: 11,094파일(project/ 10,598 = 선택 10,595 + 압축 짝 .xz.sha256 3). 코드 `Assets/AIPlayerSpike`·`Assets/HeadlessTools`·`Tools`·두 .meta의 커밋 트리 해시가 49bd748과 같다. 100MB 넘는 blob 0. 변경 10,958파일.
- project/: 원본 1,562MB → 저장 1,036MB(xz 3개 그대로, 원본 sha 같아 재압축 안 함), tar.gz 추정 약 119MB.
- 추가 제외: Steam appmanifest 사본 `.acf` 7개(`LastOwner` Steam ID가 든 2개 포함; 규칙 `steam-id-acf`로 모두 뺌) — `2026-11-06-게임상호작용자료/inputs`, `2026-11-07-행동20건수집/B-targets/catalog-inputs`, `.../review/B-evidence/food-catalog-recheck/catalog-inputs`, `2026-11-07-행동결과완결/full/A/projectile-helper26/fixed-event`, `.../pilot/evidence`, `.../review/progressive-six/unrelated.acf`, `.../terra-sample/evidence/installed-appmanifest.acf`.
- 최종 제외 집계: 사본·로그 폴더 10,043파일 27,995MB, 게임 원본 6,963파일 631MB, 확장자 3,393파일 3,449MB, 미선택 14,085파일 7,208MB, 우리 것 아닌 .cs 117파일 3.3MB, .acf 7파일.
- 비밀값(최종 파일 집합): 키·토큰 꼴 0, 키=값 꼴 1(환경변수 이름 문자열, 비밀값 아님), 비밀 이름 파일 0, Steam ID 0, 사용자 이메일 0, `C:\Users\<USER>` 경로 333회/189파일.
- 관문(publish worktree, Unity 제외, KG_DATA=본 저장소 Tools/knowledge/data): failed gates 0 — compile-check 오류 0, core-tests 1581, exp-sim, recall-loop 41, bridge-wiring 23, memgraph 84, protocol 190, fake-bridge·logshape·movecheck SELFTEST OK. project/는 시험 대상에 섞이지 않았다.
- 정리: publish worktree `C:\Dev\CoreKeeperModSDK-publish-gap` 지움, 브랜치 publish-gap-20261108(d6d1f3b) 남김. 공식 체크아웃·다른 worktree는 건드리지 않음.
