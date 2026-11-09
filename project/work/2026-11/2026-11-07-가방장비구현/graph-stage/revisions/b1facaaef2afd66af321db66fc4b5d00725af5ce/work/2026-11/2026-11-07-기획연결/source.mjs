// 재기획 그래프의 편집 원본. 결정의 권위는 각 evidence가 가리키는 합의 문서다.
export const entry = 'P.release-goal';
export const documents = {
 commonImplementation: {layer:'참고', path:'../2026-11-07-공통실행자료/implementation.md'},
 commonContract: {layer:'참고', path:'../2026-11-07-공통실행자료/contract.md'},
 commonOffline: {layer:'참고', path:'../2026-11-07-공통실행자료/offline-execution.md'},
 commonReview: {layer:'참고', path:'../2026-11-07-공통실행자료/review.md'},
 release: {layer:'재기획', path:'../2026-11-07-모드배포-최종목표.md'},
 experience: {layer:'재기획', path:'../2026-11-07-플레이경험과-기획큰그림.md'},
 goals: {layer:'재기획', path:'../2026-11-07-목표계층과-제약자율조정.md'},
 agency: {layer:'재기획', path:'../2026-11-07-동료형AI-계획주도권.md'},
 perspective: {layer:'재기획', path:'../2026-11-06-모드그래프-사용자관점.md'},
 actions: {layer:'재기획', path:'../2026-11-06-게임그래프-대상과행위.md'},
 cycle: {layer:'재기획', path:'../2026-11-06-행동중심모드-수행흐름.md'},
 noncombat: {layer:'재기획', path:'../2026-11-07-비전투-연속판단.md'},
 cancel: {layer:'재기획', path:'../2026-11-07-행동취소-판단주체.md'},
 common: {layer:'재기획', path:'../2026-11-07-공통실행-합의.md'},
 field: {layer:'재기획', path:'../2026-11-07-현장발견과선택지.md'},
 copper: {layer:'참고', path:'../2026-11-07-동료행동-현재부족분.md'},
 memoryReview: {layer:'참고', path:'../2026-11-07-기억시스템평가.md'},
 missing: {layer:'참고', path:'../2026-11-06-행동누락조사/03-findings.md'},
 extraction: {layer:'참고', path:'../2026-11-07-행동전체추출/01-전체추출결과.md'},
 base: {layer:'참고', path:'../2026-11-05-코어-안전지대/51-4a-오프라인LLM.md'},
 todo: {layer:'참고', path:'../../../todo.md'},
 oldGraph: {layer:'참고', path:'../../지식그래프/README.md'},
 oldCapabilities: {layer:'참고', path:'../../지식그래프/기획/기능.md'},
 oldNatural: {layer:'참고', path:'../2026-11-05-자연어-명령/0-합의.md'}
};
const ev=(doc,anchor)=>({doc,anchor});
const n=(id,title,status,summary,doc,anchor,extra=[])=>({id,title,layer:id.startsWith('R.')?'참고':'재기획',status,summary,evidence:[ev(doc,anchor),...extra]});
export const nodes = [
 n('R.common-implementation','공통 실행 685dde4 구현과 원래 부탁 보존','참고·구현 및 오프라인 검증','AI의 명시 선택으로 행동 중단·현재 단계 완료·재계획을 수행한다. 수량·시간·무진척·호출 실패는 관측으로 전달한다. ai-character 685dde4에 통합했으며 새 실게임과 설치본 배포는 수행하지 않았다.','commonImplementation','## 실행 연결'),
 n('R.stop-action','stop_action — 현재 몸 동작만 중단','참고·구현 계약','AI가 관측한 실행 회차를 명시 중단한다. 목표와 원래 부탁을 유지하고 이전 예약·보류·다음 타격을 해제하여 과거 선택이 부활하지 않게 한다.','commonContract','stop_action은 현재 몸 동작만 AI가 중단한다.'),
 n('R.finish-goal','finish_goal — 현재 계획 단계 완료','참고·구현 계약','AI가 현재 단계를 완료한다. 기존 다음 단계가 있으면 이어지고 마지막 단계면 상위에 보고한다. 원래 부탁의 완료는 상위 order=done이 결정한다.','commonContract','finish_goal은 현재 작업 단계를 AI가 완료 처리한다.'),
 n('R.request-plan','request_plan — 행동을 유지하며 상위 재계획 요청','참고·구현 계약','현재 관측과 이유를 상위에 전달하고 응답을 기다리는 동안 실행 가능한 몸 동작을 유지한다. 요청이 진행 중이면 후속 사실을 합친다.','commonContract','request_plan은 현재 관측과 실행 이유를 상위 LLM에게 전달한다.'),
 n('R.common-offline','공통 실행 실제 코드 입구 오프라인 검증','참고·오프라인 실행','기준판의 10/10 강제 취소를 3개 확인으로 재현하고 수정본의 실제 메서드 입구·종료 콜백·예약 흐름 31개를 확인했다. 게임 상태와 몸 입력은 fixture이며 전체 게임 물리·원격 모델 시험은 범위 밖이다.','commonOffline','## 확인 결과',[ev('commonOffline','## 실제 실행한 소스와 경계')]),
 n('R.common-review','공통 실행 독립 검수','참고·독립 검수','수량·시간·실패에 따른 정책 취소와 늦은 응답·중단 후 예약 재시작 경합을 실제 소비 경로에서 대조했다. 전체 컴파일 및 회귀 1141개, 실제 코드 입구 31개를 확인한 수용 기록이다.','commonReview','## 판정',[ev('commonReview','## 검증 증거와 경계')]),
 n(entry,'Core Keeper 모드로 만들어 배포','합의','AI 단톡방 멤버들과 게임 안에서 자연어로 협력하고 함께 생활하는 경험을 Core Keeper 모드로 만들어 배포하는 것이 제작자의 최종 목표다.','release','이거 코어키퍼 모드로 만들어서 배포까지 하는게 최종 목표야.',[ev('release','AI 단톡방 멤버들이 게임 안에서 동료로 함께 플레이하는 경험을 Core Keeper 모드로 만들고 배포하는 데까지가 프로젝트의 범위다.'),ev('experience','사용자가 원하는 경험은 단순한 봇이 아니라')]),
 n('P.experience','동료·친구와 협력하고 생활하는 경험','합의','자연어로 소통하며 사용자와 함께 협력하고 생활하는 경험을 기획 판단 기준으로 삼는다.','experience','사용자가 원하는 경험은 단순한 봇이 아니라'),
 n('P.planning-order','전체 그림을 먼저, 항목별 세부는 그다음','합의','한 항목의 세부 규칙에 먼저 깊게 들어가지 않는다.','experience','먼저 대략적인 전체 그림을 그리고'),
 n('P.axes','전체 기획의 여덟 축','제안','정체성 / 세계 인식과 지식 / 생활 목표와 자율성 / 행동과 활동 범위 / 관계와 공동생활 / 기억과 성장 / 사용자와의 접점 / 생활의 연속성. 검토용 구성이며 각 축의 제품 정책은 미확정.','experience','전체 기획을 바라보는 축으로'),
 n('P.life','자율 생활과 사용자 도움을 함께 이어감','합의','부탁이 없어도 자기 생활·탐험을 이어 가며 요청이 있으면 돕는다. 별도 생활 모드로 나누지 않는다.','goals','AI는 부탁이 없어도 자기 생활과 탐험을 이어 가며'),
 n('P.hierarchy','생활 방향 → 프로젝트 → 작업 → 행동','합의','지속되는 큰 방향과 현재 완성할 결과, 수행 작업을 연결한다.','goals','생활 방향 → 현재 프로젝트 → 당면 작업 → 실제 행동'),
 n('P.role','프로젝트 이후에도 남는 개인 담당 역할','합의','사용자 지시가 공동 목표·개인 역할·행동 제약을 함께 바꿀 수 있다. 역할은 프로젝트 완료 후에도 남는다.','goals','지속 역할은 개별 프로젝트가 완료된 뒤에도 남는다.'),
 n('P.constraint','게임 내 활동 제약의 자율 조정','합의','타당한 경우 LLM이 먼저 알린 뒤 게임 내 활동 제약을 조정한다. 개발 작업의 승인 규칙을 뜻하지 않는다.','goals','사용자에게 받은 게임 내 활동 제약도 LLM이 타당성을 판단해 풀 수 있게 한다.'),
 n('P.base-example','코어를 베이스로 예쁘게 가꾸기','제안','기본 생활 방향의 후보다. 최종 기본 목표 문구나 실제 캐릭터 설정으로 확정하지 않았다.','goals','기본 생활 방향의 후보로 코어를 베이스로 예쁘게 가꾸기를 제안했다.'),
 n('P.continuity','진행 유지·중단 후 재개·휴식','제안','현재 프로젝트와 진행을 유지하고 필요만큼 계획하며 쉴 수 있는 방향을 논의했다. 상태 구조는 미정.','goals','현재 프로젝트와 진행 상황을 유지하고'),
 n('P.agent','모드를 직접 사용하는 AI 에이전트','합의','모드는 AI가 세계를 인식하고 판단 정보를 얻으며 몸으로 행동하는 능력을 제공한다. 최종 플레이 경험의 수혜자는 사용자다.','perspective','이 모드를 직접 사용하는 주체는 AI 에이전트다.'),
 n('P.interface','AI가 정보·행동·결과를 주고받는 인터페이스','합의','사람의 관찰·지시 UI는 보조 흐름이다. 구체 API는 미정.','perspective','화면: AI 에이전트가 정보를 받고 행동을 선택하는 인터페이스.'),
 n('P.cycle','판단 → 동적 계획 → 실행 → 결과 갱신','합의','현재 상태에서 목표를 정하고 준비·이동·행동의 순서와 분기를 구성한 뒤 결과로 다음 판단을 잇는다.','cycle','## 합의한 수행 순환'),
 n('P.agency','위쪽 LLM이 목적·작업 계획과 중요한 변경을 주도','합의','목적지·대상·수량·준비·순서·대안을 작업 계획으로 정한다. 비전투 작업 중의 구체 행동과 다음 대상 선택은 Decisions가 맡는 것으로 보완했다.','agency','LLM이 목적지, 작업 대상과 수량, 필요한 준비, 작업 순서와 대안을 정한다.',[ev('noncombat','위쪽 LLM은 목적·계획과 중요한 변경을 맡고')]),
 n('P.execution','코드가 선택된 행동의 몸 입력과 실행을 담당','합의','길 찾기·이동 입력·채굴·타격·제작 요청과 결과 확인을 수행한다. 비전투에서는 넓은 구역을 코드가 자율 처리하도록 묶기보다 Decisions의 연속 선택과 매끄러운 실행 전환을 우선한다.','agency','코드는 지정된 작업의 길 찾기, 이동 입력, 채굴·타격, 제작 요청과 결과 확인을 담당한다.',[ev('noncombat','코드는 선택된 행동의 몸 입력과 실행을 담당한다.')]),
 n('P.noncombat-choice','Decisions가 비전투 구체 행동을 연속 판단','합의','갱신된 상황을 보고 계속하기·대상 변경·이동·중단 등을 반복 판단한다. 위쪽 LLM의 목표·작업 계획 주도권을 유지한다. 호출 증가 자체나 고정 주기를 목표로 삼지 않는다.','noncombat','Decisions는 갱신된 상황을 보고 계속하기·대상 변경·이동·중단 등을 판단한다.'),
 n('P.noncombat-continuity','판단 주기와 몸의 멈춤을 분리','합의','유효한 현재 행동은 새 판단 응답을 기다릴 때마다 멈추지 않고 이어 간다. 새 선택이 달라지면 전환할 수 있어야 한다.','noncombat','판단하는 주기와 몸이 행동을 멈추는 시점을 분리한다.'),
 n('P.noncombat-next','다음 대상도 Decisions가 선택·미리 준비','합의','현재 작업이 끝나기 전에 다음 선택을 준비해 완료 후 판단 대기 공백을 줄인다. 실행 가능성의 전환 시점 확인 방법은 후속 설계다.','noncombat','다음 대상을 고르는 판단도 Decisions에 둔다.'),
 n('Q.noncombat-rules','비전투 호출·다음 선택 준비·전환의 세부','미정','호출 간격·사건 조건·상한, 다음 선택 준비 시점과 변화 전 세계 상태 처리 등은 세부 설계 사항이다. 후속 공통 실행 합의에서 유효한 선택의 유지·중단 주체 및 원래 부탁의 종료 주체를 정했다. 2분 주기는 당시 구조의 설명이다.','noncombat','## 남은 설계 사항',[ev('cancel','현장 행동의 중단·전환은 Decisions가 담당하고'),ev('common','## 구현 중 정한 공통 입구')]),
 n('P.observation','현재 관찰','합의','주변 대상·가방·체력·배고픔·장비 상태·작업 진행을 판단에 제공한다.','agency','현재 관찰: 주변 대상, 가방, 체력·배고픔, 장비 상태와 작업 진행.'),
 n('P.spatial','공간 기억','합의','기억한 자원·제작대·상자 위치와 마지막 관측 정보.','agency','공간 기억: 기억한 자원·제작대·상자의 위치와 마지막 관측 정보.'),
 n('P.events','경험 기억','합의','과거 목표 결과·막힌 이유와 장소·적·동료·당시 조건.','agency','경험 기억: 과거 목표 결과, 막힌 이유, 관련 장소·적·동료와 당시 조건.'),
 n('P.knowledge','캐릭터에게 열린 게임 지식','합의','제작법과 행동 조건을 판단에 제공한다. 개발자가 조사한 게임 전수 자료와 캐릭터가 사용할 수 있는 지식을 동일시하지 않는다.','agency','게임 지식: 캐릭터에게 열린 제작법과 행동 조건.'),
 n('P.result','실행 결과로 다음 판단을 갱신','합의','완료 여부·실제 확보량·막힌 이유·새 발견·상태 변화에 따라 계속·중단·순서 변경을 결정한다.','agency','실행 결과: 완료 여부, 실제 확보량, 진행하지 못한 이유, 새 발견과 상태 변화.'),
 n('P.opportunity','현장 기회를 보고 계획을 수정','합의','원래 부탁·계획을 유지하며 추가 채집·조사·위치만 기억·귀환을 비교한다. 목표 수량 충족은 사실이고 파밍 종료·귀환은 LLM의 결정이다.','agency','목표 수량 충족은 코드가 확인할 수 있는 사실이다.'),
 n('P.action-unit','행동 = 대상 × 행위','합의','입력·행동·목표를 구분하며 실패한 시도도 행동이다. 모든 대상×행위 조합이 가능한 것은 아니다.','actions','**행동 = 대상 × 행위**'),
 n('P.game-first','게임 행동 체계부터 모드 능력과 비교','합의','게임의 대상·행위·가능 조건을 정리한 뒤 모드 제공 능력·누락과 비교한다. 현행 코드가 게임 행동 목록을 제한하지 않는다.','actions','모드의 현재 코드·선택지에 앞서 코어키퍼 게임 자체의 그래프를 만든다.'),
 n('P.confidence','기억 신뢰도 하락 요청','합의','자동 기록을 유지하고 실제 확인 결과가 특정 기억과 다를 때 LLM이 신뢰도 하락을 요청한다. 자유로운 교훈 작성·관계 편집까지 이번 범위에 넣지 않는다.','agency','이번 기억 편집 범위는 LLM이 실제 경험과 대조해 특정 기억의 신뢰도를 낮추도록 요청하는 것이다.'),
 n('P.inactive','기준 미달 기억을 복구 가능한 비활성 상태로','합의','일반 검색·목차·판단·행동 근거에서 제외하되 필요시 확인·복구할 수 있게 남긴다. 재접속·재구성에도 비활성 상태를 보존한다.','agency','비활성 정보는 일반 검색·목차·판단과 행동 후보의 근거에서 제외한다.'),
 n('P.memory-guards','확인 결과·현재 상태·과거 사건을 구분','합의','중복 감점 방지, 확인 실패와 틀림 구분, 새 관측의 갱신/복구, 현재 상태와 과거 사건 구분. 확정 관측으로 역할이 끝난 공간 항목 삭제는 별개다.','agency','같은 확인 결과를 반복해서 감점하지 않는다.'),
 n('P.iron-example','철검 제작 부탁의 연결 흐름','설명용 사례','조건·자원 확인 → 기억에서 재료 위치 조회 → 작업 순서 결정 → 현장 기회 판단 → 결과에 따라 수정 → 제작 완료. 현행 지원·성공을 입증한 사례가 아니다.','agency','예를 들어 철검 제작을 부탁받으면'),
 n('Q.identity','정체성·성격·관계의 구체 규칙','미정','친구 같은 경험을 합의했지만 구체 성격·관계 규칙은 확정하지 않았다.','experience','구체적인 구현 방식이나 성격·관계 규칙까지 이번 문장으로 확정한 것은 아니다.'),
 n('Q.goal-rules','기본 목표·프로젝트 선택·조율·완료 규칙','미정','기본 생활 목표 문구, 공동 프로젝트와 역할 조율, 선택·중단·재개·완료 판정의 세부.','goals','공동 프로젝트와 개인 역할의 조율, 프로젝트 선택·중단·재개 및 완료 판정의 구체 규칙.'),
 n('Q.constraint-rules','제약 조정의 타당성·범위·통지 실패','미정','판단 기준, 일시 예외/지속 변경, 종료·복귀 조건, 누구에게 어떤 경로로 알리는지와 전달 실패 처리.','goals','제약을 풀 타당성의 판단 기준:'),
 n('Q.tools','조회·계획·실행 API와 재판단 조건','미정','LLM 역할은 합의됐지만 구체 도구 API와 호출 조건·빈도는 아직 정하지 않았다.','agency','조회·계획·작업 실행 인터페이스와 계획 재판단 조건.'),
 n('Q.memory-rules','신뢰도 수치·식별·복구 규칙','미정','초기값·감점·기준·복구, 기억 식별과 동일 확인 결과 중복 반영 방지의 구현 방법.','agency','신뢰도 초기값, 감점 크기, 비활성화 기준과 새 관측에 따른 복구 규칙.'),
 n('Q.autonomy','개인 목표·성격·요청 거절 범위·호출 비용','미정','자율성의 상세 범위와 비용 한도는 후속 설계 대상이다.','agency','개인 목표·성격·사용자 요청을 미루거나 거절할 수 있는 범위와 호출 비용 한도.'),
 n('R.memory','현행 기억 시스템 평가','참고·평가','자동 기록·공간/사건/게임 지식 구분은 유지할 만하나 실제 조회와 행동 변화는 완성으로 판정하지 않았다. 새 계획 주도권 합의의 구현 증거가 아니다.','memoryReview','검색 구현의 정상 동작과 모델이 필요한 때 검색하는 능력은 별도로 판정한다.'),
 n('R.missing','현행 행동 누락 조사','참고·코드 조사','b25099f 기준 장비 선택·정리·보급·수리 및 일반 목표 연결 공백. 새 기획의 우선순위나 구현 승인이 아니다.','missing','현행 봇은 필요한 물건을 챙기고'),
 n('R.extraction','게임 행동 규칙 전체 원본 적용','참고·정적 추출','3,235 source에 검수 규칙을 적용해 1,432행, 요리 6,561쌍. 모든 가능한 게임 행동의 전수 발견이나 실제 월드 성공은 아니다.','extraction','게임의 모든 가능한 행동을 전수 발견했다는 뜻은 아니다.'),
 n('R.base','기지 안전 4a 부분 확인·보류','참고·개발 중','base-4a b016a5c의 부분 오프라인 LLM 확인. 새 호출을 멈췄고 실게임/병합 완료로 보지 않는다. 이번 재기획 연결이 재개 지시는 아니다.','base','**중간에 멈춤**'),
 n('R.chapters','함께 한 일의 장 요약','참고·과거 후속 아이디어','큰 사건 때 장 요약을 쌓는 과거 미완료 할 일. 새 기획의 확정 요구로 승계하거나 폐기하지 않는다.','todo','LLM 기억 다음 단계:'),
 n('R.memo','LLM 교훈·해석 메모','참고·과거 후속 아이디어','근거 사건에 기대는 교훈·지지·의심의 과거 후속 단계. 현재 신뢰도 하락 범위 밖이며 이번 정리가 승인·취소를 뜻하지 않는다.','todo','기억 그래프에 LLM이 쓰는 해석 얹기'),
 n('R.mention','AI끼리 협력·위험 멘션 호출','참고·과거 후속 아이디어','행동이 필요한 말의 AI 간 멘션 호출, 잡담 제외와 반복 호출 방지 세부를 후속으로 남긴 과거 기록.','todo','AI끼리 멘션 호출 규칙 넣기'),
 n('R.playtest','실플레이 로그와 사용자 정정','참고·과거 후속 계획','사용자가 이상 장면을 짚으면 관찰·판단·실행 기록으로 구분하고, 이후 정보 정정 반영을 확인하는 과거 후속 계획.','todo','실제 플레이하며 다듬기'),
 n('R.natural','과거 자연어 명령 기획','참고·과거 합의','멘션·우선 지시·미루기/복귀·호출 주기·여러 단계 계획의 기존 설계. 세부를 새 기획 정책으로 자동 승계하지 않는다.','oldNatural','## 조건 (사용자 결정)'),
 n('R.graph','기존 개발·제품 지식그래프','참고·색인','기존 기획과 당시 구현·검증 근거를 조회하는 입구. 각 기록의 시점과 지위를 따른다.','oldGraph','# 코어키퍼-AI-캐릭터 개발 지식그래프'),
 n('P.original-request','중간 단계가 끝나도 원래 부탁을 유지','합의','현장 행동의 중단·전환은 Decisions가 고르고, 원래 부탁의 종료와 중요한 계획 변경은 위쪽 LLM이 결정한다. 구리 10/10은 재료 수량을 달성했다는 관측이다.','cancel','현장 행동의 중단·전환은 Decisions가 담당하고',[ev('cancel','구리 10/10은 관측된 수량 달성이다.'),ev('common','중간 단계 진척과 사용자 원래 부탁을 보존한다.')]),
 n('P.valid-action','AI가 선택한 실행 가능한 행동을 판단 대기 중에도 지속','합의','수량·시간·진척·호출 실패를 관측으로 넘긴다. 대상이 사라지면 이유를 보고하며 새 AI 선택과 사용자의 직접 중지를 따른다.','cancel','대상 소실로 실행할 수 없어진 경우에는',[ev('common','AI가 명시적으로 계속·중단·전환할 수 있게 한다.'),ev('common','새 응답을 기다리는 동안 실행 가능한 선택을 이어 간다.')]),
 n('P.field-choice','원래 부탁을 기억하며 현장 기회·위협에 대응','합의','탐색·추가 채집·발견 기록·무기 전환·강화 후 복귀는 상황에 따라 고를 수 있는 행동이다.','field','동료는 원래 부탁을 기억하면서 주변의 기회와 위협에 대응해 행동을 바꿀 수 있다.'),
 n('R.copper','구리 10개 뒤 갑옷 부탁이 완료로 기록된 장면','참고·기존 실게임 관측','2026-11-07 조사에서 ai-character 11eb461 코드와 보존된 실게임 로그를 대조했다. 구리 10개 확보 뒤 다음 파기가 취소되고 갑옷 부탁도 완료로 기록됐다가 상위 재계획으로 다시 진행됐다. 별도 4a 개발판은 조사 범위 밖이며, 개선 후 동작은 실게임 미확인이다.','copper','구리 갑옷을 부탁받은 Gemini가',[ev('copper','제품 저장소 ai-character 11eb461의 후보 생성·조립·실행·상위 계획 경로와 보존된 실게임 로그를 대조했다.')]),
 n('R.capabilities','기존 행동·계획·기억 능력 색인','참고·구현 색인','F.plan-queue, F.memory-recall, F.craft, F.executor 등. 항목마다 구현/계획 상태와 기준판이 다르므로 원문에서 확인한다.','oldCapabilities','### F.plan-queue')
];
// 관계의 class=해석은 원문들을 읽기 쉽게 엮은 연결이며 새 제품 결정이 아니다.
const e=(from,to,type,doc,anchor,classification='해석')=>({from,to,type,class:classification,evidence:[ev(doc,anchor)]});
export const edges = [
 e('P.valid-action','R.common-implementation','참고: 합의를 구현한 코드 기준','commonImplementation','## 실행 연결'),
 e('P.original-request','R.finish-goal','참고: 단계와 부탁 종료의 구현','commonContract','finish_goal은 현재 작업 단계를 AI가 완료 처리한다.'),
 e('P.valid-action','R.stop-action','참고: 명시 중단 구현','commonContract','stop_action은 현재 몸 동작만 AI가 중단한다.'),
 e('P.valid-action','R.request-plan','참고: 재계획 요청 구현','commonContract','request_plan은 현재 관측과 실행 이유를 상위 LLM에게 전달한다.'),
 e('R.common-implementation','R.common-offline','참고: 실제 코드 입구 검증','commonOffline','## 확인 결과'),
 e('R.common-implementation','R.common-review','참고: 독립 검수 결과','commonReview','## 판정'),
 e('P.experience','P.planning-order','기획 진행 원칙','experience','## 기획 진행 방식','합의'),
 e('P.experience','P.axes','검토할 기획 축','experience','전체 기획을 바라보는 축으로','제안'),
 e('P.experience','P.life','생활 경험으로 구체화','goals','AI는 부탁이 없어도 자기 생활과 탐험을 이어 가며'),
 e('P.experience','P.agent','실현하는 모드의 직접 소비자','perspective','이 모드를 직접 사용하는 주체는 AI 에이전트다.'),
 e('P.experience','Q.identity','남은 기획 질문','experience','구체적인 구현 방식이나 성격·관계 규칙까지'),
 e('P.life','P.hierarchy','지속 방향과 일을 연결','goals','지속되는 큰 방향 아래 현재 완성할 결과와 수행 작업을 연결','합의'),
 e('P.life','P.role','지속 역할을 함께 적용','goals','개인 담당 역할과 행동 제약은 프로젝트 선택 및 실행 방법에 함께 적용되는 조건이다.','합의'),
 e('P.role','P.hierarchy','프로젝트 선택·실행 조건','goals','개인 담당 역할과 행동 제약은 프로젝트 선택 및 실행 방법에 함께 적용되는 조건이다.','합의'),
 e('P.constraint','P.hierarchy','프로젝트 선택·실행 조건','goals','개인 담당 역할과 행동 제약은 프로젝트 선택 및 실행 방법에 함께 적용되는 조건이다.','합의'),
 e('P.life','P.constraint','활동 제약도 판단 대상','goals','사용자에게 받은 게임 내 활동 제약도 LLM이 타당성을 판단해 풀 수 있게 한다.','합의'),
 e('P.hierarchy','P.base-example','생활 방향 후보','goals','기본 생활 방향의 후보로','제안'),
 e('P.hierarchy','P.continuity','유지·재개 방향','goals','현재 프로젝트와 진행 상황을 유지하고','제안'),
 e('P.hierarchy','Q.goal-rules','세부 미정','goals','공동 프로젝트와 개인 역할의 조율'),
 e('P.constraint','Q.constraint-rules','세부 미정','goals','## 추가로 구체화할 사항'),
 e('P.hierarchy','P.cycle','작업을 실제 수행으로 연결','goals','LLM이 계획과 변경을 판단하고 코드는 행동을 실행한다는 기존 합의를 유지한다.'),
 e('P.agent','P.interface','정보·선택·결과를 받음','perspective','화면: AI 에이전트가 정보를 받고 행동을 선택하는 인터페이스.','합의'),
 e('P.interface','P.cycle','목표 수행 순환을 제공','perspective','주변 상태 파악 → 필요한 정보 조회 → 행동 선택·실행 → 결과 확인 → 다음 행동 결정'),
 e('P.cycle','P.agency','11/07에 판단·계획 주체 구체화','agency','LLM이 기억과 관찰을 바탕으로 계획을 세우고 수정하며'),
 e('P.cycle','P.execution','11/07에 실행 역할 구체화','agency','코드는 실제 행동을 수행한다.'),
 e('P.agency','P.observation','판단에 조회·사용','agency','현재 관찰:','합의'),
 e('P.agency','P.spatial','판단에 조회·사용','agency','공간 기억:','합의'),
 e('P.agency','P.events','판단에 조회·사용','agency','경험 기억:','합의'),
 e('P.agency','P.knowledge','판단에 조회·사용','agency','게임 지식:','합의'),
 e('P.agency','P.execution','의미 있는 작업을 지정','agency','LLM은 필요한 정보를 조회하고, 대상과 장소를 지정해 작업을 실행하며','합의'),
 e('P.agency','P.opportunity','현장 기회를 판단','agency','원래 부탁과 현재 계획을 유지하면서 현장에서 발견한 기회도 판단한다.','합의'),
 e('P.execution','P.result','실제 결과를 반환','agency','실행 결과:','합의'),
 e('P.result','P.agency','계속·중단·순서 변경','agency','결과에 따라 계속·중단·순서 변경을 결정','합의'),
 e('P.result','P.cycle','다음 판단으로 순환','cycle','결과를 상태에 반영하고 다음 판단으로 돌아간다.','합의'),
 e('P.execution','P.action-unit','제공할 행동의 단위','cycle','기능: AI에게 제공할 게임 행동.'),
 e('P.action-unit','P.game-first','조건과 가능한 조합을 조사','actions','대상과 행위를 조합해 가능한 행동을 확인하고','합의'),
 e('P.agency','P.confidence','실제 경험과 기억 대조','agency','LLM이 특정 기억과 실제 확인 결과가 다르다고 판단한다.','합의'),
 e('P.confidence','P.inactive','기준 미달 시','agency','신뢰도가 기준 아래로 내려가면 정보를 비활성화한다.','합의'),
 e('P.inactive','P.spatial','일반 판단 근거에서 제외·복구','agency','비활성 정보는 일반 검색·목차·판단과 행동 후보의 근거에서 제외한다.'),
 e('P.inactive','P.events','일반 판단 근거에서 제외·복구','agency','비활성 정보는 일반 검색·목차·판단과 행동 후보의 근거에서 제외한다.'),
 e('P.confidence','P.memory-guards','갱신 시 지킬 조건','agency','다음 조건을 지킨다.','합의'),
 e('P.confidence','Q.memory-rules','세부 미정','agency','신뢰도 초기값, 감점 크기'),
 e('P.agency','Q.tools','도구·재판단 세부 미정','agency','조회·계획·작업 실행 인터페이스와 계획 재판단 조건.'),
 e('P.interface','Q.tools','API 미정','agency','도구의 세부 API는 아직 정하지 않았다.'),
 e('P.life','Q.autonomy','자율성 세부 미정','agency','개인 목표·성격·사용자 요청을 미루거나 거절할 수 있는 범위와 호출 비용 한도.'),
 e('P.agency','P.iron-example','설명용 시나리오','agency','예를 들어 철검 제작을 부탁받으면','사례'),
 e('P.iron-example','P.spatial','재료 위치를 조회','agency','기억에서 재료 조달 위치를 조회하여','사례'),
 e('P.iron-example','P.execution','계획을 실제 작업으로','agency','예상과 다른 결과가 나오면 계획을 수정하고 제작 완료까지 이어 간다.','사례'),
 e('P.agency','R.memory','참고: 현행 기억 평가','memoryReview','자동 기록과 공간 상태·사건 이력·게임 지식의 구분은 유지할 만하다.'),
 e('P.execution','R.missing','참고: 현행 행동 공백','missing','실행 기술 자체가 없는 경우와'),
 e('P.game-first','R.extraction','참고: 정적 행동 추출 자료','extraction','기존 자료와의 관계'),
 e('P.hierarchy','R.base','참고: 진행 중 기지 작업','base','**중간에 멈춤**'),
 e('P.events','R.chapters','참고: 과거 후속 아이디어','todo','LLM 기억 다음 단계:'),
 e('P.confidence','R.memo','참고: 현재 범위 밖의 과거 후속','agency','자유로운 교훈 작성이나 관계 편집 체계까지 확장하지 않는다.'),
 e('P.life','R.mention','참고: 과거 협력 호출 아이디어','todo','AI끼리 멘션 호출 규칙 넣기'),
 e('P.result','R.playtest','참고: 과거 실플레이 검증 계획','todo','실제 플레이하며 다듬기'),
 e('P.life','R.natural','참고: 과거 자연어 지시 설계','oldNatural','## 목표'),
 e('P.agent','R.graph','참고: 기존 그래프의 관점 차이','perspective','현재 제품 그래프의 사용자 활동·화면 층은 사람이 게임 HUD·채팅창·상태 출력을 보고 AI를 확인하는 흐름에 중심을 두었다.'),
 e('P.execution','R.capabilities','참고: 기존 능력 색인','oldCapabilities','### F.plan-queue'),
 e('R.capabilities','R.graph','참고 색인 소속','oldGraph','기획/기능.md'),
 e('R.memory','R.capabilities','참고 구현 위치','oldCapabilities','### F.memory-recall')
 ,e('P.agency','P.noncombat-choice','비전투의 구체 선택 주체를 보완','noncombat','위쪽 LLM은 목적·계획과 중요한 변경을 맡고, Decisions API는 작업 중의 구체적인 행동 선택을 반복해서 맡는다.','합의')
 ,e('P.noncombat-choice','P.execution','선택한 행동을 코드가 실행','noncombat','코드는 선택된 행동의 몸 입력과 실행을 담당한다.','합의')
 ,e('P.noncombat-choice','P.noncombat-continuity','응답 대기와 실행을 분리','noncombat','유효한 현재 행동은 새 판단 응답을 기다릴 때마다 멈추지 않고 이어 간다.','합의')
 ,e('P.noncombat-choice','P.noncombat-next','다음 대상 선택도 담당','noncombat','다음 대상을 고르는 판단도 Decisions에 둔다.','합의')
 ,e('P.noncombat-choice','Q.noncombat-rules','호출·선택 세부 미정','noncombat','Decisions의 호출 간격·사건별 호출 조건·비용 및 호출 상한.')
 ,e('P.noncombat-next','Q.noncombat-rules','준비·전환 세부 미정','noncombat','다음 선택을 준비할 시점과 아직 바뀌지 않은 세계 상태를 다루는 방법.')
 ,e('P.noncombat-continuity','Q.noncombat-rules','전환·중단 세부 미정','noncombat','완료·새 지시·위험·막힘·오래된 응답 때의 전환과 중단 규칙.')
 ,e('P.result','P.noncombat-choice','갱신된 상황으로 계속 판단','noncombat','갱신된 상황을 바탕으로 연속 판단하는 데 목적을 둔다.'),
 e('P.hierarchy','P.original-request','단계와 원래 부탁의 완료 구분','common','구리 확보가 갑옷 부탁 완료로 처리되지 않아야 한다.','합의'),
 e('P.noncombat-continuity','P.valid-action','유효한 선택의 유지·취소 기준','cancel','현재 행동을 계속할지, 스스로 취소할지, 다른 행동으로 전환할지는 AI가 선택한다.','합의'),
 e('P.original-request','P.valid-action','원래 부탁과 현재 행동의 판단 주체 구분','cancel','현장 행동의 중단·전환은 Decisions가 담당하고','합의'),
 e('P.opportunity','P.field-choice','원래 부탁을 보존하며 현장 대안 판단','field','이 사례들은 상황에 따라 고를 행동이다.','합의'),
 e('P.original-request','R.copper','참고: 과거 중간 완료 관측','copper','구리 갑옷을 부탁받은 Gemini가'),
 e(entry,'P.experience','모드가 제공할 플레이 경험','release','모드가 제공할 경험은','합의'),
];

