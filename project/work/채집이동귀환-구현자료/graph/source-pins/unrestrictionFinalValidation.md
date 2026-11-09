# 제약 해제 최종 검증

최종 통합 기준은 **5cc5a43ff8df91634c04b44bac9f15f849bdb611**이다. 마지막 제품 소스 변경은5cc5a43이다. 앞선84ba9f2는전투생활Stale를고쳤고6637e8e는시험스냅샷만동기화했다. 공식 출발은5a05b80이다. 설치·게임 플레이·새 모델 호출은 하지 않았다.

## 결과

|검사|결과|로그/범위|
|---|---|---|
|core-tests|1396 PASS|core-final.log. 목표·계획·지시·후보·장소·가드 회귀|
|bridge protocol|188 PASS|protocol-final.log. 내부188검사|
|배치·장거리·기준점 production|47 PASS|restriction.log. 실제 PlaceTilesSkill/GoalDone/Remaining/ShapeList/shared ArrivedAtBase 원본추출, Core AIPlaces/TunnelSearch 직접링크|
|생활 production|98 PASS|life-final.log. 실제Protocol→목표→목록→실행/수령/잔량,6번째직접실행·None/combat보존·실제Stale경유전투조회·원격base진척거부|
|제작 production|29 PASS|actions-base-craft.log/craft-tests.log.20000목표→9998+9998+4 정상커서요청·수령/잔량·intMax overflow|
|전투/손 입력 production|43 PASS|actions-input.log. 실제후보→스킬→BodyInput, 소환미최대/최대충전해제 구분|
|가방 production|152 PASS|actions-base-inventory.log. 보유7종실제손선택·정상swap·수령,100칸원격base와생략목적지구별,blockedanchor3칸stand도착|
|정비 통합 production|115 PASS|maintenance-final.log. 소유권/버퍼/양슬롯/수리·강화·회수/목표진척 유지. 전투중실제수리선택→Apply→정상요청1회→수령1 확인|
|요리 production|51 PASS|cook-final.log. 실제후보/투입/수령과상태회귀|
|공통 실행 production|55 PASS|execution-final.log. 실제Apply/Stale/실행관측/보류/몸동작/목표·지시보존/호출·비용gate|
|기억검색|84 PASS|info-memgraph.log. 실제지식누출차단·긴원문·전체목차·넓은반경|
|검색호출루프|41 PASS|info-recall.log. deadline/검색횟수 보호|
|정보보존 production|7 PASS|info-retention.log. 실제ExpReport/ChatLog/FightLog 직접링크|
|경험회귀·100시간합성|ALL CHECKS OK|info-experience.log.1339회보고포함|
|실제게임DLL참조 .NET 전체컴파일|경고0/오류0|compile-final.log|
|Unity 실제 전체스크립트컴파일|CompileCheck ok, exit0|unity-producers-final.log:1874 및 unity-producers-final-exit.txt|

변경영역이 같은 소스인 검사 결과는 다시 실행하지 않고 통합했다. 마지막 생활Stale 변경 뒤에는 생활·정비·요리·공통실행·전체.NET·Unity를 검증했다. 마지막생성입구묶음뒤에는해당입구생산회귀47·core1396·전체.NET·Unity를새로통과했다. 제작·가방·전투·기억 소스는 각 최종 소유 검증 이후 변경되지 않았다.

## 대표 경로와 경계

배치 전체200칸 요청을128칸 평가 묶음 뒤에도 보존하고 수령200개를 유지한다.64개 기록상한의 진척후퇴가 없다.100M칸 shape는 lazy뷰로 마지막 좌표를 조회하며 signed int좌표 한칸·넓이 overflow를 검증한다.128칸 전부건너뜀/기존배치도 다음72칸으로 넘어가고 기존배치를 신규배치성과로 세지 않는다. 실제Core 길찾기는400칸 대상과 signed좌표wrap방지를 검증한다.

명시base100칸밖은 실제InArea/Arrived guard로현재자리배치를막고 기준점도착뒤같은PlaceTilesSkill로base칸수령을 확인한다. 막힌기준점대신3칸밖정상stand도착은 실제공유ArrivedAtBase함수로인정한다. 가방은실제CandidatesFor/메뉴목표결합/Stale/수령경로에서같은도착기준을쓰며 원격상자를base성과로세지않는다. 기준점으로 이동하는 물리월드·경로소비자는adapter이므로 실제게임왕복성공을 주장하지 않는다.

