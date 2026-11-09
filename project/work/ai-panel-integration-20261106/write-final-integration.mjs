import fs from 'node:fs';
import crypto from 'node:crypto';
import {spawnSync} from 'node:child_process';
const W='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work',T=W+'/ai-panel-integration-20261106',G=W+'/지식그래프',L=W+'/2026-11/2026-11-05-지식그래프-실화면';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8').replace(/^\uFEFF/,'')),sha=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const backup=read(T+'/canonical-before-ui/manifest.json');
for(const row of backup)if(sha(row.backup)!==row.sha256)throw Error('backup mismatch '+row.backup);
const before=read(T+'/evidence/main-dirty-before-integration.json');
for(const row of before)if(sha('C:/Dev/CoreKeeperModSDK/'+row.path).toUpperCase()!==row.sha256.toUpperCase())throw Error('dirty changed '+row.path);
const head=spawnSync('git',['rev-parse','HEAD'],{cwd:'C:/Dev/CoreKeeperModSDK',encoding:'utf8'}).stdout.trim();
if(head!=='11eb461ae122f404acec94563c086b09e002987c')throw Error('HEAD changed');
const pg=read(L+'/D/graph.json'),dg=read(G+'/graph/graph.json');
const check=read(T+'/evidence/product-9-product.mjs.log');
const findings={head,dirtyTrackedPreserved:before.length,backupFilesVerified:backup.length,backupBytes:backup.reduce((n,r)=>n+r.bytes,0),development:{nodes:dg.nodes.length,edges:dg.edges.length},product:{nodes:pg.nodes.length,edges:pg.edges.length,structuralErrors:check.structuralErrors,stale:check.stale,baselineDrift:check.baselineDrift},uiEvidenceFiles:pg.uiRuntimeObservations.inputs.length,uiObservations:pg.uiRuntimeObservations.observations.map(o=>o.id)};
fs.writeFileSync(T+'/evidence/final-integration-check.json',JSON.stringify(findings,null,2)+'\n');
fs.writeFileSync(T+'/09-final-integration.md',`# 정식 통합과 공식 그래프 완료

사용자가 승인한 검증 후보 **${head}**를 본 C:/Dev/CoreKeeperModSDK의 ai-character에 git merge --ff-only로 통합했다. 직전 기준은 b25099f6fd31e4c4c3618357eb9e8101e0e17124이다. 제품 코드 추가 수정·push는 하지 않았다. Unity/게임/설치는 본 담당이 실행하지 않았으며 정식 설치 결과는 별도 실게임 담당의10 보고를 따른다.

## 기존 작업 보존

본 트리 dirty tracked **${before.length}개**의 통합 전후 및 그래프 완료 시 SHA256이 모두 같다. 기존 untracked 목록도 그대로 보존했다. 증거는 evidence/main-status-before-integration.txt, main-dirty-before-integration.json, main-dirty-after-integration.json 및 final-integration-check.json이다. 공통 설정·패키지·게임 데이터는 건드리지 않았다.

## 공식 그래프 반영

원본·생성물 **${backup.length}개 / ${findings.backupBytes} bytes**를 canonical-before-ui/에 복사하고 모든 백업 해시를 완료 시 다시 확인했다. manifest.json에 원본·백업 경로와 해시가 있다. 선행 판단 선택지 b25099f 원천 변경은 유지했다.

개발 그래프는 새 코드8개를 등록하여 총96개 C#를 읽는다. F.ai-info-ui, F.chat-compact-screen 및 pol.ui-embedded-readonly를 추가했다. 두 새 기능의 최종 실게임 선 서명은 각각2c91bcf86e15 / 24857121287a이다. 기존 기능의 과거 확인 상태는 승격하지 않았다. 최종 개발 그래프는${dg.nodes.length}노드 / ${dg.edges.length}연결이다.

제품 그래프는 최종11eb461의 Git blob309개와 외부 runtime 소스11개를 고정·대조했다. 새 OnGUI 기능2개를 정적 진입점으로 추출하고 새 사용자 활동1개·화면2개·요소3개 및9연결을 추가했다. 실행 trace로 간주하지 않는다. 최종 제품 그래프는${pg.nodes.length}노드 / ${pg.edges.length}연결이다.

실게임 근거${findings.uiEvidenceFiles}개는 C/ui-20261106/에 해시로 연결했다. UI-CHAT-11eb461 및 UI-INFO-11eb461은 현재판 동일 revision 관측이다. 과거76563bf 화면과902721e 실행 근거는 별도 역사적 관측으로 보존했다. 새 기능을 과거 매핑과 혼동하지 않도록 C 참조검사기가 현재 B의 기능 ID도 확인하도록 수정했다. 새 결정적 표본8개의 소스 선언·외부 경계를 대조했고 이전 표본 기록도 보존했다.

## 최종 관문

- 개발 strict build / query check: 통과. 원본 해시·인용 일치, 인용·앵커·ID 경고 없음.
- 제품 pin → Roslyn Extract → build → verify: 통과. AST syntax diagnostic0; 외부 게임 참조가 없는 분석의 semantic diagnostic은 기존 도구 한계이며 제품 컴파일 결과와 구분한다.
- 결정성: 추출 산출물7개 두 번 실행 byte-identical.
- C 통합·실화면 remap·C 연결검사·질문·entrypoint: 모두 통과.
- product check: structuralErrors0, stale0, baseline warning null, 본 HEAD와 기준11eb461 일치.
- product-build --check: byte-identical.

명령과 전체 출력은 evidence/canonical-b-gates.json, canonical-dev-gates.json, canonical-product-gates.json 및 각 log에 있다. 제품 실행을 하지 않는 이 단계에서 Unity 관문은 반복하지 않았다. 독립 코드 검수와11eb461 실게임 검증은07/04/08, 정식 설치 검수는10에 분리한다.

## 수용 범위와 남은 한계

기본 채팅 본문은 실제 AI 발언 한글43~44→24 캡처px, 줄간격26~29px로 축소됐다. 실제 화면3840×2160/캡처2560×1440이며 입력행·멘션 힌트 크기는 기존대로 보존한다. UI는 읽기 전용이고 추가 모델 호출이나 자동착용이 없다. 스킬 값과 빈 장비 슬롯, 세 부위 실제 장착 fixture 양성 표시, off/on 갱신·입력차단을 관측했다. fixture 원본 복원은 실게임 보고에 남겼다.

실제 OS IME 조합, 다중버튼 동시 유지, 다른 해상도 실게임 및 성능 benchmark는 미시험이다. 자동착용·행동 선택지는 사용자가 별도 작업으로 명시했다. 03-graph-handoff.md는 최종 후보와04/08 근거 기준으로 바로잡았다.
`);
console.log(JSON.stringify(findings));
