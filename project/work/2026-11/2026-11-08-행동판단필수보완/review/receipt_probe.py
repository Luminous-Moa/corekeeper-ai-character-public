"""Exercise the production receipt gate against isolated review-only fixtures."""
from pathlib import Path
import hashlib, importlib.util, json
P=Path(__file__).resolve().parents[1]; O=P/'review'/'receipt-fixtures';O.mkdir(exist_ok=True)
s=importlib.util.spec_from_file_location('essential_review_gate',P/'overlay.py');m=importlib.util.module_from_spec(s);s.loader.exec_module(m)
def put(p,v):p.write_text(json.dumps(v),encoding='utf-8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
put(O/'contract.json',{'pins':[]});contract=sha(O/'contract.json');put(O/'report.md',{'text':'fixture'})
def fixture(fs='independent_pass',fc=None,rs='independent_pass',rc=None,broken_report=False,broken_findings=False):
 put(O/'findings.json',{'status':fs,'reviewedContractSha256':fc or contract,'report':{'path':str(O/'report.md'),'sha256':'0'*64 if broken_report else sha(O/'report.md')}})
 put(O/'review-acceptance.json',{'status':rs,'reviewedContractSha256':rc or contract,'review':{'path':str(O/'findings.json'),'sha256':'0'*64 if broken_findings else sha(O/'findings.json')}})
m.P=O
def reached():raise RuntimeError('REACHED_BASE')
original_read=m.read
def guarded_read(p):
 if Path(p).parent==(m.R/'history-current'):return reached()
 return original_read(p)
m.read=guarded_read
results=[]
for name,args,expected in [('valid',{},'REACHED_BASE'),('fail-findings',{'fs':'fail'},'ValueError'),('pending-findings',{'fs':'pending_independent_review'},'ValueError'),('wrong-findings-contract',{'fc':'a'*64},'ValueError'),('pending-receipt',{'rs':'pending_independent_review'},'ValueError'),('wrong-receipt-contract',{'rc':'b'*64},'ValueError'),('changed-report',{'broken_report':True},'ValueError'),('changed-findings',{'broken_findings':True},'ValueError')]:
 fixture(**args)
 try:m.build_history();actual='UNEXPECTED_SUCCESS'
 except ValueError as e:actual='ValueError'
 except RuntimeError as e:actual=str(e)
 assert actual==expected,(name,actual,expected)
 results.append({'case':name,'result':actual})
(P/'review'/'receipt-probe.json').write_text(json.dumps({'status':'pass','generatorSha256':sha(P/'overlay.py'),'cases':results},indent=2)+'\n',encoding='utf-8');print(json.dumps(results))
