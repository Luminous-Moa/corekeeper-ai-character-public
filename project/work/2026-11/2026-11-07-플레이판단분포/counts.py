from collections import Counter, defaultdict
from pathlib import Path
import json,re
from datetime import datetime,timezone,timedelta
out=Path(__file__).parent
base=Path.home()/'AppData/LocalLow/Pugstorm/Core Keeper/AIPlayers'
bridge=Path('C:/Dev/CoreKeeperModSDK/Tools/ai-bridge/logs/plan.jsonl')
def rows(path):
 for l in path.open(encoding='utf-8-sig'):
  if l.strip(): yield json.loads(l)
tot=defaultdict(Counter); files={}; samples={}; ck=[]
def inc(k,x): tot[k][str(x)]+=1
for f in sorted(base.glob('decisions-*.log')):
 d=defaultdict(Counter); pending={}; applied=set()
 for l in f.open(encoding='utf-8-sig',errors='replace'):
  ap=re.match(r'^t=[\d.]+ @(\S+) DECISION #(\d+) (held switch|queued answer|next swing) APPLY\b',l)
  if ap:
   applied.add((ap[1],ap[2]));d['applied'][ap[3]]+=1
  m=re.match(r'^t=([\d.]+) @(\S+) DECISION #(\d+) (INPUT|SEND|ANSWER|RESULT)\b(.*)',l)
  if not m:continue
  t,c,i,stage,tail=m.groups(); key=(c,i); d['stages'][stage]+=1
  if stage=='SEND':
   trg=re.search(r" trigger='([^']+)'",tail)
   if trg and trg[1]=='전투 중 계속 다시 판단':d['triggerFamily'][trg[1]]+=1
  if stage=='INPUT':
   options=tail.rsplit(' options(',1)[-1]
   candidates=dict((id,(kind,act,desc)) for id,kind,act,desc in re.findall(r'\|\| (\S+) \[([^ ]+) ([^\]]+)\] ([^|]+)',options))
   mobs=re.search(r'보이는 범위.*?적 (\d+), 떨어진 아이템',tail)
   optCount=re.search(r'\] options\((\d+)\):',tail)
   pending[key]=(candidates, 'zero' if mobs and mobs[1]=='0' else 'seen' if mobs else 'unknown', int(optCount[1]) if optCount else None)
  if stage=='RESULT':
   outcome=re.search(r'cost=\$\S+\s+(\S+)',tail)
   d['result'][outcome[1] if outcome else '?']+=1
   if outcome and outcome[1]=='APPLY':applied.add(key);d['applied']['immediate']+=1
  if stage=='ANSWER':
   a=re.search(r'choice=(\S+) \[([^\s\]]*)\s*([^\]]*)\]',tail)
   if not a: continue
   id,kind,action=a.groups(); label=action.split(':')[0]
   candidates,seen,nopt=pending.get(key,({},'unknown',None))
   desc=candidates.get(id,('','',''))[2].partition(' — ')[0].strip() or '?'
   d['kind'][kind]+=1;d['action'][label]+=1;d['actor'][c]+=1;d['visibleEnemy'][seen]+=1;d['desc_'+seen][desc]+=1;d['kind_'+seen][kind]+=1;d['action_'+seen][label]+=1
   d['continuedUnderlying' if kind=='continue' else 'newUnderlying'][label]+=1
   if seen=='zero' and kind=='wait': d['zeroWaitOptions'][str(nopt)]+=1
 d['applied']['unique IDs']=len(applied)
 for grp,v in d.items():tot['dec_'+grp].update(v)
 files[f.name]={'date':datetime.fromtimestamp(f.stat().st_mtime).strftime('%F'),**{k:dict(v.most_common()) for k,v in d.items()}}
