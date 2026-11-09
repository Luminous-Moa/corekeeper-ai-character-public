# AI 동반 플레이의 화면 전체 끊김 — 읽기 진단

2026-11-06. 조사 대상은 높은 표시 FPS에도 화면 전체가 지속적으로 드득거리는 체감이다. 코드·게임 조작·설치·설정 변경 없이 기존 canon, 소스, 설치 파일, 실행 로그를 읽었다. 사용자 증상 당시의 프레임별 추적을 새로 얻지는 않았다.

## 판단

**우선 확인할 범위는 AI 모드가 게임 Update 안에서 하는 동기 작업이다.** AI 판단의 관측·후보 생성·길찾기가 화면 갱신과 같은 호출 흐름에서 실행되며, 기존 실제 넷 플레이에서는 한 판단 준비에 100~320ms가 걸린 기록이 있다. 최신 별도 시험에서도 117~119FPS와 150~208ms 프레임 지연이 함께 관측된다. 평균 FPS만으로 부드러움을 판정할 수 없다.

다만 사용자의 “항상 드득드득” 체감 전체를 먼 길찾기 한 가지로 확정할 수 없다. 반복되는 10~30ms 수준의 판단 준비, 매 프레임 실행되는 AI 처리와 여러 AI의 작업 집중, 그리고 더 큰 간헐적 지연을 나눠 측정해야 한다. GC·디스크 쓰기·게임 엔진/그래픽·현재 PC의 다른 개발 작업은 후보이지만 이 조사에서 원인으로 입증하지 않았다.

## 실행 흐름: API 응답을 기다리는 시간과 로컬 준비 시간을 구분

공식 저장소 HEAD는 `902721ec505df42d74d0b2d245ed34a049faadad`였다. 다음 경로에는 async/작업 스레드 전환이 없다.

1. [AIPlayerSpikeMod.Update](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs:221)는 모든 AI를 foreach로 순차 처리한다. 각 Tick 전체를 Stopwatch로 재서 `mod per AI avg/max`에 넣는다(233~247행).
2. Walking 단계는 [Step3_Drive](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs:941)를 호출하고, 958행 `AIBrain.Tick`, 960행 `AIBody.Tick`을 동기 실행한다.
3. [AIBrain.Tick](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:20)에서 필요 시 `Send`를 호출한다(155·231행). [Send](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:633)는 관측(645~647), BuildCandidates(654), 입력·JSON 작성(702~703)을 모두 실행한 뒤 704행에서 `prepMs`를 읽는다.
4. API 전송은 그 뒤 752행이다. [AIDecisionsApi.Post](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIDecisionsApi.cs:34)는 `SendWebRequest()` 후 반환하고, 다음 Tick에서 `_req.isDone`을 확인한다(AIBrain.Loop.cs:118). **로그 prep 수치에는 API 응답을 기다린 시간이 포함되지 않는다.**
5. [PlanC](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBrain.Loop.cs:1007)는 캐시 미스 때 `AITunnel.Plan`을 동기 호출한다(1036). [AITunnel.Plan](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AITunnel.cs:229) → [AITunnelSearch.Find](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/Core/AITunnelSearch.cs:26)는 최대 120,000 탐색 가드가 있는 while 탐색이다. CPU 작업을 여러 프레임에 나누는 길찾기 경로가 아니다.

탐험 경계 찾기 `_fJob.Step(400)`는 여러 프레임에 분산하지만, 이것도 AIBrain.Tick 안에서 직접 실행한다(29~35행). 코드의 “background”라는 주석만 보고 워커 스레드라고 읽으면 안 된다. 개별 목적지 PlanC와도 별개다.

## 실제 근거 1: 이전 넷 플레이의 반복 지연

[넷이 함께 플레이](2026-11-04-넷이-함께-플레이.md) §1.6·§2.1·§3:

- 1차 넷 플레이 FPS 중앙값 109.7, 프레임 p99 중앙값 25ms, 최장 프레임 308ms.
- Claude 판단 준비 p50 19.9ms / p95 175.5ms / p99 222.4ms / 최대 303ms. 다른 AI도 준비 p50 11.9~14.0ms, p95 31~49.8ms였다.
- 주석 목표에서 8~10개 길을 한 번에 계획하며 200~300ms를 썼다. 캐시를 통하지 않던 상자·기지·제작대 경로를 PlanC로 모으고 평화 시 캐시 5초를 적용했지만, 2차에도 27개 30초 묶음 중 4개에서 AI 틱 최대 180ms 초과가 남았다.