// Approved bag/equipment scope; current implementation evidence remains a reference.
Object.assign(documents,{bag:{layer:'재기획',path:'../2026-11-07-가방장비구현/agreement.md'},bagPlan:{layer:'참고',path:'../2026-11-07-가방장비-계획안.md'},bagInvest:{layer:'참고',path:'../2026-11-07-가방장비-조사.md'},bagSpec:{layer:'참고',path:'../2026-11-07-가방장비구현/inventory-spec.md'},bagStatus:{layer:'참고',path:'../2026-11-07-가방장비구현/implementation-status.md'},bagRules:{layer:'참고',path:'../2026-11-07-가방장비구현/transfer-rules.md'}});
nodes.push(n('P.bag-equipment','가방·장비 선택과 실제 결과를 다음 판단으로 연결','합의','승인된 장착·손선택·수량 이동·전달 순서를 정상 게임 입력과 결과 관측으로 연결한다. 공통 실행을 중복 구현하지 않으며 원래 부탁과 진척을 보존한다.','bag','- 결과:'),n('P.bag-agency','목적·현장 선택·입력 관측의 담당 유지','합의','자동 최적 장비를 추가하지 않는다. 위쪽 LLM 목적·계획, Decisions 구체 선택, 코드 입력·관측 경계를 유지한다.','bag','- 판단 주체:'),n('R.bag-validation','이번 가방·장비 고정판 검증 범위','참고·컴파일/오프라인, 미실게임','최종 커밋과 실행한 검사만 연결하며 다른 후보의 수용·과거 실게임을 승계하지 않는다.','bagStatus','# 가방·장비 구현 검증 범위'));
edges.push(e('P.field-choice','P.bag-equipment','구체 승인 범위','bag','- 결과:','합의'),e('P.original-request','P.bag-equipment','부탁·진척 보존','bag','- 공통 실행:','합의'),e('P.bag-equipment','P.bag-agency','판단 역할 유지','bag','- 판단 주체:','합의'),e('P.bag-agency','P.noncombat-choice','현장 선택 담당','bag','- 판단 주체:','합의'),e('P.bag-equipment','R.bag-validation','참고: 이번 고정판 검증 범위','bagStatus','# 가방·장비 구현 검증 범위'));

