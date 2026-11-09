<!-- head-context branch:1 -->
# 리팩토링

이름표: 리팩토링
시작: 2026-11-04T13:32:42.201Z
갈래: 코어키퍼-AI-캐릭터/메인@2026-11-04T13:25:41.590Z
갈래-ID: %EC%BD%94%EC%96%B4%ED%82%A4%ED%8D%BC-AI-%EC%BA%90%EB%A6%AD%ED%84%B0/main@2026-11-04T13:25:41.590Z
상태: 끝남

## 지금까지 요약

리팩토링 전체: 사용자가 앞으로 기능이 많이 붙고 방향이 바뀔 수 있는 초기 상태이니 유지보수가 쉽게 나누자고 해서, 3c3adbe 재점검 → 중간안 C(계약을 타입으로 + 예정 변경이 닿는 선으로 분리) 선택 → 계획·독립 검토(고칠 것 13 반영) → S0 도구·탐침 → L0 기준 실게임(새 시험 월드 rftest, 게임 컴파일러가 하위 폴더·partial·new() 수용 확인) → S1 정식 JSON 판독, S2 결과 코드값, S3 목표 끝 한 경로·GoalSource, S4 보고 분리·받은편지함·Post(owner), S6a 조립기 기록, S8 브리지 protocol·preflight·plan 분리 → L1 실게임 통과 → S2b 코드값 전환, S5 AIBrain partial·AIDecisionsApi·AIActions·BuildContext, S6b Unity 없는 후보 조립기(옛 코드 기록 854/854 정확 일치)·생성기 CandBuild, S7 AISlot·Get<T> → L2 네 캐릭터 통합 실게임 통과 → 최종 독립 검수 막음 1(B1 제어 문자) 수정·재확인 막음 0 → ai-character e82eb87, GitHub mine/main 1c2a61b, worktree 정리. 실게임 세 번은 사용자 사전 승인으로 자율 진행·매번 되돌림(다름 0). 동작 차이는 사용자가 받아들인 것(JSON escape·중첩)과 HEAD가 받아들인 것(S1 도달 불가 3·거절 문장 숫자 1, down·kill 값 판정, '기다리기뿐' 문구의 제작 막힘 분류, 보고 제어 문자 공백화)뿐. AIDecisions.cs 2,992→182줄, Scripts 전체 약 16,700줄. 시험 기반: dotnet core-tests 161(조립기 재생 포함), compile-check(C# 8 고정), gates.sh, logshape·movecheck·asmcheck·fake-bridge. 남은 작은 정리는 할 일(skill-nopath 값 전환, 개발 지식그래프 새 파일 반영, 실게임 절차 메모). 작업 파일: work/2026-11/2026-11-04-리팩토링/1~11, runs/L0~L2.

## 2026-11-04T13:35:20.086Z

요약: 리팩토링 착수(사용자 "점검하고 코드분리 및 리팩토링 하자"): 앞으로 기능이 많이 붙고 방향이 바뀔 수 있는 초기 상태라, 유지보수·변경이 쉬운 구조를 목표로 현 상태 재점검부터 한다

- 사용자 요청: "점검하고 코드분리 및 리팩토링 하자. 지금 이 프로젝트는 엄청나게 다양한 기능들이 더 붙을 예정이고, 지금은 프로젝트 전체에서 상당한 초기 상태야. 보완할점이 엄청나게 많고 이후 방향성이 틀어질 여지도 충분해. 그러니 그런거 감안해서 유지보수가 쉽게 만들어야 해."
- 문제: 모드 C# 14개 15,902줄(AIDecisions 2,992·AIBodySkills 2,480·AIPlayerSpikeMod 2,084에 집중)과 브리지 JS(bridge·memgraph·recall-loop·replay 약 1,400줄)가 빠르게 자라 기능을 더하거나 방향을 바꿀 때 여러 곳을 함께 고쳐야 한다. 이전 점검(초기-코드-구조점검, 039d8b5 기준)은 좁은 정리 3곳을 권했지만 그 뒤 기억 검색(+1,700줄)이 들어왔고, 사용자는 좁은 정리보다 앞으로의 확장을 견디는 구조를 원한다.
- 목표: 새 기능 추가와 방향 전환이 국소적인 변경으로 끝나게 책임·경계를 나눈다. 바로 다음 소비자: 호스트 목표 아래에서도 위쪽 LLM이 계속 생각하기(리팩토링 다음 첫 작업), 전투 짧은 동작 선택지·관찰(전투-개선-계획), 자연어 목표·질문, 기억 검색 보완·LLM 교훈 쓰기.
- 조건(HEAD 제안): 동작은 바꾸지 않는다(후보 순서·개수·전투 전환·재시도·응답 계약 유지 — 동작 변경은 별도 작업). 상상한 미래를 위한 틀은 만들지 않고, 실제로 예정된 변경이 쉬워지는 경계만 만든다. 기준 ai-character 3c3adbe, 별도 worktree에서 작업. 단계마다 컴파일 오류 0·브리지 시험 통과, 끝에 대표 실게임 1회(게임 사용은 허락 뒤). 합치기·push는 사용자 승인 뒤. 구조 방향 중 큰 선택은 사용자에게 돌려 정한다.
- 근거 자료: 이전 점검 work/2026-11/2026-11-04-초기-코드-구조점검/(판정·상세진단·검증), 전투 계획 work/2026-11/2026-11-04-전투-개선-계획/2-개선계획.md, 개발 지식그래프 work/지식그래프.
- 남은 정리: 합쳐진 worktree -kg·-life·-memory·-recall 정리는 이 대화 몫(기억-그래프-검색 기록). 지우기는 사용자 확인 뒤.

다음: 재점검(독립 에이전트, 읽기 전용) → 구조 방향 선택지를 사용자와 정함 → 단계별 분리 계획 → 구현·검수 → 실게임 → 반영



## 2026-11-04T13:50:20.522Z

요약: 재점검 완료(독립 에이전트, 3c3adbe 읽기 전용): 막는 것은 static·파일 길이가 아니라 목표 칸 하나에 몰린 출처·끝내기 7곳 복사, 사람 문장을 계약으로 다시 읽는 20여 곳, 흩어진 위쪽 LLM 응답 계약. 중간안 C(계약을 타입으로 + 예정 변경이 닿는 선으로 분리, S0~S8) 추천

근거: [1-재점검.md](../../../../../../work/2026-11/2026-11-04-리팩토링/1-재점검.md)(구조 지도, 예정 변경 1~4별 변경 비용, 문제 지점 파일:줄, 선택지 A/B/C 비교, 단계 S0~S8, 시험 기반, 질문 7개, 확인 한계).
- 핵심 발견: ① AIGoal 한 칸이 호스트·LLM·자유 시간·없음(자기방어)을 다 맡고, 목표 끝내기가 7곳에 조금씩 다른 순서로 복사, 출처 문자열("host"/"llm:…")을 9곳에서 따로 해석. ② 스킬 실패 이유·막힘 이유(AIExpCore가 부분 문자열로 분류, exp-sim이 문장 고정)·"도구 부족" 등 사람 문장을 20여 곳에서 결정에 다시 읽음, replay.mjs도 Player.log 줄을 파싱. ③ 모드가 손 파서 AIPlanner.Field로 답을 읽어 escape·중첩을 못 다루고(HEAD 확인: 첫 따옴표에서 자름), 브리지가 그 파서에 맞춰 키 순서를 맞춤. 정식 파서 ExpJson.Parse(AIExpCore.cs:49)가 이미 모드에 있음(HEAD 확인).
- 예정 변경별 지금 비용: 1(호스트 목표 아래 LLM) 15곳 이상·끝내기 복사 7곳 중 하나 빠뜨리면 조용히 실패, 2(짧은 전투 동작) switch 2곳+BuildCandidates 726줄 여러 곳+경계 기다리기, 3(자연어) 채팅 훅이 캐릭터 범위 밖이라 Cur가 Claude로 떨어짐(HEAD 확인 AIAgents.cs:45)·플래너가 바쁘면 메시지 잃음·say가 잘림, 4(기억) 대부분 브리지 plan()에 몰림.
- 선택지: A 좁은 정리(이전 판정) / C 중간: A + AIBrain을 목표·판단 고리·후보·동작 표·OpenAI 클라이언트로, AIPlanner를 정책·보고·프로토콜로 나누고 목표 출처·계기·결과를 타입으로, 캐릭터별 받은편지함, 브리지 plan() 단계 분리 / B 전면 재계층(static→인스턴스, 인터페이스, 이벤트 버스, 별도 어셈블리). 이득 1/2/3/4: A 작음/중/중/작음, C 큼/중+/큼/중, B C와 비슷하나 비용·위험 큼.
- 단계: S0 기준 기록 → S1 응답 계약(공용 fixture, dotnet·node) → S2 결과 코드 → S3 목표 끝내기 한 경로 → S4 플래너 분리·받은편지함 → S5 판단 고리 분리 → S6 후보 생성기+Unity 없는 조립기(dotnet 재생 정확 비교) → S7 타입 있는 상태 접근 → S8 브리지 단계 분리(병렬 가능). 변경 1·3은 S1~S4 뒤, 2는 S5~S6 뒤, 4는 S8 뒤 시작 가능. 하지 않을 것: static 전부 교체, 별도 asmdef, 이벤트 버스·플러그인, 네트워크·저장 코드, 설명 문구·로그 형식·기억 파일 형식 변경.
- 시험 기반: 브리지 node 시험 3파일 약 88검사+replay. C#은 AIExpCore만 exp-sim으로 시험. 제안: Unity 없는 핵심을 늘려 dotnet으로 시험, 로그 재생 비교를 두 번째 그물로.
- HEAD 판단: 질문 2(끝내기 순서)는 합의 조건 '동작 유지'로 HEAD가 정함 — 리팩토링 중엔 각 순서 유지, 통일은 따로. 질문 3·4·7은 변경 1·3 작업에서 정할 동작 질문이라 지금 묻지 않음. 사용자에게 물을 것: 방향(A/C/B), 범위(C 전부/S1~S4+S8 먼저), 정식 JSON 파서로 바뀌는 결과를 버그 수정으로 받을지, 시험 도구 추가.
- 확인 한계: 실행 없음(게임·Unity·node·dotnet). ModSDK가 Scripts 하위 폴더·두 번째 asmdef를 다루는지, 게임이 모드 소스를 직접 컴파일하는지, 지원 C# 버전, Newtonsoft 런타임 사용 가능 여부 미확인 — 구현 계획 전에 확인 필요.

다음: 사용자 결정(방향·범위·JSON 파서·시험 도구) → ModSDK 제약 확인 → 단계별 계획 → 구현·검수 → 실게임 → 반영



## 2026-11-04T14:01:24.877Z

요약: 사용자 결정: C안 전체(S0~S8)로 진행, 정식 JSON 파서로 바뀌는 결과는 버그 수정으로 받음, 시험 도구 추가 허용. 모드는 지금처럼 소스로 넣고 게임이 컴파일(두뇌 dll 분리 안 함)

- 사용자: "c로 진행", 그리고 HEAD 제안(범위 S0~S8 전부, JSON 파서 교체를 버그 수정으로, dotnet 시험 프로젝트·판단 기록 등 시험 도구 추가)에 "네가 제시한대로 진행할게".
- 사용자가 완성된 dll로 넣을 수 없는지 물음. HEAD가 SDK ModBuilder.cs로 확인: .cs는 소스로 Scripts/에 복사되어 게임이 실행 때 컴파일하고(모드는 skipSafetyChecks 켬), .dll은 Libraries/로 복사하는 길이 있다(게임이 실제로 불러오는지는 미확인). ECS 쪽은 Unity 코드 생성에 기대므로 소스가 안전하고, 게임 무관 두뇌만 dll로 뺄 수 있다고 설명. 사용자: "굳이 그렇게 할 필요 없어. 직접 컴파일해서 실행이 잘 된다면 굳이 어렵게 돌아갈 필요는 없으니까" — dll 분리는 하지 않는다.
- 남은 환경 확인(게임이 컴파일): Scripts 하위 폴더 소스를 읽는지, 지원 C# 문법, Newtonsoft 런타임 사용 가능 여부. HEAD 판단: 별도 시험 모드 대신 S0 기준 실게임 때 함께 확인하고, 답 판독은 이미 게임에서 돌아가는 ExpJson.Parse를 우선 쓴다. 문법은 지금 코드가 이미 쓰는 범위를 넘기 전에 확인한다.
- 리팩토링 중 동작 유지: 목표 끝내기 7곳의 순서 차이는 그대로 두고 통일은 따로(HEAD 결정). 비용 상한 무통보·경험 기록 집계·채팅 전달 범위는 해당 기능 작업에서 정한다.

다음: 단계별 구현 계획(S0~S8, 단계별 확인 방법·게임 사용 시점) → 독립 검토 → S0부터 구현



## 2026-11-04T14:26:18.965Z

요약: 구현 계획(S0~S8, 실게임 L0·L1·L2 세 번) 작성과 독립 검토 완료: 판정 '고친 뒤 진행', 막음 없음, 고칠 것 13(탐침 설계, csproj gitignore, error=null 로그 문구, 긴급·도구부족 플래그 초기화, S2b 코드화 범위, 네비게이터 실패 생산처, 오류 갈래 가짜 브리지 회차, 조립기 순환 검증, 기록기 부작용, 이동 검사, 공유 자료 백업, S1↔S8 경계)

근거: [2-계획.md](../../../../../../work/2026-11/2026-11-04-리팩토링/2-계획.md), [3-계획검토.md](../../../../../../work/2026-11/2026-11-04-리팩토링/3-계획검토.md).
- 계획 요지: worktree C:\Dev\CoreKeeperModSDK-refactor(ai-refactor)·-refactor-bridge(ai-refactor-bridge), 3c3adbe에서. 단계마다 로컬 태그 rf-S<n>이 되돌릴 지점, 합치기·push 없음. 공통 관문: 빠른 컴파일(Tools/compile-check, 하위 폴더 포함), Unity CompileCheck, exp-sim, Tools/core-tests(dotnet), moved-code diff. 순서 S0 → L0 → S1·S2·S3·S4·S6a(+S8 병렬) → L1 → S2b·S5·S6b·S7 → L2. S1~S7은 한 작업자가 차례로, S8만 병렬.
- 검토 판정: 고친 뒤 진행, 막음 없음. HEAD 판단: 13건 모두 합의 결과 안의 보완이라 사용자 결정이 필요 없다. F6은 S2b에서 결과·자리로 정해지는 분류만 코드로 바꾸고 나머지는 지금처럼 문장 분류(Classify)로 두는 것으로 정한다(동작 유지). F9 때문에 조립기 기록이 부족하면 옛 코드 실게임 L1b가 한 번 더 필요할 수 있다 — 그때 사용자에게 묻는다.
- 사실 정정: 지금 코드는 이미 C# 8 기능을 쓰고 게임이 컴파일한다(AICook.cs:68) — CompileCheck를 LangVersion 8로 고정해 새 문법을 막는다. 제네릭 제약도 이미 있다(AIPlayerSpikeMod.cs:1734). memgraph.test는 KG 자료가 없어 못 돈다(bridge-wiring은 19/19).
- 기준선(검토자 실행): exp-sim ALL OK, recall-loop 41/41, 게임 설치본 = 3c3adbe.

다음: 계획에 F1~F13·정정 반영 → S0(worktree·도구, 제품 동작 변경 없음) → L0 실게임 허락 요청



## 2026-11-04T14:48:08.638Z

요약: 계획에 검토 13건 반영, S0 완료: ai-refactor d47f8f5·태그 rf-S0(비상용 rf-S0-noprobe), 제품 코드 변경은 탐침 3개뿐. 오프라인 관문 모두 통과(빠른 컴파일 0/0, CompileCheck 0, core-tests 6/6, exp-sim 74 OK, recall-loop 41/41, bridge-wiring 19/19, memgraph 68/68). L0 실게임 절차 준비, 사용자 허락 대기

근거: [2-계획.md](../../../../../../work/2026-11/2026-11-04-리팩토링/2-계획.md)(검토 반영 절), [4-S0.md](../../../../../../work/2026-11/2026-11-04-리팩토링/4-S0.md)(관문 결과, L0 절차 §4).
- HEAD 확인: worktree C:\Dev\CoreKeeperModSDK-refactor(ai-refactor d47f8f5), 태그 rf-S0·rf-S0-noprobe, 3c3adbe 대비 Scripts 변경은 탐침 3파일(AIProbePartA/B, Core/AIProbeSub)뿐. 게임·포트 8331 사용 중 아님.
- 반영: F1~F13 전부(반대 없음). 월드 저장은 백업 관행이 없어 L0 절차에서 함께 백업한다. C# 9 문법(is not)은 빠른 컴파일에서 CS8400으로 걸러진다.
- 외부 산출: Tools/knowledge/data KG 자료를 다시 만들어(3159 노드) memgraph 시험이 돈다(git 밖).
- L0: 사용자 60~75분 + 작업자 앞뒤 약 10분. 작업자가 설치본·AIPlayers·스위치·월드 저장을 백업하고 rf-S0을 빌드·설치·해시 확인, 사용자가 게임을 켜 시험 월드를 열고 /ai 시나리오 1~11을 입력, 9번은 실제 브리지(Claude 구독 사용량 씀), 10번은 가짜 브리지. 끝나면 설치본·스위치 복원, AI 기억·경험·월드 저장 되돌릴지는 사용자가 정한다. 컴파일 실패 시 rf-S0-noprobe로 바꿔 보고. partial이 안 되면 멈추고 사용자에게.

다음: 사용자 L0 허락·게임 차례 → L0 실게임 → 결과 판정 → S1~S4·S6a(+S8 병렬) → L1



## 2026-11-04T14:59:09.863Z

요약: 사용자 사전 승인: 실게임 L0·L1·L2(필요 시 L1b) 자율 진행(게임 켜기·시험 월드·Mods 설치·실제 구독 사용), 회차 뒤 AI 기억·경험·월드 저장은 백업으로 되돌림, 모든 관문+L2 통과+최종 독립 검수 막음 0이면 ai-character 합치기·GitHub push·worktree 정리까지 조건부 승인. 태블릿을 FoldRemote 원격 제어 모드로 화면 연결

- 사용자: "최저화질과 최저프레임으로 진행하면 돼. 자율적으로 게임 켜서 진행하고 내 승인이 없어도 할 수 있는 것부터 진행해. 승인 필요한게 있다면 지금 미리 요청하면 좋을거 같아." → HEAD 질문 3개에 답: 실게임 '전부 맡김', 시험 기록 '되돌림', 반영 '조건부 미리 승인'.
- 반영 조건(사용자 승인 문구 그대로): 모든 시험 관문, L2 통과, 독립 최종 검수에서 진행을 막는 문제 0건 → ai-character에 합치기, GitHub mine/main에 이전처럼 모드·브리지 런타임 파일만 push, 끝난 worktree 정리.
- 실게임은 다른 대화가 게임을 쓰고 있으면 기다린다. 게임은 최저 화질·최저 프레임으로 돌린다(원래 설정 기록 뒤).
- 화면: 빔프로젝터를 끄면 모니터가 없어서 FoldRemoteDesktop(배포 호스트 이미 실행 중)으로 태블릿(Galaxy Tab S7+, 무선 adb <LAN-IP>:45223)을 원격 제어 모드로 연결(23:55, 50% 해상도·60fps). 원격 제어 모드에서는 물리 출력(DENON-AVR)을 호스트가 끈다. 프로젝터가 켜진 동안 배치 되돌림으로 한 번 재시작됨(문서의 알려진 제한).

다음: C# 작업자: L0 자율 실행(rf-S0 설치) → S1~S4·S6a 이어서, S8 병렬 → L1 → S2b·S5·S6b·S7 → L2 → 최종 독립 검수 → 조건 충족 시 반영



## 2026-11-04T15:05:20.231Z

요약: S8 완료(브리지): plan()을 protocol·preflight·plan 모듈로 분리, ai-refactor-bridge 81155cc·태그 rf-S8. 옛·새 브리지가 같은 32요청에 응답 32/32·plan.jsonl 27/27·recall.jsonl 20/20 동일, 시험 protocol 34/34·recall-loop 41/41·memgraph 68/68·bridge-wiring 19/19(HEAD가 protocol·recall-loop 재실행 확인)

근거: [6-S8.md](../../../../../../work/2026-11/2026-11-04-리팩토링/6-S8.md), 비교 도구 6-S8-tools/.
- 새 배치: protocol.mjs(응답 계약: 200 봉투 23키 순서, 400·409·503·429·404·500 오류 봉투), preflight.mjs(멤버 상태·인증·한도·건강 검사), plan.mjs(plan() — busy→preflight→기억 맥락→검색 정책→recallLoop→봉투→기록, 단계 주석, 결정 사이 기억 상태), bridge.mjs(설정·좌석·서버·시작 검사). 옮긴 코드는 들여쓰기 말고 글자 그대로.
- 계획과 다른 점: plan()은 단계 함수로 쪼개지 않고 한 함수에 단계 주석(지역 변수 십여 개를 넘기며 옮긴 글자가 바뀌므로). HEAD 판단: 별도 모듈 안 107줄이라 기억 기능 변경이 국소적이므로 받아들인다. protocol.test가 mock으로 plan()·preflight까지 시험, PLAN_FIXTURES 환경변수 추가.
- 합칠 때: S8은 공유 파일 중 bridge.mjs만 고침. 합친 뒤 protocol.test의 fixture 대조가 SKIP이 아니라 실제로 도는지 확인, 계획 공통 관문에 protocol 시험 추가. 503·429는 HTTP 가짜 CLI로 못 타서 L1 시나리오 9와 protocol 시험 기대값으로 덮는다.

다음: C# 작업자: L0 자율 실행 → S1~S4·S6a → ai-refactor-bridge(rf-S8) 합쳐 L1 → S2b·S5·S6b·S7 → L2 → 최종 검수 → 반영



## 2026-11-04T16:04:09.164Z

요약: L0 자율 실게임 완료·되돌림(탐침: 하위 폴더·partial·new() 모두 게임이 받음, 새 시험 월드 rftest 시드 917121490949), S1~S4·S6a 완료와 S8 합치기(rf-pre-L1 8a55bd0, 관문 전부 통과). 작업자가 L1 시작 직후 대화 길이 초과로 멈춰 새 작업자가 L1을 이어받음

근거: [4-S0.md](../../../../../../work/2026-11/2026-11-04-리팩토링/4-S0.md) "L0 결과", [5-S1~S4.md](../../../../../../work/2026-11/2026-11-04-리팩토링/5-S1~S4.md), runs/L0/.
- L0(00:04~01:05, rf-S0 옛 코드): 탐침 probeSub=1 probePartial=1 probeNew=1 → Scripts/Core/ 하위 폴더와 S5 partial 설계를 그대로 간다. ai2 월드는 코어 옆 MushroomBrute·슬라임으로 Claude가 15분에 7번 쓰러져 목표 끝 갈래를 못 남겨(L0a 로그만 보관), 사용자 허락대로 새 월드 rftest(시드 917121490949, 기본 설정)에서 시나리오 1~11. 새 ERROR 0, 기준 모양 Player 65·decisions 39, 가짜 브리지 순서 전체 기록, 실제 claude 호출 4번. 시나리오 8(Brute 쓰러짐)은 rftest에서 재현 안 됨(ai2 로그에 back_up 갈래 있음).
- 되돌림: 설치본·AI 자료·월드·캐릭터 저장·그래픽·Steam remote·화면 설정·스위치 모두 백업과 같음. 남은 흔적 둘(지운 rftest의 호스트 지도 maps\3\2.mapparts.gzip — Steam Cloud에도 있음, pugcloud 레지스트리 시각)은 기존 월드 영향 없어 그대로 둠. L1·L2 절차 보정: 끝나면 게임 안에서 rftest부터 지우고 종료·되돌림, 다시 켜면 해상도 레지스트리 되돌림.
- S1~S4·S6a: S1 정식 판독(ExpJson, *Raw로 옛 로그 문구 유지, 32경우 중 같음 24·escape 2·nested 2·malformed 1·unreachable 3), S2 결과 코드값(전환 기간 불일치 로그), S3 목표 상태를 AIGoals로·끝내는 길 하나·GoalSource, S4 보고 작성 분리·받은편지함·Post(owner), S6a 조립기 기록기. 관문: core-tests 133, exp-sim OK, recall-loop 41, bridge-wiring 21, memgraph 68, protocol(fixture 29 실제 대조), Unity CompileCheck error CS 0. 태그 rf-S1~S4, rf-S6a, rf-pre-L1.
- HEAD 판단: S1의 malformed 1(for:null 거절 사유 문장 숫자만 다르고 거절 결과는 같음)과 unreachable 3(브리지가 JSON을 다시 쓰므로 모드에 오지 않음)은 실제 동작 차이가 없어 사용자가 받아들인 버그 수정 범위로 받는다.
- 작업자 교체: 이전 작업자가 L1 백업·rf-pre-L1 설치·게임 실행(01:01:47) 뒤 대화 길이 초과로 멈춤. 새 작업자에게 백업 완전성부터 확인하고 L1을 이어 가게 맡김(결과 7-L1.md, runs/L1/).

다음: L1 결과 판정(조립기 기록 충분성·L1b 필요 여부 포함) → S2b·S5·S6b·S7(새 작업자) → L2 → 최종 독립 검수 → 조건 충족 시 반영



## 2026-11-04T16:55:03.592Z

요약: L1 통과(메모 있음): 불일치 로그 4종 0, 새 ERROR 0, 목표 끝 갈래·가짜 브리지 순서·보고 키 24개 모두 L0과 같음, 끄고 켜면 목표 초기화(2번), 조립기 기록 864회 형식 통과 → L1b 불필요. 되돌림 다름 0

근거: [7-L1.md](../../../../../../work/2026-11/2026-11-04-리팩토링/7-L1.md), runs/L1/(logshape-allow.txt 포함). rf-pre-L1(8a55bd0), 새 월드 rftest 같은 시드.
- 두 회차 모두에 나온 갈래(LLM 따라가기 완료 cleared>EXP>SKILL CANCELLED>report goal_done, 기지로 완료, LLM 모으기 완료, 모으기 막힘)는 줄 모양·순서가 같다. L1에만 나온 기지·보관 막힘, 탐험 끝은 rf-S0과 조건·문구가 같지만 L0 기준이 없다. '기다림만' 막힘과 LLM 출처 막힘 보고는 두 회차 모두 안 나왔다.
- logshape 새 모양(Player 6, decisions 7)은 모두 코드 위치로 설명(S6a ASM 줄, 상황 차이, 시험 명령). replay pick/check 지적 5/10은 L0 6/11과 같은 종류(가짜 브리지 0초 응답).
- 실제 claude 호출 3번 성공(1번 /ai stop 중단, 1번 브리지 끔으로 끊김), 사용량 누적 약 676호출·$0.109 증가(되돌리지 않음, 계획대로).
- 되돌림: rftest 게임 안에서 삭제(클라우드도 삭제 확인) → 백업 복원 → 다시 켜 확인 → 해상도 레지스트리 되돌림, 최종 다름 0. Steam remotecache는 회차 뒤 상태라 saves/3.json·maps/3/2.mapparts.gzip(백업 내용)를 Steam이 올린다 — 원래 내용이므로 문제없음.
- HEAD 결정: ① 조립기 BodyCan·Doable이 false인 경우가 864회 중 0번 → S6b에서 손으로 만든 기록으로 core-tests를 채우고(옛 코드 판정식을 그대로 옮긴 것은 movecheck로 확인), L2에 가방 가득·도구 없음 시나리오를 넣어 일관성을 본다. L1b는 하지 않는다. ② asmcheck.mjs CRLF 한 줄 수정은 다음 작업자가 한다. ③ '기다림만' 막힘·LLM 출처 막힘은 core-tests 오프라인 시험으로 덮고, L2에서 재현을 시도한다.

다음: S2b·S5·S6b·S7(새 작업자) → L2(새 작업자) → 최종 독립 검수 → 조건 충족 시 반영



## 2026-11-04T17:30:22.237Z

요약: S2b·S5·S6b 완료(rf-S2b 2b98fe8, rf-S5 89ad81e, rf-S6b 4755173): 결과 코드 전환, AIBrain partial 분리·AIDecisionsApi·AIActions(20종)·BuildContext, Unity 없는 후보 조립기가 L1 옛 코드 기록 854/854 정확히 일치, 생성기는 CandBuild 중첩 클래스로 글자 그대로 이동. AIDecisions.cs 2,992→182줄. S7 남음

근거: [8-S2b~S7.md](../../../../../../work/2026-11/2026-11-04-리팩토링/8-S2b~S7.md). 관문 전부 통과(core-tests 152, Unity CompileCheck ok), movecheck로 옮긴 본문 줄 변경 0 확인. HEAD가 worktree 깨끗함·태그 확인.
- S2b 계획과 다른 점: 스킬 끝 중 쓰러짐(down)·처치(kill)는 L1에 나왔고 불일치 0이라 값으로 판단하게 바꿈, 길 없음(skill-nopath)은 실게임에 0번이라 문장 판단 유지(L2에서 확인 후 바꿈, SkillRule 한 줄). 불일치 로그 4종은 시험 스위치(testcmds.on)일 때만 남김. HEAD 판단: 계획의 'L1 불일치 0이면 전환' 규칙에 맞아 받아들인다.
- 의도한 동작 차이 1: 제작 막힘 문구 자체에 '기다리기뿐'이 들어가면 only_wait 대신 cannot_craft로 기록(L1 0번). HEAD 판단: F6 결정(결과·자리로 정해지는 분류는 코드로)의 결과로 받아들이고 사용자 보고에 적는다.
- S5: AIBrain → AIBrain.cs + AIBrain.Loop.cs(2,086줄 글자 그대로), AIDecisionsApi(Post/Read→DecisionReply), AIActions(종류별 Make/Stale 20종), BuildContext(빌드 임시 필드 12개, [ThreadStatic] 표시).
- S6b: Core/AICandidateAssembler(Select/Mark/Finish), core-tests AssemblerReplayTests가 L1 기록(fixtures/asm-L1.log.gz) 854 완전 빌드 정확 일치, 한도 13으로 바꾸면 28개 실패해 잡힘 확인. BodyCan/Doable false는 손 기록 H1~H3. 생성기는 AIBrain.Candidates.cs(CandBuild, 블록 14개+Assemble), 준비 경로 AIBrain.Prep.cs. CloseFight 판정은 관찰을 읽어 AIBrain에 남김. asmcheck CRLF 수정. StallTests 13(기다림만·LLM 출처 막힘 오프라인).
- 남은 주석 하나: AIBrain.Loop.cs 머리 주석에 'preparation'이 남음(글자 그대로 옮기려고) — 정리 단계에서 고친다.
- 작업자 교체 이유: 작업자가 대화 길이로 강제 반환됐고, 생성기 분리는 그 작업자가 띄운 보조 작업자가 끝내 원 작업자가 확인 후 태그.

다음: S7(새 작업자: AISlot·Get<T>, StateOf 16곳) → L2(새 작업자, 8-S2b~S7.md 체크리스트) → 최종 독립 검수 → 조건 충족 시 반영



## 2026-11-04T17:36:52.789Z

요약: S7 완료(rf-S7 e0c671b): AISlot 이름 칸 0~15·Get<T>, StateOf 16곳 교체, 시험 스위치일 때만 캐릭터 범위 밖 Cur 읽기 집계, 머리 주석 정리. 관문 전부 통과(Unity 포함). L2 실게임을 새 작업자에게 맡김

근거: [8-S2b~S7.md](../../../../../../work/2026-11/2026-11-04-리팩토링/8-S2b~S7.md) S7 절. movecheck: 결정 경로 코드 변경 0. 관문: compile-check 0, core-tests 152, exp-sim OK, recall-loop 41, bridge-wiring 21, memgraph 68, protocol 63, Unity error CS 0.
- Cur 집계(계획의 선택 항목): testcmds.on이 꺼져 있으면 동작 같음. 켜져 있으면 60초 창마다 'TEST-ONLY Cur outside scope' 줄과 호출 경로 3개. 한계: Enter(null) 범위 안 읽기도 밖으로 셈, 마지막 창은 다음 읽기가 있어야 찍힘.
- L2 맡김: rf-S7 설치, 새 rftest(같은 시드), 시나리오 1~11·다시 켜기 2번 + 가방 가득/도구 없음, 길 없음 강제, LLM 목표 중 /ai nonew, /ai all on 네 캐릭터, 둘 이상 동시 실제 LLM 적용, Cur 밖 호출 경로 수집, PERF. 결과 9-L2.md, runs/L2/.

다음: L2 결과 판정 → 최종 독립 검수 → 조건 충족 시 ai-character 합치기·GitHub push·worktree 정리 → 사용자 보고



## 2026-11-04T18:51:32.470Z

요약: L2 통과(메모 있음): rf-S7 설치, 불일치 0·ERROR 0·예외 0, 네 캐릭터 동시 접속·동시 LLM 적용 상태 섞임 0, 가방 가득/도구 없음·길 없음·LLM 출처 막힘·Doable false 모두 실게임 확인, Cur 범위 밖 읽기 0, 조립기 2406회 일관. 되돌림 다름 0. 최종 독립 검수로 넘어감

근거: [9-L2.md](../../../../../../work/2026-11/2026-11-04-리팩토링/9-L2.md), runs/L2/(약 41MB, 비밀값 검사 통과).
- 비교: L0/L1과 다른 동작 없음. logshape 새 모양(Player 9·decisions 4)은 rf-S0과 같은 코드 줄의 시험 명령·상황 차이로 설명(runs/L2/logshape-allow.txt). 보고 JSON 60개 키 순서 같음. back_up 보고가 rftest에서 처음 나옴(시나리오 13). PERF: AI 1명 Claude mod per AI 0.431→0.464ms, 4명 약 29.4fps.
- 실제 사용량: claude 11·gpt 13(CLI 17)·gemini 17·grok 7 결정, 오류 0. 하위 판단 API +2378회 +$0.36(이달 $9.72).
- 되돌리지 않음(설계대로): decisions-usage.json, Steam remotecache.vdf, L0 때의 maps\3\2.mapparts.gzip.
- HEAD 결정: ① skill-nopath는 길 없음 한 갈래만 확인되고 막힘·시간 초과는 실게임 미확인이라, 검증된 상태를 반영하려고 이번에는 문장 판정 그대로 두고 값 전환은 할 일로 남긴다. ② BodyCan 체크리스트의 '공격·부수기 뺌' 기대 문구는 문서 오류(코드는 rf-S0과 같이 한 줄만 씀). ③ 시나리오 13은 '/ai all stop' 뒤 '/ai all goal auto'가 필요 — 다음 실게임 절차 메모. ④ Cur 범위 밖 셈 코드는 시험 스위치 전용이고 자연어 입력 작업(캐릭터별 받은편지함)에서 쓸 감시라 남긴다.
- ai-character는 아직 3c3adbe라 rf-S7로 fast-forward 가능(97파일 +7010 −3177).

다음: 최종 독립 검수(3c3adbe..rf-S7, 막음 0이 반영 조건) → ai-character ff·GitHub mine/main 모드·브리지 런타임만 push·worktree 정리 → 사용자 보고



## 2026-11-04T19:04:57.226Z

요약: 최종 독립 검수: 판정 '먼저 고친 뒤 merge' — 막음 1(B1: S1 정식 판독이 LLM 답의 \t·\r·\uXXXX를 실제 제어 문자로 풀어, 거절 문장·목표 문장을 거쳐 다음 보고 JSON이 깨지고 그 캐릭터의 위쪽 LLM이 멈춤), 고칠 것 2(push 때 브리지 모듈 넷 함께, 커밋 밖 Unity 생성 변경 섞지 않기). 반영 조건 미충족이라 B1 수정 맡김

근거: [10-최종검수.md](../../../../../../work/2026-11/2026-11-04-리팩토링/10-최종검수.md). 오프라인 관문 10개 통과(core-tests 152, recall-loop 41, bridge-wiring 21, memgraph 68, protocol 63).
- B1 경로: 답 goal.item="Copper\tOre" → 거절 문장(AIPlanner.cs:383)이 _rejected에 남음 → AIPlanReport.Esc(:88)는 \ " \n만 처리 → 브리지 JSON.parse가 500 → 30·60초 재시도 뒤 Down(300s) 순환, 다른 계기 보고도 같은 _rejected를 실어 실패. 예전 Field()는 escape를 풀지 않아 문제없었음. 같은 뿌리: Enum.TryParse가 앞뒤 공백 허용 → "CopperOre\t"가 목표로 받아들여져 _lastGoalText로 보고에 실림.
- 수정 방향(검수 권고): Esc가 \r·\t·U+0000~001F를 \n처럼 공백으로. 올바른 보고 바이트는 그대로라 replay 비교·실게임 재검증 불필요. 기능 3(자연어) 전에도 어차피 필요.
- G1: bridge.mjs가 plan·preflight·protocol.mjs를 import하므로 mine/main push에 넷을 함께(빠지면 ERR_MODULE_NOT_FOUND). push 대상: 모드 .cs/.meta(Scripts/Core 포함), HeadlessSdk.cs, 브리지 JS 넷. 제외: replay.mjs, Tools/ai-bridge/test/**, Tools/core-tests/**, Tools/compile-check/**, 루트 .gitignore, Tools/ai-bridge/data/.
- G2: worktree의 커밋 밖 Unity 생성 변경(ConditionsTable.asset 등)·추적 안 된 파일은 반영에서 빼고 worktree 지울 때 버린다.
- ai-character = 3c3adbe라 fast-forward 가능, 새 .meta guid 13개 충돌 없음, 디버그 잔재·비밀값 없음.

다음: B1 수정(rf-final)·관문 → 검수자 재확인(막음 0) → ai-character ff·mine/main push(G1 목록)·worktree 정리 → 사용자 보고



## 2026-11-04T19:12:06.280Z

요약: 리팩토링 완료·반영: 최종 검수 막음 1(B1)을 고쳐 재확인 막음 0 → 사용자 조건부 승인대로 ai-character를 rf-final(e82eb87)로 fast-forward, GitHub mine/main에 모드·브리지 런타임만 1c2a61b push, refactor·refactor-bridge·publish worktree 정리

근거: [10-최종검수.md](../../../../../../work/2026-11/2026-11-04-리팩토링/10-최종검수.md)(재확인 절, 기능 (1)~(4)별 이제 고칠 곳 표), [11-B1수정.md](../../../../../../work/2026-11/2026-11-04-리팩토링/11-B1수정.md).
- B1 수정 e82eb87(태그 rf-final): AIPlanReport.Esc가 U+0000~001F를 공백으로, core-tests B1 9개(rf-S7 Esc로는 Node JSON.parse 실패, 정상 보고 바이트 같음). 관문 10개 통과(core-tests 161), Unity error CS 0. 검수자 재확인: 막음 0, rf-S7..rf-final 변경은 세 파일뿐.
- 반영: 메인 트리 ai-character ff → e82eb87(커밋 밖 SDK 설정 7개는 겹치지 않아 그대로). GitHub mine/main adabaa4 → 1c2a61b: Assets/AIPlayerSpike/Scripts(Core 포함, .meta), HeadlessSdk.cs, Tools/ai-bridge bridge·plan·preflight·protocol.mjs. rf-final과 경로 차이 0, 비밀값 없음(키는 환경변수에서 읽는 코드뿐). 시험 도구(core-tests, compile-check, test/, replay.mjs, .gitignore)는 올리지 않음. 한 커밋으로 올림(이전엔 원본 커밋마다 하나).
- 정리: worktree -refactor(커밋 밖 Unity 생성 변경·복사한 dll·시험 자료·.head 색인은 버림), -refactor-bridge, -publish 제거. git 브랜치 ai-refactor·ai-refactor-bridge·publish-refactor와 태그 rf-*는 되돌릴 지점으로 남김.
- 게임 설치본은 L2 되돌림으로 리팩토링 전 빌드 그대로다. 다음 실제 플레이·기능 작업 전에 ai-character e82eb87로 빌드·설치해야 한다.
- 최종 검수가 본 다음 기능별 남은 다중 수정 지점: (1) 호스트 목표 아래 LLM — 목표 끝 7곳→1곳·출처 해석 5곳→GoalSource로 줄었으나 AIGoal이 아직 한 칸이라 '지시+하위 목표' 두 층은 Goal을 읽는 곳 전체에 닿음, 플래너 정책이 AIPlanner 다섯 자리에 흩어짐, 시스템 끝 셋(비용 상한·키 없음·연속 실패)은 EndGoal을 안 거침. (2) 전투 — 종류는 AIActions 한 줄, 생성기 파일 하나, 보류·긴급 규칙과 IsMove 문장 판정은 표 밖. (3) 자연어 — 판독기·소유자 해결, 받은편지함은 '마지막 것이 이김' 한 칸이라 대기열로 바꿀 때 _pendingTrigger 12곳 판단 필요, 결정 종류 표가 모드·브리지·brief 세 곳. (4) 기억 — 국소적, events 스키마를 C#·memgraph가 따로 앎.

