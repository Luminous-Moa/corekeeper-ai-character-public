"""Representative real CLI runs; does not edit graph inputs or production output."""
import hashlib
import json
from pathlib import Path
import subprocess
import sys

HERE = Path(__file__).resolve().parent
WORK = HERE.parents[2]
ROOT = WORK / '게임행동-그래프'
TOOL = ROOT / 'graph.py'

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

graph = json.loads((ROOT / 'graph.json').read_text(encoding='utf8'))
nodes = graph['nodes']
claims = [n for n in nodes.values() if n['kind'] == 'claim']
sources = {n['sourceKey']: n for n in nodes.values() if n['kind'] == 'source'}
def pick(dataset, object_id, action=None):
    return next(n for n in claims if n['dataset'] == dataset and sources[n['sourceKey']]['objectId'] == object_id and (action is None or n['action'] == action))

before = {name: digest(ROOT / name) for name in ('graph.py', 'graph.json', 'coverage.json', 'manifest.json')}
runs = []
def call(name, *args):
    result = subprocess.run([sys.executable, '-X', 'utf8', str(TOOL), *args], capture_output=True, text=True, encoding='utf8')
    (HERE / (name + '.json')).write_text(result.stdout, encoding='utf8')
    if result.returncode:
        raise RuntimeError(result.stderr)
    value = json.loads(result.stdout)
    runs.append({'name': name, 'arguments': args, 'exitCode': result.returncode, 'outputSha256': digest(HERE / (name + '.json'))})
    return value

summary = call('overview', 'overview')
assert summary['sources'] == 3235 and summary['initialClaims'] == 3499 and summary['baseClaims'] == 1432 and summary['suppClaims'] == 949
chest = pick('initial', 100, 'open_inventory')
cs = call('chest-target', 'target', chest['sourceKey'])
assert cs['total'] == 1 and any(n['id'] == chest['id'] for n in cs['sources'][0]['claims'])
cv = call('chest-claim', 'show', chest['id'])
assert any(n['kind'] == 'condition' for n in cv['fields']) and any(n['kind'] == 'result' for n in cv['fields'])
ce = next(n for n in cv['fields'] if n['field'] == 'codeEvidence')
source = call('chest-code-source', 'source', ce['id'])
assert source['value']
repair = pick('supp', 1, 'repair')
rv = call('repair-supplement', 'show', repair['id'])
assert any(r['relation'] == 'governed_by_prior_rule' and any(f['kind'] == 'result' for f in r['fields']) for r in rv['rules'])
assert any(r['relation'] == 'supplements' for r in rv['relations'])
plate = pick('supp', 6510)
pv = call('pressure-plate', 'show', plate['id'])
# Pick the explicit C input -> A propagation edge, not a name-based inferred causal link.
edge = next(e for e in graph['edges'] if e[1] == 'declared_input_for' and nodes[e[0]].get('sourceKey') in sources and sources[nodes[e[0]]['sourceKey']]['objectId'] == 6510)
path = call('plate-to-power', 'path', edge[0], edge[2])
assert path['found'] and path['edges'] == [edge]
drow = next(n for n in claims if n['dataset'] == 'supp' and n['id'].startswith('supp:D:') and n['action'] == 'automatic_recipe_result')
dv = call('automatic-rule-result', 'show', drow['id'])
assert any(any(f['kind'] == 'condition' for f in r['fields']) and any(f['kind'] == 'result' for f in r['fields']) for r in dv['rules'])
cook = call('cooking-ordered', 'cooking', '1645', '1646')
assert cook['total'] == 1 and cook['pairs'][0]['value']['inputObjectIds'] == [1645, 1646]
g6506 = call('gap-6506', 'gaps', '6506')
g4600 = call('gap-4600', 'gaps', '4600')
assert g6506['total'] and g4600['total']
unknown = call('unknown-2406', 'target', '2406')
assert any(n['coverageDispositions'] for n in unknown['sources'])
absent = call('absent-4600', 'target', '4600')
assert absent['total'] == 0 and absent['noSourceMeaning'] and absent['relatedGapNodes']
exact = call('exact-id-1', 'target', '1')
assert exact['sources'] and all(x['objectId'] == 1 for x in exact['sources'])
check = call('check', 'check')
assert check['status'] == 'pass'
after = {name: digest(ROOT / name) for name in before}
assert before == after
(HERE / 'summary.json').write_text(json.dumps({'status': 'pass', 'runs': runs, 'unchangedQueryArtifacts': before, 'coverage': summary}, ensure_ascii=False, indent=2), encoding='utf8')
print(json.dumps({'status': 'pass', 'cliRuns': len(runs), 'output': str(HERE / 'summary.json')}, ensure_ascii=False))
