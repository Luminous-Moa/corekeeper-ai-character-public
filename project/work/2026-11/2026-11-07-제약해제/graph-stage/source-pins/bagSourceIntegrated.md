# 검수 통합 소스의 공식 작업폴더 반영

HEAD의 [소스 수용 판단](source-acceptance.md)과 최종 실행 지시에 따라 `C:/Dev/CoreKeeperModSDK`의 `ai-character`를 `c4a0f5e38016c79567f880bbaea5e56e0eeae347`에서 **`39b22df96dffe869ab0ccd474b18d31462b71d70`**로 `git merge --ff-only` 반영했다. 설치·bridge 배포/재시작·게임·외부 모델 실행은 하지 않았다. 공식 지식그래프는 이 작업에서 쓰지 않았다.

반영 직전에 main HEAD가 c4인 것, staged 변경0, 대상31파일과 기존 수정 파일 사이 충돌0을 재확인했다. c4→39의 차이는 제품 C#/meta와 관련 테스트뿐이며 실행 bridge·설치·환경 설정을 바꾸는 diff는0이다. reset/stash/delete나 기존 작업 재작성 없이 fast-forward만 실행했다.

[반영 전 원문·해시](integration/source-integration-before.json)와 [반영 후 대조](integration/source-integration-after.json)에 status와 파일별 결과를 기록했다.

- 기존 tracked 환경 수정7파일은 raw SHA256이 전부 같다. GameVersionRegistry, ConditionsTable와 meta, manifest/lock, ProjectSettings, ShaderGraphSettings다.
- 기존 미추적450파일도 전부 존재하며 raw SHA256이 같다. `.codex`·`.head`·SDK assemblies와 기존 bridge data/log 등을 포함한다. 반영 전후 `git status --porcelain` 목록도 같다.
- 실행 bridge의 `brief.md`와 `protocol.mjs` 원문 hash2개 및 설치된 mod의96파일 hash는 전부 같다. 현재 설치에 최신 소스가 로드됐다는 뜻으로 사용하지 않는다.
- main의 제품 C#103개와 검수·빌드 후보를 비교해 raw102개 동일, 나머지1개 `AIBrain.Loop.cs`는 CRLF/LF만 다르다. main은 CRLF1482개, 후보는 CRLF1480개+단독LF2개이며 줄끝을 정규화한 텍스트 차이는0이다.
- 전체 반영 대상31파일도 정규화 텍스트 차이0이다. 그중 raw7파일 차이는 줄끝뿐이며 정확한 경로는 after JSON에 남겼다. 제품 논리 차이나 임의 수정으로 축약하지 않고 raw 차이를 구별했다.

현재 공식 소스 HEAD는 검수판39와 같다. [통합 결과](integration-report.md), [현재 후보·실게임 준비](integration-runtime-plan.md), [독립 최종 검수](review-integrated.md)가 현재 진입점이다. 소스·오프라인 연결·Unity·설치 없는 후보 패키지 수용과 실제 모델/게임 소비자·설치 후 성공은 분리한다. 실제 소비자 검증과 설치 보류는 유지한다.
