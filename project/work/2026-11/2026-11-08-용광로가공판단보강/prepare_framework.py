"""Reuse the accepted260 publication framework for the ProcessResources chain."""
from pathlib import Path
import json,re
P=Path(__file__).resolve().parent
R=P.parent/'2026-11-08-이동수면판단보강'
for name in ('overlay.py','compile_contract.py','verify.py','install_reviewed.py','finalize_reviewed.py','check_published_inputs.py','query_published.py'):
    text=(R/name).read_text(encoding='utf-8')
    if name=='overlay.py':
        text=text.replace('"""Append reviewed decision-critical static connections over accepted256."""','"""Append the reviewed ProcessResources processing chain over accepted260."""')
        text=text.replace("R=P.parent/'2026-11-08-행동판단필수보완'","R=P.parent/'2026-11-08-이동수면판단보강'")
        text=text.replace("spec_from_file_location('accepted256'","spec_from_file_location('accepted260'")
        # Scope text first: the generic accepted256 rename below would otherwise make it unmatched (review F3).
        scope='Four bounded movement and condition-sleep continuations over preserved accepted256; no runtime success inferred.'
        if text.count(scope)!=1:raise ValueError('Scope text not found')
        text=text.replace(scope,'ProcessResources recipe activation, material move, automation timer and output chain over preserved accepted260; no runtime success inferred.')
        text=text.replace('Accepted256','Accepted260').replace('accepted256','accepted260').replace('Prior accepted256','Prior accepted260')
        text=text.replace("'acceptedBatches':256+","'acceptedBatches':260+")
        text=text.replace('movement_sleep_reviewed_input','process_resources_reviewed_input').replace('movement_sleep_exact_review_contract','process_resources_exact_review_contract').replace('movement_sleep_independent_','process_resources_independent_')
        text=text.replace("aid='acceptance:movement-sleep:'","aid='acceptance:process-resources:'")
        text=text.replace("recordFormat='movement_sleep_decision_connection'","recordFormat='process_resources_decision_connection'")
        text=text.replace("'has_movement_sleep_supplement'","'has_process_resources_supplement'")
        text=text.replace("'movementSleepCompletionGroups'","'processResourcesCompletionGroups'").replace("'movementSleepAddedRecords'","'processResourcesAddedRecords'").replace("'movementSleepCompletionStatus'","'processResourcesCompletionStatus'").replace("'movementSleepCompletion'","'processResourcesCompletion'")
        text=text.replace("graph['nodes'][t].get('recordFormat') in {'essential_decision_connection','movement_sleep_decision_connection'}","graph['nodes'][t].get('recordFormat') in {'essential_decision_connection','movement_sleep_decision_connection','process_resources_decision_connection'}")
        if 'movement and condition-sleep' in text:raise ValueError('Stale scope text')
        if text.count("view=previous.view")!=1:raise ValueError('Unexpected view binding')
        for needle in ('movement_sleep_reviewed','acceptance:movement-sleep'):
            if needle in text:raise ValueError('Unreplaced token '+needle)
        if re.search(r'(?<![sS]ha)256',text):raise ValueError('Unreplaced 256 in '+name)
    elif name=='compile_contract.py':
        text=text.replace('"""Freeze the four scoped continuations and physical code/evidence references."""','"""Freeze the ProcessResources chain records and physical code/evidence references."""')
        text=text.replace("Mutable published input must use baseline-256 copy","Mutable published input must use baseline-260 copy")
        text=text.replace("record['id'].split('movement-sleep:',1)[1]","record['id'].split('process-resources:',1)[1]")
        text=text.replace("P/'baseline-256'/n","P/'baseline-260'/n").replace("P/'baseline-256/graph.json'","P/'baseline-260/graph.json'")
        text=text.replace("P.parent/'2026-11-08-행동판단필수보완/history-current'/n","P.parent/'2026-11-08-이동수면판단보강/history-current'/n")
        for needle in ('movement-sleep','행동판단필수보완'):
            if needle in text:raise ValueError('Unreplaced token '+needle)
        if re.search(r'(?<![sS]ha)256',text):raise ValueError('Unreplaced 256 in '+name)
    elif name in ('verify.py','install_reviewed.py','check_published_inputs.py','query_published.py','finalize_reviewed.py'):
        text=text.replace('baseline-256','baseline-260').replace('Accepted256','Accepted260').replace('accepted256','accepted260').replace('preserved256','preserved260').replace('preserved256PhysicalBytes','preserved260PhysicalBytes')
        if name=='install_reviewed.py':text=text.replace('pending-movement-sleep','pending-process-resources')
        if re.search(r"(?<![sS]ha)256",text) or "movement-sleep" in text:raise ValueError('Unreplaced token in '+name)
    (P/name).write_text(text,encoding='utf-8',newline='\n')
config={'schemaVersion':1,'defaultView':'published','generator':'../2026-11/2026-11-08-용광로가공판단보강/overlay.py','preservedAcceptedBatches':260,'historyGraphDirectory':str(P/'history-current'),'wholeScopeComplete':False,'runtimeTested':False}
(P/'publication.config.json').write_text(json.dumps(config,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
print('prepared framework')