nodes.push(n('R.bag-rules','장착·상자 이동·드롭·수령의 게임 정적 규칙','참고·게임 소비자 정적 조사','장착/상자 이동의 검수된 정적 규칙과 정상 UI DropItem/자동 줍기 소비 경로다. 각 규칙의 runtimeTested=false와 전달 경쟁·상관관계 한계를 유지한다.','bagInvest','### 장착 대표',[ev('bagInvest','### 상자 대표'),ev('bagRules','## 요청 API와 실제 소비'),ev('bagRules','## 즉시 재습득 차단과 수령 규칙')]));
edges.push(e('P.bag-equipment','R.bag-rules','참고: 정상 게임 입력·소비자 조사','bagRules','## 요청 API와 실제 소비'));

for(const [mode,title,summary,anchors,symbol,anchor] of [["equip","장비 장착·교체·해제","실제 활성 장비 슬롯과 출발/도착 물품을 고르고 정상 UI Swap 뒤 양 슬롯 교환을 관측한다. 반지·파우치 목적지와 용량/태그 조건을 구별한다.","AIInventory.Candidates, AIInventory.GearSlots, AIInventory.Stale, AIInventory.CapacityAfterSwap, InventorySkill.Start, InventorySkill.Tick","AIInventory.Candidates","장착/교체/해제:"],["hold","명시 도구·근접무기 손선택 유지","Decisions가 고른 구체 핫바칸의 물품을 실제 장비 관측으로 확인한다. 후속 채굴·근접공격은 선택 물품 신원과 파손을 확인하고 자동 대체하지 않는다.","AIInventory.TryChosenHand, AIHandLease.Check, AIHeldChoice.Check, AIBody.PickTool, AIBody.PickWeapon","AIInventory.TryChosenHand","손 선택:"],["store","품목·자기 상자·수량 지정 보관","선택한 일반 가방 품목을 자기 상자의 유효 슬롯으로 보관한다. 요청 잔량과 keep을 반영하며 실제 source 감소=destination 증가만 누적한다.","AIInventory.ChestCandidates, AIInventory.StoreRemaining, AIInventory.Stale, InventorySkill.Tick, AIInventoryRules.MoveCount, AIInventoryRules.ObservedTransfer","AIInventory.ChestCandidates","## 후속 상자 조작"],["take","자기 상자에서 지정 수량 꺼내기","자기 상자를 열어 관측한 품목만 실제 가방 목적 슬롯으로 꺼낸다. 부분 이동은 관측 수량만 기록하고 다음 선택을 받는다.","AIInventory.ChestCandidates, AIInventory.Destination, AIInventory.Stale, InventorySkill.Tick, AIInventoryRules.ObservedTransfer","AIInventory.ChestCandidates","## 후속 상자 조작"],["drop","명시 수량 내려놓기","명시 목표에서 정상 DropItem을 한 번 요청하고 자기 슬롯 감소와 이번에 생긴 바닥 물품을 확인한다. trash 삭제를 제공하지 않는다.","AIInventory.DropCandidates, AIInventory.Stale, InventorySkill.Tick","AIInventory.DropCandidates","## 후속 내려놓기·전달"],["give","지정 수령자 전달 결과 관측","보이는 수령자에 접근해 정상 바닥 드롭을 사용한다. 추적 바닥 물품의 지정 수령 요청과 실제 감소량=수령자 일치 품목 증가량이 함께 관측된 수량만 기록한다.","AIInventory.DropCandidates, AIInventory.Recipient, AIInventory.MatchingCount, InventorySkill.Tick","InventorySkill.Tick","## 후속 내려놓기·전달"]]){
 nodes.push(n('R.bag-'+mode,title,'참고·구현/오프라인, 실게임 미확인',summary,'bagSpec',anchor,[ev('bagTreeSpec','## Leaf와 목표 진척')]));
 edges.push(e('P.bag-equipment','R.bag-'+mode,'참고: 승인 범위의 구현 능력','bagSpec',anchor),e('R.bag-'+mode,'R.bag-validation','참고: 검증 범위·미관측','bagStatus','# 가방·장비 구현 검증 범위'),e('R.bag-'+mode,'R.bag-rules','참고: 정상 게임 소비 규칙·경계','bagRules','## 요청 API와 실제 소비'));
}

