"""Join two accepted generation executions with actual published physical pins."""
from pathlib import Path
import json
import overlay as gen
P=gen.P;G=gen.G
generation=gen.read(P/'final-generation.json');installation=gen.read(P/'installation.json')
assert generation['status']=='generated' and installation['status']=='installed'
assert generation['contractSha256']==gen.sha(P/'contract.json')
assert generation['receiptSha256']==gen.sha(P/'review-acceptance.json')
hashes={name:gen.sha(G/'published'/name) for name in gen.NAMES}
assert generation['publication']==installation['hashes']==hashes
assert {name:gen.sha(P/'candidate-final'/name) for name in gen.NAMES}==hashes
assert installation['configSha256']==gen.sha(G/'publication.json')==gen.sha(P/'publication.config.json')
for name in gen.NAMES:assert gen.sha(P/'published-before-switch'/name)==gen.sha(P/'baseline-260'/name)
manifest=gen.read(G/'published/manifest.json');checked=0
for pin in manifest['files']:
    path=(G/pin['path']).resolve()
    assert gen.sha(path)==pin['sha256'] and path.stat().st_size==pin['bytes'],str(path)
    checked+=1
out={'status':'pass','twoAcceptedRebuildsMatchPublished':True,'rebuildExecutions':[{'evidence':str(P/'final-generation.json'),'method':'finalize_reviewed.py invokes gen.values once and captures its exact raw history','sha256':gen.sha(P/'final-generation.json')},{'evidence':str(P/'installation.json'),'method':'install_reviewed.py invokes gen.values again and asserts each candidate SHA against each rebuilt canonical encoding before installation','sha256':gen.sha(P/'installation.json')}],'contractSha256':generation['contractSha256'],'receiptSha256':generation['receiptSha256'],'physicalManifestFiles':checked,'physicalPublicationConfigChecked':True,'preserved260PhysicalBytes':True,'hashes':hashes,'wholeScopeComplete':False,'runtimeTested':False}
(P/'check-published.json').write_bytes(gen.api.encode(out));print(json.dumps(out,ensure_ascii=False))
