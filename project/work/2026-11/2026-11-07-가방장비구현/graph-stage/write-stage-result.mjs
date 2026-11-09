import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),parent=path.dirname(stage),json=p=>JSON.parse(fs.readFileSync(path.join(stage,p),'utf8'));
const G='work/지식그래프',P='work/2026-11/2026-11-05-지식그래프-개편',L='work/2026-11/2026-11-05-지식그래프-실화면',T='work/2026-11/2026-11-07-가방장비구현';
const pin=json('final-pin.json'),audit=json('stage-audit.json'),queries=json('query-results.json'),commands=json('command-results.json'),code=json(P+'/B/code.graph.json'),dev=json(G+'/graph/graph.json'),product=json(L+'/D/graph.json'),evidence=json(T+'/implementation-evidence.json'),reviewed=process.argv[2]==='reviewed';
const drift=json('official-drift.json');
if([product.meta.sourceCommit,queries.sourceCommit,code.meta.sourceCommit,evidence.sourceCommit].some(s=>s!==pin.sourceCommit)||commands.length!==13||commands.some(r=>r.exitCode!==0)||queries.assertions.some(r=>!r.ok))throw Error('Final graph evidence not complete');
const state={status:reviewed?'741 staging 검증·독립 출처 검수 완료;HEAD 공식 적용 판단 대기':'741 staging 검증 완료;독립 출처 검수 마지막 확인 중',sourceCommit:pin.sourceCommit,codeExtractionCommit:pin.sourceCommit,currentProductGraphCommit:pin.sourceCommit,generatedGraphsReflectTree:true,authoringPrepared:true,stagingVerificationPassed:true,graphIndependentReview:reviewed?'통과':'마지막 확인 중',stagingReadyForRootReview:true,finalAcceptance:false,canonicalWritten:false,installed:false,liveGameObserved:false,externalModelCalled:false};
Object.assign(state,{currentOfficialHead:drift.currentOfficialHead,recordedOfficialBaseline:drift.recordedOfficialCodeBaseline,canApplyDirectlyToCurrentOfficial:false,applicationBlock:'최신 공식 생활 트리와 가방 코드를 통합한 SHA/기획 재기반 필요. 741 관측에 c4 생활 코드를 혼합하지 않음'});
fs.writeFileSync(path.join(stage,'stage-state.json'),JSON.stringify(state,null,2)+'\n');
const text=`# 가방·장비 그래프 staging 결과

2026-11-07. **741532f 격리판의 staging 생성·검증 완료. ${reviewed?'독립 출처 검수까지 완료했다.':'독립 출처 검수의 마지막 확인 중이다.'} 최신 공식c4 생활 트리가 병렬 반영되어 직접 적용은 차단했다.** 두 코드/기획을 합친 새 SHA에서 재핀·재기반해야 한다. 이 작업에서 공식 그래프·본 저장소·설치·게임·외부 모델을 변경/실행하지 않았다. 코드 기준은 \`${pin.sourceCommit}\` (${pin.repo})다. 실게임/모델의 기존 보류와 U.bag-live를 유지한다.

## 이번 판이 연결하는 것

[트리 합의](tree-agreement.md)의 인벤토리→실제 보유 분류→정확한 보유 물품→조작→필요 인자→leaf 흐름을 P.bag-item-first/P.bag-navigation에 연결했다. 뒤로·선택 전체 취소는 메뉴 상태만 바꾸며 몸 중지/단계 완료/원래 부탁 종료와 구별한다. P.bag-no-cut은 일반 가방8·비전투14·동일 목적 world/closeFight ONE quota 제거를 연결한다. 관측·소유·실행 조건, solver/물리·보고/기억·로그 및 기존 call/cost 제한과 상위 CoreEdge.Options4 소비자는 별도 보존한다.

개발의 여섯 F.bag 조작과 F.bag-navigation, 승인 정책 넷, 기존 F.plan-queue/F.action-select를 실제 메뉴·MenuMoves/TransferTick/DropTick·Apply/BuildJson·공통 제어 앵커에 연결했다. 남은 flat 후보 API 넷은 정적 원본에서 역사적 내부 함수군으로 분류했고 새 메뉴의 현재 실행 경로로 승격하지 않았다. 새 그래프 아키텍처는 만들지 않았다.

제품의 R.bag-navigation/R.bag-cap-audit/R.bag-menu-examples/R.bag-connected-trace/R.bag-review/R.bag-source-export를 통해 승인→구현 위치→게임 정적 규칙→실제 검사·검수·source 후보의 범위로 내려간다. 생성 JSON은 손으로 편집하지 않았다.

## 검증 결과

| 항목 | 고정 결과 |
|---|---|
| 코드 원본 | Git blob321파일 일치, C#102파일(제품101+기존 Editor1) 등록 |
| 파서·결정성 | Roslyn5.9/Acorn8.15, 새 추출2회의7출력 바이트 동일 |
| 생성·check | 기존13단계 전부 exit0, strict/check·제품 참조/해시·재생성 바이트 동일 |
| 대표 조회 | ${queries.assertions.length}단언 / ${queries.commands.length}조회 통과 |
| 개발 그래프 | ${dev.nodes.length}노드 / ${dev.edges.length}연결, 선 밖 코드 기존67 유지·새 증가0 |
| 제품 그래프 | ${product.nodes.length}노드 / ${product.edges.length}연결, 기획 진입점 P.release-goal 유지 |
| 근거 원본 | ${evidence.inputs.length}개 문서/로그/fixture의 실제 SHA256, connected 원본11개 raw hash 및 Git blob 텍스트 대응 |

생산자 메뉴348/0, 독립 메뉴394/0와 각각의 연결102/102를 합산하지 않았다. b1의 Core1204/protocol118/Loop55/primitive67은 당시 실행과741 관련 입력 불변 대조로 표시한다. 연결19회는 로컬 요청구성 합계이며 navigation17회·정상Swap1회, 외부 모델0·게임 consumer0이다. 실제 Menu/Surface/BuildJson/Apply/registry/InventorySkill의 연결과 대역 World/수동 결과를 구별한다. [현재 노드 표본](graph-stage/${T}/menu-examples.md)121개와 [연결 요청 표본](graph-stage/${T}/connected-menu-examples.md)19개를 보존했다.

741 자체 Unity exit0/CompileCheck:ok와 설치 없는 SDK export 근거를 고정했다. SDK 후보는101개 C#와 manifest의 소스 패키지이며 Unity compiler DLL·런타임 설치·바이너리 릴리스가 아니다. producer/export raw101일치, review raw92+CRLF/LF-only9를 별도로 기록했다. 입력 raw 바이트는 정규화해 덮지 않았다. ZIP의 SHA256는5fe44b67281e10778acfe95639f2d40611c8e6b0e62d675e341d7503903fc512다. 최종 코드/검증/빌드 범위는 [검증 원문](tree-validation.md)과 [독립 검수](review-tree.md)를 따른다.

## 최신 공식 보존과 복구

11eb 기준의 이전 staging을 파일·기록에서 복구했고 recovery-before에 당시 목록을 보존했다. 복구 시점의 공식 G/P/L·최상위 모드 제작·배포 P.release-goal·공통 실행685dde4 입력에서 가방 델타만 재적용했다. 당시 공식 기획 source.mjs의 정확한 해시3bb1c881ce0c78143c412d92d70628f21c5fd780e24a98882d16092706d08059가 staging 가방 블록 앞부분과 일치함, release 진입점과 공통 제어8앵커 보존을 단언했다.

최종 대조 중 공식 본과 G/P/L이 생활 트리c4a0f5e38016c79567f880bbaea5e56e0eeae347로 변경된 것을 발견했다. Psource의 생활 기획과 Dplanning의 AILife 코드 앵커는741 코드에 없으므로, 이를741의 코드 관측으로 혼합하지 않았다. official-drift.json에 변경${drift.changed.length}개와 직접 적용 불가를 기록하고 최신 공식의 변경 원본20개를 ${drift.capturedFolder}/에 읽기 사본으로 보존했다. 원래685 입력 해시는 canonical-before.json(${audit.canonicalInputCount}개)에 그대로 남겼다. product check의 기준741/현재 공식c4 차이는 격리 후보와 공식 본의 차이다. 세션 기록 및 새 생활 기획을 역복사로 덮지 않는다.

- 77484ec flat 전체 생성·검증은 revisions/77484ec7f563f4169600a5b01aa66487cfbbb493에 보존했다.
- 1fc89b9 flat primitive 코드 추출 및14개 중간 근거는 revisions/1fc89b93edbee31a9001a174581e32568119efc1에 보존했다.
- b1facaa 트리 체크포인트의13단계·58단언·62조회는 revisions/b1facaaef2afd66af321db66fc4b5d00725af5ce에 보존했다. 활성 착용 source-role 수정 전 판이므로741의 최종 검수로 승계하지 않는다.
- UI11eb461/리팩토링902721e/화면76563bf의 sourceCommit은 역사적으로 유지하며 현재판 실행 확인으로 승격하지 않는다. 774 Unity와 b1 export 실패도 각각 당시 근거다.

## HEAD 적용 경계와 전달물

[원본 패치 목록](graph-stage/원본-패치목록.md)의 현재 ${audit.sourcePatches.length}개 원본 후보와 baseSha256/stagedSha256를 검토한다. 그중 ${audit.sourcePatches.filter(x=>x.applyBlocked).length}개는 기준 드리프트로 diff 생성·직접 적용을 차단했다. 최신 생활 트리를 삭제하는 diff를 만들지 않는다. [적용 경계](graph-stage/패치-적용경계.md), official-drift.json, stage-audit.json, final-pin.json, command-results.json, query-results.json과 evidence/에 실제 근거가 있다. 이 폴더 전체·history/context·옛 canonical 사본을 공식에 덮어쓰지 않는다.

공식 적용 전에 최신 원본 해시를 다시 비교하고, 실제 병합 SHA/repo 및 공식 snapshot 위치에 codeRoot를 맞춰 재핀·기존13단계로 재생성한다. 생성 graph JSON을 패치로 고치지 않는다. 현재 source-patches는 검토용이며 이 작업에서는 적용하지 않았다.

독립 검수 보고서의 graph 부록을 마지막으로 추가한 뒤 그 원문을 한 번 재핀/재생성/check하고, 검수자는 읽기만 확인하는 순서로 순환 해시를 피한다. 실제 사용자 월드·외부 모델·정상 게임 소비자의 장착/보관/전달과 원래 부탁 지속은 [실게임 재개안](tree-runtime-plan.md)의 승인 후 확인 범위다.
`;
fs.writeFileSync(path.join(parent,'graph-stage-result.md'),text.replaceAll('\\`','`'));
console.log(state.status);
