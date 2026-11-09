# 공식 병합 독립 검수 (2026-11-08)

- 대상: worktree `C:\Dev\CoreKeeperModSDK-gap-integrated`, 병합 커밋 `8ba7c1a` = 우리 `1e3ac43` + 공식 ai-character `61aa67b`. 병합 기준점은 `ae09acf`다.
- 방법
  - 코드 읽기: `git diff 61aa67b 8ba7c1a`, `git diff 1e3ac43 8ba7c1a`, `git show --cc 8ba7c1a`
  - 시험: `git archive 8ba7c1a`로 만든 scratchpad 복사본에서만 돌렸다. worktree는 바꾸지 않았다(`git status`는 기존 미커밋 환경 파일뿐).
  - Unity는 실행하지 않았다.
- 결론
  - 차단 0, 중요 1, 경미 7.
  - 양쪽 변경은 글자 단위로 모두 들어갔다. 빠진 것은 합의대로 지운 `FreeStoreCandidates` 하나다.
  - 의미 충돌은 공식의 '목표 없는 대기' 문(IdleHold)과 우리 상자 열림 모드 사이에서 하나 나왔다(G-1).

## 1. 병합이 잃은 것 (양쪽 diff 대조)

파일마다 "그쪽이 원래 바꾼 줄(ae09acf→부모)"과 "병합 결과에 들어간 줄(다른 부모→8ba7c1a)"을 줄 단위로 비교했다.

| 파일 | 공식 의도 | 우리 의도 | 차이 |
|---|---|---|---|
| `AIBase.cs` | 모두 반영 | `FreeStoreCandidates`(R5 공용 상자 진입 조건 포함)만 빠짐 | 합의된 삭제다. 도우미 `RouteTriesPerDecision`·`InViewChest`·`NearestFirst`는 보관 목표·재료·음식·씨앗 경로가 계속 쓴다(AIBase.cs:559, 586-589, 665, 689) |
| `AIBrain.Loop.cs` | 모두 반영 | 모두 반영 | 병합에서 `AIExpedition.NoteChoices(chestOnly)` 한 줄 추가(1340) |
| `AIExpedition.cs` | 모두 반영 | (우리 변경 없음) | 병합에서 `NoteChoices` 추가(157-158) |
| `AIInventoryMenu.cs`, `AIInventory.cs`, `AIBrain.Candidates.cs`, `brief.md`, `maintenance-tests/BrainStubs.cs` | 모두 반영 | 모두 반영 | 없음 |
| `AIDecisions.cs`, `AIMaintenance.cs` | 한쪽만 바꿈 | 한쪽만 바꿈 | 우리 추가 줄에 `IsReal`·`GoalKind.Free`·호출 상한 키·`LastChoices`를 쓰는 곳이 없다. `IsReal` 의미 변화(Free도 참)의 영향은 시험 고정값뿐이다(경미 L-4) |
| `inventory-menu-tests` csproj, `restriction-tests/prepare.mjs` | 반영 | 반영 | 하네스 조정. prepare.mjs의 새 끝 표식(follow 블록 뒤 첫 `yield return 0;`)은 현재 `FollowSteps`(AIBrain.Candidates.cs:613-622)에서 올바른 범위다 |

`fixes.md` '공식 병합' 절의 주장을 코드로 확인했다.
- 글자 충돌은 AIBase 한 곳뿐이다.
- 상자 열림 모드는 `Compose`를 거치지 않으므로 `NoteChoices`가 필요하다.
- 가방 메뉴는 양쪽이 보존됐다.
- hold 아래에서 판단 조립이 멈춘다.

다만 "토큰 절감 ① 조정 완료"는 선택지 참조만 맞춘 것이다. 현장 관측 자체는 이전 조립의 값으로 남는다(경미 L-2).

## 2. 발견

### 중요

**G-1. 목표가 없을 때 상자 열림 모드가 한 번 조작한 뒤 멈추고, 열린 채로 남는다**
- 위치
  - `AIBrain.Loop.cs:298-300`: `IdleHold(..., menuOpen: AIInventoryMenu.Active || AILife.Cursor.Open || AIExpedition.Active)`
  - `AIInventoryMenu.cs:503-508`: `NoteLeafSelected`는 최종 조작을 고르면 `S.path.Clear()`
  - `AIBrain.Loop.cs:1333-1342`
- 원인
  - 공식 항목6과 B1의 대기 문은 메뉴가 열려 있으면 목표가 없어도 호출을 통과시킨다.
  - 상자 열림 모드의 실제 상태는 `AIInventory.S.openChest`다. 메뉴 경로(`chestroot`)는 판단 조립 때마다 다시 만드는 표시일 뿐이다.
  - 그래서 상자에서 꺼내기·넣기·빠른 쌓기·정렬을 실행하면 메뉴 경로가 비고, `AIInventoryMenu.Active`가 거짓이 된다.
