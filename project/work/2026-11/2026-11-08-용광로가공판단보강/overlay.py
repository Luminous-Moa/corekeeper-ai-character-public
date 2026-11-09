"""Append the reviewed ProcessResources processing chain over accepted260."""
from collections import Counter
from pathlib import Path
import argparse,hashlib,importlib.util,json,sys
P=Path(__file__).resolve().parent
R=P.parent/'2026-11-08-이동수면판단보강'
spec=importlib.util.spec_from_file_location('accepted260',R/'overlay.py')
previous=importlib.util.module_from_spec(spec);spec.loader.exec_module(previous)
api=previous.api;G=previous.G;NAMES=previous.NAMES
view=previous.view
def sha(path):return previous.sha(Path(path))
def read(path):return api.read(Path(path))
def write(value,path):return previous.write(value,path)
KINDS={'conditions':'condition','costs':'condition','flow':'flow','results':'result','failures':'failure','boundary':'unknown','codeReferences':'evidence','sourceEvidence':'evidence','existingReuse':'evidence','summary':'evidence','decisionImpact':'evidence'}

def build_history():
    contract=read(P/'contract.json')
    for pin in contract['pins']:
        if sha(pin['path'])!=pin['sha256']:raise ValueError('Reviewed input changed: '+pin['path'])
    status='pending_independent_review';review=None
    if (P/'review-acceptance.json').exists():
        receipt=read(P/'review-acceptance.json');review=receipt['review']
        if receipt['status']!='independent_pass' or receipt['reviewedContractSha256']!=sha(P/'contract.json') or sha(review['path'])!=review['sha256']:raise ValueError('Exact independent receipt required')
        findings=read(review['path'])
        if findings['status']!='independent_pass' or findings['reviewedContractSha256']!=sha(P/'contract.json'):raise ValueError('Independent findings do not pass this exact contract')
        if sha(findings['report']['path'])!=findings['report']['sha256']:raise ValueError('Independent report changed')
        status='independent_pass'
    # Accepted260 was already independently rebuilt and published. Consume its
    # immutable raw history as a pinned input rather than redoing old research.
    base={name:read(R/'history-current'/name) for name in NAMES}
    if 'decisionView' in base['graph.json']:raise ValueError('Accepted260 input must be preserved raw history')
    # Ensure the complete prior logical history and its published view are kept.
    projected=view.project(base['graph.json'])
    if hashlib.sha256(api.encode(projected)).hexdigest()!=contract['baselineGraphSha256']:raise ValueError('Prior accepted260 graph changed')
    b=api.Builder();b.nodes=base['graph.json']['nodes'].copy();b.edges={tuple(e) for e in base['graph.json']['edges']};b.inputs={}
    aliases=[]
    for f in base['manifest.json']['files']:
        item={**f,'roles':list(f['roles'])}
        if (G/f['path']).resolve()==G/'publication.json':
            if f['sha256']!=sha(P/'publication-before.json'):raise ValueError('Unexpected prior publication config')
            item['path']=api.rel(P/'publication-before.json')
            aliases.append({'logicalPath':f['path'],'physicalPath':item['path'],'sha256':item['sha256'],'basis':'Exact accepted260 configuration retained before publication switch.'})
        b.inputs[item['path']]=item
    def pin(path,role,expected=None,logical=None):
        path=Path(path).resolve();actual=sha(path)
        if expected and actual!=expected:raise ValueError('Pin changed: '+str(path))
        key=api.rel(logical or path)
        if key in b.inputs:
            if b.inputs[key]['sha256']!=actual:raise ValueError('Conflicting physical pin: '+key)
            b.inputs[key]['roles']=sorted(set(b.inputs[key]['roles']+[role]))
        else:b.inputs[key]={'path':key,'bytes':path.stat().st_size,'sha256':actual,'roles':[role]}
    for ref in contract['pins']:pin(ref['path'],'process_resources_reviewed_input',ref['sha256'])
    pin(P/'contract.json','process_resources_exact_review_contract')
    if review:
        pin(review['path'],'process_resources_independent_review',review['sha256']);pin(P/'review-acceptance.json','process_resources_independent_receipt')
        pin(findings['report']['path'],'process_resources_independent_report',findings['report']['sha256'])
    pin(P/'publication.config.json','canonical_publication_config',logical=G/'publication.json')
    records=contract['records'];planned={r['id'] for r in records}
    if len(planned)!=len(records):raise ValueError('Duplicate new IDs')
    added=[];groups={}
    for item in records:
        path=Path(item['path']);record=api.pointer(read(path),item['pointer']);ident=record['id'];value=record['value']
        if api.digest(record)!=item['recordSha256']:raise ValueError('Record changed')
        if ident in b.nodes:raise ValueError('Append-only identity conflict: '+ident)
        group=item['group'];aid='acceptance:process-resources:'+group
        if group not in groups:
            b.node(aid,'review_acceptance',status=status,scope=group,origin=api.origin(P/'contract.json','/groups/'+str(len(groups)),group));groups[group]=aid
        bindings=[{**role,'conditional':True} for role in record.get('sourceBindings',[])]
        b.node(ident,record['kind'],action=record['action'],scope=group,status=status,conditional=True,sourceComplete=False,runtimeTested=False,recordFormat='process_resources_decision_connection',roleBindings=bindings,origin=api.origin(path,item['pointer']+'/value',value),evidenceContext={'recordDirectory':api.rel(path.parent),'fixedCodeRoot':api.rel(api.WORK/'게임원본-상호작용/code')})
        b.edge(ident,'accepted_by',aid)
        for field,valuepart in value.items():
            kind=KINDS.get(field,'evidence');fid=ident+'/'+kind+'/'+field
            b.node(fid,kind,field=field,value=valuepart,origin=api.origin(path,item['pointer']+'/value/'+api.escape(field),valuepart));b.edge(ident,'has_'+kind,fid)
        for role in bindings:
            sid='source:'+role['sourceKey'];source=b.nodes[sid]
            if (source['objectId'],source['variation'])!=(role['objectId'],role['variation']):raise ValueError('Source identity mismatch')
            b.edge(sid,role['role'],ident)
        for link in record.get('links',[]):
            start=link.get('from',ident);target=link['to']
            if start not in b.nodes and start not in planned:raise ValueError('Unknown start: '+start)
            if target not in b.nodes and target not in planned:raise ValueError('Unknown target: '+target)
            b.edge(start,link['relation'],target)
            # A reusable consumer exposes the supplement when entered from the
            # existing action. This relation means supplemental evidence only.
            if start==ident and target not in planned:b.edge(target,'has_process_resources_supplement',ident)
        added.append(ident)
    for s,r,t in b.edges:
        if s not in b.nodes or t not in b.nodes:raise ValueError('Dangling edge')
    coverage={**base['coverage.json'],'acceptedBatches':260+(len(groups) if status=='independent_pass' else 0),'completionCases':sum(n['kind']=='action_completion' for n in b.nodes.values()),'processResourcesCompletionGroups':len(groups),'processResourcesAddedRecords':len(added),'processResourcesCompletionStatus':status,'wholeScopeComplete':False,'runtimeTested':False,'nodeKinds':dict(sorted(Counter(n['kind'] for n in b.nodes.values()).items())),'relations':dict(sorted(Counter(r for _,r,_ in b.edges).items()))}
    for f in b.inputs.values():f['roles'].sort()
    manifest={**base['manifest.json'],'files':sorted(b.inputs.values(),key=lambda f:f['path']),'historicalPublicationAliases':base['manifest.json'].get('historicalPublicationAliases',[])+aliases,'processResourcesCompletion':{'contract':api.rel(P/'contract.json'),'addedRecordIds':added,'status':status,'wholeScopeComplete':False,'runtimeTested':False}}
    return {'graph.json':{**base['graph.json'],'nodes':dict(sorted(b.nodes.items())),'edges':[list(e) for e in sorted(b.edges)],'coverage':coverage},'coverage.json':coverage,'manifest.json':manifest}

