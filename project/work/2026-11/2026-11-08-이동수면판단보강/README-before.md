# 게임 행동 그래프

게임 원본과 검수된 상호작용 추출에서 **대상 → 행위 또는 처리 단계 → 조건·결과·실패 → 근거**를 찾아가는 개발용 파생 그래프다. 사실의 정본은 기존 추출 자료이며 이 폴더에서 게임 규칙을 새로 결정하지 않는다.

## 현재 게시본

기본 `graph.py` 명령은 **`published/`의256수용묶음·2,539case**를 조회한다. 수용246판의 모든251,264노드·350,068관계를 보존하고, 판단에 필요한10범위12레코드의 발동·입력·대상선택·비용·조건·후속 결과를 추가했다. Grapple/Lightning/Effect58와 세 조건귀속, 번식/대시/소환수폭발/사망·부활의 정적 연결이다. 현재251,417노드·350,275관계이며 기존5,880주장·3,235원본도 보존한다. 수용묶음·case·역할행은 서로 다른 기록 단위이며 고유 게임행동 수로 합산하지 않는다. `wholeScopeComplete=false`, `runtimeTested=false`다. [필수 연결 보완](../2026-11/2026-11-08-행동판단필수보완/completion-report.md), [246판 추가 기록](../2026-11/2026-11-08-행동그래프추가기록/completion-report.md), [잔여 두 경로 보완](../2026-11/2026-11-08-행동그래프잔여점검/completion-report.md)을 따른다.

기존 환경음13개 분류/상세82노드 제외 필터는 유지한다. 전수 관련성 조사의 미적용 분리 제안은 적용하지 않았다. 같은source의다른관계는유지한다. `--include-history`를붙이면 현재 보완분까지 포함한 비필터 원형을 명시적으로 조회한다. 직전246판 실물은 필수연결 보완 폴더의 `baseline-246/`와 `published-before-switch/`, 그 수용raw 이력은 추가기록 폴더의 `history-current/`에 보존한다. 과거 고정 원형과 정확 코드·영수증을 덮어쓰지 않는다.

| 경로 | 용도 |
|---|---|
| `published/graph.json`, `coverage.json`, `manifest.json` | 현재정본의기본조회세트 |
| 루트 `graph.json`, `coverage.json`, `manifest.json` | 기존생성기들의고정입력,게시시덮어쓰지않음 |
| `legacy_graph.py` | 기존생성기코드의정확한보관바이트,직접실행하는게시명령이아님 |
| `publication.json`, `baseline-source-aliases.json` | 게시생성기와과거논리코드pin의물리보관위치 |

과거생성기가 `graph.py`를import할때는기존논리ROOT/코드pin의의미를보존한다. 이호환API의 `sha(graph.py)`는과거코드의정확한보관바이트를가리킨다. 현재게시manifest는그pin을실제 `legacy_graph.py`로명시하고현재 `graph.py`도별도실파일hash로기록한다. 일반정본조회는아래CLI를사용한다.

기존 원본 3,235 source의 3,499개 상호작용 주장, 전체 추출 1,432행, 후속 보강 949행, 요리 6,561순서쌍을 각각 보존한다. 기존과 보강을 서로 다른 게임 행동으로 단순 합산하지 않는다. sourceKey와 variation이 다른 원본을 이름이나 ObjectID만으로 합치지 않는다.

## 조회

Python 표준 라이브러리만 쓴다. 이 PC의 `python` 명령은 Windows Store stub이므로 아래 고정 실행 파일을 사용한다. 이 폴더에서 실행한다.

```powershell
$graphPython = 'C:/Users/<USER>/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $graphPython -X utf8 graph.py overview
& $graphPython -X utf8 graph.py find InventoryChest
& $graphPython -X utf8 graph.py target 100 --variation 0
& $graphPython -X utf8 graph.py target 6510
& $graphPython -X utf8 graph.py target 2406
& $graphPython -X utf8 graph.py cooking 1645 1646
& $graphPython -X utf8 graph.py gaps 6506
& $graphPython -X utf8 graph.py gaps 4600
& $graphPython -X utf8 graph.py target 6613 --variation 0
& $graphPython -X utf8 graph.py target 2404 --variation 0
& $graphPython -X utf8 graph.py --include-history show consumer:A-ambient/flow/consumer
```

`target`은 정확한 원본별 주장 ID를 보여준다. 그 ID를 다음 명령에 그대로 넣는다.

```powershell
& $graphPython -X utf8 graph.py show '<주장 ID>'
& $graphPython -X utf8 graph.py source '<주장 또는 필드 ID>'
& $graphPython -X utf8 graph.py path '<시작 노드 ID>' '<끝 노드 ID>'
```

- `show`는 해당 행의 조건·결과·실패·처리 흐름과 적용되는 공통 규칙의 실제 내용을 함께 반환한다. 특히 B의 기존 규칙 보강과 D의 공통 규칙이 전개된다. 행과 공통 규칙 양쪽의 내용은 출처별로 유지한다.
- `source`는 `origin.path`의 `origin.pointer`를 읽고 정규화된 내용 해시를 확인한 후 원문을 반환한다. 그래프에 원본 컴포넌트 전체를 중복 복사하지 않아도 원래 주장과 근거 전체를 볼 수 있다.
- `show`의 `relations`와 `incoming`에는 보강·선행·입력 생산자·소비 연결이 있다. `path`는 명시적으로 등록된 방향의 관계만 따라간다. source가 같다는 이유로 실행 인과를 추가하지 않는다.
- `gaps`는 역사적 원문 미결, 후속 현재 판정, 주요 미결 평가, 범위 경계를 구별한다. 기본 반환 수는 20이며 생략 여부와 전체 수를 표시한다. `--limit`으로 바꿀 수 있다.
- `find`/`target`은 이름 일부·ObjectID·전체 sourceKey를 받는다. ObjectID가 같아도 여러 variation/prefab 후보가 나오며 정확한 source를 골라 조회한다.
- 이번 source 연결은 `target`의 `essentialDecisionEvidence.decision`에서 조건·비용(있는 경우)·처리·결과·실패·경계를 직접 반환한다. `nextQuery`로 정확한 `show`/`source`를 이어간다. source가 없는 Effect58 지원과 플레이어 사망 흐름은 기존 소비자의 `has_essential_supplement` 관계로 도달하며 임의 prefab 원본을 부여하지 않는다.

