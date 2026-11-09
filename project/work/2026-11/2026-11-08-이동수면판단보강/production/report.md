# 이동·수면 네 범위 원문 보완 생산

승인된 네 묶음을 **5개 append-only 후보 레코드**로 만들었다. 수면210과322는 원본 귀속을 혼동하지 않도록 나눴다. `runtimeTested:false`, `wholeScopeComplete:false`다. production 디렉터리 밖의 게임·모드·세이브·원문·세션 기록·그래프를 수정하거나 게시하지 않았다.

기준은 `../baseline-256/graph.json`, SHA256 `94ab86d02ae1c6b2d276e72fa1f3bb6eafac26a1a44618d9400a20b45e8e8120`이다. 기존 평가와 독립 검수는 재사용하되, 고정 원문과 이 기준판의 실제 payload를 다시 대조했다. 이름/별칭만으로 기존 의미를 판단하지 않았다.

| 묶음 | 생산 판정과 실제 추가 차이 | 막는 오판 |
|---|---|---|
| Grapple | `grapple-pull-end-v1`: 기존 입력/aim/hit·쿨다운 요청을 재사용하고, timer 만료 또는 거리<0.5의 Inactive/Walk 및 curve의 현재/다음 tick 차이×거리/physicsTimeStep에 따른 속도 가산·force0을 연결했다.8373/8374 authoringRef와 정확 OffHandAuthoring·curve 변환을 확인했다. | 발동 성공을 도착 또는 무기한 당김으로 취급하지 않는다. |
| Mount | `mount-safe-dismount-v1`: 안장 변환/교체·속도 배율을 재사용하고, 최초 facing/랜덤 오프셋→거리순 네 이웃 후보의 walkable/구 충돌 검사, 최초 실패의 상태 유지/Emote16, 제어 해제 뒤 이전 tick 월드의 ExitState 재검사를 연결했다. | 하차 입력이나 최초 후보 성공을 무조건 최종 위치 확정으로 보지 않는다. |
| Minecart | `minecart-rail-brake-dismount-v1`: 정확6551 배치/생성과 피드백을 재사용하고 authoring→MinecartConverter/CD를 직접 검증했다. rail 후보, 역입력 timer,600/1000×dt 감속·마찰 예외·가속/force, 감속보다 먼저 실행하는 interact/실제 속도 gate를 연결했다. | 같은 tick에 먼저 제동해서 하차한다고 오판하지 않는다. |
| 조건 수면 | `condition-sleep210-v1`, `condition-sleep322-v1`:9609의 기존210 공급·변환/섭취와 공통 lifecycle/침대회복 소비를 재사용하고 Sleep push·회복 허용·해제와 Sleep+210의 이동0을 구분했다.322는 진입 당시 flag와 조건 소실/이동 target>0.1의 PopState 경로다. | 9609→322 귀속,322의210식 이동 봉쇄,강제210의 무조건 회복/이동 해제를 만들지 않는다. |

`source-bindings.json`은 원본 objects.json의 네 정확 source→authoringRef component pointer→components.jsonl의 정확4행을 담는다. 직접 binding은8373/8374/6551/9609 총4개다. mount와322는 `sourceBindings:[]`인 typed consumer이며 기존 노드에만 연결한다.6551의 maxSpeed800은 정확 authoring/변환이 runtime에 선택되는 조건 아래의 값이다.9609 authored210 duration5는 실제 관측5초 이동불가 약속이 아니다.

`existing-values.json`은 비교한 기존 실제 노드를 보존한다. `static-check.json`은29개 current ID·origin pointer·기존 value와 원문 pointer의 동등성,각 source 검증,code SHA/행 범위 및 네 묶음의 차이를 담는다. `normalized-origin-check.json`은 `legacy_graph.py`와 같은 `json.dumps(ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+newline`와 특수상수 규칙으로29개 origin 계약 SHA를 모두 재검증하여 일치했다. static-check의 별도 compact JS diagnostic SHA는 계약 SHA라고 표기하지 않는다.

`pins.json`은 직접 읽은42개 입력의 byte SHA를 담는다. mutable published graph를 pin하지 않고 immutable baseline만 사용했다. `code-excerpts.txt`에는 실제 고정 원문의 행번호/해시가 있다. 정적 생산·origin 확인을 재생성하려면 다음 순서로 실행한다(둘 다 production에만 출력한다).

```powershell
node .\production\produce.mjs
& 'C:\Users\<USER>\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -B -X utf8 .\production\verify_origins.py
```

독립 원문 검수 지적1건을 반영했다. Minecart `!flag3 && speed>200` 가속 설명을 유일한 수직 후보에만 좁혔던 초안에서, 현재 진행방향 또는 유일한 수직 rail 후보를 얻은 `flag7=true` 경로로 바로잡았다. 다른 네 레코드 payload는 이 수정에서 바뀌지 않았다.

동결 산출물 SHA256:

- supplement.json: `901d6c8b625264303a2b993be4b35df379eca95c1231be2eaf84c6bdf5a85733`
- pins.json: `195dd5cb5c0340bdf5b9e270c48342a125c2e899b765c93c3f4d308204547815`
- static-check.json: `487c535167e04a3ea1ecb452ae2693121351be4c6b1f716d65ed3761d950bcd4`
- normalized-origin-check.json: `f846b20322fbeb885a3ec413286a56bafb93f00541b85826d36ae513e81b6c0b`

실제 도착/실행 성공,전체 Grapple collision helper,모든 mount 종별 source,전체 minecart turn plan,322 공급자 전수,새 gap scan과전체공백0은 범위 밖이다. 후속 통합자는 기존 노드→새 supplement의 역방향 연결과 기본 CLI 소비를 따로 검증해야 한다.