- 실패 시나리오
  1. 목표 없음. '새 적 보임'·'기지 둘레 부서짐' 같은 이슈로 호출이 간다.
  2. 판단 모델이 가방 메뉴 → 상자 → 접근을 고른다. 메뉴가 열려 있으니 호출이 이어진다.
  3. 열어보기(inspect)가 끝나 `Opened`가 기록된다. 다음 판단은 `chestroot` 상자 열림 모드다.
  4. 물건 하나를 꺼내는 최종 조작을 고른다. 경로가 비고 이동 기술이 끝난다.
  5. `IdleHold`가 참이 되어 호출이 끊긴다. 상자는 계속 '열림'이고 모드도 유지된다.
  6. 다음 이슈가 오면 그 판단은 상자 안 일과 닫기만 받는다. 기지 둘레 부서짐에도 닫기 → 한 번 더 호출을 거쳐야 대응 선택지가 나온다.
- 같은 일이 생기는 다른 경우: 목표가 있는 상태에서 상자 모드 조작으로 목표가 끝났는데, 상위가 hold나 무목표를 답한 경우.
- 근거
  - 합의 '상자 열림 모드'는 닫기를 고르면 원래 선택면으로 돌아간다고 정했다. 이는 모드가 닫기까지 이어지는 것을 전제한다.
  - 공식 B1은 '열린 메뉴는 통과'를 의도했다.
  - 병합 전 우리 쪽에서는 목표가 없으면 호출이 전혀 없었다. 이 경로는 병합으로 새로 생겼다.
- 제안: 다음 둘 중 하나.
  - `IdleHold`의 menuOpen에 열린 상자(`AIInventory.ValidOpenChest`가 Null이 아님, 또는 같은 뜻의 읽기 전용 플래그)를 더한다.
  - 상자 모드에서는 최종 조작 뒤에도 `chestroot` 경로를 남긴다.
  - 어느 쪽이든 판정은 한 곳에 둔다.

### 경미

**L-1. 목표가 없을 때 판단 입력의 '자기 방어 중' 문구가 상자 모드·메뉴 선택면과 어긋남**
- 위치: `AIBrain.Loop.cs:1521`. 문구는 공식 것이다.
- 내용
  - 목표가 없으면 항상 "자기 방어 중: … 싸우기·피하기·물러나기·먹기·기다리기 가운데 고른다"가 붙는다.
  - 병합 뒤에는 목표가 없어도 메뉴가 열려 있거나 상자 모드이면 호출이 간다(G-1 경로).
  - 그때 실제 선택지는 상자 안 일과 닫기뿐이라 입력 문장과 맞지 않는다. `_sdWhy`도 지난 에피소드 값일 수 있다.
- 영향: 판단 모델은 실제 선택지 값만 고를 수 있으므로 오동작은 아니고 문구 혼선이다.

**L-2. 상자 열림 모드 판단의 '마지막 현장 관측'은 이전 일반 조립의 값**
- 위치: `AIBrain.Loop.cs:1340`, `AIExpedition.cs:167-169`, 410-433
- 내용
  - 상자 모드는 `ComposeChoices`(`Opportunities`·`CatalogIds` 갱신, `Pacer.Sync`)를 건너뛰고 `LastChoices`만 바꾼다.
  - 그래서 `FieldObservation`이 이전 조립의 자원 목록을 "지금 선택지에 없음(가방·생활 메뉴 중 등)"으로 싣는다.
- 영향
  - 문구에 "이후 상태는 재확인"이 붙어 있어 사실과 정면으로 어긋나지는 않는다.
  - 다만 상자 모드가 길어지면 오래된 좌표가 매 판단에 반복된다(토큰 절감 취지와 반대 방향).

**L-3. 상자 모드에서 메뉴 경로 문구에 '상자 열림'이 없음**
- 위치: `AIInventoryMenu.cs:510-525`(`PathText`에 `chestroot` 처리가 없음). `GoalRelation`(554)과 `PromptLine`(563)이 이 문구를 쓴다.
- 내용
  - 상자 모드 뿌리에서 경로는 "인벤토리"로, 상자 꺼내기 쪽 분류에서는 "인벤토리 → 무기"처럼 나온다.
  - 공식 4/4의 완료 거절 안내도 "메뉴 인벤토리"라고 쓴다.
  - 상자 모드 안내는 진입 직후 notice 한 번뿐이다.

