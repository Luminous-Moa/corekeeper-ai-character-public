> **소스 반영 완료:** 공식 ai-character HEAD는39b22df입니다. [반영 전후 대조](source-integration.md)에서 기존 환경·미추적 파일·설치·bridge 보존을 확인했습니다. 설치·게임·외부 모델 실행은 하지 않았습니다.

# 통합 후보 실게임 재개 준비

대상은 `39b22df96dffe869ab0ccd474b18d31462b71d70` 통합판이다. 이전741 후보는 역사 근거이며 이번 생활 통합 검증판과 혼용하지 않는다. 실게임·설치·외부 모델 실행 보류를 유지한다. 아래는 HEAD의 최종 검토와 재개 승인 뒤 실행할 계획이다.

- SDK 후보는 `integration/final-39b22df/candidate-39b22df/AIPlayerSpike`, ZIP은 `integration/final-39b22df/AIPlayerSpike-integrated-39b22df.zip`이며 [package.json](integration/final-39b22df/package.json)의 정확한 hash와 대응한다. raw C#와 manifest가 정상 배포 payload이고 Unity compiler DLL은 설치 대상이 아니다.
- 설치 대상은 `C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike`이다. 현재 파일을 읽어 hash만 기록하며, 실제 후보 생성 전후 불변은 [manifest](integration/final-39b22df/artifact-manifest.json)로 비교한다.
- 승인 뒤 변경 직전에 기존 설치와 [사전 관측](runtime-preflight.json)에 있는 LocalLow Core Keeper/AIPlayers·Steam 및 Steam userdata `1621690/remote`의 현재 저장 파일을 새 `runtime-backup-before-39b22df-YYYYMMDD-HHMMSS`로 복사하고 hash를 확인한다. 기존 백업/월드/캐릭터를 덮지 않는다. 현재는 백업·설치·복원을 실행하지 않았다.
- 새 `BagLife-39b22df` 월드와 `BagLifeTest-39b22df` 플레이어를 쓴다. 기존 프로세스를 자동 종료하거나 사용자 월드를 테스트 대상으로 삼지 않는다. 승인 때 실제 설치가 사전 관측과 달라지면 먼저 실제 상태를 다시 비교한다.
- bridge의 [최신 통합 후보](integration/final-39b22df/bridge-candidate.json) `brief.md`·`protocol.mjs`는 mode/second/count/slot/keep와 일치해야 한다. 저장소 source와 실행 bridge를 HEAD가 조정하며 현재는 배포/재시작하지 않는다. 비용/호출 상한도 유지한다.

먼저 실제 자연어 부탁에서 상위 계획/원래 order → inventory → 분류 → 물품 → 뒤로 → 다른 물품 → 조작 → 필요한 인자 → 최종 leaf → 정상1회 요청 → 실제 양 슬롯/착용 상태 관측을 잇는다. leaf 전 요청0, 취소 뒤 유효 몸/원래 목표 유지, 8/14를 넘는 목록 전항목 도달, 역할/물품 변경의 사유와 복구를 기록한다. 자기 상자는 실제 inspect 전 내용 비공개, inspect 뒤 분류/물품을 통한 보관·꺼내기, full/한자리·비중첩 상태·수량을 확인한다. 내려놓기/전달은 실제 출발 감소·이번 바닥·지정 수령자 증가/바닥 감소를 연결하고 재요청 없이 관측한다.

다음은 생활의 작업→재료/대상→시설/영역→실행과 정확한 알려진 대상의 직접 실행을 확인한다. 농사 요청12의 여러 묶음이 같은 작업량을 공유하고, 요리의 미지정 재료를 다른 실제 짝으로 좁혀도 상위 수량을 공유하며, 제작은 실제 recipe 출력/비용·가방 수령을 구별한다. store5의 직접 실행과 가방 메뉴 store는 같은 정상 MoveAmount/실제 용량·receipt를 써야 한다. 다른 품목 별도 행동은 원래 목표 진척에 합산하지 않는다.

양쪽 조회를 닫고 여는 동안 실제 진행 몸/생활 작업량·run binding과 held/queued 보존 정책을 비교한다. 가방 최종 leaf 응답이 늦게 도착해도 닫힌 가방 단계의 권한으로 생활 조회를 덮거나 몸을 시작하지 않아야 한다. 재진입/닫기 뒤 이미 만료된 과거 답도 되살아나지 않아야 한다. 작업 완료 관측, 모델의 finish_goal, 상위 order=done은 별개로 확인한다.

각 경로의 탐색/실행 없는 모델 호출 수, leaf와 실제 결과까지 지연, 입력 토큰·비용 및 기존 예산 제한 대기를 기록한다. 오프라인 대체 소비자나 직접 입력을 실제 모델 판단/게임 소비자 성공으로 보고하지 않는다.