for f in sorted(base.glob('*/*/events.jsonl')):
 for e in rows(f):
  k=e['kind']; d=e['d']; inc('events',k)
  ck.append(datetime.fromtimestamp(e['t'],timezone(timedelta(hours=9))).date())
  if k=='plan_result':
   for key in ('result','trigger','decision'):inc('upper_'+key,d.get(key,''))
   inc('upper_resulttrigger', str(d.get('result'))+' '+str(d.get('trigger')))
   if d.get('result')=='accepted':inc('upper_accepted_trigger',d.get('trigger'));inc('upper_accepted_decision',d.get('decision'));inc('upper_accepted_goal',str(d.get('goal','')).split(' ')[0])
   if 'recall' in json.dumps(d,ensure_ascii=False).lower():inc('ev_recall_mention','present')
for f in sorted(base.glob('*/*/goals.jsonl')):
 for g in rows(f):
  for key in ('kind','outcome','source'):inc('goal_'+key,g.get(key))
  inc('goal_class',g['cls'] or '(empty)');inc('goal_by_outcome',g['kind']+' '+g['outcome'])
  if g['outcome']=='blocked':
   inc('blocked_kind',g['kind']);inc('blocked_causes',g['cls'] or '(empty)');inc('blocked_key',g['key'])
   samples.setdefault(g['cls'],[f.relative_to(base).as_posix(),g['run'],g['cause']])
for x in rows(bridge):
 if 'member' in x and 'trigger' in x:
  inc('bridge_world',x.get('world'));inc('bridge_date',x['t'][:10]);inc('bridge_fields','rounds' if 'rounds' in x else 'n/a')
  if 'rounds' in x:inc('bridge_rounds',x['rounds'])
  if x.get('prefetch_used'):inc('bridge_prefetch','yes')
data={'events_kst':[str(min(ck)),str(max(ck))],'files':files,'total':{k:dict(v.most_common()) for k,v in tot.items()},'blocked_examples':samples}
(out/'counts.json').write_text(json.dumps(data,ensure_ascii=False,indent=1),encoding='utf8')
for k,v in tot.items():
 print(k,v.most_common(16))
print('days',data['events_kst']);print('files',[(f,d.get('stages',{}).get('ANSWER',0)) for f,d in files.items()])

def agg(fs,grp):
 c=Counter()
 for f in fs:c.update(files[f].get(grp,{}))
 return c
variants={}
for name,group in [('all',list(files)),('exceptLargest',[f for f in files if f!='decisions-211545.log']),('exceptLargestTwo',[f for f in files if f not in ('decisions-194122.log','decisions-211545.log')])]+[(day,[f for f in files if files[f]['date']==day]) for day in sorted(set(d['date'] for d in files.values()))]:
 a=agg(group,'action'); s=agg(group,'kind'); v=agg(group,'visibleEnemy')
 variants[name]={'answers':sum(s.values()),'continue':s['continue'],'awayRetreatAttack':a['away']+a['retreat']+a['attack'],'visibleEnemiesZero':v['zero'],'choiceKinds':dict(s.most_common(8)),'topAction':dict(a.most_common(8))}
print('sensitivity',json.dumps(variants,ensure_ascii=False))
print('blocked',samples)
print('sources',[(str(f.relative_to(base)),sum(1 for _ in rows(f))) for f in base.glob('*/*/events.jsonl')])
hosts=[]; planRows=[]
for f in sorted(base.glob('*/*/events.jsonl')):
 for e in rows(f):
  if e['kind']=='order' and len(hosts)<20:hosts.append(e)
  if e['kind']=='plan_result':planRows.append(e)
bridgeCheck=Counter()
for b in rows(bridge):
 if 'rounds' not in b:continue
 matches=[e for e in planRows if e['world']==b.get('world') and e['char']==b['member'] and e['d']['report']==b['report_id'] and e['d']['trigger']==b['trigger'] and abs(e['t']-datetime.fromisoformat(b['t']).timestamp())<=8]
 bridgeCheck['match' if matches else 'noMatch']+=1
print('bridgeMatch8s',bridgeCheck)
print('ORDERS',json.dumps(hosts[:8],ensure_ascii=False)[:2600])
data['sensitivity']=variants;data['bridgeMatch8s']=dict(bridgeCheck)
(out/'counts.json').write_text(json.dumps(data,ensure_ascii=False,indent=1),encoding='utf8')

