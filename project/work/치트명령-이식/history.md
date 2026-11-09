<!-- head-context branch:1 -->
# 치트명령-이식

이름표: 치트명령-이식
시작: 2026-11-08T06:04:21.199Z
갈래: 코어키퍼-AI-캐릭터/메인@2026-11-08T05:54:51.249Z
갈래-ID: %EC%BD%94%EC%96%B4%ED%82%A4%ED%8D%BC-AI-%EC%BA%90%EB%A6%AD%ED%84%B0/main@2026-11-08T05:54:51.249Z
상태: 끝남

## 지금까지 요약

ChatCommands(MIT)의 placetile·placetilearea·removetile·removetilearea·spawn을 CoreLib 없이 /ai 시험 명령으로 이식하고(c0bd393, 검수 F1~F4 수정 8a9fab4), 실게임에서 통로·광석·작업대·상자·화로·적·장비 전달 조건을 명령 하나당 3~6초로 세팅되는 것을 확인했다. 부족분으로 드러난 /ai hold(모델 호출·자동 행동 전부 정지)와 /ai tp x z|host를 추가(3dbae75, ae09acf)해 실게임에서 hold 중 호출0, 해제 시 보류 요청 1회, tp 6.2초 도착을 확인했고 hold 상태 시험장 재구성은 명령11개 57초·호출0이었다. 세 차례 독립 검수 수용, 공식 ai-character ae09acf에 통합. 실게임 미확인: ae09acf의 F1·F2·tp 거부, hold 중 피격·쓰러짐 계기(부활 무적 해제 후 확인 필요). 시험장 일괄 구성(명령 묶음 또는 기준 세이브)은 실제 시험장을 만들 때 정한다. 근거 폴더: work/2026-11/2026-11-08-cheat-commands/.

## 2026-11-08T06:04:29.491Z

요약: 시험용 지형·물체 명령 이식 착수: ChatCommands(MIT) 5개 명령을 /ai 시험 명령으로, CoreLib 미사용

[구현 합의](../../work/2026-11/2026-11-08-cheat-commands/agreement.md)에 문제·목표·조건·완료 기준을 남겼다. 대상은 placeTile·placeTileArea·removeTile·removeTileArea·spawn이며 testcmds.on 아래 /ai 명령으로 붙인다. CoreLib은 /ai 가로채기 충돌과 GPLv3 때문에 설치·복사하지 않는다. 좌표는 월드 절대 좌표 기본, 상대 표기는 명령 받은 AI 기준(HEAD 기본값). 공식 20b1ace에서 격리 worktree로 구현한다. 실게임 설치는 보류된 시험 데이터 복원·최종 설치와 겹쳐 사용자 확인 뒤 진행한다.

다음: 격리 worktree 구현·compile-check·Unity CompileCheck 후 독립 검수, 사용자 확인 받아 실게임 명령 확인.



## 2026-11-08T06:50:39.406Z

요약: 치트 명령 c0bd393 구현·컴파일·독립검수(수정필요 F1~F4)·실게임 확인: 다섯 세팅 모두 즉시 가능, 복원 차이0

[구현](../../work/2026-11/2026-11-08-cheat-commands/implementation.md): worktree CoreKeeperModSDK-cheat-commands 브랜치 ai-cheat-commands-20261108 커밋 c0bd393, 새 파일 AITestWorldEdit.cs에 placetile·placetilearea·removetile·removetilearea·spawn, compile-check·Unity CompileCheck 통과. [독립 검수](../../work/2026-11/2026-11-08-cheat-commands/review.md): 합의 조건 충족, 수정 필요 — F1 범위 칸수 int 오버플로로 4096 상한 우회, F2 아이템 spawn이 DropNewEntity에 AI 엔티티를 넘겨 AI 가방으로 끌려감, F3 미정의 tileset NRE 무응답, F4 ObjectID 경로 CheckTile 추가 미문서화. [실게임](../../work/2026-11/2026-11-08-cheat-commands/live-validation.md): ai2·Gemini 단독으로 통로 갈림길·막다른 길, 광석·흙벽, 작업대·상자·화로(열림·제작·제련), 적 생성, 장비 전달 조건을 명령 하나당 3~6초로 세팅, AI 인식 확인. F2 실게임 재현. 부족한 점: AI를 조용히 둘 수 없음(/ai stop 뒤에도 자기방어·상위 재계획, 103회 0.25USD), AI 순간이동 명령 없음, 시험장 일괄 구성 수단 없음. 3600칸 범위 명령 66.7ms 프레임 1회. 시험 뒤 새 백업으로 복원·해시 차이0, 설치판 09e 유지, 이전 보류 복원·최종 설치는 손대지 않음.

다음: F1~F4 수정·컴파일·재검수 후 공식 통합 여부 확인. 새 명령 후보(AI 정지 스위치·AI 순간이동·일괄 구성)는 사용자 결정.



