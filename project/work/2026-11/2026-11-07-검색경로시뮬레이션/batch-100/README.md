# 검색 흐름 100회 병렬 시험

기존 단일 시뮬레이션의 세 합성 장면 각각에 대해 A/B를 100번씩 실제 호출한다. 총 300쌍, 600경로다. **600개의 서로 다른 게임 장면이 아니라, 3개의 고정 장면에서 모델 선택과 지연을 반복 측정**한다. 제품·게임·운영 브리지는 바꾸지 않는다.

## 실행과 판정

- [queue.json](queue.json): seed 20261107로 섞은 고정 300쌍. 각 장면 A 선행 50쌍/B 선행 50쌍. 동시에 최대 4쌍이 진행되며 한 쌍 안의 두 경로는 순차다.
- [core.mjs](core.mjs), [contract.json](contract.json), [validation.json](validation.json): 파일럿에서 장면/검색/계획 프롬프트/선택지/원래 수락기를 그대로 추출했다. 파일럿의 실제 위쪽 프롬프트 10개와 Decisions 입력·선택지 12개를 호출 없이 대조했다.
- 위쪽은 구독 CLI와 기존 로그인/사용량 preflight, 현장 선택은 실제 Decisions API다. 모델 설정은 첫 실행의 `models.json`에 고정하며 재개 때 달라졌으면 정지한다. 각 시도의 작업 폴더는 분리한다.
- 사용량은 최대 60초 또는 위쪽 호출 20회마다 실제로 다시 확인한다. 기존 85% 초과/조회 실패/로그인 실패는 중단한다. Decisions의 월 상한 및 분당 호출 상한을 지키고, 최근 시작시각과 429 대기는 `api-clock.json`에 저장한다.
- 단일 transport 실패는 지연을 두고 두 번까지 재시도한다. 오류·재시도·대기시간을 모두 남긴다. 의미상 틀린 답이나 수락 거절을 성공으로 채우려고 다시 부르지 않는다.
- 첫 행동 시간은 각 경로가 실행을 시작한 뒤 모델 판단·검색·API 순서 대기·추가 사용량 확인·재시도까지 포함한다. 전체 큐에서 worker를 기다린 시간은 `queueWaitMs`로 분리한다. 초기 인증/색인은 `sessions/<시작>/setup.json`에 별도로 기록한다.
- s3는 오래된 광맥으로 가지 않고 탐색을 골랐는지와, 상위 `explore` 계획을 받은 뒤 움직였는지를 구분한다. 적절한 탐색이지만 상위 작업이 `gather`이면 원래 자동 `valid:false`를 보존하고 `boundary_unresolved`로 분리한다.

## 확인할 파일

- [status.json](status.json): 10초마다 및 경로 종료마다 갱신하는 전체 진행 수, 장면×경로별 진행, 최근 한도와 갱신 시각. `rate_limit`은 제한 응답 횟수이며 독립 결과 수가 아니다.
- `trials/<장면-번호-경로>/attempt-<번호>/events.jsonl`: 실제 입력/응답, 검색, 재상신, API 대기·재시도. `result.json`에 최종 행동/판정과 시간이 있다. 오류와 중간 중단도 보존한다.
- [summary.md](summary.md), [summary.json](summary.json): 기술 오류, 적절한 수락, 경계 미정, 모델/검색/대기 시간, 토큰, 쌍 안에서의 B−A 시간차. 검색 실패나 미완료를 성공으로 바꿔 집계하지 않는다.

## 명령

```powershell
node .\batch.mjs --mode dry-run
node .\validate.mjs
node .\summarize.mjs
```

위 세 명령은 모델 호출이 없다. 아래 명령은 실제 모델을 호출한다. 단일 실행 잠금, 이미 끝난 성공/실패 trial 건너뛰기, 부분 시도 보존과 새 attempt 번호를 지원한다.

```powershell
node .\batch.mjs --mode run
```
