> 현재 고정 코드 기준은 `20b1aced7594a47eacb4be060405bcb53d38f48f`다.09e418a 실기와20b1aced 추가 경계의 오프라인·Unity 검증을 구별한다. [이번 반영](../../2026-11-08-observation-redecision/graph-update.md)에 설치·복원 승인 대기를 기록했다.

> 현재 고정 코드 기준은 `43c3817fc1a36142c8591fb087ed867665633fe1`다. 공유 검색과 기존 원정 트리 연결의 정적 코드층이다. [이번 반영](../../2026-11-08-path-optimization/graph-update.md)의 대표 실기 범위와 설치 상태를 따른다.

> 현재 고정 코드 기준: `7af8f91e5d26e4fd9ce5996809ae41e213340118`. 채집·이동·귀환 구현의 정적 코드층이며 조명 시야는 사용자 결정으로 후속이다. 실게임 trace가 아니다.

> 제약해제 현재 코드층 기준은 `5cc5a43ff8df91634c04b44bac9f15f849bdb611`다. 공식 소스는 `5cc5a43ff8df91634c04b44bac9f15f849bdb611`로 같다. 임의 제약 해제와 새 지원동작의 정적 연결/오프라인 근거는 [이번 반영](../../2026-11-07-제약해제/graph.md)을 따른다. 과거 실게임·모델 증거를 새 코드판 성공으로 승격하지 않는다.

> 과거 공식 소스와 코드층 기준은 `5a05b80c4fe1ea764caa62ee83d06ac7fb3177ef`다. 일반 단일 수리·단일 레벨 업그레이드·자기 pending 회수의 새 통합 변경을 반영했다. [본 반영](../../../장비정비-트리/10-main-integration.md)과 [새 변경 검수](../../../장비정비-트리/08-integration-review.md)를 따른다. 실제 모델·실게임은 치트환경 구축 후 묶음 검증까지 미확인이다.

