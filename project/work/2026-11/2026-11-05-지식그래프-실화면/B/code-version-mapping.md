> 현재 공식 소스 기준은 `39b22df96dffe869ab0ccd474b18d31462b71d70`다. 생활·가방 통합 소스/오프라인·Unity/sourceSDK 한정 수용이며,설치·실게임·외부모델은미검증이다. 아래과거기록은당시근거로보존한다.

> 공식 코드 기준: `11eb461ae122f404acec94563c086b09e002987c` (2026-11-06 사용자 승인 후 UI 흡수판). 본 ai-character와 일치. 현재 실게임·수용 근거는 work/ai-panel-integration-20261106/04-live-verification.md 및08-acceptance.md, 통합 결과는09-final-integration.md. 아래 과거 기록은 당시 근거로 보존한다.

> 공식 코드 기준: `b25099f6fd31e4c4c3618357eb9e8101e0e17124` (2026-11-06 사용자 병합 승인 후 적용). 본 저장소 ai-character와 기준이 같다. 공식 적용 결과는 work/2026-11/2026-11-06-선택지개선/08-merge-and-graph.md를 따른다. 과거76563bf/e82eb87/902721e 증거와 이번 b25099f 한정 실게임 관측은 구별한다.

# 코드 기준판·설치판 대응

> **현재 공식 코드 기준은 `902721ec505df42d74d0b2d245ed34a049faadad`다.** 화면 관측 설치 소스는 과거 `76563bf`로 유지한다. 아래 본문과 갱신 이력의 “지금 설치본”은 각 기록 당시의 설치 상태를 가리키며, 현재 설치 상태나 이번 리팩토링의 실행 확인을 뜻하지 않는다. 최신 정적 대응은 `existing-node-mapping.json`과 `C/overlay-source.json`(현재 폴더 기준 `../C/overlay-source.json`)에 있다. 이동 전 경로를 따라 비교한 51개 중 39개 동일 소스 구간은 새 실측 증거로 승격하지 않았다. 현재 결과·한계는 `work/리팩토링-20261106/14-graph-stage-result.md`를 따른다.

> 2026-11-06 갱신(내 문 판정 반영): 코드 그래프 기준이 `a07166f`로 바뀌어 `remap-base.mjs`로 a07166f→76563bf 대응을 다시 만들었다. 51개 구간 중 39개가 같은 본문이고, 다른 12개는 9df72b7 때와 같은 목록이다(내 문 판정은 이 대응 구간을 새로 바꾸지 않았다).
>
> 2026-11-06 갱신(물건 놓기 반영): 코드 그래프 기준이 `9df72b7`로 바뀌어 `remap-base.mjs`로 9df72b7→76563bf 대응을 다시 만들었다. 51개 구간 중 39개가 같은 본문이고, 다른 12개는 99c9a95 때와 같은 목록이다(물건 놓기는 이 대응 구간을 새로 바꾸지 않았다).
>
> 2026-11-06 갱신(코어 기지 안전 반영): 코드 그래프 기준이 `99c9a95`로 바뀌어 `remap-base.mjs`로 99c9a95→76563bf 대응을 다시 만들었다. 51개 구간 중 39개가 같은 본문이다. 자연어 명령으로 바뀐 7개에 더해 기지 안전으로 바뀐 5개(`AIBody.HandleCommand`·`IsSkillCommand`·`StartSkill`·`TestCommands`, `AIMemory.Ensure`)는 화면 관측 때와 같은 본문이 아니라 잇지 않는다.
>
> 2026-11-05 갱신(자연어 명령 반영): 코드 그래프 기준이 `b2fa4cc`로 바뀌어 `remap-base.mjs`로 b2fa4cc→76563bf 대응을 다시 만들었다. 51개 구간 중 44개가 같은 본문이고, 7개(`AIGoals.HandleGoalCommand`, `AIPlanner.StatusText`, `AIPlayerSpikeMod.Dispatch`·`InstallPatches`·`Reply`·`TryHandleChatCommand`·`Update`)는 자연어 명령으로 바뀌어 대응하지 않는다. `remap-base.mjs`는 화면 관측 판(76563bf)에 없던 파일의 같은 이름 함수(`AIHostOrders.Find`, `AIMention.All`·`Members`)를 고르지 않게 고쳤다. 지금 설치본은 b2fa4cc다([반영 기록](../../2026-11-05-자연어-명령/13-반영.md)).
>
> 2026-11-05 갱신: 코드 그래프 기준이 `fc8161b`(전투 단계 2 반영)로 바뀌어 `existing-node-mapping.json`을 `remap-base.mjs`(audit-code.py의 대응표 부분을 Node로 옮긴 것, e82eb87 입력으로 옛 파일을 바이트까지 다시 만듦을 확인)로 fc8161b→76563bf 대응으로 다시 만들었다. 51개 구간 모두 같은 본문이다(전에 달랐던 `AIBody.StartSkill`도 fc8161b에서는 76563bf와 같다). `version-evidence.json`과 아래 본문은 화면 관측 때(설치 76563bf, 그래프 e82eb87)의 기록으로 그대로 둔다. 지금 설치본은 fc8161b다([반영 기록](../../2026-11-05-전투-단계2/8-반영.md)).

