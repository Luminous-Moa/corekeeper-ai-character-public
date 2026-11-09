"""Read-only source/installed-script comparison. Writes only this report directory.
Uses existing graph node ranges; does not extract or alter a product graph.
"""
import json, hashlib, subprocess, pathlib, datetime
OUT = pathlib.Path(__file__).parent
OLD = OUT.parents[1] / '2026-11-05-지식그래프-개편' / 'B'
REPO = pathlib.Path('C:/Dev/CoreKeeperModSDK-combat-s2')
INSTALL = pathlib.Path('C:/Program Files (x86)/Steam/steamapps/common/Core Keeper/CoreKeeper_Data/StreamingAssets/Mods/AIPlayerSpike')
BASE = 'e82eb87f25641cc7c31e6216f64e20a34e58d0c0'
def git(*args):
    return subprocess.check_output(['git', '-C', str(REPO), *args])
RUN = git('rev-parse','76563bf').decode().strip()
HEAD = git('rev-parse','HEAD').decode().strip()
def sha(b): return hashlib.sha256(b).hexdigest()
def norm(b): return b.replace(b'\r\n', b'\n')
def blob(c,p): return git('show', c+':'+p)
def write(name,x): (OUT/name).write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
g = json.loads((OLD/'code.graph.json').read_text(encoding='utf-8'))
files = sorted({n['file'] for n in g['nodes'] if n['type']=='코드' and n.get('file')})
comparisons=[]
for f in files:
    a,b=blob(BASE,f),blob(RUN,f)
    comparisons.append({'file':f,'baseSha256':sha(a),'runSha256':sha(b),'sameGitBytes':a==b,'snapshotMatchesBase':(OLD/'snapshot'/f).read_bytes()==a,'runCheckoutNormalizedEqualsCommit':norm((REPO/f).read_bytes())==norm(b)})
installed=[]
for p in sorted(INSTALL.rglob('*.cs')):
    rel=p.relative_to(INSTALL/'Scripts'/'Scripts').as_posix()
    f='Assets/AIPlayerSpike/Scripts/'+rel
    a,b=p.read_bytes(),blob(RUN,f)
    installed.append({'installed':str(p),'source':f,'installedSha256':sha(a),'runCommitSha256':sha(b),'normalizedSha256':sha(norm(a)),'equalRaw':a==b,'equalNormalized':norm(a)==norm(b),'lastWriteUtc':datetime.datetime.fromtimestamp(p.stat().st_mtime,datetime.timezone.utc).isoformat()})
