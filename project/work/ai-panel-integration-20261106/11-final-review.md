# 최종 통합·정식 설치 독립 검수

2026-11-06. ui_independent_review. **판정: 통과.** 승인·검수 후보 `11eb461ae122f404acec94563c086b09e002987c`의 본 통합, 공식 그래프, 정식 설치가 일치한다. 남은 통합 blocker는 없다.

## 확인 결과

- **본 코드:** C:/Dev/CoreKeeperModSDK의 ai-character HEAD가 검수 SHA와 정확히 같고 diff0. 검수 후 자동착용 등 행동 변경이 추가되지 않았다.
- **설치:** 현재 C#95개를 공식 소스와 원시 SHA256으로 재대조하고 manifest 경로/GUID를 .meta와 확인해 차이0. manifest SHA는 `D2D72FEB490B1C6365025A9A06981C543ABD5D14EA719E165DDF48A7E4782370`. BuildAndInstall exit0/done 및 C#오류0 기록과 일치하며 배포 OFL·저자·원본 pin도 확인했다.
- **보존:** 실질 dirty6개와 usage 파일의 현재 해시가 설치전 기록과 같다. status의7번째 ShaderGraphSettings.asset은 raw/HEAD blob이 동일하므로6개 보고와 모순되지 않는다. 기존 untracked 목록 보존과 사용자 AIPlayers52/localsteam251/remote49·registry 불변은09/10 및 담당자 비교 기록 범위로 확인했다.
- **그래프:** 백업738개 실제 해시 차이0. 개발2731노드/21868연결, 제품5973/22771. 독립 query check는 해시·인용·앵커 경고0, product check는 structuralErrors0/stale0/baseline=HEAD=11eb461/warning=null, product-build --check는 byte-identical이었다.
- **새 연결:** 현재 B의 AIChatScreenLayer.OnGUI/AIInfoPanel.OnGUI와 overlay 연결이 맞고, C 검사기는 현재 정식 B 기능 ID도 참조한다. 정적 Unity message 연결을 실제 함수 trace로 주장하지 않는다. 신규 두 개발 기능의 확인 서명도 현재 코드와 같다.

## 발견·해소

08-acceptance.md의 그래프 사본이 승인전판인 것을 독립 해시 대조로 발견했다. HEAD의 승인후 갱신에 따른 차이였으며 작성자가 사본·manifest·생성물을 동기화했다. 최종적으로 실게임 근거 **11개 원본/사본/기록 해시 모두 일치, 차이0**을 재확인했다. 최신08은 `b39c068d0e23410a53fac6967d71abc0758765399256881ff1620c53a3907bd2` /3756bytes. 후속 query/productcheck/byte 통과 기록은 evidence/acceptance-evidence-sync.json이다.

## 증거 범위와 한계

최종판 채팅 본문43~44→24 캡처px와 입력행/힌트 기존 크기 유지가04/08/09 및 그래프boundary에 일치한다. 게임3840×2160과 캡처2560×1440을 구분한다. 빈 장비 및 세 부위 장착 fixture의 server/client/renderer 원천과 UI 표시가 맞는다는 실게임 기록을 대조했다. fixture는 **표시 검증용 데이터이며 제품 자동착용 성공이 아니다.** 원본 복구 SHA 일치 기록도 있다. 최초2762495 관측 당시 메모리까지 소급확정하지 않는다.

OS IME 실제 조합, 다중버튼 동시 유지, 다른 해상도 추가 실게임, 피해량 정량 및 성능 benchmark는 미시험이다. 과거76563bf/902721e 관측은 별도 역사로 유지한다. 자동착용·행동 선택 변경은 별도 작업이다.

최종 단계는 결과 동등성과 근거 대조에 한정했다. 제품 코드 전체/전체 테스트 재실행, 제품·그래프 수정, Unity/게임/CU/설치, 세션 load/save/push는 수행하지 않았다. 허용된 이 보고서만 작성했다.

근거: [07 코드 검수](07-chat-screen-review.md), [04 실게임](04-live-verification.md), [08 수용](08-acceptance.md), [09 통합](09-final-integration.md), [10 설치](10-official-install.md), evidence/final-integration-check.json 및 evidence/official.
