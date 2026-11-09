# A-2 실제 화면 설계서

목적별 화면 4개와 공통 HUD 1개, 총 57개 요소. 원본 14장(A1 13장 + A3 관리 상세 1장)은 증거 표본이며 각각 별도 화면이 아니다. 코드·직접 UI·웹 업로드 없이 PNG를 실제 보고 작성했다.

| 화면 | 문서 |
|---|---|
| CKAI-01 월드·AI 캐릭터 | [설계서](CKAI-01.md) |
| CKAI-02 채팅 입력·행동 로그·/ai 목록 | [설계서](CKAI-02.md) |
| CKAI-03 일반 게임 메뉴 | [설계서](CKAI-03.md) |
| CKHUD-01 공통 월드 HUD | [설계서](CKHUD-01.md) |
| CKAI-04 플레이어 관리 | [설계서](CKAI-04.md) |

[manifest.json](manifest.json)은 화면/요소 ID·문구·정책 후보·증거별 좌표·A1 조작 키를 담는다. 정본 요소 정의는 screens, 시점별 표시/좌표는 evidence다.

| 증거 | 화면 | 원본 | 매칭본 |
|---|---|---|---|
| E01 월드·AI 캐릭터 | CKAI-01 | [PNG](../A1/01-world.png) | [HTML](E01.annotated.html) · [PNG](E01.annotated.png) |
| E02 채팅 열기·기존 행동 로그 | CKAI-02 | [PNG](../A1/02-chat-open.png) | [HTML](E02.annotated.html) · [PNG](E02.annotated.png) |
| E03 /ai 입력 초안 | CKAI-02 | [PNG](../A1/03-ai-draft.png) | [HTML](E03.annotated.html) · [PNG](E03.annotated.png) |
| E04 /ai 목록·도움말 결과 | CKAI-02 | [PNG](../A1/04-ai-help.png) | [HTML](E04.annotated.html) · [PNG](E04.annotated.png) |
| E05 조회 결과와 빈 채팅 입력 | CKAI-02 | [PNG](../A1/05-chat-reopen.png) | [HTML](E05.annotated.html) · [PNG](E05.annotated.png) |
| E06 /ai Claude 미실행 초안 | CKAI-02 | [PNG](../A1/06-target-draft.png) | [HTML](E06.annotated.html) · [PNG](E06.annotated.png) |
| E07 대상 초안 취소 | CKAI-02 | [PNG](../A1/07-target-cancelled.png) | [HTML](E07.annotated.html) · [PNG](E07.annotated.png) |
| E08 채팅 다시 열기 | CKAI-02 | [PNG](../A1/08-chat-open-again.png) | [HTML](E08.annotated.html) · [PNG](E08.annotated.png) |
| E09 채팅 위 휠 시도 후 슬롯 0 선택 | CKAI-02 | [PNG](../A1/09-chat-scroll-up.png) | [HTML](E09.annotated.html) · [PNG](E09.annotated.png) |
| E10 채팅 닫힘·슬롯 0 선택 | CKAI-02 | [PNG](../A1/10-chat-closed.png) | [HTML](E10.annotated.html) · [PNG](E10.annotated.png) |
| E11 슬롯 1 복원·월드 | CKAI-01 | [PNG](../A1/11-slot-restored.png) | [HTML](E11.annotated.html) · [PNG](E11.annotated.png) |
| E12 일반 게임 메뉴 | CKAI-03 | [PNG](../A1/12-game-menu.png) | [HTML](E12.annotated.html) · [PNG](E12.annotated.png) |
| E13 최종 월드·메뉴 닫힘 | CKAI-01 | [PNG](../A1/13-world-final.png) | [HTML](E13.annotated.html) · [PNG](E13.annotated.png) |
| E14 플레이어 관리 상세 | CKAI-04 | [PNG](../A3/new-user-05-player-management.png) | [HTML](E14.annotated.html) · [PNG](E14.annotated.png) |

## 해석과 한계

- 화면 ID CKAI-01 월드, CKAI-02 채팅, CKAI-03 일반 메뉴, CKAI-04 플레이어 관리, CKHUD-01 공통 HUD. H1은 CKHUD-01#1을 뜻한다.
- 정책은 관측 범위 후보이며 확정 계약이 아니다. HTTP/IPC/스트림이 없어 기능 연결은 비워 두었다. A1 'local help response'는 처리 위치 증거가 아니다.
- 공격 로그의 두 체력 숫자 사이 기호는 판독 불확실. 〔기호 불확실〕은 편집 표기. A1의 70→70 전사와 차이를 보존했다.
- 외부 앱 알림·상단 성능 표시를 제품 요소에서 제외. 가림·생략 부분은 복원하지 않음.
- 원본 14장만 다뤘으며 전체 게임 상태 전수 완료가 아니다.

## 재현과 검증

[render-annotations.ps1](render-annotations.ps1)은 manifest 좌표로 PNG를 생성한다. HTML은 로컬 원본 위 SVG 상자를 그린다. [validation.json](validation.json)에 ID·좌표·파일 확인 결과가 있다.


후속 재검수: 일반 메뉴의 플레이어 관리·SPACE는 A1 미실행과 A3 실제 진입을 구분했다. /ai 안내의 꺾쇠 안 인자명은 원본 확대에도 판독 불확실하여 이전 전사를 철회하고 편집 표기로 변경했다. [확대 원본 부분](qa-help-crop.png).
