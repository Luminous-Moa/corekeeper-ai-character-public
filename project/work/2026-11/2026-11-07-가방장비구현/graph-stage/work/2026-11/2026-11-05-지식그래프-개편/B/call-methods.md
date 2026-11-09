# B-1 호출 방식과 B-2 처리 판단

이 표의 처리 선택과 C4 묶음은 LLM 판단이다. 사용 여부는 pinned source의 Roslyn/Acorn AST와 raw facts로 확인했다. 함수가 등장하는 원본 자리와 인자는 raw JSON에 보존했다. 함수 내부 순서나 조건 분기 그래프를 저장하지 않았다.

| 스택 후보 | 실제 코드 형태와 근거 | 처리 | 추출 방식/남는 경계 |
|---|---|---|---|
| C# 직접 호출·생성자 | 모든 scoped 파일; call 6952/new 964 | 따라감 | Roslyn symbol의 원본 정의 ID. Unity metadata 부재의 단일 후보는 possible, 복수/없음은 경계 |
| 부분 클래스·오버로드·로컬 함수 | AIBrain 여러 파일, overloaded 메서드 | 따라감 | 파일+완전 signature. 같은 이름만으로 다른 클래스나 overload 연결 안 함 |
| 프로퍼티·접근자 | AIAgent.Cur/각 멤버 상태 S 등 | 따라감 | property reference→property 코드, accessor callback. getter/setter 중 실제 선택은 보수적 potential |
| static 필드 초기화 | AIActions.Kinds, AIAgent.All | 따라감 | AST static initializer, framework-invoked possible. 실제 초기화 시점은 저장하지 않음 |
| delegate/method group/람다 | AIActions.Kinds의 Make/Stale, Func 인자, 220개 람다 | 따라감/경계 | Roslyn method group/inline lambda/field Invoke. 동적 수신자의 실제 값은 확정하지 않음 |
| interface/virtual override | IMod, INetworkStreamDriverConstructor, AISkill | 따라감 | IMod·driver signature로 입구, 가상 base→override는 possible 후보. 누락 metadata일 때 source bases/modifiers 확인 |
| 런타임 리플렉션/Harmony | InstallPatches AccessTools.Method(typeof, nameof); FindStartGameSpawnLambda; HeadlessSdk.FindType/GetMethod | 따라감+경계 | source-local typeof+상수/nameof target만 따라감. game SDK runtime target/선택 탐색/Invoke는 경계 |
| framework naming entry | HeadlessSdk public static 4개 / Unity -executeMethod | 따라감 | 코드 signature에 기반한 CLI 입구. 구동 관례는 판단으로 표시 |
| attribute/decorator | Serializable 등 C# attribute 17개 | 경계/타입 근거 | 무조건 호출 입구로 만들지 않음. method/serialization metadata만 보존 |
| IDisposable using / ToString | AIAgent.Scope.Dispose / ToString | 경계 | 암시 호출을 완전히 모델링하지 않아 미도달 목록에 명시 |
| Node ESM import/export | bridge의 5개 내부 모듈 및 node:* | 내부 따라감/런타임 경계 | Acorn import literal로 내부 파일·export 연결. import되지 않는 bridge는 module entry |
| Node 동적 import | ai-chatroom 3개 절대 경로 및 AI_BRIDGE_FAKE_CHAT | 경계 | 표현식 보존. 외부 모듈은 SDK 커밋 밖, fake는 테스트 주입 |
| JS lexical 함수/inline callback | createServer, map, res.on, listen, createPlan 등; call 876/new 28 | 따라감/경계 | lexical binding, inline callback, 직접 factory return/return object destructuring. 임의 points-to 추정 안 함 |
| 객체 함수 등록표 | memgraph Q={about:qAbout,...}, runQuery의 Q[q.q] | 따라감 | 유한 객체 literal 등록 대상 전부 callback possible. q 값에 따른 실행선택은 저장하지 않음 |
| Node dependency injection | createPlan({briefFor,log,recallLog,p90Sec,...}) | 따라감/경계 | 이름 있는 함수 값을 객체 인자로 넘기는 callback. 일반 동적 객체 member는 경계 |
| HTTP/네트워크 | UnityWebRequest POST decisions, GET health, POST plan; http.createServer req.method/req.url | 내부 따라감/외부 경계 | bridge 주소상수+verb를 내부 route handler로 http 연결. OpenAI 주소는 외부 연결. 게임 IPC는 SDK 경계 |
| 파일/디렉터리/stream | File.Read*/Write*/Append*/Move/Replace, StreamWriter/FileStream, fs.* | 데이터 read/write | 고정 literal 또는 경로 표현식 유지. stream writer 생성 시 write; 명시 FileAccess 판별 |
| 환경변수/로컬 설정 | Environment.GetEnvironmentVariable, process.env, EditorPrefs.SetString | 데이터 read/write | 키 literal/표현식. API key 값 자체를 읽지 않음. env key 삭제도 write |
| DB/ORM/SQL 후보 | scoped source에 스키마/쿼리 전송 없음 | 발견 안 됨 | PugDatabase 이름만으로 DB 노드 생성 안 함. JSON/JSONL은 데이터 |
| 분석 이벤트 전송 후보 | ExpRecorder 사건/JSONL, bridge logs | 발견 안 됨 | 기억 이벤트·개발 로그를 계측으로 잘못 승격하지 않음 |
| 파일명 라우트/React/cron/webhook 후보 | 해당 스택 구조 없음 | 발견 안 됨 | 서버의 명시 method/path만 API. 타이머 기반 cron 선언 없음 |
| eval/변수 import 후보 | eval 없음; 변수 import 4개는 위에 기록 | 경계 | dynamic-import facts에 표현식/근거줄 |
| 테스트·개발 도구 | ai-bridge/test, replay.mjs, memgraph developer CLI, test-only switch | 범위 밖/구분 | 테스트/replay는 graph 추출 제외. memgraph CLI source는 같은 파일이라 코드로 남지만 제품 entry 기능을 발급하지 않음. 모드에 실제 포함된 testcmds 분기는 코드·플래그로 남고 제품 실행하지 않음 |

C4: Context는 이 제품 하나, Container는 게임 안 모드·Unity 에디터 도구·별도 Node 브리지, Component는 각 소스 디렉터리다. 배포 의존성 Tools/knowledge는 import 경계이며 게임 데이터 그래프 내용을 여기로 복제하지 않는다.

API 실행 입구, IMod/driver hook, Harmony 등록 hook, slash와 build CLI만 기능으로 만든다. C# 모든 public method나 JS 모든 export를 기능으로 만들지 않는다. 서버 startup은 독립 함수가 없는 top-level ESM이므로 `ai-bridge:startup:bridge.mjs.module`로 코드에서 기계적으로 이름을 정한 module entry임을 표시했다.
