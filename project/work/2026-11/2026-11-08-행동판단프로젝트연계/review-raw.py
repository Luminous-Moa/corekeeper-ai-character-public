import json,hashlib,pathlib
folder=pathlib.Path(__file__).resolve().parent
game=folder.parent.parent/'게임행동-그래프'
rows=[]
def load(p):return json.loads(p.read_text(encoding='utf-8-sig'),parse_constant=lambda x:{'floatSpecial':{'Infinity':'positive_infinity','-Infinity':'negative_infinity','NaN':'not_a_number'}[x]})
for f in sorted((folder/'review-evidence').glob('R.game-topic.*-source.stdout.txt')):
    response=load(f);origin=response['origin'];p=(game/origin['path']).resolve();raw=load(p)
    for part in origin['pointer'].split('/')[1:] if origin['pointer'] else []:
        part=part.replace('~1','/').replace('~0','~');raw=raw[int(part)] if isinstance(raw,list) else raw[part]
    digest=hashlib.sha256((json.dumps(raw,ensure_ascii=False,sort_keys=True,indent=2,allow_nan=False)+'\n').encode('utf-8')).hexdigest()
    assert digest==origin['normalizedValueSha256'],str(f)
    assert raw==response['value'],str(f)
    rows.append({'topic':f.name.split('-source.')[0],'origin':origin,'actualPath':str(p),'rawValueMatchesResponse':True,'hashMatches':True})
assert len(rows)==22,len(rows)
(folder/'review-raw.json').write_text(json.dumps({'passed':True,'checked':len(rows),'rows':rows},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('22 direct raw JSON pointers and independently normalized hashes match')
