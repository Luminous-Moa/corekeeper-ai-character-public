import fs from 'node:fs';import p from 'node:path';import c from 'node:crypto';
const walk=d=>fs.readdirSync(d,{withFileTypes:true}).flatMap(x=>x.isDirectory()?walk(p.join(d,x.name)):[p.join(d,x.name)]);
const h=f=>c.createHash('sha256').update(fs.readFileSync(f)).digest('hex');
for(const [name,target]of [['localsteam','C:/Users/<USER>/AppData/LocalLow/Pugstorm/Core Keeper/Steam/435751233'],['remote','C:/Program Files (x86)/Steam/userdata/435751233/1621690/remote']]){
 const base='C:/Dev/_scratch/refactor-maint-final-install-20261106/backup/'+name,files=walk(base);
 console.log(JSON.stringify({name,count:files.length,changed:files.map(f=>p.relative(base,f)).filter(f=>!fs.existsSync(p.join(target,f))||h(p.join(base,f))!==h(p.join(target,f))),extra:walk(target).map(f=>p.relative(target,f)).filter(f=>!fs.existsSync(p.join(base,f)))}));
}
