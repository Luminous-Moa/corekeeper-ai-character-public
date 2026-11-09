# 중간 flat 통합판 검증

현재 보존 코드는 `1fc89b93edbee31a9001a174581e32568119efc1`, 격리 저장소 `C:/Dev/CoreKeeperModSDK-bag-common`이다. 새 사용자 트리 방향으로 전환되어 이 flat판의 최종 수용과 추가 Unity/패키지 생성은 보류됐다. 본 ai-character/설치본의 배포, 트리 선택 흐름 또는 실게임 완료 판정이 아니다.

| 검증 | 결과 | 근거 |
|---|---|---|
| 실제 게임 assembly 참조 fast C# compile | 1fc89b9, 경고 0, 오류 0, exit 0 | [capacity-compile.log](capacity-compile.log) |
| Core 회귀 | 1fc89b9, 1,201 검사 통과, exit 0 | [capacity-core.log](capacity-core.log) |
| 상위 브리지 protocol | 1fc89b9, 118 검사 통과, exit 0 | [capacity-protocol.log](capacity-protocol.log) |
| 실제 공통 코드 입구 | 1fc89b9, 36/36 통과, exit 0 | [capacity-execution.log](capacity-execution.log) |
| 실제 AIInventory 소스 + 최소 World 어댑터 | 1fc89b9, 43 검사, 실패 0, exit 0 | [capacity-adapter.log](capacity-adapter.log) |
| Unity CompileCheck | **이전 77484ec만** CompileCheck: ok, error CS 0, exit 0. 1fc89b9에는 승계하지 않음 | [입력](final-unity-input.json), [로그](final-unity.log), [exit](final-unity-exit.json) |

`final-*` fast/회귀 로그와 adapter 40개는 앞선 `77484ec`의 검증으로 보존한다. `capacity-*`는 열린 상자의 실제 유효 공간 공유 수정을 반영한 `1fc89b9`의 재실행 결과다. 설치 없는 SDK 패키지는 방향 변경으로 아직 생성하지 않았다.

공통 입구 검증은 실제 AIGoals/Apply/Stop/EndSkill/명시 제어 등 생산 소스의 연결을 실행한다. 장비 receipt→관측→명시 단계 완료→원래 부탁 보존과 slot-only Store의 실제 SetGoal 품목 바인딩을 포함한다. 인벤토리 엔진·네트워크·게임 입력과 월드는 어댑터로 대체하므로 실제 게임 소비자의 실행은 아니다.

최소 World 어댑터는 실제 AIInventory.cs와 Core 규칙을 직접 링크하지만, 버퍼·DB·이동·큐는 최소 대체 구현이고 슬롯 변화는 수동으로 공급한다. 지연된 Swap을 강제로 끝내거나 다시 보내지 않는 것, 실제 교환 뒤 동일 행동이 완료되는 것, 바닥 미소비+우연한 수령자 증가의 거짓 성공 방지, 일치하는 부분 수령, 다른 바닥으로 넘친 물건의 수령 미확인을 포함한다. 이 통과를 게임 소비자·UI·드롭 타이밍·모델 판단의 성공으로 해석하지 않는다.

독립 최종 재검수는 별도 컨텍스트가 같은 고정판을 대조한다. 이전 [feature 검수](review-feature.md)의 실패가 수정됐다는 최종 판정은 그 독립 보고서에 따른다.

## Unity 환경 및 미수행 범위

Unity `6000.0.59f2`의 격리 작업 폴더에서 이전 `77484ec`의 `HeadlessSdk.CompileCheck`만 실행했다. `BuildAndInstall`은 실행하지 않았다. 기존 본 저장소의 게임 SDK assembly 사본 440개는 [입력 해시](unity-sdk-inputs.json)에 있으며 격리 환경의 `Standalone`에 `USE_PUG_OTHER`를 더했다. 최초 Unity import는 toolchain package 2.0.10→2.0.11 및 shader graph 설정도 격리 작업 폴더에서 갱신했다. 이 환경 차이는 코드 커밋에 포함하지 않았다. 본 저장소·게임 설치 폴더·월드·캐릭터는 수정하지 않았다.

설치, 게임 실행/자동 조작, 외부 모델 호출은 수행하지 않았다. [대표 실게임 계획](runtime-plan.md)의 자연어 부탁→상위 계획→모델 선택→실제 입력/소비→가방·착용 변화→다음 판단은 기존 보류 해제 후 확인해야 한다.
