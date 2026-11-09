import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url));
const current=JSON.parse(fs.readFileSync(path.join(root,'candidate-code-current.json'),'utf8'));
const candidate=JSON.parse(fs.readFileSync(path.join(root,current.relativeRoot,'anchors.graph.json'),'utf8'));
const pinned=JSON.parse(fs.readFileSync(path.join(root,current.relativeRoot,'input-manifest.json'),'utf8'));
let md='# 생활 트리 격리 후보의 코드 참고\n\n';
md+='## 고정 후보와 지위\n\n';
md+='후보 `'+current.sourceCommit+'`, 기반 `'+candidate.baseCommit+'`를 Git blob으로 고정했다. 현재 지위는 **격리 구현 후보·검증 진행중**이다. 공식 ai-character의685dde4 코드 그래프를 이 후보로 바꾸지 않았고, 설치·실게임·최종 독립 수용은 확인하지 않았다.\n\n';
md+='이 판은 HEAD가 고정한 최종 생산 후보이며 최종 독립 수용 지위는 pending이다. 이전24c61c2는 중간 참고로 보존한다. 제품 코드/Tools의 고정 상태와 자체검증 보고는 아래 별도 근거로 구분하고 공식 반영·설치·실게임으로 승격하지 않는다.\n\n';
md+='이 문서의 자체 확인은 Git blob 신원·소스 앵커의 존재와 정적 추출이다. 실행/컴파일 수치는 생산자가 남긴 고정판 자체검증 보고이며 이 그래프 컨텍스트가 재실행한 검증이 아니다.\n\n';
md+='## 대표 코드와 경계\n\n';
md+='기존 제품 코드층에서 사용하는 Roslyn Extract.dll을 재사용했다. 관련'+pinned.files.length+'개 생산소스 중 변경'+pinned.checks.changedFilesExtracted+'개만 다시 추출하고 동일 blob의 미변경'+pinned.checks.unchangedFilesReused+'개를 재사용해 실제 함수'+candidate.nodes.length+'개의 신원·범위를 확인했다. 선택 심볼 사이 정적 관계'+candidate.edges.length+'개, 구문 진단'+candidate.boundaries.syntaxDiagnostics+'개다. delta 범위 밖 호출을 새로 추정하지 않아 부분 정적 관계 수는 이전판과 다를 수 있다. Unity/게임 metadata를 로드하지 않은 semantic 진단은 제품 컴파일/런타임 실패를 뜻하지 않는다. 호출 관계는 정적 후보이며 실행 trace가 아니다.\n\n';
md+='정식 프로젝트 그래프의 기존 상위 기획·공통실행 및 코드 기준은 유지한다. 별도 candidate-project-preview.json은 기획↔후보 코드의 부분 색인이지 정식 제품 그래프 전체가 아니다.\n\n';
md+='| 실제 심볼 | 후보 원본 |\n|---|---|\n';
for(const n of candidate.nodes){const snapshot=current.relativeRoot+'/snapshot/'+n.file;md+='| `'+n.signature.replaceAll('|','\\|')+'` | ['+n.file.split('/').at(-1)+':'+n.lineStart+']('+snapshot+':'+n.lineStart+') |\n';}
md+='\n파일별 Git blob OID·SHA256는 ['+current.relativeRoot+'/input-manifest.json]('+current.relativeRoot+'/input-manifest.json), 정적 원자료는 [csharp.raw.json]('+current.relativeRoot+'/csharp.raw.json)에 있다.\n';
fs.writeFileSync(path.join(root,'candidate-implementation.md'),md);
const resultRoot=path.resolve(root,'..'),validationPath=path.join(resultRoot,'logs/final-validation.json');
if(fs.existsSync(validationPath)){
 const result=JSON.parse(fs.readFileSync(validationPath,'utf8').replace(/^\uFEFF/,''));
 if(result.sourceCommit!==current.sourceCommit)throw Error('자체검증 sourceCommit과 후보 코드판 불일치');
 const evidenceRoot=path.join(root,current.relativeRoot,'evidence');fs.mkdirSync(evidenceRoot,{recursive:true});
 const inputs=[];
 for(const rel of ['05-implementation.md','logs/final-validation.json','logs/unity-start.json','logs/unity-exit.json','logs/unity-summary.json']){
  const source=path.join(resultRoot,rel);if(!fs.existsSync(source))throw Error('검증 원본 없음: '+rel);
  const bytes=fs.readFileSync(source),out=path.join(evidenceRoot,rel);fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,bytes);
  inputs.push({path:rel,sha256:crypto.createHash('sha256').update(bytes).digest('hex')});
 }
 fs.writeFileSync(path.join(evidenceRoot,'manifest.json'),JSON.stringify({sourceCommit:current.sourceCommit,source:'생산자 자체검증 보고와 HEAD가 고정한 후보',finalAcceptance:'pending',installed:result.installed,liveGame:result.liveGame,inputs},null,2)+'\n');
 let v='# 고정 생활 후보의 자체검증 참고\n\n## 생산자 자체검증과 수용 경계\n\n';
 v+='고정판 `'+current.sourceCommit+'`의05 구현 보고와 final-validation.json, Unity 시작/종료/요약 JSON의 sourceCommit을 연결했다. 이 컨텍스트는 보고·커밋일치·원본해시를 확인했고 해당 실행을 재실행하지 않았다. 최종 독립 수용은 대기 중이다.\n\n';
 v+='Core '+result.core+', bridge '+result.bridge+', 공통실행 '+result.execution+', 생활 '+result.life+', 요리 '+result.cook+', 제작 '+result.craft+', 상자 '+result.inventory+' 자체검증 통과 보고다. fast compile 경고'+result.compilerWarnings+'·오류'+result.compilerErrors+', Unity exit '+result.unityExit+'·compiler error '+result.unityCompilerErrors+'를 보고했다.\n\n';
 v+='어댑터는 생산 정책·응답 분기를 실행하지만 월드/ECS·DB·가방·요청 큐·소비 상태를 통제한다. 전체 CandBuild 선별/경로 탐색과 AIBrain.Tick 스케줄러·실제 모델/API·실게임 소비자의 성공은 증명하지 않는다. 설치 '+result.installed+', 실게임 '+result.liveGame+'이며 후속 수리·자동가공·강화·거래·낚시를 지원 완료로 표시하지 않는다.\n\n';
 v+='고정 증거 사본과 해시는 ['+current.relativeRoot+'/evidence/manifest.json]('+current.relativeRoot+'/evidence/manifest.json)에 있다. 최종 독립검수 결과가 오기 전에는 이 참고를 최종 수용으로 승격하지 않는다.\n';
 fs.writeFileSync(path.join(root,'candidate-validation.md'),v);
}
console.log('후보 지위와 '+candidate.nodes.length+' 실제 심볼 참고 문서 생성; 최종 수용 pending.');