manifest=json.loads((INSTALL/'ModManifest.json').read_text(encoding='utf-8-sig'))
logpath=REPO/'Logs/headless-build-R3c.log'
log=logpath.read_text(encoding='utf-8-sig')
marker=log.index('[HeadlessSdk] BuildAndInstall done:')
loggedManifest,_=json.JSONDecoder().raw_decode(log[log.index('{',marker):])
write('version-evidence.json',{'collectedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'baseCommit':BASE,'runCandidateCommit':RUN,'combatCheckoutHead':HEAD,'productDiffRunToHead':git('diff','--name-only',RUN,HEAD,'--','Assets/AIPlayerSpike').decode().splitlines(),'graphSha256':sha((OLD/'code.graph.json').read_bytes()),'analysisFiles':comparisons,'installedScripts':installed,'manifestSha256':sha((INSTALL/'ModManifest.json').read_bytes()),'manifestFileCount':len(manifest['files']),'manifestEqualsR3cBuildLog':manifest==loggedManifest,'buildLog':str(logpath),'buildLogSha256':sha(logpath.read_bytes()),'limitations':['Installed disk script identity does not prove loaded in-memory assembly identity.','Comparison normalizes CRLF to LF separately; raw hashes retained.','Current runtime world, settings and character values were not read.']})
selectedNames={
'TryHandleChatCommand','Dispatch','IsAiCommand','DefaultAgent','IsSkillCommand','IsTestCommand','TestCommands','TestCommandsOn','TestSwitchPath','HandleCommand','StartSkill','StatusAll','StatusText','StageText','Reply','FlushReplies','ChatSendPrefix','HandleGoalCommand','InventoryCounts','Summary','Ensure','Load','Prepare','CreateNewCharacter','DistinctLook','CreateServerCopy','CharacterDataPostfix','Members','PathFor','FilePath','All','Find','Update','InstallPatches','PlayerSpawned','OnSpawned','ApplyPlayerName','InitPlayer','Settings'
}
rows=[]
for n in g['nodes']:
    if n['type']!='코드' or n.get('name') not in selectedNames: continue
    if n.get('kind') not in ('MethodDeclaration','VariableDeclarator','PropertyDeclaration'): continue
    if not n.get('file','').endswith('.cs'): continue
    a=blob(BASE,n['file']).decode('utf-8'); b=blob(RUN,n['file']).decode('utf-8')
    part='\n'.join(a.splitlines()[n['lineStart']-1:n['lineEnd']])
    hits=[]; pos=0
    while True:
        idx=b.find(part,pos)
        if idx<0: break
        hits.append(b[:idx].count('\n')+1);pos=idx+len(part)
    rows.append({'id':n['id'],'file':n['file'],'baseLines':[n['lineStart'],n['lineEnd']],'sourceTextSha256':sha(part.encode()),'exactSegmentMatchesInRun':hits,'runLines':[hits[0],hits[0]+n['lineEnd']-n['lineStart']] if len(hits)==1 else None,'sameSourceSegment':len(hits)==1})
# Explicit inventory output and name-setting method (selected by existing AST range).
for n in g['nodes']:
    if n.get('signature','').startswith('InvSkill.Start(') or (n.get('file','').endswith('AIPlayerSpikeMod.cs') and n.get('kind')=='MethodDeclaration' and n.get('lineStart',99999)<=1090<=n.get('lineEnd',0)):
        a=blob(BASE,n['file']).decode();b=blob(RUN,n['file']).decode();part='\n'.join(a.splitlines()[n['lineStart']-1:n['lineEnd']]);idx=b.find(part);line=b[:idx].count('\n')+1 if idx>=0 else None
        rows.append({'id':n['id'],'file':n['file'],'baseLines':[n['lineStart'],n['lineEnd']],'sourceTextSha256':sha(part.encode()),'runLines':[line,line+n['lineEnd']-n['lineStart']] if line else None,'sameSourceSegment':idx>=0})
ids={r['id'] for r in rows}
features=[n for n in g['nodes'] if n['type']=='기능' and (n['id']=='slash:ai' or 'AIPlayerSpike' in n['id'])]
edges=[e for e in g['edges'] if (e['from'] in ids and (e['to'] in ids or e['type'] in ('read','write'))) or (e['type']=='기능→코드' and e['to'] in ids)]
dataids={e['to'] for e in edges if e['type'] in ('read','write')}
write('existing-node-mapping.json',{'authority':'Existing machine IDs and AST ranges reused; exact source segment comparison only. No new graph nodes or inferred runtime edges.','baseCommit':BASE,'runCandidateCommit':RUN,'nodes':rows,'features':features,'existingEdges':edges,'dataNodes':[n for n in g['nodes'] if n['id'] in dataids]})
print(json.dumps({'analysisFiles':len(comparisons),'unchangedFiles':sum(x['sameGitBytes'] for x in comparisons),'installedScripts':len(installed),'installedRawMatches':sum(x['equalRaw'] for x in installed),'installedNormalizedMatches':sum(x['equalNormalized'] for x in installed),'manifestEqualsBuild':manifest==loggedManifest,'mappedNodes':len(rows),'sameSegments':sum(x['sameSourceSegment'] for x in rows)},ensure_ascii=False))