이 조회는 개발용 상세 보기다. AI 캐릭터에게 노출하는 요약, `known.json` 공개 범위, 현재 실행 가능 행동, 모드가 제공하는 능력을 판정하거나 변경하지 않는다. 지금의 게임 지식그래프와는 같은 게임 사실을 다루지만 현재 AI 소비 경로에 연결된 상태는 아니다.

## 생성과 검증

```powershell
& $graphPython -X utf8 graph.py --output-dir '<새 빈 출력 폴더>' build
& $graphPython -X utf8 graph.py check
```

`build`는 이미 독립 수용된246판의 정확한 raw 이력3파일을 고정 입력으로 재사용하고, 그 projection이246기준 그래프와 같은지 확인한 뒤 새12레코드를 덧붙여 기존 환경음 필터를 적용한다. 이전 수용 계약·생성기·영수증과 원문 추적성을 보존한다. 명시한 새 빈 출력 폴더에만 생성하며 현재 게시본을 자동으로 덮어쓰지 않는다. 루트의고정입력3JSON·원본·게임·모드는덮어쓰지않는다. 현재구성에없는추가수집이나미검수자료를자동승인하지않고,검수대상 선택·원본·생산기·생성규칙의 계약 해시 또는 독립 findings 자체의 수용 상태·계약·보고서 해시가 달라지면 중단한다. 과거 current config의 논리pin은 당시 config template의 실제 보관 경로와 구별한다. 원본의 bare Infinity와 후속의 특수 float는 `{ "floatSpecial": "positive_infinity" }` 같은 표준 JSON 객체로 의미를 보존한다. 64비트 pathID·주소는 원문의 문자열을 유지한다.

`check`는 고정 수용246 raw 입력과 정확 검수 계약·독립 수용, 보완/필터의 두차례 재생성과 현재 게시3파일의 바이트 일치, 게시manifest의 실제 파일hash/bytes를 검사한다. 이번 게시에서는 finalizer와 installer의 별도 두 재생성이 동일한3파일을 만들었고, 실제 게시·2,719물리핀·기본 조회를 대조했다. [게시 검증](../2026-11/2026-11-08-행동판단필수보완/check-published.json)을 따른다. 확인된입력을스스로새게임원본으로승인하는기능은없다. 조회내용이실제게임에서실행됐다는시험도아니다.

파일은주변원본과검수패키지를G기준상대경로로참조한다. 이폴더만복사하면근거가분리된다. 현재조회세트는 `published/manifest.json`을확인한다. 게시graph는원문근거를찾는상세인덱스이며실시간LLM프롬프트로통째로넣는자료가아니다.

## 그래프에서 구별하는 것

- `source`: 고정 대상 원본. `object_id_reference`는 숫자 ID를 공유한다는 참조이며 source/variation 병합이 아니다.
- `claim`: 초기 상호작용 주장, 전체 추출, 후속 보강. `rowKind`가 선행·자동 소비·전기 사실·기존 보강을 구분한다. 의미 분류가 정해지지 않은 초기/기본 행은 `reported_interaction`/`extracted_action_or_stage`로 남긴다.
- `rule`과 `condition/result/failure/flow/evidence/unknown`: 행이나 공통 규칙에 실제로 적힌 내용. 원본 pointer가 있다.
- `cooking_pair`: 입력 순서를 보존한 조건부 결과 계산. 두 재료 source와 결과 ObjectID가 연결된다.
- `historical_gap`: 초기 추출의 미결 문구. 후속 행이 생겼다는 이유로 자동 폐쇄하지 않는다.
- `coverage_disposition`: 후속 추출에서 추적한 1,671개 source×rule의 현재 판정. 비적용은 그 규칙에 한정한다.
- `gap_assessment`/`boundary`: 회로 6506, 제품 4600, 임의 미래 tick, 실제 월드 관측 등 남은 경계.

## 빈 곳의 의미

전체 source를 처리했다는 사실과 게임의 모든 행동을 규명했다는 사실은 다르다. 5,090개의 초기 미결 문구에는 공통 Baker/ECS 범위 설명이 반복되므로 독립된 실제 행동 공백 수로 세지 않는다. 후속 1,671개에 판정이 연결됐다고 모든 행동이 성공하거나 모든 미결이 사라진 것도 아니다.

초기 상호작용이 없는 source 592개와 전체 보강 후에도 주장 없는 source 수를 `coverage.json`에서 각각 볼 수 있다. 주장 없음은 불가능 판정이 아니다. 낚시·농사 등 일부 계열은 시작 단계가 있지만 대상별 조건·후속 결과가 모두 완결됐다고 검증하지 않았고, 좌표·액체·추상 상태의 독립적인 전수 모집단도 확립되지 않았다. 실제 실행 성공과 모드 능력 비교는 별도다.

작업 근거: [반영 합의](../2026-11/2026-11-07-행동그래프반영/02-합의.md), [입력 조사와 명세](../2026-11/2026-11-07-행동그래프반영/00-조사.md), [완전성 평가](../2026-11/2026-11-07-행동그래프반영/01-완전성평가.md).
