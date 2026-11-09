# 최종 코드의 그래프 갱신 인계

2026-11-06. 실제 스크립트와 직전 `35-지식그래프반영2.md` 조사만 수행했다. 그래프 원본/생성물을 수정하거나 재생성하지 않았다. 최종 리팩토링 커밋과 실게임 증거가 확정된 뒤 실행한다.

## 경로와 기준

- G = `C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/지식그래프`
- P = `C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-05-지식그래프-개편`
- L = `C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/2026-11/2026-11-05-지식그래프-실화면`
- Node = `C:/Codex/Apps/Development-SDKs-2026-09-20/node-lts/node.exe`; dotnet SDK 10.0.403.
- 현행 baseline `a07166f38086e0241c37c601c16452664236813e`, 본 저장소 `C:/Dev/CoreKeeperModSDK`; worktree `C:/Dev/CoreKeeperModSDK-refactor-20261106`.

개발 그래프는 config의 본 저장소 working tree를 읽고 제품 그래프는 고정 commit blob을 읽는다. 병합 전 후보 검사는 그래프/원본의 별도 사본에서 codeRoot를 후보로 지정한다. 공식 경로를 몰래 후보로 돌리지 않는다. 최종 반영 때 본 저장소 HEAD와 제품 baseline이 의도한 커밋인지 확인하고 사용자 로컬 변경을 보존한다.

## 개발 그래프

`G/tools/config.json`의 codeRoot/codeDirs와 **명시적 codeFiles**를 대조한다. 현행 54개(모드 53 + HeadlessSdk)이며 분리 파일이 자동 추가된다고 가정하지 않는다. 원본 `G/기획/제품코어.md`, `정책.md`, `기능.md`, `미결.md` 중 기능의 함수 앵커와 미결의 옛 함수/경로 참조를 새 구조에 맞춘다.

G에서:

```powershell
node tools/build.mjs --strict
node tools/query.mjs check
node tools/query.mjs code F.my-door
```

마지막은 기능 하나의 선 서명 확인 예시다. `확인한 선`은 해당 빌드에서 실제 확인한 기능만 승격한다. 코드 이동/정적 검사만으로 옛 실게임 증거를 최신 실행 증거로 바꾸지 않는다. 선 밖 코드/확인 뒤 변경 건수 변화는 함수·앵커 대응과 함께 설명한다.

## 제품 그래프 재추출

먼저 G 및 P/L의 변경 원본/생성물을 이번 작업의 새 백업에 보존한다. 직전은 `35-graph-before` devkg/product archive였다. **pin-snapshot은 새 파일을 쓰지만 이전 snapshot에서 삭제된 파일을 제거하지 않는다.** 파일 이동 시 오래된 snapshot/runtime-package가 섞이지 않게 새 출력 또는 백업 후 승인된 정리 절차를 마련한다. 이 인계는 삭제하지 않았으며 경로 확인 없는 정리 명령을 제공하지 않는다.

고정값 변경:

| 파일 | 조치 |
|---|---|
| `P/B/extractor/pin-snapshot.mjs:2` | repo 본 저장소, commit a07166f → 최종 전체 SHA |
| `P/B/extractor/verify.mjs:2` | 같은 commit → 같은 최종 SHA |
| `P/B/extractor/build.mjs:71` | graph.meta.sourceCommit → 같은 SHA |
| `L/C/overlay-source.json` | baseCommit 및 boundaries 코드 기준 설명 |
| `G/README.md`, `G/제품그래프-사용법.md` | 코드 기준과 검증 결과 |

P/B에서 순서대로:

```powershell
# extractor 의존성 준비가 실제 필요할 때만:
# npm ci --prefix ./extractor --ignore-scripts --no-audit --no-fund
node ./extractor/pin-snapshot.mjs
dotnet run --project ./extractor/Extract.csproj -- ./snapshot ./csharp.raw.json
node ./extractor/build.mjs
node ./extractor/verify.mjs
node ./extractor/determinism.mjs
```

pin은 Assets/AIPlayerSpike·Assets/HeadlessTools·Tools/ai-bridge를 exact git blob bytes로 추출하고 runtime-package에 Tools/ai-bridge·Tools/knowledge를 둔다. external-runtime.manifest의 외부 정적 import 검사와 런타임 실행 증거는 다르다. `P/B/validation-review.json` 표본 8개의 파일/줄을 새 소스와 대조한다. 이동이 있으므로 직전의 “줄 동일” 판단을 재사용하지 않는다. B README에 새 baseline과 검증을 적는다.

## 통합과 실화면 대응

아래 G/P/L을 실제 절대 경로로 확장해 순서대로 실행한다. 실패를 해결한 뒤 다음 단계로 간다.

```text
node P/C/integrate-c.mjs
node P/C/build-graph-C.mjs
node L/B/remap-base.mjs --base <최종 전체 SHA>
node L/C/validate-C.mjs
node L/D/questions.mjs
node G/tools/product-build.mjs
node L/D/check-entrypoints.mjs
node G/tools/product-build.mjs
node G/tools/product.mjs check
node G/tools/product-build.mjs --check
```

remap의 base 기본값은 B graph sourceCommit이며 `--base`는 그 값과 같아야 한다. `--graph`/`--out`도 지원한다. **run 기본값 76563bf는 과거 화면 관측 당시 설치 소스**다. 새 baseline을 올린다고 `--run`/overlay installedSourceCommit까지 최신으로 바꾸지 않는다. 새 실게임 증거를 별도로 연결한 경우에만 해당 근거를 갱신한다. exact source segment 일치에 의한 대응이며 실제 함수 trace가 아니다. 분리한 함수 이름/경로가 selectedNames/legacy 대응에서 빠지는지 검토하고 overlay boundaries의 동일 구간 수/변경 목록을 새 결과로 고친다.

`P/C/integrate-c.mjs`에는 기존 기획·e82eb87·격리 HTTP 관측을 구별하는 역사적 문구가 있다. 옛 SHA 일괄 치환을 하지 말고 baseline과 관측 당시 기준을 구별한다. `L/D/verify.mjs`는 직전에도 “이전 생성물 불변” 단언이 재추출과 충돌해 실행하지 않았다. 이번에도 전체 검증 통과라고 포괄하지 말고 해당 한계/실제 수행 check를 기록한다.

최종 기록: 최종 SHA, 백업 위치, codeFiles/기능 앵커 변화, strict/query, B verify/determinism, 제품 structuralErrors/stale/baseline 경고와 byte-identical 결과, 확인 선을 옮긴 실제 근거와 미확인 항목. 노드/간선 수는 변화 설명용이며 과거 수와 같아야 하는 합격 기준이 아니다.

근거: G README/제품그래프-사용법/config, 위 실행 파일, P/B README, `work/2026-11/2026-11-05-코어-안전지대/35-지식그래프반영2.md`. 제품 코드·그래프·다른 프로젝트 파일은 변경하지 않았다.
