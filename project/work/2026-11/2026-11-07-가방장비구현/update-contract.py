import json
from pathlib import Path
import uuid

root = Path('C:/Dev/CoreKeeperModSDK-bag-equipment')
p = root / 'Tools/ai-bridge/test/fixtures/goal-contract.json'
d = json.loads(p.read_text(encoding='utf-8'))
for wire, kind, korean in [('equip','Equip','장착'),('unequip','Unequip','벗기'),('hold','Hold','들기'),('take','Take','꺼내기'),('drop','Drop','내려놓기'),('give','Give','건네기')]:
    if wire not in d['wireNames']: d['wireNames'].append(wire)
    d['hostWords'][kind] = [korean, wire]
cases = [
 ('equip exact slot', {'do':'equip','item':'CopperHelm','where':'head','slot':12}, None),
 ('unequip named gear slot', {'do':'unequip','where':'head'}, None),
 ('hold specific sword', {'do':'hold','item':'CopperSword','slot':13,'count':1}, None),
 ('reserve wood when storing', {'do':'store','item':'Wood','keep':10,'where':'chest:1,2'}, None),
 ('take partial stack', {'do':'take','item':'Wood','count':20}, None),
 ('drop partial stack', {'do':'drop','item':'Wood','count':2}, None),
 ('give to visible named player', {'do':'give','item':'Wood','count':2,'where':'Gemini'}, None),
 ('give requires recipient', {'do':'give','item':'Wood'}, 'give needs recipient in where'),
 ('inventory rejects negative slot', {'do':'hold','item':'CopperSword','slot':-1}, 'inventory slot must be an integer in 0..9999'),
 ('inventory rejects fractional reserve', {'do':'store','item':'Wood','keep':2.5}, 'inventory keep must be an integer in 0..9999'),
 ('equipment rejects multiple count', {'do':'equip','item':'CopperHelm','count':2}, 'equipment action count must be 1'),
 ('transfer rejects zero count', {'do':'take','item':'Wood','count':0}, 'transfer count must be positive'),
]
names={c['name'] for c in d['cases']}
for name,goal,error in cases:
    if name not in names: d['cases'].append({'name':name,'goal':goal,'bridgeError':error})
p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
for source in [root/'Assets/AIPlayerSpike/Scripts/Core/AIInventoryGoalForm.cs']:
    meta=Path(str(source)+'.meta')
    if not meta.exists(): meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n',encoding='utf-8')