def values():
    raw=build_history();graph=view.project(raw['graph.json'])
    manifest={**raw['manifest.json'],'view':'behavior_decision','historyGraphDirectory':str(P/'history-current'),'publication':{'acceptedBatches':graph['coverage']['acceptedBatches'],'completionCases':graph['coverage']['completionCases'],'wholeScopeComplete':False,'runtimeTested':False,'scope':'ProcessResources recipe activation, material move, automation timer and output chain over preserved accepted260; no runtime success inferred.'}}
    return {'graph.json':graph,'coverage.json':graph['coverage'],'manifest.json':manifest}

def check(directory):
    first=values();second=values()
    hashes={n:hashlib.sha256(api.encode(first[n])).hexdigest() for n in NAMES}
    if any(api.encode(first[n])!=api.encode(second[n]) for n in NAMES):raise ValueError('Non-deterministic generator')
    if hashes!={n:sha(directory/n) for n in NAMES}:raise ValueError('Publication differs from exact rebuild')
    for f in first['manifest.json']['files']:
        path=G/f['path']
        if sha(path)!=f['sha256'] or path.stat().st_size!=f['bytes']:raise ValueError('Physical pin changed: '+f['path'])
    result={'status':'pass','twoRebuildsMatchPublished':True,'physicalManifestFiles':len(first['manifest.json']['files']),'hashes':hashes,'coverage':first['coverage.json']}
    (P/'check-published.json').write_bytes(api.encode(result));print(json.dumps(result,ensure_ascii=False));return 0