**L-4. 시험 고정값이 이제 만들어질 수 없는 `GoalKind.Free`를 씀**
- 위치
  - `Tools/life-tests/BedCases.cs:49`
  - `Tools/life-tests/FarmCases.cs:142, 148`
  - `Tools/life-craft-tests/Program.cs:70, 82, 85, 98, 106, 114, 121, 124`
- 내용
  - 자유 시간 제거 뒤 Free는 어디서도 설정되지 않는다. `IsReal`도 이제 Free에서 참이다.
  - 이 시험들은 지금 통과하지만, 실제 상태(무목표 `None` 또는 실제 목표)를 대표하지 않는다.

**L-5. 자유 시간 제거로 죽은 매개변수**
- 위치: `AIBase.cs:479-482`, 583 (`Candidates(..., AIGoal asGoal = null)`)
- 내용
  - `asGoal`을 넘기던 유일한 호출처가 `FreeStoreCandidates`였다. 지금은 아무도 넘기지 않는다.
  - 583줄 `asGoal != null ? 빈 목록 : S.missing` 분기도 죽은 분기다.
- 동작 영향은 없다.

**L-6. (병합 무관, 우리 1e3ac43) `AreaRepWalks`가 캐릭터별 상태가 아님**
- 위치: `AIInventory.cs:481`(static HashSet), `AIBrain.Loop.cs:1331`(조립 시작 때 Clear)
- 내용
  - 판단 준비는 캐릭터마다 여러 프레임에 나뉘어 돈다.
  - 그래서 AI 둘이면 한쪽 조립이 다른 쪽의 구역 대표 목록을 지우거나 섞는다.
  - 그 결과 G4 필터(`AIBrain.Candidates.cs` `IsAreaRepresentativeWalk` + 경로 없음 제거)가 어긋날 수 있다.
- 같은 종류: `AIChestMode.Reset()`(AIChestMode.cs:44)는 호출하는 곳이 없다. `LastEnded`도 읽는 곳이 없다(로그와 notice로만 전달).

**L-7. (문구, 우리 쪽) brief 42줄 "seeds_in_chests 내 상자의 씨앗"**
- 모든 상자 공용 결정(L12 계열)의 남은 표현이다.

## 3. 항목별 확인

### 자유 시간 제거와 우리 변경
- 남은 죽은 경로는 L-5 하나다. 우리 코드·문구에 남은 '자유 시간' 전제는 없다(grep).
  - 남은 언급은 공식 쪽 주석, `AICandidateAssembler`의 free=false 호환 입력, 시험 고정값(L-4)이다.
- 보관 의도는 보관 목표의 평면 후보(구역 대표)와 생활 트리 보관으로 남는다. 목표가 없으면 기다린다는 사용자 결정과 맞는다.
- 목표 없는 대기와 상자 모드의 상호작용은 G-1이다.

### 토큰 절감 형식과 우리 선택지·보고
- ① 직전 선택지 참조
  - 일반 조립: `Compose`의 결과 목록을 `InventoryChoiceSurface`가 같은 객체로 고친다. 그래서 `LastChoices`는 최종 선택면과 같다.
  - 상자 모드: `NoteChoices`로 맞췄다. 남은 것은 관측값이 이전 값이라는 점이다(L-2).
- ③ 짧은 기록: `AIFieldText.Choice`·`Result`(240자)는 원정 기록에만 쓴다. 우리 결과 문장 전문은 '최근 결과'(④, 최근 20건 전문)에 남는다.
- ② gather 묶음: 우리 선택지와 무관하다.
- 상위 보고 `chests`·`chest_ids`·구역 요약(`AIBase.ReportFields`, `AIInventory.ReportJson`, `AIPlaces`)은 공식이 건드리지 않았다. brief 40·45줄과도 일치한다.

### 이동 중 호출 계기·호출 간격
- `travelMoving` 거르기(`AIExpedition.cs:170-176`)는 `Gather` 종류만 대상이다. 정비 걷기, 상자 걷기, 구역 대표, 대장간 회수 같은 `dig` 걷기는 대상이 아니다.
- 화면 밖으로 가는 `dig` 걷기는 공식 1/4의 '현장 이탈' 판정(`Choosing`)을 받는다. 관련 자원이 미검토로 남아 있으면 상위 보고가 한 번 간다. 공식 설계 그대로다.
- 상자 모드의 탐색 단계는 `_treeStepNext`(1089)로 간격 없이 이어진다. 피격 판정은 `hpDropTrigger`로, 공식이 바꾸지 않았다.
- TravelSkill과 상자 모드가 겹치는 경우는 다음 하나뿐이고 일시적이다.
  - 대기 중이던 전환(held switch)이 열어보기 직후 이동을 시작하면, 2칸 안에 있는 동안 공간 질문의 답이 상자 선택면을 받을 수 있다.
  - 2칸을 벗어나면 풀린다.

