import fs from 'node:fs';
import path from 'node:path';

const work = 'C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work';
const common = path.join(work, '2026-11/2026-11-07-공통실행자료');
const oldCommit = '11eb461ae122f404acec94563c086b09e002987c';
const commit = '685dde4d27dd91a782a14d8ae33fb576160fa578';
const b = path.join(work, '2026-11/2026-11-05-지식그래프-개편/B');
const change = (file, transform) => {
  const original = fs.readFileSync(file, 'utf8');
  const updated = transform(original);
  if (original === updated) throw new Error('No change in ' + file);
  fs.writeFileSync(file, updated);
};
for (const file of ['pin-snapshot.mjs', 'build.mjs', 'verify.mjs']) {
  change(path.join(b, 'extractor', file), text => text.replaceAll(oldCommit, commit));
}
for (const folder of ['snapshot', 'runtime-package']) {
  const source = path.resolve(b, folder);
  const target = path.resolve(common, 'graph-before', 'original-' + folder);
  if (source !== path.resolve(b, folder) || !source.startsWith(path.resolve(work) + path.sep)
      || !target.startsWith(path.resolve(common, 'graph-before') + path.sep)) throw new Error('Invalid snapshot move');
  if (fs.existsSync(target)) throw new Error('Archive already exists ' + target);
  fs.renameSync(source, target);
}
const overlay = path.join(work, '2026-11/2026-11-05-지식그래프-실화면/C/overlay-source.json');
change(overlay, text => {
  const value = JSON.parse(text);
  value.baseCommit = commit;
  value.historicalUiObservationCommit = oldCommit;
  return JSON.stringify(value, null, 2) + '\n';
});
change(path.join(work, '2026-11/2026-11-05-지식그래프-실화면/D/integrate.mjs'), text => text
  .replace("uiObservations.sourceCommit!==g.meta.sourceCommit)throw Error('UI observation code baseline mismatch')", "(uiObservations.sourceCommit!==g.meta.sourceCommit&&uiObservations.sourceCommit!==c.historicalUiObservationCommit))throw Error('UI observation code baseline mismatch')")
  .replaceAll('sameRevision:true,evidenceAppliesToCurrentRevision:true', 'sameRevision:uiObservations.sourceCommit===g.meta.sourceCommit,evidenceAppliesToCurrentRevision:uiObservations.sourceCommit===g.meta.sourceCommit'));
const banner = '> 현재 코드 기준은 `' + commit + '`이다. 공통 실행 변경과 오프라인 검증을 반영했다. 기존 실게임·화면 관측은 각각의 당시 커밋에 한정한다. [공통 실행 반영](../2026-11/2026-11-07-공통실행자료/graph-update.md)에서 범위와 검증을 확인한다.\n\n';
for (const name of ['README.md', '제품그래프-사용법.md']) {
  change(path.join(work, '지식그래프', name), text => banner + text.replaceAll('> 공식 코드 기준:', '> 과거 코드 기준:').replaceAll('본 ai-character와 일치.', '당시 ai-character 기준.').replaceAll('본 저장소 ai-character와 기준이 같다.', '당시 ai-character와 같은 기준이었다.'));
}
change(path.join(common, 'implementation.md'), text => text.replace('원래 ai-character나 설치본은 이 작업에서 수정하지 않았다.', '이후 root가 이 커밋을 ai-character에 fast-forward 통합했다. 설치본 배포는 수행하지 않았다.'));
console.log('Updated source baseline and archived exact old snapshot folders.');
