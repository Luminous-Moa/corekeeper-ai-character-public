# 최적화 코드의 지식 기록 연결점

확정 후보43c3817과부모커밋2a0112d의구현을다음정본기록에연결할수있다. 실제공식반영과설치는HEAD가실기증거와함께판정한다.

- AIBrain.Preparation의후보준비수명과프레임예산,변경취소,실패정보보존,전송직전검증이새생산진입경로다. 기존Loop.Send가시작하고Tick에서이어실행한다. PlanC도Preparation파일로이동했다.
- AITunnel.PlanBatch가초기출발위치·도구·객체와lazy타일facts를공유한다. SharedSearchJob의닫힌경계/열린경계두트리를여러목적지가재사용하고SharedQueryJob이실제경로를복원한다. 목적지경계벽은마지막진입으로처리한다. 같은최소비용경로의동률선택순서가달라질수있고위험정보는그경로에서산출한다.
- CandBuild의DeferRoute/PreparePublishedRoutes는기존원정query카탈로그를사용한다. 원정닫힘에서는미선택세계리프의경로를계산하지않고,travel목록을열기전에는기억frontier검색도시작하지않는다.
- AIExpedition.Compose와Core.ExpeditionQuery/ExpeditionCursor는큰말단을안정된key범위로탐색한다. 원본대상을삭제하지않는다. 현재행동·눈앞채집묶음·명시목적지·위협은직접선택경로를유지하며동일key교차충돌도보호한다. 가방노드의전용계약은이변경의대상이아니다.
- HTTPchoices255기술상한은실제로확인됐다. 요청직전검사와오류본문/request ID관측이추가됐다. 극단적으로직접면만한도를넘는경우전송진단경계가남는다.
- 해결근거는core1571,production scheduler25,원정Compose/Nav/BuildJson118,제약회귀47,생산배치72084,Unity컴파일및2차실기자연어→상위계획→하위29개정상응답→이동실행이다. 상세보고implementation-final.md와live-validation.md를연결한다.
- 남은경계는원자world epoch없는관측구간,일부단일동기작업,이동중장애물변경시반복취소다. 실기8.9초/7회재준비사례가있어이를완전해소로기록하지않는다.
