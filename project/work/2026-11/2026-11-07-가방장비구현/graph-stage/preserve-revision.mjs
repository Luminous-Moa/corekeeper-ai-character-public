import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const stage=path.dirname(fileURLToPath(import.meta.url)),pin=JSON.parse(fs.readFileSync(path.join(stage,'final-pin.json'),'utf8'));
const revision=path.join(stage,'revisions',pin.sourceCommit);fs.mkdirSync(revision,{recursive:true});
const G='work/지식그래프',P='work/2026-11/2026-11-05-지식그래프-개편',L='work/2026-11/2026-11-05-지식그래프-실화면';
for(const rel of ['final-pin.json','canonical-before.json','baseline-summary.json','official-drift.json','stage-state.json','evidence-plan.json','command-results.json','query-results.json','stage-audit.json','evidence',G+'/graph',G+'/기획',P+'/B/code.graph.json',P+'/B/validation-review.json',P+'/B/snapshot.manifest.json',P+'/B/snapshot-verification.json',P+'/B/determinism.json',L+'/D/graph.json',L+'/D/validation.json','work/2026-11/2026-11-07-기획연결/source.mjs',L+'/D/bag-source.mjs','work/2026-11/2026-11-07-가방장비구현/evidence','work/2026-11/2026-11-07-가방장비구현/menu-examples.md','work/2026-11/2026-11-07-가방장비구현/connected-menu-examples.md','work/2026-11/2026-11-07-가방장비구현/review-tree-741.md','work/2026-11/2026-11-07-가방장비구현/implementation-evidence.json','work/2026-11/2026-11-07-가방장비구현/implementation-status.md']){
 const src=path.join(stage,rel),dest=path.join(revision,rel);if(!fs.existsSync(src)||fs.existsSync(dest))continue;fs.mkdirSync(path.dirname(dest),{recursive:true});fs.cpSync(src,dest,{recursive:true});
}
fs.writeFileSync(path.join(revision,'경계.md'),'# 중간판 보존\n\n'+(process.argv[2]||'이 판의 추출·strict/check·조회 성공을 보존한다. 이후 독립검수에서 추가 상자 공간 P2가 발견되어 최종 수용판으로 승격하지 않는다.')+' snapshot은 sourceCommit의 Git blob과 snapshot manifest로 다시 고정할 수 있다. 미실게임이다.\n');
console.log('Preserved intermediate revision '+pin.sourceCommit);
