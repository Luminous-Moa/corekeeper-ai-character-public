import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const dir=path.dirname(fileURLToPath(import.meta.url));
const lines=fs.readFileSync(path.join(dir,'Player-swarm4.snapshot.log'),'utf8').split(/\r?\n/);
const mode=process.argv[2];
for(let i=0;i<lines.length;i++){
const s=lines[i],t=Number(s.match(/t=([\d.]+)/)?.[1]);
if(mode==='full'&&t>=640&&t<=723&&s.includes(' FULL input=')){
 console.log('\nLINE '+(i+1)+' '+s.slice(0,85));
 const [input,...options]=s.split(' || ');
 console.log(input.match(/내 상태:.*?보이는 범위/)?.[0]);
 console.log(input.match(/지금 하는 일:.*?최근 10초/)?.[0]);
 console.log(input.match(/전투 상황:.*?탐색 기억/)?.[0]);
 for(const option of options)console.log(option);
 for(let j=i+1;j<i+12;j++)if(lines[j]?.includes(' RESULT '))console.log('RESULT '+(j+1)+' '+lines[j]);
}
if(mode==='range'&&t>=Number(process.argv[3])&&t<=Number(process.argv[4]))console.log(`${i+1}: ${s}`);
}
