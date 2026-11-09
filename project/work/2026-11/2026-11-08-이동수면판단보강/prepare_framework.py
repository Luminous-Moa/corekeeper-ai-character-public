"""Reuse the accepted256 publication framework for four bounded continuations."""
from pathlib import Path
import json,shutil
P=Path(__file__).resolve().parent
R=P.parent/'2026-11-08-행동판단필수보완'
for name in ('overlay.py','compile_contract.py','verify.py','install_reviewed.py','finalize_reviewed.py','check_published_inputs.py','query_published.py'):
    text=(R/name).read_text(encoding='utf-8')
    if name=='overlay.py':
        text=text.replace("R=P.parent/'2026-11-08-행동그래프추가기록'","R=P.parent/'2026-11-08-행동판단필수보완'")
        text=text.replace('view=previous.previous.previous.previous.view','view=previous.view')
        text=text.replace('246','256')
        text=text.replace('essential_reviewed_input','movement_sleep_reviewed_input').replace('essential_exact_review_contract','movement_sleep_exact_review_contract').replace('essential_independent_','movement_sleep_independent_')
        text=text.replace("aid='acceptance:essential:'","aid='acceptance:movement-sleep:'")
        text=text.replace("recordFormat='essential_decision_connection'","recordFormat='movement_sleep_decision_connection'")
        text=text.replace("'has_essential_supplement'","'has_movement_sleep_supplement'")
        text=text.replace('essentialCompletion','movementSleepCompletion').replace('essentialAddedRecords','movementSleepAddedRecords')
        text=text.replace("graph['nodes'][t].get('recordFormat')=='essential_decision_connection'","graph['nodes'][t].get('recordFormat') in {'essential_decision_connection','movement_sleep_decision_connection'}")
        text=text.replace('for _,child in out[target] if',"for field_relation,child in out[target] if field_relation.startswith('has_') and child.startswith(target+'/') and")
        text=text.replace('Decision-relevant connections for six recorded candidates and four bounded static lifecycle gaps; all prior256 content preserved.','Four bounded movement and condition-sleep continuations over preserved accepted256; no runtime success inferred.')
        text=text.replace('six recorded candidates and four bounded static lifecycle gaps','four movement and condition-sleep continuations')
    elif name=='compile_contract.py':
        text=text.replace('ten bounded supplements','four scoped continuations')
        text=text.replace("for folder in ('six','four'):","for folder in ('production',):")
        text=text.replace('246','256')
        text=text.replace("P.parent/'2026-11-08-행동그래프추가기록/history-current'","P.parent/'2026-11-08-행동판단필수보완/history-current'")
        text=text.replace("record['id'].split('essential:',1)[1]","record['id'].split('movement-sleep:',1)[1]")
    elif name in ('verify.py','install_reviewed.py','check_published_inputs.py'):
        text=text.replace('246','256')
        if name=='install_reviewed.py':text=text.replace('pending-essential','pending-movement-sleep')
        if name=='verify.py':text=text.replace("for _,t in [(r,t) for s,r,t in edges if s==ident] if nodes[t].get('field')==key","for r,t in [(r,t) for s,r,t in edges if s==ident] if r.startswith('has_') and t.startswith(ident+'/') and nodes[t].get('field')==key")
    (P/name).write_text(text,encoding='utf-8',newline='\n')
config={'schemaVersion':1,'defaultView':'published','generator':'../2026-11/2026-11-08-이동수면판단보강/overlay.py','preservedAcceptedBatches':256,'historyGraphDirectory':str(P/'history-current'),'wholeScopeComplete':False,'runtimeTested':False}
(P/'publication.config.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
