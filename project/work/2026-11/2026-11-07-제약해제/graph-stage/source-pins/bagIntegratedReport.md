> **소스 반영 완료:** 공식 ai-character HEAD는39b22df입니다. [반영 전후 대조](source-integration.md)에서 기존 환경·미추적 파일·설치·bridge 보존을 확인했습니다. 설치·게임·외부 모델 실행은 하지 않았습니다.

# 생활·가방 선택 통합 후보

고정 소스는 `39b22df96dffe869ab0ccd474b18d31462b71d70`, 격리 저장소는 `C:/Dev/CoreKeeperModSDK-bag-common`, 브랜치는 `bag-life-integration-20261107`이다. 최신 공식 생활판 `c4a0f5e`에서 새 브랜치를 만들고 가방 트리 `741532f`를 병합했다. 두 부모를 보존한 `7dc9c7e` merge 뒤 전투 시 조회 접근 경계만 수정한 판이며 공식 main은 수정하지 않았다. 741의 코드·검수·Unity·SDK 후보는 중간판 증거로 그대로 보존한다.

## 연결한 동작

인벤토리는 분류 → 보유 개별 물품 → 가능한 조작 → 필요한 인자 → 실행 흐름을 유지한다. 현재 자식을 모두 전달하고 뒤로·전체 선택 취소를 제공한다. 생활은 별도로 승인한 작업 → 대상/재료 → 시설/영역 흐름과 완전히 지정된 대상의 직접 실행을 유지한다. 생활의 페이지는 항목을 없애지 않는 기존 조회 방식으로 보존하며, 인벤토리의 무페이지 합의를 생활 전체 재설계로 확대하지 않았다. 일반 후보의 14개/생성기의 숫자 누락은 다시 적용하지 않는다. 요청 작업량, 농사 실행 묶음6칸·요리 묶음5짝, 탐색/물리 guard, 보고 요약량은 후보 개수 cut과 구별해 유지한다.

열린 생활 메뉴에는 가방 루트를 덧붙이지 않고, 열린 가방 메뉴에는 생활 조회를 합성하지 않는다. 다른 조회를 선택하면 그쪽의 이전 커서/token만 폐기한다. `AILife.LeaveNavigation`은 Cursor/Offered만 초기화하며 Progress/Runs/목표를 지우지 않는다. 생활 탐색은 기존 held/queued를 보존한다. 가방 탐색은 승인된 최신 선택권한 규칙에 따라 예전 미적용 답을 취소하지만 이미 진행 중인 몸과 목표는 유지한다. `AIActions.Stale`와 최종 `Apply`에서 폐기된 가방 leaf도 다시 검사한다.

일반 예전 store/take 및 장비 평면 후보는 숨기되, 생활의 완전 지정 저장 요청은 실제 `AIGoalFeatures.AddCandidates` → `AIInventory.Candidates/ExactCandidate`를 통해 직접 노출할 수 있다. 실제 life binding이 있는 후보를 public surface에서 지우지 않는다. c4의 CandidatesFor·정확 슬롯/variation 재판정·공유 작업 수량·receipt와 741의 메뉴/모든 유효 목적지 helper를 함께 보존했다. action kind는 `life_nav`와 `inventory_menu`로 구별된다.

## 실제 검증

| 검증 | 결과 | 근거 |
|---|---:|---|
| 제품 fast compile | 경고0/오류0 | [compile.log](integration/compile-final.log) |
| Core | 1298 통과 | [core.log](integration/core.log) |
| bridge protocol | 154 통과 | [protocol.log](integration/protocol.log) |
| 실제 Loop/공통 실행·모델 JSON | 55/55 | [execution.log](integration/execution-final.log) |
| 실제 가방 메뉴·Inventory 엔진 | 348/0 | [menu.log](integration/menu.log) |
| 생활 대표 실제 입구 | 96/0 | [life-main.log](integration/life-main-final.log) |
| 요리/제작/가방 생활 회귀 | 51/19/91, 실패0 | [생활 회귀 설명](integration/life-regression.md) |
| 두 실제 메뉴·Apply·가방 leaf 연결 | 192/192 | [connected.log](integration/connected-final.log), [trace](integration/tree-connected/connected-trace.json), [소스핀](integration/tree-connected/source-pin.json) |

연결 harness는 실제 `AIInventoryMenu`, `AIInventory`, `AILife`, Core 선택/목표 규칙을 링크하고 실제 Loop.Apply·public surface·BuildJson·AIActions·AIGoalFeatures.AddCandidates 본문을 추출한다. 분류→물품→뒤로→다른 물품→조작→목적지→단 하나의 정상 Swap→관측 완료, 취소 및 inspect-await 유지가 한 흐름으로 이어진다. 생활 완전 지정 store5는 실제 feature 입구에서 MoveAmount5와 출발/도착 변화·공유 receipt를 확인한다. 최종 leaf를 아직 적용하지 않은 채 메뉴를 닫고 생활을 명시 진입한 표본에서는 물품 snapshot이 여전히 유효해도 메뉴 권한이 폐기되어 실제 Apply가 새 몸/요청을 만들지 않는다. 조회 전후 실제 생활 잔량4와 진행 run binding을 보존해 뒤늦은 결과 집계 후 잔량2를 확인했다. 닫힌 메뉴의 과거 open 답이 다시 살아나지 않는 표본도 포함한다.