2026-11-05. 독립 B 코드 관점. 새 A 화면·조작 기록·이미지는 입력하지 않았다. start-job, C:/Dev/AGENTS.md, 02a-graph-definitions.md, 02b-graph-workflow.md를 읽었다. 관련 checkout과 작업 경로에 추가 AGENTS.md는 검색되지 않았다. 제품 코드·설치·월드·프로세스를 변경하거나 실행하지 않았고, 이 B 폴더의 보고서와 비교 스크립트만 작성했다.

## 결론과 증거 강도

- 기존 그래프 기준은 `e82eb87f25641cc7c31e6216f64e20a34e58d0c0`이며 C:/Dev/CoreKeeperModSDK HEAD도 같다. 기준의 기존 분석 파일 33개는 고정 snapshot 바이트와 Git blob이 모두 일치한다.
- 설치판 후보는 `76563bf3ac3af56241bb796c6510ad7e96c3ccd7`. C:/Dev/CoreKeeperModSDK-combat-s2 HEAD `e87866e02bfc2ce55864f0f6abd254e83e0662ac`는 Tools/core-tests/logshape-allow-combat-s2.txt만 추가한다. 두 커밋 사이 Assets/AIPlayerSpike 차이는 없다. C:/Dev/_scratch/combat-s2 자체는 저장소가 아니라 빌드·실행 기록 폴더다.
- **현재 설치 C# 30개 전부가 76563bf의 해당 소스와 CRLF→LF 정규화 후 전체 SHA-256이 일치한다.** 원바이트는 11/30 일치한다. 나머지는 줄바꿈 차이이며 정규화 전후 해시를 모두 `version-evidence.json`에 보존했다. 설치 경로는 `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`이다. DLL 배포로 단정하지 않는다. 현재 패키지는 ModManifest.json과 C# 스크립트 30개다.
- 현재 ModManifest.json의 JSON 내용은 `C:/Dev/CoreKeeperModSDK-combat-s2/Logs/headless-build-R3c.log`에 기록된 manifest와 동일하다. 이 로그 1982줄은 BuildAndInstall done과 실제 설치 경로, 2152줄은 return code 0이다. `compile-R3c.log:1874`는 CompileCheck: ok이다. `_scratch/combat-s2/R3/notes.txt:35`의 76563bf 설치 기록과도 맞는다. 이 notes의 다른 조작 결과는 근거로 사용하지 않았다.
- 디스크 소스 최종 수정 시각은 2026-11-05 01:01:16.072Z. 조회한 CoreKeeper PID 20120은 01:01:31.3861029Z에 해당 Steam CoreKeeper.exe에서 시작했다. **따라서 76563bf 설치 후 시작된 프로세스라는 강한 정황까지 있다. 메모리 내 컴파일/로드 어셈블리의 해시, 실제 어떤 함수가 호출됐는지, 관측 시점에 어떤 데이터가 표시됐는지는 증명하지 않는다.** 설치 해시·시작 시각을 runtime coverage로 바꾸면 안 된다.
- 기존 분석 파일 33개 중 23개는 기준↔76563bf Git 바이트 동일, 10개는 변경. 선택한 기존 AST 노드 51개 중 50개 소스 구간은 실행 후보에서 그대로 발견됐다. 유일한 변경 표본은 AIBody.StartSkill의 run 번호 부여다. 전체 전투 처리 동일성을 주장할 수는 없다.