[막힘·멈춤 정리](2026-11-04-막힘-멈춤-정리.md) 288~291행은 후속 재측정으로 원인을 정정한다.

- ‘캐기/싸움 때 추정 피해가 바뀌어 캐시를 계속 비움’ 가설은 재측정에서 맞지 않았다. 이 가설을 원인으로 다시 제시하면 안 된다.
- 같은 주석 목표에서 준비 82~195ms, 그중 계획 50~179ms(2~7개). 먼 기억 광석·용광로까지 길 하나에 20~30ms가 들었다.
- 2차 준비 1,673번 중 150ms 초과 15번, 최대 320.7ms. 180ms 초과 PERF 창 5개는 모두 그 목표를 준 뒤였다.
- 이는 [U.prep-long-routes](../지식그래프/기획/미결.md:267)에 미결로 남았다.

현행 PlanC에는 평화 5초·전투 2초와 거리 조건, ‘길 없음’ 10초 캐시가 그대로 있다. 캐시 완화는 이미 적용됐지만, 캐시 미스 시 큰 계산을 한 프레임에 동기 실행하는 구조는 남았다. 이후 리팩토링은 동작 변화 없는 경로 추출로 기록돼 있다. 오래된 수치를 현재 사용자의 정확한 재현 수치로 취급하지는 않는다.

## 실제 근거 2: 오늘의 별도 1인 시험

읽는 당시 최신 Player.log는 사용자의 평소 월드가 아닌 다른 작업의 `choice1106` 시험이었다. [선택지 수정 실게임 기록](2026-11-06-선택지개선/03-live-validation.md)에 따르면 `b25099f` 후보, 게임 시작 19:58:58, 종료 20:07:49, 원복 20:08:16이다. 상위 브리지는 꺼져 있고 하위 판단만 실행됐다. 원본 로그를 [choice1106-Player.log](2026-11-06-stutter-diagnosis/choice1106-Player.log)에 보존했다.

| 로그 행 / 시각 | AI | FPS | 프레임 avg / p95 / p99 / max(ms) | AI Tick avg / max(ms) |
|---|---:|---:|---|---|
| 313 / t167.6 | 0 | 119.5 | 8.40 / 8.33 / 8.34 / 116.7 | 없음 |
| 391 / t197.6 | Gemini 1 | 118.7 | 8.43 / 8.33 / 8.34 / 208.3 | 0.21 / 120.8 |
| 968 / t257.6 | Gemini 1 | 117.3 | 8.53 / 8.33 / 16.67 / 150.0 | 0.52 / 87.4 |
| 1326 / t287.6 | Gemini 1 | 117.1 | 8.54 / 8.33 / 8.34 / 166.7 | 0.51 / 164.2 |
| 1889 / t377.6 | Gemini 1 | 118.9 | 8.40 / 8.33 / 8.34 / 25.0 | 0.34 / 21.3 |

`AIPerf.Frame`은 Unity `Time.unscaledDeltaTime`을 모아 30초 창을 만든다([AIAgents.cs:183](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIAgents.cs:183)). AI Tick 최대와 프레임 최대가 같은 창에 있다는 뜻이며 **같은 프레임인지 연결하는 ID는 없다**. 또한 끝날 때 퇴장한 AI의 창 통계는 출력에서 빠지므로 `AIs 0`인 마지막 창을 온전한 off 기준으로 쓰면 안 된다.

이 시험의 첫 준비 #1은 106.9ms(관측 2.5, 후보 100.5, 그중 길계획 15.9)였다. 그 뒤 SEND 준비 최대는 24.5ms, 즉 최신 164.2ms Tick 스파이크는 SEND에 기록된 길 준비 하나로 설명되지 않는다. 준비 조기 반환은 SEND가 남지 않을 수도 있고, Tick에는 몸 동작·메모리·저장·API 답 적용도 포함된다.

