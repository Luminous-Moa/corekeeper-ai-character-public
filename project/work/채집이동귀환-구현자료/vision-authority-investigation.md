# 사람 시야와 조명 — 원본 조사 및 HEAD 결정 경계

2026-11-07. 현재 AIVision을 바꾸지 않고 실제 게임 코드에서 읽을 수 있는 기준과 추가 범위를 조사했다.

## 확인한 권위 있는 경로

원본 기준 폴더는 `C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work/게임원본-상호작용/code`다.

1. `Pug.Other/MapUpdateSystem.cs:16`은 ClientSimulation 시스템이다. `:146-211`의 ExtractNearbyWallAndLightJob은 **GPU irradiance readback**을 world 좌표→pixel로 변환하고 `0.2126R+0.7152G+0.0722B >= 0.0015`로 HasLight를 만든다. 픽셀 좌표 보정 `-4`도 실제 코드에 있다. `:310-330`의 IsLit은 비벽이면 그 칸의 HasLight, 벽이면 인접 비벽의 HasLight를 사용한다. 이는 게임이 지도 공개에 실제로 쓰는 빛 기준이며 임의 CPU 광원 반경이 아니다. 단, 이 기준이 화면에서 모든 개별 물체가 지각 가능한지를 판정하는 함수라는 증거는 없다. 지도 공개는 기본 7칸 반경도 별도로 쓰므로 그 반경을 사람 화면 시야로 복사하면 범위를 잘못 좁힌다.
2. 같은 파일 `:465-515`는 `Manager.camera.gameCamera.GetPugCamera()` → `PugRP.TryGetCameraData(...)` → `TryGetRenderFeature<IndirectLightRenderFeature>` → `irradiance` → `AsyncGPUReadback.Request`로 자료를 얻는다. 광원 버퍼 범위·원점은 PugCamera.indirectLightSize / indirectLightAnchor / SnapBufferPosition에서 나온다. **호스트 카메라의 실제 렌더 범위를 벗어난 AI 위치는 이 버퍼의 현재 광도값으로 판정할 수 없다.** readback 미지원·실패·버퍼 미생성 경계도 원본에 존재한다.
3. `Pug.Other/ConditionsEffectsHandler.cs:297-350`는 플레이어의 SummarizedConditionEffectsBuffer, SummarizedConditionsBuffer(색상별 glow), ActAsLightSourceWhenHeldInHandCD, SmallGlowLightCD 등을 모아 실제 glowLight의 색·range를 설정한다. 이는 플레이어 빛의 입력이며 최종 환경 광도/그림자/간접광 판정값은 아니다.
4. `Pug.Other/LightManager.cs:202`의 UpdateShadowsAtTilePosition, `ManagedLight.cs:98-150`의 최적화는 렌더 카메라·PugLight 품질·fallback sprite에 의존한다. `ManagedLight.isLightEnabled`만으로 해당 칸이 어두운지를 판정할 수 없다.
5. `Pug.Other/DistanceToPlayerSystem.cs:427-443`의 DistanceToPlayerCD.isVisible은 **어느 플레이어와 거리 제곱이 676 이하인지**를 집계한다. 빛·가림·해당 AI 개인의 시야를 검사하지 않으므로 대체 API로 사용하면 잘못이다.
6. 현재 모드 `Assets/AIPlayerSpike/Scripts/AIPlayerSpikeMod.cs:691-696`는 ThinClientSimulation 시스템만으로 AI world를 만든다. 각 AI 전용 PugCamera/조명 render feature를 만들거나 갱신하는 구현은 현재 없다. 기존 AIVision의 화면 크기를 AI 위치로 평행 이동하는 것만으로 별도 조명장을 얻을 수 없다.

## 선택 가능한 구현 경로와 차이

| 경로 | 동작 및 정확성 | 성능·추가범위 | 검증 |
|---|---|---|---|
| 실제 게임 조명 버퍼 재사용, 유효 영역만 관측 | 호스트 render buffer와 겹친 AI 위치는 게임 지도 공개와 같은 luminance/벽 경계 판정 가능. 버퍼 밖은 unavailable이며 밝다고 추정하지 않음. 독립 원정이 버퍼 밖으로 나가면 새 관측이 막히므로 현재 목표를 제한하는 제품 선택이다. | GPU readback·world/pixel 정합성·색형식·frame 시각 관리. 기존 게임 render feature 재사용이라 비교적 작은 변경이나 렌더 없으면 사용할 수 없음. | 오프라인 좌표/임계값 fixture 가능. 실제 광원·벽·어둠·카메라 이동은 실게임 readback과 사람 화면 대조 필요. |
| AI 위치별 실제 PugRP 조명 렌더/읽기 | 개별 AI 위치에서 동일 렌더 계산을 확보하는 방향. 호스트 시야 밖을 자유롭게 관측할 수 있어 목표에 가장 부합할 가능성이 있다. 단순 Camera 복제만으로 타일/광원/물체 스트리밍·렌더원점이 함께 준비된다는 근거는 아직 없다. | 전용 오프스크린 camera/render target, PugRP feature 설정, 원점/광원/월드렌더 활성 범위, 자원 해제·4캐릭터 비용 관리. 현재 thin-client 구조를 넘는 렌더 통합 작업이며 실행 시험이 필요하다. | 실제 SDK/게임에서 카메라 등록·빛 버퍼 생성·여러 위치 동시 관측·host 화면 무영향·성능을 확인해야 한다. 현재 게임 시험 보류 중 오프라인만으로 완결 판정 불가. |
| CPU 광원·가림 근사 | 실제 glow 조건·light source 입력과 타일 가림으로 근사. 무렌더 위치에서도 계산 가능하지만 간접광·그림자·최적화/fallback·발광 물체 결과와 동일한 사람 시야라고 보장하지 못한다. | 별도 조명 모델/범위·임계값·벽 처리 정책을 선택해야 함. 기존 원본 사실을 조합해도 게임 렌더와 동등성은 새 검증 과제다. | fixture와 실제 화면 비교 가능. 사용자에게 근사 정책을 설명하고 합의해야 하며 정확 시야 구현으로 조용히 치환하면 안 된다. |

## HEAD에 반환하는 판단

확인한 원본에는 서버/ThinClientSimulation에서 임의 AI 좌표의 최종 지각 가능 여부를 바로 반환하는 함수가 없다. 실제 게임이 사용하는 빛 판정은 호스트 렌더의 GPU 결과를 읽는 경로다. 그러므로 **사람 시야에서 빛·어둠까지 동일하게 보장하는 것은 현재 자료만으로 작은 AIVision 조건 추가가 아니다.** 사람 화면 동일성의 정확한 완료 의미와 offscreen 렌더 통합/실게임 검증 재개 여부를 HEAD가 결정해야 한다. 조사자는 새 조명 반경·LOS 정책을 임의로 적용하지 않았으며 AIVision.cs는 변경하지 않았다.

현재의 통로 폭/방 분기 및 순환 개선은 이 판단과 독립적으로 진행한다. 조명이 확보되면 동일한 CanSee 경계를 모든 일반 관측·통행기억·후보에서 재사용해야 하며, 허용한 다음 갈림길 방향 선행 읽기만 그 경계 밖 예외로 남는다.