// Approved category-first bag navigation. Implementation revision is pending separately.
documents.bagTree={layer:'재기획',path:'../2026-11-07-가방장비구현/tree-agreement.md'};
nodes.push(
 n('P.bag-item-first','실제 보유 분류에서 물품과 조작으로 좁히기','합의','인벤토리→물품이 있는 실제 분류→정확한 보유 슬롯/변형/상태→조작→필요한 인자로 이어 간다. 목표 맥락을 유지하며 목록을 목표 품목 하나로 숨기지 않는다. 관측한 자기 상자 꺼내기 경로도 같은 방식으로 잇는다.','bagTree','## 만들 결과'),
 n('P.bag-navigation','선택 탐색과 정상 조작 실행을 구별','합의','현재 메뉴 자식만 제시하는 실제 다단계 상태를 유지한다. 뒤로는 부모로, 전체 선택 취소는 일반 판단으로 돌아간다. 메뉴 탐색은 물품 이동/몸 중지/단계 완료/원래 부탁 취소가 아니며 최종 leaf 선택에서만 정상 조작을 한 번 요청한다.','bagTree','## 실행·모델 연결 조건',[ev('bagTree','## 만들 결과')]),
 n('P.bag-no-cut','후보 개수 상한을 없애고 현재 노드 전부 제공','합의','일반 가방8·전체 비전투14와 같은 개수 기반 잘라내기를 제거한다. 분류/물품/조작/대상을 임의 목록 cut이나 페이지로 나누지 않는다. 관측·소유·실행 가능 조건과 로그/기억 보관량 및 기존 호출/비용 제한은 구별한다.','bagTree','## 선택지 상한 제거',[ev('bagTree','## 실행·모델 연결 조건')]),
 n('R.bag-navigation','분류·물품 선택 메뉴와 실행 경계의 새판 검증','참고·정적구현/생산자오프라인, 미수용·미실게임','현재 체크포인트의 실제 메뉴 심볼과 생산자 소스 링크 검사만 연결한다. 연결 trace·독립검수·Unity·실게임·최종 수용은 별도다. flat 774/1fc 부품 검증을 새 트리 통과로 승계하지 않는다.','bagTree','## 검증과 전달')
);
edges.push(
 e('P.bag-equipment','P.bag-item-first','평면 후보 부분을 새 선택 흐름으로 교체','bagTree','2026-11-07 사용자 방향 변경.','합의'),
 e('P.bag-item-first','P.bag-navigation','물품별 단계 탐색과 실행','bagTree','## 만들 결과','합의'),
 e('P.bag-item-first','P.bag-no-cut','분류로 나누고 실제 항목은 모두 제공','bagTree','## 선택지 상한 제거','합의'),
 e('P.noncombat-choice','P.bag-no-cut','현장 후보 상한 제거의 요청 범위','bagTree','## 선택지 상한 제거','합의'),
 e('P.bag-agency','P.bag-navigation','메뉴 선택과 목표·몸 제어의 역할 구분','bagTree','## 실행·모델 연결 조건','합의'),
 e('P.bag-navigation','P.valid-action','실행 가능한 몸 동작과 기존 대응 유지','bagTree','## 실행·모델 연결 조건','합의'),
 e('P.bag-navigation','R.bag-navigation','참고: 새 트리 고정판 검증 경계','bagTree','## 검증과 전달'),
 e('R.bag-navigation','R.bag-validation','참고: 최종 SHA와 실제 검증 범위','bagStatus','# 가방·장비 구현 검증 범위')
);
for(const mode of ['equip','hold','store','take','drop','give'])edges.push(e('R.bag-navigation','R.bag-'+mode,'참고: 최종 leaf에서 재사용하는 조작·관측','bagTree','## 실행·모델 연결 조건'));

