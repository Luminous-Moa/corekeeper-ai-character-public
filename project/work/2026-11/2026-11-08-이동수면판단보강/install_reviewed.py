"""Install an exact reviewed candidate while retaining accepted256 unchanged."""
from pathlib import Path
import hashlib,json,os,shutil,sys
import overlay as gen
P=gen.P;G=gen.G
def install(candidate):
    receipt=gen.read(P/'review-acceptance.json')
    if receipt['status']!='independent_pass':raise ValueError('Independent receipt required')
    expected=gen.values()
    if any(gen.sha(candidate/n)!=hashlib.sha256(gen.api.encode(expected[n])).hexdigest() for n in gen.NAMES):raise ValueError('Candidate differs from accepted rebuild')
    destination=(G/'published').resolve();retired=(P/'published-before-switch').resolve();pending=(G/'published.pending-movement-sleep').resolve();config=G/'publication.json'
    if destination.parent!=G.resolve() or not retired.is_relative_to(P.resolve()) or pending.parent!=G.resolve():raise ValueError('Unexpected destination paths')
    if retired.exists() or pending.exists():raise ValueError('Previous install artifacts exist')
    for name in gen.NAMES:
        if gen.sha(destination/name)!=gen.sha(P/'baseline-256'/name):raise ValueError('Accepted256 changed before install')
    if gen.sha(config)!=gen.sha(P/'publication-before.json'):raise ValueError('Config changed before install')
    pending.mkdir()
    for name in gen.NAMES:shutil.copyfile(candidate/name,pending/name)
    if any(gen.sha(pending/n)!=gen.sha(candidate/n) for n in gen.NAMES):raise ValueError('Candidate copy mismatch')
    old=config.read_bytes();os.replace(destination,retired)
    try:
        os.replace(pending,destination)
        tmp=G/'publication.json.pending-movement-sleep';tmp.write_bytes((P/'publication.config.json').read_bytes());os.replace(tmp,config)
    except BaseException:
        if destination.exists():
            failed=P/'failed-publication-preserved'
            if failed.exists():raise ValueError('Rollback preservation already exists')
            os.replace(destination,failed)
        os.replace(retired,destination);config.write_bytes(old);raise
    result={'status':'installed','preserved256':str(retired),'candidate':str(candidate),'hashes':{n:gen.sha(destination/n) for n in gen.NAMES},'configSha256':gen.sha(config),'wholeScopeComplete':False,'runtimeTested':False}
    (P/'installation.json').write_bytes(gen.api.encode(result));print(json.dumps(result,ensure_ascii=False))
if __name__=='__main__':install(Path(sys.argv[1]).resolve())
