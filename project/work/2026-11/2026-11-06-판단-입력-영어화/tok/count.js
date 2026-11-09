const {getEncoding}=require('js-tiktoken');const e=getEncoding('o200k_base');const T=s=>e.encode(s).length;
const KI="목표와 지금 상황에서 {N}가 다음에 할 행동 하나를 고르시오. 각 선택지는 지금 실제로 시작할 수 있는 행동이다. 지금 하는 행동(계속하기)이 여전히 알맞으면 그것을 이어 가고, 새로 생긴 사실이 있을 때 바꾼다(바꾸면 새 행동은 처음부터 시작한다).";
let tk=0,te=0;
for(const k of ['S1','S2','S3','S4']){const ko=require('./samples/'+k+'.ko.json'),en=require('./samples/'+k+'.en.json');
 const n=ko.who;const ki=T(KI.replace('{N}',n)),ei=T(en.instructions.replace('{N}',n));
 const kin=T(ko.input),ein=T(en.input);const kd=ko.opts.reduce((a,o)=>a+T(o.desc)+T(o.value),0),ed=en.opts.reduce((a,o)=>a+T(o.desc)+T(o.value),0);
 const ov=Math.round(93+2.5*ko.opts.length);const K=kin+ki+kd+ov,E=ein+ei+ed+ov;tk+=K;te+=E;
 const kc=ko.input.length+ko.opts.reduce((a,o)=>a+o.desc.length,0),ec=en.input.length+en.opts.reduce((a,o)=>a+o.desc.length,0);
 console.log(k,'opts',ko.opts.length,'api',ko.tokens,'| ko input',kin,'instr',ki,'desc',kd,'tot~',K,'| en input',ein,'instr',ei,'desc',ed,'tot~',E,'| ratio',(E/K).toFixed(3),'content-only',((ein+ei+ed)/(kin+ki+kd)).toFixed(3),'| chars ko',kc,'en',ec);}
console.log('sum ratio',(te/tk).toFixed(3));