## 2026-11-08T06:56:07.225Z

요약: 치트 명령 F1~F4 수정 8a9fab4 컴파일·Unity 통과, 재검수 수용; F2 실게임 재확인과 공식 통합은 대기

[구현](../../work/2026-11/2026-11-08-cheat-commands/implementation.md) 갱신: 8a9fab4에서 좌표 ±1,000,000 제한과 long 칸수(F1), 아이템 spawn을 바닥에만 떨굼(F2), 미정의 tileset 응답(F3), ObjectID 경로 CheckTile 문서화(F4). [재검수](../../work/2026-11/2026-11-08-cheat-commands/review-fix.md) 수용, 남은 낮음 항목: 아이템 생성 실패 미보고(원본 동일), 범위 명령 사용법의 좌표 상한 표기 누락, 미정의 타일셋 숫자 응답 문구가 덜 구체적. F2는 실게임에서 3칸 옆 아이템이 땅에 남는지 재확인 필요. 공식 ai-character에는 아직 병합하지 않았다.

다음: 사용자 결정: 공식 통합 여부, 새 명령(AI 정지 스위치·AI 순간이동·일괄 구성) 포함 여부. 이후 F2 실게임 재확인.



## 2026-11-08T06:58:26.242Z

요약: 치트 명령 8a9fab4 공식 ai-character에 fast-forward 통합, AI 정지 스위치·순간이동 추가 착수

사용자 결정으로 8a9fab4를 공식 ai-character에 fast-forward로 합쳤다(미커밋 환경 파일 7개 보존). 실게임에서 드러난 부족분 중 `/ai <멤버> hold`(모델 호출·자동 행동 전부 정지)와 `/ai <멤버> tp x z|host`(게임 텔레포트 재사용)를 추가하기로 하고 [합의 추가 범위](../../work/2026-11/2026-11-08-cheat-commands/agreement.md)에 기록했다. 시험장 일괄 구성은 실제 시험장을 만들 때 정한다.

다음: hold·tp 구현·컴파일 후 독립 검수, 실게임에서 hold·tp·F2 확인 후 공식 통합.



## 2026-11-08T07:30:20.342Z

요약: hold·tp 3dbae75 독립검수 수용(F1·F2 권고)·실게임 대부분 통과, 피해0 현상 원인 조사와 수정 진행

[검수](../../work/2026-11/2026-11-08-cheat-commands/review-control.md): 모델 호출 지점 두 곳(AIDecisionsApi.Post, AIPlanner.Report)과 tick 밖 경로까지 게이트가 덮음, 기본 상태 불변. 권고 F1 hold 시 진행 중 호스트 말 유실 가능, F2 스위치 없으면 hold off 불가·상태 표시 없음. [실게임](../../work/2026-11/2026-11-08-cheat-commands/live-validation-control.md): hold 다섯 구간 하위·상위 요청 0, 진행 행동 즉시 중단, 해제 때 보류 상위 요청 정확히 1회, tp 6.2초 도착·도착 전 판단 0·새 위치 판단, hold 상태 시험장 재구성 명령11개 57초·호출0(1차 103회). F2 수정은 3칸 통과, 1칸은 근접 자동 줍기로 AI가 주움. 걸을 수 없는 칸 tp는 게임이 코어 스폰으로 보냄. 미확인: AI가 적에게 피해를 받지 않아 hold 중 피격·쓰러짐 계기 확인 불가, 원인 미상. 사용량 +65회 0.110USD(모두 hold 해제 구간). 복원 차이0, 설치 09e 유지.

다음: 피해0 원인 판정(이번 변경 여부)과 F1·F2·tp 불가 칸 거부 수정, 재검수 후 공식 통합.



## 2026-11-08T07:42:14.818Z

요약: 시험 명령 이식·hold·tp 완료: 공식 ai-character ae09acf 통합, 재검수 수용, 피해0은 게임 부활 무적

ae09acf 후속 수정: hold 때 진행 중 호스트 보고 보존(F1), 스위치 없이 hold off·상태 표시(F2), 걸을 수 있는 칸 없는 tp 거부. [재검수](../../work/2026-11/2026-11-08-cheat-commands/review-followup.md) 수용, hold 미사용 기본 상태의 상위 보고 불변 확인. 실게임 피해0 원인은 게임의 부활 무적(ImmuneToDamageAfterRespawn, 첫 이동·공격 2초 뒤 해제)이고 순간이동 무적은 6.1초 고정으로, 우리 변경 결함 아님 — [구현](../../work/2026-11/2026-11-08-cheat-commands/implementation.md) '후속 수정' 절에 시험 주의사항으로 기록. 공식 ai-character를 8a9fab4→ae09acf로 fast-forward(미커밋 환경 파일 7개 보존). 게임 설치판은 09e 그대로.