// Bounded tree checkpoint sources. References are not user decisions.
Object.assign(documents,{bagTreeSpec:{layer:'참고',path:'../2026-11-07-가방장비구현/tree-spec.md'},bagCapAudit:{layer:'참고',path:'../2026-11-07-가방장비구현/inventory-category-cap-audit.md'},bagCapResult:{layer:'참고',path:'../2026-11-07-가방장비구현/tree-cap-result.md'},bagMenuSamples:{layer:'참고',path:'../2026-11-07-가방장비구현/menu-examples.md'}});
nodes.push(n('R.bag-cap-audit','현장 후보 상한과 다른 소비자 제한의 구별','참고·정적 조사/HEAD 적용 판정','현장 Decisions의 개수 cut과 closeFight ONE quota는 제거 범위다. 상위 둘레 설계 CoreEdge.Options4·관측/solver/물리·보고/기억/로그·call/cost 제한은 다른 의미로 보존한다. 이는 전체 world 트리 재설계나 실행 성공 근거가 아니다.','bagCapAudit','## 8. HEAD 적용 판정',[ev('bagCapResult','## 변경한 경로'),ev('bagCapAudit','## 5. cap 지도 B')]),n('R.bag-menu-examples','현재 메뉴 노드 payload 표본','참고·단독 메뉴 어댑터, 미외부모델·미실게임','실제 menu/inventory 소스에 대역 World/수동 결과를 공급한 현재 노드 자식 예시다. 연결된 실제 Loop 전송·실제 외부 모델·게임 소비자 성공과 구별한다.','bagMenuSamples','# 현재 메뉴 노드 표본'));
edges.push(e('P.bag-no-cut','R.bag-cap-audit','참고: 소비 경로별 상한 적용 판정','bagCapAudit','## 8. HEAD 적용 판정'),e('P.bag-item-first','R.bag-menu-examples','참고: 현재 보유 노드 표본','bagMenuSamples','# 현재 메뉴 노드 표본'),e('R.bag-navigation','R.bag-menu-examples','참고: 현재 노드 자식·선택 경로 예시','bagMenuSamples','# 현재 메뉴 노드 표본'),e('R.bag-navigation','R.bag-cap-audit','참고: 상한 제거와 유지 경계','bagCapAudit','## 8. HEAD 적용 판정'));