## 통합에 사용할 기존 기능 ID

하위 명령을 새 API/기능으로 만들지 않는다. 기존 기계 추출 `slash:ai` 하나가 `/ai` 및 하위 명령을 받는다. 다음은 원본 소스로 확인한 매칭 후보이며, 새 A 관측 사실은 D에서 결합해야 한다.

| 대상 | 기존 기능 ID | 요청·출력 근거 |
|---|---|---|
| 채팅 명령 가로채기 | `hook:AIPlayerSpikePatches.ChatSendPrefix` | AIPlayerSpikeMod.cs 기준 1409–1421 → 설치 후보 1410–1422. stream integration 메시지는 제외하고 TryHandleChatCommand에 넘김. |
| `/ai`, `/ai status`, `/ai 상태`, 명시 멤버 상태 | `slash:ai` | TryHandleChatCommand 412–470, Dispatch 489–555. 상태 전체 454, 단일 대상 507–510. |
| 알 수 없는 명령의 도움말 | `slash:ai` | Dispatch 539–553, 특히 552의 고정 안내 문구. testcmds.on 여부에 따라 시험용 명령 목록 추가. `/ai help` 전용 feature는 없음. |
| 인자 없는 `/ai goal`·`목표` | `slash:ai` | AIBody.HandleCommand→AIBrain.HandleGoalCommand→AIGoals.HandleGoalCommand 105–111. 인자 없을 때만 상태 출력 분기. |
| 가방 `/ai inv`·`가방` | `slash:ai` | AIBodySkills.cs 기준 271–273 → 실행 후보 277–279, StartSkill(new InvSkill). 테스트 스위치 및 입장 상태 조건 적용. 순수 조회가 아님. |
| 기억 `/ai mem`·`기억` | `slash:ai` | AIBodySkills.cs 기준 577–581 → 실행 후보 583–587. AIMemory.Summary 출력. 테스트 스위치 및 입장 상태 조건 적용. |
| 답글을 다음 프레임 채팅에 표시 | `hook:AIPlayerSpikeMod.Update` | Update 252→FlushReplies 682–703. Reply 674–680에서 queue에 넣고 FlushReplies 696에서 게임 ChatWindow.RenderText에 reflection 호출. |
| AI 캐릭터 자료 주입 | `hook:AIPlayerSpikePatches.CharacterDataPostfix` | 기준 1445–1462 → 실행 후보 1446–1463. spawn 요청 문맥일 때 준비된 AI CharacterData의 server copy로 교체. |
| AI 입장·표시 데이터 준비 | `hook:AIPlayerSpikeMod.Update` | Step2_NameAndPrepareInput 1078–1122, AICharacterStore.Prepare/CreateNewCharacter/DistinctLook. 게임의 일반 캐릭터 렌더러는 추출 범위 밖. |

모든 기능 ID는 기존 `개편/B/code.graph.json`에서 확인했다. 정확한 전체 코드 ID·원본 줄·실행 후보 줄·기존 간선은 `existing-node-mapping.json`에 들어 있다. 아래 표의 ID 표기는 `code:Assets/AIPlayerSpike/Scripts/<파일>#<서명>`을 결합한 기존 ID 그대로다.

