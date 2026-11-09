// Continue-option wording variants for the close fight (only the warned continue, except V5). Facts from the original only.
const RE=/^계속하기 — (.*?)\. ((?:지금 동작: [^.]*\. )?)(.*?)바꾸는 비용: 다른 행동을 고르면 지금 동작이 끝나는 대로 이 일은 멈추고, 지금까지의 진행은 이어지지 않으며 새 행동은 처음부터\(다가가기·무기 들기·길 찾기부터\) 시작함\. 남은 시간: 이 행동은 약 ([\d.]+)초 뒤 끝남\(이 판단의 답은 약 ([\d.]+)초 뒤 옴\)( — 답이 오기 전에 끝남: 이 '계속하기'를 고르면 그 답은 쓰이지 않고 다음 답까지 서 있음)?\.(.*)$/;
const COST='바꾸는 비용: 다른 행동을 고르면 지금 동작이 끝나는 대로 이 일은 멈추고, 지금까지의 진행은 이어지지 않으며 새 행동은 처음부터(다가가기·무기 들기·길 찾기부터) 시작함.';
const WARN="이 '계속하기'를 고르면 그 답은 쓰이지 않고 다음 답까지 서 있음";
const V={
 // V1: the warning (with its two times) right after the head; everything else as it was
 V1:(m,w)=>!w?null:`계속하기 — 답이 오기 전에 끝남: 이 행동은 약 ${m[4]}초 뒤 끝나고 이 판단의 답은 약 ${m[5]}초 뒤 옴. ${WARN}. ${m[1]}. ${m[2]}${m[3]}${COST}${m[7]}`,
 // V2: the warning where it was; the switching-cost sentence (which the warning makes moot) left out
 V2:(m,w)=>!w?null:`계속하기 — ${m[1]}. ${m[2]}${m[3]}남은 시간: 이 행동은 약 ${m[4]}초 뒤 끝남(이 판단의 답은 약 ${m[5]}초 뒤 옴)${m[6]}.${m[7]}`,
 // V3: V1 + V2
 V3:(m,w)=>!w?null:`계속하기 — 답이 오기 전에 끝남: 이 행동은 약 ${m[4]}초 뒤 끝나고 이 판단의 답은 약 ${m[5]}초 뒤 옴. ${WARN}. ${m[1]}. ${m[2]}${m[3]}`.trimEnd()+m[7],
 // V4: short and plain: the head says it, one sentence of fact, no switching cost
 V4:(m,w)=>!w?null:`계속하기(답보다 먼저 끝남) — 이 행동은 약 ${m[4]}초 뒤 끝나고, 이 판단의 답은 그보다 늦은 약 ${m[5]}초 뒤 옴: 고르면 이 답은 쓰이지 않고 다음 답이 올 때까지 서 있음. ${m[1]}. ${m[2]}${m[3]}`.trimEnd()+m[7],
 // V5 (side-effect probe, also changes the unwarned continue): the remaining-time sentence always right after the head; warned: no switching cost
 V5:(m,w)=>`계속하기 — 남은 시간: 이 행동은 약 ${m[4]}초 뒤 끝남(이 판단의 답은 약 ${m[5]}초 뒤 옴)${m[6]||''}. ${m[1]}. ${m[2]}${m[3]}${w?'':COST}`.trimEnd()+m[7],
};
function apply(x,v){const opts=x.opts.map(o=>{if(!o.desc.startsWith('계속하기'))return o;const m=o.desc.match(RE);if(!m)return o;const w=!!m[6];const d=V[v](m,w);return d==null?o:{...o,desc:d.replace(/  +/g,' ')}});return opts;}

// V6: V3, and the head no longer says "이어 하기" (nothing is left to carry on when the answer comes): the running action is
// named as the one that ends before the answer, and the plain consequence of the warning is said once.
V.V6=(m,w)=>{if(!w)return null;const head=m[1].replace(/ 이어 하기( \(|$)/,' — 이 답이 오기 전에 끝남$1');
 return `계속하기 — 답이 오기 전에 끝남: 이 행동은 약 ${m[4]}초 뒤 끝나고 이 판단의 답은 약 ${m[5]}초 뒤 옴. 그래서 이 답이 올 때는 이어 갈 행동이 남아 있지 않음: ${WARN}. ${head}. ${m[2]}${m[3]}`.trimEnd()+m[7];};
// V7: V6, and the forecast is labelled as what happens when the action ends (it ends before this answer either way)
V.V7=(m,w)=>{const d=V.V6(m,w);return d==null?null:d.replace(' 계속하면: ',' 이 행동이 끝날 때(이 답이 오기 전): ');};
// V8: V7 with the consequence first in the head (what choosing it does), then the times
V.V8=(m,w)=>{const d=V.V7(m,w);if(d==null)return null;
 const old=`계속하기 — 답이 오기 전에 끝남: 이 행동은 약 ${m[4]}초 뒤 끝나고 이 판단의 답은 약 ${m[5]}초 뒤 옴. 그래서 이 답이 올 때는 이어 갈 행동이 남아 있지 않음: ${WARN}.`;
 const neu=`계속하기 — 고르면 다음 답까지 서 있음: 이 행동은 약 ${m[4]}초 뒤, 이 판단의 답(약 ${m[5]}초 뒤)이 오기 전에 끝나므로 이 답이 올 때 이어 갈 행동이 남아 있지 않고, 이 '계속하기' 답은 쓰이지 않음.`;
 if(!d.startsWith(old))throw new Error('V8 head');return neu+d.slice(old.length);};
// V9: V7, and the warning says that the instruction's "keep the current action if it still fits" does not apply (nothing is left to keep)
V.V9=(m,w)=>{const d=V.V7(m,w);if(d==null)return null;const a="다음 답까지 서 있음. ";const i=d.indexOf(a);return d.slice(0,i+a.length)+"이어 갈 행동이 없으므로 '지금 하는 행동이 여전히 알맞으면 이어 간다'는 이 선택지에 해당하지 않음. "+d.slice(i+a.length);};
// V10: V9, and the standing is put next to the hold option it equals in effect (both: stand, no attack, until the next answer)
V.V10=(m,w)=>{const d=V.V9(m,w);if(d==null)return null;const a="이 선택지에 해당하지 않음. ";const i=d.indexOf(a);return d.slice(0,i+a.length)+"그동안 서 있기만 하므로 결과는 '제자리 유지'처럼 공격 없이 다음 판단까지 서 있는 것과 같음. "+d.slice(i+a.length);};
// V11: V9 without V7's relabel of the forecast ("계속하면:" stays): fewer changes
V.V11=(m,w)=>{const d=V.V6(m,w);if(d==null)return null;const a="다음 답까지 서 있음. ";const i=d.indexOf(a);return d.slice(0,i+a.length)+"이어 갈 행동이 없으므로 '지금 하는 행동이 여전히 알맞으면 이어 간다'는 이 선택지에 해당하지 않음. "+d.slice(i+a.length);};
// diagnostic only (not a description change): the warned continue's value renamed (the step key told the model 'do that step')
V.VX=(m,w)=>null; V.VX7=(m,w)=>null;
function apply2(x,v){if(v!='VX'&&v!='VX7')return null;const base=v=='VX7'?apply(x,'V7'):x.opts;return base.map(o=>{if(!o.desc.startsWith('계속하기'))return o;const mm=o.desc.match(RE)||(x.opts.find(q=>q.value==o.value)||{desc:''}).desc.match(RE);if(!mm||!mm[6])return o;return {...o,value:'continue_running'}});}
module.exports={apply:(x,v)=>apply2(x,v)||apply(x,v),RE,V};
