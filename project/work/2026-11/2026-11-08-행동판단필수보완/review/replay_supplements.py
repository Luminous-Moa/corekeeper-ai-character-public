"""Run the pinned supplier rules with only their output directory redirected."""
from pathlib import Path
import ast, hashlib, importlib.util,json
P=Path(__file__).resolve().parents[1];O=P/'review/replayed';O.mkdir(exist_ok=True)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
four=P/'four/produce.py';spec=importlib.util.spec_from_file_location('review_four_producer',four);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.O=O/'four';m.O.mkdir(exist_ok=True);m.generate()
six=P/'six/build_six.py';tree=ast.parse(six.read_text(encoding='utf-8-sig'));rewritten=0
for statement in tree.body:
 if isinstance(statement,ast.Assign) and any(isinstance(t,ast.Name) and t.id=='O' for t in statement.targets):
  statement.value=ast.Call(func=ast.Attribute(value=ast.Name(id='pathlib',ctx=ast.Load()),attr='Path',ctx=ast.Load()),args=[ast.Constant(str(O/'six'))],keywords=[]);rewritten+=1
assert rewritten==1
ast.fix_missing_locations(tree);exec(compile(tree,str(six),'exec'),{'__file__':str(six),'__name__':'review_six_producer'})
results=[]
for folder,script in [('four',four),('six',six)]:
 expected=sha(P/folder/'supplement.json');actual=sha(O/folder/'supplement.json');assert actual==expected,(folder,actual,expected)
 results.append({'supplier':folder,'pinnedGeneratorSha256':sha(script),'actualReplayedSupplementSha256':actual,'matchesExactSupplierSupplement':True,'transformation':'Output directory only; __file__, source input paths, rules and payloads unchanged.'})
(P/'review/supplement-replay.json').write_text(json.dumps({'status':'pass','suppliers':results},indent=2)+'\n',encoding='utf-8');print(json.dumps(results))
