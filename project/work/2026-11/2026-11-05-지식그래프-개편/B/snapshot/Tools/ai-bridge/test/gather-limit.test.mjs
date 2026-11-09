import assert from 'node:assert/strict';
import { shape } from '../protocol.mjs';
const goal = (max) => ({do:'gather',item:'CopperOre',count:9,...(max===undefined?{}:{max_count:max})});
for (const max of [undefined,null,10,2147483647]) {
  const answer={decision:'set',goal:goal(max)};
  assert.equal(shape(answer),null);
  if(max!==undefined) assert.equal(answer.goal.max_count,max);
}
for (const max of [-1,0,1.5,'10',true,{},2147483648,8]) assert.ok(shape({decision:'set',goal:goal(max)}));
assert.ok(shape({decision:'set',goal:{do:'craft',item:'CopperOre',count:1,max_count:10}}));
assert.equal(shape({decision:'set',plan:[goal(10),{do:'gather',item:'Wood',count:1000}]}),null);
console.log('PASS 14 explicit gather ceiling protocol cases');
for(const id of [1,17,2147483647]) assert.equal(shape({decision:'set',goal:{...goal(10),max_order:id}}),null);
for(const id of [0,-1,1.5,'17',true,{},2147483648]) assert.ok(shape({decision:'set',goal:{...goal(10),max_order:id}}));
assert.ok(shape({decision:'set',goal:{...goal(undefined),max_order:17}}));
console.log('PASS 11 explicit ceiling owner protocol cases');
