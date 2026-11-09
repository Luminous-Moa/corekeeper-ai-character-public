# 이동·수면 보완 원문·생성 독립 검수

판정은 **independent_pass**다. 정확 계약 `74614162f5aafdbc594318fa5b08f93b02dc23f011cbc2a22ed6edd485d1b743`와 `candidate-review-v2`의 5개 레코드·4개 묶음에 한정한다. 열린 finding은 없다. 기존 평가와 독립 원문 검수는 재사용하되, 새 payload와 새 생성 프레임워크·후보 실물·기본 CLI는 별도로 검수했다. 실제 게임 실행·전체 공백0·모든 공급자 완료는 주장하지 않는다.

고정 원본 objects.json의8373/8374/6551/9609 정확4개 source와 authoringRef component pointer를 직접 대조했다. components.jsonl의36118/43429/79520/81520 정확4행도 직접 비교했다. 고정 코드10개를 직접 읽고 SHA를 보존했다(`source-original-checks.json`). Mount와322는 source-free typed consumer이며,3750의 새 직접 귀속이나9609→322 공급을 만들지 않았다.

- Grapple은 activeRoutine gate 뒤 timer 만료 또는 거리<0.5에서 Inactive/Walk를 요청한다. 그 전에는 정확 hook prefab의 OffHandCD.curve를 현재/다음 tick 비율에 평가한 차이×거리/physicsTimeStep로 선형 속도를 가산하며 force0을 쓴다. OffHandAuthoring의 curve→BlobCurve→OffHandCD 변환을 확인했다. 도착 보장은 없다.
- Mount는 facing/랜덤 첫 오프셋, 거리순 네 이웃, walkable 및 기본 filter의 반지름0.17999999 구 충돌 부재를 검사한다. 최초 성공에서만 interact 소비/Walk를 요청하고, 최초 실패는 Emote16과 해당 하차 경로의 탑승 유지다. ExitState는 제어를 먼저 해제한 뒤 이전 tick collision world로 별도 재검사하며, 실패 시 후보 위치 쓰기만 생략한다.
- Minecart6551은 정확 authoring의 currentSpeed0/isBreaking0/maxSpeed800→MinecartCD 복사에 조건부 귀속한다. rail 후보와 정렬·회전 gate, 역입력 timer, 기본600×dt와 유효 역입력1000×dt, 마찰0 예외의 AND/OR, speed clamp와 force 쓰기를 보존했다. `!flag3 && speed>200` 가속은 현재 진행방향 또는 유일 수직 후보를 얻은 flag7=true 경로임을 정밀화했다. 실제 하차 gate는 activeVelocity 제곱길이>250000의 부정이며 등호는 허용한다. interact 좌항 소비와 하차 검사가 해당 tick의 감속보다 먼저라는 순서를 보존했다.
- 수면은210 또는322 비영이고 Sleep/Teleporting이 없을 때 Sleep push다. 진입 회복은 `!210 OR ExtraHealFromSleepPercentage!=0`, 값은 int(20+20×extra/100)이다.210 소실에 따른 pop과 진입 당시322 flag의 조건 소실/target 길이>0.1에 따른 pop을 구분한다.322 해제에는 최소수면 timer 만료가 필요하지 않다. 목표 이동0 predicate는 Sleep+210이며322 자체를 읽지 않는다. authored210 duration5를 관측5초 봉쇄로 쓰지 않았다.

생성 프레임워크는 이전256 raw history 3파일과 baseline 3파일을 정확 SHA 입력으로 받는다. raw graph의 decisionView 부재와 raw→projection의 baseline SHA `94ab86d02ae1c6b2d276e72fa1f3bb6eafac26a1a44618d9400a20b45e8e8120` 일치를 독립 확인했다. 이전 251,417개 노드가 값까지 동일하고 350,275개 간선이 모두 보존됐다. 새 origin 60개의 실제 JSON pointer와 정규 digest, 새 레코드의 모든 필드·sourceEvidence 원문·기존 노드 진입 연결을 확인했다. receipt는 자신의 status와 exact contract를 요구하고, findings 자체의 independent_pass/exact contract 및 report SHA도 검사한다.

정확 후보 3파일 hash는 `source-generation-checks.json`에 있다. 계약 pin57개와 manifest 물리 입력 2,755개를 모두 확인했다. 아직 현재 publication.json은 기존 설정이다. 새 publication.config.json SHA에 대응하는 논리 publication.json **1개만 계획된 전환**으로 구분해 검사했으며, 이미 게시됐다고 기록하지 않았다.

실제 CLI7개를 실행했다. target8373은 기존 입력 레코드와 새 pull 레코드의 각 own decision을 정확히 보존했다. 새 pull show에는 기존 입력 결과로 이어지는 continues_after 관계가 남고, 그 기존 결과의 show/source도 정확 value와 origin을 반환했다. Minecart source, Mount show,322의 기존 공통 lifecycle→새 consumer→result path도 통과했다. source-summary는 자기 ID 하위의 has_* 필드만 요약하므로 재사용 필드를 덮어쓰지 않으며, 재사용 관계를 삭제하지 않는다. 소비자는 기존 essential payload와 새 movement payload를 함께 노출한다.

공유 consumer의 link.mjs와 검증 계획도 읽었다. 최소 topics3행에 grapple_pull_movement_end→move, mount_safe_dismount/minecart_rail_brake_dismount→vehicle, condition_sleep210/322→food만 더하고 기존 build로 정확 참조와 meta.sources를 갱신하는 범위가 적절하다. G 계약에 project meta를 역참조하지 않아 순환 의존을 만들지 않는다. 실제 게시 후 accepted 두 생성의 SHA 일치·물리 pin, canonical CLI와 최종 project consumer 검증은 별도 게시 검수에서 확인해야 한다.

해결한 지적은 광차 가속 후보 설명 정밀화와 자기 필드 summary/검증 selector 한정이다. 독립 검수 스크립트도 처음에 재사용 results를 자기 필드로 세는 오류를 수정했으며, old candidate 통과 근거는 v1 파일로 보존하고 최종 계약과 혼용하지 않았다. 새 원문 전수조사·collision helper 확장·종별 전수·runtime/source/product 변경은 하지 않았다. 검수 폴더 외 파일과 세션 기록은 쓰지 않았다.
