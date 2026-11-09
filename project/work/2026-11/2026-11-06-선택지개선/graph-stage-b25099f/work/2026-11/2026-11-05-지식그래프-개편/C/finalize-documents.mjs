import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const C=path.dirname(fileURLToPath(import.meta.url)),root=path.dirname(C);
const read=p=>fs.readFileSync(path.join(C,p),'utf8');
const write=(p,t)=>fs.writeFileSync(path.join(C,p),t,'utf8');
const append=(p,t)=>{let s=read(p).split('\n<!-- C2 final -->')[0];write(p,s+'\n<!-- C2 final -->\n'+t+'\n');};
append('기획/제품코어.md',`## 통합 확인 범위

A는 로컬 브리지 시작·상태/오류 출력만 실제 관측했다. B 기준판은 게임 모드와 목표 계획 브리지, 구독 CLI와 별도 현장 행동 판단 경로를 가진다. B-7의 ‘호스트가 AI 동료를 제어하는 모드’ 추정은 기존 의도와 맞지만 실제 플레이 가치가 이번 관측에서 달성되었다는 뜻은 아니다. CORE-A-01·BCORE-01은 이 제품코어를 대체하는 새 제품이 아니라 부분 관점이며 원본 추정으로 보존한다.

브랜딩은 여전히 미정이다. 코드의 AIPlayerSpike 명칭이나 콘솔 ai-bridge 문자열을 공식 제품명 결정으로 올리지 않는다. 게임 화면 확인은 사용자 지시로 유예했고, HTTP 연결 준비 확인은 ACT-A-01과 연결준비-통합.md에서 별도로 다룬다. 앞 C-1 문단의 A/B 수신 전이라는 표현은 전환 당시 상태이며 현재는 C-2 검토를 마쳤다.`);
append('기획/유저저니.md',`## 연결 준비 확인 분기

ACT-A-01은 운영자가 받은 health 주소로 응답 도달과 준비 보고값을 확인하는 실제 부분 흐름이다. A-3와 새 C-3 모두 준비 완료 근거·다음 조치를 얻지 못한 상태에서 관측을 끝냈다. 이를 제품 이탈·해지로 부르지 않는다. C-3은 GET 1회만 수행했고 정상 게임 진입으로 이어지지 않았다. A의 JOURNEY-A-01은 이 준비 분기로 통합하며, 연결 준비를 모든 게임 유저가 반드시 거치는 순서로 만들지 않는다.

B-7 BJOURNEY-01은 참가→목표→중단·복귀라는 사용 가능 경로로 기존 의도와 대응한다. 기지·농사·요리·생활 기억 초기화는 코드 추정에 추가로 나타났고 별도 차이 목록에 보존한다. 그것을 사용자 기획 결정이나 실제 선호/이탈 동기로 승격하지 않는다. 앞 문단에서 A-3/C-3 전 미확인으로 적었던 첫 사용 부분은 이제 HTTP에 한해 확인했고 게임 부분은 계속 유예한다.`);
write('기획/IA.md',`# IA 문서

ID: IA-01
class: 기존 의도와 실제 HTTP 출력·코드 추정 통합

게임 화면, 호스트 채팅, AI에게 전달되는 글, 개발 도구 출력이 함께 있다. 웹 URL이나 새 메뉴를 만들지 않는다. 실제 출력 원본은 A/A2/screens.md이고 C는 같은 노드를 재정의하지 않는다.

## 이번 실제 출력 목록

| 화면 ID | 출력·상태 | IA 묶음 |
|---|---|---|
| BRG-01 | 격리 브리지 시작, game graph 경고 뒤 listening | 시작·상태 확인 |
| BRG-02 | GET /v1/health, HTTP 200·ok:false | 시작·상태 확인 |
| BRG-03 | GET /, HTTP 404·not found | 요청 오류 확인 |
| BRG-04 | POST /v1/plan, HTTP 400·unknown member | 요청 오류 확인 |
| BRG-05 | POST /v1/plan, HTTP 500·bridge/parse detail | 요청 오류 확인 |

ENV-01·ENV-02는 환경 접속/패키지 오류 기록으로 제품 화면이 아니다. BRG-03은 홈 화면이 아니다. BRG-01/04/05는 탐색에서 관측했지만 정상 사용자 활동 단계로 연결하지 않았다. 목적 없는 출력이라고 판정한 것이 아니라 활동 연결 미완이다.

## 미관측·유예 목록

C-1 예약 OUT-01~06/GAME-01은 수집 계획이며 적재하지 않는다. 게임 채팅 상태·참가·목표·기지·밭·요리·자동 판단·시험·기억 삭제, 정상 plan과 기억 결과, SDK 빌드 출력은 B/inference.md의 코드 추정으로 추적한다. 실제 화면 문구·상태별 분리와 게임 유저 C-3은 사용자 지시로 유예했다. B 추정 화면을 실제 받은 원문으로 대신하지 않는다.

## 진입과 이동

호스트는 호스팅 월드에 들어온다. A가 실제 사용한 진입은 시작 콘솔과 별도로 받은 health 주소다. 전체 전역 메뉴·공통 영역·클릭 이동은 미확인이다. A-3의 루트 요청은 스스로 떠올린 탐색이지 출력의 navigate가 아니다. IA는 화면 이동 간선을 만들지 않는다.
`);
write('C2-통합판정.md',`# C-2 기존 의도·A 관측·B 코드 추정의 통합 판정

기준은 기존 기획과 인용 원문이다. A의 유저 경험은 격리 HTTP 범위에서 우선하고, 구현 사실은 e82eb87 B 코드로 확인한다. B-7의 코드 추정은 B/inference.md·inference.json 전체를 별도 보존하며 사용자 결정으로 올리지 않는다. B-7은 소스 검색 오류로 범위 밖 결과 일부가 출력된 입력 격리 사고를 스스로 기록했고, A/기존 기획을 근거로 사용하지 않았다고 보고했다. 이 제한도 보존한다.

| 매칭 대상 | 차이 | 판정과 근거 |
|---|---|---|
| 기존 core / CORE-A-01 / BCORE-01 | A는 브리지 단면, B는 코드상 제어 모드, 기존은 AI가 몸을 갖고 함께 플레이 | CORE-01에 의도와 확인 범위를 분리. 브랜딩 새 결정 없음 |
| 저니/IA | A는 health 준비 실패, B는 채팅·HTTP·개발 출력 경로 | A 실제 5종 A ID 참조. 게임/SDK는 코드 추정 보존 |
| UA-01 / BA-01 | 켜기/상태/참여 흐름 대응 | slash:ai와 StatusText 코드. 게임 실제 요소 연결 유예 |
| UA-02·03 / BA-02 | B는 보관·기지로·농사·요리까지 포함 | 추가 코드 능력을 B7-correspondence에 보존. 새 사용자 범위 결정 없음 |
| UA-04 / BA-03 | 자동 목표·지연·실패 대응이 코드에 존재 | AIPlanner 적용과 브리지 plan. 정상 외부 호출 이번 관측 없음 |
| UA-07 / BA-05 | stop은 목표 중단, off는 퇴장 | 같은 동작으로 합치지 않음. 저장 실게임은 과거 근거 |
| UA-08 / BA-07·08 | SDK/시험 출력 추정 | 개발 능력 대응. 이번 실행·설치로 쓰지 않음 |
| ACT-A-01 / BA-06 | B는 원인 구분 구조를 추정, A/C3는 다음 행동 판단 실패 | 실제 얻는 것은 보고값까지. C4 안내 미결 |
| BAPI-01 / BRG-02 | 가능한 응답 대 실제 preflight 비활성 사례 | method/path 같지만 상태 관측 범위 다름 |
| BAPI-03 / BRG-03~05 | 여러 오류를 B 한 종류로 묶음 | A의 실제 상태별 화면 유지. fallback404 독립 기능 아님 |
| BLOG-01 / BRG-01 | 시작 출력 대응 | 빈 멤버/없는 game graph 격리 실행만 확인 |
| BP-03·04 / 기존 참여·월드 정책 | 코드 규칙과 의도 대응 | 기존 의도 유지. B 정책은 별도 코드 추정 |
| BP-09 / pol.llm-subscription | 구독 검사 대응 | 사용자 원문+plan/preflight+API 키 제거 근거 |
| BP-10·11·16~19 | 사용량85%, 단일 계획, 접두사·길이·health·envelope | 코드 사실, 사용자 정책 자동 승격 없음 |
| BP-20 / BA-09 | farm/cook 잊기 코드, test 주석과 스위치 목록 불일치 | 구현만 있음/정책 의도 미정. 삭제 실행·스위치 추가 안 함 |
| Jev·반사·비용·지급·단일 참여 | 원문과 후속 메모 공존 | graph notes/applicationNote 및 차이 목록으로 시기·예외 보존 |

## 별도 보존 범위

B-7의 91노드·197간선은 별도 추정 자료다. 게임 BCH-01~10, 정상 plan BAPI-02, SDK BSDK-01~05와 BP-*는 실제 검증 완료로 적재하지 않는다. B7-correspondence.json에서 모든 ID를 A/C 대응 또는 명시적 별도 보존으로 덮는다. B graph를 복사해 현재 게임 화면을 완성했다고 하지 않는다.

BRG-01/04/05는 개발 시작·오류 입력 탐색이다. 실제 목적 있는 활동이 없다고 단정하지 않고 정상 활동 연결 미완으로 둔다. 정합성 검사 숫자를 없애려고 활동을 발명하지 않는다.
`);
append('C4-반영.md',`## 정합성 검사에서 남은 활동 연결

BRG-01/04/05는 시작·오류 입력 탐색에서 관측한 출력이며 정상 사용 활동 매핑은 없다. 후속 진단 활동 관측 거리로 보존한다. 목적 없는 출력이라고 확정하거나 검사 통과를 위해 활동을 만들지 않는다. 게임 활동 UA-01~08은 화면요소 연결을 유예/미관측으로 유지한다.`);
const inf=JSON.parse(fs.readFileSync(path.join(root,'B/inference.graph.json'),'utf8'));
const mappings={BCORE:'CORE-01',BJOURNEY:'JOURNEY-01',BIA:'IA-01','BA-01':'UA-01','BA-02':'UA-02, UA-03','BA-03':'UA-04','BA-05':'UA-07','BA-06':'ACT-A-01','BA-07':'UA-08','BA-08':'UA-08','BAPI-01':'BRG-02','BAPI-03':'BRG-03, BRG-04, BRG-05','BLOG-01':'BRG-01'};
const bi=inf.nodes??inf.graph?.nodes??[];
const rows=bi.map(n=>{const base=n.id.split('#')[0];const target=mappings[base]??Object.entries(mappings).find(([k])=>base.startsWith(k)&&['BCORE','BJOURNEY','BIA'].includes(k))?.[1]??null;return {id:n.id,type:n.type,source:n.doc??n.source??'B/inference.md#'+n.id,matchingDocumentOrObservedOutput:target,status:target?'부분 대응; 실제 상태/요소와 동일시 금지':'별도 코드 추정 보존; 사용자 기획·실제 관측으로 승격하지 않음',retainedAt:'B/inference.graph.json'};});
write('B7-correspondence.json',JSON.stringify({source:'B/inference.graph.json',allNodes:bi.length,mappings:rows},null,2)+'\n');
write('README.md',`# C — 통합 기획 결과

C-1~C-5의 현재 허용 범위를 마쳤다. 실제 게임 화면과 그 C-3은 사용자 지시로 후속 유예했다. HTTP C-3은 새 컨텍스트에서 GET 1회로 수행했고 부분 충족이었다. 새 사용자 결정이나 운영 제품 변경은 없다.

- 정본: [제품코어](기획/제품코어.md), [저니](기획/유저저니.md), [IA](기획/IA.md), 유저활동 UA-01~08 및 A의 ACT-A-01.
- [C2 통합 판정](C2-통합판정.md), [연결 근거](기획/연결근거.md), [연결 준비 통합](기획/연결준비-통합.md), [C3 실제 결과](C3/report.md), [C4 반영·미결](C4-반영.md).
- [graph-C.json](graph-C.json): 문서 선언에서 만든 제품코어·저니·IA·활동·정책. A의 화면/요소는 참조하며 복제하지 않음. D가 요청/코드 입구를 매칭하여 기능→출력 간선을 만든다.
- [legacy-capability-map.json](legacy-capability-map.json): 36 능력 전부 대응. 내부 능력을 활동이나 표준 요청 기능으로 자동 승격하지 않음. B 코드 앵커와 입구 관련성은 메타데이터이며 새 임의 간선 아님.
- 정책 원문60·후보19·문서 미결60 보존, 표준 정책43·나머지17 문서 보존 이유는 policy-disposition.json. 전체 legacy 1,551노드/10,416간선 및 게임312/관련3,381간선 보존 인벤토리 유지.
- 인용352·보존 사본 해시는 C1 점검에서 모두 일치. B7 91노드는 B7-correspondence.json으로 전부 보존/대응.
- 재생성: node C/integrate-c.mjs → node C/build-graph-C.mjs. 그래프 파서는 기획/그래프-선언.md의 JSON만 읽고 원문 위치·외부 ID·36능력 대응을 검사한다. build-c1.mjs는 초기 초안을 덮으므로 최종본에 실행하지 않는다.

남은 것: 정상 게임 화면/정상 plan·자발적 recall의 실제 성공, 시작/오류 진단 출력의 활동 연결, 복구 안내 제품 판단. 이 경계를 명시한 그래프는 조회에 사용할 수 있지만 전체 제품 실행 검증 완료는 아니다.
`);
console.log(JSON.stringify({B7Nodes:bi.length,documentsFinalized:true}));