192개 연결 검사에서 로컬로 구성한 Decisions 요청32개와 탐색30회를 기록했다. 원래 가방 대표 경로의 19개 요청/탐색17회에 양방향 통합 경계가 더해진 수치다. 외부 모델/API·게임 소비자 호출은0이다. 예산 정책을 높이지 않았고 Loop55의 기존 호출/월 비용 제한 검사가 유지된다. 생활 전용 harness에는 가방 metadata/wiring만 대체한 경계가 있으며 실제 가방 정책 검증과 구별한다. DB/World/물리·소비자 상태 변화는 통제된 fixture이고 전체 CandBuild 경로탐색이나 전체 Tick 스케줄러·실제 게임 소비자 성공을 뜻하지 않는다.

## 빌드·반영 경계

최종39b22df 소스로 Unity 6000.0.59f2와 정상 SDK BuildMod 내보내기를 설치 없는 새 경로에서 실행했다. PID52288, exit0, CompileCheck:ok1건, C# compiler error0이다. [입력](integration/final-39b22df/unity-input.json), [설정](integration/final-39b22df/unity-settings.json), [실행/종료](integration/final-39b22df/unity-exit.json), [로그](integration/final-39b22df/unity-export.log), [소스/출력 비교](integration/final-39b22df/artifact-manifest.json)에 근거가 있다. 103개 C#가 실행 전후 같고 SDK payload와 일치하며 공식c4 소스/HEAD와 기존 설치96파일도 불변이다. settings-after의 입력8개도 모두 같다. 임시 reflection Editor helper와 SDK DLL·설정 입력 hash를 함께 보존했다. 초기7dc 빌드의 후처리 비교 스크립트는 SDK의 정상 Scripts/Scripts 경로를 잘못 예상해 멈췄으나 Unity/SDK 자체 exit0였고, 경로 수정 후 같은 산출물103개를 대조했다. 이 후처리 오류를 제품 컴파일 오류로 세지 않는다. 최종39b22df 빌드는 후처리까지 exit0이다.

새 후보는 [AIPlayerSpike-integrated-39b22df.zip](integration/final-39b22df/AIPlayerSpike-integrated-39b22df.zip), 707242bytes, SHA256 `D8D8E891BFD2CAAEDBA34671958DBFFA21C125BC38246D5E8EA346381D3E107A`이다. [package.json](integration/final-39b22df/package.json)에 대응한다. 정상 SDK의 raw C#103개와 ModManifest가 배포 payload이며 compiler DLL hash `77F57D019747BF37C3C70BFC0A9078E3803D39D784B44AB583F60D93869E3598`는 검증용이다. 최신 bridge brief/protocol도 [후보 폴더](integration/final-39b22df/bridge-candidate.json)에 복사했으며 배포/재시작하지 않았다.

통합 독립 검수에서 열린 생활 cursor가 전투 때 숨은 뒤 가방 진입까지 막는 P2를 확인했다. 중간b1에서 전투 진입을 열고 최종39b22df는 실제 생활 view가 전투로 잠시 숨은 경우 가방 진입을 허용하고 shared Stale/Apply까지 일관되게 연결한다. 비전투 열린 생활의 현재 자식 소유는 유지한다. connected192에는 실제 전투 가방 entry→Stale→Apply→cancel과 몸/목표/생활 잔량/요청0 보존을 포함한다. 7dc181 및 중간 SDK 산출물은 최종39b22df의 증거를 대신하지 않는다.

설치/실게임/외부 모델/bridge 재시작은 수행하지 않았다. 환경용 SDK assembly·프로젝트 설정·임시 Editor helper는 제품 커밋에서 제외했다. 독립 검수·공식 반영 및 게임 재개 판단은 HEAD가 소유한다. [현재 통합 실행 계획](integration-runtime-plan.md)을 따르며 741/7dc 중간 패키지를 현재 후보로 설치하지 않는다.
최종39b22df는 생활 메뉴가 실제 표시되었는지/전투로 숨겨졌는지를 Compose의 actor별 ViewSuspended에 저장해 공유 guard가 그 값을 읽는다. 단순 근접 적 유무로 전투 적격 조건을 다시 추정하지 않는다. Compose 때 갱신하고 goal/actor Sync·다른 view로 전환 때 false로 초기화한다. 근접하지만 위협 적격이 아닌 적 앞에서 생활 현재 자식 유지, 실제 숨김 때 가방 진입/취소 도달을 192검사에 포함한다.