| 파일 | 기존 코드 ID의 # 뒤 서명 | 기준 줄 → 76563bf 줄 | 확인한 의미 |
|---|---|---|---|
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.TryHandleChatCommand(string)` | 412–470 → 동일 | 호스트 월드 확인, 멤버/all/default 선택과 명령 분배 |
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.Dispatch(World, string, string[], int)` | 489–555 → 동일 | 테스트 명령 차단, 입장 상태 조건, 안내 출력 |
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.StatusAll()` | 660–670 → 동일 | 4명 이름별 켜짐/꺼짐·입장 단계, Walking일 때 목표 |
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.StatusText()` | 654–658 → 동일 | 이름·id·월드 활성화·단계·Walking 목표 |
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.StageText()` | 637–652 → 동일 | 들어와 있지 않음/곧 들어옴/들어오는 중/들어와 있음/오류 문구 |
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.Reply(string)` | 674–680 → 동일 | 멤버 이름 접두사와 pending queue |
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.FlushReplies()` | 682–703 → 동일 | ChatWindow.RenderText reflection, 창/메서드 없음·예외면 로그 |
| AIBodySkills.cs | `AIBody.HandleCommand(World, Entity, string, string[])` | 262–602 → 268–608 | goal/inv/mem 등 하위 분기 본문 동일 |
| AIBodySkills.cs | `AIBody.IsSkillCommand(string)` | 681–693 → 687–699 | 영어·한국어 명령 인식 |
| AIBodySkills.cs | `AIBody.TestCommands` | 698–703 → 704–709 | inv/가방, mem/기억은 시험용 목록에 포함 |
| AIBodySkills.cs | `AIBody.TestCommandsOn` | 706 → 712 | testcmds.on 존재 읽기 |
| AIBodySkills.cs | `InvSkill.Start(World, Entity, float)` | 1919–1945 → 1937–1963 | 슬롯 번호·ObjectID·수량, 현재 든 칸/아이템, 앞 40칸 빈 칸 수 |
| AIBodySkills.cs | `AIBody.StartSkill(World, Entity, AISkill, float)` | 203–230 → 실행 후보 208–236 | **본문 변경**: `s.Run = ++S.runSeq` 추가. 기존에도 현재 실행 기술 취소·EndSkill 후 새 기술 실행 |
| AIGoals.cs | `AIGoals.HandleGoalCommand(World, Entity, string[])` | 105–170 → 동일 | 인자 없는 조회와 변경 명령을 같은 처리기에서 구별 |
| AIBrain.cs | `AIBrain.StatusText(World, Entity)` | 383–393 → 406–416 | 목표·진척·Planner 상태·호출/적용/버림/실패/미리 묻기·지연·월 사용량 |
| AIPlanner.cs | `AIPlanner.StatusText()` | 106–111 → 동일 | 계획 계층 상태 문구 |
| AIExplore.cs | `AIMemory.Summary()` | 416–420 → 동일 | 본 구역(4×4 셀)·서 본 구역·기억 광석·제작대 개수. bridge 의미 기억 검색과 다름 |
| AIExplore.cs | `AIMemory.Ensure()` | 147–188 → 동일 | 월드별 멤버 기억 확보·로드 및 필요 시 이전 월드 저장/마이그레이션 |
| AIPlayerSpikeMod.cs | `AIWorldSettings.Members(string)` | 1501–1521 → 1502–1522 | settings.json members, 구형 aiEnabled 지원, 없으면 OFF |
| AIAgents.cs | `AIAgent.All` | 63–69 → 동일 | Claude/ChatGPT/Gemini/Grok와 claude/gpt/gemini/grok 표 |
| AIPlayerSpikeMod.cs | `AIPlayerSpikeMod.Step2_NameAndPrepareInput(World)` | 1078–1122 → 동일 | PlayerCustomizationCD.name 및 PlayerGhost.onlineName을 AiName으로 맞춤 |
| AIPlayerSpikeMod.cs | `AICharacterStore.Prepare(string, string, string)` | 1603–1634 → 1604–1635 | 기존 캐릭터 저장 로드, 없으면 생성 |
| AIPlayerSpikeMod.cs | `AICharacterStore.CreateNewCharacter(string, string)` | 1646–1706 → 1647–1707 | 새 CharacterData·name·역할·초기 외형/장비 자료 |
| AIPlayerSpikeMod.cs | `AICharacterStore.DistinctLook(PlayerCustomizationTableDataBlock, PlayerCustomization, PlayerCustomization)` | 1753–1790 → 1754–1791 | 호스트 외형과 다른 index 선택. 실제 색/모양은 게임 customization 테이블과 기존 저장값에 의존 |

## 데이터 연결과 경계

- 상태 활성화: `AIWorldSettings.Members` → 기존 `data:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs:p`의 read 간선(1508,1509). 실제 p는 PathFor(worldId), 기준 1549–1552 / 실행 후보 1550–1553의 `AIPlayers/<world>/settings.json`이다. 실행 중 `_stage`와 Goal 등은 코드 필드이며 별도 저장 데이터 노드와 같지 않다.
- 캐릭터: `AICharacterStore.Prepare` → 기존 `data:Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs:path` read(1613,1615), `:legacy` read(1609). FilePath 기준 1579–1582는 `AIPlayers/<world>/<Name>.json`이다. 저장 파일 내용의 현재 이름·외형·장비를 이번 조사에서는 읽지 않았다.
- 탐색 기억: `AIMemory.Ensure` → 기존 `data:Assets/AIPlayerSpike/Scripts/AIExplore.cs:_path` read(159,161). CharDir 123–127 및 Ensure 155에 의해 `AIPlayers/<world>/<member>/memory.json`. 디렉터리/마이그레이션은 코드 원문에 있고 새 DB 노드로 올리지 않는다.
- 시험용 스위치: 기존 `data:Assets/AIPlayerSpike/Scripts/AIBodySkills.cs:TestSwitchPath`. TestSwitchPath는 persistentDataPath/AIPlayers/testcmds.on. 현재 존재 여부를 변경하거나 확인할 필요 없이 코드 조건으로만 기록했다.
- 가방/든 물건은 `ContainedObjectsBuffer`, `EquippedObjectCD`의 ECS 값이다. 그래프에는 InvSkill.Start 코드가 있으나 게임 metadata 부재 때문에 이 필드를 완전한 schema/DB read 노드로 제공하지 않는다. DB가 없다는 이유로 가방 값이 없다고 답하면 안 된다.
- 채팅 입력창, 일반 메뉴, 캐릭터 스프라이트·이름표·체력바의 게임 본체 렌더링 코드는 이 모드의 33파일 추출에 없다. 모드의 `FlushReplies()`는 화면 표시 경계 후보지만 게임 `ChatWindow.RenderText`의 코드 노드는 없다. boundaries.json에 AIPlayerSpikeMod.cs:690 reflection 외부/미해결이 명시되어 있다. 이름표를 실제로 그리는 코드와 이름 데이터를 설정하는 코드를 구분한다.
- Reply→queue→다음 Update/FlushReplies의 비동기 전달 전체는 직접 call 사슬 하나가 아니다. 기존 그래프에는 Update→FlushReplies call(252)이 있다. FlushReplies→게임 RenderText는 경계이며, 이번 보고서를 근거로 가짜 render/call 간선을 그래프 JSON에 직접 추가하면 안 된다.

## 읽기 전용 탐색에서 주의할 코드 사실

`/ai inv`는 표시 내용이 읽기 결과여도 **행동 상태를 바꾼다**. 기준 StartSkill 205–209는 현재 Running 기술을 Cancelled로 바꾸고 EndSkill을 실행한다. 실행 후보는 추가로 runSeq도 증가시킨다. 기존 그래프/정책에서 이를 무부작용 조회로 적으면 오기다.

`/ai mem`은 AIMemory.Summary→Ensure를 거친다. Ensure가 이미 같은 월드를 갖고 있으면 빠르게 돌아오지만, 아니면 Save(true)(152), CharDir의 디렉터리 생성(123–127), MigrateLegacy의 File.Copy(132–144)가 가능하다. 따라서 모든 상태에서 디스크 읽기만 하는 명령으로 보증할 수 없다. 실제 실행하지 않았다.

인자 없는 `/ai goal`의 108–111 분기는 목표를 바꾸지 않고 StatusText를 출력한다. `없음`/`verbose` 등 인자가 붙으면 변경 분기로 갈 수 있다. 출력 과정의 Reply 로그·queue까지도 엄격하게 말하면 쓰기가 있으므로, 여기서 읽기 조회란 게임 목표/행동을 변경하는 명령과 구별하는 뜻이다.

## 빠진 범위와 통합 결정

1. **기존 ID 재사용 가능**: 명령/도움말·상태·목표 조회·가방 출력·탐색 기억 출력·AI 이름/외형 데이터 준비. 표에 원문 줄과 후보판의 동일 본문을 제시했다. 화면요소 ID는 A/D가 정하고 이 B는 만들지 않았다.
2. **기준판 그래프를 실행판 전체로 바꿔 부를 수 없음**: 76563bf에는 AIChargeWatch.cs, AIShortSkills.cs, Core/AIChargeMath.cs, Core/AIRunGate.cs 4개가 추가됐다. 기존 B 추출에는 없다. 기존 10파일도 변경되어 전투 판단·짧은 행동·돌진 관측·run gate의 노드와 참조는 후속 판 추출이 필요하다. 이번 범위의 표시 본문이 같아도 그 출력의 런타임 카운터/목표/장비 값과 발생 시점까지 같다는 뜻은 아니다.
3. **화면 실측 근거 없음**: 이번 코드 조사만으로 버튼 위치, 게임 메뉴 이름, 실제 줄바꿈/잘림, 이름표 색, 표시 성공을 확정하지 않는다. A 관측은 D에서 동일 출력 문구·명령·상태 조건과 대조한다.
4. **외부 게임 코드 경계**: 메뉴/채팅/캐릭터 실제 렌더러가 요구되는 질문은 현재 그래프에서 미해결이다. 원본 범위 확대 없이 기존 모드 메서드를 게임 renderer라고 대체하지 않는다.
5. **판 일치의 최대 주장**: 현재 설치 소스는 76563bf에 해당하며 e87866e의 제품 코드와 같다. 실행 프로세스 시작 순서도 그 배포와 부합한다. 로드 어셈블리 및 실제 요청별 실행 대조는 미수행이다.

## 산출물과 재현

- `version-evidence.json`: 33 입력 파일의 blob SHA-256, snapshot 대조, 30 설치 소스의 원바이트/정규화 SHA-256, manifest/build log 일치. timestamp는 증거 수집 시각이며 그래프에 넣지 않았다.
- `existing-node-mapping.json`: 원래 기계 ID·줄 범위, 76563bf에서 동일 구간의 위치, 기존 기능/간선/일부 데이터 노드. 생성 그래프를 수정하지 않는 별도 대응표다. `sameSourceSegment:false`는 미해결/변경을 의미하며 자동으로 새 ID를 만들지 않는다.
- `audit-code.py`: 기존 graph와 pinned source를 읽고 Git blob/설치 스크립트를 비교한다. 실행은 제품 서버·게임을 시작하지 않는다. 출력은 이 B 폴더의 위 JSON 두 개뿐이다. Python 실행 파일은 `C:/Users/<USER>/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`를 사용했다.

기존 `B.graph.json`, `code.graph.json`, snapshot, 제품 checkout, 설치 파일에는 쓰지 않았다. HEAD가 이 대응표와 A를 받아 확인·저장한다.
