# 채집·이동·귀환 그래프 delta 독립 검수

검수자 `/root/expedition_review`, 2026-11-08. 작성자 expedition_graph와 분리하여 실제 정본·Git·고정 원문·제품 조회를 확인했다. 제품 코드와 그래프 정본을 변경하지 않았다.

**판정: 이번 그래프 반영분 수용. 필수 수정 없음.** 사용자 조명 후속 분리 결정과 7af8f91 구현 수용을 정확히 연결한다. R1 해결, 사람과 동일한 조명 시야 완료, 이번 구현의 실게임 성공으로 승격하지 않는다.

## 직접 확인

- 공식 `C:/Dev/CoreKeeperModSDK` HEAD, 개편 B/code.graph.json, 실화면 D/graph.json의 sourceCommit이 모두 `7af8f91e5d26e4fd9ce5996809ae41e213340118`이다. 대표 `check`의 structuralErrors/stale는 빈 배열이고 baselineDrift.warning은 null이다.
- final-source-pins의 13개 사본 모두 선언 SHA256와 일치하며 검수 시점 원본 바이트와도 일치한다. 사용자 최신 결정은 실제 `source P.expedition-light-followup`에서 원문 3·7행과 해시로 소비된다. 과거 발견과 최종 검수가 같은 review 원문에 함께 보존돼도 각각의 상태와 앵커가 역사/현재 범위를 구별한다.
- 신규 30개 대응을 작성자의 ok 필드에 의존하지 않고 B 코드 노드에서 signature로 다시 찾았다. 각각 유일하며 해당 파일의 Git blob과 B/snapshot blob이 일치한다. 제품 그래프에서 정확한 from→codeId 대응이 하나씩 존재하고 모두 runtimeTrace=false 및 정확 commit을 가진다. 정적 대응을 runtime trace로 표현하지 않는다.
- 목적 `P.release-goal`에서 원래 부탁을 거쳐 `P.expedition` 및 최신 `P.expedition-light-followup`까지 실제 intent 조회로 도달한다. 신규 20개 노드 모두 최상위에서 도달 가능하다. 합의→실제 코드→오프라인·독립검수와 조명후속의 연결을 확인했다.
- 신규 20노드의 요약과 원문 범위를 읽었다. 곁일 판단을 고정 quota로 바꾸지 않으며, count 일반 목표와 사용자 max_count/max_order 소유·수명을 구별한다. 실행·관측·보고·원작업/귀환후속 설명은 앞선 독립 코드 검수 결과와 부합한다.
- 적용 전 source.mjs와 현재 정본을 직접 비교해 기존 기획 111노드·175관계의 내용이 그대로 포함됨을 확인했다. 기존 자료를 삭제하거나 병행 후속을 이번 완료로 바꾸지 않았다. 행동그래프 126판 참고는 고정 개발 근거이며 최신 전체 그래프의 완료나 런타임 성공을 뜻하지 않는다.
- 과거 가방 evidence와 connectedSourceCorrespondence 모두 sameRevision=false/evidenceAppliesToCurrentRevision=false이다. 후계 정적 대응의 AIInventoryMenu.cs는 과거39와 현재7af8f91의 Git blob이 실제로 같다. 옛 실화면/리팩터/UI 검증은 원래 revision과 제한이 제품 check에 유지된다.
- final-manifest의 정본 파일 해시와 코드 결정성 기록 7개 출력의 현재 바이트가 일치한다. 제품 `node tools/product-build.mjs --check`를 독립 실행해 `regeneration byte-identical`과 exit0을 확인했다. 변경 없는 전체 코드 추출이나 Unity를 재실행하지 않았다.

## 직접 실행 근거

[독립 검사 코드](reviewer-check.mjs), [독립 결과](reviewer-verification.json), [대표 조회 출력](reviewer-queries/)을 남겼다. 9개 읽기 조회는 모두 exit0, stderr 없음이다: intent 원정/조명후속, source 조명후속, show 귀환·게임근거·최종검수·오프라인·시야정책, check. 추가 제품 결정성 검사의 원문 출력은 `regeneration byte-identical`이다.

원본/정본 범위는 `final-manifest.json`, `final-source-pins.json`, 실제 `work/2026-11/2026-11-07-기획연결/source.mjs`, `work/지식그래프/tools/product.mjs`, 개편 B 코드층 및 실화면 D 제품층이다. 작성자 final-verification과 독립 검사 결과가 일치한다.

## 남는 범위

R1은 사용자가 이번 범위에서 분리한 미해결 조명 후속이다. CPU 근사나 AI별 오프스크린 렌더 방식을 확정한 것으로 읽히지 않는다. 코드 검수·오프라인 하네스·Unity 컴파일을 실게임의 충돌·회수·상자 소비 또는 외부 모델 판단 품질 검증으로 취급하지 않는다. 설치·실게임·외부 모델 호출 보류는 유지된다. 이 검수는 새 그래프 delta의 의미·참조·동일성·대표 소비 경로를 수용하는 것이며 변경 없는 전체 355파일 추출을 다시 검수한 것은 아니다.
