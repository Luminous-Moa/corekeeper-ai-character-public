# 제약 해제 그래프 사전 stage — 역사적 준비 기록

> 아래의 정본 미변경·후보 미확정 문구와10노드/17관계는 **사전 준비 당시**의 범위다. 이후 HEAD 승인으로 정본을 갱신했고 현재 기준은 공식5cc다. 현재 반영 결과는 [graph.md](../graph.md), 최종 정합성은 [final-verification.json](final-verification.json)을 따른다. 사전 prepare/verify 명령을 현재 정본에 다시 적용하지 않는다.

정본 source.mjs·제품 graph.json·실행 코드는 변경하지 않았다. 기존 기획연결의 compile.mjs와 graph.mjs를 복사해 동일한 노드/관계/근거 검사로 stage를 재생성한다.

- canonical-addition.mjs.txt: 정본 source.mjs에 들어갈 신규 delta와 역사 노드 정정. 정본에 아직 적용하지 않았다.
- delta-source.json: 사람이 검토할10노드/17관계 및 역사 노드4개 정정.
- planning/: 기존 생성/조회 도구와 재생성된108노드/172관계의 기획 사본.
- source-pins/: 현재 작업 중 변경되는 todo·info 문서 등에 흔들리지 않게 등록 근거63개를 해시와 함께 고정한 사본. 원본 위치는 source-pins.json에 있다.
- stage-manifest.json: 정본 source 해시·현재 후보 HEAD와 dirty상태·수용/추출/설치/게임 미수행 경계.
- verification.json:11대표조회·신규 노드 도달성·결정성·정본불변 확인.

지식그래프를 일반 게임지식 DB나 게임 AI의 전체 공개로 바꾸지 않는다. 지식공개·소유권·사용자예산 보호는 유지 조건이다. 현재 구현 후보는 통합SHA/검수결과 확정 전이며 구현수용·본반영·실게임 완료로 표시하지 않는다.

실행:

```powershell
node prepare-stage.mjs
node planning/graph.mjs build
node verify-stage.mjs
node planning/graph.mjs path P.release-goal P.unrestricted-support
node planning/graph.mjs source P.unrestricted-guards
```

prepare-stage는 stage만 갱신한다. 현재 근거가 바뀌었다면 새 사본을 만들고 build/check를 다시 수행한다. 적용 전 정본 source 해시가 달라졌으면 새 변경을 보존한 상태에서 delta를 재검토한다.