반복 미세 지연의 예로 COMBAT #6(1277행)은 약 10초 동안 115FPS, 25ms 초과 4번(0.40회/초), 50ms 초과 2번, 최대 167ms다. 준비는 p50 9.4 / p95 17.1 / 최대 17.7ms였다. 반대로 #8(2019행)은 119FPS, 25ms 초과 2번/16.7초, 50ms 초과 0번이었다. 따라서 이 시험이 사용자의 ‘항상’ 체감을 재현했다고 주장하지 않는다.

설치 대응: 조사 순간 설치 폴더 `…/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike/Scripts/Scripts`의 AIPlayerSpikeMod.cs, AIBrain.Loop.cs, AIBrain.Candidates.cs, AITunnel.cs, AIAgents.cs, AIDecisionsApi.cs 6개는 공식 902721e 소스와 SHA256가 일치했다. 이는 선택지 시험 **원복 뒤 디스크 상태**이며, 방금 로그의 로드 버전을 902721e라고 입증하는 것은 아니다. 20:08 이후 다른 정보패널 대화의 임시 빌드·설치 예정도 전달받았다. 조사 시작 시 CoreKeeper 프로세스는 없었다.

## 지속적인 미세 끊김과 관련해 남은 범위

- AI는 서버 20틱 때만 처리되는 것이 아니라 게임 매 프레임 순차 Tick된다. 따라서 120FPS 예산 약 8.33ms에 여러 AI의 작업이 더해진다. 1인 시험의 전체 Tick 평균 0.15~0.52ms가 작아도 일부 프레임의 10~30ms 작업은 평균에서 희석된다. 기존 넷 플레이의 준비 중간값 12~20ms도 한 프레임 예산보다 크다.
- [AIBody.cs:273](C:/Dev/CoreKeeperModSDK/Assets/AIPlayerSpike/Scripts/AIBody.cs:273)는 메모리 관측·플래너를 동기 호출한다. `AIMemory.Observe`는 보통 1초 주기이며 판단 전에는 force 호출된다(AIMemory.cs:242~293). 경험은 1초 싸움 관측·2초 구역/아이템 관측·30초 저장(AIExperience.cs:406~408), 캐릭터는 60초 저장이다. 분산 여부와 실시간 비용은 아직 따로 계측되지 않았다.
- AIChargeWatch는 매 프레임이나 `Time.frameCount` 가드로 전 AI 공통 한 번이다(62~67행). AI 수만큼 이 스캔이 중복된다고 단정하면 안 된다.
- 동기 파일 쓰기와 List/Dictionary/String 생성 경로는 있지만 GC 수집·할당량·I/O 지연을 측정한 자료는 이번 근거에 없다. GC가 원인이라고 결론내리지 않는다.
- AI 0명 기준 창에도 116.7ms 최대 프레임이 있었다. 모든 끊김을 AI로 귀속할 수 없다. 사용자 당시 GPU/CPU/메모리 및 다른 프로그램 부하도 모른다.

## 다음 최소 계측

사용자가 끊김을 느끼는 실제 월드·같은 카메라/이동 장면에서 짧은 표본을 잡는다. 평균 FPS 대신 프레임 시간과 16.7/25/33/50ms 초과 횟수를 본다. 먼저 현재 로그의 대표 장면을 확보하고, 필요하면 가벼운 임시 계측으로 프레임 번호와 총 모드 시간, AI별 brain/body, 후보 생성/길계획, 관측/저장, GC 수집 변화만 묶어 남긴다. 이로써 SEND에 나타나지 않는 Tick 스파이크도 분리한다.

그 뒤 동일 장면의 AI on/off 또는 인원별 비교가 필요하다. 현재 로그는 다른 후보 빌드·다른 월드이므로 비교군으로 대신 쓰지 않는다. 원인 구간이 잡히면 해당 계산만 여러 프레임으로 나누거나 안전한 스냅샷 기반 작업 스레드·캐시 개선을 검토한다. 후보 내용을 빼거나 판단 빈도를 임의로 낮추는 것은 제품 동작 변경이므로 이번 진단에서 결정하지 않았다.

이번 작업은 기존 실행 증거와 현재 코드 경로를 연결한 진단이다. 수정·성능 개선 완료 또는 사용자 장면 재현 완료를 뜻하지 않는다.