def main():
    parser=argparse.ArgumentParser(add_help=False);parser.add_argument('--graph-dir',type=Path);parser.add_argument('--output-dir',type=Path);parser.add_argument('--include-history',action='store_true');a,rest=parser.parse_known_args()
    if rest==['build']:
        if not a.output_dir:raise ValueError('Explicit empty output required')
        print(json.dumps({'status':'built','hashes':write(values(),a.output_dir)}));return 0
    if rest==['check']:return check(a.graph_dir or G/'published')
    directory=a.graph_dir or (P/'history-current' if a.include_history else G/'published');graph=read(directory/'graph.json')
    if a.include_history and 'decisionView' in graph:raise ValueError('Explicit history needs raw graph')
    view.install();original=api.source_summary
    def summary(graph,ident,out):
        result=original(graph,ident,out)
        for entry in result.get('relatedActions',[]):
            statuses=[graph['nodes'][t].get('status') for r,t in out[entry['id']] if r=='accepted_by'];entry['reviewStatus']='independent_pass' if 'independent_pass' in statuses else ('pending_independent_review' if statuses else 'unreviewed')
        result['relatedGapAssessments']=[{'id':t,'relation':r,'status':graph['nodes'][t].get('status',graph['nodes'][t].get('value',{}).get('status','historical_assessment'))} for r,t in out[ident] if graph['nodes'][t]['kind']=='gap_assessment']
        result['relatedRecordedEvidence']=[{'id':t,'relation':r,'kind':graph['nodes'][t]['kind'],'status':graph['nodes'][t]['status'],'conditional':True,'sourceComplete':False,'scope':graph['nodes'][t]['scope']} for r,t in out[ident] if graph['nodes'][t].get('recordFormat')=='scoped_additional_record' and graph['nodes'][t]['kind'] in {'configuration_consumer','conditional_relation','evidence','runtime_role_endpoint'}]
        def decision_evidence(relation,target):
            node=graph['nodes'][target]
            decision={graph['nodes'][child]['field']:graph['nodes'][child]['value'] for field_relation,child in out[target] if field_relation.startswith('has_') and child.startswith(target+'/') and graph['nodes'][child].get('field') in {'summary','conditions','costs','flow','results','failures','boundary'}}
            return {'id':target,'relation':relation,'kind':node['kind'],'action':node['action'],'reviewStatus':node['status'],'conditional':True,'sourceComplete':False,'runtimeTested':False,'decision':decision,'nextQuery':['show '+target,'source '+target]}
        result['essentialDecisionEvidence']=[decision_evidence(r,t) for r,t in out[ident] if graph['nodes'][t].get('recordFormat') in {'essential_decision_connection','movement_sleep_decision_connection','process_resources_decision_connection'}]
        return result
    api.source_summary=summary;api.load_graph=lambda:graph;sys.argv=[sys.argv[0],*rest];return api.main()
if __name__=='__main__':
    try:sys.exit(main())
    except (KeyError,ValueError,OSError,AssertionError) as exc:print('ERROR: '+str(exc),file=sys.stderr);sys.exit(1)