기존854개 assembler replay는 당시기록된후보/판정의교집합으로기존정렬·사실을검증한다. 새로노출된후보를옛로그에없다는이유로실패시키지않되새계약전체후보회귀를별도로추가했다. replay전체를skip하지않았다. 정비의전투자동배제/자동뷰양도기대는 전투중조회/실행유지 및 명시close후가방전환으로바꿔원래토큰·pending·수령검증을유지했다. execution runner는BOM없는생산UTF8소스를명시UTF8로읽도록고쳤다.

## 최종 Unity와 source 동일성

`Unity.exe -batchmode -quit -projectPath C:/Dev/CoreKeeperModSDK-unrestricted -executeMethod HeadlessSdk.CompileCheck -logFile .../unity-producers-final.log`만 실행했다. BuildAndInstall은 호출하지 않았다. `CompileCheck: ok`, exit0, 정상shutdown을 확인했다.

최종 `Library/ScriptAssemblies/AIPlayerSpike.dll`: 2026-11-07 17:27:20,1580544바이트. SHA256은unity-final-artifact.txt에기록했다. 최종커밋과검증시제품source가같음을확인한blob은 AILife=`b089661644e5a96fbf7d2038b48e0a3c219f60f8`, AIInventory=`25bafb96c7e3b669d70ec1264ee1595133fdd0e9`, AIBase=`6eab05db3cc654c71c999673b7ecdc6aa81bed4d`이다. 제품source미커밋차이는없다.

검증환경은설치된SDK DLL을격리worktree에읽기용복사하고USE_PUG_OTHER define을일시추가했다. Unity가자동변경한Packages/ProjectSettings/ConditionsTable asset과.vsconfig는이작업도구부산물만정리했다. 미추적Assets/Plugins/CoreKeeper/는검증용SDK복사이며제품커밋에포함하지않았다. 공식main의사용자설정은수정하지않았다.

초기unity.log의entitlement404는진행중로그였고실제로CompileCheck ok까지진행했다. 중간unity-final/anchor/release검증은해당시점소스의역사이며최종성공근거는unity-producers-final과위blob동일성이다.

## 완료 경계

경계adapter의World/ECS·정상요청소비·네트워크·충돌·관측은시험용이다. 실제투사체명중·소환생성·마나소비·모든모드아이템조합·실서버왕복·실제모델정책행동은검증하지않았다. 합의된새원정/갈림길이동전체구현의완료를의미하지않는다. 독립수용판정은review.md를따르며공식통합·설치·게임플레이는HEAD가별도처리한다.


중간6637e8e는시험스냅샷3파일만동기화한커밋이었다. 그후고정입구목록을5cc5a43에서추가수정했으므로최종제품소스와Unity는5cc5a43을기준으로한다.


## 마지막 생성입구 일괄 대조

5cc5a43에서 고정된추가목록만수정했다: None/combat의AIFood.Candidates호출입구, None의AIEdge/Guard호출, Follow/Explore/Free로한정했던화면내동료따라가기, gather+wantedFirst만보던기억광석, 목표수확품과일치하는야생미성숙식물제외, 가방음식0일때만만들던보급및첫best1개종료다. 후보노출과목표진척을분리해다른광석/동료이동/미성숙수확을허위목표성과로세지않는다. 자기성장중밭·다른소유밭·동일key·정상도구/경로조건은유지한다.

restriction47검증은실제생산Food/AIFood.Candidates/FoodTakeCandidates/RememberedOres와원본Prep/Follow/식물입구slice를사용한다. 허기90에서None/Gather×전투/평화4조합의식품생성·3개상자/품목보급출처와최종Coreassembler보존을검증한다. None의edge/guard2호출, 일반목표의동료따라가기, 캐릭터기억2광석/목표진척분리, 야생미성숙식물허용/소유보호도확인한다. ECS/도구/상자이동소비자/관측은경계fixture이며이검증을실게임행동성공으로표기하지않는다.

최종AIBrain.Candidates source blob=`a23b2e95d15b2f72a2c9948dbee64904ea952c20`. 최종Unity프로세스정상exit0,환경부산물원복,제품source미커밋없음. 독립review.md의고정범위판정을최종수용근거로따른다.


생성입구 회귀 재현 명령(저장소 루트):
```powershell
node Tools/restriction-tests/prepare.mjs
dotnet run --project Tools/restriction-tests -c Release
```
생성된 Generated.cs와bin/obj는시험실행부산물이며추적하지않는다.
