"""Run the frozen generator and retain its raw history from the same build."""
from pathlib import Path
import json
import overlay as gen
R=Path(__file__).resolve().parent
receipt=gen.read(R/'review-acceptance.json')
if receipt['status']!='independent_pass':raise ValueError('Exact independent acceptance required')
original=gen.build_history;captured=[]
def capture():
    value=original();captured.append(value);return value
gen.build_history=capture
try:publication=gen.values()
finally:gen.build_history=original
if len(captured)!=1:raise ValueError('Expected one exact generator call')
raw=captured[0];published_hashes=gen.write(publication,R/'candidate-final');history_hashes=gen.write(raw,R/'history-current')
out={'status':'generated','contractSha256':gen.sha(R/'contract.json'),'receiptSha256':gen.sha(R/'review-acceptance.json'),'publication':published_hashes,'history':history_hashes,'coverage':publication['coverage.json']}
(R/'final-generation.json').write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in out.items() if k!='coverage'},ensure_ascii=False))
