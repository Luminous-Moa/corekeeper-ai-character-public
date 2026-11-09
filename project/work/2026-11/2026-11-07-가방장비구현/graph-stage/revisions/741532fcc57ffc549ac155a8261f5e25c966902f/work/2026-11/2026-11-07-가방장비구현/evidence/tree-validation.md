# 트리 후보 검증과 한계

후보는 `741532fcc57ffc549ac155a8261f5e25c966902f`다. 아래는 실제 source/입구/생성물을 확인한 결과이며 게임/모델까지 완료됐다는 판정은 아니다.

| 검증 | 실제 범위·결과 | 근거 |
|---|---|---|
| fast compile | 역할 복구 수정 포함, 경고0/오류0 | [role compile](tree-menu-role-compile.log), 독립 741 재검수 |
| Core | checkpoint b1facaa의1,204 검사 통과. 741의 Core/해당 시험 소스 동일 확인 | [Core 로그](tree-checkpoint-core.log), [동일성](tree-validation-continuity.json) |
| 브리지 protocol | 118 검사 통과. 741의 브리지/시험 소스 동일 확인 | [protocol](tree-checkpoint-protocol.log), 동일성 기록 |
| 실제 Loop 입구/공개 surface/JSON/예산 | 55/55. 메뉴 상태는 경계 대체; 실제 Apply/CancelPendingChoices/요청 JSON·기존 call/cost gate를 실행 | [Loop 로그](tree-loop-execution.log) |
| 실제 메뉴·인벤토리 엔진 | 생산자348/0. 가짜 World/DB/큐와 수동 슬롯 변화 | [role-results](tree-menu-adapter/role-results.log), [모듈](tree-menu-engine.md) |
| 연결 대표 흐름 | 102/102. 실제 메뉴·surface/JSON·Loop.Apply·inventory registry/leaf factory·InventorySkill을 같은 실행에서 연결 | [connected 로그](tree-connected/results.log), [소스 pin](tree-connected/source-pin.json), [전이 trace](tree-connected/connected-trace.json) |
| Unity + 정상 SDK export | exit0, CompileCheck: ok, error CS0. 설치 없이 별도 후보 경로 생성 | [입력](tree-final-unity-input.json), [로그](tree-final-unity-export.log), [exit](tree-final-unity-exit.json) |
| 생성물/기존 설치 | 제품101개 C# 입력·실행 후 불변, SDK에 복사된101개와 일치. 기존 설치96개 파일 해시/개수 불변 | [artifact manifest](tree-artifact-manifest.json), [ZIP](tree-package.json) |

독립 source 검수에서는 같은741의 Menu394/0, connected 복사·재핀·추출 재실행102/0와 fast compile0/0을 확인했다. 앞선 active preset P2의 정확한 반례와 무관한 가방 선택 유지도 해소 확인했다. 상세 판정과 범위는 독립 검수자의 `review-tree.md` 및 그 검수 파일을 따른다.

## 한 실행으로 이은 대표 흐름

실제 물품 상태가 있는 대체 World에서 인벤토리 진입 → 무기 분류 → slot12 검 선택 → 뒤로 → slot13 다른 변형 검 선택 → 들기 → 점유 hotbar5 → 실행 확인을 거쳤다. 각 단계의 실제 cN/모델 JSON과 선택이 같은 후보 객체로 해석됨을 확인했다. 그 전까지 인벤토리 큐·현재 몸·목표·원래 host-order 기록은 유지됐다.

마지막에 실제 생산 Loop.Apply와 inventory 등록이 InventorySkill을 시작하고, 그 실제 Skill이 대체 큐에 Swap을 한 번 요청했다. 미변경 상태에서는 재요청/거짓 성공이 없었고, 수동으로 소비 결과인 양 슬롯 교환과 실제 EquippedObject 상태를 공급한 뒤에만 receipt가 됐다. 이어 일반 전체 취소와 진행 중 inspect-await의 메뉴 취소도 같은 Apply 입구를 통해 몸·목표·원래 부탁을 유지했다.

이 trace는 로컬 모델 요청 JSON19개 구성/내비게이션17선택을 포함한다. **외부 모델 응답이나 게임 소비자를 실행한 것이 아니다.** World·DB·물리 dispatcher/네트워크·planner 경계는 대체 구현이고 물품 소비 결과는 수동 공급했다. 실제 HostOrder 자료형과 상태는 링크했다. 같은 전체 scheduling loop·게임 인벤토리 consumer·UI·네트워크·모델 판단을 실행한 증거로 확대하지 않는다.

## 호출과 지연

대표 짧은 경로는 hold6선택(내비5+leaf1), 비중첩 drop5, 닫힌 상자 preset 보관9(내비7+inspect1+move1)이다. 두 자리 직접 입력 보관은12선택으로 preset보다3개 늘어난다. back 후 다른 검을 고른 연결 대표는8선택이며 앞선 큐 요청은0, 마지막 Swap만1이다. 이는 단계 수이지 실제 네트워크 지연 측정값이 아니다.

기존 호출 간격·분당 호출 제한·월 비용 제한을 바꾸지 않았다. 실제 기존 rate/cost gate가 메뉴 탐색 중 새 호출을 거부해도 leaf 실행이나 몸/목표 취소로 바뀌지 않는 경계는 Loop55에서 확인했다. 각 메뉴 판단이 모델 호출을 하나 소비하므로 실제 체감 지연·총 토큰/비용은 실게임·모델 재개 후 측정해야 한다.

## 빌드와 배포의 구분

checkpoint b1의 첫 export 시도는 임시 Editor helper가 SDK assembly를 직접 참조한 오류로 exit1이었다. 제품 오류로 기록하지 않으며 [그 로그](tree-checkpoint-unity-export.log)는 보존했다. 기존 HeadlessSdk와 같은 reflection SDK 호출로 helper만 보완한 뒤741을 새 입력 pin으로 다시 컴파일/export했다.

SDK의 실제 배포 payload는101개의 C# 소스와 ModManifest.json이다. Unity의 `Library/ScriptAssemblies/AIPlayerSpike.dll`은 컴파일 증거로 따로 해시를 남겼으며 배포 payload DLL이라고 주장하지 않는다. clone 설정으로 `ModBuilder.BuildMod`의 exportPath만 세션 폴더로 지정했다. 원 SDK 설정 해시도 불변이다.

본 저장소 반영·공식 그래프 적용·bridge 재배포/재시작·설치·게임·외부 모델 시험은 이 상세 실행에서 하지 않았다. [실행 준비](tree-runtime-plan.md)의 실제 게임 흐름 확인이 남는다.
