# 그래프 변경분 독립 검수 인계

공식 소스7af8f91, 제품 그래프도7af8f91이다. 변경 원본/근거/범위와 확인 결과는 final.md. 새 delta는 final-delta.json과 new-code-mappings.json, 실제 작성자 확인은 final-verification.json이다. 생성기 전체 재실행이나 구현/Unity 검수 반복 대신 새 delta의 의미·원문·정적앵커·현재판 동일성·대표 조회를 독립적으로 확인한다.

- 정본: work/지식그래프, work/2026-11/2026-11-07-기획연결, 2026-11-05-지식그래프-개편/B·C, 2026-11-05-지식그래프-실화면/B·C·D.
- 기존 source.mjs에서 최상위 목적과 P.original-request/P.execution/P.valid-action 등으로 새 원정 합의가 이어진다.
- 사용자 최신 결정은 2026-11-08-채집이동귀환-조명후속분리.md. P.expedition-light-followup 및 pol.expedition-visibility-scope가 현재 범위이고 R1은 해결이 아닌 후속이다. review/coverage/handoff의 과거 선택대기 문구를 현재 상태로 오인하지 않는다.
- 구현당시126판 행동그래프 안내를 pinned reference로 보존했다. 병행 갱신된 행동그래프는 건드리지 않았다. 사용한 정확 조회는 R.expedition-game-evidence와 graph-handoff의 source ID·경로·hash에 있다.
- 신규30정적대응은 code graph signature 유일성, 현재 Git blob=snapshot, runtimeTrace=false를 확인할 수 있다.
- 미래 max_count/max_order·원부탁 수명·실제 입력 경계는 구현사실/시험근거이며 사용자 합의와 원래 부탁 권위를 새로 정의하지 않는다. 일반 목표량 제한이나 곁일 quota를 도입한 것으로 표시하지 않는다.
- 기존39 가방 증거는 successor-projection의 동일 menu blob에만 정적 대응하며 옛 증거 적용범위를 현재7af8f91로 승격하지 않는다.
- 작성자가 실제13질의/check, 코드1회추출·동일raw 결정성, 제품결정성을 확인했다. 검수자는 자체 읽기 질의와 필요한 정확 근거를 직접 확인한다.

정본 수정이 필요하면 작성자에게 반환한다. 검수 결과는 이 graph 폴더의 review.md 또는 HEAD가 지정한 별도 경로에 기록한다.