### /ai hold
- 우리 변경이 더한 모델 호출 경로는 `AIMaintenance.Abandon` → `AIPlanner.RequestReplan` → `Report` 하나다.
  - `Report`는 hold에서 막힌다(AIPlanner.cs:415).
  - `Abandon` 자체도 기술 실행 안에서만 돈다. hold 중에는 `HeldTick`이 기술을 멈춘다.
- 상자 모드 판정·닫기·피격 종료(`AIChestMode.Active`)는 `BuildCandidateSteps` 안에서만 돈다.
  - `BuildCandidateSteps`의 호출처는 둘이다: `BuildCandidates`, `StepPreparation`. 둘 다 `AIBrain.Tick` 안이다.
  - `AIBrain.Tick`은 맨 앞(Loop.cs:71)의 `BlocksBrain`에서 멈춘다.
- B3 프리셋 입력: `HeldTick`도 같은 `Emit`을 거치므로 hold 중에 프리셋이 0으로 돌아가지 않는다.
- 새로 생긴 hold 우회 호출은 없다.

## 4. 시험 수치 (scratchpad 복사본, 정규 생성 스크립트)

- 실행 스크립트: `C:\Users\<USER>\AppData\Local\Temp\claude\c237366b8a8ea80b\44263ae2-99f5-4724-9951-89bf0b261264\scratchpad\runall.ps1`
- 로그: 같은 폴더 `logs\`
- 생성 스크립트가 있는 하네스는 그 스크립트로 돌렸다(`.generated` 재추출 포함).
  - run.ps1: execution, life, maintenance, inventory-menu, gather, gather/native, candidate-progress
  - README의 Extract*.ps1: life-inventory, life-craft
  - prepare.mjs: restriction
  - run.mjs: action-input
- 재추출한 `BaseSourceSlices.cs`와 두 `LifeBindingSource.cs`는 커밋본과 같다(오래된 추출본 아님).

| 시험 | 결과 | fixes.md 주장 |
|---|---|---|
| execution (run.ps1) | 217/217 | 217/217 일치 |
| life (run.ps1) | 129/0 | 일치 |
| maintenance (run.ps1) | 168/0 (상자 모드 10개 포함) | 일치 |
| inventory-menu (run.ps1) | 6/0 | 일치 |
| gather (run.ps1) | PASS 61 | 일치 |
| gather/native (run.ps1) | PASS 14 | — |
| life-inventory (Extract 2개 + run) | 253/0 | 일치 |
| life-craft (Extract + run) | 49/0 | 일치 |
| life-cook | 70/0 | 일치 |
| restriction (prepare.mjs + run) | ALL PASS 54. G3 MEASURE 125 / 245 / 255 | 일치 |
| travel | 105/105 | 일치 |
| path-preparation | 25 OK | 일치 |
| action-input (run.mjs) | PASS 43 | 일치 |
| info | PASS 7, FAIL 0 | 통과 일치 |
| gates.sh (Unity 없이) | compile-check 오류 0, core-tests 1581, exp-sim OK, recall-loop 41, bridge-wiring 23, protocol 190, fake-bridge·logshape·movecheck OK, memgraph SKIPPED(KG 데이터 없음), 실패 0 | 일치 |
| travel-tests/navigator | 컴파일 실패(`AIGoals`·`AITestControl`·`MarkAreaQuestion` 없음) | 병합 무관. `61aa67b`·`1e3ac43` 단독에서도 같은 실패 |
| travel-tests/candidate-progress (run.ps1) | 8/11 | 병합 무관. `61aa67b` 단독도 8/11이고, 공식 기록상 20b1ace부터 있던 실패 |
| Unity 컴파일 | 실행하지 않음(지시) | — |

## 5. 정리
- 차단: 없음.
- 중요: G-1. 목표가 없을 때 대기 문(IdleHold)이 열린 상자 모드를 '메뉴 열림'으로 보지 않는다. 그래서 상자 모드가 한 번 조작한 뒤 멈추고, 다음 이슈 판단이 상자 선택면만 받는다.
- 경미: L-1~L-7.
- 시험: 생성 스크립트 경로로 fixes.md의 병합 뒤 수치가 모두 재현됐다. 실패 두 건(navigator 컴파일, candidate-progress 8/11)은 두 부모에서도 같아 병합 탓이 아니다.
