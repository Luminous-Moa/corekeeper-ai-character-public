# 코어키퍼 오브젝트 상호작용 수집

이 폴더에서 `node reproduce.mjs`를 실행한다. 공통 수집, equipment·world·creatures·items 수집, 통합 검사를 순서대로 실행하며 어느 단계든 오류면 중단한다. Node.js와 각 worker가 참조하는 로컬 디컴파일 파일이 필요하다. 최초 실행에 고정 입력 스냅샷이 없다면 collect.mjs가 지정된 게임 자료에서 복사한다. 게임 실행이나 기존 그래프 변경은 하지 않는다.

모집단은 Steam build 26332184의 고정 덤프와 ObjectID 등록 목록의 합집합 3,031개 key다. variation별 행을 유지하며 좌표·액체·추상 상태 전체를 포괄하지 않는다. data는 덤프 관계 관측, code_candidate는 디컴파일 코드 후보이며 프리팹 연결, 버전 대응, 실제 실행을 증명하지 않는다. 전체 key를 처리했다는 것은 모든 상호작용을 발견했다는 뜻이 아니다. 미확인은 불가능을 뜻하지 않고 수집은 최종 그래프 설계가 아니다.

- `수집지침.md`: 원본과 수집 계약, 철검 pilot 대조 방법.
- `inputs/`, `sources.json`: 고정 스냅샷 및 해시.
- `pilot-iron-sword.json`: 철검 8901:0 원본 대조 예시.
- `workers/<group>/results.json`, `report.md`: 각 그룹의 결과, 규칙과 한계.
- `collection.json`: 원본 ID·variation·raw·기존 관계와 그룹별 결과 및 모든 규칙.
- `coverage.json`: 입력과 출력 및 근거 수준별 집계.
- `objects-interactions.md`: 전체 key의 이름, 행위, 근거 수준, 미확인 목록.

통합은 스냅샷 해시, 키의 완전성·중복, 기존 interaction의 보존, 코드 파일 해시·행 범위, 데이터 JSON pointer, 규칙 참조를 확인한다. 해시와 pointer 검사는 원문 존재와 무결성을 확인하며 해석의 타당성이나 게임 내 성공을 보장하지 않는다. 원본 코드가 바뀌거나 worker 산출물이 공통 입력과 어긋나면 오류를 확인하고 수집을 다시 해야 한다.

`code-sources.json`은 코드 근거의 고정 해시다. 재현 전에 검사하며, 새로운 코드 근거가 필요하면 수집 결과를 검토한 뒤 `node reproduce.mjs --pin-code`로 명시적으로 갱신하고 재검수해야 한다. 이 해시는 설치 빌드와 디컴파일의 대응을 증명하지 않는다.
