import fs from 'node:fs';
const W='C:/Dev/.head/sessions/코어키퍼-AI-캐릭터/work',T=W+'/ai-panel-integration-20261106',B=W+'/2026-11/2026-11-05-지식그래프-개편/B';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8')),write=(p,o)=>fs.writeFileSync(p,JSON.stringify(o,null,2)+'\n');
const old=read(B+'/validation-review.json'),current=read(B+'/validation.json');
const reasons={
'hook:AIPlayerSpikeMod.Init':'AIPlayerSpikeMod.cs:199 empty IMod.Init declaration; no hidden work inferred.',
'hook:AIChatScreenLayer.OnGUI':'AIChatScreenLayer.cs:131 Repaint/camera guard and owned glyph draw loop; MonoBehaviour.OnGUI registration is an engine message, not a runtime trace.',
'cli:HeadlessSdk.BuildAndInstall':'HeadlessSdk.cs:107 settings/read/buildBundles=false and reflective BuildMod; external SDK reflection boundary retained.',
'cli:HeadlessSdk.CreateMod':'HeadlessSdk.cs:67 existing-settings guard and reflective CreateNewMod; external SDK boundary retained.',
'hook:AIPlayerSpikeMod.EarlyInit':'AIPlayerSpikeMod.cs:193 IMod entry logs host/off default; no behavior beyond source body inferred.',
'hook:AIPlayerSpikePatches.ServerNetworkUpdatePrefix':'AIPlayerSpikePatches.cs:59 delegates Pump; Harmony prefix registration AIPlayerSpikeMod.cs:514 checked.',
'hook:AIPlayerSpikeMod.CanBeUnloaded':'AIPlayerSpikeMod.cs:207 IMod boolean entry returns true.',
'hook:AIPlayerSpikeMod.Update':'AIPlayerSpikeMod.cs:226 hosted-state/member Tick loop, chat layer and info panel Frame calls; independent live evidence stays in D.'};
old.previousSampleReview={sourceCommit:old.currentSourceCommit,samples:old.samples};
old.samples=current.samples.map(s=>({...s,sourceCheck:'manual source declaration and entry boundary inspected',result:'pass',reason:reasons[s.id]}));
if(old.samples.some(s=>!s.reason))throw Error('unreviewed sample');
old.currentSourceCommit='11eb461ae122f404acec94563c086b09e002987c';old.recheck+=' 2026-11-06 final UI 11eb461: two OnGUI feature roots change deterministic shuffled sample membership. All new eight declarations/boundaries reread; previous samples retained separately. No runtime trace inferred.';write(B+'/validation-review.json',old);
fs.writeFileSync(T+'/03-graph-handoff.md',`# 최종 그래프 반영

사용자가 승인한 최종 후보 11eb461ae122f404acec94563c086b09e002987c를 본 ai-character에 FF한 뒤 공식 그래프에 반영했다. 최종 근거는 04-live-verification.md와 08-acceptance.md이며, 초기2762495/25db8ed 계획과 구분한다. 공식 원본/생성물은 canonical-before-ui/manifest.json에 해시 검증 백업했다. 절차·결과는09-final-integration.md를 따른다.

개발 그래프는 새 C#8개를 명시 등록했다(전체96개, HeadlessSdk 포함). F.ai-info-ui, F.chat-compact-screen, pol.ui-embedded-readonly를 추가했다. UI 두 기능만 최종 실게임 코드 선 서명을 기록했으며 기존 기능의 과거 확인 상태는 승격하지 않았다.

제품 그래프는 최종 Git blob으로 재추출했다. Unity MonoBehaviour.OnGUI 두 진입점은 정적으로 식별한 기능이며 실제 함수 trace가 아니다. 새 사용자 활동→선택/상세/채팅 출력→OnGUI 기능을 연결하고 C/ui-20261106/observations.json에 최종 실행판·근거 해시·한계를 별도 기록했다. 기존76563bf 화면 및902721e 실행 근거는 역사적 관측으로 보존했다.

수용 범위: 기존 채팅 출력 본문43~44→24 캡처px, 줄간격26~29px; 입력행과 멘션 힌트 크기는 기존대로. 실제 게임3840×2160/캡처2560×1440. 스킬 실제값, 빈 슬롯 및 실제 장착 fixture 양성, off/on 갱신, UI 입력 차단·HUD 불변 확인. fixture는 제품 자동착용 성공이 아니다. 사용자 지시대로 자동착용·행동선택지는 범위 밖이다. 실제 OS IME 조합·다중버튼 동시 유지·다른 해상도 실게임·성능 benchmark는 미시험으로 남겼다.

생성물 직접 수정 없이 원본/추출기에서 수정한 뒤 strict/query check, pin/Extract/build/verify/determinism, C/remap/연결검사, 제품 질문/entrypoint, product check 및 byte-identical 재생성을 수행했다. 최종 증거와 별도 설치 인계는09 기록을 따른다.
`);
console.log('updated source sample review and final handoff');
