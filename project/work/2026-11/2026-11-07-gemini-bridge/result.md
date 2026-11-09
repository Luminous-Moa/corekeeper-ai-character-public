# Gemini 브리지 연결

- 요청 범위: 코어키퍼 봇 작업 세션 확인 후 현재 게임의 Gemini 브리지 연결.
- 확인한 상태: 게임 실행 중, 월드 설정은 aiEnabled=true / members=[gemini]. 브리지 프로세스와 8331 포트 리스너가 없고 게임 로그에 연결 실패가 남아 있었다.
- 실행: 기존 C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/bridge.mjs를 기본 설정으로 시작. AI_BRIDGE_MEMBERS=gemini, AI_BRIDGE_PORT=8331. 프로세스 ID 72948. 새 창은 숨김.
- 건강 확인: http://127.0.0.1:8331/v1/health 의 members.gemini.ok=true, login=true, subscriptionOk=true, skip=false. 모델은 기존 기본값 gemini-3.8-flash-low.
- 시작 로그: [stdout](stdout.log), [stderr](stderr.log).
- 실제 왕복 확인 완료: 게임 재시작 후 호스트 인사 요청(report #1)에 Gemini가 13.7초 뒤 HTTP 200 / ok=true로 답했고 게임 로그에 APPLY decision=keep order=answered가 기록됨. 응답: “안녕! 반가워, 오늘은 뭐부터 해볼까?” 이후 구리 갑옷 제작 요청도 브리지로 전달됨. 브리지는 실행 상태로 유지함.


## 사용자 요청에 따른 종료
사용자의 종료 요청으로 실행 경로와 PID를 확인한 뒤 Gemini 브리지 프로세스 72948을 종료했다. 종료 후 8331 포트 리스너가 없는 것을 확인했다.
