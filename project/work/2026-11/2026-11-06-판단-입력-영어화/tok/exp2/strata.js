const r=require('../recs.json');
const goalKind=x=>{const m=x.input.match(/\n목표: (.*?)(?: — 지금 [-\d]+\/\d+)?\.\n/);const g=m?m[1]:'?';
 if(g=='없음')return 'None';if(/모으기$/.test(g))return 'Gather';if(/만들기$/.test(g))return 'Craft';if(g=='탐험')return 'Explore';if(g=='호스트 따라가기')return 'Follow';
 if(/^자유 시간/.test(g))return 'Free';if(/보관$/.test(g))return 'Store';if(g=='기지로 돌아가기')return 'Home';if(/^밭/.test(g))return 'Farm';if(/^요리/.test(g))return 'Cook';return 'other:'+g;};
const cls=x=>x.brute&&/적 관측\(서버 상태/.test(x.input)?'brute':x.combat?'combat':'calm';
const ob=n=>n<=5?'2-5':n<=9?'6-9':n<=14?'10-14':'15+';
module.exports={goalKind,cls,ob};
if(require.main===module){const t={};for(const x of r){const k=cls(x)+' '+goalKind(x);t[k]=(t[k]||0)+1}
console.log(Object.entries(t).sort((a,b)=>b[1]-a[1]));
const o={};for(const x of r){const k=cls(x)+' '+ob(x.nopts);o[k]=(o[k]||0)+1};console.log(o);
console.log('has probs',r.filter(x=>x.probs).length);}
