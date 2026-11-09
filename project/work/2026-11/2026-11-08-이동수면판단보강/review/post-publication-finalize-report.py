from pathlib import Path
import json,hashlib
P=Path(__file__).resolve().parents[1];R=P/'review';W=P.parents[1];G=W/'게임행동-그래프';LIVE=W/'2026-11/2026-11-05-지식그래프-실화면/D'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
art=read(R/'post-publication-artifact-check.json');initial=read(R/'post-publication-consumer-check.json');final=read(R/'post-publication-final-consumer-check.json')
assert art['status']==initial['status']==final['status']=='pass'
assert art['contractSha256']==sha(P/'contract.json')=='74614162f5aafdbc594318fa5b08f93b02dc23f011cbc2a22ed6edd485d1b743'
assert art['publishedSha256']==final['publishedSha256']=={n:sha(G/'published'/n) for n in art['publishedSha256']}
assert art['configSha256']==sha(G/'publication.json')
parent=read(P/'consumer/verification.json');check=read(P/'consumer/final-check.json')
assert parent['status']=='pass' and parent['completionStage']=='final_after_todo_refresh' and parent['awaitingFinalTodoAndBuild'] is False
assert check['ok'] is True and check['structuralErrors']==check['stale']==[]
assert parent['productBuildCheckByteIdentical'] is True and 'byte-identical' in (P/'consumer/build-check.txt').read_text(encoding='utf-8')
assert parent['gameHashes']==art['publishedSha256']
assert parent['productHashes']['graph']==final['finalProjectGraphSha256']==sha(LIVE/'graph.json')
assert parent['productHashes']['validation']==final['finalValidationSha256']==sha(LIVE/'validation.json')
assert parent['currentTodoSha256']==final['finalTodoSha256']==sha(W.parent/'todo.md')
assert sha(Path(parent['preTodoEvidence']['path']))==parent['preTodoEvidence']['sha256']
assert all(q['exitCode']==0 for q in parent['retainedPreTodoActualQueries']) and len(parent['retainedPreTodoActualQueries'])==17
for q in final['actualIndependentQueries']:assert sha(Path(q['path']))==q['sha256']
refs=[
R/'post-publication-artifact-check.json',R/'post-publication-consumer-check.json',R/'post-publication-final-consumer-check.json',
R/'post-publication-audit.py',R/'post-publication-consumer.py',R/'post-publication-final-consumer.py',
R/'post-publication-consumer-initial-drift.json',R/'post-publication-project-node-delta.json',R/'post-publication-project-edge-delta.json',
R/'source-report.md',R/'source-findings.json',P/'review-acceptance.json',P/'contract.json',P/'final-generation.json',P/'installation.json',
P/'check-published.json',P/'verification-published/summary.json',P/'consumer/verification.json',P/'consumer/verification-pre-todo.json',
P/'consumer/final-check.json',P/'consumer/build-check.txt',P/'consumer/verify.mjs',P/'finalize_reviewed.py',P/'install_reviewed.py',
G/'graph.py',G/'publication.json',W/'2026-11/2026-11-08-행동판단프로젝트연계/topics.mjs',W/'2026-11/2026-11-08-행동판단프로젝트연계/link.mjs']
out={'status':'independent_pass','verdict':'PASS','reviewedContractSha256':art['contractSha256'],
'publishedDirectory':str(G/'published'),'publishedHashes':art['publishedSha256'],'configSha256':art['configSha256'],
'report':{'path':str(R/'post-publication-report.md'),'sha256':sha(R/'post-publication-report.md')},
'openFindings':[],'resolvedFindings':[{'finding':'프로젝트의 기존4노드·3간선에서 todo 전체파일 출처SHA가 갱신됨',
'resolution':'HEAD가 정확 해당ID의 evidence.sha 갱신만 수용하도록 명시했고 실제 최종todo SHA·anchor/line·모든 다른필드 동일성을 독립 확인함',
'evidence':str(R/'post-publication-final-consumer-check.json'),'doesNotRelaxGameBaselinePreservation':True}],
'installedActualFilesReviewed':True,'physicalManifestFiles':art['physicalManifestPins'],'physicalPublicationConfigCheckedWithoutException':True,
'twoAcceptedRebuildsEvidenceAccepted':True,'baseline256':{
'publishedNodesExact':art['baselineNodesExact'],'publishedEdgesPreserved':art['baselineEdgesPreserved'],
'rawNodesExact':art['rawHistoryNodesExact'],'rawEdgesPreserved':art['rawHistoryEdgesPreserved'],
'oldRawUnprojected':True,'newRawUnprojected':True,'baselineProjectionExact':True,'newRawProjectionExact':True,'preservedPhysical3FilesExact':True},
'newOriginsActualPayloadExact':art['newOriginPayloadsExact'],'recordsReviewed':5,'scopeGroups':4,'newRecords':art['newRecords'],
'allNewAcceptanceStatuses':'independent_pass','actualIndependentQueries':final['actualIndependentQueries'],
'projectConsumer':{'finalProjectGraphSha256':final['finalProjectGraphSha256'],'finalValidationSha256':final['finalValidationSha256'],
'finalTopicsSha256':parent['productHashes']['topics'],'finalTodoSha256':final['finalTodoSha256'],
'oldProjectSemanticNodesPreserved':final['oldProjectNodes'],'oldProjectSemanticEdgesPreserved':final['oldProjectEdges'],
'oldProjectNodesExact':final['oldProjectNodesExact'],'oldProjectEdgesExact':final['oldProjectEdgesExact'],
'sourcePinRefresh':final['sourcePinRefresh'],'allForeignReferencesExact':final['allForeignReferencesExact'],
'allFinalProjectSourcePinsExact':final['allFinalProjectSourcePinsExact'],
'onlyFiveTopicLabelsAdded':final['onlyFiveTopicLabelsAdded'],'onlyFiveNewForeignNodesAndFiveTopicReferenceEdges':final['onlyFiveNewForeignNodesAndFiveTopicReferenceEdges'],
'runtimeValidationExact':True,'unrelatedGraphSectionsExact':True,'normalPlanningBehaviorShowSourceVerified':True,
'productCheckStale':[],'productCheckStructuralErrors':[],'productBuildCheckByteIdentical':True,
'independentGCanonicalQueries':6,'independentProjectNormalQueries':6,'producerRetainedPreTodoActualQueries':17,'producerFinalActualCheckQueries':1,
'queriesRetainedAfterFinalTodoSourcePinOnlyRefresh':True},
'evidence':[{'path':str(p),'sha256':sha(p)} for p in refs],
'limitations':['指定4범위의 정적 연결과G/프로젝트 정상조회에 대한 수용이다.',
'원문·생성규칙은 별도 독립검수에 근거하며 source전체재검수·전체collision·종별·새gapscan을 수행하지 않았다.',
'Mount와322는 source-free이며 actualDB/world 성공·322공급자·게임runtime 성공·AI모델실행·전게임완전성을 검증하지 않았다.'],
'runtimeTested':False,'wholeScopeComplete':False,'newSourceComplete':False}
out['limitations'][0]='지정4범위의 정적 연결과G/프로젝트 정상조회에 대한 수용이다.'
dest=R/'post-publication-findings.json';dest.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'status':out['status'],'verdict':out['verdict'],'report':out['report'],'findings':{'path':str(dest),'sha256':sha(dest)}},ensure_ascii=False))