> 과거 공식 코드와 이 코드층의 고정 snapshot 기준은 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`이다. 당시 공식 ai-character와 일치했으며, [본 코드 반영](../../2026-11-07-생활트리구현/08-integration.md)과 [그래프 반영·검증](../../2026-11-07-생활트리구현/09-graph-integration.md)을 따른다. 설치·실게임은 보류이며, 아래 과거 코드판의 관측·추출 기록을 현재판의 실게임 성공으로 승격하지 않는다.

> 과거 코드 기준: `11eb461ae122f404acec94563c086b09e002987c` (2026-11-06 사용자 승인 후 UI 흡수판). 당시 ai-character와 일치했다. 당시 실게임·수용 근거는 work/ai-panel-integration-20261106/04-live-verification.md 및08-acceptance.md, 통합 결과는09-final-integration.md다.

> 과거 코드 기준: `b25099f6fd31e4c4c3618357eb9e8101e0e17124` (2026-11-06 사용자 병합 승인 후 적용). 당시 본 저장소 ai-character와 기준이 같았다. 공식 적용 결과는 work/2026-11/2026-11-06-선택지개선/08-merge-and-graph.md를 따른다. 과거76563bf/e82eb87/902721e 증거와 이번 b25099f 한정 실게임 관측은 구별한다.

> 2026-11-06 코어-안전지대 내 문 판정(mydoor-fix) 반영 뒤 다시 추출: 기준 커밋을 `a07166f38086e0241c37c601c16452664236813e`(ai-character, 9df72b7 위 mydoor-fix 3커밋)로 바꿔 같은 실행법으로 다시 만들었다(B.graph 노드 5549·간선 21612, 기능 22 그대로, 미도달 37, 결정성 2회 동일, 스냅샷 223개). 표본 8개의 파일(AIPlayerSpikeMod.cs·HeadlessSdk.cs)은 바뀌지 않아 줄 번호 그대로다(`recheck` 메모). 이전 생성물은 [반영 기록](../../2026-11-05-코어-안전지대/35-지식그래프반영2.md)의 `runs/35-graph-before/`에 있다. 아래 9df72b7 메모와 본문 수치는 이전 추출 기록이다.

# B 코드 층 결과

> 2026-11-06 물건 놓기·장소로 가기(제작대-놓기) 반영 뒤 다시 추출: 기준 커밋을 `9df72b7ec31f50ce98985355f8421ee6635761ab`(ai-character, place-s1 fast-forward — 그 아래 계속하기 문구 8fd1e34·문 단계 4238cce 포함)로 바꿔 같은 실행법으로 다시 만들었다(B.graph 노드 5527·간선 21511, 코드 층 노드 5436(코드 5335·기능 22·데이터 69·외부 10)·간선 21314, 기능 22 그대로, 미도달 37, 결정성 2회 동일, 스냅샷 223개). `validation-review.json` 표본 8개는 새 스냅샷에서 다시 읽어 줄 번호만 고쳤다. 이전 생성물은 [반영 기록](../../2026-11-05-제작대-놓기/16-반영.md)의 `runs/16-graph-before/`에 있다. 아래 99c9a95 메모와 본문 수치는 이전 추출 기록이다.

> 2026-11-06 코어 기지 안전 단계 1~3 반영 뒤 다시 추출: 기준 커밋을 `99c9a95a9b69b323f179cfe927c4f66530cef306`(ai-character, base-s3 0869165 fast-forward + 시험 기대 고침)로 바꿔 같은 실행법으로 다시 만들었다(B.graph 노드 5150·간선 20055, 코드 층 노드 5059(코드 4960·기능 22·데이터 67·외부 10)·간선 19858, 기능 22 그대로, 미도달 31, 결정성 2회 동일, 스냅샷 209개). `validation-review.json` 표본 8개는 새 스냅샷에서 다시 읽어 줄 번호만 고쳤다. 이전 생성물은 [반영 기록](../../2026-11-05-코어-안전지대/24-지식그래프반영.md)의 `runs/24-graph-before/`에 있다. 아래 b2fa4cc 메모와 본문 수치는 이전 추출 기록이다.

> 2026-11-05 자연어-명령 반영 뒤 다시 추출: 기준 커밋을 `b2fa4ccb6e0ae1defcf411b5e16371df563806f5`(ai-character, 자연어 명령 반영)로 바꿔 같은 실행법으로 다시 만들었다(B.graph 노드 4089·간선 15935, 코드 층 노드 3998(코드 3900·기능 22·데이터 66·외부 10)·간선 15738, 기능 22 그대로, 미도달 19, 결정성 2회 동일, 스냅샷 189개). `validation-review.json` 표본 8개는 새 스냅샷에서 다시 읽어 줄 번호만 고쳤다. 이전 생성물은 [반영 기록](../../2026-11-05-자연어-명령/13-반영.md)의 `runs/13-graph-before/`에 있다. 아래 fc8161b 메모와 본문 수치는 이전 추출 기록이다.

> 2026-11-05 다시 추출: 기준 커밋을 `fc8161befa5ec059d05064706cd15d89c6286492`(ai-character, 전투 단계 2 반영)로 바꿔 아래 실행법 그대로 다시 만들었다(C# 31개·JS 6개, 노드 3724·간선 14471, 기능 22 그대로, 미도달 18, 결정성 2회 동일). 아래 본문의 수치와 커밋은 첫 추출(e82eb87, 노드 3367·간선 12920) 기록이다. `validation-review.json`의 표본 8개는 새 스냅샷에서 다시 읽어 줄 번호만 고쳤다(`recheck`). 이전 생성물은 [반영 기록](../../2026-11-05-전투-단계2/8-반영.md)의 `runs/8-graph-before/`에 있다.

기준 커밋은 `e82eb87f25641cc7c31e6216f64e20a34e58d0c0`이다. 기존 기획·정책·화면 문서 및 A 결과를 입력으로 쓰지 않았다. 기존 tools/config.json의 27개 C# 파일 범위만 재사용했고 build.mjs/lib.mjs는 구현 방식을 검토했다. 기존 정규식 파서, 문서 기능 ID, 기존 graph 결과는 추출 원본으로 사용하지 않았다. 제품 작업 폴더·게임·서비스 프로세스를 변경하거나 실행하지 않았다.

- `code.graph.json`: 기계 추출 코드 층. 기능 22, 코드 3271, 데이터 64, 외부 연결 10. 간선 12920.
- `meanings.json`: 기능 한 줄 22개를 사람이 읽는 뜻으로 작성한 별도 입력. DB·계측 이벤트 뜻은 해당 노드가 없어 비어 있다.
- `B.graph.json`: 코드 층에 뜻과 B-7 추정 노드를 합친 임시 B 그래프. 추정은 관측 사실이 아니다.
- `inference.md`, `inference.graph.json`: B-1~B-6을 수행하지 않은 별도 컨텍스트의 코드만 보고 쓴 추정.
- `csharp.raw.json`, `javascript.raw.json`: 구문 트리 정의·참조·호출 인자·리터럴·진단. code.graph의 근거를 다시 찾는 원자료.
- `validation.json`: 도달하지 않는 실행 가능 단위 15개, API 호출자, 고정 씨앗 표본. 표본의 실제 판정은 `validation-review.json`에 분리했다.
- `question-coverage.json`: 상태 조회→설정 읽기, 호스트 목표→설정, plan→기억 검색 경로 및 health가 plan에 도달하지 않는 검사.
- `boundaries.json`: 미해결 호출, 리플렉션, Node runtime, 외부 모듈과 동적 import. 경계는 누락을 숨기는 대체 코드 노드가 아니다.
- `snapshot.manifest.json`, `snapshot-verification.json`: 입력 파일 SHA-256와 고정 커밋 Git blob 일치 168개(분석 스냅샷과 배포 패키지 합계).
- `external-runtime.manifest.json`: SDK 커밋 밖 ai-chatroom 런타임 의존성 11개 정적 전이 import의 해시·git HEAD·변경 상태. 코드 값을 실행하거나 설정/인증 내용을 읽지 않았다. 실행 당시 해시를 보증하는 기록은 아니므로 A 실행과의 일치는 D에서 확인한다.
- `determinism.json`: 새 Roslyn/Acorn 추출 2회, 추출 파일 7개 모두 바이트 동일.

## 범위와 원본

분석 대상은 기존 모드 C# 26개, Unity 에디터 빌드 도구 C# 1개, Node 브리지의 bridge/memgraph/plan/preflight/protocol/recall-loop 6개다. `snapshot`은 이 입력을 포함한 고정 커밋 파일들이다. `runtime-package`는 A가 복제하여 실행할 수 있도록 Tools/knowledge 배포 의존성을 더 담았다. 게임 지식그래프 데이터나 생성기를 이 개발 그래프로 개편하지 않았다.

초기 `git archive`는 Windows 줄바꿈 필터가 적용되어 일부 파일 바이트가 blob과 달랐다. `extractor/pin-snapshot.mjs`가 `git cat-file blob`의 원바이트를 직접 저장하는 방식으로 교정했다. 두 폴더의 최종 모든 파일은 커밋 blob과 같다. 기존 ZIP 파일은 초기 archive 전송본이며 **최종 분석 원본이 아니다**. 재배포에는 최종 `runtime-package` 폴더를 복사한다.

## 실행법

PowerShell에서 B 폴더를 현재 경로로 둔다. .NET SDK 10.0.403, Node v24.21.0, Acorn 8.15.0을 사용했다. Roslyn은 SDK에 포함된 DLL을 참조하므로 NuGet 패키지가 필요 없다. Acorn은 package-lock.json으로 고정되어 있다.

```powershell
npm ci --prefix ./extractor --ignore-scripts --no-audit --no-fund
node ./extractor/pin-snapshot.mjs
dotnet run --project ./extractor/Extract.csproj -- ./snapshot ./csharp.raw.json
node ./extractor/build.mjs
node ./extractor/verify.mjs
node ./extractor/determinism.mjs
```

pin-snapshot은 이 산출물 폴더에만 쓰고 제품 checkout은 읽기만 한다. 이미 고정된 snapshot을 재사용할 때 생략할 수 있다. build는 추출 파일을 재생성하며 `meanings.json`과 존재하는 `inference.graph.json`을 별도 입력으로 B.graph에 합친다. verify는 실제 서비스 실행 없이 입력 바이트·경계 의존성 해시·코드 경로를 검사한다. determinism은 C#부터 두 번 다시 실행한다. timestamps, mtime, 절대 실행 시각을 그래프에 넣지 않았고 정렬은 ordinal이다. Roslyn 병렬 semantic 진단 순서가 달라질 수 있어 직렬화 전에 정렬했다.

## 정확도와 경계

C# 구문 오류는 0이다. Unity/게임 metadata를 이 분석에 로드하지 않아 semantic 진단 4416개가 남는다. 이는 제품 컴파일 결과가 아니다. 유일한 Roslyn 후보만 있는 오류 호출, 가상 메서드의 가능한 구현, static 초기화, 유한 JS 등록표 후보는 `possible: true`와 resolution 근거를 붙여 보수적으로 연결했다(356개). 실제로 호출되었다는 뜻이 아니다. 여러 후보의 임의 선택이나 문자열 함수 이름만으로 전역 연결은 하지 않는다.

단일 데이터 노드는 확인된 저장 키/경로 표현식 기준이다. `file:p` 같은 동적 경로는 p의 실제 값 전체를 안다는 뜻이 아니다. 파일 단위로 경로 식이 같은 경우만 묶으므로 동일 물리 파일이 여러 표현식 노드로 남을 수 있다. JSON/JSONL·env·EditorPrefs는 데이터로 두고, PugDatabase는 게임 SDK 경계다. 스키마 코드나 외부 분석 전송이 발견되지 않았으므로 DB·계측 이벤트 노드를 만들지 않았다.

API 입구는 method/path 조건의 실제 handler 범위를 각각 코드 노드로 만들어 GET health와 POST plan을 분리했다. 공유 HTTP callback 전체를 두 기능에 붙여 생기던 거짓 경로를 제거했다. C# AIPlanner.HealthProbe와 Report의 주소 상수는 실제 Node route handler에 http reference로 연결된다. api 호출자는 기능→코드에도 별도 연결된다.

미도달 목록은 죽은 코드 목록이 아니다. implicit using Dispose, 외부 ToString, metadata 부재 생성자, 편의 오버로드, 더 이상 호출 안 되는 보조 함수 등이 섞인다. 진짜 미사용 여부와 runtime code coverage는 D에서 독립 실행 결과를 대조해야 한다. B는 A 입력 금지를 지켜 runtime 검사를 미수행으로 남겼다.

기능 ID는 API 2개, startup 1개, IMod/네트워크/Harmony hook 14개, `/ai` slash 1개, Unity 배치 CLI 4개다. `/ai status`, 목표 조작, 자율 행동 같은 큰 활동은 별도 API라고 만들지 않고 slash 또는 Update 입구 아래 원본 메서드에서 읽는다. 빌드·설치 기능은 그래프 추출만 했고 실행하지 않았다.


## 2026-11-06 격리 리팩토링 후보

이 사본의 코드 기준은 `902721ec505df42d74d0b2d245ed34a049faadad`다. 공식 원본과 본 저장소 HEAD는 a07166f로 보존한다. 개발 그래프는 후보 worktree 85개 C# 소스, 제품 그래프는 Git blob 고정 snapshot을 읽는다. 새로운 제품 기능은 추가하지 않고 심볼·책임 위치만 갱신했다. 과거 화면 설치 기준 76563bf와 역사적 추론 e82eb87은 유지하며 새 실행 증거로 승격하지 않는다. 현재 상태와 검증 결과, 병합 승인 뒤 반영 순서는 `work/리팩토링-20261106/14-graph-stage-result.md` 참조.
