# 네 범위 정적 보완 제안

6개 append-only 레코드로 4개 확인 공백을 보완한다. 정본 게시·제품·게임·원본·세션 기록은 변경하지 않았다. `runtimeTested=false`, `wholeScopeComplete=false`다. 사망 3개 레코드는 한 lifecycle 범위다.

| 범위 | 기존 표현 재사용 | 추가한 최소 의미 | 오행동 방지 |
|---|---|---|---|
| 번식 후속 | breed-toggle consumer·animal-feed | 대표 Cow1300:0의 실제 partner/양쪽 Meals 재검증→babyType 생성 요청·양쪽 Meals reset·variation 누적확률/부모 fallback | Breed 진입만 보고 새끼가 생겼다고 판단하거나 식사량을 재사용하지 않게 한다. |
| dash8340/8341 | equip-gear-swap consumer | offhand gate·초기 방향·condition205 timer·현재 speed/OffHand 값·충돌 보정 force·만료Walk | 장착만으로 발동했다고 보거나 고정 거리/무적 이동으로 오인하지 않게 한다. |
| miniondetonator8330/8331 | equip consumer·generic summon/dismiss tracker | 소유 tracked minion 세 플래그·death-effect gate·현재 offhand 배율→explosive damage/tileDamage 초기화 | 다른 소유자 소환수를 대상으로 잡거나 일반 해제를 폭발 성공으로 오인하지 않게 한다. |
| 플레이어 사망 lifecycle | object6002, shared inventory dispatch gate, 기존 SetAmount gate, bed/default 위치·관전·combat immunity gate | 모드별 무덤/이전, armor producer와 감산식, permakill/timer·HP/hunger/tick/condition 요청 | Casual/일반/hardcore의 손실과 복귀를 구분하고 정확히15% 비용 또는 즉시 부활을 가정하지 않게 한다. |

`supplement.json`이 통합 입력이다. `static-check.json`은 기준 그래프의 exact source ID/variation/원문 origin 및 역할·기존 값·추가 의미를 보존한다. 재사용 링크는 모두 기준 노드에 실제 존재하며 새 ID는 기준에 없다. 기준 origin JSON 포인터의 정규화 해시는 전부 일치했다. `pins.json`에는 읽은 코드·조사 근거·원문·고정 기준 그래프 해시가 있다. 줄 범위와 SHA는 각 레코드 codeReferences에 있다. 이 자기 정적검사는 독립 수용 검수가 아니다.

번식은 원문 authoring을 별도 확인한 Cow1300:0만 source binding을 부여했다. 시스템 일반성과 다른 종별 authoring 수용을 혼동하지 않는다. 6002 원본7개 variation0의 기존 역할은 그대로 비교했고 runtime player 사망/armor/respawn에 임의 source를 부여하지 않았다. 6002 source를 has_completion으로 붙이지 않고 object:6002를 생성 대상 문맥으로 연결한다. shared container 링크는 inventory validity dispatcher만 재사용하며 MoveAmount 알고리즘을 death MoveInventory와 동등화하지 않는다. armor는 weapon TryReplaceBrokenObject를 상속하지 않는다.

ECB·state·inventory·condition 요청과 실제 출생/이동/폭발/손실/복귀 성공은 구별한다. minion 실제 blast, DB primary 선택, immunity 실제 지속시간, 전체 게임 완전성은 미확정 경계다. 수면·탈것·다른 미완 후보는 확대 조사하지 않았다.
