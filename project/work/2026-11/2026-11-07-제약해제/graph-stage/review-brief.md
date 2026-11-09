# 새 그래프 delta 독립 검수 입구

현재 graph.md 맨 위의 source/수용 상태를 먼저 확인한다. source 후속이 예정돼 있으면 해당 후보 검수이며 최종 수용으로 표시하지 않는다.

- 방향 근거: 00-agreement.md와01-acceptance.md, 기획연결source의 P.unrestricted-*와 R.unrestriction-*.
- 현재 원본: work/2026-11/2026-11-07-기획연결/source.mjs와 지식그래프/기획의 새정책/능력. 과거4노드는지우지않고정책대체범위를표시했다.
- 코드: 최종 B/snapshot·manifest가 실제 Git blob과 같은지, raw 추출과 테스트전용 후속pin의 source동일성 기록을 확인한다.
- 증거 분리: actions43/craft29/info 자체검증, 최종통합검증과독립검수, 설치/실게임미관측을구별하는지 본다.
- 과거39가방실행증거가current로승격되지 않았는지 successor-projection과 bagImplementationEvidence의 sameRevision/evidenceAppliesToCurrentRevision=false를 본다. 같은menu blob은 정적 투영의 제한된 근거다.
- source소비자: currentpolicy3개의예전5단계/900자/40항목메모가현재제약으로표시되지않도록 후속대체note/관계가있는지 확인한다. 원규칙/원문은보존된다.
- source14정적앵커는현재기계추출심볼과Git본문을가리키며runtimeTrace=false다. 검증은graph-stage/verify-final.mjs와결과를확인한다.

명령은 work/지식그래프에서 product.mjs check, product-build.mjs --check, intent P.unrestricted-actions, source P.unrestricted-work, source pol.action-constraints-open, show R.unrestriction-actions 등을 쓴다. 과거실화면A/C3는그당시source근거로유지한다.

정본생성JSON을직접편집하지않고source/생성기에서수정한다. 제품실행코드·설치·게임·모델호출은검수범위밖이다. 검수결과는제약해제/graph-review.md에남기고HEAD에수용범위/남은문제를보고한다.
