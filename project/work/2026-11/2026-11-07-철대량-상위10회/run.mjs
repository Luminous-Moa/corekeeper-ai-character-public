import fs from 'node:fs'; import path from 'node:path'; import net from 'node:net'; import {spawn} from 'node:child_process'; import {fileURLToPath} from 'node:url';
const root=path.dirname(fileURLToPath(import.meta.url)), stage=path.join(root,'isolated-bridge'), src='C:/Dev/CoreKeeperModSDK/Tools/ai-bridge';
fs.mkdirSync(path.join(root,'knowledge'),{recursive:true});for(const name of fs.readdirSync(path.resolve(src,'../knowledge')).filter(x=>x.endsWith('.mjs')))fs.copyFileSync(path.resolve(src,'../knowledge',name),path.join(root,'knowledge',name));
fs.mkdirSync(stage,{recursive:true});for (const name of fs.readdirSync(src).filter(x=>x.endsWith('.mjs')||x==='brief.md'))fs.copyFileSync(path.join(src,name),path.join(stage,name));
fs.appendFileSync(path.join(stage,'brief.md'),'\n원정 계획의 맥락: 지속되는 생활 방향과 현재 프로젝트 아래 이번 작업의 목적, 현장 판단에 맡길 범위, 귀환 및 다음 작업의 조건을 함께 고려한다. 현장 모델이 구체 행동과 대상 묶음을 고른다. 작은 행동이 끝날 때마다 새 프로젝트를 만들지 않는다.\n');
const sock=net.createServer();await new Promise(resolve=>sock.listen(0,'127.0.0.1',resolve));const port=sock.address().port; await new Promise(resolve=>sock.close(resolve));
const env={...process.env,AI_BRIDGE_MEMBERS:'gpt',AI_BRIDGE_PORT:String(port),AI_BRIDGE_LOGS:path.join(stage,'logs'),AI_BRIDGE_CONFIG:path.join(stage,'config.json')};delete env.AI_BRIDGE_FAKE_CHAT;
fs.writeFileSync(env.AI_BRIDGE_CONFIG,JSON.stringify({recall:false,memoryIndex:false}));
const bridge=spawn(process.execPath,[path.join(stage,'bridge.mjs')],{cwd:stage,env,windowsHide:true,stdio:['ignore','pipe','pipe']});
bridge.stdout.on('data',b=>process.stdout.write(b));bridge.stderr.on('data',b=>process.stderr.write(b));
try {
const ready=await new Promise((resolve,reject)=>{let buf='';const t=setTimeout(()=>reject(Error('startup timeout')),110000);bridge.stdout.on('data',b=>{buf+=b;if(buf.includes('ai-bridge listening')){clearTimeout(t);resolve(true)}});bridge.on('exit',c=>{clearTimeout(t);reject(Error('bridge exited '+c))});});
const url='http://127.0.0.1:'+port+'/v1/', h=await (await fetch(url+'health')).json();const m=h.members.gpt;if(!m.found||!m.login||m.skip)throw Error('gpt preflight: '+JSON.stringify(m));
const b=JSON.parse(fs.readFileSync(path.join(root,'report.json'),'utf8'));
for(let i=1;i<=10;i++){const r=await fetch(url+'plan',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(b)});const a=await r.json();fs.writeFileSync(path.join(root,'response-'+i+'.json'),JSON.stringify(a,null,2));console.log('RUN',i,r.status,a.ok,JSON.stringify(a.answer??a.error));if(!a.ok)throw Error('check failed attempt '+i+': '+JSON.stringify(a).slice(0,900));}
}finally{bridge.kill();}
