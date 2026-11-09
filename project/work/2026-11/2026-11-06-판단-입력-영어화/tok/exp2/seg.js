// segmentation + masking shared by template extraction and translation
const H=/[가-힣]/;
function split(text){ // split at depth-0 ". ", " — ", " / " keeping separators as their own pieces
 const out=[];let depth=0,cur='';
 for(let i=0;i<text.length;i++){const ch=text[i];
  if(ch=='('||ch=='[')depth++;else if(ch==')'||ch==']')depth=Math.max(0,depth-1);
  if(depth==0){
   if(ch=='.'&&text[i+1]==' '){cur+='.';out.push(cur);out.push({sep:' '});cur='';i++;continue;}
   if(text.startsWith(' — ',i)){out.push(cur);out.push({sep:' — '});cur='';i+=2;continue;}
   if(text.startsWith(' / ',i)){out.push(cur);out.push({sep:' / '});cur='';i+=2;continue;}
  }
  cur+=ch;}
 out.push(cur);return out;}
const TOK=/[-+]?[A-Za-z0-9_][A-Za-z0-9_:.,\-+]*[A-Za-z0-9_%]|[-+]?[A-Za-z0-9_]/g;
function mask(seg){const vals=[];const key=seg.replace(TOK,m=>{vals.push(m);return '{}'});return {key,vals};}
module.exports={split,mask,H};
