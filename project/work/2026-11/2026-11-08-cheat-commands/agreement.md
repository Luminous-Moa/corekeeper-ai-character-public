# 시험용 지형·물체 명령 이식

## 문제

실게임 검증(채집·이동·귀환, 생활, 장비, 통로 탐험)에 필요한 시험장을 만들 수단이 없다. 기존 `/ai` 시험 명령은 AI 가방·체력·배고픔·상태효과, 호스트 텔레포트, 적 생성, 제작대·상자 제거까지만 된다. 지형(벽·바닥·광석 벽)을 놓거나 지우는 명령과 제작대·상자·화로 같은 물체를 생성하는 명령이 없다.

기존 치트 모드 ChatCommands를 그대로 설치하면 의존 모드 CoreLib이 `ChatWindow.Deactivate` 접두 패치에서 `/`로 시작하는 입력을 모두 자기 명령 시스템으로 보내고 원래 전송을 취소한다. 우리 모드는 `NetworkCommSystem.SendChatMessage` 접두 패치에서 `/ai`를 받으므로 `/ai` 명령 전체가 막힐 것으로 소스상 판단했다. 조사: [치트 모드 조사](../2026-11-08-cheat-mod-survey.md).

## 목표

ChatCommands(limoka/CoreKeeperMods, MIT)의 지형·물체 명령 코드를 우리 `/ai` 시험 명령으로 옮겨, 시험용 스위치 `testcmds.on`이 켜진 상태에서 채팅 명령으로 시험장 지형과 물체를 만들 수 있게 한다.

## 조건

- 대상 명령: placeTile, placeTileArea, removeTile, removeTileArea, spawn. 아이템 지급·상태 설정·텔레포트·적 생성은 기존 `/ai` 명령을 쓰므로 가져오지 않는다.
- CoreLib(GPLv3)은 설치하지 않고 코드도 복사하지 않는다. CoreLib 도우미(ParsePos, ParseItemName, GetPlayerEntity, API.Server.World, CommandOutput 등)는 우리 모드의 처리로 바꾼다.
- 옮긴 코드에는 MIT 원저작권 표시(Copyright (c) 2021 limoka)를 남기고, 라이선스 전문을 배포판에 포함될 위치에 둔다.
- 기존 시험 명령과 같이 `testcmds.on`이 없으면 동작하지 않는다. 기존 명령·제품 행동은 바꾸지 않는다.
- 좌표는 기존 `/ai hostto x z`, `/ai rmstation x z`처럼 월드 타일 절대 좌표를 기본으로 하고, 원본의 상대 좌표 표기는 명령을 받은 AI 위치 기준으로 둔다(HEAD 기본값).
- 공식 소스 ai-character 20b1ace에서 격리 worktree로 구현한다. 공식 작업트리의 사용자 미커밋 환경 파일과 다른 진행 중 worktree(move-deliver 등)는 건드리지 않는다.

## 추가 범위 (2026-11-08, 실게임 확인 뒤 사용자 결정)

8a9fab4를 공식 ai-character에 fast-forward로 합쳤다. 실게임에서 드러난 부족분 중 두 명령을 추가한다. 시험장 일괄 구성은 실제 시험장을 만들 때 정한다.

- AI 정지 스위치 `/ai <멤버> hold`: AI를 월드에 둔 채 하위 판단·상위 계획·자기방어·반사 행동을 포함한 모델 호출과 자동 행동을 모두 멈춘다. 다시 입력하거나 `hold off`로 푼다. 실행 중 행동은 멈춘다. 게임 재시작 뒤까지 유지하지 않는다(HEAD 기본값).
- AI 순간이동 `/ai <멤버> tp x z`: 기존 `arena go`가 쓰는 게임 텔레포트 방식으로 옮긴다. 좌표 규칙은 다른 시험 명령과 같고, `tp host`는 호스트 옆으로 옮긴다(HEAD 기본값). 옮긴 뒤 이전 위치 기준 실행이 이어지지 않게 한다.
- 둘 다 testcmds.on 아래에서만 동작한다. 실게임에서 F2 수정(아이템이 바닥에 남는지)과 함께 확인한다.

## 완료 기준

모드 compile-check와 Unity CompileCheck 통과, 독립 검수 수용 뒤, 실게임 시험 월드에서 각 명령이 실제 타일·물체를 만들고 지우는 것을 확인한다. 실게임 설치는 보류된 시험 데이터 복원·최종 설치 상태와 겹치므로 사용자 확인 뒤 진행한다. 시험장 기준 세이브를 만드는 일은 이번 범위가 아니다(제안 단계).
