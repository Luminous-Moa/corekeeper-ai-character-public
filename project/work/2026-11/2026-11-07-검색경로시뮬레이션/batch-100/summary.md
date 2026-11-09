# 100회씩 병렬 검색 경로 시험 집계

집계 2026-11-07T02:34:42.717Z. 계획 600경로(3장면 × A/B × 100).

| 조건 | 완료 | 오류 | 미완료 | 수락 적절 | 경계 미정 | 기타 판정 | 전체 종료 p50/p90/p95(초) |
|---|---:|---:|---:|---:|---:|---:|---|
| s1-A | 100 | 0 | 0 | 100 | 0 | 0 | 16.58 / 20.49 / 21.45 |
| s1-B | 100 | 0 | 0 | 100 | 0 | 0 | 9.89 / 17.54 / 22.05 |
| s2-A | 100 | 0 | 0 | 100 | 0 | 0 | 17.09 / 21.56 / 23.41 |
| s2-B | 100 | 0 | 0 | 100 | 0 | 0 | 23.32 / 31.38 / 33.24 |
| s3-A | 100 | 0 | 0 | 100 | 0 | 0 | 17.10 / 21.12 / 21.52 |
| s3-B | 100 | 0 | 0 | 1 | 99 | 0 | 10.12 / 18.10 / 24.30 |

전체 토큰·호출은 재개 전 중단 시도와 실패/재시도까지 포함한다.

위쪽 호출 1002, Decisions 호출 1215, 429 0, 실패 모델 시도 0. 위쪽 입력 11963362(캐시 9116928)·출력 93614, Decisions 입력 1294347.

원본: trials/<ID>/attempt-<번호>/events.jsonl 및 result.json. summary.json에 경로별 전체/수락/경계별 분위수, 모델·검색·대기 시간, 재상신·조회 빈도, 개별 판정이 있다.

- Each scene×route repeats the same fixed synthetic scene 100 times; these are not 100 distinct scenes. Failed terminal trials are not replaced to fill successes.
- Quantiles use nearest rank: sorted value at ceil(p*n), with no interpolation.
- Latencies include retries, API scheduling and quota wait after trial start; queue wait before a worker picks the trial is excluded. Initial auth/preflight is excluded from trial latency but included in session elapsed wall time. Fixture indexing occurs during static import before batchT and is excluded from both trial latency and batch elapsed wall time.
- All-ended includes final failure times; appropriate accepted and unresolved boundary times are separate.
- s3 exploratory action under gather is boundary_unresolved, not automatically a retrieval error. Original acceptance flag is preserved.
- Prior blocked/interrupted attempts are retained in attempts and usageAllAttempts, even when the trial later resumes.
- Model calls with missing token reports are counted as attempts; token totals cover only reported usage.
- Calls and searches in group metrics count final attempts; usageAllAttempts includes every saved attempt. Cached input tokens are subsets of input tokens, not additional tokens.
