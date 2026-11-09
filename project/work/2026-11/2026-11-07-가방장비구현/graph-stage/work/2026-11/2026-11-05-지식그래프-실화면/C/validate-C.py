"""Read-only reference checks; writes only C/validation.json."""
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
BASE = HERE.parent
OLD = BASE.parent / '2026-11-05-지식그래프-개편'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

overlay = read(HERE / 'overlay-source.json')
a2 = read(BASE / 'A2/manifest.json')
b = read(BASE / 'B/existing-node-mapping.json')
old_c = read(OLD / 'C/graph-C.json')
screens = {s['id'] for s in a2['screens']}
elements = {e['id'] for s in a2['screens'] for e in s['elements']}
features = {n['id'] for n in b['features']}
new_ids = {n['id'] for n in overlay['nodes']}
known = screens | elements | features | new_ids
errors = []
seen = set()
for edge in overlay['edges']:
    key = (edge['source'], edge['type'], edge['target'])
    if key in seen:
        errors.append(f'duplicate edge: {key}')
    seen.add(key)
    for end in ('source', 'target'):
        if edge[end] not in known:
            errors.append(f'unknown {end}: {edge[end]}')
    if not (BASE / edge['evidence']).is_file():
        errors.append(f'missing evidence: {edge["evidence"]}')
old_ids = {n['id'] for n in old_c['nodes']}
for note in overlay['existingNodeAnnotations']:
    if note['id'] not in old_ids:
        errors.append(f'unknown old node: {note["id"]}')
for note in overlay['screenAnnotations']:
    if note['id'] not in screens:
        errors.append(f'unknown screen: {note["id"]}')
for path in overlay['evidenceReferences']:
    if not (BASE / path).is_file():
        errors.append(f'missing evidence input: {path}')
activity_text = (HERE / '활동-동료확인.md').read_text(encoding='utf-8')
for edge in overlay['edges']:
    if edge['type'] == '유저 활동→화면요소' and edge['target'] not in activity_text:
        errors.append(f'activity element absent from prose: {edge["target"]}')
report = {
    'status': 'pass' if not errors else 'fail',
    'errors': errors,
    'nodes': len(overlay['nodes']),
    'edges': len(overlay['edges']),
    'activityElements': sum(e['type'] == '유저 활동→화면요소' for e in overlay['edges']),
    'functionMappings': sum(e['type'] != '유저 활동→화면요소' for e in overlay['edges']),
    'overlaySha256': hashlib.sha256((HERE / 'overlay-source.json').read_bytes()).hexdigest(),
    'limit': 'Reference validation only; not runtime trace. C3 actual result and B version boundaries remain separate.'
}
(HERE / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps(report, ensure_ascii=False))
raise SystemExit(bool(errors))
