# 100회 배치 실행 전 독립 검수

2026-11-07. 모델을 추가 호출하지 않고 코드와 저장된 dry-run 자료를 검수했다. 실제 모델 호출은 작업자가 담당한다. 최종 판정: **실행 전 검수 통과, 시작 가능**. 결과 통계와 원문 표본의 후속 검수는 실행 후 별도로 진행한다.

## 확인 완료

- queue.json은 고유 trial ID 600개다. 세 장면 각각 A 100개/B 100개이며 장면별 A 선행 50쌍, B 선행 50쌍이다. seed=20261107로 쌍 순서를 섞고 쌍 안에서는 두 경로를 순차 실행한다.
- contract.json의 core/source 해시를 직접 재계산해 일치를 확인했다. 초기 시험의 fixture·검색·상위 prompt·route brief·Decisions choices·자동 accept 함수 원문이 core에 그대로 포함된다. batch의 Decisions instructions도 초기 원문과 동일하다.
- batch 원문의 worker 함수를 네트워크 없는 mock trial과 실행했다. 완료 trial 14개를 건너뛰고 나머지 586개가 중복 없이 실행됐으며 최대 동시 trial은 4였다. 실제 trial 안의 모델 호출은 await로 순차 실행한다.
- trial/attempt마다 Adapters cwd, retrieved, plan, events를 새로 만든다. 기존 6개 초기 결과 폴더를 덮어쓰지 않는다. completed/failed trial은 재실행하지 않고, interrupted/blocked는 기존 attempt를 남긴 채 새 attempt 번호를 쓴다.
- 원문 요청·응답과 transport attempt를 append log에 보존한다. 최초 시도+최대 2 transport retry, backoff, API slot 대기, 실패 상태를 별도 기록한다. 의미 오류는 자동 재시도하지 않는다.
- 상위 preflight는 60초 또는 상위 attempt 20회마다 force refresh하며 동시에 하나만 검사한다. 기존 구독 85% 기준과 사용량 읽기 실패 중단을 유지한다. API는 공유 60초 호출 제한·idle gap·429 대기를 적용하며 기존 월간 사용량과 저장된 배치 사용량을 함께 확인한다.
- 비밀은 환경에서 읽고 모델 prompt/로그에 의도적으로 추가하지 않는다. 운영 게임·브리지는 시작하지 않으며 운영 설정과 기록을 수정하지 않는다.

## 보완 확인 완료

재개 때 운영 설정이 바뀌어 서로 다른 모델이 섞이는 것을 막는 동결 비교, 그리고 최근 API 시작시각·429 cooldown의 재개 복원을 요청했고 저장된 최종 코드를 확인했다. models.json은 최초 모델 설정을 저장하고 재개 설정과 다르면 모델 호출 전에 중단한다. api-clock.json은 최근 60초 starts, lastStart, coolUntil을 복원하며 slot 발급과 429 수신 때 즉시 저장한다. 두 지적은 해소되었다.

## 해석 조건

completed는 trial 절차 종료이며 자동 valid와 동일하지 않다. 통계는 failed·blocked·재시도·API 대기를 숨기지 않아야 한다. s3의 적절한 탐색 선택과 상위 enum 일치는 따로 집계하고, 역할 경계 미정 결과를 모델 정확도 실패나 무조건 통과로 바꾸지 않는다. 병렬 배치 지연에는 공유 API 호출 제한과 서버·구독 CLI 부하가 섞이므로 초기 단일 실행 지연과 구분한다.
