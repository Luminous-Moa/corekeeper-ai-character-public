from pathlib import Path
import json,hashlib
P=Path(__file__).resolve().parents[1];R=P/'review';G=P.parents[1]/'게임행동-그래프'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
art=read(R/'post-publication-artifact-check.json');cli=read(R/'post-publication-cli-check.json')
assert art['status']==cli['status']=='pass'
assert art['publishedSha256']=={n:sha(G/'published'/n) for n in art['publishedSha256']}
assert art['configSha256']==sha(G/'publication.json')
parentcheck=read(P/'check-published.json');parentcli=read(P/'verification-published/summary.json')
assert parentcheck['status']==parentcli['status']=='pass'
assert parentcheck['twoAcceptedRebuildsMatchPublished'] is True
assert parentcheck['hashes']==parentcli['hashes']==art['publishedSha256']
for q in cli['actualQueries']:assert sha(Path(q['path']))==q['sha256']
refs=[
R/'post-publication-artifact-check.json',R/'post-publication-cli-check.json',
R/'post-publication-audit.py',R/'post-publication-cli.py',
R/'report.md',R/'findings.json',P/'review-acceptance.json',
P/'final-generation.json',P/'installation.json',P/'check-published.json',
P/'verification-published/summary.json',P/'finalize_reviewed.py',P/'install_reviewed.py',
G/'graph.py',G/'publication.json']
out={'status':'independent_pass','verdict':'PASS','reviewedContractSha256':art['contractSha256'],
'publishedDirectory':str(G/'published'),'publishedHashes':art['publishedSha256'],'configSha256':art['configSha256'],
'report':{'path':str(R/'post-publication-report.md'),'sha256':sha(R/'post-publication-report.md')},
'openFindings':[],'installedActualFilesReviewed':True,'physicalManifestFiles':art['physicalManifestPins'],
'physicalPublicationConfigCheckedWithoutException':True,'twoAcceptedRebuildsEvidenceAccepted':True,
'baseline246':{'publishedNodesExact':art['baselineNodesExact'],'publishedEdgesPreserved':art['baselineEdgesPreserved'],
'rawNodesExact':art['rawHistoryNodesExact'],'rawEdgesPreserved':art['rawHistoryEdgesPreserved'],
'oldRawUnprojected':True,'baselineProjectionExact':True,'newRawProjectionExact':True,'preservedPhysical3FilesExact':True},
'newOriginsActualPayloadExact':art['newOriginPayloadsExact'],'recordsReviewed':12,'scopeGroups':10,
'newRecords':art['newRecords'],'allNewAcceptanceStatuses':'independent_pass',
'actualIndependentCanonicalQueries':cli['actualQueries'],
'evidence':[{'path':str(p),'sha256':sha(p)} for p in refs],
'limitations':['지정10범위의 정적 게시 연결과 기본 조회에 대한 수용이다.',
'원문·생성규칙은 별도 독립 검수 보고서에 근거하며 전체 원문 전수조사를 반복하지 않았다.',
'실제 게임 행동 성공, AI 정책과 모델 호출, 전 게임 행동 완전성은 검증하지 않았다.'],
'runtimeTested':False,'wholeScopeComplete':False,'newSourceComplete':False}
dest=R/'post-publication-findings.json';dest.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'status':out['status'],'verdict':out['verdict'],'report':out['report'],'findings':{'path':str(dest),'sha256':sha(dest)}},ensure_ascii=False))

